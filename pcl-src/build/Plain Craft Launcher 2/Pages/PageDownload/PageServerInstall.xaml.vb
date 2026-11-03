Imports Newtonsoft.Json.Linq

Public Class PageServerInstall

    Private SelectedVersion As String = ""
    Private SelectedLoader As String = "原版"
    Private VersionsLoaded As Boolean = False

    Private Sub PageServerInstall_Loaded(sender As Object, e As RoutedEventArgs) Handles Me.Loaded
        If VersionsLoaded Then Return
        VersionsLoaded = True
        RunInNewThread(Sub()
            Try
                Dim J As JObject = JObject.Parse(NetRequestByLoader("https://launchermeta.mojang.com/mc/game/version_manifest_v2.json", Timeout:=15000, IsJson:=True))
                Dim LatestSnap As String = J("latest")?("snapshot")?.ToString
                Dim LatestRel As String = J("latest")?("release")?.ToString
                Dim Items As New List(Of Tuple(Of String, String))
                For Each V As JToken In J("versions")
                    Dim Id As String = V("id")?.ToString
                    Dim Ty As String = V("type")?.ToString
                    If String.IsNullOrWhiteSpace(Id) OrElse Ty = "old_beta" OrElse Ty = "old_alpha" Then Continue For
                    Items.Add(Tuple.Create(Id, If(Ty, "")))
                Next
                RunInUi(Sub()
                    PanLatest.Children.Clear() : PanRelease.Children.Clear() : PanSnapshot.Children.Clear()
                    For Each It In Items
                        Dim Id As String = It.Item1, Ty As String = It.Item2
                        Dim Target As StackPanel = If(Ty = "release", PanRelease, PanSnapshot)
                        Target.Children.Add(MakeVersionItem(Id, Ty))
                        '最新版本卡片是「额外展示」，正式版/预览版里仍然保留这一条
                        If Id = LatestRel OrElse Id = LatestSnap Then PanLatest.Children.Add(MakeVersionItem(Id, Ty))
                    Next
                End Sub)
            Catch ex As Exception
                RunInUi(Sub() LabStatus.Text = "获取版本列表失败：" & ex.Message)
            End Try
        End Sub, "Server Version List")
    End Sub

    Private Function MakeVersionItem(Id As String, Ty As String) As MyListItem
        Dim Item As New MyListItem With {.Title = Id & If(Ty = "release", "（正式版）", "（预览版）"), .Tag = Id, .Height = 36, .MinPaddingRight = 35}
        AddHandler Item.MouseLeftButtonUp, Sub(s, a) ShowDetail(Id)
        Return Item
    End Function

    Private Sub ShowDetail(Id As String)
        SelectedVersion = Id
        LabDetailVersion.Text = "Minecraft " & Id
        LabStatus.Text = ""
        PanList.Visibility = Visibility.Collapsed
        PanDetail.Visibility = Visibility.Visible
        PanDetail.BringIntoView()
    End Sub

    Private Sub BtnBack_Click(sender As Object, e As MouseButtonEventArgs) Handles BtnBack.Click
        PanDetail.Visibility = Visibility.Collapsed
        PanList.Visibility = Visibility.Visible
    End Sub

    Private Sub BtnLoaderVanilla_Click(sender As Object, e As MouseButtonEventArgs) Handles BtnLoaderVanilla.Click
        SetLoader("原版")
    End Sub

    Private Sub BtnLoaderForge_Click(sender As Object, e As MouseButtonEventArgs) Handles BtnLoaderForge.Click
        SetLoader("Forge")
    End Sub

    Private Sub BtnLoaderNeoForge_Click(sender As Object, e As MouseButtonEventArgs) Handles BtnLoaderNeoForge.Click
        SetLoader("NeoForge")
    End Sub

    Private Sub BtnLoaderFabric_Click(sender As Object, e As MouseButtonEventArgs) Handles BtnLoaderFabric.Click
        SetLoader("Fabric")
    End Sub

    Private Sub BtnLoaderPaper_Click(sender As Object, e As MouseButtonEventArgs) Handles BtnLoaderPaper.Click
        SetLoader("Paper")
    End Sub
    Private Sub BtnLoaderPurpur_Click(sender As Object, e As MouseButtonEventArgs) Handles BtnLoaderPurpur.Click
        SetLoader("Purpur")
    End Sub
    Private Sub BtnLoaderFolia_Click(sender As Object, e As MouseButtonEventArgs) Handles BtnLoaderFolia.Click
        SetLoader("Folia")
    End Sub

    '''<summary>切换加载器：选中态 + 拉该加载器的版本列表。</summary>
    Private Sub SetLoader(Name As String)
        SelectedLoader = Name
        BtnLoaderVanilla.ColorType = If(Name = "原版", MyButton.ColorState.Highlight, MyButton.ColorState.Normal)
        BtnLoaderForge.ColorType = If(Name = "Forge", MyButton.ColorState.Highlight, MyButton.ColorState.Normal)
        BtnLoaderNeoForge.ColorType = If(Name = "NeoForge", MyButton.ColorState.Highlight, MyButton.ColorState.Normal)
        BtnLoaderFabric.ColorType = If(Name = "Fabric", MyButton.ColorState.Highlight, MyButton.ColorState.Normal)
        BtnLoaderPaper.ColorType = If(Name = "Paper", MyButton.ColorState.Highlight, MyButton.ColorState.Normal)
        BtnLoaderPurpur.ColorType = If(Name = "Purpur", MyButton.ColorState.Highlight, MyButton.ColorState.Normal)
        BtnLoaderFolia.ColorType = If(Name = "Folia", MyButton.ColorState.Highlight, MyButton.ColorState.Normal)
        If Name = "原版" Then
            ComboLoaderVersion.Visibility = Visibility.Collapsed
            LabLoaderVersionTitle.Visibility = Visibility.Collapsed
            LabLoaderHint.Text = "当前：原版服务端。"
            Return
        End If
        ComboLoaderVersion.Visibility = Visibility.Visible
        LabLoaderVersionTitle.Visibility = Visibility.Visible
        ComboLoaderVersion.Items.Clear()
        LabLoaderHint.Text = "正在获取 " & Name & " 版本…"
        RunInNewThread(Sub()
            Try
                Dim Ids As New List(Of String)
                If Name = "Fabric" Then
                    Dim Arr As JArray = JArray.Parse(NetRequestByLoader($"https://meta.fabricmc.net/v2/versions/loader/{SelectedVersion}", Timeout:=15000, IsJson:=True))
                    For Each V As JToken In Arr
                        Dim Id As String = V("loader")?("version")?.ToString
                        If Not String.IsNullOrWhiteSpace(Id) Then Ids.Add(Id)
                    Next
                ElseIf Name = "Forge" Then
                    'bmclapi 这个数组是**乱序**的（实测首条是 47.0.1、末条是 47.4.27），
                    '不排序就会把很老的版本摆在第一条，用户以为「最新就这个」
                    Dim Arr As JArray = JArray.Parse(NetRequestByLoader($"https://bmclapi2.bangbang93.com/forge/minecraft/{SelectedVersion}", Timeout:=15000, IsJson:=True))
                    For Each V As JToken In Arr
                        Dim Id As String = V("version")?.ToString
                        If Not String.IsNullOrWhiteSpace(Id) Then Ids.Add(Id)
                    Next
                ElseIf Name = "NeoForge" Then
                    'NeoForge 有两条线：
                    '  1.20.1 走 legacy（net/neoforged/forge，版本形如 1.20.1-47.1.106）
                    '  1.20.2 起走新线（net/neoforged/neoforge，版本形如 20.4.237，20.4 = MC 1.20.4）
                    If SelectedVersion.StartsWith("1.20.1") Then
                        Dim LegacyXml As String = NetRequestByLoader("https://bmclapi2.bangbang93.com/maven/net/neoforged/forge/maven-metadata.xml", Timeout:=15000)
                        For Each M As System.Text.RegularExpressions.Match In System.Text.RegularExpressions.Regex.Matches(LegacyXml, "<version>([^<]+)</version>")
                            Dim Id As String = M.Groups(1).Value.Trim()
                            If Id.StartsWith(SelectedVersion & "-") Then Ids.Add(Id)
                        Next
                    Else
                        Dim Xml As String = NetRequestByLoader("https://bmclapi2.bangbang93.com/maven/net/neoforged/neoforge/maven-metadata.xml", Timeout:=15000)
                        Dim Prefix As String = If(SelectedVersion.StartsWith("1."), SelectedVersion.Substring(2), SelectedVersion)
                        Dim All As New List(Of String)
                        For Each M As System.Text.RegularExpressions.Match In System.Text.RegularExpressions.Regex.Matches(Xml, "<version>([^<]+)</version>")
                            Dim Id As String = M.Groups(1).Value.Trim()
                            If Id = "" OrElse Id.Contains("beta") Then Continue For
                            All.Add(Id)
                            If Id.StartsWith(Prefix & ".") Then Ids.Add(Id)
                        Next
                        If Ids.Count = 0 Then Ids = All
                    End If
                ElseIf Name = "Paper" OrElse Name = "Folia" Then
                    '两者是同一套 fill v3 API，只有 project 名不同；列的是构建号
                    Dim Builds As JArray = JArray.Parse(NetRequestByLoader($"https://fill.papermc.io/v3/projects/{Name.ToLowerInvariant()}/versions/{SelectedVersion}/builds", Timeout:=20000, IsJson:=True))
                    For Each V As JToken In Builds.OrderByDescending(Function(x) Val(If(x("id")?.ToString, "0")))
                        Dim Id As String = V("id")?.ToString
                        If Not String.IsNullOrWhiteSpace(Id) Then Ids.Add(Id)
                    Next
                ElseIf Name = "Purpur" Then
                    'Purpur 用自己的 v2 API：builds.all 是升序的字符串数组
                    Dim J As JObject = JObject.Parse(NetRequestByLoader($"https://api.purpurmc.org/v2/purpur/{SelectedVersion}", Timeout:=20000, IsJson:=True))
                    Dim All = J("builds")?("all")
                    If All IsNot Nothing Then
                        For Each B As JToken In All
                            Dim Id As String = If(B, "").ToString.Trim
                            If Id <> "" Then Ids.Add(Id)
                        Next
                        Ids.Sort(AddressOf CompareVersionDesc)
                    End If
                Else
                    Throw New Exception("未知加载器")
                End If
                '统一按数字段倒序：Forge / NeoForge 的接口都是乱序的，
                '不排就会把旧版摆在第一条（实测 Forge 1.20.1 首条是 47.0.1，最新是 47.4.28）
                If Ids.Count > 1 Then Ids.Sort(AddressOf CompareVersionDesc)
                RunInUi(Sub()
                    ComboLoaderVersion.Items.Clear()
                    For Each Id In Ids
                        'Content 是给人看的中文/带前缀文本，Tag 存真正用于拼下载地址的版本号
                        ComboLoaderVersion.Items.Add(New MyComboBoxItem With {.Content = LoaderVersionLabel(Name, Id), .Tag = Id})
                    Next
                    If Ids.Count > 0 Then ComboLoaderVersion.SelectedIndex = 0
                    'Forge / NeoForge / Fabric 的安装器要从官方仓库拉依赖，国内直连基本必超时
                    Dim ProxyTip As String = If(Name = "Forge" OrElse Name = "NeoForge" OrElse Name = "Fabric",
                                                " 安装时要连官方仓库，国内建议先开加速器 / 代理。", "")
                    LabLoaderHint.Text = If(Ids.Count > 0,
                        $"已加载 {Ids.Count} 个 {Name} 版本（下拉选择）。" & ProxyTip,
                        $"{Name} 没有可用版本：这个 MC 版本可能不支持 {Name}，换 1.20.1 之类的版本再试。")
                End Sub)
            Catch ex As Exception
                RunInUi(Sub() LabLoaderHint.Text = "获取 " & Name & " 版本失败：" & ex.Message)
            End Try
        End Sub, "Loader Version")
    End Sub

    '''<summary>下拉里显示给人看的文本。真正用于拼下载地址的值放在 Tag 里，不要从这儿取。</summary>
    Private Function LoaderVersionLabel(Loader As String, Id As String) As String
        Select Case Loader
            Case "Paper", "Folia", "Purpur"
                '这些的“版本”其实是构建号，光一个 133 看不出是什么
                Return "构建 " & Id
            Case "NeoForge"
                'legacy 线（1.20.1-47.1.106）自带 MC 前缀；新线是裸的 21.1.265，补上前缀方便对照
                If Id.StartsWith("1.") AndAlso Id.Contains("-") Then Return Id
                Return SelectedVersion & "-" & Id
            Case Else
                Return Id
        End Select
    End Function

    '''<summary>取下拉里当前选中项的真实版本号（Tag），拿不到再退回显示文本。</summary>
    Private Function GetSelectedLoaderVersion() As String
        Dim Item As MyComboBoxItem = TryCast(ComboLoaderVersion.SelectedItem, MyComboBoxItem)
        If Item IsNot Nothing AndAlso Item.Tag IsNot Nothing Then Return CStr(Item.Tag).Trim()
        Return If(ComboLoaderVersion.Text, "").Trim()
    End Function

    '''<summary>按点分段做数字比较，倒序（新的在前）。非数字段按字符串比。</summary>
    '''<summary>
    ''' 版本号倒序比较（新的在前）。同时按 `.` 和 `-` 切段，这样
    ''' `47.4.28`、`1.20.1-47.1.106`、`20.4.237` 都能按数字比大小 ——
    ''' 只按 `.` 切的话 `1.20.1-47.1.106` 会退化成字符串比较，排序就乱了。
    ''' </summary>
    Private Function CompareVersionDesc(A As String, B As String) As Integer
        Dim Sep As Char() = {"."c, "-"c}
        Dim PA() As String = A.Split(Sep, StringSplitOptions.RemoveEmptyEntries)
        Dim PB() As String = B.Split(Sep, StringSplitOptions.RemoveEmptyEntries)
        Dim N As Integer = Math.Min(PA.Length, PB.Length)
        For i = 0 To N - 1
            Dim NA As Integer, NB As Integer
            Dim OA As Boolean = Integer.TryParse(PA(i), NA)
            Dim OB As Boolean = Integer.TryParse(PB(i), NB)
            If OA AndAlso OB Then
                If NA <> NB Then Return NB.CompareTo(NA)
            Else
                Dim C As Integer = String.CompareOrdinal(PB(i), PA(i))
                If C <> 0 Then Return C
            End If
        Next
        If PB.Length <> PA.Length Then Return PB.Length.CompareTo(PA.Length)
        '段数也一样时按整串倒序，保证顺序稳定（不会每次刷新换个排法）
        Return String.CompareOrdinal(B, A)
    End Function

    '''<summary>
    ''' 安装器退出码非 0 时统一处理：绝不能再提示"安装完成"，那种说法和事实不一致。
    ''' 从 installer 日志里捞一行异常摘要，因为绝大多数失败是连不上官方仓库。
    ''' </summary>
    Private Sub ReportInstallerFailure(LoaderName As String, ExitCode As Integer, Dir As String, LogPath As String)
        Dim Tail As String = SummarizeInstallerLog(LogPath)
        Dim Diag As String = DiagnoseInstallerLog(LogPath)
        LabStatus.Text = $"{LoaderName} 安装失败（退出码 {ExitCode}），目录：{Dir}" &
                         If(Diag = "", "", vbCrLf & Diag) & If(Tail = "", "", vbCrLf & Tail)
        Hint($"{LoaderName} 安装器报错了（退出码 {ExitCode}），目录里的文件不完整。" &
             If(Diag <> "", vbCrLf & Diag, "") &
             If(Tail <> "", vbCrLf & "日志摘要：" & Tail, "") & vbCrLf & vbCrLf &
             "安装器要直连官方仓库（maven.neoforged.net / maven.minecraftforge.net 等），" &
             "国内网络经常连不上 —— Mojang 的文件能下、这些仓库下不动，就是这个问题。" & vbCrLf & vbCrLf &
             "两个办法：" & vbCrLf &
             "① 开加速器 / 让系统代理生效后重试；" & vbCrLf &
             "② 在上面的「安装器代理」里填本机代理地址（如 127.0.0.1:7890），安装器就会走它。" & vbCrLf &
             "实在装不上就改用原版 / Paper：它们走镜像源，不需要代理。",
             HintType.Red, False)
    End Sub

    '''<summary>
    ''' 从安装器日志里定位「卡在哪个仓库」：找第一次下载失败之前最后一条
    ''' `Downloading library from https://host/...`，那个 host 就是元凶；顺带数失败次数。
    ''' 只报最后一行 "There was an error during installation" 等于没说。
    ''' </summary>
    Private Function DiagnoseInstallerLog(LogPath As String) As String
        Try
            If Not FileUtils.Exists(LogPath) Then Return ""
            Dim Lines As String() = FileUtils.ReadAsLines(LogPath)
            Dim LastHost As String = ""
            Dim FailHost As String = ""
            Dim Fails As Integer = 0
            For i = 0 To Lines.Length - 1
                Dim Line As String = If(Lines(i), "").Trim
                If Line.StartsWith("Downloading library from https://") Then
                    Dim Rest As String = Line.Substring("Downloading library from https://".Length)
                    Dim Slash As Integer = Rest.IndexOf("/"c)
                    LastHost = If(Slash > 0, Rest.Substring(0, Slash), Rest)
                ElseIf Line.Contains("SocketTimeoutException") OrElse Line.Contains("SocketException") OrElse
                       Line.Contains("UnknownHostException") OrElse Line.Contains("ConnectException") Then
                    Fails += 1
                    If FailHost = "" Then FailHost = LastHost
                End If
            Next
            If Fails = 0 Then Return ""
            Dim Where As String = If(FailHost <> "", "主要卡在 " & FailHost, "仓库不可达")
            Return $"下载依赖失败 {Fails} 次，{Where}。"
        Catch ex As Exception
            Logger.Warn(ex, "分析安装器日志失败")
            Return ""
        End Try
    End Function

    '''<summary>从 installer 日志末尾往前找一行异常/错误，用作失败摘要。</summary>
    Private Function SummarizeInstallerLog(LogPath As String) As String
        Try
            If Not FileUtils.Exists(LogPath) Then Return ""
            Dim Lines As String() = FileUtils.ReadAsLines(LogPath)
            For i = Lines.Length - 1 To Math.Max(0, Lines.Length - 500) Step -1
                Dim Line As String = If(Lines(i), "").Trim
                If Line = "" Then Continue For
                If Line.Contains("Exception") OrElse Line.StartsWith("java.net.") OrElse Line.Contains(" error") Then
                    If Line.Length > 160 Then Line = Line.Substring(0, 160) & "…"
                    Return Line
                End If
            Next
            Return ""
        Catch ex As Exception
            Logger.Warn(ex, "读取安装器日志失败：" & LogPath)
            Return ""
        End Try
    End Function

    '''<summary>当前选中的 Java 主版本号，拿不到返回 0。</summary>
    Private Function GetJavaMajor() As Integer
        Try
            If McLaunchJavaSelected IsNot Nothing AndAlso McLaunchJavaSelected.Version IsNot Nothing Then
                Return McLaunchJavaSelected.Version.Major
            End If
            Dim First = Configs.JavaList.Get().FirstOrDefault()
            If First IsNot Nothing AndAlso First.Version IsNot Nothing Then Return First.Version.Major
        Catch ex As Exception
            Logger.Warn(ex, "读取 Java 版本失败")
        End Try
        Return 0
    End Function

    '''<summary>该 MC 版本的服务端至少需要哪个 Java 主版本。0 = 不判断。</summary>
    Private Function RequiredJavaFor(McVer As String) As Integer
        Try
            Dim V As Version = Nothing
            If Not Version.TryParse(If(McVer, "").Split("-"c)(0).Trim, V) Then Return 0
            If V.Major >= 26 Then Return 21 '新命名（26.x）
            If V.Major = 1 Then
                If V.Minor >= 21 Then Return 21
                If V.Minor = 20 AndAlso V.Build >= 5 Then Return 21
                If V.Minor >= 17 Then Return 17
                Return 8
            End If
        Catch
        End Try
        Return 0
    End Function

    '''<summary>目录里已有哪些服务端痕迹，用于提示「再下一个会覆盖」。空串表示干净。</summary>
    Private Function DescribeExistingServer(Dir As String) As String
        Try
            Dim Cleaned As String = Dir.TrimEnd("\"c)
            Dim Marks As New List(Of String)
            For Each N In {"server.jar", "fabric-server-launch.jar", "run.bat", "forge-installer.jar", "neoforge-installer.jar"}
                If FileUtils.Exists(Cleaned & "\" & N) Then Marks.Add(N)
            Next
            If DirectoryUtils.Exists(Cleaned & "\libraries") Then Marks.Add("libraries\")
            Return Marks.Join("、")
        Catch
            Return ""
        End Try
    End Function

    '''<summary>首次下载时写一份能直接开服的 server.properties；已存在就绝不动。</summary>
    Private Sub EnsureServerProperties(Dir As String)
        Try
            Dim Path As String = Dir.TrimEnd("\"c) & "\server.properties"
            If FileUtils.Exists(Path) Then Return
            '不加注释：MC 用 ISO-8859-1 读这个文件，中文注释会变乱码（虽然不影响解析，但看着难受）
            'online-mode 必须是 false：本启动器服务端页的「加入时用」默认就是「离线登录（推荐）」，
            '写成 true 会和它对不上 —— 界面显示离线、服务端却要求正版，离线客户端直接被踢
            Dim Lines As String() = {
                "server-port=25565",
                "online-mode=false",
                "max-players=20",
                "view-distance=10",
                "simulation-distance=10",
                "difficulty=easy",
                "gamemode=survival",
                "motd=A Minecraft Server"}
            FileUtils.Write(Path, Lines.Join(vbCrLf) & vbCrLf, encoding:=New System.Text.UTF8Encoding(False))
        Catch ex As Exception
            Logger.Warn(ex, "写 server.properties 失败")
        End Try
    End Sub

    '''<summary>
    ''' 拼安装器的启动参数。带 <c>-Djava.net.useSystemProxies=true</c> 让 JVM 走系统代理 ——
    ''' 安装器内部要直连 Forge / NeoForge 官方仓库，不认代理的话国内网络必超时。
    ''' 没开代理时这个参数不生效，无副作用。
    ''' </summary>
    ''' <summary>
    ''' 拼安装器的 JVM 参数。`useSystemProxies` 只在系统代理真的开着时有用；
    ''' Forge / NeoForge 的仓库（maven.neoforged.net 等）国内常直接不可达，
    ''' 所以额外支持手填一个 HTTP 代理，写成 JVM 的 proxyHost/proxyPort 传进去。
    ''' </summary>
    Private Function BuildInstallerArgs(Jar As String, ExtraArgs As String) As String
        Dim Args As String = "-Djava.net.useSystemProxies=true "
        Try
            Dim Raw As String = If(Settings.Get(Of String)("ServerInstallerProxy"), "").Trim
            If Raw <> "" Then
                '允许写成 127.0.0.1:7890 / http://127.0.0.1:7890 两种
                Raw = Raw.Replace("http://", "").Replace("https://", "").TrimEnd("/"c)
                Dim Parts As String() = Raw.Split(":"c)
                Dim Host As String = Parts(0).Trim
                Dim Port As Integer
                If Host <> "" AndAlso Parts.Length >= 2 AndAlso Integer.TryParse(Parts(1).Trim, Port) AndAlso Port > 0 Then
                    Args &= $"-Dhttp.proxyHost={Host} -Dhttp.proxyPort={Port} -Dhttps.proxyHost={Host} -Dhttps.proxyPort={Port} "
                Else
                    Logger.Warn("安装器代理格式不对，已忽略：" & Raw)
                End If
            End If
        Catch ex As Exception
            Logger.Warn(ex, "读取安装器代理设置失败")
        End Try
        Return Args & $"-jar ""{Jar}"" " & ExtraArgs
    End Function

    '''<summary>按物理内存推荐服务端内存（MB）：取 1/4，夹在 2G–8G，按 512M 取整。</summary>
    Public Shared Function SuggestServerMemoryMb() As Integer
        Try
            Dim TotalGb As Double = My.Computer.Info.TotalPhysicalMemory / 1024.0 / 1024.0 / 1024.0
            Dim Mb As Integer = CInt(Math.Round(TotalGb * 1024 / 4 / 512) * 512)
            Return Math.Max(2048, Math.Min(8192, Mb))
        Catch ex As Exception
            Logger.Warn(ex, "读取物理内存失败")
            Return 4096
        End Try
    End Function

    '''<summary>
    ''' Forge / NeoForge 生成的 user_jvm_args.txt 把 -Xmx 注释掉了，用户不自己改就只能跑默认值。
    ''' 按机器内存写一行进去；已经手动设过就绝不动。
    ''' </summary>
    Public Shared Sub EnsureServerJvmArgs(Dir As String)
        Try
            Dim P As String = Dir.TrimEnd("\"c) & "\user_jvm_args.txt"
            If Not FileUtils.Exists(P) Then Return
            Dim Lines As String() = FileUtils.ReadAsLines(P)
            Dim HasXmx As Boolean = False
            For Each L In Lines
                If If(L, "").TrimStart().StartsWith("-Xmx") Then HasXmx = True '已经设过，不覆盖
            Next
            Dim Mb As Integer = SuggestServerMemoryMb()
            Dim Out As New List(Of String)
            Dim XmxWritten As Boolean = HasXmx
            For Each L In Lines
                Dim T As String = If(L, "").Trim()
                '把模板里那行 "# -Xmx4G" 换成实际值
                If Not XmxWritten AndAlso T.StartsWith("#") AndAlso T.Contains("-Xmx") Then
                    Out.Add($"-Xmx{Mb}M")
                    XmxWritten = True
                Else
                    Out.Add(L)
                End If
            Next
            If Not XmxWritten Then Out.Add($"-Xmx{Mb}M") '模板里没有示例行就自己加一条
            '编码统一 UTF-8：run.bat 走这个文件，不设的话控制台发中文命令会乱码
            For Each E In {"-Dfile.encoding=UTF-8", "-Dstdout.encoding=UTF-8", "-Dstderr.encoding=UTF-8",
                           "-Dsun.stdout.encoding=UTF-8", "-Dsun.stderr.encoding=UTF-8", "-Dsun.stdin.encoding=UTF-8"}
                If Not Out.Any(Function(L) If(L, "").Trim().Equals(E, StringComparison.OrdinalIgnoreCase)) Then Out.Add(E)
            Next
            FileUtils.Write(P, Out.Join(vbCrLf) & vbCrLf, encoding:=New System.Text.UTF8Encoding(False))
            Logger.Info($"已写入 user_jvm_args.txt：-Xmx{Mb}M + UTF-8 编码参数")
        Catch ex As Exception
            Logger.Warn(ex, "写 user_jvm_args.txt 失败")
        End Try
    End Sub

    Private Sub BtnInstall_Click(sender As Object, e As MouseButtonEventArgs) Handles BtnInstall.Click
        If SelectedVersion = "" Then
            Hint("先选一个版本。", HintType.Red, False)
            Return
        End If
        '防重入：点一次立刻禁用。原先禁用放在选目录之后，双击会走两遍完整流程，表现为「下载两次原版」
        If Not BtnInstall.IsEnabled Then Return
        BtnInstall.IsEnabled = False
        '先判断 Java 够不够，不然选完目录、下完文件才在安装器那一步失败，等于白折腾
        Dim NeedJava As Integer = RequiredJavaFor(SelectedVersion)
        Dim JavaMajor As Integer = GetJavaMajor()
        If NeedJava > 0 AndAlso JavaMajor > 0 AndAlso JavaMajor < NeedJava Then
            If MyMsgBox($"当前列表里第一个 Java 是 {JavaMajor}，Minecraft {SelectedVersion} 的服务端至少要 Java {NeedJava}。" & vbCrLf & vbCrLf &
                        "继续的话安装器基本跑不起来。两个办法：" & vbCrLf &
                        $"· 把那个低版本从「设置 → 启动 → Java」里去掉，安装时会自动下载一个 Java {NeedJava}" & vbCrLf &
                        "· 或者自己装一个够新的加进列表",
                        "Java 版本偏低", "仍然继续", "先去换 Java", IsWarn:=True) <> 1 Then
                BtnInstall.IsEnabled = True
                Return
            End If
        End If
        Dim F As String = Dialogs.SelectFolder("选择服务端保存目录", False).FirstOrDefault()
        If String.IsNullOrWhiteSpace(F) Then
            BtnInstall.IsEnabled = True
            Return
        End If
        'SelectFolder 返回的路径带尾随反斜杠（Dialogs 里 AddSlashSuffix 加的），必须去掉。
        '不去掉会连锁出三个问题：
        '① F & 反斜杠 & server.jar 会拼成双斜杠，FileUtils.Exists 判为不存在 -> 每次重下原版；
        '② FileUtils.Delete 内部用 extended-length 前缀，双斜杠在那种路径里非法
        '   -> 报「文件名、目录名或卷标语法不正确」；
        '③ 命令行里的尾反斜杠会转义后面的引号，安装器读到 D:/xxx 带引号 -> Illegal char。
        F = F.TrimEnd("\"c)
        '所有服务端都写 server.jar，同一个目录放两个会互相覆盖（Forge 和 Paper 混在一起那种）
        Dim ExistingMark As String = DescribeExistingServer(F)
        If ExistingMark <> "" Then
            If MyMsgBox($"这个目录里已经有服务端相关文件：{vbCrLf}  {ExistingMark}{vbCrLf}{vbCrLf}" &
                        "继续会覆盖 server.jar，不同加载器的文件也可能混在一起。" & vbCrLf &
                        "建议每个服务端放一个单独的目录。",
                        "目录里已有服务端", "继续覆盖", "换个目录", IsWarn:=True) <> 1 Then
                BtnInstall.IsEnabled = True
                Return
            End If
        End If
        DirectoryUtils.Create(F)
        Dim Ver As String = SelectedVersion
        LabStatus.Text = "正在获取版本信息…"
        Dim Loader As String = SelectedLoader
        Dim LoaderVer As String = GetSelectedLoaderVersion()
        If Loader <> "原版" AndAlso LoaderVer = "" Then
            Hint("先在「加载器版本」下拉里选一个版本。", HintType.Red, False)
            BtnInstall.IsEnabled = True
            Return
        End If
        RunInNewThread(Sub()
            Try
                If Loader = "Forge" Then
                    InstallForge(Ver, LoaderVer, F)
                    Return
                ElseIf Loader = "NeoForge" Then
                    InstallNeoForge(Ver, LoaderVer, F)
                    Return
                ElseIf Loader = "Fabric" Then
                    InstallFabric(Ver, LoaderVer, F)
                    Return
                ElseIf Loader = "Paper" OrElse Loader = "Folia" OrElse Loader = "Purpur" Then
                    InstallPaperFamily(Ver, LoaderVer, F, Loader.ToLowerInvariant())
                    Return
                End If
                Dim Manifest As String = NetRequestByLoader("https://launchermeta.mojang.com/mc/game/version_manifest_v2.json", Timeout:=15000, IsJson:=True)
                Dim J As JObject = JObject.Parse(Manifest)
                Dim Url As String = Nothing
                For Each V As JToken In J("versions")
                    If V("id")?.ToString = Ver Then
                        Url = V("url")?.ToString
                        Exit For
                    End If
                Next
                If String.IsNullOrWhiteSpace(Url) Then Throw New Exception("找不到版本 " & Ver)
                Dim VJ As JObject = JObject.Parse(NetRequestByLoader(Url, Timeout:=15000, IsJson:=True))
                Dim ServerUrl As String = VJ("downloads")?("server")?("url")?.ToString
                If String.IsNullOrWhiteSpace(ServerUrl) Then Throw New Exception("该版本没有官方服务端")
                '走镜像源（与 PCL 其他下载一致），直连 Mojang 在本机经常失败
                Dim ServerUrls As IEnumerable(Of String) = DlSourceLauncherOrMetaGet(ServerUrl)
                '统一用 server.jar 命名：安装器准备（PrepareVanillaForInstaller）、Forge / Fabric 都认这个名字
                Dim Target As String = F & "\server.jar"
                RunInUi(Sub() LabStatus.Text = "正在下载服务端…")
                Dim Combo As LoaderCombo(Of String) = Nothing
                '与百宝箱一致：Loader 的创建 / 启动 / 登记全部在 UI 线程完成，否则任务栏收不到
                RunInUiWait(Sub()
                    Dim Dl As New LoaderDownload("下载服务端 " & Ver, New List(Of NetFile) From {New NetFile(ServerUrls, Target)}) With {.ProgressWeight = 5}
                    Combo = New LoaderCombo(Of String)("服务端 " & Ver, {Dl}) With {.OnStateChanged = AddressOf LoaderStateChangedHintOnly}
                    Combo.Start()
                    LoaderTaskbarAdd(Combo)
                    FrmMain.BtnExtraDownload.ShowRefresh()
                    FrmMain.PageChange(FormMain.PageType.TaskManager)
                End Sub)
                Do While Combo.State.ToString() = "Loading"
                    System.Threading.Thread.Sleep(200)
                Loop
                If Combo.State.ToString() = "Cancelled" Then
                    System.Threading.Thread.Sleep(1500) '等 Loader 网络线程收尾，避免文件仍被占用
                    RunInUi(Sub()
                        LabStatus.Text = "已取消下载。"
                        FrmMain.PageChange(FormMain.PageType.Download, FormMain.PageSubType.DownloadServer)
                    End Sub)
                    Return
                End If
                If Combo.State.ToString() <> "Finished" Then
                    '多线程失败：改用普通下载重试一次
                    RunInUi(Sub() LabStatus.Text = "多线程下载失败，改用普通下载重试…")
                    Try
                        NetDownloadByLoader(ServerUrls, Target)
                    Catch ex2 As Exception
                        Logger.Error(ex2, "服务端下载失败：" & String.Join(" | ", ServerUrls))
                        Throw New Exception("下载失败（已重试普通下载）：" & ex2.Message)
                    End Try
                End If
                FileUtils.Write(F & "\eula.txt", "eula=true" & vbCrLf, encoding:=New System.Text.UTF8Encoding(False))
                EnsureServerProperties(F)
                WriteServerMeta(F, Ver, "", "")
                RunInUi(Sub()
                    LabStatus.Text = "已下载：" & Target
                    AddServerRecord(F, Ver)
                    Hint("服务端已下载到 " & Target & "，eula 和 server.properties 已写好。" & vbCrLf &
                         "回联机页点「开服」就能启动（它已经记为当前服务端）。", HintType.Green, False)
                    FrmMain.PageChange(FormMain.PageType.Download, FormMain.PageSubType.DownloadServer)
                End Sub)
            Catch ex As InstallCanceledException
                RunInUi(Sub() LabStatus.Text = "已取消")
            Catch ex As Exception
                RunInUi(Sub() LabStatus.Text = "下载失败：" & ex.Message)
            Finally
                RunInUi(Sub() BtnInstall.IsEnabled = True)
            End Try
        End Sub, "Server Download")
    End Sub

    ' ── 已安装服务端 ─────────────────────────────────
    Public Shared Sub AddServerRecord(Dir As String, Ver As String)
        Try
            Dim Raw As String = If(Settings.Get(Of String)("LinkServerList"), "")
            Dim Arr As JArray = If(String.IsNullOrWhiteSpace(Raw), New JArray, JArray.Parse(Raw))
            Dim Kept As New JArray
            For Each It As JToken In Arr
                If It("dir")?.ToString <> Dir Then Kept.Add(It)
            Next
            Kept.Add(New JObject From {{"dir", Dir}, {"ver", Ver}, {"time", Now.ToString("yyyy-MM-dd HH:mm")}})
            Settings.Set("LinkServerList", Kept.ToString(Newtonsoft.Json.Formatting.None))
            '刚操作过的这个就是「当前服务端」：联机页的配置 / 白名单 / 开服都按它算，
            '不必再要求用户手动去挑一次 server.jar（Forge 那种没有 server.jar 的目录挑不出来）
            Settings.Set("LinkServerDir", Dir.TrimEnd("\"c))
        Catch ex As Exception
            Logger.Warn(ex, "记录已安装服务端失败")
        End Try
    End Sub

    ''' <summary>
    ''' 在服务端目录里写一份 `server.json`（MC 版本 / 加载器 / 加载器版本）。
    ''' 这是「服务端实例」的第一块地基：目录里能自报家门，联机页就不用再从记录字符串里猜，
    ''' 换机、换启动器、别人拷过来的目录也认得出。
    ''' </summary>
    Public Shared Sub WriteServerMeta(Dir As String, McVer As String, Loader As String, LoaderVer As String)
        Try
            Dim CleanDir As String = Dir.TrimEnd("\"c)
            If Not DirectoryUtils.Exists(CleanDir) Then Return
            Dim O As New JObject From {
                {"mc", McVer}, {"loader", Loader}, {"loaderVer", LoaderVer}, {"updated", Now.ToString("yyyy-MM-dd HH:mm")}}
            FileUtils.Write(CleanDir & "\server.json", O.ToString(Newtonsoft.Json.Formatting.Indented) & vbCrLf,
                            encoding:=New System.Text.UTF8Encoding(False))
        Catch ex As Exception
            Logger.Warn(ex, "写 server.json 失败")
        End Try
    End Sub

    ''' <summary>读目录里的 `server.json`；没有或读不出来返回 Nothing（别硬猜）。</summary>
    Public Shared Function ReadServerMeta(Dir As String) As Tuple(Of String, String, String)
        Try
            If String.IsNullOrWhiteSpace(Dir) Then Return Nothing
            Dim P As String = Dir.TrimEnd("\"c) & "\server.json"
            If Not FileUtils.Exists(P) Then Return Nothing
            Dim O As JObject = JObject.Parse(System.IO.File.ReadAllText(P))
            Dim Mc As String = If(O("mc")?.ToString, "").Trim()
            If Mc = "" Then Return Nothing
            Return New Tuple(Of String, String, String)(Mc, If(O("loader")?.ToString, "").Trim(), If(O("loaderVer")?.ToString, "").Trim())
        Catch ex As Exception
            Logger.Warn(ex, "读 server.json 失败")
        End Try
        Return Nothing
    End Function

    Private Sub RemoveServer(Dir As String, Ver As String)
        If MyMsgBox($"确定删除服务端「{Ver}」吗？{vbCrLf}{Dir}{vbCrLf}{vbCrLf}会删除该目录下的 server.jar、eula.txt、world 等全部文件，无法恢复。", "删除服务端", "删除", "取消", IsWarn:=True) <> 1 Then Return
        Try
            If DirectoryUtils.Exists(Dir) Then DirectoryUtils.Delete(Dir)
            Dim Raw As String = If(Settings.Get(Of String)("LinkServerList"), "")
            Dim Arr As JArray = If(String.IsNullOrWhiteSpace(Raw), New JArray, JArray.Parse(Raw))
            Dim Kept As New JArray
            For Each It As JToken In Arr
                If It("dir")?.ToString <> Dir Then Kept.Add(It)
            Next
            Settings.Set("LinkServerList", Kept.ToString(Newtonsoft.Json.Formatting.None))
            Hint("已删除服务端目录。", HintType.Green, False)
        Catch ex As Exception
            Logger.Error(ex, "删除服务端失败")
            Hint("删除失败：" & ex.Message, HintType.Red, False)
        End Try
    End Sub

    '''<summary>带任务栏进度地下载一个文件；失败时普通下载重试一次。</summary>
    Private Sub DownloadWithTaskbar(Urls As IEnumerable(Of String), Target As String, Title As String)
        Dim Combo As LoaderCombo(Of String) = Nothing
        RunInUiWait(Sub()
            Dim Dl As New LoaderDownload(Title, New List(Of NetFile) From {New NetFile(Urls, Target)}) With {.ProgressWeight = 5}
            Combo = New LoaderCombo(Of String)(Title, {Dl}) With {.OnStateChanged = AddressOf LoaderStateChangedHintOnly}
            Combo.Start()
            LoaderTaskbarAdd(Combo)
            FrmMain.BtnExtraDownload.ShowRefresh()
            FrmMain.PageChange(FormMain.PageType.TaskManager)
        End Sub)
        Do While Combo.State.ToString() = "Loading"
            System.Threading.Thread.Sleep(200)
        Loop
        If Combo.State.ToString() = "Cancelled" OrElse Combo.State.ToString() = "Canceled" Then Throw New InstallCanceledException()
        If Combo.State.ToString() <> "Finished" Then NetDownloadByLoader(Urls, Target)
    End Sub

    '''<summary>解析原版服务端下载地址（已换成镜像源）。</summary>
    Private Function GetVanillaServerUrls(McVer As String) As IEnumerable(Of String)
        Dim Manifest As String = NetRequestByLoader("https://launchermeta.mojang.com/mc/game/version_manifest_v2.json", Timeout:=15000, IsJson:=True)
        Dim J As JObject = JObject.Parse(Manifest)
        Dim Url As String = Nothing
        For Each V As JToken In J("versions")
            If V("id")?.ToString = McVer Then
                Url = V("url")?.ToString
                Exit For
            End If
        Next
        If String.IsNullOrWhiteSpace(Url) Then Throw New Exception("找不到版本 " & McVer)
        Dim VJ As JObject = JObject.Parse(NetRequestByLoader(Url, Timeout:=15000, IsJson:=True))
        Dim ServerUrl As String = VJ("downloads")?("server")?("url")?.ToString
        If String.IsNullOrWhiteSpace(ServerUrl) Then Throw New Exception("该版本没有官方服务端")
        Return DlSourceLauncherOrMetaGet(ServerUrl)
    End Function

    '''<summary>
    ''' 跑安装器前把原版准备好：`server.jar` 复制成 `minecraft_server.&lt;版本&gt;.jar`
    ''' （Forge / NeoForge 只认这个名字），再写 eula。
    ''' 预置好这两样，安装器才不会自己去 Mojang 下 —— 国内下不动，表现就是「装完只剩 installer」。
    ''' </summary>
    Private Sub PrepareVanillaForInstaller(McVer As String, Dir As String)
        Dim CleanDir As String = Dir.TrimEnd("\"c)
        Dim Target As String = CleanDir & "\server.jar"
        If Not FileUtils.Exists(Target) Then Throw New Exception("原版 server.jar 没下成功")
        Dim ForgeName As String = CleanDir & "\minecraft_server." & McVer & ".jar"
        If Not FileUtils.Exists(ForgeName) Then
            Try
                FileUtils.Copy(Target, ForgeName)
                Logger.Info("已准备 " & IO.Path.GetFileName(ForgeName) & "，安装器不会再下原版")
            Catch ex As Exception
                Logger.Warn(ex, "复制 minecraft_server 副本失败")
            End Try
        End If
        FileUtils.Write(CleanDir & "\eula.txt", "eula=true" & vbCrLf, encoding:=New System.Text.UTF8Encoding(False))
    End Sub

    '''<summary>
    ''' 一条任务干完「下原版 → 下安装器 → 静默安装」三步，共用同一个任务栏条目。
    ''' 安装步骤也计入进度（权重最大）：否则进度条一到 100% 用户就以为装完了，
    ''' 实际安装器还在联网拉依赖，很容易以为「下载好了」直接去开服。
    ''' ExpectedInstallSeconds：安装器不吐进度，只能按预估时间给个单调递增的进度。
    ''' </summary>
    Private Sub InstallLoaderServer(Title As String, McVer As String, Dir As String,
                                    InstallerUrl As String, InstallerPath As String,
                                    RunInstaller As Action(Of LoaderTask(Of String, String)),
                                    SuccessCheck As Func(Of Boolean),
                                    Optional ExpectedInstallSeconds As Double = 120)
        Dim CleanDir As String = Dir.TrimEnd("\"c)
        DirectoryUtils.Create(CleanDir)
        Dim VanillaPath As String = CleanDir & "\server.jar"
        Dim NeedVanilla As Boolean = Not FileUtils.Exists(VanillaPath)
        'URL 要先解析出来（这一步是网络请求），但真正的下载放进任务里
        Dim VanillaUrls As IEnumerable(Of String) = Nothing
        If NeedVanilla Then
            RunInUi(Sub() LabStatus.Text = "正在解析原版服务端地址…")
            VanillaUrls = GetVanillaServerUrls(McVer)
        End If
        Dim Combo As LoaderCombo(Of String) = Nothing
        RunInUiWait(Sub()
            LabStatus.Text = "正在下载并安装（最后一步的进度按预计时间显示，装完才算完成）…"
            Dim Loaders As New List(Of LoaderBase)
            If NeedVanilla Then
                Loaders.Add(New LoaderDownload("下载原版服务端 " & McVer,
                    New List(Of NetFile) From {New NetFile(VanillaUrls, VanillaPath)}) With {.ProgressWeight = 60})
            End If
            Loaders.Add(New LoaderDownload("下载 " & Title & " 安装器",
                New List(Of NetFile) From {New NetFile({InstallerUrl}, InstallerPath)}) With {.ProgressWeight = 5})
            Loaders.Add(New LoaderTask(Of String, String)("安装 " & Title,
                Sub(Task As LoaderTask(Of String, String))
                    RunInstaller.Invoke(Task)
                    If Not SuccessCheck.Invoke() Then
                        Throw New Exception("安装器没有产出可启动的文件（run.bat / fabric-server-launch.jar 都不在）")
                    End If
                End Sub) With {.ProgressWeight = 120})
            '失败由调用方的 Catch 统一报（带日志摘要），这里再弹一次就成了两条内容不同的错
            Combo = New LoaderCombo(Of String)(Title, Loaders)
            Combo.Start()
            LoaderTaskbarAdd(Combo)
            FrmMain.BtnExtraDownload.ShowRefresh()
            FrmMain.PageChange(FormMain.PageType.TaskManager)
        End Sub)
        Do While Combo.State.ToString() = "Loading"
            System.Threading.Thread.Sleep(200)
        Loop
        If Combo.State.ToString() = "Cancelled" OrElse Combo.State.ToString() = "Canceled" Then Throw New InstallCanceledException()
        If Combo.State.ToString() <> "Finished" Then
            Throw New Exception(If(Combo.[Error] IsNot Nothing, Combo.[Error].Message, "安装未完成"))
        End If
    End Sub

    '''<summary>
    ''' 等安装器退出，同时按**时间**推进进度。
    ''' 安装器没有进度输出（LoaderTask 的 Progress 默认 -1，界面上就是一直 0，看着像卡死），
    ''' 所以按预估耗时给一条先快后慢的曲线，封顶 95%，真正结束再给 100%。
    ''' 时间不准没关系 —— 它只回答「还在动」，不回答「还剩几秒」。
    ''' </summary>
    Private Sub WaitInstallerWithProgress(P As System.Diagnostics.Process,
                                          Task As LoaderTask(Of String, String),
                                          ExpectedSeconds As Double, TimeoutMs As Integer)
        Dim StartAt As Date = Now
        Do While Not P.HasExited
            Dim Elapsed As Double = (Now - StartAt).TotalSeconds
            Dim F As Double
            If ExpectedSeconds <= 0 Then
                F = 0.5
            ElseIf Elapsed <= ExpectedSeconds * 0.3 Then
                '前 30% 时间走完 70% 进度：预编译/解包阶段动得快
                F = Elapsed / (ExpectedSeconds * 0.3) * 0.7
            Else
                '之后按剩余时间慢慢逼近 95%
                F = 0.7 + Math.Min((Elapsed - ExpectedSeconds * 0.3) / (ExpectedSeconds * 0.7), 1) * 0.25
            End If
            Try
                Task.Progress = Math.Min(F, 0.95)
            Catch
            End Try
            If (Now - StartAt).TotalMilliseconds > TimeoutMs Then Exit Do
            System.Threading.Thread.Sleep(250)
        Loop
        Try
            Task.Progress = 1
        Catch
        End Try
    End Sub
    ' ── 镜像自建安装：不依赖官方 maven ────────────────────────
    ' 背景：maven.neoforged.net / maven.minecraftforge.net 在国内常不可达，而安装器
    ' 内部写死了这些绝对 URL（`--mirror` 参数不改写 version json 里的库地址）。
    ' 做法：把安装器当模板 —— 取出它自带的 install_profile.json / version.json / data/，
    ' 库全部从 bmclapi 镜像下，再用它自己的 installertools 在本地跑完补丁流程。
    ' 2026-11-02 手工验证：NeoForge 26.3.0.101 用这条路能正常起服（Done in 3s）。

    '''<summary>官方 maven 的地址换成 bmclapi 镜像；已经是镜像或其它地址的原样返回。</summary>
    Private Shared Function MirrorMavenUrl(Url As String) As String
        If String.IsNullOrWhiteSpace(Url) Then Return Url
        For Each Prefix In {"https://maven.neoforged.net/releases/", "https://maven.minecraftforge.net/", "https://libraries.minecraft.net/"}
            If Url.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase) Then
                Return "https://bmclapi2.bangbang93.com/maven/" & Url.Substring(Prefix.Length)
            End If
        Next
        Return Url
    End Function

    '''<summary>maven 坐标 group:artifact:version[:classifier][@ext] → libraries 下的相对路径。</summary>
    Private Shared Function MavenCoordToPath(Coord As String) As String
        Dim S As String = If(Coord, "").Trim
        Dim Ext As String = "jar"
        Dim At As Integer = S.LastIndexOf("@"c)
        If At > 0 Then
            Ext = S.Substring(At + 1).Trim.ToLowerInvariant()
            S = S.Substring(0, At)
        End If
        Dim Parts As String() = S.Split(":"c)
        If Parts.Length < 3 Then Return ""
        Dim G As String = Parts(0).Replace("."c, "/"c)
        Dim A As String = Parts(1)
        Dim V As String = Parts(2)
        Dim C As String = If(Parts.Length >= 4, "-" & Parts(3), "")
        Return $"{G}/{A}/{V}/{A}-{V}{C}.{Ext}"
    End Function

    '''<summary>
    ''' 解析 install_profile.data 里的一个值，得到实际路径：
    ''' `[group:artifact:version:classifier@ext]` → libraries 下；`/data/x.lzma` → 解出来的 data 目录；
    ''' `'文本'` → 去引号。
    ''' </summary>
    Private Shared Function ResolveDataValue(Raw As String, Dir As String) As String
        Dim S As String = If(Raw, "").Trim
        If S = "" Then Return ""
        If S.StartsWith("'") AndAlso S.EndsWith("'") AndAlso S.Length >= 2 Then Return S.Substring(1, S.Length - 2)
        If S.StartsWith("[") AndAlso S.EndsWith("]") Then
            Return Dir & "\libraries\" & MavenCoordToPath(S.Substring(1, S.Length - 2)).Replace("/"c, "\"c)
        End If
        If S.StartsWith("/") Then Return Dir & S.Replace("/"c, "\"c)
        Return S
    End Function

    '''<summary>读安装器 jar 里的一个 json 文本。</summary>
    Private Shared Function ReadZipJsonText(JarPath As String, EntryName As String) As String
        Using Z As System.IO.Compression.ZipArchive = System.IO.Compression.ZipFile.OpenRead(JarPath)
            Dim E As System.IO.Compression.ZipArchiveEntry = Z.GetEntry(EntryName)
            If E Is Nothing Then Throw New Exception("安装器里没有 " & EntryName & "，这个版本不支持镜像安装")
            Using Sr As New System.IO.StreamReader(E.Open(), System.Text.Encoding.UTF8)
                Return Sr.ReadToEnd()
            End Using
        End Using
    End Function

    '''<summary>
    ''' 把安装器 jar 里某个目录下的条目**按原相对路径**解到磁盘。
    ''' 早先只取 GetFileName，data/ 若有子目录会被压平、同名文件互相覆盖；
    ''' 当前 Forge / NeoForge 的 data/ 都是平铺的，但上游一改就会静默少文件。
    ''' </summary>
    Private Shared Sub ExtractZipFolder(JarPath As String, EntryPrefix As String, TargetDir As String)
        DirectoryUtils.Create(TargetDir)
        Dim Prefix As String = EntryPrefix.Replace("\"c, "/"c)
        Using Z As System.IO.Compression.ZipArchive = System.IO.Compression.ZipFile.OpenRead(JarPath)
            For Each E As System.IO.Compression.ZipArchiveEntry In Z.Entries
                Dim Name As String = E.FullName.Replace("\"c, "/"c)
                If Not Name.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase) Then Continue For
                If Name.EndsWith("/"c) Then Continue For
                Dim Rel As String = Name.Substring(Prefix.Length).TrimStart("/"c)
                '防 zip slip：条目里带 .. 的一律不写，否则可能落到目标目录之外
                If Rel = "" OrElse Rel.Contains("..") Then Continue For
                Dim Dst As String = IO.Path.Combine(TargetDir, Rel.Replace("/"c, "\"c))
                DirectoryUtils.Create(IO.Path.GetDirectoryName(Dst))
                Using S As IO.Stream = E.Open()
                    Using F As New IO.FileStream(Dst, IO.FileMode.Create, IO.FileAccess.Write)
                        S.CopyTo(F)
                    End Using
                End Using
            Next
        End Using
    End Sub

    '''<summary>
    ''' 只靠镜像装 Forge / NeoForge：原版 + 安装器 → 从镜像下全部库 → 本地跑安装器自带的
    ''' installertools 做补丁与解包，最后写出 run.bat / win_args.txt。
    ''' </summary>
    Private Sub InstallLoaderFromMirror(Title As String, McVer As String, Dir As String,
                                        InstallerUrl As String, InstallerPath As String,
                                        Optional ExpectedSeconds As Double = 90)
        Dim CleanDir As String = Dir.TrimEnd("\"c)
        DirectoryUtils.Create(CleanDir)
        Dim VanillaPath As String = CleanDir & "\server.jar"
        Dim NeedVanilla As Boolean = Not FileUtils.Exists(VanillaPath)
        Dim VanillaUrls As IEnumerable(Of String) = Nothing
        If NeedVanilla Then
            RunInUi(Sub() LabStatus.Text = "正在解析原版服务端地址…")
            VanillaUrls = GetVanillaServerUrls(McVer)
        End If
        Dim Java As String = GetJavaPath()
        If Java = "" Then Throw New Exception("找不到 Java：先在设置里加一个")

        Dim Combo As LoaderCombo(Of String) = Nothing
        RunInUiWait(Sub()
            LabStatus.Text = "正在从镜像下载并组装（不经过官方 maven）…"
            Dim Loaders As New List(Of LoaderBase)
            If NeedVanilla Then
                Loaders.Add(New LoaderDownload("下载原版服务端 " & McVer,
                    New List(Of NetFile) From {New NetFile(VanillaUrls, VanillaPath)}) With {.ProgressWeight = 60})
            End If
            Loaders.Add(New LoaderDownload("下载 " & Title & " 安装器",
                New List(Of NetFile) From {New NetFile({InstallerUrl}, InstallerPath)}) With {.ProgressWeight = 5})
            Loaders.Add(New LoaderTask(Of String, String)("从镜像组装 " & Title,
                Sub(Task As LoaderTask(Of String, String))
                    BuildLoaderServerFromMirror(Task, Title, McVer, CleanDir, InstallerPath, Java)
                End Sub) With {.ProgressWeight = 120})
            '镜像这条路失败后要回退官方安装器，这里故意不弹红提示 ——
            '否则用户先看到「失败」、紧接着又看到开始重试，会以为整个安装挂了直接取消
            Combo = New LoaderCombo(Of String)(Title, Loaders)
            Combo.Start()
            LoaderTaskbarAdd(Combo)
            FrmMain.BtnExtraDownload.ShowRefresh()
            FrmMain.PageChange(FormMain.PageType.TaskManager)
        End Sub)
        Do While Combo.State.ToString() = "Loading"
            System.Threading.Thread.Sleep(200)
        Loop
        If Combo.State.ToString() = "Canceled" OrElse Combo.State.ToString() = "Cancelled" Then Throw New InstallCanceledException()
        If Combo.State.ToString() <> "Finished" Then
            Throw New Exception(If(Combo.[Error] IsNot Nothing, Combo.[Error].Message, "镜像安装未完成"))
        End If
    End Sub

    '''<summary>
    ''' 用户在任务栏点了取消。单独一个类型，是为了让上层能区分「取消」和「失败」：
    ''' 失败要回退官方安装器，取消就该老实停下 —— 早前两者都是普通 Exception，
    ''' 结果点了取消反而开始装第二遍，看着像取消不掉。
    ''' </summary>
    Private Class InstallCanceledException
        Inherits Exception
        Public Sub New()
            MyBase.New("已取消")
        End Sub
    End Class

    '''<summary>下载库、解 data、跑 processors。全部在后台线程执行。</summary>
    Private Sub BuildLoaderServerFromMirror(Task As LoaderTask(Of String, String),
                                            Title As String, McVer As String, Dir As String,
                                            InstallerPath As String, Java As String)
        Dim Profile As JObject = JObject.Parse(ReadZipJsonText(InstallerPath, "install_profile.json"))
        Dim VersionJson As JObject = JObject.Parse(ReadZipJsonText(InstallerPath, "version.json"))

        '① 解出安装器自带的 data/（run.bat、win_args.txt、补丁包都在里面）
        Task.Progress = 0.02
        ExtractZipFolder(InstallerPath, "data/", Dir & "\data")

        '①b 建 data 占位符表（{BINPATCH} / {MAPPINGS} / {PATCHED} …）。必须按版本现读，
        '   不能写死：NeoForge 26.3 的补丁包是 client.lzma，Forge 1.20.1 的服务端是 server.lzma，
        '   而且还有 MAPPINGS / MOJMAPS / MC_SLIM 这些只有老版本才有的键
        Dim Data As New Dictionary(Of String, String)(StringComparer.OrdinalIgnoreCase)
        Dim DataNode As JObject = TryCast(Profile("data"), JObject)
        If DataNode IsNot Nothing Then
            For Each Kv As KeyValuePair(Of String, JToken) In DataNode
                Dim Raw As String = ""
                If Kv.Value IsNot Nothing AndAlso Kv.Value.Type = JTokenType.Object Then
                    Raw = If(Kv.Value("server")?.ToString, Kv.Value("client")?.ToString)
                ElseIf Kv.Value IsNot Nothing Then
                    Raw = Kv.Value.ToString
                End If
                Data(Kv.Key) = ResolveDataValue(Raw, Dir)
            Next
        End If

        '② 下载全部库：安装器的 libraries + 版本的 libraries
        Dim Files As New List(Of Tuple(Of String, String)) ' (url, 相对路径)
        Dim Seen As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)
        For Each Root As JObject In {Profile, VersionJson}
            Dim Libs As JArray = TryCast(Root("libraries"), JArray)
            If Libs Is Nothing Then Continue For
            For Each L As JToken In Libs
                Dim Art As JToken = L("downloads")?("artifact")
                If Art Is Nothing Then Continue For
                Dim Rel As String = If(Art("path")?.ToString, "").Trim
                Dim Url As String = If(Art("url")?.ToString, "").Trim
                If Rel = "" OrElse Url = "" Then Continue For
                If Not Seen.Add(Rel) Then Continue For
                Files.Add(New Tuple(Of String, String)(MirrorMavenUrl(Url), Rel))
            Next
        Next
        For i = 0 To Files.Count - 1
            Dim Rel As String = Files(i).Item2.Replace("/"c, "\"c)
            Dim Target As String = Dir & "\libraries\" & Rel
            If FileUtils.Exists(Target) Then Continue For
            DirectoryUtils.Create(IO.Path.GetDirectoryName(Target))
            NetDownloadByLoader({Files(i).Item1}, Target)
            Task.Progress = 0.05 + 0.55 * ((i + 1) / Math.Max(Files.Count, 1))
        Next

        '③ 跑 processors：EXTRACT_FILES 我们自己解；其余用安装器自带的 installertools
        Dim Procs As JArray = TryCast(Profile("processors"), JArray)
        If Procs Is Nothing Then Throw New Exception("安装器里没有 processors，这个版本只能走官方安装器")
        Dim ProcArgs As New List(Of List(Of String))
        For Each P As JToken In Procs
            Dim A As JArray = TryCast(P("args"), JArray)
            If A Is Nothing Then Continue For
            Dim L As New List(Of String)
            For Each X As JToken In A
                L.Add(If(X, "").ToString)
            Next
            ProcArgs.Add(L)
        Next
        Dim Total As Integer = Math.Max(ProcArgs.Count, 1)
        For Index = 0 To ProcArgs.Count - 1
            Dim A As List(Of String) = ProcArgs(Index)
            If A.Contains("EXTRACT_FILES") Then
                ExtractByArgs(Dir, InstallerPath, A)
            Else
                RunProcessor(Dir, Procs(Index), A, McVer, InstallerPath, Java, Data)
            End If
            Task.Progress = 0.6 + 0.35 * ((Index + 1) / Total)
        Next

        '④ 收尾：eula、内存参数、确认入口存在
        FileUtils.Write(Dir & "\eula.txt", "eula=true" & vbCrLf, encoding:=New System.Text.UTF8Encoding(False))
        Dim HasEntry As Boolean = FileUtils.Exists(Dir & "\run.bat") OrElse FileUtils.Exists(Dir & "\run.sh")
        If Not HasEntry Then Throw New Exception("没有生成 run.bat，镜像安装不完整")
        Task.Progress = 1
    End Sub

    '''<summary>按 EXTRACT_FILES 的参数把安装器里的文件搬到目标位置（--from/--to 成对出现）。</summary>
    Private Sub ExtractByArgs(Dir As String, InstallerPath As String, Args As List(Of String))
        Dim Froms As New List(Of String)
        Dim Tos As New List(Of String)
        For i = 0 To Args.Count - 2
            If Args(i) = "--from" Then Froms.Add(Args(i + 1))
            If Args(i) = "--to" Then Tos.Add(ResolvePlaceholder(Args(i + 1), Dir, "", InstallerPath, Nothing))
        Next
        If Froms.Count = 0 Then Return
        Using Z As System.IO.Compression.ZipArchive = System.IO.Compression.ZipFile.OpenRead(InstallerPath)
            For i = 0 To Math.Min(Froms.Count, Tos.Count) - 1
                Dim Entry As String = Froms(i).TrimStart("/"c)
                Dim E As System.IO.Compression.ZipArchiveEntry = Z.GetEntry(Entry)
                If E Is Nothing Then Continue For
                Dim Dest As String = Tos(i)
                DirectoryUtils.Create(IO.Path.GetDirectoryName(Dest))
                If FileUtils.Exists(Dest) AndAlso Entry.EndsWith("user_jvm_args.txt") Then Continue For '别覆盖用户改过的
                Using S As IO.Stream = E.Open()
                    Using F As New IO.FileStream(Dest, IO.FileMode.Create, IO.FileAccess.Write)
                        S.CopyTo(F)
                    End Using
                End Using
            Next
        End Using
    End Sub

    '''<summary>
    ''' 组装安装器的 classpath：libraries 下所有 jar 串起来。
    ''' Forge 的工具（installertools / jarsplitter / binarypatcher）是**瘦 jar**，
    ''' 直接 `java -jar` 会 NoClassDefFoundError（缺 gson 等），必须给全 classpath。
    ''' </summary>
    '''<summary>
    ''' processor 的 classpath。**优先用 install_profile 里每个 processor 自带的 `classpath` 字段** ——
    ''' Forge / NeoForge 都是逐步骤声明精确依赖的（jarsplitter 要 jopt 5.0.4、FART 要 asm 9.2、
    ''' binarypatcher 要 guava 25.1、NeoForge 的 installertools 只要它自己那个 fatjar）。
    ''' 早先把整个 libraries 塞进 -cp，等于让类加载器在几十个版本里抽签：
    ''' NeoForge 26.3 就是抽到了 at-parser 11.0.2 的 AtParser，
    ''' 报 `NoSuchMethodError: AtParser.<init>(String[])`（fatjar 里那个才是对的）。
    ''' </summary>
    Private Shared Function BuildProcessorClasspath(Dir As String, Proc As JToken, ToolJar As String,
                                                    Exclude As IEnumerable(Of String)) As String
        Dim Decl As JArray = TryCast(Proc("classpath"), JArray)
        If Decl Is Nothing OrElse Decl.Count = 0 Then
            '老版本 install_profile 可能没声明，退回全量（至少还能跑）
            Return BuildLibraryClasspath(Dir, Exclude)
        End If
        Dim List As New List(Of String)
        For Each C In Decl
            Dim Coord As String = If(C?.ToString, "").Trim
            If Coord = "" Then Continue For
            Dim P As String = Dir & "\libraries\" & MavenCoordToPath(Coord).Replace("/"c, "\"c)
            If FileUtils.Exists(P) Then
                If Not List.Any(Function(X) String.Equals(X, P, StringComparison.OrdinalIgnoreCase)) Then List.Add(P)
            Else
                '声明了却没下到，就是下载漏了。这里必须报出来：
                '静默退回全量只会再次踩类冲突，错误更难查
                Throw New Exception("缺少安装工具依赖：" & Coord)
            End If
        Next
        If Not List.Any(Function(X) String.Equals(X, ToolJar, StringComparison.OrdinalIgnoreCase)) Then
            If FileUtils.Exists(ToolJar) Then List.Add(ToolJar)
        End If
        Return String.Join(";", List)
    End Function

    '''<summary>参数含空格 / 制表符就加双引号。我们直接起 java.exe、不经过 cmd，所以只需防空格拆参数。</summary>
    Private Shared Function QuoteArg(S As String) As String
        Dim Q As String = Chr(34)
        If String.IsNullOrEmpty(S) Then Return Q & Q
        If S.Contains(" "c) OrElse S.Contains(ChrW(9)) Then Return Q & S & Q
        Return S
    End Function

    '''<summary>
    ''' 拼 processor 的 classpath：libraries 下全部 jar 减去 Exclude。
    ''' Exclude 必须传「本步骤要读写的文件」—— JVM 一旦打开某个 jar 就会在 Windows 上锁住它，
    ''' jarsplitter 的 checkOutput 删不掉旧输出，直接 IOException: Could not delete file。
    ''' </summary>
    Private Shared Function BuildLibraryClasspath(Dir As String, Optional Exclude As IEnumerable(Of String) = Nothing) As String
        Dim Skip As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)
        If Exclude IsNot Nothing Then
            For Each X In Exclude
                If String.IsNullOrWhiteSpace(X) Then Continue For
                Try
                    Skip.Add(IO.Path.GetFullPath(X))
                Catch
                End Try
            Next
        End If
        Dim Jars As New List(Of String)
        Try
            For Each F In DirectoryUtils.EnumerateFiles(Dir & "\libraries", True, "*.jar")
                'net\minecraft\server 下是 jarsplitter 的输入输出（unpacked / slim / extra），
                '一律不进 classpath：它们只是数据，不需要当依赖，锁住反而会挡住重装
                If F.Replace("/"c, "\"c).IndexOf("\net\minecraft\server\", StringComparison.OrdinalIgnoreCase) >= 0 Then Continue For
                Dim Full As String = F
                Try
                    Full = IO.Path.GetFullPath(F)
                Catch
                End Try
                If Skip.Contains(Full) Then Continue For
                Jars.Add(F)
            Next
        Catch ex As Exception
            Logger.Warn(ex, "枚举 libraries 失败")
        End Try
        Return String.Join(";", Jars)
    End Function

    '''<summary>读 jar 的 Main-Class；读不到返回空串（那就退回 java -jar）。</summary>
    Private Shared Function GetJarMainClass(JarPath As String) As String
        Try
            Using Z As System.IO.Compression.ZipArchive = System.IO.Compression.ZipFile.OpenRead(JarPath)
                Dim E As System.IO.Compression.ZipArchiveEntry = Z.GetEntry("META-INF/MANIFEST.MF")
                If E Is Nothing Then Return ""
                Using Sr As New System.IO.StreamReader(E.Open())
                    For Each Line As String In Sr.ReadToEnd().Split({ChrW(13), ChrW(10)}, StringSplitOptions.RemoveEmptyEntries)
                        Dim T As String = Line.Trim()
                        If T.StartsWith("Main-Class:", StringComparison.OrdinalIgnoreCase) Then
                            Return T.Substring("Main-Class:".Length).Trim()
                        End If
                    Next
                End Using
            End Using
        Catch ex As Exception
            Logger.Warn(ex, "读 jar 的 Main-Class 失败")
        End Try
        Return ""
    End Function

    '''<summary>执行一个 processor（java -cp 全部库 + Main-Class 参数…）。</summary>
    Private Sub RunProcessor(Dir As String, Proc As JToken, Args As List(Of String),
                             McVer As String, InstallerPath As String, Java As String,
                             Data As Dictionary(Of String, String))
        Dim Coord As String = If(Proc("jar")?.ToString, "").Trim
        Dim Rel As String = MavenCoordToPath(Coord).Replace("/"c, "\"c)
        Dim JarPath As String = Dir & "\libraries\" & Rel
        If Not FileUtils.Exists(JarPath) Then Throw New Exception("缺少安装工具：" & Coord)
        Dim Resolved As New List(Of String)
        For Each A In Args
            Dim R As String = ResolvePlaceholder(A, Dir, McVer, InstallerPath, Data)
            '输出路径（{PATCHED} / {MERGED_MAPPINGS} / {MC_SLIM} …）的父目录不一定存在，先建好
            If R.StartsWith(Dir, StringComparison.OrdinalIgnoreCase) AndAlso
               (R.Contains("\libraries\") OrElse R.Contains("\data\")) Then
                Try
                    DirectoryUtils.Create(IO.Path.GetDirectoryName(R))
                Catch
                End Try
            End If
            Resolved.Add(R)
        Next
        Dim MainClass As String = GetJarMainClass(JarPath)
        '参数里出现的 jar / zip 一律不进 classpath：它们要么是输入要么是输出，
        '被 JVM 打开就锁住，jarsplitter 删旧输出会报 Could not delete file
        Dim SkipCp As New List(Of String)
        For Each R In Resolved
            If R.EndsWith(".jar", StringComparison.OrdinalIgnoreCase) OrElse
               R.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) Then SkipCp.Add(R)
        Next
        Dim Cp As String = BuildProcessorClasspath(Dir, Proc, JarPath, SkipCp)
        '参数逐个加引号：安装目录常在「新建文件夹 (2)」这种带空格的路径下，
        '不引起来 java 会把一个路径拆成两个参数，报的却是别处的错，很难查
        Dim Cmd As String = If(MainClass <> "",
            $"-cp ""{Cp}"" {MainClass} ",
            $"-jar ""{JarPath}"" ") & String.Join(" ", Resolved.Select(AddressOf QuoteArg))
        Dim LogPath As String = Dir & "\mirror-install.log"
        Dim P As System.Diagnostics.Process = System.Diagnostics.Process.Start(New System.Diagnostics.ProcessStartInfo(Java, Cmd) With {
            .WorkingDirectory = Dir, .UseShellExecute = False, .CreateNoWindow = True,
            .RedirectStandardOutput = True, .RedirectStandardError = True,
            .StandardOutputEncoding = New System.Text.UTF8Encoding(False),
            .StandardErrorEncoding = New System.Text.UTF8Encoding(False)
        })
        '两条流必须并发读，否则 stderr 写满 4KB 缓冲区会卡住进程（WaitForExit 永远等不到）
        Dim OutTask As System.Threading.Tasks.Task(Of String) = P.StandardOutput.ReadToEndAsync()
        Dim ErrTask As System.Threading.Tasks.Task(Of String) = P.StandardError.ReadToEndAsync()
        P.WaitForExit(600000)
        Dim Out As String = ""
        Try
            Out = OutTask.Result & vbCrLf & ErrTask.Result
        Catch
        End Try
        Try
            FileUtils.Write(LogPath, Cmd & vbCrLf & vbCrLf & Out, encoding:=New System.Text.UTF8Encoding(False))
        Catch
        End Try
        If P.ExitCode <> 0 Then
            '这两个错最常见，直接在异常里说清怎么办，别让人去翻日志猜
            Dim Extra As String = ""
            If Out.IndexOf("Could not delete file", StringComparison.OrdinalIgnoreCase) >= 0 Then
                Extra = "；输出文件被占用 —— 多半是上一次安装残留的 java 进程还开着，任务管理器结束 java.exe 后重试"
            ElseIf Out.IndexOf("NoClassDefFoundError", StringComparison.OrdinalIgnoreCase) >= 0 Then
                Extra = "；缺依赖库 —— 上一步的 libraries 没下全，重试一次"
            End If
            Throw New Exception($"组装步骤失败（退出码 {P.ExitCode}，见 mirror-install.log）" & Extra)
        End If
    End Sub

    '''<summary>
    ''' 替换 processor 参数里的占位符。**先按 data 表替换**（{BINPATCH}/{MAPPINGS}/{PATCHED}… 每个版本不同），
    ''' 再处理固定项（{ROOT}/{INSTALLER}/{MINECRAFT_JAR}/{SIDE}），最后兜底替换 [coords] 写法。
    ''' 早先写死 {BINPATCH}=client.lzma，Forge 1.20.1 的服务端其实是 server.lzma，直接踩空。
    ''' </summary>
    Private Function ResolvePlaceholder(Raw As String, Dir As String, McVer As String,
                                        InstallerPath As String, Data As Dictionary(Of String, String)) As String
        Dim S As String = If(Raw, "")
        If Data IsNot Nothing Then
            For Each Kv In Data
                S = S.Replace("{" & Kv.Key & "}", Kv.Value)
            Next
        End If
        S = S.Replace("{ROOT}", Dir)
        S = S.Replace("{INSTALLER}", InstallerPath)
        S = S.Replace("{MINECRAFT_VERSION}", If(McVer, ""))
        S = S.Replace("{MINECRAFT_JAR}", Dir & "\server.jar")
        S = S.Replace("{LIBRARY_DIR}", Dir & "\libraries")
        S = S.Replace("{SIDE}", "server")
        '没在 data 表里的 {X} 直接报出来，别让它带着花括号去执行
        Dim Leftover As System.Text.RegularExpressions.Match = System.Text.RegularExpressions.Regex.Match(S, "\{[A-Z_]+\}")
        If Leftover.Success Then Throw New Exception("未知的安装占位符：" & Leftover.Value)
        Dim M As System.Text.RegularExpressions.Match = System.Text.RegularExpressions.Regex.Match(S, "\[([^\]]+:[^\]]+:[^\]]+)\]")
        Do While M.Success
            Dim Rel As String = MavenCoordToPath(M.Groups(1).Value).Replace("/"c, "\"c)
            S = S.Replace(M.Value, Dir & "\libraries\" & Rel)
            M = M.NextMatch()
        Loop
        Return S
    End Function

    ' ── 加载器服务端安装 ───────────────────────────────
    '''<summary>
    ''' NeoForge 服务端：**先走镜像自建**（不需要访问 maven.neoforged.net），
    ''' 失败再回退官方安装器（网络通/开代理时它也能成）。
    ''' </summary>
    Private Sub InstallNeoForge(McVer As String, NeoVer As String, Dir As String)
        Dim CleanDir As String = Dir.TrimEnd("\"c)
        Dim Url As String = If(NeoVer.StartsWith("1.") AndAlso NeoVer.Contains("-"),
            $"https://bmclapi2.bangbang93.com/maven/net/neoforged/forge/{NeoVer}/forge-{NeoVer}-installer.jar",
            $"https://bmclapi2.bangbang93.com/maven/net/neoforged/neoforge/{NeoVer}/neoforge-{NeoVer}-installer.jar")
        Dim Jar As String = CleanDir & "\neoforge-installer.jar"
        Try
            InstallLoaderFromMirror($"NeoForge 服务端 {McVer}-{NeoVer}", McVer, CleanDir, Url, Jar)
            FinishLoaderInstall(CleanDir, McVer, "neoforge", NeoVer, "NeoForge")
            Return
        Catch ex As InstallCanceledException
            '用户自己点的取消，别回退重装
            RunInUi(Sub() LabStatus.Text = "已取消")
            Return
        Catch ex As Exception
            Logger.Warn(ex, "镜像安装 NeoForge 失败，回退官方安装器：" & ex.Message)
            RunInUi(Sub() LabStatus.Text = "镜像组装失败（" & ex.Message & "），改用官方安装器重试…")
        End Try
        InstallNeoForgeViaInstaller(McVer, NeoVer, CleanDir)
    End Sub

    '''<summary>
    ''' Forge 服务端：同样先走镜像自建，失败回退官方安装器。
    ''' </summary>
    Private Sub InstallForge(McVer As String, ForgeVer As String, Dir As String)
        Dim CleanDir As String = Dir.TrimEnd("\"c)
        Dim Url As String = $"https://bmclapi2.bangbang93.com/maven/net/minecraftforge/forge/{McVer}-{ForgeVer}/forge-{McVer}-{ForgeVer}-installer.jar"
        Dim Jar As String = CleanDir & "\forge-installer.jar"
        Try
            InstallLoaderFromMirror($"Forge 服务端 {McVer}-{ForgeVer}", McVer, CleanDir, Url, Jar)
            FinishLoaderInstall(CleanDir, McVer, "forge", ForgeVer, "Forge")
            Return
        Catch ex As InstallCanceledException
            '用户自己点的取消，别回退重装
            RunInUi(Sub() LabStatus.Text = "已取消")
            Return
        Catch ex As Exception
            Logger.Warn(ex, "镜像安装 Forge 失败，回退官方安装器：" & ex.Message)
            RunInUi(Sub() LabStatus.Text = "镜像组装失败（" & ex.Message & "），改用官方安装器重试…")
        End Try
        InstallForgeViaInstaller(McVer, ForgeVer, CleanDir)
    End Sub

    '''<summary>镜像安装成功后的统一收尾：配置、内存、记录、提示。</summary>
    Private Sub FinishLoaderInstall(Dir As String, McVer As String, Loader As String, LoaderVer As String, DisplayName As String)
        EnsureServerProperties(Dir)
        EnsureServerJvmArgs(Dir)
        Settings.Set("LinkServerDir", Dir)
        WriteServerMeta(Dir, McVer, Loader, LoaderVer)
        AddServerRecord(Dir, McVer & "-" & Loader & "-" & LoaderVer)
        RunInUi(Sub()
            LabStatus.Text = $"{DisplayName} {LoaderVer} 安装完成（镜像组装），目录：{Dir}"
            Hint($"{DisplayName} 服务端安装完成：原版 {McVer} + {DisplayName} {LoaderVer}，启动脚本是目录里的 run.bat。" & vbCrLf &
                 "全程走镜像，没有访问官方 maven；回联机页点「开服」即可。", HintType.Green, False)
        End Sub)
    End Sub

    '''<summary>
    ''' 按 MC 版本推服务端需要的 Java 版本范围。
    ''' 1.21+ / 1.20.5+ 要 21，1.17~1.20.4 要 17，更早 8；26.x 这类新版本号按 21 处理。
    ''' </summary>
    Public Shared Function ServerJavaRange(McVer As String) As ValueRange(Of Version)
        Dim V As Version = Nothing
        If Not Version.TryParse(If(McVer, "").Trim(), V) Then Return ValueRange(Of Version).AtLeast(New Version(17, 0))
        If V.Major >= 22 Then Return ValueRange(Of Version).AtLeast(New Version(21, 0))
        If V.Major = 1 Then
            If V.Minor >= 21 Then Return ValueRange(Of Version).AtLeast(New Version(21, 0))
            If V.Minor = 20 AndAlso V.Build >= 5 Then Return ValueRange(Of Version).AtLeast(New Version(21, 0))
            If V.Minor >= 17 Then Return ValueRange(Of Version).AtLeast(New Version(17, 0))
            Return ValueRange(Of Version).AtLeast(New Version(8, 0))
        End If
        Return ValueRange(Of Version).AtLeast(New Version(17, 0))
    End Function

    '''<summary>
    ''' 找一个能用的 Java：① 用户选中的 → ② Java 列表第一个 → ③ **让 PCL 自动下载**。
    ''' 第三条是关键：PCL 本体不带 Java，原版启动游戏时会用 SelectOrDownloadJava 自动装，
    ''' 服务端这边以前直接放弃（提示「先去设置里加一个」），等于把环境问题甩给用户。
    ''' </summary>
    Public Shared Function GetJavaPath(Optional McVer As String = "") As String
        If McLaunchJavaSelected IsNot Nothing AndAlso FileUtils.Exists(McLaunchJavaSelected.JavaExePath) Then
            Return McLaunchJavaSelected.JavaExePath
        End If
        Try
            Dim First = Configs.JavaList.Get().FirstOrDefault()
            If First IsNot Nothing AndAlso FileUtils.Exists(First.JavaExePath) Then Return First.JavaExePath
        Catch ex As Exception
            Logger.Warn(ex, "读取 Java 列表失败")
        End Try
        '一个都没有：从 Mojang 的 java-runtime 拉一个（走 bmclapi 镜像），和原版启动同一条路
        Try
            Logger.Info($"没有可用 Java，尝试自动下载（目标：{If(McVer, "未知版本")} → {ServerJavaRange(McVer)}）")
            Dim J As Java = SelectOrDownloadJava(ServerJavaRange(McVer), True)
            If J IsNot Nothing AndAlso FileUtils.Exists(J.JavaExePath) Then
                Logger.Info("自动下载 Java 完成：" & J.JavaExePath)
                Return J.JavaExePath
            End If
        Catch ex As Exception
            Logger.Warn(ex, "自动下载 Java 失败")
        End Try
        Return ""
    End Function

    Private Sub InstallNeoForgeViaInstaller(McVer As String, NeoVer As String, Dir As String)
        Dim CleanDir As String = Dir.TrimEnd("\"c)
        Dim ExitCode As Integer = -1
        Try
            Dim Url As String
            If NeoVer.StartsWith("1.") AndAlso NeoVer.Contains("-") Then
                'legacy 线（1.20.1）：版本号形如 1.20.1-47.1.106
                Url = $"https://bmclapi2.bangbang93.com/maven/net/neoforged/forge/{NeoVer}/forge-{NeoVer}-installer.jar"
            Else
                '新线（1.20.2+）：版本号形如 20.4.237
                Url = $"https://bmclapi2.bangbang93.com/maven/net/neoforged/neoforge/{NeoVer}/neoforge-{NeoVer}-installer.jar"
            End If
            Dim Jar As String = CleanDir & "\neoforge-installer.jar"
            Dim Java As String = GetJavaPath()
            If Java = "" Then Throw New Exception("找不到 Java：先在设置里加一个")
            InstallLoaderServer($"NeoForge 服务端 {McVer}-{NeoVer}", McVer, CleanDir, Url, Jar,
                Sub(Task As LoaderTask(Of String, String))
                    PrepareVanillaForInstaller(McVer, CleanDir)
                    Dim P As System.Diagnostics.Process = System.Diagnostics.Process.Start(New System.Diagnostics.ProcessStartInfo(Java, BuildInstallerArgs(Jar, $"--installServer ""{CleanDir}""")) With {
                        .WorkingDirectory = CleanDir, .UseShellExecute = False, .CreateNoWindow = True
                    })
                    WaitInstallerWithProgress(P, Task, 180, 600000)
                    ExitCode = P.ExitCode
                    If ExitCode <> 0 Then Throw New Exception($"安装器退出码 {ExitCode}")
                End Sub,
                Function() FileUtils.Exists(CleanDir & "\run.bat"),
                180)
            EnsureServerProperties(CleanDir)
            EnsureServerJvmArgs(CleanDir)
            Settings.Set("LinkServerDir", CleanDir)
            WriteServerMeta(CleanDir, McVer, "neoforge", NeoVer)
            AddServerRecord(CleanDir, McVer & "-neoforge-" & NeoVer)
            RunInUi(Sub()
                LabStatus.Text = $"NeoForge {NeoVer} 安装完成，目录：{CleanDir}"
                Hint($"NeoForge 服务端安装完成：原版 {McVer} + NeoForge {NeoVer}，启动脚本是目录里的 run.bat。" & vbCrLf &
                     "回联机页点「开服」即可，不用再手动挑 jar。", HintType.Green, False)
            End Sub)
        Catch ex As InstallCanceledException
            RunInUi(Sub() LabStatus.Text = "已取消（目录可能不完整，重新安装一次即可）")
        Catch ex As Exception
            RunInUi(Sub()
                LabStatus.Text = "NeoForge 安装失败：" & ex.Message
                If ExitCode > 0 Then
                    ReportInstallerFailure("NeoForge", ExitCode, CleanDir, CleanDir & "\neoforge-installer.jar.log")
                Else
                    Hint("NeoForge 安装失败：" & ex.Message & vbCrLf &
                         "如果卡在「下载原版服务端」或「安装」这一步，多为网络问题；开加速器后重试，或改用原版 / Paper。",
                         HintType.Red, False)
                End If
            End Sub)
        End Try
    End Sub

    '''<summary>PaperMC 家族（paper / folia）与 Purpur 的下载，都是拿构建号直接下一个可执行 jar。</summary>
    Private Sub InstallPaperFamily(McVer As String, BuildId As String, Dir As String, Project As String)
        Try
            Dim DisplayName As String = Project.Substring(0, 1).ToUpperInvariant() & Project.Substring(1)
            RunInUi(Sub() LabStatus.Text = $"正在获取 {DisplayName} 构建信息…")
            Dim Url As String
            If Project = "purpur" Then
                'Purpur 的 v2 API 直接给下载地址，不用先查构建列表
                Url = $"https://api.purpurmc.org/v2/purpur/{McVer}/{BuildId}/download"
            Else
                Dim Builds As JArray = JArray.Parse(NetRequestByLoader(
                    $"https://fill.papermc.io/v3/projects/{Project}/versions/{McVer}/builds", Timeout:=20000, IsJson:=True))
                Dim Target As JToken = Builds.FirstOrDefault(Function(x) x("id")?.ToString = BuildId)
                If Target Is Nothing Then Throw New Exception($"找不到 {DisplayName} 构建 {BuildId}")
                Url = Target("downloads")?("server:default")?("url")?.ToString
                If String.IsNullOrWhiteSpace(Url) Then Throw New Exception($"{DisplayName} 构建信息里没有下载地址")
            End If
            Dim CleanDir As String = Dir.TrimEnd("\"c)
            Dim TargetPath As String = CleanDir & "\server.jar"
            RunInUi(Sub() LabStatus.Text = $"正在下载 {DisplayName}…")
            DownloadWithTaskbar({Url}, TargetPath, $"下载 {DisplayName} {McVer}-{BuildId}")
            FileUtils.Write(CleanDir & "\eula.txt", "eula=true" & vbCrLf, encoding:=New System.Text.UTF8Encoding(False))
            EnsureServerProperties(CleanDir)
            WriteServerMeta(CleanDir, McVer, Project, BuildId)
            RunInUi(Sub()
                LabStatus.Text = "已下载：" & TargetPath
                AddServerRecord(Dir, McVer & "-" & Project & "-" & BuildId)
                Hint($"{DisplayName} 服务端已下载，eula 和 server.properties 已写好。" & vbCrLf &
                     "回联机页点「开服」就能启动，首次启动会自动生成完整配置。", HintType.Green, False)
                FrmMain.PageChange(FormMain.PageType.Download, FormMain.PageSubType.DownloadServer)
            End Sub)
        Catch ex As InstallCanceledException
            RunInUi(Sub() LabStatus.Text = "已取消")
        Catch ex As Exception
            RunInUi(Sub() LabStatus.Text = "安装失败：" & ex.Message)
        End Try
    End Sub

    Private Sub InstallForgeViaInstaller(McVer As String, ForgeVer As String, Dir As String)
        Dim CleanDir As String = Dir.TrimEnd("\"c)
        Dim ExitCode As Integer = -1
        Try
            Dim Url As String = $"https://bmclapi2.bangbang93.com/maven/net/minecraftforge/forge/{McVer}-{ForgeVer}/forge-{McVer}-{ForgeVer}-installer.jar"
            Dim Jar As String = CleanDir & "\forge-installer.jar"
            Dim Java As String = GetJavaPath()
            If Java = "" Then Throw New Exception("找不到 Java：先在设置里加一个")
            '一个任务：下原版 → 下安装器 → 静默安装（安装也占进度，装完才算完成）
            InstallLoaderServer($"Forge 服务端 {McVer}-{ForgeVer}", McVer, CleanDir, Url, Jar,
                Sub(Task As LoaderTask(Of String, String))
                    PrepareVanillaForInstaller(McVer, CleanDir)
                    '尾斜杠会转义引号，安装器会读到带引号的路径导致 Illegal char
                    Dim P As System.Diagnostics.Process = System.Diagnostics.Process.Start(New System.Diagnostics.ProcessStartInfo(Java, BuildInstallerArgs(Jar, $"--installServer ""{CleanDir}""")) With {
                        .WorkingDirectory = CleanDir, .UseShellExecute = False, .CreateNoWindow = True
                    })
                    WaitInstallerWithProgress(P, Task, 180, 600000)
                    ExitCode = P.ExitCode
                    If ExitCode <> 0 Then Throw New Exception($"安装器退出码 {ExitCode}")
                End Sub,
                Function() FileUtils.Exists(CleanDir & "\run.bat"),
                180)
            '退出码 0 且确实产出 run.bat 才算成功（SuccessCheck 已经校验过 run.bat）
            EnsureServerProperties(CleanDir)
            EnsureServerJvmArgs(CleanDir)
            Settings.Set("LinkServerDir", CleanDir)
            WriteServerMeta(CleanDir, McVer, "forge", ForgeVer)
            AddServerRecord(CleanDir, McVer & "-forge-" & ForgeVer)
            RunInUi(Sub()
                LabStatus.Text = $"Forge {ForgeVer} 安装完成，目录：{CleanDir}"
                Hint($"Forge 服务端安装完成：原版 {McVer} + Forge {ForgeVer}，启动脚本是目录里的 run.bat。" & vbCrLf &
                     "回联机页点「开服」即可，不用再手动挑 jar。", HintType.Green, False)
            End Sub)
        Catch ex As InstallCanceledException
            RunInUi(Sub() LabStatus.Text = "已取消（目录可能不完整，重新安装一次即可）")
        Catch ex As Exception
            RunInUi(Sub()
                LabStatus.Text = "Forge 安装失败：" & ex.Message
                If ExitCode > 0 Then
                    ReportInstallerFailure("Forge", ExitCode, CleanDir, CleanDir & "\forge-installer.jar.log")
                Else
                    Hint("Forge 安装失败：" & ex.Message & vbCrLf &
                         "如果卡在「下载原版服务端」或「安装」这一步，多为网络问题；开加速器后重试，或改用原版 / Paper。",
                         HintType.Red, False)
                End If
            End Sub)
        End Try
    End Sub

    Private Sub InstallFabric(McVer As String, LoaderVer As String, Dir As String)
        Dim CleanDir As String = Dir.TrimEnd("\"c)
        Dim ExitCode As Integer = -1
        Try
            Dim InstVer As String = "1.0.1"
            Dim Url As String = $"https://maven.fabricmc.net/net/fabricmc/fabric-installer/{InstVer}/fabric-installer-{InstVer}.jar"
            Dim Jar As String = CleanDir & "\fabric-installer.jar"
            Dim Java As String = GetJavaPath()
            If Java = "" Then Throw New Exception("找不到 Java：先在设置里加一个")
            '不给 -downloadMinecraft：我们先把 server.jar 下好，让它直接用本地这份
            InstallLoaderServer($"Fabric 服务端 {McVer}-{LoaderVer}", McVer, CleanDir, Url, Jar,
                Sub(Task As LoaderTask(Of String, String))
                    PrepareVanillaForInstaller(McVer, CleanDir)
                    Dim P As System.Diagnostics.Process = System.Diagnostics.Process.Start(New System.Diagnostics.ProcessStartInfo(Java, BuildInstallerArgs(Jar, $"server -dir ""{CleanDir}"" -mcversion {McVer} -loader {LoaderVer}")) With {
                        .WorkingDirectory = CleanDir, .UseShellExecute = False, .CreateNoWindow = True
                    })
                    WaitInstallerWithProgress(P, Task, 40, 600000)
                    ExitCode = P.ExitCode
                    If ExitCode <> 0 Then Throw New Exception($"安装器退出码 {ExitCode}")
                End Sub,
                Function() FileUtils.Exists(CleanDir & "\fabric-server-launch.jar"),
                40)
            EnsureServerProperties(CleanDir)
            Settings.Set("LinkServerDir", CleanDir)
            WriteServerMeta(CleanDir, McVer, "fabric", LoaderVer)
            AddServerRecord(CleanDir, McVer & "-fabric-" & LoaderVer)
            RunInUi(Sub()
                LabStatus.Text = $"Fabric {LoaderVer} 安装完成，目录：{CleanDir}"
                Hint($"Fabric 服务端安装完成：原版 {McVer} + Fabric {LoaderVer}，启动入口是 fabric-server-launch.jar。" & vbCrLf &
                     "回联机页点「开服」即可，不用再手动挑 jar。", HintType.Green, False)
            End Sub)
        Catch ex As InstallCanceledException
            RunInUi(Sub() LabStatus.Text = "已取消（目录可能不完整，重新安装一次即可）")
        Catch ex As Exception
            RunInUi(Sub()
                LabStatus.Text = "Fabric 安装失败：" & ex.Message
                If ExitCode > 0 Then
                    ReportInstallerFailure("Fabric", ExitCode, CleanDir, CleanDir & "\fabric-installer.jar.log")
                Else
                    Hint("Fabric 安装失败：" & ex.Message & vbCrLf &
                         "检查网络与 Java 版本（Fabric 需要 Java 17+），或改用原版 / Paper。", HintType.Red, False)
                End If
            End Sub)
        End Try
    End Sub

End Class