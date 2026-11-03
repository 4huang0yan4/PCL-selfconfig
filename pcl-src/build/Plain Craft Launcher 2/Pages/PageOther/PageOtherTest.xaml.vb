'自用分支：开源版把整个百宝箱页面替换成了一行提示，这里补回日常用得上的功能。
'内存优化沿用上游的调用入口（启动前的加载任务会调用 MemoryOptimize），
'清理垃圾则是原文里只剩空壳的 RubbishClear，另加了几个常用文件夹的快捷入口。
Public Class PageOtherTest

    Private Shadows IsLoaded As Boolean = False

    Private Sub PageOtherTest_Loaded(sender As Object, e As RoutedEventArgs) Handles Me.Loaded
        PanBack.ScrollToHome()
        '每次进页面都对齐一次：按钮初始是「可点」，但地址可能一直是空的
        RefreshDlStartButton()
        If IsLoaded Then Return
        IsLoaded = True
    End Sub

    Private Sub BtnMemoryOptimize_Click(sender As Object, e As MouseButtonEventArgs) Handles BtnMemoryOptimize.Click
        MemoryOptimize(True)
    End Sub
    Private Sub BtnRubbishClear_Click(sender As Object, e As MouseButtonEventArgs) Handles BtnRubbishClear.Click
        RubbishClear()
    End Sub
    Private Sub BtnSpaceAnalyze_Click(sender As Object, e As MouseButtonEventArgs) Handles BtnSpaceAnalyze.Click
        ModCustom.SpaceAnalyzeImpl()
    End Sub

    '快捷文件夹
    Private Sub BtnOpenIndie_Click(sender As Object, e As MouseButtonEventArgs) Handles BtnOpenIndie.Click
        ModCustom.OpenInstanceFolderImpl("indie")
    End Sub
    Private Sub BtnOpenLogs_Click(sender As Object, e As MouseButtonEventArgs) Handles BtnOpenLogs.Click
        ModCustom.OpenInstanceFolderImpl("logs")
    End Sub
    Private Sub BtnOpenCrash_Click(sender As Object, e As MouseButtonEventArgs) Handles BtnOpenCrash.Click
        ModCustom.OpenInstanceFolderImpl("crash")
    End Sub
    Private Sub BtnOpenSaves_Click(sender As Object, e As MouseButtonEventArgs) Handles BtnOpenSaves.Click
        ModCustom.OpenInstanceFolderImpl("saves")
    End Sub
    Private Sub BtnOpenMods_Click(sender As Object, e As MouseButtonEventArgs) Handles BtnOpenMods.Click
        ModCustom.OpenInstanceFolderImpl("mods")
    End Sub
    Private Sub BtnOpenScreenshots_Click(sender As Object, e As MouseButtonEventArgs) Handles BtnOpenScreenshots.Click
        ModCustom.OpenInstanceFolderImpl("screenshots")
    End Sub
    Private Sub BtnOpenLauncherLog_Click(sender As Object, e As MouseButtonEventArgs) Handles BtnOpenLauncherLog.Click
        Try
            Dim Dir As String = IO.Path.Combine(IO.Path.GetDirectoryName(PathExe), "PCL")
            If Not IO.Directory.Exists(Dir) Then IO.Directory.CreateDirectory(Dir)
            Process.Start("explorer.exe", Chr(34) & Dir & Chr(34))
        Catch ex As Exception
            Logger.Warn(ex, "打开启动器日志目录失败")
            Hint("打开失败：" & ex.Message, HintType.Red, False)
        End Try
    End Sub

    '===============================================================
    '  以下静态方法保持与上游一致的签名，避免别处调用时找不到成员
    '===============================================================

    Public Shared Sub MemoryOptimize(ShowHint As Boolean)
        ModCustom.MemoryOptimizeImpl(ShowHint)
    End Sub
    Public Shared Sub MemoryOptimizeInternal(ShowHint As Boolean)
        ModCustom.MemoryOptimizeImpl(ShowHint)
    End Sub
    Public Shared Sub RubbishClear()
        ModCustom.RubbishClearImpl()
    End Sub

    '未补回的功能：保留空实现
    Public Shared Sub StartCustomDownload(Url As String, FileName As String, Optional Folder As String = Nothing)
        StartCustomDownloadImpl(Url, FileName, Folder)
    End Sub

    '===============================================================
    '  自定义下载（2026-10-30 补回）
    '  上游开源版把 StartCustomDownload 剥成了空桩（调一次只弹「未包含」），
    '  这里按 PCL CE 的思路重写：校验 → 定位保存路径 → LoaderDownload → LoaderCombo 挂状态回调。
    '  只支持 http / https：CE 还有 UNC（\\server\share）分支走 LoaderDownloadUnc，本分支没有那个类，故不做。
    '  返回值只给界面用（决定成功后要不要清空输入框），公开的 Sub 保持上游签名不变。
    '===============================================================

    Private Shared Function StartCustomDownloadImpl(Url As String, FileName As String, Folder As String) As Boolean
        Try
            Url = If(Url, "").Trim()
            FileName = If(FileName, "").Trim()
            '1. 地址
            If Url = "" Then
                Hint("下载地址是空的，先填个网址。", HintType.Red, False)
                Return False
            End If
            If Not (Url.StartsWithF("http://") OrElse Url.StartsWithF("https://")) Then
                Hint("只支持 http / https 开头的地址（网络共享的 \\server\share 不支持）。", HintType.Red, False)
                Return False
            End If
            '2. 文件名：没给就从地址末段推断
            If FileName = "" Then
                FileName = GetFileNameFromUrl(Url)
                If FileName = "" Then
                    Hint("这个地址看不出文件名，手动填一个再下。", HintType.Red, False)
                    Return False
                End If
            End If
            '3. 保存位置：没给就弹保存对话框；用户取消就整个放弃（不静默改成默认目录）
            If String.IsNullOrWhiteSpace(Folder) Then
                Dim Target As String = Dialogs.SaveFile("选择保存位置", FileName)
                If String.IsNullOrWhiteSpace(Target) Then Return False
                Dim TargetDir As String = IO.Path.GetDirectoryName(Target)
                If TargetDir <> "" Then Folder = TargetDir
                Dim TargetName As String = IO.Path.GetFileName(Target)
                If TargetName <> "" Then FileName = TargetName
            End If
            If String.IsNullOrWhiteSpace(Folder) Then
                Hint("没有可用的保存位置。", HintType.Red, False)
                Return False
            End If
            Folder = Folder.Replace("/", "\").TrimEnd("\"c) & "\"
            '4. 建目录：建不出来就当场说，别等下载跑到一半才报
            Try
                If Not IO.Directory.Exists(Folder) Then IO.Directory.CreateDirectory(Folder)
            Catch ex As Exception
                Logger.Warn(ex, $"创建下载目录失败：{Folder}")
                Hint("无法创建保存目录：" & Folder & vbCrLf & ex.Message, HintType.Red, False)
                Return False
            End Try
            '5. 起任务：下载器挂在「下载」标签页，用户可以去那儿看进度或取消
            Logger.Info($"[自定义下载] {Url} → {Folder}{FileName}")
            Dim DownloadTask As New LoaderDownload($"自定义下载（{FileName}）",
                {New NetFile({Url}, Folder & FileName)})
            Dim Combo As New LoaderCombo(Of Integer)("自定义下载", {DownloadTask})
            AddHandler Combo.OnStateChangedUi,
                Sub(Loader As LoaderBase, NewState As LoadState, OldState As LoadState)
                    Select Case NewState
                        Case LoadState.Finished
                            Hint($"「{FileName}」下载完成。", HintType.Green, False)
                        Case LoadState.Failed
                            Logger.Warn(Loader.Error, $"自定义下载失败：{FileName}")
                            Hint($"「{FileName}」下载失败，已下载的部分不会保留；再点一次「开始下载」即可重来。原因见任务管理页。", HintType.Red, False)
                        Case LoadState.Canceled
                            Hint($"「{FileName}」下载已中止。", HintType.Blue, False)
                    End Select
                End Sub
            'Start() 只负责跑，不负责登记：不调 LoaderTaskbarAdd，任务列表里就没有这一项，
            '「任务管理」页会是空的。之前只给了句"去下载标签页看"的提示，等于让人去找一个不存在的东西。
            '四步是 PCL 里所有下载入口的统一写法（见 ModDownloadLib / PageDownloadCompDetail）。
            Combo.Start()
            LoaderTaskbarAdd(Combo)
            FrmMain.BtnExtraDownload.ShowRefresh()
            Hint($"已开始下载「{FileName}」，进度在任务管理页。", HintType.Green, False)
            FrmMain.PageChange(FormMain.PageType.TaskManager)
            Return True
        Catch ex As Exception
            Logger.Error(ex, "开始自定义下载失败")
            Hint("开始下载失败：" & ex.Message, HintType.Red, False)
            Return False
        End Try
    End Function

    ''' <summary>从 URL 末段取文件名（去掉查询串、做 URL 解码）。取不到返回空串。</summary>
    Private Shared Function GetFileNameFromUrl(Url As String) As String
        Try
            Dim Clean As String = If(Url, "").Trim()
            Clean = Clean.Split("?"c)(0).Split("#"c)(0)
            Dim Slash As Integer = Clean.LastIndexOf("/"c)
            If Slash < 0 OrElse Slash = Clean.Length - 1 Then Return ""
            Return System.Uri.UnescapeDataString(Clean.Substring(Slash + 1))
        Catch
            Return ""
        End Try
    End Function

    ''' <summary>界面上「保存位置」留空时对应的默认目录。</summary>
    Private Shared Function GetDefaultDownloadFolder() As String
        Return IO.Path.Combine(IO.Path.GetDirectoryName(PathExe), "PCL\MyDownload")
    End Function

    ' ── 自定义下载的界面事件 ────────────────────────────────

    Private Sub TextDlUrl_TextChanged(sender As Object, e As TextChangedEventArgs) Handles TextDlUrl.TextChanged
        '地址填了、文件名还空着时顺手猜一个；已经手填过的不覆盖
        If TextDlName IsNot Nothing AndAlso TextDlName.Text.Trim() = "" Then
            Dim Guess As String = GetFileNameFromUrl(TextDlUrl.Text)
            If Guess <> "" Then
                AniControlEnabled += 1
                TextDlName.Text = Guess
                AniControlEnabled -= 1
            End If
        End If
        RefreshDlStartButton()
    End Sub

    Private Sub TextDlName_TextChanged(sender As Object, e As TextChangedEventArgs) Handles TextDlName.TextChanged
        RefreshDlStartButton()
    End Sub

    Private Sub TextDlFolder_TextChanged(sender As Object, e As TextChangedEventArgs) Handles TextDlFolder.TextChanged
        RefreshDlStartButton()
    End Sub

    ''' <summary>地址为空时按钮置灰并说明原因——别让人点了才被告知「地址是空的」。</summary>
    Private Sub RefreshDlStartButton()
        If BtnDlStart Is Nothing Then Return
        Dim HasUrl As Boolean = TextDlUrl IsNot Nothing AndAlso TextDlUrl.Text.Trim() <> ""
        BtnDlStart.IsEnabled = HasUrl
        BtnDlStart.ToolTip = If(HasUrl, "开始下载；任务会出现在「下载」标签页里", "先填上面的「下载地址」")
    End Sub

    Private Sub BtnDlFolderPick_Click(sender As Object, e As MouseButtonEventArgs) Handles BtnDlFolderPick.Click
        Try
            Dim Dir As String = Dialogs.SelectFolder("选择下载保存位置", False).FirstOrDefault()
            If String.IsNullOrWhiteSpace(Dir) Then Return
            AniControlEnabled += 1
            TextDlFolder.Text = Dir
            AniControlEnabled -= 1
            RefreshDlStartButton()
        Catch ex As Exception
            Logger.Error(ex, "选择下载目录失败")
            Hint("选择目录失败：" & ex.Message, HintType.Red, False)
        End Try
    End Sub

    Private Sub BtnDlStart_Click(sender As Object, e As MouseButtonEventArgs) Handles BtnDlStart.Click
        '真正开始下载了才清空地址与文件名（保存位置保留），失败则原样留着方便改
        If StartCustomDownloadImpl(TextDlUrl.Text, TextDlName.Text, TextDlFolder.Text) Then
            AniControlEnabled += 1
            TextDlUrl.Text = ""
            TextDlName.Text = ""
            AniControlEnabled -= 1
            RefreshDlStartButton()
        End If
    End Sub

    Private Sub BtnDlOpen_Click(sender As Object, e As MouseButtonEventArgs) Handles BtnDlOpen.Click
        Try
            Dim Dir As String = If(TextDlFolder Is Nothing, "", TextDlFolder.Text.Trim())
            If Dir = "" Then Dir = GetDefaultDownloadFolder()
            If Not IO.Directory.Exists(Dir) Then IO.Directory.CreateDirectory(Dir)
            Process.Start("explorer.exe", Chr(34) & Dir & Chr(34))
        Catch ex As Exception
            Logger.Warn(ex, "打开下载目录失败")
            Hint("打开失败：" & ex.Message, HintType.Red, False)
        End Try
    End Sub

    Public Shared Sub Jrrp()
    End Sub

    '===============================================================
    '  「你知道吗」语料（2026-10-30 补回）
    '
    '  上游开源版这三处返回的是「为便于维护，开源内容中不包含百宝箱功能……」。
    '  本分支已经把百宝箱补回来了（自定义下载 / 内存优化 / 垃圾清理），那句话已经是假话；
    '  后来它又被清成空串，于是启动页只剩一个只有标题的「你知道吗」空框，
    '  自定义主页 / 自定义帮助里的 {hint} {cave} 也替换成空白——
    '  用户看不出是功能缺失还是自己写错了占位符。
    '
    '  这里换成真语料：Hint 讲本启动器确实有的功能，Cave 讲 Minecraft 冷知识。
    '  两条原则：① 每条都必须是真的，拿不准的不写；② 控制在两行内（卡片宽 260px，字号 12.5）。
    '===============================================================

    ''' <summary>启动器使用提示。同时用作自定义主页 / 帮助里的 {hint} 占位符。</summary>
    Private Shared ReadOnly HintPool As String() = {
        "联机页能一键建房，把房间码发给朋友就行；对方用 HMCL 这类陶瓦系启动器也能直接加入。",
        "导出整合包时勾上「额外写入 MCBBS v2 元数据」，同一个包就能被 HMCL、Prism 识别。",
        "百宝箱里的「自定义下载」可以直接填下载地址，不用另外开浏览器。",
        "把整合包压缩包直接拖到启动器窗口上，就能开始安装。",
        "版本设置左侧有「导出」，可以只导出这个实例，不会把启动器本体一起打进去。",
        "连不上朋友时，联机页下方可以配一个第三方穿透工具：通常能连上，但延迟会高一些。",
        "设置 → 启动 →「启动卡住时自动重试」，能救那些一直卡在「等待游戏窗口出现」的客户端。",
        "每个实例都能单独指定 Java，改版本设置即可，不影响其它实例。",
        "整合包安装会挂在「下载」页，那里的任务都能单独取消。",
        "百宝箱里的「内存优化」可以在游戏卡顿时回收一下内存。",
        "百宝箱里的「垃圾清理」会清掉日志、崩溃报告和 Natives，清理前先关掉游戏更稳妥。",
        "版本设置里可以给单个实例改窗口标题，启动器据此认出游戏窗口，配合自动重试更好用。"
    }

    ''' <summary>Minecraft 冷知识。用作 {cave} 占位符。</summary>
    Private Shared ReadOnly CavePool As String() = {
        "苦力怕最初是 Notch 把猪的模型长宽写反了才有的，他觉得效果不错就留了下来。",
        "末影人的音效是把人说「hi」「hey」的录音倒放做出来的。",
        "下界里 1 格距离等于主世界的 8 格，所以下界通道是最快的长途交通方式。",
        "猫能吓跑苦力怕和幻翼，基地里养一只很实用。",
        "幻翼只会在你连续 3 个游戏日没睡觉时出现。",
        "下雨时钓鱼的等待时间会缩短。",
        "用命名牌把兔子改名为 Toast，它会变成一只黑白花的特殊兔子。",
        "附魔台周围最多只有 15 个书架有效，摆更多不会继续提升等级。",
        "铁傀儡会手持虞美人，送给村子里的幼年村民。",
        "音符盒放在不同方块上，发出的音色不一样。",
        "雪傀儡在沙漠、下界这类高温环境里会持续受到伤害。",
        "僵尸猪灵被激怒后，附近的同类会一起围攻过来。",
        "岩浆里其实可以游泳，前提是身上有抗火药水的效果。",
        "1.9 之后附魔金苹果不再能合成，只能靠开箱子获得。",
        "铁砧从高处落下会砸伤生物，伤害和下落高度有关。",
        "用剪刀剪树叶是最快的，而且会直接掉落树叶本身。",
        "鞘翅滑翔时撞到方块会受到伤害，速度越快伤得越重。",
        "苦力怕被闪电击中会变成高压苦力怕，爆炸威力更大。",
        "末影龙旁边的每颗末影水晶都会给它回血，先清水晶再打龙。",
        "村民被僵尸攻击有概率变成僵尸村民，可以用金苹果加虚弱药水治好。"
    }

    '共享一个 Random：每次 New Random() 在同一毫秒内会取到相同种子，导致连续几次抽到同一条。
    Private Shared ReadOnly HintRandom As New Random()

    Private Shared Function PickOne(Pool As String()) As String
        If Pool Is Nothing OrElse Pool.Length = 0 Then Return ""
        Return Pool(HintRandom.Next(Pool.Length))
    End Function

    Public Shared Function GetRandomCave() As String
        Return PickOne(CavePool)
    End Function

    Public Shared Function GetRandomHint() As String
        Return PickOne(HintPool)
    End Function

    ''' <summary>上游遗留签名：当前没有调用点。给同样语料，将来若启用也不会是空串。</summary>
    Public Shared Function GetRandomPresetHint() As String
        Return PickOne(HintPool)
    End Function

End Class
