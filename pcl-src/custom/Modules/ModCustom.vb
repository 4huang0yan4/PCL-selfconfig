'PCL 自配版（第三方分支，基于上游开源版 2.99.0.0）：
'本文件在开源版基础上补充的功能全部为本地实现，不依赖官方服务器。
'  1. 上游版本提示（查询 GitHub tag，仅提示，不自动更新）
'  2. 百宝箱：内存优化、清理游戏垃圾

Imports System.Diagnostics
Imports System.IO
Imports System.Runtime.InteropServices
Imports System.Windows.Threading
Imports System.Linq

Friend Module ModCustom

#Region "上游版本提示"

    Private Const UpstreamRepo As String = "Meloong-Git/PCL"
    Private Const UpstreamTagPage As String = "https://github.com/Meloong-Git/PCL/tags"
    Private UpstreamChecking As Boolean = False

    Private ReadOnly Property UpstreamTagCache As String
        Get
            Return PathTemp & "Cache\UpstreamTag.cfg"
        End Get
    End Property
    Private ReadOnly Property UpstreamNoticeCache As String
        Get
            Return PathTemp & "Cache\UpstreamTagNoticed.cfg"
        End Get
    End Property

    ''' <summary>
    ''' 取上游最新的 tag 版本号，失败返回 Nothing。
    ''' </summary>
    Friend Function FetchLatestUpstreamTag() As String
        Dim Urls As String() = {
            $"https://api.github.com/repos/{UpstreamRepo}/tags?per_page=100",
            $"https://gh-proxy.com/https://api.github.com/repos/{UpstreamRepo}/tags?per_page=100",
            $"https://ghfast.top/https://api.github.com/repos/{UpstreamRepo}/tags?per_page=100"
        }
        Dim Raw As String = NetRequestByLoader(Urls, Timeout:=12000, IsJson:=True)
        Dim Arr As JArray = JArray.Parse(Raw)
        Dim Best As String = Nothing
        For Each Item As JToken In Arr
            Dim Name As String = Item("name")?.ToString
            If String.IsNullOrWhiteSpace(Name) Then Continue For
            If Best Is Nothing OrElse CompareVersion(Name, Best) > 0 Then Best = Name
        Next
        Return Best
    End Function

    ''' <summary>
    ''' 比较两个版本号（形如 2.14.0.0），A 大于 B 返回正数。
    ''' </summary>
    Friend Function CompareVersion(A As String, B As String) As Integer
        Dim PA As Integer() = ParseVersion(A)
        Dim PB As Integer() = ParseVersion(B)
        Dim Count As Integer = Math.Max(PA.Length, PB.Length)
        For i = 0 To Count - 1
            Dim VA As Integer = If(i < PA.Length, PA(i), 0)
            Dim VB As Integer = If(i < PB.Length, PB(i), 0)
            If VA <> VB Then Return VA.CompareTo(VB)
        Next
        Return 0
    End Function
    Private Function ParseVersion(S As String) As Integer()
        Dim Result As New List(Of Integer)
        If String.IsNullOrWhiteSpace(S) Then Return Result.ToArray()
        For Each Part In S.Trim().TrimStart("v"c, "V"c).Split("."c, "-"c, "+"c, " "c)
            Dim N As Integer
            If Integer.TryParse(Part, N) Then
                Result.Add(N)
            Else
                Exit For
            End If
        Next
        Return Result.ToArray()
    End Function

    ''' <summary>
    ''' 点「检查更新」按钮时调用。
    ''' </summary>
    Friend Sub UpdateCheckImpl()
        If Settings.Get(Of Boolean)("SystemSelfUpdate") Then
            UpdateSelf.CheckByButton()
            Return
        End If
        If UpstreamChecking Then
            Hint("正在检查，请稍候……", HintType.Blue, False)
            Return
        End If
        UpstreamChecking = True
        Hint("正在检查上游版本……", HintType.Blue, False)
        RunInNewThread(
        Sub()
            Try
                Dim Latest As String = FetchLatestUpstreamTag()
                If String.IsNullOrWhiteSpace(Latest) Then Throw New Exception("没有取到任何版本标签")
                FileUtils.Write(UpstreamTagCache, Latest)
                If CompareVersion(Latest, VersionBaseName) > 0 Then
                    RunInUi(
                    Sub()
                        If MyMsgBox($"发现上游新版本：{Latest}{vbCrLf}当前版本：{VersionBaseName}（第三方分支）{vbCrLf}{vbCrLf}本分支不提供自动更新，需要你手动同步源码后重新编译。{vbCrLf}是否打开上游页面查看？",
                                    "检查更新", "打开页面", "取消") = 1 Then OpenWebsite(UpstreamTagPage)
                    End Sub)
                Else
                    RunInUi(Sub() MyMsgBox($"当前已是最新的上游版本（{Latest}）。{vbCrLf}本分支为本地自用版，不提供自动更新。", "检查更新"))
                End If
            Catch ex As Exception
                Logger.Error(ex, "检查上游更新失败")
                RunInUi(Sub() Hint("检查更新失败，无法连接 GitHub：" & ex.Message, HintType.Red, False))
            Finally
                UpstreamChecking = False
            End Try
        End Sub, "Check Upstream")
    End Sub

    ''' <summary>
    ''' 启动时静默检查一次：同一个版本只提示一次。
    ''' </summary>
    Friend Sub UpdateCheckBackground()
        Try
            If Settings.Get(Of Boolean)("SystemSelfUpdate") Then
                UpdateSelf.CheckBackground()
                Return
            End If
            Logger.Info("开始检查上游版本")
            Dim Latest As String = FetchLatestUpstreamTag()
            If String.IsNullOrWhiteSpace(Latest) Then Return
            FileUtils.Write(UpstreamTagCache, Latest)
            If CompareVersion(Latest, VersionBaseName) <= 0 Then Return
            Dim Noticed As String = If(FileUtils.Exists(UpstreamNoticeCache), FileUtils.ReadAsString(UpstreamNoticeCache), "")
            If Noticed.Trim() = Latest Then Return
            FileUtils.Write(UpstreamNoticeCache, Latest)
            RunInUi(
            Sub()
                If MyMsgBox($"PCL 上游发布了新版本：{Latest}{vbCrLf}当前版本：{VersionBaseName}（第三方分支）{vbCrLf}{vbCrLf}是否需要打开上游页面查看？", "发现新版本", "打开页面", "以后再说") = 1 Then OpenWebsite(UpstreamTagPage)
            End Sub)
        Catch ex As Exception
            Logger.Warn(ex, "后台检查上游版本失败")
        End Try
    End Sub

#End Region

#Region "百宝箱"

    <DllImport("psapi.dll", SetLastError:=True)>
    Private Function EmptyWorkingSet(hProcess As IntPtr) As Boolean
    End Function

    ''' <summary>
    ''' 内存优化：把各进程的工作集换出到页面文件，降低物理内存占用。
    ''' </summary>
    Friend Sub MemoryOptimizeImpl(ShowHint As Boolean)
        Try
            Dim Self As Process = Process.GetCurrentProcess()
            Dim Count As Integer = 0
            For Each Proc As Process In Process.GetProcesses()
                Try
                    If Proc.Id <= 4 Then Continue For
                    If Proc.Id = Self.Id Then Continue For
                    If Proc.ProcessName = "java" OrElse Proc.ProcessName = "javaw" Then Continue For '正在启动的游戏不要动
                    If EmptyWorkingSet(Proc.Handle) Then Count += 1
                Catch
                    '无权访问的进程直接跳过
                Finally
                    Proc.Dispose()
                End Try
            Next
            EmptyWorkingSet(Self.Handle)
            GC.Collect()
            GC.WaitForPendingFinalizers()
            GC.Collect()
            If ShowHint Then Hint($"已优化 {Count} 个进程的内存占用，并回收了启动器自身的内存", HintType.Green, False)
        Catch ex As Exception
            Logger.Error(ex, "内存优化失败")
            If ShowHint Then Hint("内存优化失败：" & ex.Message, HintType.Red, False)
        End Try
    End Sub

    ''' <summary>
    ''' 清理游戏垃圾：只清日志、崩溃报告与 Mixin 输出，不碰存档、模组、配置。
    ''' </summary>
    ''' <summary>统计各 Minecraft 文件夹与启动器数据目录的占用，结果弹窗显示。只读，不改任何文件。</summary>
    Friend Sub SpaceAnalyzeImpl()
        Hint("正在统计磁盘占用，大文件夹可能要几十秒…")
        RunInNewThread(
        Sub()
            Try
                Dim Report As String = BuildSpaceReport()
                RunInUi(Sub() MyMsgBox(Report, "空间分析", "关闭"))
            Catch ex As Exception
                Logger.Error(ex, "空间分析失败")
                RunInUi(Sub() Hint("统计失败：" & ex.Message, HintType.Red, False))
            End Try
        End Sub, "Space Analyze")
    End Sub

    Private Function BuildSpaceReport() As String
        Dim Sb As New System.Text.StringBuilder
        Dim GrandTotal As Long = 0
        Sb.AppendLine("各 Minecraft 文件夹（列出占空间最多的 6 项）")
        Dim HasFolder As Boolean = False
        For Each Folder In McFolderList
            If Folder Is Nothing OrElse String.IsNullOrWhiteSpace(Folder.Location) Then Continue For
            Dim Root As String = Folder.Location
            If Not DirectoryUtils.Exists(Root) Then Continue For
            HasFolder = True
            Dim Items As New List(Of KeyValuePair(Of String, Long))
            Try
                For Each ChildDir In IO.Directory.GetDirectories(Root)
                    Items.Add(New KeyValuePair(Of String, Long)(IO.Path.GetFileName(ChildDir), GetDirSize(ChildDir)))
                Next
            Catch ex As Exception
                Logger.Warn(ex, "枚举 Minecraft 文件夹失败：" & Root)
            End Try
            '根目录下的散装文件（options.txt、日志等）
            Dim LooseSize As Long = 0
            Try
                For Each F As IO.FileInfo In New IO.DirectoryInfo(Root).EnumerateFiles()
                    LooseSize += F.Length
                Next
            Catch ex As Exception
                Logger.Warn(ex, "统计根目录文件失败：" & Root)
            End Try
            If LooseSize > 0 Then Items.Add(New KeyValuePair(Of String, Long)("（散装文件）", LooseSize))

            Dim FolderTotal As Long = Items.Sum(Function(Pair) Pair.Value)
            GrandTotal += FolderTotal
            Dim DisplayName As String = If(String.IsNullOrWhiteSpace(Folder.Name),
                                           IO.Path.GetFileName(Root.TrimEnd("\"c)), Folder.Name)
            Sb.AppendLine()
            Sb.AppendLine($"  {DisplayName}  —  共 {FormatSizeText(FolderTotal)}")
            For Each Pair In Items.OrderByDescending(Function(P) P.Value).Take(6)
                If Pair.Value = 0 Then Continue For
                Sb.AppendLine($"      {Pair.Key.PadRight(18)}{FormatSizeText(Pair.Value)}")
            Next
            If Items.Count > 6 Then Sb.AppendLine($"      …另有 {Items.Count - 6} 项未列出")
        Next
        If Not HasFolder Then Sb.AppendLine("  （还没有添加任何 Minecraft 文件夹）")

        '启动器自身的数据目录（配置、日志、陶瓦组件等）；不含 .minecraft，避免与上面重复统计
        Dim LauncherData As String = IO.Path.Combine(Paths.Base, "PCL")
        If DirectoryUtils.Exists(LauncherData) Then
            Dim DataSize As Long = GetDirSize(LauncherData)
            GrandTotal += DataSize
            Sb.AppendLine()
            Sb.AppendLine($"  启动器数据目录（PCL 文件夹）  —  共 {FormatSizeText(DataSize)}")
        End If

        Sb.AppendLine()
        Sb.AppendLine($"合计 {FormatSizeText(GrandTotal)}")
        Sb.AppendLine()
        Sb.AppendLine("日志与崩溃报告可以在「清理游戏垃圾」里删掉；其余文件删除会影响游戏，请自行判断。")
        Return Sb.ToString.TrimEnd
    End Function

    ''' <summary>递归算文件夹大小；单个文件或子目录无权限时跳过，不中断整体统计。</summary>
    Friend Function GetDirSize(FolderPath As String) As Long
        If Not DirectoryUtils.Exists(FolderPath) Then Return 0
        Dim Total As Long = 0
        Try
            For Each F As IO.FileInfo In New IO.DirectoryInfo(FolderPath).EnumerateFiles("*", IO.SearchOption.AllDirectories)
                Total += F.Length
            Next
        Catch ex As Exception
            Logger.Warn(ex, "统计文件夹大小失败：" & FolderPath)
        End Try
        Return Total
    End Function

    Friend Function FormatSizeText(Bytes As Long) As String
        If Bytes >= 1024L * 1024 * 1024 Then Return (Bytes / 1024 / 1024 / 1024).ToString("0.00") & " GB"
        If Bytes >= 1024L * 1024 Then Return (Bytes / 1024 / 1024).ToString("0.0") & " MB"
        If Bytes >= 1024 Then Return (Bytes / 1024).ToString("0.0") & " KB"
        Return Bytes & " B"
    End Function

    Friend Sub RubbishClearImpl()
        Try
            If McInstanceSelected Is Nothing Then
                Hint("请先在「版本选择」里选一个游戏版本！", HintType.Red, False)
                Return
            End If
            Dim Indie As String = McInstanceSelected.PathIndie
            Dim Folders As New List(Of String) From {
                Path.Combine(Indie, "logs"),
                Path.Combine(Indie, "crash-reports"),
                Path.Combine(Indie, ".mixin.out")
            }
            '开了版本隔离时，版本目录下可能还有一份
            Dim VersionPath As String = McInstanceSelected.PathVersion
            If Not String.IsNullOrWhiteSpace(VersionPath) AndAlso Not VersionPath.TrimEnd("\"c).Equals(Indie.TrimEnd("\"c), StringComparison.OrdinalIgnoreCase) Then
                Folders.Add(Path.Combine(VersionPath, "logs"))
                Folders.Add(Path.Combine(VersionPath, "crash-reports"))
            End If
            Dim Files As New List(Of String)
            Dim Size As Long = 0
            For Each Folder In Folders
                If Not Directory.Exists(Folder) Then Continue For
                Dim Found As String()
                Try
                    Found = Directory.GetFiles(Folder, "*", SearchOption.AllDirectories)
                Catch ex As Exception
                    Logger.Warn(ex, $"枚举 {Folder} 失败")
                    Continue For
                End Try
                For Each F In Found
                    Files.Add(F)
                    Try
                        Size += New FileInfo(F).Length
                    Catch
                        '拿不到大小就按 0 算
                    End Try
                Next
            Next
            If Files.Count = 0 Then
                Hint("没有可清理的垃圾文件，已经很干净了！", HintType.Green, False)
                Return
            End If
            Dim SizeText As String = If(Size >= 1024 * 1024, (Size / 1024 / 1024).ToString("0.0") & " MB", (Size / 1024.0).ToString("0.0") & " KB")
            If MyMsgBox($"将删除 {Files.Count} 个文件，共 {SizeText}：{vbCrLf}" &
                        "- logs 里的游戏日志" & vbCrLf &
                        "- crash-reports 里的崩溃报告" & vbCrLf &
                        "- .mixin.out 里的 Mixin 输出" & vbCrLf & vbCrLf &
                        "不会动存档、模组和配置文件。要继续吗？", "清理游戏垃圾", "清理", "取消") <> 1 Then Return
            Dim Failed As Integer = 0
            For Each F In Files
                Try
                    File.Delete(F)
                Catch ex As Exception
                    Failed += 1
                    Logger.Warn(ex, $"删除 {F} 失败")
                End Try
            Next
            If Failed = 0 Then
                Hint($"已清理 {Files.Count} 个文件，释放 {SizeText}", HintType.Green, False)
            Else
                Hint($"已清理 {Files.Count - Failed} 个文件，{Failed} 个被占用未能删除", HintType.Red, False)
            End If
        Catch ex As Exception
            Logger.Error(ex, "清理游戏垃圾失败")
            Hint("清理失败：" & ex.Message, HintType.Red, False)
        End Try
    End Sub

    ''' <summary>
    ''' 打开当前版本的某个文件夹（游戏目录 / logs / crash-reports / saves / mods）。
    ''' 文件夹不存在时就地建出来，省得用户自己一层层点。
    ''' </summary>
    Friend Sub OpenInstanceFolderImpl(Kind As String)
        Try
            If McInstanceSelected Is Nothing Then
                Hint("请先在「版本选择」里选一个游戏版本！", HintType.Red, False)
                Return
            End If
            Dim Indie As String = McInstanceSelected.PathIndie
            Dim Target As String
            Select Case Kind
                Case "logs" : Target = Path.Combine(Indie, "logs")
                Case "crash" : Target = Path.Combine(Indie, "crash-reports")
                Case "saves" : Target = Path.Combine(Indie, "saves")
                Case "mods" : Target = Path.Combine(Indie, "mods")
                Case "screenshots" : Target = Path.Combine(Indie, "screenshots")
                Case Else : Target = Indie
            End Select
            If Not Directory.Exists(Target) Then Directory.CreateDirectory(Target)
            Logger.Info($"打开版本文件夹（{Kind}）：{Target}")
            Process.Start("explorer.exe", Chr(34) & Target.TrimEnd(Chr(92)) & Chr(34))
        Catch ex As Exception
            Logger.Warn(ex, "打开文件夹失败")
            Hint("打开文件夹失败：" & ex.Message, HintType.Red, False)
        End Try
    End Sub

#End Region

#Region "官方服务凭据"

    '设置项名称（存在启动器自己的注册表项里，随设置一起备份）
    Private Const KeyCredentialMsId As String = "CredentialMsClientId"
    Private Const KeyCredentialCfKey As String = "CredentialCfKey"
    Private Const KeyCredentialCfDays As String = "CredentialCfDays"
    Private Const KeyCredentialCfStart As String = "CredentialCfStart"
    Private Const KeyCredentialMute As String = "CredentialExpireMute"
    Private Const KeyCredentialNotified As String = "CredentialExpireNotified"
    Private Const KeyCredentialForcedOn As String = "CredentialExpireForcedOn"
    ''' <summary>剩余天数小于等于此值时强制弹窗（忽略「不再提示」）。</summary>
    Private Const CfForceDays As Integer = 3
    ''' <summary>剩余天数小于等于此值时开始给提示（高于强制档）。</summary>
    Private Const CfWarnDays As Integer = 10
    ''' <summary>CurseForge 控制台签发的密钥有效期，官方默认为 90 天。</summary>
    Private Const CfDefaultDays As Integer = 90
    Private Const CfConsoleUrl As String = "https://console.curseforge.com/"
    Friend Const EntraUrl As String = "https://entra.microsoft.com/#view/Microsoft_AAD_RegisteredApps/CreateApplicationBlade"

    Friend ReadOnly Property CredentialMsId As String
        Get
            Return If(Settings.Get(Of String)(KeyCredentialMsId), "").Trim()
        End Get
    End Property
    Friend ReadOnly Property CredentialCfKey As String
        Get
            Return If(Settings.Get(Of String)(KeyCredentialCfKey), "").Trim()
        End Get
    End Property
    ''' <summary>密钥有效期天数，未设置时按官方默认的 90 天算。</summary>
    Friend ReadOnly Property CredentialCfDays As Integer
        Get
            Dim N As Integer = Settings.Get(Of Integer)(KeyCredentialCfDays)
            Return If(N > 0, N, CfDefaultDays)
        End Get
    End Property
    ''' <summary>开始计时的日期；从未填过密钥则为 Nothing。</summary>
    Friend ReadOnly Property CredentialCfStart As Date?
        Get
            If CredentialCfKey = "" Then Return Nothing
            Dim S As String = If(Settings.Get(Of String)(KeyCredentialCfStart), "").Trim()
            If S = "" Then Return Date.Today
            Dim D As Date
            If Not Date.TryParse(S, D) Then Return Date.Today
            Return D
        End Get
    End Property
    ''' <summary>剩余天数；没有密钥返回 -1。</summary>
    Friend ReadOnly Property CredentialCfDaysLeft As Integer
        Get
            Dim Start = CredentialCfStart
            If Start Is Nothing Then Return -1
            Return CInt(Math.Floor((Start.Value.Date.AddDays(CredentialCfDays) - Date.Today).TotalDays))
        End Get
    End Property
    ''' <summary>到期日的文本；没有密钥返回空串。</summary>
    Friend ReadOnly Property CredentialCfExpireText As String
        Get
            Dim Start = CredentialCfStart
            If Start Is Nothing Then Return ""
            Return Start.Value.Date.AddDays(CredentialCfDays).ToString("yyyy-MM-dd")
        End Get
    End Property

    ''' <summary>
    ''' 把设置里的凭据写进运行时变量，之后立即生效（登录、CurseForge 请求都会用新值）。
    ''' 只有设置里确实填了值才覆盖，避免把编译期内嵌的密钥（secrets.env + Set-Keys.cmd 那条老路）清掉。
    ''' </summary>
    Friend Sub CredentialApply()
        Try
            Dim MsId As String = CredentialMsId
            Dim CfKey As String = CredentialCfKey
            If MsId <> "" Then OAuthClientId = MsId
            If CfKey <> "" Then CurseForgeAPIKey = CfKey
        Catch ex As Exception
            Logger.Warn(ex, "应用凭据失败")
        End Try
    End Sub

    ''' <summary>
    ''' 保存凭据。若 CurseForge 密钥发生变化，到期计时从今天重新开始。
    ''' </summary>
    Friend Sub CredentialSave(MsId As String, CfKey As String, Days As Integer)
        MsId = If(MsId, "").Trim()
        CfKey = If(CfKey, "").Trim()
        If Days <= 0 Then Days = CfDefaultDays
        Dim KeyChanged As Boolean = Not String.Equals(CfKey, CredentialCfKey, StringComparison.Ordinal)
        Settings.Set(KeyCredentialMsId, MsId)
        Settings.Set(KeyCredentialCfKey, CfKey)
        Settings.Set(KeyCredentialCfDays, Days)
        If KeyChanged OrElse (If(Settings.Get(Of String)(KeyCredentialCfStart), "")).Trim() = "" Then
            Settings.Set(KeyCredentialCfStart, Date.Today.ToString("yyyy-MM-dd"))
            Settings.Set(KeyCredentialNotified, 0)
            Settings.Set(KeyCredentialForcedOn, "")
            Settings.Set(KeyCredentialMute, False)
        End If
        CredentialApply()
        Logger.Info($"已保存凭据：微软登录 Client ID {(If(MsId = "", "未填写", "已填写"))}，CurseForge 密钥 {(If(CfKey = "", "未填写", "已填写"))}，有效期 {Days} 天")
    End Sub

    ''' <summary>
    ''' 启动时调用：载入凭据，并在界面就绪后检查是否临近到期。
    ''' </summary>
    Friend Sub CredentialStartup()
        CredentialApply()
        RunInNewThread(
        Sub()
            Threading.Thread.Sleep(8000) '等主界面加载完再弹提醒，避免和启动动画抢焦点
            CredentialExpireCheck(False)
        End Sub, "Credential Expire Check", Threading.ThreadPriority.BelowNormal)
    End Sub

    ''' <summary>
    ''' 检查 CurseForge 密钥有效期，分两档处理：
    ''' 剩余 ≤3 天（含已过期）→ 强制弹窗，忽略「不再提示」，同一天最多一次；
    ''' 剩余 4–10 天 → 小提示框，尊重「不再提示」，每档位只提示一次。
    ''' AlwaysReport 为 True 时不受档位去重限制（设置页手动触发用）。
    ''' </summary>
    Friend Sub CredentialExpireCheck(AlwaysReport As Boolean)
        Try
            Dim Left As Integer = CredentialCfDaysLeft
            If Left < 0 Then
                If AlwaysReport Then RunInUi(Sub() Hint("还没有填写 CurseForge 密钥，当前搜索走 MCIM 镜像，功能不受影响。", HintType.Blue, False))
                Return
            End If
            Dim ExpireText As String = CredentialCfExpireText
            If Left > CfWarnDays Then
                If AlwaysReport Then RunInUi(Sub() Hint($"CurseForge 密钥还有 {Left} 天到期（{ExpireText}），暂时不需要处理。", HintType.Green, False))
                Return
            End If
            Dim Head As String = If(Left <= 0,
                $"CurseForge 密钥已经过期（到期日 {ExpireText}）。",
                $"CurseForge 密钥还有 {Left} 天到期（到期日 {ExpireText}）。")

            '—— 第一档：3 天内（含已过期）强制弹窗 ——
            If Left <= CfForceDays Then
                Dim Today As String = Date.Today.ToString("yyyy-MM-dd")
                If Not AlwaysReport Then
                    '强提醒也做每日一次节流，否则一天里反复开启动器会一直弹
                    If String.Equals(Settings.Get(Of String)(KeyCredentialForcedOn), Today, StringComparison.Ordinal) Then Return
                End If
                Settings.Set(KeyCredentialForcedOn, Today)
                Settings.Set(KeyCredentialNotified, 0) '重新回到最紧急档，后续换钥匙后能再提示一轮
                RunInUi(
                Sub()
                    Select Case MyMsgBox(
                        Head & vbCrLf &
                        "到期后直连官方源会返回 403，模组搜索会退回 MCIM 镜像（仍能搜索，但新版本同步可能延迟）。" & vbCrLf & vbCrLf &
                        "请去控制台重新生成一把密钥，然后在 设置 → 其他 → 官方服务凭据 里更新。",
                        "CurseForge 密钥即将失效", "打开控制台", "确认", IsWarn:=True)
                        Case 1
                            OpenWebsite(CfConsoleUrl)
                    End Select
                End Sub)
                Return
            End If

            '—— 第二档：4–10 天，小提示框 ——
            If Settings.Get(Of Boolean)(KeyCredentialMute) Then Return
            Dim Level As Integer = If(Left <= 5, 5, CfWarnDays) '档位数字越小越紧急，同一档只提示一次
            Dim Notified As Integer = Settings.Get(Of Integer)(KeyCredentialNotified)
            If Not AlwaysReport AndAlso Notified <> 0 AndAlso Notified <= Level Then Return
            Settings.Set(KeyCredentialNotified, Level)
            RunInUi(
            Sub()
                Select Case MyMsgBox(
                    $"CurseForge 密钥还有 {Left} 天到期（{ExpireText}）。" & vbCrLf &
                    "到期后直连官方源会返回 403，可提前去控制台重新生成。",
                    "密钥即将到期", "打开控制台", "确认", "不再提示")
                    Case 1
                        OpenWebsite(CfConsoleUrl)
                    Case 3
                        Settings.Set(KeyCredentialMute, True)
                        Hint("已关闭这档提示，剩余 3 天内仍会强制弹窗；可在 设置 → 其他 → 官方服务凭据 中重新开启。",, False)
                End Select
            End Sub)
        Catch ex As Exception
            Logger.Warn(ex, "检查 CurseForge 密钥有效期失败")
        End Try
    End Sub

    Friend ReadOnly Property CredentialMuted As Boolean
        Get
            Return Settings.Get(Of Boolean)(KeyCredentialMute)
        End Get
    End Property
    Friend Sub CredentialSetMuted(Value As Boolean)
        Settings.Set(KeyCredentialMute, Value)
        If Not Value Then Settings.Set(KeyCredentialNotified, 0) '重新开启提醒时，允许再提示一轮
    End Sub

#End Region

#Region "自定义下载源"

    Private Const KeyCustomMirror As String = "CustomDownloadMirror"
    Private Const DefaultMirror As String = "https://bmclapi2.bangbang93.com"

    ''' <summary>界面里填的镜像根地址；未填写返回空串（表示用默认 BMCLAPI）。</summary>
    Friend ReadOnly Property CustomMirrorSetting As String
        Get
            Return If(Settings.Get(Of String)(KeyCustomMirror), "").Trim()
        End Get
    End Property

    Friend ReadOnly Property MirrorIsCustom As Boolean
        Get
            Return CustomMirrorSetting <> ""
        End Get
    End Property

    ''' <summary>实际使用的镜像根地址，不带结尾斜杠。未自定义时返回 BMCLAPI。</summary>
    Friend Function MirrorBase() As String
        Dim S As String = CustomMirrorSetting
        If S = "" Then Return DefaultMirror
        Return S
    End Function

    ''' <summary>规范化用户填的地址：去空格、补协议、去结尾斜杠。</summary>
    Private Function NormalizeMirror(Url As String) As String
        Url = If(Url, "").Trim()
        If Url = "" Then Return ""
        If Not Url.StartsWith("http", StringComparison.OrdinalIgnoreCase) Then Url = "https://" & Url
        Return Url.TrimEnd("/"c)
    End Function

    ''' <summary>保存自定义镜像地址；传空串表示恢复默认。重启启动器后生效。</summary>
    Friend Sub CustomMirrorSave(Url As String)
        Dim Norm As String = NormalizeMirror(Url)
        Settings.Set(KeyCustomMirror, Norm)
        Logger.Info($"已保存自定义下载源：{If(Norm = "", "（默认 BMCLAPI 镜像）", Norm)}")
    End Sub

    ''' <summary>判断一个 URL 是否由镜像提供（镜像专属的限速、403 重试逻辑要用）。</summary>
    Friend Function IsMirrorUrl(Url As String) As Boolean
        If String.IsNullOrEmpty(Url) Then Return False
        If Url.Contains("bmclapi") OrElse Url.Contains("mcimirror.top") Then Return True
        Dim Custom1 As String = CustomMirrorSetting
        Return Custom1 <> "" AndAlso Url.StartsWith(Custom1, StringComparison.OrdinalIgnoreCase)
    End Function

#End Region

#Region "联机方案"

    Private Const KeyLinkName As String = "LinkToolName"
    Private Const KeyLinkUrl As String = "LinkToolUrl"
    Private Const KeyLinkPath As String = "LinkToolPath"
    Private Const KeyLinkAutoLaunch As String = "LinkAutoLaunch"
    ''' <summary>未指定工具时的占位名与默认下载页（樱花穿透，国内用得最多，免费额度够开一局）。</summary>
    Private Const LinkNoneText As String = "未选择"
    Private Const DefaultLinkUrl As String = "https://www.natfrp.com/tunnel/download"

    ''' <summary>工具名；未指定时为空串。</summary>
    Friend ReadOnly Property LinkToolName As String
        Get
            Return If(Settings.Get(Of String)(KeyLinkName), "").Trim()
        End Get
    End Property

    ''' <summary>展示用的工具名：未指定时显示「未选择」，避免让人以为已经选好了。</summary>
    Friend ReadOnly Property LinkToolDisplayName As String
        Get
            Return If(LinkToolName = "", LinkNoneText, LinkToolName)
        End Get
    End Property

    ''' <summary>下载 / 官网地址；留空则用默认的陶瓦联机地址。</summary>
    Friend ReadOnly Property LinkToolUrl As String
        Get
            Dim S As String = If(Settings.Get(Of String)(KeyLinkUrl), "").Trim()
            Return If(S = "", DefaultLinkUrl, S)
        End Get
    End Property

    ''' <summary>本地程序路径；未配置为空串。</summary>
    Friend ReadOnly Property LinkToolPath As String
        Get
            Return If(Settings.Get(Of String)(KeyLinkPath), "").Trim()
        End Get
    End Property

    Friend ReadOnly Property LinkToolIsCustom As Boolean
        Get
            Return If(Settings.Get(Of String)(KeyLinkName), "").Trim() <> "" OrElse
                   If(Settings.Get(Of String)(KeyLinkUrl), "").Trim() <> "" OrElse
                   LinkToolPath <> ""
        End Get
    End Property

    ''' <summary>本地程序是否存在（配置了路径且文件还在）。</summary>
    Friend ReadOnly Property LinkToolIsInstalled As Boolean
        Get
            Dim P As String = LinkToolPath
            Return P <> "" AndAlso IO.File.Exists(P)
        End Get
    End Property

    ''' <summary>联机工具现在是不是已经开着（按 exe 名匹配进程）。</summary>
    Friend Function LinkToolRunning() As Boolean
        Try
            Dim P As String = LinkToolPath
            If P = "" Then Return False
            Dim ExeName As String = IO.Path.GetFileNameWithoutExtension(P)
            If ExeName = "" Then Return False
            Return Process.GetProcessesByName(ExeName).Any()
        Catch ex As Exception
            Logger.Warn(ex, "检查联机工具是否在运行失败")
            Return False
        End Try
    End Function

    ''' <summary>保存工具名称与程序路径（下载地址不再由界面编辑）。</summary>
    Friend Sub LinkToolSaveNamePath(Name As String, Path As String)
        Settings.Set(KeyLinkName, If(Name, "").Trim())
        Settings.Set(KeyLinkPath, If(Path, "").Trim())
        Logger.Info($"已保存联机方案：{LinkToolDisplayName}，程序：{If(LinkToolPath = "", "（未配置）", LinkToolPath)}")
    End Sub

    ''' <summary>保存联机方案；三项都传空串表示恢复默认（陶瓦联机）。</summary>
    Friend Sub LinkToolSave(Name As String, Url As String, Optional Path As String = "")
        Settings.Set(KeyLinkName, If(Name, "").Trim())
        Settings.Set(KeyLinkUrl, If(Url, "").Trim())
        Settings.Set(KeyLinkPath, If(Path, "").Trim())
        Logger.Info($"已保存联机方案：{LinkToolName} — {LinkToolUrl}，程序：{If(LinkToolPath = "", "（未配置）", LinkToolPath)}")
    End Sub

    ''' <summary>
    ''' 启动联机工具：配了本地程序就启动它（已在运行只提示），否则用浏览器打开下载页。
    ''' 返回一句给用户看的提示。
    ''' </summary>
    Friend Function LinkToolLaunch() As String
        Dim P As String = LinkToolPath
        If P <> "" AndAlso Not IO.File.Exists(P) Then
            '配置过但文件没了，退回打开地址
            OpenWebsite(LinkToolUrl)
            Return $"找不到「{LinkToolDisplayName}」的程序（{P}），已打开它的下载页。重新选一下程序路径就好。"
        End If
        If P = "" Then
            OpenWebsite(LinkToolUrl)
            Return $"还没指定联机工具，已用浏览器打开樱花穿透的下载页；装好后在「程序路径」里选一下 exe，以后就能一键启动。"
        End If
        If LinkToolRunning() Then Return $"「{LinkToolDisplayName}」已经在运行了，直接去它里面创建或加入房间就行。"
        Process.Start(New ProcessStartInfo(P) With {
            .UseShellExecute = True,
            .WorkingDirectory = IO.Path.GetDirectoryName(P)
        })
        Return $"已启动「{LinkToolDisplayName}」，接下来在它里面创建或加入房间。"
    End Function

    ''' <summary>联机页 / 设置页共用的状态文案。</summary>
    Friend ReadOnly Property LinkToolStatusText As String
        Get
            If LinkToolPath = "" AndAlso LinkToolName = "" Then Return "当前：未选择（点「选择」指定本地程序；直接点「启动」会打开樱花穿透下载页）"
            If LinkToolPath = "" Then Return $"当前：{LinkToolDisplayName}（未指定本地程序）"
            If Not LinkToolIsInstalled Then Return $"当前：{LinkToolDisplayName}（程序路径无效：{LinkToolPath}）"
            Return $"当前：{LinkToolDisplayName}（{LinkToolPath}{If(LinkToolRunning(), "，正在运行", "")}）"
        End Get
    End Property

''' <summary>启动游戏时是否自动拉起联机工具（默认关闭）。</summary>
    Friend Property LinkAutoLaunchEnabled As Boolean
        Get
            Return Settings.Get(Of Boolean)(KeyLinkAutoLaunch)
        End Get
        Set(Value As Boolean)
            Settings.Set(KeyLinkAutoLaunch, Value)
        End Set
    End Property

''' <summary>游戏开始启动时调用：按需拉起联机工具，已在运行就跳过。</summary>
    Friend Sub LinkAutoLaunchOnGameStart()
        Try
            If Not LinkAutoLaunchEnabled Then Return
            If Not LinkToolIsInstalled Then Return
            If LinkToolRunning() Then Return
            Process.Start(New ProcessStartInfo(LinkToolPath) With {
                .UseShellExecute = True,
                .WorkingDirectory = IO.Path.GetDirectoryName(LinkToolPath)
            })
            Logger.Info($"游戏启动前已自动拉起联机工具：{LinkToolPath}")
            RunInUi(Sub() Hint($"已顺带启动「{LinkToolDisplayName}」", HintType.Blue, False))
        Catch ex As Exception
            Logger.Warn(ex, "自动启动联机工具失败")
        End Try
    End Sub

#End Region

#Region "陶瓦联机"

    ' 联机由 Terracotta（陶瓦联机）提供：原样打包官方二进制，只经其 HTTP API 通信，
    ' 符合其 AGPL 例外条款第 1、2 条。开源源码：https://github.com/burningtnt/Terracotta
    '
    ' 集成契约（2026-10-27 实测，详见 launcher-dev\06-multiplayer.md）：
    '   1. terracotta.exe --hmcl <文件>  →  文件里出现 {"port":N}（实测 0.10s）
    '   2. 之后全走 http://127.0.0.1:<port> 的 GET：/state /state/scanning /state/guesting /state/ide /meta /panic
    '   3. 端口随机，必须等文件；本地回环必须禁代理，否则被系统代理劫持成 502

    Private Const KeyTcExePath As String = "TcExePath"
    Private Const KeyTcLastRoom As String = "TcLastRoom"
    Private Const KeyTcCopyAddr As String = "TcCopyAddr"
    Private Const KeyTcAutoAddr As String = "TcAutoAddr"

    ''' <summary>「复制」按钮复制什么：True = 直连地址，False = 房间码（默认）。
    ''' 做成显式勾选而不是按状态自动切换——自动切换时用户点之前不知道会复制到哪个，
    ''' 而复制错了要贴给别人的东西，代价比多点一次高。
    ''' 读取失败一律退回默认值：这里崩掉会让整个界面加载失败，代价远大于"记不住上次的选择"。</summary>
    Friend Property TcCopyAddrMode As Boolean
        Get
            Try
                Return Settings.Get(Of Boolean)(KeyTcCopyAddr)
            Catch ex As Exception
                Logger.Warn(ex, "读取复制目标设置失败，按房间码处理")
                Return False
            End Try
        End Get
        Set(value As Boolean)
            Try
                Settings.Set(KeyTcCopyAddr, value)
            Catch ex As Exception
                Logger.Warn(ex, "保存复制目标设置失败")
            End Try
        End Set
    End Property

    ''' <summary>复制时是否自动检测：True（默认）= 勾选那种暂时用不了就自动改复制另一种；
    ''' False = 严格按勾选，没有就提示没有。做成可关是因为有人习惯固定用直连地址，
    ''' 不希望系统替他把目标换掉。读取失败按"开启"处理（与默认一致）。</summary>
    Friend Property TcAutoAddrMode As Boolean
        Get
            Try
                Return Settings.Get(Of Boolean)(KeyTcAutoAddr)
            Catch ex As Exception
                Logger.Warn(ex, "读取自动检测设置失败，按开启处理")
                Return True
            End Try
        End Get
        Set(value As Boolean)
            Try
                Settings.Set(KeyTcAutoAddr, value)
            Catch ex As Exception
                Logger.Warn(ex, "保存自动检测设置失败")
            End Try
        End Set
    End Property

    ''' <summary>当前内置的陶瓦版本与校验值（v0.4.2，windows-x86_64）。</summary>
    Private Const TcVersion As String = "0.4.2"
    Private Const TcPackageSha512 As String =
        "6a98f524d4f00373696517306af8aa50d01d55ce4eadb27e9e4bc2f882707a0b5f20d5d4c33371d1459dcf5bf144ffed9beb414202d9ccf32b11dbbfcf19d650"

    Private TcPort As Integer = -1
    Private TcPortFile As String = ""

    ''' <summary>陶瓦的房间码字符表：34 个，去掉了易混的 I 与 O。</summary>
    Private Const TcCodeChars As String = "0123456789ABCDEFGHJKLMNPQRSTUVWXYZ"

    ' ── 位置与状态 ────────────────────────────────────────────────────────────

    ''' <summary>陶瓦程序目录，跟其它自用数据一起放在 PCL 目录下。</summary>
    Friend ReadOnly Property TcDir As String
        Get
            Return PathPure.Value & "Terracotta\"
        End Get
    End Property

    ''' <summary>实际使用的 exe：优先用户指定，其次目录里的 terracotta*.exe，没有再返回空串。</summary>
    Friend ReadOnly Property TcExePath As String
        Get
            Try
                Dim Custom As String = If(Settings.Get(Of String)(KeyTcExePath), "").Trim()
                If Custom <> "" AndAlso File.Exists(Custom) Then Return Custom
                If Directory.Exists(TcDir) Then
                    Dim Hit As String = Directory.GetFiles(TcDir, "terracotta*.exe").FirstOrDefault()
                    If Hit IsNot Nothing Then Return Hit
                End If
            Catch ex As Exception
                Logger.Warn(ex, "查找陶瓦程序失败")
            End Try
            Return ""
        End Get
    End Property

    Friend ReadOnly Property TcInstalled As Boolean
        Get
            Return TcExePath <> ""
        End Get
    End Property

    Friend Sub TcSetExePath(Path As String)
        Settings.Set(KeyTcExePath, If(Path, "").Trim())
    End Sub

    ''' <summary>上次用的房间码，方便复用。</summary>
    Friend ReadOnly Property TcLastRoom As String
        Get
            Return If(Settings.Get(Of String)(KeyTcLastRoom), "").Trim()
        End Get
    End Property

    ' ── 进程与端口 ────────────────────────────────────────────────────────────

    ''' <summary>读全局锁文件里的端口（前两字节，大端）。文件不存在或读不到返回 -1。</summary>
    Private Function TcLockPort() As Integer
        Try
            Dim F As String = IO.Path.Combine(IO.Path.GetTempPath(), "terracotta", "terracotta.lock")
            If Not File.Exists(F) Then Return -1
            ' 守护进程可能正持有该文件，必须用 FileShare.ReadWrite 才读得到
            Using Fs As New FileStream(F, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)
                Dim B(1) As Byte
                If Fs.Read(B, 0, 2) < 2 Then Return -1
                Return (CInt(B(0)) << 8) Or CInt(B(1))
            End Using
        Catch ex As Exception
            Return -1
        End Try
    End Function

    ''' <summary>探活：该端口上是否真有一个陶瓦在应答。</summary>
    Private Function TcAlive(Port As Integer) As Boolean
        If Port <= 0 Then Return False
        Try
            Dim Txt As String = TcHttpGet(Port, "/meta", 1500)
            Return Txt.Contains("version")
        Catch ex As Exception
            Return False
        End Try
    End Function

    ''' <summary>取一个正在运行的陶瓦端口，**不启动新进程**。没有则返回 -1。</summary>
    Private Function TcRunningPort() As Integer
        If TcPort > 0 AndAlso TcAlive(TcPort) Then Return TcPort
        '锁文件里可能是别的启动器（HMCL）起的，能通就直接复用
        Dim Locked As Integer = TcLockPort()
        If TcAlive(Locked) Then
            TcPort = Locked
            Return Locked
        End If
        Return -1
    End Function

    ''' <summary>确保守护进程在跑（必要时启动），返回端口；起不来返回 -1。</summary>
    Friend Function TcEnsure() As Integer
        Dim Running As Integer = TcRunningPort()
        If Running > 0 Then Return Running

        Dim Exe As String = TcExePath
        If Exe = "" Then Return -1

        Try
            If TcPortFile = "" Then
                TcPortFile = IO.Path.Combine(IO.Path.GetTempPath(), "PCL-Terracotta-Port.json")
            End If
            If File.Exists(TcPortFile) Then File.Delete(TcPortFile)

            Dim Psi As New ProcessStartInfo(Exe) With {
                .UseShellExecute = False,
                .WorkingDirectory = IO.Path.GetDirectoryName(Exe)
            }
            Psi.Arguments = "--hmcl """ & TcPortFile & """"
            Process.Start(Psi)

            '父进程在 Windows 上会立刻退出并把服务交给子进程，所以不能拿父进程存活当判据
            For i As Integer = 1 To 100
                Thread.Sleep(200)
                If Not File.Exists(TcPortFile) Then Continue For
                Dim Txt As String = ""
                Try
                    Txt = File.ReadAllText(TcPortFile)
                Catch
                    Continue For
                End Try
                Dim M As Text.RegularExpressions.Match =
                    Text.RegularExpressions.Regex.Match(Txt, """port""\s*:\s*(\d+)")
                If Not M.Success Then Continue For
                Dim P As Integer = CInt(M.Groups(1).Value)
                If TcAlive(P) Then
                    TcPort = P
                    Logger.Info($"陶瓦联机：已启动守护进程，端口 {P}（{Exe}）")
                    Return P
                End If
            Next
            Logger.Warn($"陶瓦联机：启动超时，20 秒内没等到端口文件（{TcPortFile}）")
            Return -1
        Catch ex As Exception
            Logger.Error(ex, "启动陶瓦联机失败")
            Return -1
        End Try
    End Function

    ' ── HTTP（必须绕开系统代理）──────────────────────────────────────────────

    Private Function TcHttpGet(Port As Integer, ApiPath As String, Optional TimeoutMs As Integer = 4000) As String
        ' 本地回环走系统代理会被劫持成 502，这里显式禁用代理
        Dim Handler As New Net.Http.HttpClientHandler With {.UseProxy = False}
        Using Client As New Net.Http.HttpClient(Handler)
            Client.Timeout = TimeSpan.FromMilliseconds(TimeoutMs)
            Return Client.GetStringAsync($"http://127.0.0.1:{Port}{ApiPath}").GetAwaiter().GetResult()
        End Using
    End Function

    ' ── 状态读取 ──────────────────────────────────────────────────────────────

    ''' <summary>陶瓦状态快照。</summary>
    Friend Class TcSnapshot
        Friend Ok As Boolean = False
        Friend Stage As String = ""
        Friend Room As String = ""
        Friend Url As String = ""
        Friend Difficulty As String = ""
        Friend ExceptionType As Integer = -1
    End Class

    Friend Function TcReadState(Optional AutoStart As Boolean = True) As TcSnapshot
        Dim S As New TcSnapshot
        Dim Port As Integer = If(AutoStart, TcEnsure(), TcRunningPort())
        If Port <= 0 Then Return S
        Dim Txt As String
        Try
            Txt = TcHttpGet(Port, "/state")
        Catch ex As Exception
            TcPort = -1     '进程没了，下次重建
            Return S
        End Try
        Try
            Dim O As Newtonsoft.Json.Linq.JObject = Newtonsoft.Json.Linq.JObject.Parse(Txt)
            S.Ok = True
            S.Stage = If(O("state") Is Nothing, "", O("state").ToString())
            If O("room") IsNot Nothing Then S.Room = O("room").ToString()
            If O("url") IsNot Nothing Then S.Url = O("url").ToString()
            If O("difficulty") IsNot Nothing Then S.Difficulty = O("difficulty").ToString()
            If O("type") IsNot Nothing Then S.ExceptionType = CInt(O("type").ToString())
        Catch ex As Exception
            Logger.Warn(ex, "解析陶瓦状态失败")
        End Try
        Return S
    End Function

    ' ── 动作 ──────────────────────────────────────────────────────────────────

    ''' <summary>
    ''' 切换状态前先把守护进程拉回空闲。
    ''' 必须这么做的原因：陶瓦是状态机，上一次加入/建房失败后会停在 exception / guesting 等非空闲态，
    ''' 此时再发 /state/guesting 或 /state/scanning 会被拒——表现就是「恢复网络后点『加入』没反应，
    ''' 必须点『关闭陶瓦』重启守护进程才能进」。这里先发一次 /state/ide，让重试不必重启进程。
    ''' 复位失败不阻断主流程：目标状态请求本身会返回错误，交给上层提示。
    ''' </summary>
    Friend Sub TcEnsureIdle(Port As Integer)
        If Port <= 0 Then Return
        Try
            TcHttpGet(Port, "/state/ide", 2500)
        Catch ex As Exception
            Logger.Warn(ex, "切换前复位陶瓦状态失败")
        End Try
    End Sub

    ''' <summary>创建房间。返回给用户看的提示。房间码留空则由陶瓦随机生成。</summary>
    Friend Function TcStartHost(Optional RoomCode As String = "") As String
        Dim Port As Integer = TcEnsure()
        If Port <= 0 Then Return TcNoBinaryHint()

        '接管判断：进来时若已有房间（可能来自 HMCL，也可能是上一次没复位的会话），
        '下面的 /state/scanning 会重建房间并换码。这件事必须明说——否则用户以为"复用了原码"，
        '把旧码发给朋友后对方连不上，而两边都不会报错。
        Dim Before As TcSnapshot = TcReadState(False)
        Dim OldRoom As String = ""
        If Before IsNot Nothing AndAlso Before.Ok AndAlso Before.Stage = "host-ok" Then OldRoom = Before.Room

        TcEnsureIdle(Port)

        Try
            Dim Q As String = If(RoomCode.Trim() = "", "", "?room=" & Uri.EscapeDataString(RoomCode.Trim()))
            TcHttpGet(Port, "/state/scanning" & Q, 5000)
        Catch ex As Exception
            Logger.Warn(ex, "请求陶瓦建房失败")
            Return "启动建房失败：" & ex.Message
        End Try

        Dim S As TcSnapshot = TcReadState()
        Dim NewRoom As String = S.Room

        '换码提示优先级最高：它改变的是"要发给别人的那串字符"，比状态文案重要。
        If OldRoom <> "" AndAlso Not String.Equals(OldRoom, NewRoom, StringComparison.OrdinalIgnoreCase) Then
            If NewRoom = "" Then
                Return $"已接管原有房间，旧码 {OldRoom} 已失效，正在生成新的房间码，稍等几秒。"
            End If
            Return $"已接管原有房间，房间码已更换为 {NewRoom}；旧码 {OldRoom} 已失效，请把新码发给朋友。"
        End If

        If S.Stage = "host-scanning" Then
            Return "已开始等待：请切到游戏，按 Esc → 「对局域网开放」→ 点「开始」，房间码随后出现。"
        End If
        Return "已发出建房请求，当前状态：" & TcStageText(S.Stage)
    End Function

    ''' <summary>加入房间。房间码非法时直接返回提示，不发请求。</summary>
    Friend Function TcJoin(RoomCode As String) As String
        Dim Code As String = TcNormalizeCode(RoomCode)
        If Code = "" Then Return "房间码格式不对：应为 U/XXXX-XXXX-XXXX-XXXX，共 21 个字符。"
        If Not TcCheckCode(Code) Then Return "房间码校验位不对，可能抄错了一个字符，请核对后重试。"

        Dim Port As Integer = TcEnsure()
        If Port <= 0 Then Return TcNoBinaryHint()
        TcEnsureIdle(Port)

        Try
            TcHttpGet(Port, "/state/guesting?room=" & Uri.EscapeDataString(Code), 5000)
            Settings.Set(KeyTcLastRoom, Code)
        Catch ex As Exception
            Logger.Warn(ex, "请求陶瓦加入房间失败")
            Return "加入失败：" & ex.Message
        End Try
        Return $"正在加入房间 {Code}，连上后进游戏「多人游戏」双击「陶瓦联机大厅」即可。"
    End Function

    ''' <summary>复位到空闲（取消建房 / 取消加入）。不启动新进程。</summary>
    Friend Sub TcReset()
        Dim Port As Integer = TcRunningPort()
        If Port <= 0 Then Return
        Try
            TcHttpGet(Port, "/state/ide", 3000)
        Catch ex As Exception
            Logger.Warn(ex, "复位陶瓦状态失败")
        End Try
    End Sub

    ''' <summary>关闭陶瓦守护进程。它会直接 exit，不回响应，连接被重置属正常。</summary>
    Friend Sub TcShutdown()
        Dim Port As Integer = TcRunningPort()
        If Port <= 0 Then Return
        Try
            TcHttpGet(Port, "/panic?peaceful=true", 2000)
        Catch ex As Exception
            '预期：进程退出，连接被重置
        End Try
        TcPort = -1
        Logger.Info("陶瓦联机：已请求关闭守护进程")
    End Sub

    ' ── 文案 ──────────────────────────────────────────────────────────────────

    Private Function TcNoBinaryHint() As String
        Return "还没安装陶瓦联机：点「安装 / 更新陶瓦」自动下载（约 8 MB）。"
    End Function

    Private Function TcStageText(Stage As String) As String
        Select Case Stage
            Case "waiting" : Return "空闲"
            Case "host-scanning" : Return "正在等待游戏开启局域网"
            Case "host-starting" : Return "正在创建房间"
            Case "host-ok" : Return "房间已就绪"
            Case "guest-connecting" : Return "正在连接房间"
            Case "guest-starting" : Return "正在打洞连接"
            Case "guest-ok" : Return "已连上"
            Case "exception" : Return "失败"
            Case "" : Return "未运行"
            Case Else : Return Stage
        End Select
    End Function

    Private Function TcDifficultyText(D As String) As String
        Select Case D
            Case "EASIEST" : Return "网络极佳"
            Case "SIMPLE" : Return "网络良好"
            Case "MEDIUM" : Return "网络一般，可能要多等几秒"
            Case "TOUGH" : Return "网络较差，可能连不上"
            Case Else : Return "正在评估网络"
        End Select
    End Function

    ''' <summary>exception.type 的 0–5 对照，全部来自源码 ExceptionType 枚举顺序。</summary>
    Private Function TcExceptionText(Kind As Integer) As String
        Select Case Kind
            Case 0 : Return "连不上房主（对方可能已关房或离线，也可能是网络被限制）。"
            Case 1 : Return "房主主动断开了连接。"
            Case 2 : Return "本机的组网组件崩了，重试一次；反复失败就换樱花穿透那条路。"
            Case 3 : Return "房主那边的组网组件崩了，让对方重开房间。"
            Case 4 : Return "房主那边的游戏已关闭（退出世界或退了游戏）。"
            Case 5 : Return "房间码或版本对不上，确认双方都用同一个版本。"
            Case Else : Return "未知错误，可看陶瓦的运行日志。"
        End Select
    End Function

    ''' <summary>一句话状态，给界面用。轮询调用，**不会**因此启动进程。</summary>
    Friend ReadOnly Property TcStatusText As String
        Get
            If Not TcInstalled Then Return "状态：陶瓦未安装。点「安装 / 更新陶瓦」自动下载（约 8 MB）。"
            Return TcStatusTextFor(TcReadState(False))
        End Get
    End Property

    ' ── 本机网络可达性（T-05a：host-ok 断网提示）────────────────────────────
    '只看「有没有带默认网关的已启用网卡」，不用 GetIsNetworkAvailable：
    'VMware 的 VMnet1/VMnet8 常驻 Up 且无默认网关，只数网卡会永远判成可用（2026-10-29 实测踩到）。
    '已知边界：路由器有网关但本身没连外网时，这里仍判「可用」——宁可漏报，不做会误报的探测。
    Private TcNetLastState As Integer = -1 ' -1 未记录 / 0 可用 / 1 不可用

    Friend Function TcLocalNetworkAvailable() As Boolean
        Dim GatewayNic As String = ""
        Try
            For Each Nic In System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces()
                If Nic.OperationalStatus <> System.Net.NetworkInformation.OperationalStatus.Up Then Continue For
                If Nic.NetworkInterfaceType = System.Net.NetworkInformation.NetworkInterfaceType.Loopback Then Continue For
                If Nic.GetIPProperties().GatewayAddresses.Count > 0 Then
                    GatewayNic = Nic.Name
                    Exit For
                End If
            Next
        Catch ex As Exception
            '检测异常按可用处理，避免误报断网
            Logger.Warn(ex, "枚举本机网卡失败")
            Return True
        End Try

        If GatewayNic = "" Then
            TcLogNetState(False, "没有带默认网关的网卡")
            Return False
        End If
        TcLogNetState(True, "网关网卡：" & GatewayNic)
        Return True
    End Function

    '状态变化时写一行日志，方便复测时直接看日志定位
    Private Sub TcLogNetState(Available As Boolean, Reason As String)
        Dim Now As Integer = If(Available, 0, 1)
        If TcNetLastState = Now Then Return
        TcNetLastState = Now
        Logger.Info($"联机断网检测：本机网络{(If(Available, "可用", "不可用"))}（{Reason}）")
    End Sub

    ''' <summary>把一份已经取到的快照转成一句话状态，避免界面重复请求。</summary>
    Friend Function TcStatusTextFor(S As TcSnapshot) As String
        If S Is Nothing OrElse Not S.Ok Then Return "状态：陶瓦未运行（点「创建房间」或「加入房间」时会自动启动）。"
        Select Case S.Stage
                Case "waiting"
                    Return "状态：空闲，可以创建或加入房间。"
                Case "host-scanning"
                    Return "状态：正在等待游戏开启局域网 —— 请切到游戏按 Esc → 「对局域网开放」→ 「开始」。"
                Case "host-starting"
                    Return $"状态：正在创建房间…（房间码 {S.Room}）"
                Case "host-ok"
                    'host-ok 只表示本机房间成立，不校验外人是否可达（T-05a）：
                    '本机断网时仍提示「房间已就绪」会误导，这里追加一句网络告警。
                    Dim HostText As String = $"房间已就绪，点「复制房间码」发给朋友：{S.Room}"
                    If Not TcLocalNetworkAvailable() Then
                        HostText &= vbCrLf & "（注意：当前网络不可用。本机房间已建好，但其他人现在可能连不上。）"
                    End If
                    Return HostText
                Case "guest-connecting"
                    Return $"状态：正在连接房间 {S.Room}…"
                Case "guest-starting"
                    Return $"状态：正在打洞连接…（{TcDifficultyText(S.Difficulty)}）"
                Case "guest-ok"
                    '地址单独占一行：这是连不上"陶瓦联机大厅"条目时的唯一退路，
                    '混在长句里会被忽略（实测多次都是靠它救回来的）。
                    If S.Url = "" Then Return "已连上！进游戏「多人游戏」双击「陶瓦联机大厅」即可。"
                    Return "已连上！进游戏「多人游戏」双击「陶瓦联机大厅」。" & vbCrLf &
                           $"连不上时改用「直接连接」填：{S.Url}（勾选「复制直连地址」可一键复制）"
                Case "exception"
                    '陶瓦守护进程刚被拉起时会先报一个"空异常"（日志里就是「（无）→ exception」）：
                    '既没有房间码也没有直连地址，说明它只是还没开始，不是真的失败。
                    '真正的失败一定挂着房间码或地址，所以拿这两个是否为空来区分——否则一进联机页就先挨一句"失败"。
                    If S.Room = "" AndAlso S.Url = "" Then
                        Return "状态：陶瓦已启动，还没有房间 —— 点「创建房间」或「加入房间」开始。"
                    End If
                    Return "状态：失败 —— " & TcExceptionText(S.ExceptionType)
                Case Else
                    Return "状态：" & S.Stage
        End Select
    End Function

    ' ── 房间码校验（与陶瓦 parse 规则一致）────────────────────────────────────

    ''' <summary>归一化：去空白、转大写、I→1、O→0，并截出 U/ 开头的 21 位。</summary>
    Friend Function TcNormalizeCode(Code As String) As String
        If Code Is Nothing Then Return ""
        Dim S As String = Code.Trim().ToUpperInvariant().Replace(" ", "").Replace("I", "1").Replace("O", "0")
        Dim I As Integer = S.IndexOf("U/", StringComparison.Ordinal)
        If I < 0 Then Return ""
        S = S.Substring(I)
        If S.Length < 21 Then Return ""
        Return S.Substring(0, 21)
    End Function

    ''' <summary>校验：16 位按 34 进制取模 7 必须为 0（逐步取模，避免 34^16 溢出）。</summary>
    Friend Function TcCheckCode(Code As String) As Boolean
        Dim S As String = TcNormalizeCode(Code)
        If S.Length <> 21 OrElse Not S.StartsWith("U/") Then Return False
        Dim Body As String = S.Substring(2)
        If Body.Length <> 19 Then Return False

        Dim Mod7 As Integer = 0
        For i As Integer = 0 To Body.Length - 1
            Dim C As Char = Body(i)
            If i = 4 OrElse i = 9 OrElse i = 14 Then
                If C <> "-"c Then Return False
                Continue For
            End If
            Dim V As Integer = TcCodeChars.IndexOf(C)
            If V < 0 Then Return False
            Mod7 = (Mod7 * 34 + V) Mod 7
        Next
        Return Mod7 = 0
    End Function

    ' ── 安装 / 更新 ───────────────────────────────────────────────────────────

    ''' <summary>下载并解压官方发行包，校验 SHA-512。返回提示文案。阻塞，需在后台线程调用。</summary>
    Friend Function TcInstall() As String
        Dim Urls As String() = {
            $"https://cnb.cool/HMCL-Terracotta/Terracotta/-/releases/download/v{TcVersion}/terracotta-{TcVersion}-windows-x86_64-pkg.tar.gz",
            $"https://gitee.com/burningtnt/Terracotta/releases/download/v{TcVersion}/terracotta-{TcVersion}-windows-x86_64-pkg.tar.gz",
            $"https://github.com/burningtnt/Terracotta/releases/download/v{TcVersion}/terracotta-{TcVersion}-windows-x86_64-pkg.tar.gz"
        }

        Directory.CreateDirectory(TcDir)
        Dim Pkg As String = IO.Path.Combine(TcDir, $"terracotta-{TcVersion}.tar.gz")
        Dim LastError As String = ""

        For Each Url As String In Urls
            Try
                Logger.Info($"陶瓦联机：开始下载 {Url}")
                Using Handler As New Net.Http.HttpClientHandler
                    ' 下载走系统代理（若有），只有本地回环才需要禁代理
                    Using Client As New Net.Http.HttpClient(Handler)
                        Client.Timeout = TimeSpan.FromMinutes(3)
                        Dim Bytes As Byte() = Client.GetByteArrayAsync(Url).GetAwaiter().GetResult()
                        File.WriteAllBytes(Pkg, Bytes)
                    End Using
                End Using
                If New FileInfo(Pkg).Length < 1000000 Then
                    LastError = "下载内容过小，可能被拦截"
                    Continue For
                End If
                Dim Sha As String = TcSha512(Pkg)
                If Not Sha.Equals(TcPackageSha512, StringComparison.OrdinalIgnoreCase) Then
                    LastError = $"校验值不符（得到 {Sha.Substring(0, 16)}…）"
                    Logger.Warn($"陶瓦联机：{LastError}，换下一个源")
                    Continue For
                End If
                TcExtractTarGz(Pkg, TcDir)
                File.Delete(Pkg)
                Dim Hit As String = Directory.GetFiles(TcDir, "terracotta*.exe").FirstOrDefault()
                If Hit Is Nothing Then Return "解压完成但没找到 exe，请重试安装或检查压缩包是否完整。"
                Logger.Info($"陶瓦联机：安装完成 {Hit}")
                Return $"陶瓦联机 {TcVersion} 已安装：{IO.Path.GetFileName(Hit)}"
            Catch ex As Exception
                LastError = ex.Message
                Logger.Warn(ex, $"陶瓦联机：从 {Url} 下载失败")
            End Try
        Next
        Return "安装失败：" & LastError & "。可点「打开下载页」手动下载后解压到 " & TcDir
    End Function

    Private Function TcSha512(Path As String) As String
        Using Fs As New FileStream(Path, FileMode.Open, FileAccess.Read)
            Using Sha As Security.Cryptography.SHA512 = Security.Cryptography.SHA512.Create()
                Dim Hash As Byte() = Sha.ComputeHash(Fs)
                Dim Sb As New Text.StringBuilder(Hash.Length * 2)
                For Each B As Byte In Hash
                    Sb.Append(B.ToString("x2"))
                Next
                Return Sb.ToString()
            End Using
        End Using
    End Function

    ''' <summary>解压 tar.gz（发行包里只有两三个平铺文件，不处理目录与链接）。</summary>
    Private Sub TcExtractTarGz(Gz As String, Dest As String)
        Using Fs As New FileStream(Gz, FileMode.Open, FileAccess.Read)
            Using Z As New IO.Compression.GZipStream(Fs, IO.Compression.CompressionMode.Decompress)
                Dim Header(511) As Byte
                Do
                    If TcReadFull(Z, Header, 512) < 512 Then Exit Do
                    If Header.All(Function(B) B = 0) Then Exit Do

                    Dim Name As String = TcTarString(Header, 0, 100)
                    Dim Size As Long = 0
                    Dim SizeText As String = TcTarString(Header, 124, 12).Trim()
                    If SizeText <> "" Then Size = Convert.ToInt64(SizeText, 8)
                    Dim TypeFlag As Byte = Header(156)
                    Dim IsFile As Boolean = (TypeFlag = 0 OrElse TypeFlag = AscW("0"c))

                    If IsFile AndAlso Name <> "" Then
                        Dim Out As String = IO.Path.Combine(Dest, IO.Path.GetFileName(Name))
                        Using Ofs As New FileStream(Out, FileMode.Create, FileAccess.Write)
                            Dim Left As Long = Size
                            Dim Buf(8191) As Byte
                            While Left > 0
                                Dim N As Integer = Z.Read(Buf, 0, CInt(Math.Min(Left, Buf.Length)))
                                If N <= 0 Then Exit While
                                Ofs.Write(Buf, 0, N)
                                Left -= N
                            End While
                        End Using
                    Else
                        Dim Left As Long = Size
                        Dim Buf(8191) As Byte
                        While Left > 0
                            Dim N As Integer = Z.Read(Buf, 0, CInt(Math.Min(Left, Buf.Length)))
                            If N <= 0 Then Exit While
                            Left -= N
                        End While
                    End If

                    Dim Pad As Integer = CInt((512 - (Size Mod 512)) Mod 512)
                    If Pad > 0 Then
                        Dim Tmp(Pad - 1) As Byte
                        TcReadFull(Z, Tmp, Pad)
                    End If
                Loop
            End Using
        End Using
    End Sub

    Private Function TcReadFull(S As Stream, Buf() As Byte, Count As Integer) As Integer
        Dim Total As Integer = 0
        While Total < Count
            Dim N As Integer = S.Read(Buf, Total, Count - Total)
            If N <= 0 Then Exit While
            Total += N
        End While
        Return Total
    End Function

    Private Function TcTarString(B() As Byte, Offset As Integer, Length As Integer) As String
        Dim Last As Integer = Offset + Length - 1
        While Last >= Offset AndAlso B(Last) = 0
            Last -= 1
        End While
        If Last < Offset Then Return ""
        Return Text.Encoding.UTF8.GetString(B, Offset, Last - Offset + 1)
    End Function

#End Region

#Region "重启启动器"

    ''' <summary>
    ''' 重启启动器：用带 --wait 参数起一个新实例（PCL 自带单例等待，会等当前实例退出），再关掉自己。
    ''' </summary>
    Friend Sub RestartLauncher()
        Try
            Dim MyPath As String = PathExe
            If String.IsNullOrWhiteSpace(MyPath) OrElse Not IO.File.Exists(MyPath) Then
                Hint("找不到启动器程序文件，请手动关闭后重新打开。", HintType.Red, False)
                Return
            End If
            Logger.Info("准备重启启动器")
            '用 cmd 延时 2 秒再拉起：等当前实例放开单例锁与日志文件，避免新实例抢锁失败自己退出
            Dim Q As Char = Chr(34)
            Dim CmdArgs As String = "/c ping 127.0.0.1 -n 3 >nul & start " & Q & Q & " " & Q & MyPath & Q
            Process.Start(New ProcessStartInfo("cmd.exe", CmdArgs) With {
                .CreateNoWindow = True,
                .UseShellExecute = False,
                .WindowStyle = ProcessWindowStyle.Hidden,
                .WorkingDirectory = IO.Path.GetDirectoryName(MyPath)
            })
            RunInUi(Sub() FrmMain.EndProgram(False))
        Catch ex As Exception
            Logger.Warn(ex, "重启启动器失败")
            Hint("重启失败，请手动关闭后重新打开启动器。", HintType.Red, False)
        End Try
    End Sub

    Private RestartCountdownTimer As DispatcherTimer
    Private Const RestartHintText As String = "改动需要重启启动器才会完全生效。" & vbCrLf & vbCrLf &
                                                  "点「立即重启生效」马上重启；点「取消」则等下次启动时生效。"

    ''' <summary>
    ''' 保存设置后统一走这里：弹一个 5 秒倒计时的提示框，「立即重启生效」可跳过等待，「取消」则放弃重启。
    ''' </summary>
    Friend Sub ApplyAndRestart(Message As String)
        RunInUi(
        Sub()
            Try
                If RestartCountdownTimer IsNot Nothing Then Return '已经有倒计时在跑，别叠
                Dim Seconds As Integer = 5
                RestartCountdownTimer = New DispatcherTimer(DispatcherPriority.Normal) With {
                    .Interval = TimeSpan.FromSeconds(1)
                }
                AddHandler RestartCountdownTimer.Tick,
                Sub(Sender As Object, E As EventArgs)
                    Try
                        Seconds -= 1
                        If Seconds <= 0 Then
                            StopRestartCountdown()
                            RestartLauncher()
                        Else
                            UpdateCountdownText($"{Message}，将在 {Seconds} 秒后自动重启生效。" & vbCrLf & RestartHintText)
                        End If
                    Catch ex As Exception
                        Logger.Warn(ex, "重启倒计时失败")
                        StopRestartCountdown()
                    End Try
                End Sub
                RestartCountdownTimer.Start()
                Dim Result As Integer = MyMsgBox(
                    $"{Message}，将在 5 秒后自动重启生效。" & vbCrLf & RestartHintText,
                    "重启生效", "立即重启生效", "取消",
                    Button1Action:=Sub() RestartLauncher())
                StopRestartCountdown()
                If Result = 2 Then Hint("已取消自动重启，改动将在下次启动启动器时生效。",, False)
            Catch ex As Exception
                Logger.Warn(ex, "显示重启提示失败")
                StopRestartCountdown()
            End Try
        End Sub)
    End Sub

    Private Sub StopRestartCountdown()
        Try
            If RestartCountdownTimer Is Nothing Then Return
            RestartCountdownTimer.Stop()
            RestartCountdownTimer = Nothing
        Catch ex As Exception
            Logger.Warn(ex, "停止重启倒计时失败")
        End Try
    End Sub

    ''' <summary>把已经弹出的消息框正文换成倒计时后的文本。</summary>
    Private Sub UpdateCountdownText(Text As String)
        Try
            If FrmMain Is Nothing OrElse FrmMain.PanMsg Is Nothing Then Return
            Dim Msg As MyMsgText = FrmMain.PanMsg.Children.OfType(Of MyMsgText)().LastOrDefault()
            If Msg Is Nothing Then Return
            Msg.LabCaption.Text = Text
        Catch ex As Exception
            Logger.Warn(ex, "刷新倒计时文本失败")
        End Try
    End Sub

#End Region

#Region "视频背景"

    Private Const KeyVideoEnabled As String = "UiVideoEnabled"
    Private Const KeyVideoPath As String = "UiVideoPath"
    ''' <summary>Windows 自带播放器能直接播的容器；MKV / WebM 不在其中。</summary>
    Private ReadOnly VideoExtensions As String() = {"mp4", "wmv", "avi", "mov", "m4v", "mpg", "mpeg"}
    Private VideoPlayer As System.Windows.Media.MediaPlayer = Nothing
    Private VideoIsPlaying As Boolean = False

    ''' <summary>视频背景开关，默认关闭——配置差的机器开着会掉帧。</summary>
    Friend Property VideoEnabled As Boolean
        Get
            Return Settings.Get(Of Boolean)(KeyVideoEnabled)
        End Get
        Set(Value As Boolean)
            Settings.Set(KeyVideoEnabled, Value)
        End Set
    End Property

    ''' <summary>视频文件完整路径；未配置为空串。</summary>
    Friend Property VideoPath As String
        Get
            Return If(Settings.Get(Of String)(KeyVideoPath), "").Trim()
        End Get
        Set(Value As String)
            Settings.Set(KeyVideoPath, If(Value, "").Trim())
        End Set
    End Property

    ''' <summary>开关打开、路径已配置且文件存在，才算真的在用视频背景。</summary>
    Friend ReadOnly Property VideoIsActive As Boolean
        Get
            Return VideoEnabled AndAlso VideoPath <> "" AndAlso IO.File.Exists(VideoPath)
        End Get
    End Property

    ''' <summary>设置页状态行用的说明文字。</summary>
    Friend ReadOnly Property VideoStatusText As String
        Get
            Dim Path As String = VideoPath
            If Not VideoEnabled Then Return "已关闭：视频背景默认不启用，勾上上面的开关并填写路径后才会播放。"
            If Path = "" Then Return "已开启，但还没有选择视频文件。"
            If Not IO.File.Exists(Path) Then Return "已开启，但文件不存在：" & Path
            Return "已开启：" & Path
        End Get
    End Property

    ''' <summary>
    ''' 导入视频时默认打开的文件夹。优先 Wallpaper Engine 的项目目录，
    ''' 其次是它的创意工坊订阅目录，最后退回「我的视频」。
    ''' </summary>
    Friend ReadOnly Property VideoDefaultDir As String
        Get
            Try
                For Each Folder In VideoWallpaperDirs()
                    If Folder <> "" AndAlso IO.Directory.Exists(Folder) Then Return Folder
                Next
                Dim LastDir As String = ""
                Try
                    If VideoPath <> "" Then LastDir = IO.Path.GetDirectoryName(VideoPath)
                Catch
                End Try
                If LastDir <> "" AndAlso IO.Directory.Exists(LastDir) Then Return LastDir
                Dim Videos As String = Environment.GetFolderPath(Environment.SpecialFolder.MyVideos)
                If IO.Directory.Exists(Videos) Then Return Videos
                Return Environment.GetFolderPath(Environment.SpecialFolder.MyPictures)
            Catch ex As Exception
                Logger.Warn(ex, "获取视频默认目录失败")
                Return ""
            End Try
        End Get
    End Property

    ''' <summary>所有 Steam 库目录（含 libraryfolders.vdf 里登记的其他盘）。</summary>
    Private Function SteamLibraries() As List(Of String)
        Dim Libraries As New List(Of String)
        Try
            Dim SteamPath As String = ""
            Try
                Using K = Microsoft.Win32.Registry.CurrentUser.OpenSubKey("Software\Valve\Steam")
                    If K IsNot Nothing Then SteamPath = If(TryCast(K.GetValue("SteamPath"), String), "").Replace("/"c, "\"c)
                End Using
                If SteamPath = "" Then
                    Using K = Microsoft.Win32.Registry.LocalMachine.OpenSubKey("SOFTWARE\WOW6432Node\Valve\Steam")
                        If K IsNot Nothing Then SteamPath = If(TryCast(K.GetValue("InstallPath"), String), "")
                    End Using
                End If
            Catch
            End Try
            If SteamPath <> "" Then Libraries.Add(SteamPath)
            If SteamPath <> "" Then
                Try
                    Dim Vdf As String = IO.Path.Combine(SteamPath, "steamapps\libraryfolders.vdf")
                    If IO.File.Exists(Vdf) Then
                        For Each Line In IO.File.ReadAllLines(Vdf)
                            Dim M = Text.RegularExpressions.Regex.Match(Line, """path""\s+""(.+?)""")
                            If M.Success Then Libraries.Add(M.Groups(1).Value.Replace("\\", "\"))
                        Next
                    End If
                Catch
                End Try
            End If
        Catch ex As Exception
            Logger.Warn(ex, "查找 Steam 目录失败")
        End Try
        Return Libraries
    End Function

    ''' <summary>Wallpaper Engine 的壁纸目录：自建项目在前，创意工坊订阅在后。</summary>
    Private Function VideoWallpaperDirs() As List(Of String)
        Dim Result As New List(Of String)
        Try
            For Each Library In SteamLibraries()
                Result.Add(IO.Path.Combine(Library, "steamapps\common\wallpaper_engine\projects\myprojects"))
                Result.Add(IO.Path.Combine(Library, "steamapps\workshop\content\431960"))
            Next
        Catch ex As Exception
            Logger.Warn(ex, "查找 Wallpaper Engine 目录失败")
        End Try
        Return Result
    End Function

    ''' <summary>在目录里递归找可播放的视频（最多三层、60 个）。</summary>
    Friend Function VideoScan(Folder As String) As List(Of String)
        Dim Result As New List(Of String)
        Try
            If Folder = "" OrElse Not IO.Directory.Exists(Folder) Then Return Result
            VideoScanInto(Folder, 0, Result)
        Catch ex As Exception
            Logger.Warn(ex, $"扫描视频失败：{Folder}")
        End Try
        Return Result
    End Function
    Private Sub VideoScanInto(Folder As String, Depth As Integer, Result As List(Of String))
        If Depth > 3 OrElse Result.Count >= 60 Then Return
        Try
            For Each File In IO.Directory.EnumerateFiles(Folder)
                Dim Ext As String = IO.Path.GetExtension(File).TrimStart("."c).ToLower()
                If VideoExtensions.Contains(Ext) Then
                    Result.Add(File)
                    If Result.Count >= 60 Then Return
                End If
            Next
        Catch
        End Try
        If Depth = 3 Then Return
        Try
            For Each SubDir In IO.Directory.EnumerateDirectories(Folder)
                VideoScanInto(SubDir, Depth + 1, Result)
                If Result.Count >= 60 Then Return
            Next
        Catch
        End Try
    End Sub

    ''' <summary>
    ''' 背景视频的统一入口。现在只有「PCL 自己播视频」一种实现；
    ''' 当初试过调用 Wallpaper Engine 的 -control openWallpaper -playInWindow 开窗，
    ''' 实测窗口尺寸与位置不受控、还要 WE 常驻，已移除，将来想接别的方案从这里进。
    ''' </summary>
    Friend Sub VideoBackgroundApply()
        Try
            VideoApply()
        Catch ex As Exception
            Logger.Error(ex, "应用视频背景失败")
        End Try
    End Sub

    ''' <summary>按设置启动视频背景；开关关闭、路径为空或文件不存在都会停掉。</summary>
    Friend Sub VideoApply()
        Try
            If Not VideoIsActive Then
                VideoStop()
                Return
            End If
            Dim Path As String = VideoPath
            RunInUi(Sub() VideoPlayUi(Path))
        Catch ex As Exception
            Logger.Error(ex, "应用视频背景失败")
        End Try
    End Sub

    Private Sub VideoPlayUi(Path As String)
        Try
            If FrmMain Is Nothing OrElse FrmMain.PanVideoBack Is Nothing Then Return
            If VideoPlayer Is Nothing Then
                VideoPlayer = New System.Windows.Media.MediaPlayer()
                VideoPlayer.IsMuted = True '只当背景，静音
                AddHandler VideoPlayer.MediaEnded, AddressOf VideoPlayerEnded
                AddHandler VideoPlayer.MediaFailed, AddressOf VideoPlayerFailed
                AddHandler VideoPlayer.MediaOpened, AddressOf VideoPlayerOpened
            End If
            VideoPlayer.Open(New Uri(Path, UriKind.Absolute))
            '窗口是 AllowsTransparency，MediaElement 不会渲染，所以用 MediaPlayer + VideoDrawing 画到画刷上
            Dim Drawing As New System.Windows.Media.VideoDrawing With {
                .Player = VideoPlayer, .Rect = New Rect(0, 0, 100, 100)}
            FrmMain.PanVideoBack.Background = New System.Windows.Media.DrawingBrush(Drawing) With {
                .Stretch = Stretch.UniformToFill}
            FrmMain.PanVideoBack.Visibility = Visibility.Visible
            VideoPlayer.Play()
            VideoIsPlaying = True
            Logger.Info($"视频背景已启动：{Path}")
        Catch ex As Exception
            Logger.Error(ex, $"启动视频背景失败：{Path}")
            RunInUi(Sub() Hint("视频背景启动失败，多半是格式不支持，详见日志。", HintType.Red))
        End Try
    End Sub

    ''' <summary>停止并释放视频背景。</summary>
    Friend Sub VideoStop()
        Try
            RunInUi(
            Sub()
                Try
                    If VideoPlayer IsNot Nothing Then
                        VideoPlayer.Stop()
                        VideoPlayer.Close()
                    End If
                Catch
                End Try
                VideoIsPlaying = False
                If FrmMain IsNot Nothing AndAlso FrmMain.PanVideoBack IsNot Nothing Then
                    FrmMain.PanVideoBack.Visibility = Visibility.Collapsed
                    FrmMain.PanVideoBack.Background = Nothing
                End If
            End Sub)
        Catch ex As Exception
            Logger.Warn(ex, "停止视频背景失败")
        End Try
    End Sub

    ''' <summary>窗口最小化时暂停播放，省一点资源。</summary>
    Friend Sub VideoPause(Paused As Boolean)
        Try
            If VideoPlayer Is Nothing OrElse Not VideoIsPlaying Then Return
            RunInUi(
            Sub()
                Try
                    If Paused Then VideoPlayer.Pause() Else VideoPlayer.Play()
                Catch
                End Try
            End Sub)
        Catch
        End Try
    End Sub

    ''' <summary>拿到视频真实尺寸后按 1:1 重画一遍，避免被二次缩放糊掉。</summary>
    Private Sub VideoPlayerOpened(sender As Object, e As EventArgs)
        Try
            If VideoPlayer Is Nothing OrElse FrmMain Is Nothing OrElse FrmMain.PanVideoBack Is Nothing Then Return
            Dim W As Double = VideoPlayer.NaturalVideoWidth
            Dim H As Double = VideoPlayer.NaturalVideoHeight
            If W <= 0 OrElse H <= 0 Then Return
            Dim Drawing As New System.Windows.Media.VideoDrawing With {
                .Player = VideoPlayer, .Rect = New System.Windows.Rect(0, 0, W, H)}
            FrmMain.PanVideoBack.Background = New System.Windows.Media.DrawingBrush(Drawing) With {
                .Stretch = System.Windows.Media.Stretch.UniformToFill}
            Logger.Info($"视频背景尺寸：{W}x{H}")
        Catch ex As Exception
            Logger.Warn(ex, "按视频尺寸重画背景失败")
        End Try
    End Sub

    ''' <summary>播完自动从头再来，做循环背景。</summary>
    Private Sub VideoPlayerEnded(sender As Object, e As EventArgs)
        Try
            If VideoPlayer Is Nothing OrElse Not VideoEnabled Then Return
            VideoPlayer.Position = TimeSpan.Zero
            VideoPlayer.Play()
        Catch ex As Exception
            Logger.Warn(ex, "循环播放视频背景失败")
        End Try
    End Sub

    Private Sub VideoPlayerFailed(sender As Object, e As System.Windows.Media.ExceptionEventArgs)
        Dim Message As String = If(e.ErrorException Is Nothing, "未知原因", e.ErrorException.Message)
        Logger.Error(e.ErrorException, $"视频背景播放失败：{Message}")
        RunInUi(Sub() Hint("视频背景播放失败：这个格式多半不支持，换成 H.264 的 MP4 试试。", HintType.Red))
    End Sub

#End Region

#Region "环境变量兼容修复"

    ''' <summary>
    ''' 清除环境块中「仅大小写不同」的重复环境变量。
    ''' 背景：Windows 允许 HTTPS_PROXY 与 https_proxy 同时存在（代理软件、容器注入常见），
    ''' 而 System.Diagnostics.ProcessStartInfo.EnvironmentVariables 是大小写不敏感的 StringDictionary，
    ''' 从 System.Environment.GetEnvironmentVariables() 复制时会抛 ArgumentException（已添加项），
    ''' 直接导致「Minecraft 启动失败：启动进程失败」。
    ''' 本方法只改当前进程的环境块，不写注册表、不影响其它程序。
    ''' </summary>
    Public Sub FixDuplicateEnvironmentVariables()
        Try
            Dim Seen As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)
            Dim Duplicates As New List(Of String)
            For Each Key As Object In System.Environment.GetEnvironmentVariables().Keys
                Dim Name As String = TryCast(Key, String)
                If Name Is Nothing Then Continue For
                If Not Seen.Add(Name) Then Duplicates.Add(Name)
            Next
            If Duplicates.Count = 0 Then Return
            For Each Name As String In Duplicates
                System.Environment.SetEnvironmentVariable(Name, Nothing)
            Next
            Logger.Info($"[自用修复] 已移除 {Duplicates.Count} 个大小写重复的环境变量：{String.Join(", ", Duplicates)}")
        Catch ex As Exception
            Logger.Error(ex, "FixDuplicateEnvironmentVariables 失败（已忽略，不影响继续启动）")
        End Try
    End Sub

#End Region

    ' ── 传输协议（自用分支，2026-10-29）────────────────────────────────────
    ' 是否允许旧版 TLS（1.0 / 1.1）。默认关闭；开启会在设置页二次确认。
    Friend ReadOnly Property LegacyTlsEnabled As Boolean
        Get
            Return Settings.Get(Of Boolean)("ToolDownloadLegacyTls")
        End Get
    End Property

    ' 按开关值设置 ServicePointManager 的协议集合。默认不启用 Ssl3 / TLS1.0。
    Friend Sub ApplySecurityProtocol()
        Try
            If LegacyTlsEnabled Then
                ' 只补 TLS 1.0/1.1；Ssl3 已不具备可用性，任何情况都不启用
                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls Or SecurityProtocolType.Tls11 Or SecurityProtocolType.Tls12
            ElseIf Environment.OSVersion.Version < New Version(6, 2) Then
                ' Win7 的系统默认可能过低，但也不回落到 Ssl3/TLS1.0
                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls11 Or SecurityProtocolType.Tls12
            End If
            Logger.Info($"传输协议：{(If(LegacyTlsEnabled, "已允许旧版 TLS（1.0/1.1）", "仅默认安全协议"))}")
        Catch ex As Exception
            Logger.Warn(ex, "设置 TLS 协议失败")
        End Try
    End Sub

    ' 清掉上次房间码（删除陶瓦 / 重置时用）。
    Friend Sub TcClearLastRoom()
        Settings.Set(KeyTcLastRoom, "")
    End Sub

End Module
