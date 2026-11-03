'自用分支：PCL 官方 EasyTier 联机是官方废案（上游已无法更新），本分支改用陶瓦联机。
'2026-10-29 整条链路已移除，本类只保留页面宿主与关窗接口，不再有状态机/加载器/进程管理。

Public Class PageLinkMain

    ''' <summary>
    ''' 关窗时的二次确认入口，FormMain 会调用。
    ''' 本分支不再维护内置联机状态，直接返回 False（不拦截关闭）。
    ''' </summary>
    Public Function TryExit(Silent As Boolean, Closing As Boolean) As Boolean
        If Not Closing Then Return False
        Dim Running As System.Diagnostics.Process = Nothing
        If ServerProc IsNot Nothing AndAlso Not ServerProc.HasExited Then Running = ServerProc
        If Running Is Nothing Then
            '顺手清掉已退出进程的记录
            Dim OldPid As Integer = Settings.Get(Of Integer)("LinkServerProcPid")
            If OldPid > 0 AndAlso FindServerProcess(OldPid) Is Nothing Then ClearServerProcRecord()
            Return False
        End If
        Dim KeepIt As Boolean = Settings.Get(Of Boolean)("LinkServerKeepOnExit")
        Dim AskMe As Boolean = Settings.Get(Of Boolean)("LinkServerExitAsk")
        '只有「询问」开着才弹窗。点过「以后都按设置」之后这里就是静默按 KeepIt 执行，
        '不会因为少了一层确认就把服务端莫名关掉或莫名留着。
        If Not Silent AndAlso AskMe Then
            Dim Choice As Integer = MyMsgBox(
                $"本机服务端还在运行（PID {Running.Id}）。" & vbCrLf & vbCrLf &
                "「让它继续跑」：关掉启动器后服务端仍在后台，玩家还能进；" &
                "下次打开启动器时，联机页的「本地服务器」会自动用 RCON 接管它的命令通道（实时输出拿不到），要关它点那里「关闭服务器」。" & vbCrLf &
                "「一起关掉」：先给服务端发 stop，等它保存世界后退出。" & vbCrLf & vbCrLf &
                If(KeepIt, "当前设置：让它继续跑。",
                           "当前设置：一起关掉。") & vbCrLf &
                "点「以后都按设置」就不再问了，之后关启动器直接照上面这条执行；想恢复询问，去联机页把「关闭启动器时先询问」重新勾上。",
                "服务端还在运行", "让它继续跑", "一起关掉", "以后都按设置")
            Select Case Choice
                Case 1
                    Return False
                Case 2
                    StopServerGracefully(Running)
                    Return False
                Case 3
                    Settings.Set("LinkServerExitAsk", False)
                    If CheckServerExitAsk IsNot Nothing Then CheckServerExitAsk.SetChecked(False, False)
                    If Not KeepIt Then StopServerGracefully(Running)
                    Return False
                Case Else
                    Return True '关掉弹窗 = 不退出
            End Select
        End If
        If Not KeepIt Then StopServerGracefully(Running)
        Return False
    End Function


    ' ── 第三方联机工具（「联机方案」卡片）──────────────────────────────────
    '2026-10-29：名称不再手填，改为取所选 exe 的文件名；选完立刻落盘。

    Private Sub LinkToolEnter() Handles Me.PageEnter
        LinkToolRefresh()
        ServerRefresh()
        StartStateWatch()
    End Sub

    Private Sub LinkToolLeave() Handles Me.PageExit
        StopStateWatch()
    End Sub

    Private Sub LinkToolRefresh()
        Try
            AniControlEnabled += 1
            Dim PathNow As String = If(Settings.Get(Of String)("LinkToolPath"), "").Trim()
            Dim NameNow As String = If(Settings.Get(Of String)("LinkToolName"), "").Trim()
            '名称以程序文件名为准；只有旧设置里存了路径时也补出来
            If PathNow <> "" Then NameNow = System.IO.Path.GetFileNameWithoutExtension(PathNow)
            TextLinkPath.Text = PathNow
            TextLinkName.Text = NameNow
        Catch ex As Exception
            Logger.Error(ex, "刷新联机方案失败")
        Finally
            AniControlEnabled -= 1
        End Try
        LabLinkStatus.Text = ModCustom.LinkToolStatusText
        '共用模式下这边就是「同一个工具」的另一处显示，改完必须一起刷；独立模式下它不动
        RefreshNetTool()
    End Sub

    Private Sub BtnLinkPick_Click(sender As Object, e As MouseButtonEventArgs) Handles BtnLinkPick.Click
        Try
            Dim File As String = Dialogs.SelectFile("选择联机工具的程序", False, filter:={({"exe"}, "可执行程序")}).FirstOrDefault()
            If String.IsNullOrWhiteSpace(File) Then Return
            Dim NameFromFile As String = System.IO.Path.GetFileNameWithoutExtension(File)
            AniControlEnabled += 1
            TextLinkPath.Text = File
            TextLinkName.Text = NameFromFile
            AniControlEnabled -= 1
            '选完即落盘：名称与路径一起写，避免"选了却还是旧值"
            ModCustom.LinkToolSaveNamePath(NameFromFile, File)
            LabLinkStatus.Text = ModCustom.LinkToolStatusText
            RefreshNetTool()
            Hint($"已指定「{NameFromFile}」，点「启动」即可拉起来。", HintType.Green, False)
        Catch ex As Exception
            Logger.Error(ex, "选择联机工具程序失败")
            Hint("选择失败：" & ex.Message, HintType.Red, False)
        End Try
    End Sub

    Private Sub BtnLinkUse_Click(sender As Object, e As MouseButtonEventArgs) Handles BtnLinkUse.Click
        Try
            '兜底补存：万一设置里还是旧值，启动前对一遍
            Dim NameNow As String = If(TextLinkName.Text, "").Trim()
            Dim PathNow As String = If(TextLinkPath.Text, "").Trim()
            If NameNow <> If(Settings.Get(Of String)("LinkToolName"), "").Trim() OrElse
               PathNow <> If(Settings.Get(Of String)("LinkToolPath"), "").Trim() Then
                ModCustom.LinkToolSaveNamePath(NameNow, PathNow)
                LinkToolRefresh()
            End If
            Hint(ModCustom.LinkToolLaunch(), HintType.Green, False)
            LabLinkStatus.Text = ModCustom.LinkToolStatusText
            RefreshNetTool()
        Catch ex As Exception
            Logger.Error(ex, "启动联机工具失败")
            Hint("启动失败：" & ex.Message, HintType.Red, False)
        End Try
    End Sub

    Private Sub BtnLinkReset_Click(sender As Object, e As MouseButtonEventArgs) Handles BtnLinkReset.Click
        Try
            AniControlEnabled += 1
            TextLinkName.Text = ""
            TextLinkPath.Text = ""
        Catch ex As Exception
            Logger.Error(ex, "重置联机方案失败")
        Finally
            AniControlEnabled -= 1
        End Try
        ModCustom.LinkToolSaveNamePath("", "")
        RefreshNetTool()
        LabLinkStatus.Text = "已清空并生效，当前回到默认的樱花穿透。"
        Hint("已清空并生效：回到默认的樱花穿透（未指定本地程序）。", HintType.Green, False)
    End Sub


    ' ── 运行方式：局域网 / 跨网 ─────────────────────────
    '二选一，不是两个独立开关：局域网 = 同一路由器直连；跨网 = 先用穿透 / 组网工具把端口暴露出去。
    '
    '跨网那一栏的程序默认和「联机方案（第三方工具）」是同一支（LinkToolPath / LinkToolName），
    '在任一边选，另一边都会跟着变 —— 大多数人本来也只装一个穿透工具，配两次纯属重复劳动。
    '但确实有人「联机用陶瓦、穿透用樱花」，所以留了 CheckNetToolSync：取消后跨网单开一格
    '（LinkServerNetToolPath），从此互不干扰。同步只是默认值，不是硬绑定。

    Private _NetModeSyncing As Boolean

    '''<summary>跨网这边是否与「联机方案」共用同一支工具（默认共用）。</summary>
    Private Function NetToolSynced() As Boolean
        Return Settings.Get(Of Boolean)("LinkServerNetToolSync")
    End Function

    '''<summary>跨网实际要用的程序路径：共用模式读联机方案那一份，独立模式读自己那一格。</summary>
    Private Function NetToolPath() As String
        If NetToolSynced() Then Return If(ModCustom.LinkToolPath, "").Trim()
        Return If(Settings.Get(Of String)("LinkServerNetToolPath"), "").Trim()
    End Function

    '''<summary>跨网这支工具的名字：共用模式用联机方案存的名字，独立模式直接取文件名。</summary>
    Private Function NetToolDisplayName() As String
        If NetToolSynced() Then Return If(ModCustom.LinkToolName, "").Trim()
        Dim P As String = NetToolPath()
        Return If(P = "", "", System.IO.Path.GetFileNameWithoutExtension(P))
    End Function

    '''<summary>
    ''' 按当前模式重画跨网工具那一格。联机方案那边一改就调它，两边永远显示同一份真相；
    ''' 独立模式下它只认自己那格，联机方案怎么变都不动。
    ''' </summary>
    Private Sub RefreshNetTool()
        If TextNetTool Is Nothing Then Return
        _NetModeSyncing = True
        AniControlEnabled += 1
        Try
            CheckNetToolSync.SetChecked(NetToolSynced(), False)
            TextNetTool.Text = NetToolPath()
        Catch ex As Exception
            Logger.Warn(ex, "刷新跨网工具失败")
        Finally
            AniControlEnabled -= 1
            _NetModeSyncing = False
        End Try
        LabNetToolState.Text = ModCustom.LinkToolStatusTextAt(NetToolPath(), NetToolDisplayName())
    End Sub

    Private Sub NetModeRefresh()
        Try
            Dim Mode As Integer = Settings.Get(Of Integer)("LinkServerNetMode")
            ApplyNetMode(If(Mode = 1, 1, 0))
            _NetModeSyncing = True
            TextNetPublicAddr.Text = If(Settings.Get(Of String)("LinkServerPublicAddr"), "").Trim()
            _NetModeSyncing = False
            RefreshNetTool()
            CheckNetAutoLaunch.SetChecked(Settings.Get(Of Boolean)("LinkServerNetAutoLaunch"), False)
            If If(TextNetLanAddr.Text, "").Trim() = "" Then RefreshLanAddr()
        Catch ex As Exception
            Logger.Warn(ex, "刷新运行方式失败")
        Finally
            _NetModeSyncing = False
        End Try
    End Sub

    '''<summary>按模式摆好界面：跨网才展开联机工具那一栏；切回局域网就把它收起。</summary>
    Private Sub ApplyNetMode(Mode As Integer)
        _NetModeSyncing = True
        Try
            AniControlEnabled += 1
            CheckNetLan.SetChecked(Mode = 0, False)
            CheckNetCross.SetChecked(Mode = 1, False)
        Catch ex As Exception
            Logger.Warn(ex, "设置运行方式勾选失败")
        Finally
            AniControlEnabled -= 1
            _NetModeSyncing = False
        End Try
        If PanCrossNet IsNot Nothing Then PanCrossNet.Visibility = If(Mode = 1, Visibility.Visible, Visibility.Collapsed)
    End Sub

    Private Sub CheckNetLan_Change(sender As Object, user As Boolean) Handles CheckNetLan.Change
        If _NetModeSyncing OrElse Not user Then Return
        If Not CheckNetLan.Checked Then
            '必须二选一：取消勾选等于「哪种都不是」，直接勾回来
            ApplyNetMode(0)
            Return
        End If
        Settings.Set("LinkServerNetMode", 0)
        ApplyNetMode(0)
        Hint("已切到局域网运行：同一 Wi-Fi / 同一路由器下的朋友用「本机 IPv4:端口」直连，不需要任何工具。", HintType.Blue, False)
    End Sub

    Private Sub CheckNetCross_Change(sender As Object, user As Boolean) Handles CheckNetCross.Change
        If _NetModeSyncing OrElse Not user Then Return
        If Not CheckNetCross.Checked Then
            ApplyNetMode(1)
            Return
        End If
        Settings.Set("LinkServerNetMode", 1)
        ApplyNetMode(1)
        Hint("已切到跨网运行：开服前先把联机工具起来，隧道指向 127.0.0.1:本机端口，再把公网地址发给朋友。", HintType.Blue, False)
    End Sub

    Private Sub CheckNetToolSync_Change(sender As Object, user As Boolean) Handles CheckNetToolSync.Change
        If _NetModeSyncing OrElse Not user Then Return
        Try
            '从共用切到独立：先把现在这支拷进自己那格，不然工具会「凭空消失」，用户还得再选一次
            If Not CheckNetToolSync.Checked AndAlso NetToolSynced() Then
                Settings.Set("LinkServerNetToolPath", If(ModCustom.LinkToolPath, "").Trim())
            End If
            Settings.Set("LinkServerNetToolSync", CheckNetToolSync.Checked)
        Catch ex As Exception
            Logger.Warn(ex, "保存跨网工具同步设置失败")
        End Try
        RefreshNetTool()
        If CheckNetToolSync.Checked Then
            Hint("已改为与「联机方案」共用同一支工具：那边换成什么，这里就跟着换。", HintType.Blue, False)
        Else
            Hint("已改为单独配置：跨网这边可以自己指定一支工具，联机方案那边再怎么改也不影响这里。", HintType.Blue, False)
        End If
    End Sub

    Private Sub CheckNetAutoLaunch_Change(sender As Object, user As Boolean) Handles CheckNetAutoLaunch.Change
        If Not user Then Return
        Settings.Set("LinkServerNetAutoLaunch", CheckNetAutoLaunch.Checked)
    End Sub

    Private Sub BtnNetToolPick_Click(sender As Object, e As MouseButtonEventArgs) Handles BtnNetToolPick.Click
        Try
            Dim File As String = Dialogs.SelectFile("选择联机工具的程序", False, filter:={({"exe"}, "可执行程序")}).FirstOrDefault()
            If String.IsNullOrWhiteSpace(File) Then Return
            Dim NameFromFile As String = System.IO.Path.GetFileNameWithoutExtension(File)
            If NetToolSynced() Then
                '共用模式：写的就是联机方案那一份，LinkToolRefresh 里会顺手把这边也刷一遍
                ModCustom.LinkToolSaveNamePath(NameFromFile, File)
                LinkToolRefresh()
                Hint($"已指定「{NameFromFile}」，点「启动」就能拉起来；上面的「联机方案」也同步换成了它。", HintType.Green, False)
            Else
                Settings.Set("LinkServerNetToolPath", File)
                RefreshNetTool()
                Hint($"已指定「{NameFromFile}」（只用于跨网运行，不影响「联机方案」），点「启动」就能拉起来。", HintType.Green, False)
            End If
        Catch ex As Exception
            Logger.Error(ex, "选择联机工具程序失败")
            Hint("选择失败：" & ex.Message, HintType.Red, False)
        End Try
    End Sub

    Private Sub BtnNetToolStart_Click(sender As Object, e As MouseButtonEventArgs) Handles BtnNetToolStart.Click
        Try
            Hint(ModCustom.LinkToolLaunchAt(NetToolPath(), NetToolDisplayName(), ModCustom.LinkToolUrl), HintType.Green, False)
            RefreshNetTool()
        Catch ex As Exception
            Logger.Error(ex, "启动联机工具失败")
            Hint("启动失败：" & ex.Message, HintType.Red, False)
        End Try
    End Sub

    '''<summary>开服前按需拉起联机工具：跨网 + 勾了自动启动才动手，已经在跑就跳过。</summary>
    Private Sub AutoLaunchNetToolOnServerStart()
        Try
            If Settings.Get(Of Integer)("LinkServerNetMode") <> 1 Then Return
            If Not Settings.Get(Of Boolean)("LinkServerNetAutoLaunch") Then Return
            Dim PathNow As String = NetToolPath()
            If Not ModCustom.LinkToolIsInstalledAt(PathNow) Then
                Hint("跨网运行要联机工具，但现在还没指定程序：在本地服务器卡片的「跨网运行」里点「选择」指一下 exe。", HintType.Red, False)
                Return
            End If
            Hint(ModCustom.LinkToolLaunchAt(PathNow, NetToolDisplayName(), ModCustom.LinkToolUrl), HintType.Blue, False)
            RefreshNetTool()
        Catch ex As Exception
            Logger.Warn(ex, "开服前自动启动联机工具失败")
        End Try
    End Sub

    Private Sub RefreshLanAddr()
        Try
            Dim Port As String = If(TextServerPort.Text, "").Trim()
            If Port = "" Then Port = "25565"
            Dim IP As String = LocalIPv4()
            TextNetLanAddr.Text = If(IP = "", $"读不到本机 IPv4（用 ipconfig 看一眼），端口 {Port}", $"{IP}:{Port}")
        Catch ex As Exception
            Logger.Warn(ex, "读取本机地址失败")
        End Try
    End Sub

    '''<summary>挑一个最像「局域网地址」的 IPv4：优先 192.168 / 10.x / 172.16–31。</summary>
    Private Function LocalIPv4() As String
        Try
            Dim Best As String = ""
            For Each Addr As System.Net.IPAddress In System.Net.Dns.GetHostAddresses(System.Net.Dns.GetHostName())
                If Addr.AddressFamily <> System.Net.Sockets.AddressFamily.InterNetwork Then Continue For
                Dim S As String = Addr.ToString()
                If S.StartsWith("127.") OrElse S.StartsWith("169.254.") Then Continue For
                If S.StartsWith("192.168.") OrElse S.StartsWith("10.") Then Return S
                If S.StartsWith("172.") Then
                    Dim Parts As String() = S.Split("."c)
                    Dim Second As Integer
                    If Parts.Length = 4 AndAlso Integer.TryParse(Parts(1), Second) AndAlso Second >= 16 AndAlso Second <= 31 Then Return S
                End If
                If Best = "" Then Best = S
            Next
            Return Best
        Catch
            Return ""
        End Try
    End Function

    Private Sub BtnNetLanRefresh_Click(sender As Object, e As MouseButtonEventArgs) Handles BtnNetLanRefresh.Click
        RefreshLanAddr()
        Hint("已刷新本机地址：" & If(TextNetLanAddr.Text, ""), HintType.Blue, False)
    End Sub

    Private Sub BtnNetLanCopy_Click(sender As Object, e As MouseButtonEventArgs) Handles BtnNetLanCopy.Click
        If If(TextNetLanAddr.Text, "").Trim() = "" Then RefreshLanAddr()
        Dim T As String = If(TextNetLanAddr.Text, "").Trim()
        If T = "" Then Return
        ClipboardSet(T)
        Hint("已复制本机地址：" & T, HintType.Green, False)
    End Sub

    Private Sub BtnNetPublicCopy_Click(sender As Object, e As MouseButtonEventArgs) Handles BtnNetPublicCopy.Click
        Dim T As String = If(TextNetPublicAddr.Text, "").Trim()
        If T = "" Then
            Hint("还没填对外地址：先在联机工具里建好隧道，把它给的公网地址粘到左边这一格。", HintType.Blue, False)
            Return
        End If
        ClipboardSet(T)
        Hint("已复制对外地址：" & T & "，发给外网朋友就能连。", HintType.Green, False)
    End Sub

    Private Sub TextNetPublicAddr_LostFocus(sender As Object, e As RoutedEventArgs) Handles TextNetPublicAddr.LostFocus
        If _NetModeSyncing Then Return
        Try
            Dim V As String = If(TextNetPublicAddr.Text, "").Trim()
            If V <> If(Settings.Get(Of String)("LinkServerPublicAddr"), "").Trim() Then Settings.Set("LinkServerPublicAddr", V)
        Catch ex As Exception
            Logger.Warn(ex, "保存对外地址失败")
        End Try
    End Sub

    ' ── 服务器（直连）──────────────────────────────────
    '地址存 Registry 设置；「加入」走 McLaunchOptions.ServerIp 启动一次；
    '「默认进入」由 ModLaunch 的参数拼装读取 LinkServerDefault + LinkServerAddress，不依赖版本自带设置。

    Private Sub ServerRefresh()
        Try
            AniControlEnabled += 1
            TextServerAddr.Text = If(Settings.Get(Of String)("LinkServerAddress"), "").Trim()
            TextServerName.Text = If(Settings.Get(Of String)("LinkServerName"), "").Trim()
            CheckServerDefault.SetChecked(Settings.Get(Of Boolean)("LinkServerDefault"), False)
            CheckServerLoginFollow.SetChecked(Settings.Get(Of Boolean)("LinkServerLoginFollow"), False)
            ApplyServerLoginFollow()
            If Not CheckServerLoginFollow.Checked Then
                ComboServerLogin.SelectedIndex = If(Settings.Get(Of Integer)("LinkServerLogin") = 1, 1, 0)
            End If
        Catch ex As Exception
            Logger.Error(ex, "刷新服务器设置失败")
        Finally
            AniControlEnabled -= 1
        End Try
        ServerStatusRefresh()
        ServerConfigRefresh()
        InstalledServersRefresh()
        NetModeRefresh()
        WhitelistRefresh()
        MigrateLocalRconHost()
        RconRefresh()
        ServerLeftoverRefresh()
        LocalStateRefresh()
        If CheckServerStopOnExit IsNot Nothing Then
            CheckServerStopOnExit.SetChecked(Not Settings.Get(Of Boolean)("LinkServerKeepOnExit"), False)
        End If
        If CheckServerExitAsk IsNot Nothing Then
            CheckServerExitAsk.SetChecked(Settings.Get(Of Boolean)("LinkServerExitAsk"), False)
        End If
        ApplyCardOrder()
    End Sub

    Private Sub CheckServerStopOnExit_Change(sender As Object, user As Boolean) Handles CheckServerStopOnExit.Change
        If Not user Then Return
        Settings.Set("LinkServerKeepOnExit", Not CheckServerStopOnExit.Checked)
    End Sub

    Private Sub CheckServerExitAsk_Change(sender As Object, user As Boolean) Handles CheckServerExitAsk.Change
        If Not user Then Return
        Settings.Set("LinkServerExitAsk", CheckServerExitAsk.Checked)
    End Sub

    '''<summary>
    ''' 三张服务器卡片的上下顺序。六种排列写死成表，只改 Grid.Row，不动控件树 ——
    ''' 换顺序时控件实例不变，事件绑定、输入中的文字、已经连着的 RCON 都不会丢。
    ''' </summary>
    Private Sub ApplyCardOrder()
        If PanServerCards Is Nothing Then Return
        Dim Idx As Integer = Settings.Get(Of Integer)("LinkCardOrder")
        If Idx < 0 OrElse Idx > 5 Then Idx = 0
        If ComboCardOrder IsNot Nothing AndAlso ComboCardOrder.SelectedIndex <> Idx Then
            AniControlEnabled += 1
            ComboCardOrder.SelectedIndex = Idx
            AniControlEnabled -= 1
        End If
        '注意用交错数组（String()()），不是二维数组：VB 的 {"a","b"} 字面量默认推断成 String(,)
        Dim Orders As String()() = {
            New String() {"join", "local", "remote"},
            New String() {"join", "remote", "local"},
            New String() {"local", "join", "remote"},
            New String() {"local", "remote", "join"},
            New String() {"remote", "join", "local"},
            New String() {"remote", "local", "join"}}
        Dim ByKey As New Dictionary(Of String, System.Windows.FrameworkElement) From {
            {"join", CardServerJoin}, {"local", CardServerLocal}, {"remote", CardServerRemote}}
        For I As Integer = 0 To 2
            Dim El As System.Windows.FrameworkElement = Nothing
            If ByKey.TryGetValue(Orders(Idx)(I), El) AndAlso El IsNot Nothing Then Grid.SetRow(El, I)
        Next
    End Sub

    Private Sub ComboCardOrder_SelectionChanged(sender As Object, e As SelectionChangedEventArgs) Handles ComboCardOrder.SelectionChanged
        If ComboCardOrder Is Nothing OrElse ComboCardOrder.SelectedIndex < 0 Then Return
        '控件树还没建好时（XAML 解析中途）别写设置：那一刻的索引不是用户选的。
        '不拦的话，页面每次初始化都会把存过的顺序覆盖成第一项。
        If PanServerCards Is Nothing Then Return
        Settings.Set("LinkCardOrder", ComboCardOrder.SelectedIndex)
        ApplyCardOrder()
    End Sub

    '''<summary>
    ''' 老版本把「本机托管」用的 127.0.0.1 写进了远程卡片那两格输入框（LinkRconHost / LinkRconPort），
    ''' 于是远程卡一打开就显示本机地址，看着像能管本机 —— 现在本机走独立的接管通道，这里清一次。
    ''' </summary>
    Private Sub MigrateLocalRconHost()
        If Settings.Get(Of Boolean)("LinkRconHostMigrated") Then Return
        Settings.Set("LinkRconHostMigrated", True)
        Dim Host As String = If(Settings.Get(Of String)("LinkRconHost"), "").Trim()
        If Host <> "" AndAlso IsLocalHost(Host) Then Settings.Set("LinkRconHost", "")
    End Sub

    ' ── 接管条（启动器重开后接管上次留下的服务端）─────────
    '''<summary>卡片顶部那条「还在后台跑」的提示 + 专用关服按钮。</summary>
    Private Sub ShowTakeover(Detail As String)
        If PanTakeover Is Nothing Then Return
        If LabTakeoverDetail IsNot Nothing AndAlso Detail <> "" Then LabTakeoverDetail.Text = Detail
        PanTakeover.Visibility = Visibility.Visible
    End Sub

    Private Sub HideTakeover()
        If PanTakeover IsNot Nothing Then PanTakeover.Visibility = Visibility.Collapsed
    End Sub

    '''<summary>检查上次启动器启动的服务端是否还在后台跑（启动器重启后 ServerProc 就丢了）。</summary>
    Private Sub ServerLeftoverRefresh()
        Try
            If ServerProc IsNot Nothing AndAlso Not ServerProc.HasExited Then Return '本进程起的，状态自己清楚
            Dim Pid As Integer = Settings.Get(Of Integer)("LinkServerProcPid")
            If Pid <= 0 Then Return
            Dim P As System.Diagnostics.Process = FindServerProcess(Pid)
            If P Is Nothing Then
                ClearServerProcRecord()
                Return
            End If
            Dim Dir As String = Settings.Get(Of String)("LinkServerProcDir")
            Dim Started As String = Settings.Get(Of String)("LinkServerProcTime")
            Dim Who As String = $"PID {Pid}" & If(String.IsNullOrWhiteSpace(Started), "", "，" & Started & " 启动")
            '接管后这条细节会被 ConnectLocalRcon 改写成端口，接管失败就留着 PID 让人知道它还活着
            SetLocalState(StateRunning, "后台运行", Who)
            '接管条贴在卡片最上面：这个状态下最想干的事就是把它关掉，按钮不该埋进下面那排按钮里
            ShowTakeover(Who & If(String.IsNullOrWhiteSpace(Dir), "", vbCrLf & "目录：" & Dir) &
                         vbCrLf & "启动器接管不了它的 stdin（实时输出拿不到），但会用 RCON 接管命令通道，命令和关闭都能用。")
            '关键：stdin 断了，RCON 这条 TCP 通道还在 —— 用它接管（只在本机这张卡片里，不碰远程）
            ConnectLocalRcon()
        Catch ex As Exception
            Logger.Warn(ex, "检查遗留服务端失败")
        End Try
    End Sub

    Private Sub ServerStatusRefresh()
        Dim Addr As String = If(Settings.Get(Of String)("LinkServerAddress"), "").Trim()
        If Addr = "" Then
            LabServerStatus.Text = "未填写服务器地址"
        ElseIf Settings.Get(Of Boolean)("LinkServerDefault") Then
            LabServerStatus.Text = $"启动游戏时自动进入：{Addr}"
        Else
            LabServerStatus.Text = $"当前：{Addr}（仅点「加入服务器」时使用）"
        End If
    End Sub

    '''<summary>只放行 host 或 host:port（含 [IPv6]:port），挡掉空格、协议前缀与非法端口。</summary>
    Private Function ServerAddressLooksValid(Addr As String) As Boolean
        If String.IsNullOrWhiteSpace(Addr) OrElse Addr.Contains(" ") Then Return False
        If Addr.Contains("://") Then Return False
        Dim Host As String = Addr
        Dim Port As String = ""
        If Addr.StartsWith("[") Then
            Dim CloseIdx As Integer = Addr.IndexOf("]")
            If CloseIdx < 2 Then Return False
            Host = Addr.Substring(0, CloseIdx + 1)
            If CloseIdx + 1 < Addr.Length Then
                If Addr(CloseIdx + 1) <> ":"c Then Return False
                Port = Addr.Substring(CloseIdx + 2)
            End If
        Else
            Dim Colon As Integer = Addr.LastIndexOf(":"c)
            If Colon > 0 Then
                Host = Addr.Substring(0, Colon)
                Port = Addr.Substring(Colon + 1)
            End If
        End If
        If Host = "" Then Return False
        If Port <> "" Then
            Dim P As Integer
            If Not Integer.TryParse(Port, P) OrElse P < 1 OrElse P > 65535 Then Return False
        End If
        Return True
    End Function

    Private Sub BtnServerJoin_Click(sender As Object, e As MouseButtonEventArgs) Handles BtnServerJoin.Click
        Dim Addr As String = If(TextServerAddr.Text, "").Trim()
        If Not ServerAddressLooksValid(Addr) Then
            Hint("服务器地址格式不对：填 host 或 host:port（端口 1–65535），不要带空格或 http:// 前缀。", HintType.Red, False)
            Return
        End If
        Try
            Settings.Set("LinkServerAddress", Addr)
            Settings.Set("LinkServerName", If(TextServerName.Text, "").Trim())
            ServerStatusRefresh()
            If McInstanceSelected Is Nothing Then
                Hint("地址已保存；主页还没选中版本，先选一个版本再点「启动游戏」。", HintType.Blue, False)
                Return
            End If
            '版本检测：服务端版本未知时（没启动过）从已装记录里推断，也要能看出不匹配
            Dim SrvVer As String = ServerVersion
            Dim SrvLoader As String = ""
            If SrvVer = "" Then
                Dim Info As Tuple(Of String, String) = ServerRecordInfo(ServerDir())
                If Info IsNot Nothing Then
                    SrvVer = Info.Item1
                    SrvLoader = Info.Item2
                End If
            Else
                SrvLoader = ServerRecordInfo(ServerDir())?.Item2
            End If
            Dim ClientVer As String = McInstanceSelected.Version.VanillaName
            If SrvVer <> "" AndAlso ClientVer <> SrvVer Then
                If MyMsgBox($"服务端版本是 {SrvVer}，当前客户端是 {ClientVer}。{vbCrLf}{vbCrLf}" &
                            "版本不一致会直接连不上（客户端会报「Internal Exception」或被拒绝）。" & vbCrLf &
                            $"建议先在「下载」里装一个 {SrvVer} 的客户端再连。", "启动世界与服务器版本不符",
                            "仍然尝试", "先去换版本", IsWarn:=True) <> 1 Then Return
            End If
            If (SrvLoader = "forge" OrElse SrvLoader = "neoforge") AndAlso SrvVer <> "" Then
                Hint($"提示：这个服务端装了 {If(SrvLoader = "forge", "Forge", "NeoForge")}，" & vbCrLf &
                     "客户端也要装同一版本的模组加载器和对应 mod，否则会被服务端拒绝。", HintType.Blue, False)
            End If
            '按「加入时用」的选择切换登录方式：离线为主，正版需先在主页登录过
            Dim UseMs As Boolean = ComboServerLogin.SelectedIndex = 1
            '── 账号和服务端的正版要求对不上，是「能启动但进不去」最常见的原因 ──
            '① 选了正版却没登录过微软账号
            If UseMs AndAlso If(Settings.Get(Of String)("CacheMsV2Access"), "") = "" Then
                If MyMsgBox("「加入时用」选的是微软正版登录，但启动器里还没登录过正版账号。" & vbCrLf & vbCrLf &
                            "现在连会直接失败。先去「启动」页用正版登录一次，或把「加入时用」改成离线登录。",
                            "还没登录正版账号", "仍然尝试", "取消", IsWarn:=True) <> 1 Then Return
            End If
            '② 离线登录时提醒一句就走，不拦。
            '「正版验证」是本机服务端的配置项，和「加入时用」没有绑定关系 —— 以前跨卡片联动、
            '还弹窗逼人二选一，等于把两件不相干的事捆在一起。现在只在真正要连的时候提示，
            '本机能读到配置就说得具体些，远程读不到就只说个可能性。
            If Not UseMs Then
                Dim LocalDir2 As String = ServerDir()
                If IsLocalServerAddr(Addr) AndAlso LocalDir2 <> "" AndAlso ReadPropBool(LocalDir2, "online-mode", False) Then
                    Hint("本机这个服务端开着「正版验证」，离线账号会被拒绝（客户端多半显示 Invalid session）。" & vbCrLf &
                         "要进去：在下方「本地服务器」里取消「正版验证」并重启服务端；或把「加入时用」改成微软正版登录。",
                         HintType.Red, False)
                ElseIf Not IsLocalServerAddr(Addr) Then
                    Hint("用的是离线登录。如果对方服务器开了正版验证，会直接被拒 —— 进不去就把「加入时用」改成微软正版登录（需要先在「启动」页登录正版账号）。",
                         HintType.Blue, False)
                End If
            End If
            Settings.Set("LoginType", If(UseMs, McLoginType.Ms, McLoginType.Legacy))
            If McLaunchStart(New McLaunchOptions With {.ServerIp = Addr}) Then
                Hint($"正在启动并连接 {Addr}…", HintType.Green, False)
            End If
        Catch ex As Exception
            Logger.Error(ex, "加入服务器失败")
            Hint("加入失败：" & ex.Message, HintType.Red, False)
        End Try
    End Sub

    Private Sub ComboServerLogin_SelectionChanged(sender As Object, e As SelectionChangedEventArgs) Handles ComboServerLogin.SelectionChanged
        Try
            '只记住选择。**不再联动「正版验证」** ——
            '那个勾选管的是「本机开的服」，这里是「以什么身份连别人」，两者本来就没有因果关系；
            '以前切离线就顺手把正版验证关掉，等于替用户改了他没在看的那张卡片的配置。
            Settings.Set("LinkServerLogin", If(ComboServerLogin.SelectedIndex = 1, 1, 0))
        Catch ex As Exception
            Logger.Warn(ex, "保存登录方式失败")
        End Try
    End Sub

    '''<summary>
    ''' 「跟随启动页」勾上时，把「加入时用」同步成启动页当前用的登录方式，并禁用下拉。
    ''' 启动页的登录方式存在 LoginType（McLoginType.Ms = 1，其余一律按离线对待）。
    ''' </summary>
    Private Sub ApplyServerLoginFollow()
        If CheckServerLoginFollow Is Nothing OrElse ComboServerLogin Is Nothing Then Return
        Dim Follow As Boolean = CheckServerLoginFollow.Checked
        ComboServerLogin.IsEnabled = Not Follow
        If Not Follow Then
            If LabServerLoginHint IsNot Nothing Then LabServerLoginHint.Text = "登录"
            Return
        End If
        Dim UseMs As Boolean = Settings.Get(Of Integer)("LoginType") = CInt(McLoginType.Ms)
        AniControlEnabled += 1
        ComboServerLogin.SelectedIndex = If(UseMs, 1, 0)
        AniControlEnabled -= 1
        If LabServerLoginHint IsNot Nothing Then
            LabServerLoginHint.Text = $"跟随启动页：{If(UseMs, "微软正版登录", "离线登录")}（去「启动」页切换）"
        End If
    End Sub

    Private Sub CheckServerLoginFollow_Change(sender As Object, user As Boolean) Handles CheckServerLoginFollow.Change
        If Not user Then Return
        Settings.Set("LinkServerLoginFollow", CheckServerLoginFollow.Checked)
        ApplyServerLoginFollow()
    End Sub

    Private Sub CheckServerDefault_Change(sender As Object, user As Boolean) Handles CheckServerDefault.Change
        If Not user Then Return
        Try
            Dim Addr As String = If(TextServerAddr.Text, "").Trim()
            If CheckServerDefault.Checked AndAlso Not ServerAddressLooksValid(Addr) Then
                AniControlEnabled += 1
                CheckServerDefault.SetChecked(False, False)
                AniControlEnabled -= 1
                Hint("先把「服务器地址」填对，再勾选默认进入。", HintType.Red, False)
                Return
            End If
            Settings.Set("LinkServerAddress", Addr)
            Settings.Set("LinkServerName", If(TextServerName.Text, "").Trim())
            Settings.Set("LinkServerDefault", CheckServerDefault.Checked)
            ServerStatusRefresh()
        Catch ex As Exception
            Logger.Error(ex, "保存默认服务器失败")
            Hint("保存失败：" & ex.Message, HintType.Red, False)
        End Try
    End Sub

    ' ── 本地服务端：启动 / 停止 / 配置 ─────────────────
    Private ServerProc As System.Diagnostics.Process = Nothing
    Private ServerVersion As String = ""

    '''<summary>手选过的那个 jar 所在目录（Forge 目录里没有 server.jar，这里会返回空）。</summary>
    Private Function ServerJarDir() As String
        Dim Jar As String = If(Settings.Get(Of String)("LinkServerJar"), "").Trim()
        If Jar = "" OrElse Not FileUtils.Exists(Jar) Then Return ""
        Return System.IO.Path.GetDirectoryName(Jar)
    End Function

    '''<summary>
    ''' 当前服务端目录。按可信度依次取：已装服务端下拉里选中的 → 最近一次安装/添加的那个
    ''' → 上次启动的那个 → 手选过 jar 的目录。
    ''' 必须**先认目录再看 jar**：Forge / NeoForge 装完只有 run.bat 和 libraries，没有 server.jar，
    ''' 只按 jar 找会让配置、白名单、开服全部失效，用起来就像「必须先开一次服才能改配置」。
    ''' </summary>
    Private Function ServerDir() As String
        Dim D As String = SelectedInstalledDir()
        If D <> "" AndAlso DirectoryUtils.Exists(D) Then Return D.TrimEnd("\"c)
        For Each Key In {"LinkServerDir", "LinkServerProcDir"}
            Dim P As String = If(Settings.Get(Of String)(Key), "").Trim()
            If P <> "" AndAlso DirectoryUtils.Exists(P) Then Return P.TrimEnd("\"c)
        Next
        Dim JarDir As String = ServerJarDir()
        If JarDir <> "" Then Return JarDir.TrimEnd("\"c)
        Return ""
    End Function

    'server.properties 里 difficulty / gamemode 的合法值，顺序与下拉框一一对应
    Private Shared ReadOnly ServerDifficulties As String() = {"peaceful", "easy", "normal", "hard"}
    Private Shared ReadOnly ServerGameModes As String() = {"survival", "creative", "adventure", "spectator"}

    Private Shared Function IndexOfValue(Values As String(), V As String) As Integer
        For i = 0 To Values.Length - 1
            If Values(i).Equals(If(V, "").Trim(), StringComparison.OrdinalIgnoreCase) Then Return i
        Next
        Return -1
    End Function

    Private Sub ServerConfigRefresh()
        Try
            '回填期间一律不触发自动保存：读到的值又写回去既没意义，也可能把用户还没看到的键改掉
            IsLoadingServerConfig = True
            Dim Dir As String = ServerDir()
            If Dir = "" Then Return
            Dim Props As String = Dir & "\server.properties"
            If Not FileUtils.Exists(Props) Then
                '还没有配置文件（服务端没启动过），界面先摆默认值，别留成未勾 / 空白
                CheckServerPvp.SetChecked(True, False)
                If If(TextServerViewDistance.Text, "").Trim() = "" Then TextServerViewDistance.Text = "10"
                Return
            End If
            Dim Map As New Dictionary(Of String, String)(StringComparer.OrdinalIgnoreCase)
            For Each Line In System.IO.File.ReadAllLines(Props)
                If Line.StartsWith("#") OrElse Not Line.Contains("=") Then Continue For
                Dim I As Integer = Line.IndexOf("="c)
                Map(Line.Substring(0, I).Trim()) = Line.Substring(I + 1).Trim()
            Next
            AniControlEnabled += 1
            TextServerPort.Text = If(Map.ContainsKey("server-port"), Map("server-port"), "25565")
            TextServerMax.Text = If(Map.ContainsKey("max-players"), Map("max-players"), "20")
            TextServerMotd.Text = If(Map.ContainsKey("motd"), Map("motd"), "A Minecraft Server")
            CheckServerWhitelist.SetChecked(If(Map.ContainsKey("white-list"), Map("white-list") = "true", False), False)
            '读文件里的真实值。以前这里一律按「未勾」显示，配上自动保存就等于
            '「随手改个难度」会把正版验证悄悄关掉 —— 界面必须反映事实
            CheckServerOnlineMode.SetChecked(If(Map.ContainsKey("online-mode"), Map("online-mode") = "true", False), False)
            Dim DiffIdx As Integer = IndexOfValue(ServerDifficulties, If(Map.ContainsKey("difficulty"), Map("difficulty"), "normal"))
            If DiffIdx >= 0 Then ComboServerDifficulty.SelectedIndex = DiffIdx
            Dim ModeIdx As Integer = IndexOfValue(ServerGameModes, If(Map.ContainsKey("gamemode"), Map("gamemode"), "survival"))
            If ModeIdx >= 0 Then ComboServerGamemode.SelectedIndex = ModeIdx
            TextServerViewDistance.Text = If(Map.ContainsKey("view-distance"), Map("view-distance"), "10")
            CheckServerPvp.SetChecked(If(Map.ContainsKey("pvp"), Map("pvp") = "true", True), False)
            AniControlEnabled -= 1
        Catch ex As Exception
            Logger.Warn(ex, "读取服务端配置失败")
        Finally
            IsLoadingServerConfig = False
            '切了服务端就把反馈行复位，否则还挂着上一个服务端的「已写入 14:32:05」
            If LabConfigAutoSave IsNot Nothing Then
                LabConfigAutoSave.Text = "改动会自动写入 server.properties，不需要再点保存"
            End If
        End Try
    End Sub

    '控件一改就排队保存（输入框有 500ms 防抖），不需要再点「保存配置」
    Private Sub ServerConfigControlChanged(sender As Object, e As Object) Handles TextServerPort.TextChanged, TextServerMax.TextChanged, TextServerViewDistance.TextChanged, TextServerMotd.TextChanged, CheckServerWhitelist.Change, CheckServerOnlineMode.Change, CheckServerPvp.Change, ComboServerDifficulty.SelectionChanged, ComboServerGamemode.SelectionChanged
        ServerConfigChanged(sender, e)
    End Sub

    '换了「已装服务端」里的另一个服：配置和玩家名单都要跟着换，否则看的是上一个服的数据
    Private Sub ComboInstalledServer_SelectionChanged(sender As Object, e As SelectionChangedEventArgs) Handles ComboInstalledServer.SelectionChanged
        If IsLoadingServerConfig Then Return
        If TextServerPort Is Nothing Then Return
        ServerConfigRefresh()
        WhitelistRefresh()
    End Sub

    '配置改动即时落盘：输入框停手 500ms 后写一次，勾选 / 下拉立即写。
    '不再要「保存配置」按钮 —— 用户改完还得记得点一下，忘了就等于白改。
    Private ConfigSaveTimer As System.Windows.Threading.DispatcherTimer = Nothing
    Private IsLoadingServerConfig As Boolean = False

    Private Sub ServerConfigChanged(sender As Object, e As Object)
        If IsLoadingServerConfig Then Return
        If ConfigSaveTimer Is Nothing Then
            ConfigSaveTimer = New System.Windows.Threading.DispatcherTimer()
            ConfigSaveTimer.Interval = TimeSpan.FromMilliseconds(500)
            AddHandler ConfigSaveTimer.Tick,
                Sub(s, ev)
                    ConfigSaveTimer.Stop()
                    ServerConfigSave(Silent:=True)
                End Sub
        End If
        ConfigSaveTimer.Stop()
        ConfigSaveTimer.Start()
    End Sub

    '''<summary>
    ''' 把界面上的配置写进 server.properties（其余键保持不变）。Silent 用于自动保存：
    ''' 输入中途的半截值（比如只打了「2」）不弹红字打扰，等用户打完自然会写成功。
    ''' </summary>
    Private Sub ServerConfigSave(Optional Silent As Boolean = False)
        Try
            Dim Dir As String = ServerDir()
            If Dir = "" Then
                If Not Silent Then
                    Hint("先在「已装服务端」里选一个服务端，才知道配置写到哪里。", HintType.Red, False)
                ElseIf LabConfigAutoSave IsNot Nothing Then
                    LabConfigAutoSave.Text = "还没选中服务端，配置暂未保存"
                End If
                Return
            End If
            Dim Port As Integer, Max As Integer
            If Not Integer.TryParse(If(TextServerPort.Text, "").Trim(), Port) OrElse Port < 1 OrElse Port > 65535 Then
                If Not Silent Then Hint("端口要填 1–65535 的整数。", HintType.Red, False)
                Return
            End If
            If Not Integer.TryParse(If(TextServerMax.Text, "").Trim(), Max) OrElse Max < 1 OrElse Max > 9999 Then
                If Not Silent Then Hint("最大人数要填 1–9999 的整数。", HintType.Red, False)
                Return
            End If
            Dim Props As String = Dir & "\server.properties"
            '视距留空 = 不改这一项（避免把服务端原有的值冲掉）
            Dim ViewText As String = If(TextServerViewDistance.Text, "").Trim()
            Dim ViewInt As Integer
            If ViewText <> "" AndAlso (Not Integer.TryParse(ViewText, ViewInt) OrElse ViewInt < 3 OrElse ViewInt > 32) Then
                If Not Silent Then Hint("视距要填 3–32 的整数。", HintType.Red, False)
                Return
            End If
            Dim Keys As New Dictionary(Of String, String)(StringComparer.OrdinalIgnoreCase) From {
                {"server-port", Port.ToString()},
                {"max-players", Max.ToString()},
                {"white-list", If(CheckServerWhitelist.Checked, "true", "false")},
                {"online-mode", If(CheckServerOnlineMode.Checked, "true", "false")},
                {"motd", If(TextServerMotd.Text, "").Trim()},
                {"difficulty", If(ComboServerDifficulty.SelectedIndex >= 0, ServerDifficulties(ComboServerDifficulty.SelectedIndex), "normal")},
                {"gamemode", If(ComboServerGamemode.SelectedIndex >= 0, ServerGameModes(ComboServerGamemode.SelectedIndex), "survival")},
                {"pvp", If(CheckServerPvp.Checked, "true", "false")}
            }
            If ViewText <> "" Then Keys("view-distance") = ViewInt.ToString()
            Dim Lines As New List(Of String)
            If FileUtils.Exists(Props) Then
                For Each Line In System.IO.File.ReadAllLines(Props)
                    Dim Handled As Boolean = False
                    If Not Line.StartsWith("#") AndAlso Line.Contains("=") Then
                        Dim I As Integer = Line.IndexOf("="c)
                        Dim K As String = Line.Substring(0, I).Trim()
                        If Keys.ContainsKey(K) Then
                            Lines.Add(K & "=" & Keys(K))
                            Keys.Remove(K)
                            Handled = True
                        End If
                    End If
                    If Not Handled Then Lines.Add(Line)
                Next
            End If
            For Each Kv In Keys
                Lines.Add(Kv.Key & "=" & Kv.Value)
            Next
            FileUtils.Write(Props, String.Join(vbCrLf, Lines) & vbCrLf, encoding:=New System.Text.UTF8Encoding(False))
            If Silent Then
                If LabConfigAutoSave IsNot Nothing Then LabConfigAutoSave.Text = "已自动写入 server.properties（" & Now.ToString("HH:mm:ss") & "，下次开服生效）"
            Else
                Hint("配置已保存，下次开服生效。", HintType.Green, False)
            End If
        Catch ex As Exception
            Logger.Error(ex, "保存服务端配置失败")
            '静默保存失败必须让用户看见：否则界面还挂着上次的成功时间，改了却没进文件
            If LabConfigAutoSave IsNot Nothing Then LabConfigAutoSave.Text = "写入失败：" & ex.Message
            Hint("服务端配置写入失败：" & ex.Message, HintType.Red, False)
        End Try
    End Sub

    '''<summary>
    ''' 「开服」：优先用「已装服务端」里选中的那个，其次最近安装 / 添加的，最后才让用户挑一次文件。
    ''' 目录里有 run.bat（Forge / NeoForge）就走 run.bat，有 jar 就走 jar —— 绝不会去跑 installer。
    ''' </summary>
    Private Sub BtnServerStart_Click(sender As Object, e As MouseButtonEventArgs) Handles BtnServerStart.Click
        If ServerProc IsNot Nothing AndAlso Not ServerProc.HasExited Then
            Hint("服务端已经在运行了。", HintType.Blue, False)
            Return
        End If
        Try
            Dim D As String = ServerDir()
            If D = "" Then
                Dim Selected As String = Dialogs.SelectFile("选择服务端（server.jar / fabric-server-launch.jar）", False, filter:={({"jar"}, "服务端 jar")}).FirstOrDefault()
                If String.IsNullOrWhiteSpace(Selected) Then Return
                If IO.Path.GetFileName(Selected).ToLowerInvariant().Contains("installer") Then
                    Hint("选中的是安装器（*installer.jar），不是服务端本体。" & vbCrLf &
                         "先让安装器跑完，目录里会出现 run.bat / server.jar，再选那个。", HintType.Red, False)
                    Return
                End If
                D = IO.Path.GetDirectoryName(Selected).TrimEnd("\"c)
                Settings.Set("LinkServerJar", Selected)
                Settings.Set("LinkServerDir", D)
                ServerConfigRefresh()
            End If
            StartServerByDir(D)
        Catch ex As Exception
            Logger.Error(ex, "开服失败")
            Hint("开服失败：" & ex.Message, HintType.Red, False)
        End Try
    End Sub

    '''<summary>
    ''' 按目录启动服务端：fabric-server-launch.jar → server.jar → run.bat。
    ''' 都没有时要说清是「只有安装器」还是「什么都没有」，别让用户以为装好了、去启动安装程序。
    ''' </summary>
    Private Sub StartServerByDir(Dir As String)
        '换了服务端（或上一台还没收干净）：旧的本机接管通道一律作废，免得命令发到已经不在的那个进程上
        CloseLocalRcon()
        HideTakeover()
        '跨网运行的话，工具得先开着外面才连得进来
        AutoLaunchNetToolOnServerStart()
        Dim D As String = Dir.TrimEnd("\"c)
        If Not DirectoryUtils.Exists(D) Then
            Hint("服务端目录不存在：" & D & vbCrLf & "如果被移动或删除了，用「添加已有」重新指一次。", HintType.Red, False)
            Return
        End If
        For Each N In {"fabric-server-launch.jar", "server.jar"}
            If FileUtils.Exists(D & "\" & N) Then
                Settings.Set("LinkServerJar", D & "\" & N)
                Settings.Set("LinkServerDir", D)
                StartServerWithJar(D & "\" & N)
                Return
            End If
        Next
        Dim Bat As String = D & "\run.bat"
        If FileUtils.Exists(Bat) Then
            Settings.Set("LinkServerDir", D)
            StartServerWithRunBat(D, Bat)
            Return
        End If
        Dim Installer As String = ""
        Try
            For Each F In DirectoryUtils.EnumerateFiles(D, False, "*installer*.jar")
                Installer = F
                Exit For
            Next
        Catch
        End Try
        If Installer <> "" Then
            Hint("这个目录里只有安装器（" & IO.Path.GetFileName(Installer) & "），还没有服务端本体。" & vbCrLf &
                 "回「下载 → 服务器」重装一次：现在会先把原版服务端下好，再静默安装加载器，装完才会有 run.bat。", HintType.Red, False)
        Else
            Hint("这个目录里找不到可启动的文件（server.jar / fabric-server-launch.jar / run.bat 都没有）。", HintType.Red, False)
        End If
    End Sub

    '''<summary>用 java -jar 启动（原版 / Paper / Purpur / Fabric）。</summary>
    Private Sub StartServerWithJar(Jar As String)
        WhitelistRefresh() '换了服务端目录，名单跟着变
        Dim Dir As String = System.IO.Path.GetDirectoryName(Jar)
        Try
            Dim Eula As String = Dir & "\eula.txt"
            Dim EulaText As String = If(FileUtils.Exists(Eula), System.IO.File.ReadAllText(Eula), "")
            If Not EulaText.Contains("eula=true") Then
                FileUtils.Write(Eula, "eula=true" & vbCrLf, encoding:=New System.Text.UTF8Encoding(False))
            End If
        Catch ex As Exception
            Logger.Warn(ex, "写 eula.txt 失败")
        End Try
        '首次开服默认离线：没有 server.properties 时按「离线优先」创建一份
        Dim Props As String = Dir & "\server.properties"
        If Not FileUtils.Exists(Props) Then
            Try
                Dim Lines As New List(Of String) From {
                    "server-port=25565", "max-players=20", "white-list=false", "online-mode=false",
                    "motd=A Minecraft Server", "view-distance=10", "spawn-protection=16"
                }
                FileUtils.Write(Props, String.Join(vbCrLf, Lines) & vbCrLf, encoding:=New System.Text.UTF8Encoding(False))
            Catch ex As Exception
                Logger.Warn(ex, "创建 server.properties 失败")
            End Try
        End If
        '开服前确保 RCON 可用。有它，启动器重启后 stdin 断了也能继续发命令 —— 这就是「接管」
        EnsureRconConfigured(Dir)
        '开服前按 UI 的「正版验证」写一次，保证文件与界面一致（默认离线）
        Try
            Dim PropsNow As String = Dir & "\server.properties"
            If FileUtils.Exists(PropsNow) Then
                Dim Lines As New List(Of String)
                For Each L In System.IO.File.ReadAllLines(PropsNow)
                    Lines.Add(If(L.StartsWith("online-mode="), $"online-mode={If(CheckServerOnlineMode.Checked, "true", "false")}", L))
                Next
                FileUtils.Write(PropsNow, String.Join(vbCrLf, Lines) & vbCrLf, encoding:=New System.Text.UTF8Encoding(False))
            End If
        Catch ex As Exception
            Logger.Warn(ex, "写 online-mode 失败")
        End Try
        'Java 走 PageServerInstall.GetJavaPath：没有可用 Java 时它会自己从 Mojang 下一个
        '（PCL 本体不带 Java，以前这里直接放弃，等于把环境问题甩给用户）
        Dim JavaPath As String = PageServerInstall.GetJavaPath(ServerRecordInfo(ServerDir())?.Item1)
        If String.IsNullOrWhiteSpace(JavaPath) OrElse Not FileUtils.Exists(JavaPath) Then
            Hint("没有可用的 Java，自动下载也没成功。可以到「设置 → 启动 → Java」里手动添加一个再回来开服。", HintType.Red, False)
            Return
        End If
        ServerVersion = ""
        '内存按机器给（原先写死 -Xmx2G），编码统一成 UTF-8：
        '.NET Framework 的 StandardInput 用系统 ANSI（GBK）写，Java 18+ 的 stdin 是 UTF-8
        '—— 不统一的话 say 中文会变成乱码 / 问号
        Dim MemMb As Integer = SuggestServerMemoryMb()
        Dim JvmArgs As String =
            $"-Xmx{MemMb}M -Xms{Math.Min(MemMb, 1024)}M " &
            "-Dfile.encoding=UTF-8 -Dstdout.encoding=UTF-8 -Dstderr.encoding=UTF-8 " &
            "-Dsun.stdout.encoding=UTF-8 -Dsun.stderr.encoding=UTF-8 -Dsun.stdin.encoding=UTF-8 " &
            $"-jar ""{System.IO.Path.GetFileName(Jar)}"" nogui"
        Dim Psi As New System.Diagnostics.ProcessStartInfo(JavaPath, JvmArgs) With {
            .WorkingDirectory = Dir,
            .UseShellExecute = False,
            .CreateNoWindow = True,
            .RedirectStandardInput = True,
            .RedirectStandardOutput = True,
            .RedirectStandardError = True,
            .StandardOutputEncoding = New System.Text.UTF8Encoding(False),
            .StandardErrorEncoding = New System.Text.UTF8Encoding(False)
        }
        ServerProc = System.Diagnostics.Process.Start(Psi)
        SaveServerProcRecord(ServerProc, Dir)
        AddHandler ServerProc.OutputDataReceived, AddressOf ServerOutput
        AddHandler ServerProc.ErrorDataReceived, AddressOf ServerOutput
        WatchServerExit(ServerProc)
        ServerProc.BeginOutputReadLine()
        ServerProc.BeginErrorReadLine()
        SetLocalState(StateBusy, "启动中", $"PID {ServerProc.Id}，正在加载世界")
        Hint("服务端已启动：首次启动要生成世界，可能需要 1–2 分钟。日志写在 server-run.log。", HintType.Blue, False)
    End Sub

    '''<summary>
    ''' 关掉「不是本进程起的」服务端（启动器重开后接管的那种）：先试 RCON 正常 stop，接不上就强杀。
    ''' 接管条上的「关闭后台服务端」和下面那排「关闭服务器」都走这里，逻辑只留一份。
    ''' </summary>
    Private Sub StopLeftoverServer()
        Dim OldPid As Integer = Settings.Get(Of Integer)("LinkServerProcPid")
        Dim Old As System.Diagnostics.Process = FindServerProcess(OldPid)
        If Old Is Nothing Then
            ClearServerProcRecord()
            CloseLocalRcon()
            HideTakeover()
            SetLocalState(StateIdle, "未运行")
            Hint("服务端没有在运行。", HintType.Blue, False)
            Return
        End If
        Dim Client As RconClient = If(RconLocal IsNot Nothing AndAlso RconLocal.IsOpen, RconLocal, Nothing)
        Dim Tip As String = If(Client IsNot Nothing,
            "可以走本机接管通道让它正常 stop、保存世界后退出。",
            "RCON 通道没接上，只能强制结束 —— 会丢掉未保存的进度（世界数据通常已落盘）。")
        If MyMsgBox($"这个服务端是上次启动器启动的，还在后台运行（PID {OldPid}）。" & vbCrLf & vbCrLf & Tip,
                    "关闭服务端", "关闭", "取消", IsWarn:=True) <> 1 Then Return
        '先挂上退出监听：这个进程不是本进程起的，但只要有句柄就收得到 Exited，
        '这样即使下面那条线程被异常打断，服务端一退状态也会自己被点灭
        WatchServerExit(Old)
        If Client IsNot Nothing Then
            SetLocalState(StateBusy, "关闭中", "已发 stop，等它自己退出")
            RunInNewThread(Sub()
                Dim Err As String = ""
                Dim StillAlive As Boolean = False
                Try
                    Client.Command("stop", Err)
                    Dim Waited As Integer = 0
                    Do While Not Old.HasExited AndAlso Waited < 20000
                        System.Threading.Thread.Sleep(500)
                        Waited += 500
                    Loop
                    If Not Old.HasExited Then
                        ForceKillServer(OldPid)
                        StillAlive = FindServerProcess(OldPid) IsNot Nothing
                    End If
                Catch ex As Exception
                    Logger.Warn(ex, "通过 RCON 关闭遗留服务端失败")
                    StillAlive = FindServerProcess(OldPid) IsNot Nothing
                Finally
                    '不管上面成没成，状态都得落地 —— 线程里抛异常就永远卡在「关闭中」是最糟的
                    RunInUi(Sub()
                        ClearServerProcRecord()
                        CloseLocalRcon()
                        HideTakeover()
                        If StillAlive Then
                            SetLocalState(StateError, "仍在运行", "stop 没生效，进程还在")
                            ServerConsoleAppend("[本机] stop 已发出，但进程还在（" & If(Err = "", "没有回执", Err) & "）")
                        Else
                            SetLocalState(StateIdle, "未运行")
                            ServerConsoleAppend("[本机] 已发送 stop，服务端已退出")
                        End If
                    End Sub)
                End Try
            End Sub, "Stop Server Via Rcon")
        Else
            SetLocalState(StateBusy, "关闭中", "正在结束进程，稍等")
            If ForceKillServer(OldPid) Then
                ClearServerProcRecord()
                CloseLocalRcon()
                HideTakeover()
                SetLocalState(StateIdle, "未运行", "已强制结束")
                ServerConsoleAppend($"[本机] 已强制结束遗留服务端 PID {OldPid}")
            Else
                SetLocalState(StateError, "结束失败", "权限不够？用任务管理器结束 java")
                Hint("强制结束失败，可能权限不够。用任务管理器结束 java 进程。", HintType.Red, False)
            End If
        End If
    End Sub

    Private Sub BtnTakeoverStop_Click(sender As Object, e As MouseButtonEventArgs) Handles BtnTakeoverStop.Click
        StopLeftoverServer()
    End Sub

    '''<summary>改过 server.properties 的 rcon 端口 / 密码后，断开重连一次。</summary>
    Private Sub BtnTakeoverReconnect_Click(sender As Object, e As MouseButtonEventArgs) Handles BtnTakeoverReconnect.Click
        CloseLocalRcon()
        ConnectLocalRcon()
    End Sub

    Private Sub BtnServerStop_Click(sender As Object, e As MouseButtonEventArgs) Handles BtnServerStop.Click
        If ServerProc Is Nothing OrElse ServerProc.HasExited Then
            '本进程没在管，可能还挂着上次启动器留下的那个
            StopLeftoverServer()
            Return
        End If
        If MyMsgBox("向服务端发送 stop，保存世界后关闭；玩家会被断开。继续？", "关闭服务器", "关闭", "取消", IsWarn:=True) <> 1 Then Return
        Try
            SetLocalState(StateBusy, "关闭中", "已发 stop，等它保存世界")
            WriteServerLine("stop")
            Dim P As System.Diagnostics.Process = ServerProc
            System.Threading.Tasks.Task.Run(Sub()
                Try
                    If Not P.WaitForExit(15000) Then P.Kill()
                    ClearServerProcRecord()
                    RunInUi(Sub() SetLocalState(StateIdle, "未运行"))
                Catch ex As Exception
                    RunInUi(Sub() SetLocalState(StateError, "关闭失败", "停止失败：" & ex.Message))
                End Try
            End Sub)
        Catch ex As Exception
            Logger.Error(ex, "关闭服务端失败")
            Hint("关闭失败：" & ex.Message, HintType.Red, False)
        End Try
    End Sub

    '''<summary>断开本机接管通道（本地服务端已经关掉 / 换了服务端目录时用）。</summary>
    Private Sub CloseLocalRcon()
        Try
            If RconLocal IsNot Nothing Then
                RconLocal.Close()
                RconLocal = Nothing
            End If
        Catch ex As Exception
            Logger.Warn(ex, "断开本机 RCON 通道失败")
        End Try
    End Sub

    '''<summary>
    ''' 盯着服务端进程：它自己退了（崩溃 / 被任务管理器结束 / stop 生效）就把状态点灭掉。
    ''' 没有这条，徽标会一直亮着「运行中」，比不显示还误导。
    ''' </summary>
    Private _ExitWatchedPids As New HashSet(Of Integer)

    Private Sub WatchServerExit(P As System.Diagnostics.Process)
        Try
            '同一个进程只挂一次：接管条上连点几次「关闭」会重复挂，退出时会连着清好几遍
            If P Is Nothing OrElse _ExitWatchedPids.Contains(P.Id) Then Return
            _ExitWatchedPids.Add(P.Id)
            P.EnableRaisingEvents = True
            AddHandler P.Exited, Sub(sender As Object, e As EventArgs)
                                     Dim Dead As System.Diagnostics.Process = TryCast(sender, System.Diagnostics.Process)
                                     If Dead IsNot Nothing Then _ExitWatchedPids.Remove(Dead.Id)
                                     RunInUi(Sub()
                                                 '本进程已经又起了新的服务端：这条退出是上一台的事，不能把新状态点灭
                                                 If ServerProc IsNot Nothing AndAlso Not ServerProc.HasExited AndAlso
                                                    Dead IsNot Nothing AndAlso Dead.Id <> ServerProc.Id Then Return
                                                 ClearServerProcRecord()
                                                 CloseLocalRcon()
                                                 HideTakeover()
                                                 SetLocalState(StateIdle, "未运行", "服务端已退出")
                                                 ServerConsoleAppend("[本机] 服务端进程已退出。")
                                             End Sub)
                                 End Sub
        Catch ex As Exception
            Logger.Warn(ex, "监听服务端退出失败")
        End Try
    End Sub

    Private Sub ServerOutput(sender As Object, e As System.Diagnostics.DataReceivedEventArgs)
        If e.Data Is Nothing Then Return
        Dim Line As String = e.Data
        Try
            Dim Dir As String = ServerDir()
            If Dir <> "" Then System.IO.File.AppendAllText(Dir & "\server-run.log", Line & vbCrLf, System.Text.Encoding.UTF8)
        Catch
        End Try
        If Line.Contains("Starting minecraft server version") Then
            Dim Idx As Integer = Line.IndexOf("version", StringComparison.OrdinalIgnoreCase)
            ServerVersion = Line.Substring(Idx + 7).Trim().TrimEnd("."c)
            RunInUi(Sub() SetLocalState(StateBusy, "启动中", $"服务端版本 {ServerVersion}，正在加载"))
        End If
        If Line.Contains("Done") AndAlso Line.Contains("For help") Then
            RunInUi(Sub() ServerMarkReady())
        End If
        '实时显示在界面上（文件日志照旧写，两个都要）
        RunInUi(Sub() ServerConsoleAppend(Line))
    End Sub

    ' ── 服务端控制台 ──────────────────────────────────
    Private Const ServerConsoleMaxChars As Integer = 200000
    Private ServerConsoleChars As Integer = 0
    '远程控制台单独计数：它和本地控制台是两块屏，互不覆盖，也互不清空
    Private RconConsoleChars As Integer = 0

    '''<summary>按物理内存推荐服务端堆大小（MB）：1/4，夹在 2G–8G，按 512M 取整。</summary>
    Private Function SuggestServerMemoryMb() As Integer
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
    ''' 往服务端 stdin 写一行。必须走字节流 + UTF-8：.NET Framework 的
    ''' <c>StandardInput</c> 是 StreamWriter，按系统 ANSI（GBK）编码，与 JVM 的 UTF-8 stdin 不匹配，
    ''' 中文命令会变成乱码。
    ''' </summary>
    Private Sub WriteServerLine(Line As String)
        Try
            Dim Bytes As Byte() = System.Text.Encoding.UTF8.GetBytes(Line & vbLf)
            ServerProc.StandardInput.BaseStream.Write(Bytes, 0, Bytes.Length)
            ServerProc.StandardInput.BaseStream.Flush()
        Catch ex As Exception
            Logger.Warn(ex, "写入服务端命令失败：" & Line)
        End Try
    End Sub

    '''<summary>追加一行到控制台。超过上限就砍掉前一半，避免长时间开服把内存吃满。</summary>
    Private Sub ServerConsoleAppend(Line As String)
        If TextServerConsole Is Nothing Then Return
        Try
            '自己记长度，别每次都读 TextBox.Text（那是 O(n) 重建字符串，刷屏时会卡）
            ServerConsoleChars += Line.Length + 2
            If ServerConsoleChars > ServerConsoleMaxChars Then
                Dim T As String = TextServerConsole.Text
                TextServerConsole.Text = T.Substring(T.Length \ 2)
                ServerConsoleChars = TextServerConsole.Text.Length
            End If
            TextServerConsole.AppendText(Line & vbCrLf)
            TextServerConsole.ScrollToEnd()
        Catch ex As Exception
            Logger.Warn(ex, "追加服务端控制台失败")
        End Try
    End Sub

    Private Sub SendServerCommand()
        Dim Cmd As String = If(TextServerCommand.Text, "").Trim()
        If Cmd = "" Then Return
        '服务端从 stdin 读的是不带斜杠的命令；玩家习惯带 /，帮着去掉
        If Cmd.StartsWith("/") Then Cmd = Cmd.Substring(1)
        '本机进程活着优先走 stdin（能拿到实时输出）
        If ServerRunning() Then
            Try
                WriteServerLine(Cmd)
                TextServerCommand.Text = ""
                ServerConsoleAppend("> " & Cmd)
            Catch ex As Exception
                Hint("发送失败：" & ex.Message, HintType.Red, False)
            End Try
            Return
        End If
        '进程不在、但本机接管通道还连着（启动器重启后的遗留服务端）：回显仍留在本地控制台
        If RconLocal IsNot Nothing AndAlso RconLocal.IsOpen Then
            TextServerCommand.Text = ""
            RconRunLocal(Cmd)
            Return
        End If
        '这块输入框从设计上就只管本机。远程连着也只提醒一句，不把命令偷偷发到别人的服务器上
        If Rcon IsNot Nothing AndAlso Rcon.IsOpen Then
            Hint("这里是本机服务端的命令框，不会发到远程。" & vbCrLf &
                 "要控制 " & If(TextRconHost.Text, "").Trim() & "，用下面「远程服务器（RCON）」卡片里的远程命令框。", HintType.Blue, False)
            Return
        End If
        Hint("本地服务端没在运行。想控制远程服务器，先在下面「远程服务器（RCON）」里连上。", HintType.Blue, False)
    End Sub

    '''<summary>
    ''' 远程卡片的独立控制台。RCON 的请求与回显只写这里 ——
    ''' 以前它和本机服务端共用一块 TextServerConsole，连上远程服之后本机日志会被别人的输出冲掉。
    ''' </summary>
    Private Sub RconConsoleAppend(Line As String)
        If TextRconConsole Is Nothing Then Return
        Try
            RconConsoleChars += Line.Length + 2
            If RconConsoleChars > ServerConsoleMaxChars Then
                Dim T As String = TextRconConsole.Text
                TextRconConsole.Text = T.Substring(T.Length \ 2)
                RconConsoleChars = TextRconConsole.Text.Length
            End If
            TextRconConsole.AppendText(Line & vbCrLf)
            TextRconConsole.ScrollToEnd()
        Catch ex As Exception
            Logger.Warn(ex, "追加远程控制台失败")
        End Try
    End Sub

    '''<summary>
    ''' 发一条远程 RCON 命令，回显只进远程控制台。
    ''' 本机服务端的命令走 <see cref="RconRunLocal"/>，两条线各有各的屏，不再靠参数区分。
    ''' </summary>
    Private Sub RconRun(Cmd As String, Optional OnDone As Action = Nothing)
        If Rcon Is Nothing OrElse Not Rcon.IsOpen Then Return
        RconConsoleAppend("> " & Cmd)
        RunInNewThread(Sub()
            Dim Err As String = ""
            Dim Res As String = Rcon.Command(Cmd, Err)
            RunInUi(Sub()
                If Err <> "" Then
                    RconConsoleAppend("! " & Err)
                ElseIf If(Res, "").Trim() <> "" Then
                    RconConsoleAppend(Res.TrimEnd())
                End If
                If OnDone IsNot Nothing Then OnDone()
            End Sub)
        End Sub, "Rcon Run")
    End Sub

    ' ── 远程服务器（RCON）──────────────────────────────
    ''' <summary>RCON 客户端。协议：4 字节长度 + 4 字节请求号 + 4 字节类型 + 正文 + 两个 0。全部小端。</summary>
    Private Class RconClient
        Private Tcp As System.Net.Sockets.TcpClient
        Private Stream As System.Net.Sockets.NetworkStream
        Private NextId As Integer = 1

        Public ReadOnly Property IsOpen As Boolean
            Get
                Try
                    Return Tcp IsNot Nothing AndAlso Tcp.Connected
                Catch
                    Return False
                End Try
            End Get
        End Property

        Public Function Connect(Host As String, Port As Integer, Password As String, ByRef Err As String) As Boolean
            Try
                Tcp = New System.Net.Sockets.TcpClient()
                Dim Conn = Tcp.ConnectAsync(Host, Port)
                If Not Conn.Wait(6000) Then
                    Err = "连接超时"
                    Close()
                    Return False
                End If
                Stream = Tcp.GetStream()
                Stream.ReadTimeout = 6000
                Dim Id As Integer = NextId
                NextId += 1
                SendPacket(Id, 3, Password) '3 = AUTH
                '服务端先回一个空 RESPONSE_VALUE，再回 AUTH_RESPONSE（失败时请求号为 -1）
                For i = 1 To 3
                    Dim PktType As Integer, ReqId As Integer, Body As String
                    If Not ReadPacket(PktType, ReqId, Body) Then
                        Err = "认证时连接中断"
                        Close()
                        Return False
                    End If
                    If PktType = 2 Then 'AUTH_RESPONSE
                        If ReqId = -1 Then
                            Err = "密码错误"
                            Close()
                            Return False
                        End If
                        Return True
                    End If
                Next
                Err = "没有收到认证响应"
                Close()
                Return False
            Catch ex As Exception
                Err = ex.Message
                Close()
                Return False
            End Try
        End Function

        Public Function Command(Cmd As String, ByRef Err As String) As String
            Try
                Dim Id As Integer = NextId
                NextId += 1
                SendPacket(Id, 2, Cmd) '2 = EXECCOMMAND
                Dim PktType As Integer, ReqId As Integer, Body As String
                If Not ReadPacket(PktType, ReqId, Body) Then
                    Err = "读取响应失败"
                    Return ""
                End If
                Return Body
            Catch ex As Exception
                Err = ex.Message
                Return ""
            End Try
        End Function

        Public Sub Close()
            Try
                If Stream IsNot Nothing Then Stream.Close()
            Catch
            End Try
            Try
                If Tcp IsNot Nothing Then Tcp.Close()
            Catch
            End Try
            Stream = Nothing
            Tcp = Nothing
        End Sub

        Private Sub SendPacket(Id As Integer, PktType As Integer, Body As String)
            Dim BodyBytes As Byte() = System.Text.Encoding.UTF8.GetBytes(Body)
            Dim Len As Integer = 4 + 4 + BodyBytes.Length + 2
            Dim Buf As New List(Of Byte)
            Buf.AddRange(BitConverter.GetBytes(Len))
            Buf.AddRange(BitConverter.GetBytes(Id))
            Buf.AddRange(BitConverter.GetBytes(PktType))
            Buf.AddRange(BodyBytes)
            Buf.Add(0)
            Buf.Add(0)
            Dim Arr As Byte() = Buf.ToArray()
            Stream.Write(Arr, 0, Arr.Length)
            Stream.Flush()
        End Sub

        Private Function ReadPacket(ByRef PktType As Integer, ByRef ReqId As Integer, ByRef Body As String) As Boolean
            Dim LenBytes(3) As Byte
            If Not ReadExact(LenBytes, 4) Then Return False
            Dim Len As Integer = BitConverter.ToInt32(LenBytes, 0)
            If Len < 10 OrElse Len > 4 * 1024 * 1024 Then Return False
            Dim Payload(Len - 1) As Byte
            If Not ReadExact(Payload, Len) Then Return False
            ReqId = BitConverter.ToInt32(Payload, 0)
            PktType = BitConverter.ToInt32(Payload, 4)
            '末尾两个 0 不算正文
            Body = System.Text.Encoding.UTF8.GetString(Payload, 8, Len - 10)
            Return True
        End Function

        Private Function ReadExact(Buffer As Byte(), Count As Integer) As Boolean
            Dim Got As Integer = 0
            Do While Got < Count
                Dim N As Integer = Stream.Read(Buffer, Got, Count - Got)
                If N <= 0 Then Return False
                Got += N
            Loop
            Return True
        End Function
    End Class

    Private Rcon As RconClient = Nothing

    '''<summary>
    ''' 本机接管通道：启动器重启后 stdin 没了，只能靠 RCON 接着管本机服务端。
    ''' 和上面那条 <see cref="Rcon"/> 严格分开 —— 远程卡片只管远程，本机遗留进程不进远程卡片的输入框和回显，
    ''' 否则「关掉启动器再打开」会看到远程卡片里赫然写着 127.0.0.1，谁都以为自己在管云服务器。
    ''' </summary>
    Private RconLocal As RconClient = Nothing

    ' 密码框里的占位符：真实密码不回显，只显示这几个圆点（长度也不暴露）
    Private Const RconPassMask As String = "••••••••"
    ' MC 的 RCON 默认端口；「重置」和设置缺省都用它
    Private Const DefaultRconPort As Integer = 25575

    '''<summary>
    ''' 取用户实际要用的 RCON 密码，三级回退：
    ''' ① 框里手输的（非占位符）→ ② 勾了「记住密码」时读设置 → ③ 目标是本机时，直接从服务端 server.properties 现读。
    ''' 默认配置下不会持久化密码，③ 保证本机服务端重启启动器后照样能自动接管。
    ''' ③ 只对本机地址生效 —— 连远程服却拿本机配置里的密码去凑，只会多一次莫名其妙的「密码错误」。
    ''' </summary>
    Private Function GetRconPassword(ForHost As String) As String
        Dim Shown As String = If(TextRconPass.Text, "").Trim()
        If Shown <> "" AndAlso Shown <> RconPassMask Then Return Shown
        If Settings.Get(Of Boolean)("LinkRconRememberPass") Then
            Dim Saved As String = If(Settings.Get(Of String)("LinkRconPass"), "").Trim()
            If Saved <> "" Then Return Saved
        End If
        If Not IsLocalHost(ForHost) Then Return ""
        Return ReadRconPasswordFromProps()
    End Function

    '''<summary>是不是本机地址（空白按本机算，默认就是这么用的）。</summary>
    Private Shared Function IsLocalHost(Host As String) As Boolean
        Dim H As String = If(Host, "").Trim().ToLowerInvariant()
        If H = "" Then Return True
        Return H = "127.0.0.1" OrElse H = "localhost" OrElse H = "::1" OrElse H = "0.0.0.0"
    End Function

    '''<summary>带端口的地址（127.0.0.1:25565 / [::1]:25565）是否指向本机。</summary>
    Private Shared Function IsLocalServerAddr(Addr As String) As Boolean
        Dim A As String = If(Addr, "").Trim()
        If A = "" Then Return True
        If A.StartsWith("[") Then
            Dim Close As Integer = A.IndexOf("]"c)
            If Close > 0 Then Return IsLocalHost(A.Substring(1, Close - 1))
        End If
        Dim I As Integer = A.LastIndexOf(":"c)
        Return IsLocalHost(If(I > 0, A.Substring(0, I), A))
    End Function

    '''<summary>读 server.properties 里的一个布尔键；文件不在或键不在就用 Fallback。</summary>
    Private Shared Function ReadPropBool(Dir As String, Key As String, Fallback As Boolean) As Boolean
        Try
            Dim P As String = Dir.TrimEnd("\"c) & "\server.properties"
            If Not FileUtils.Exists(P) Then Return Fallback
            For Each L As String In System.IO.File.ReadAllLines(P)
                Dim T As String = If(L, "").Trim()
                If T.StartsWith("#") OrElse Not T.Contains("=") Then Continue For
                Dim I As Integer = T.IndexOf("="c)
                If Not T.Substring(0, I).Trim().Equals(Key, StringComparison.OrdinalIgnoreCase) Then Continue For
                Return T.Substring(I + 1).Trim().Equals("true", StringComparison.OrdinalIgnoreCase)
            Next
        Catch ex As Exception
            Logger.Warn(ex, "读取 server.properties 失败：" & Key)
        End Try
        Return Fallback
    End Function

    '''<summary>从服务端目录的 server.properties 读 rcon.password —— 这是密码的权威来源。</summary>
    Private Function ReadRconPasswordFromProps() As String
        Try
            Dim Dir As String = ServerDir()
            If Dir = "" Then Dir = If(Settings.Get(Of String)("LinkServerProcDir"), "").Trim()
            If Dir = "" Then Return ""
            Dim Props As String = Dir.TrimEnd("\"c) & "\server.properties"
            If Not FileUtils.Exists(Props) Then Return ""
            For Each L As String In System.IO.File.ReadAllLines(Props)
                If L.StartsWith("rcon.password=") Then Return L.Substring("rcon.password=".Length).Trim()
            Next
        Catch ex As Exception
            Logger.Warn(ex, "读取 RCON 密码失败")
        End Try
        Return ""
    End Function

    Private Sub BtnRconConnect_Click(sender As Object, e As MouseButtonEventArgs) Handles BtnRconConnect.Click
        Dim Host As String = If(TextRconHost.Text, "").Trim()
        Dim Port As Integer
        Dim Pass As String = GetRconPassword(Host)
        If Host = "" Then
            Hint("先填服务器地址。", HintType.Blue, False)
            Return
        End If
        If Not Integer.TryParse(If(TextRconPort.Text, "").Trim(), Port) OrElse Port < 1 OrElse Port > 65535 Then
            Hint("端口要填 1–65535 的整数（MC 默认 25575）。", HintType.Red, False)
            Return
        End If
        If Pass = "" Then
            Hint("先填 RCON 密码 —— 服务器 server.properties 里的 rcon.password。", HintType.Red, False)
            Return
        End If
        Settings.Set("LinkRconHost", Host)
        Settings.Set("LinkRconPort", Port)
        '只有勾了「记住密码」才持久化；默认用完即弃
        If Settings.Get(Of Boolean)("LinkRconRememberPass") Then
            Settings.Set("LinkRconPass", Pass)
        End If
        BtnRconConnect.IsEnabled = False
        SetRemoteDetail("正在连接…")
        SetRemoteState(StateBusy, "连接中…")
        RunInNewThread(Sub()
            If Rcon IsNot Nothing Then Rcon.Close()
            Dim Client As New RconClient()
            Dim Err As String = ""
            Dim Ok As Boolean = Client.Connect(Host, Port, Pass, Err)
            RunInUi(Sub()
                BtnRconConnect.IsEnabled = True
                If Ok Then
                    Rcon = Client
                    SetRemoteDetail($"已连接 {Host}:{Port}（命令发往远程）")
                    SetRemoteState(StateRunning, "运行中")
                    RconConsoleAppend($"[RCON] 已连接 {Host}:{Port}")
                    '默认不保留密码：连上就把框清空，下次要重输
                    If Not Settings.Get(Of Boolean)("LinkRconRememberPass") Then TextRconPass.Text = ""
                    '玩家名单要跟着切到「远程模式」：连上远端后勾选是发到远端的，不能再显示本机名单
                    WhitelistRefresh()
                Else
                    SetRemoteDetail("连接失败")
                    SetRemoteState(StateError, "连接失败")
                    Client.Close()
                    Hint("RCON 连接失败：" & Err & vbCrLf &
                         "检查地址 / 端口 / 密码，以及服务端是否开了 enable-rcon=true。", HintType.Red, False)
                End If
            End Sub)
        End Sub, "Rcon Connect")
    End Sub

    Private Sub BtnRconDisconnect_Click(sender As Object, e As MouseButtonEventArgs) Handles BtnRconDisconnect.Click
        If Rcon IsNot Nothing Then
            Rcon.Close()
            Rcon = Nothing
            RconConsoleAppend("[RCON] 已断开")
        End If
        SetRemoteDetail("未连接")
        SetRemoteState(StateIdle, "未连接")
        WhitelistRefresh() '断开后回到本机名单
    End Sub

    '''<summary>
    ''' 重置远程连接这一块：断开、清空地址和密码框、端口回默认。
    ''' 只清界面和「这次填的地址」，不动服务端的 rcon.password，也不动「记住密码」保存的那份 ——
    ''' 那个开关有明确的撤销入口（取消勾选），别让重置顺手把人家的凭据删了。
    ''' </summary>
    Private Sub BtnRconResetUi_Click(sender As Object, e As MouseButtonEventArgs) Handles BtnRconResetUi.Click
        Try
            If Rcon IsNot Nothing Then
                Rcon.Close()
                Rcon = Nothing
                RconConsoleAppend("[RCON] 已断开")
            End If
            TextRconHost.Text = ""
            TextRconPort.Text = DefaultRconPort.ToString()
            TextRconPass.Text = ""
            Settings.Set("LinkRconHost", "")
            Settings.Set("LinkRconPort", DefaultRconPort)
            SetRemoteDetail("未连接")
            SetRemoteState(StateIdle, "未连接")
            WhitelistRefresh() '断开后回到本机名单
            Dim Extra As String = ""
            If Settings.Get(Of Boolean)("LinkRconRememberPass") Then
                Extra = vbCrLf & "「记住密码」保存的那份没动：要一起清掉就取消勾选「记住密码」。"
            End If
            Hint("已重置远程连接：地址、端口、密码框都清空了。" & Extra, HintType.Blue, False)
        Catch ex As Exception
            Logger.Warn(ex, "重置远程连接失败")
            Hint("重置失败：" & ex.Message, HintType.Red, False)
        End Try
    End Sub

    '''<summary>
    ''' 刷新远程连接：按设置重新回填地址 / 端口 / 密码显示，并检查已经建好的那条连接还活着没有 ——
    ''' 对面重启过、防火墙掐断之后，界面上的「已连接」会一直挂着，得有个手动校准的入口。
    ''' </summary>
    Private Sub BtnRconRefresh_Click(sender As Object, e As MouseButtonEventArgs) Handles BtnRconRefresh.Click
        Try
            '回填会按「记住密码」决定清空还是填占位符；用户刚手输的那份先留一把，别顺手抹掉
            Dim Typed As String = If(TextRconPass.Text, "").Trim()
            RconRefresh()
            If Typed <> "" AndAlso Typed <> RconPassMask AndAlso If(TextRconPass.Text, "").Trim() = "" Then
                TextRconPass.Text = Typed
            End If
            Dim Alive As Boolean = False
            If Rcon IsNot Nothing Then
                Alive = Rcon.IsOpen
                If Not Alive Then
                    '连接已经死了：关掉丢掉，别让「连接」按钮以为还连着
                    Rcon.Close()
                    Rcon = Nothing
                End If
            End If
            If Alive Then
                SetRemoteDetail($"已连接 {If(TextRconHost.Text, "").Trim()}:{If(TextRconPort.Text, "").Trim()}（命令发往远程）")
                SetRemoteState(StateRunning, "运行中")
                Hint("已刷新：连接仍然有效。", HintType.Green, False)
            Else
                SetRemoteDetail("未连接")
                SetRemoteState(StateIdle, "未连接")
                Hint("已刷新：当前未连接。", HintType.Blue, False)
            End If
            WhitelistRefresh() '连着就显示远程说明，断了就回本机名单
        Catch ex As Exception
            Logger.Warn(ex, "刷新远程连接失败")
        End Try
    End Sub

    Private Sub RconRefresh()
        Try
            If TextRconHost Is Nothing Then Return
            TextRconHost.Text = If(Settings.Get(Of String)("LinkRconHost"), "").Trim()
            TextRconPort.Text = Settings.Get(Of Integer)("LinkRconPort").ToString()
            '密码默认不回显，也不持久化：只有勾了「记住密码」才有东西可显示
            Dim Remember As Boolean = Settings.Get(Of Boolean)("LinkRconRememberPass")
            Dim Pass As String = If(Remember, If(Settings.Get(Of String)("LinkRconPass"), "").Trim(), "")
            Dim ShowPlain As Boolean = CheckRconShowPass IsNot Nothing AndAlso CheckRconShowPass.Checked
            TextRconPass.Text = If(Pass = "", "", If(ShowPlain, Pass, RconPassMask))
            If CheckRconRememberPass IsNot Nothing Then CheckRconRememberPass.SetChecked(Remember, False)
            '没记住密码就没有「存起来的那份」可显示：框里本来就是明文或空的，这个勾选框没用，置灰免得误导
            If CheckRconShowPass IsNot Nothing Then
                If Not Remember Then CheckRconShowPass.SetChecked(False, False)
                CheckRconShowPass.IsEnabled = Remember
            End If
        Catch ex As Exception
            Logger.Warn(ex, "回填 RCON 设置失败")
        End Try
    End Sub

    '''<summary>
    ''' 按当前状态决定密码框显示占位符还是明文。
    ''' 密码**以 server.properties 里的 rcon.password 为准**，启动器只是生成器和抄写员 ——
    ''' 所以必须让用户能看见、能抄走，否则换个工具就抓瞎。
    ''' </summary>
    Private Sub RefreshRconPassDisplay()
        If TextRconPass Is Nothing Then Return
        Dim Saved As String = If(Settings.Get(Of String)("LinkRconPass"), "").Trim()
        Dim Shown As String = If(TextRconPass.Text, "").Trim()
        If Saved = "" Then Return
        '占位符 → 明文、明文 → 占位符都可以切；框里是用户刚输的新值就别动
        If CheckRconShowPass.Checked Then
            If Shown = RconPassMask Then TextRconPass.Text = Saved
        Else
            If Shown = Saved Then TextRconPass.Text = RconPassMask
        End If
    End Sub

    Private Sub CheckRconShowPass_Change(sender As Object, user As Boolean) Handles CheckRconShowPass.Change
        RefreshRconPassDisplay()
    End Sub

    Private Sub CheckRconRememberPass_Change(sender As Object, user As Boolean) Handles CheckRconRememberPass.Change
        If Not user Then Return
        If CheckRconRememberPass.Checked Then
            '强提醒：这是把管理员凭据落盘，不是普通开关
            If MyMsgBox("会把 RCON 密码加密保存到本机，下次打开启动器不用再输。" & vbCrLf & vbCrLf &
                        "RCON 密码等同于管理员权限：能保存就意味着它在这台电脑上留了一份副本，" &
                        "别人用到这台机器（或拿到启动器配置）就能直接控制服务端。" & vbCrLf & vbCrLf &
                        "公用电脑、或服务端不在本机时，建议保持不勾 —— 每次连接手输一次更安全。",
                        "记住 RCON 密码", "我了解，记住", "取消", IsWarn:=True) <> 1 Then
                CheckRconRememberPass.SetChecked(False, False)
                Return
            End If
            Settings.Set("LinkRconRememberPass", True)
            '把框里已经输好的那个存下来，不用等下次连接
            Dim Typed As String = If(TextRconPass.Text, "").Trim()
            If Typed <> "" AndAlso Typed <> RconPassMask Then Settings.Set("LinkRconPass", Typed)
            RconRefresh()
        Else
            '关掉就把存过的擦掉，别再留着
            Settings.Set("LinkRconRememberPass", False)
            Settings.Set("LinkRconPass", "")
            RconRefresh()
        End If
    End Sub

    '''<summary>
    ''' 给**本机**服务端重新生成 RCON 密码：只改这个服务端目录里的 server.properties，
    ''' 新密码打印到本地控制台（可复制），不碰远程卡片里的任何东西。
    ''' </summary>
    Private Sub BtnRconReset_Click(sender As Object, e As MouseButtonEventArgs) Handles BtnRconReset.Click
        Dim Dir As String = ServerDir()
        If Dir = "" Then
            Hint("先在上面「已装服务端」里选一个服务端 —— 密码要写进它的 server.properties，没目录就无处可写。", HintType.Blue, False)
            Return
        End If
        Dim Props As String = Dir.TrimEnd("\"c) & "\server.properties"
        If Not FileUtils.Exists(Props) Then
            Hint("这个服务端目录里没有 server.properties（还没启动过一次？）。先开一次服生成配置，再来改密码。", HintType.Blue, False)
            Return
        End If
        If MyMsgBox("会给这个本机服务端生成新的随机 RCON 密码，写进它的 server.properties。" & vbCrLf & vbCrLf &
                    "密码始终以 server.properties 里的 rcon.password 为准 —— 换别的启动器、或者直接用命令行开服，" &
                    "用的也是这个密码，不会变成只有我们能连。" & vbCrLf & vbCrLf &
                    "新密码会打印在下面的服务端控制台里，方便复制；已经在跑的服务端要重启后才用新密码。",
                    "重新生成 RCON 密码", "生成", "取消", IsWarn:=True) <> 1 Then Return
        Dim NewPass As String = Guid.NewGuid().ToString("N").Substring(0, 16)
        Try
            Dim Lines As New List(Of String)
            Dim HasPass As Boolean = False
            For Each L As String In System.IO.File.ReadAllLines(Props)
                If L.StartsWith("rcon.password=") Then
                    Lines.Add("rcon.password=" & NewPass)
                    HasPass = True
                Else
                    Lines.Add(L)
                End If
            Next
            If Not HasPass Then Lines.Add("rcon.password=" & NewPass)
            FileUtils.Write(Props, String.Join(vbCrLf, Lines) & vbCrLf, encoding:=New System.Text.UTF8Encoding(False))
        Catch ex As Exception
            Logger.Warn(ex, "写入新的 RCON 密码失败")
            Hint("写入失败：" & ex.Message, HintType.Red, False)
            Return
        End Try
        '旧连接用的还是旧密码，先断 —— 只断本机这条，远程卡片那条与它无关
        CloseLocalRcon()
        ServerConsoleAppend("[本机] 已生成新的 RCON 密码（重启服务端后生效）：" & NewPass)
        Hint("新的 RCON 密码已打印在下面的服务端控制台里，可以直接选中复制。" & vbCrLf &
             "服务端重启后才用新密码。", HintType.Green, False)
    End Sub

    '''<summary>
    ''' 远程卡片上的「关闭服务器」：经 RCON 发 stop，然后盯着这条通道 ——
    ''' 服务端真的退出了，TCP 一定会断；断不开就是没退，得如实说。
    ''' </summary>
    Private Sub BtnRconStop_Click(sender As Object, e As MouseButtonEventArgs) Handles BtnRconStop.Click
        If Rcon Is Nothing OrElse Not Rcon.IsOpen Then
            Hint("先点「连接」把 RCON 通道建立起来，才能远程关掉服务端。" & vbCrLf &
                 "连不上就说明那边没开 enable-rcon，只能登录那台机器自己关。", HintType.Blue, False)
            Return
        End If
        Dim Host As String = If(TextRconHost.Text, "").Trim()
        If MyMsgBox($"经 RCON 向 {Host} 发送 stop：服务端会保存世界、断开所有玩家后退出。" & vbCrLf & vbCrLf &
                    "进程由那边自己收尾 —— 如果它跑在面板 / 容器里，面板可能会把它重新拉起来，这不归启动器管。继续？",
                    "关闭远程服务器", "关闭", "取消", IsWarn:=True) <> 1 Then Return
        SetRemoteState(StateBusy, "正在关闭…")
        SetRemoteDetail("已发送 stop，等待退出…")
        RconConsoleAppend("[RCON] 发送 stop，等待服务端保存世界并退出")
        Dim Client As RconClient = Rcon
        RunInNewThread(Sub()
            Dim Err As String = ""
            Client.Command("stop", Err)
            RunInUi(Sub()
                If Err <> "" Then
                    RconConsoleAppend("! " & Err)
                    SetRemoteState(StateError, "关闭失败")
                    SetRemoteDetail("关闭失败：" & Err)
                    Return
                End If
                WatchRemoteStop()
            End Sub)
        End Sub, "Rcon Stop")
    End Sub

    '''<summary>
    ''' stop 发出去之后每秒探一次活（发 list，看回不回），最多等 40 秒。
    ''' 通道断了 = 服务端确实退出了；一直不断 = stop 没生效，别谎报「已停止」。
    ''' </summary>
    Private Sub WatchRemoteStop()
        Dim Client As RconClient = Rcon
        If Client Is Nothing Then Return
        RunInNewThread(Sub()
            Dim Waited As Integer = 0
            Dim Dead As Boolean = False
            Do While Waited < 40000
                System.Threading.Thread.Sleep(1000)
                Waited += 1000
                If Not Client.IsOpen Then
                    Dead = True
                    Exit Do
                End If
                Dim Err As String = ""
                Client.Command("list", Err)
                If Err <> "" Then
                    Dead = True
                    Exit Do
                End If
            Loop
            'IsOpen 只看 Socket.Connected，它可能还赖着 True；命令发不出去 / 读不回来才是真断了
            Dim StillAlive As Boolean = (Not Dead) AndAlso Waited >= 40000
            RunInUi(Sub()
                If StillAlive Then
                    SetRemoteState(StateError, "仍在运行")
                    SetRemoteDetail("stop 没生效，40 秒内没退出")
                    RconConsoleAppend("[RCON] 40 秒过去服务端还响应命令：stop 没生效（控制台可能被面板锁着），去那边看一下进程。")
                    Return
                End If
                If Rcon IsNot Nothing Then
                    Rcon.Close()
                    Rcon = Nothing
                End If
                SetRemoteState(StateIdle, "已关闭")
                SetRemoteDetail("连接已断开（服务端已关闭）")
                RconConsoleAppend("[RCON] 通道断开，服务端已停止")
                WhitelistRefresh()
            End Sub)
        End Sub, "Watch Remote Stop")
    End Sub

    Private Sub BtnServerCommandSend_Click(sender As Object, e As MouseButtonEventArgs) Handles BtnServerCommandSend.Click
        SendServerCommand()
    End Sub

    Private Sub BtnServerConsoleClear_Click(sender As Object, e As MouseButtonEventArgs) Handles BtnServerConsoleClear.Click
        If TextServerConsole IsNot Nothing Then
            TextServerConsole.Clear()
            ServerConsoleChars = 0
        End If
    End Sub

    Private Sub TextServerCommand_KeyDown(sender As Object, e As System.Windows.Input.KeyEventArgs) Handles TextServerCommand.KeyDown
        If e.Key = System.Windows.Input.Key.Enter Then
            e.Handled = True
            SendServerCommand()
        End If
    End Sub

    ' ── 远程命令行（独立于本地控制台）──────────────────
    Private Sub SendRconCommand()
        If Rcon Is Nothing OrElse Not Rcon.IsOpen Then
            Hint("还没连上远程服务器：先在远程卡片里点「连接」。", HintType.Blue, False)
            Return
        End If
        Dim Cmd As String = If(TextRconCommand.Text, "").Trim()
        If Cmd = "" Then Return
        If Cmd.StartsWith("/") Then Cmd = Cmd.Substring(1)
        TextRconCommand.Text = ""
        RconRun(Cmd)
    End Sub

    Private Sub BtnRconSend_Click(sender As Object, e As MouseButtonEventArgs) Handles BtnRconSend.Click
        SendRconCommand()
    End Sub

    Private Sub TextRconCommand_KeyDown(sender As Object, e As System.Windows.Input.KeyEventArgs) Handles TextRconCommand.KeyDown
        If e.Key = System.Windows.Input.Key.Enter Then
            e.Handled = True
            SendRconCommand()
        End If
    End Sub

    Private Sub BtnRconClear_Click(sender As Object, e As MouseButtonEventArgs) Handles BtnRconClear.Click
        If TextRconConsole IsNot Nothing Then
            TextRconConsole.Clear()
            RconConsoleChars = 0
        End If
    End Sub

    Private Sub ServerMarkReady()
        Dim Dir As String = ServerDir()
        Dim Port As Integer = 25565
        Try
            Dim Props As String = Dir & "\server.properties"
            If FileUtils.Exists(Props) Then
                For Each L In System.IO.File.ReadAllLines(Props)
                    If L.StartsWith("server-port=") Then Integer.TryParse(L.Substring("server-port=".Length).Trim(), Port)
                Next
            End If
        Catch
        End Try
        Dim Addr As String = $"127.0.0.1:{Port}"
        SetLocalState(StateRunning, "运行中", $"已就绪：{Addr}" & If(ServerVersion <> "", $"（服务端 {ServerVersion}）", ""))
        TextServerAddr.Text = Addr
        Settings.Set("LinkServerAddress", Addr)
        ServerStatusRefresh()
        Hint($"本地服务端已就绪：{Addr}", HintType.Green, False)
    End Sub

    ' ── 白名单与 mod 目录 ────────────────────────────
    Private Function ServerDirOrHint() As String
        Dim Dir As String = ServerDir()
        If Dir = "" Then Hint("先在「已装服务端」里选一个服务端（或在下面连上远程 RCON），才知道文件写到哪里。", HintType.Red, False)
        Return Dir
    End Function

        Private Sub BtnWhitelistLoad_Click(sender As Object, e As MouseButtonEventArgs) Handles BtnWhitelistLoad.Click
        WhitelistRefresh()
    End Sub

    Private Function WhitelistNames() As List(Of String)
        Dim Names As New List(Of String)
        Dim Dir As String = ServerDir()
        If Dir = "" Then Return Names
        Dim P As String = Dir & "\whitelist.json"
        If Not FileUtils.Exists(P) Then Return Names
        Try
            Dim Arr As Newtonsoft.Json.Linq.JArray = Newtonsoft.Json.Linq.JArray.Parse(System.IO.File.ReadAllText(P))
            For Each Item In Arr
                Dim N As String = Item("name")?.ToString
                If Not String.IsNullOrWhiteSpace(N) Then Names.Add(N)
            Next
        Catch ex As Exception
            Logger.Warn(ex, "读取白名单失败")
        End Try
        Return Names
    End Function

    '''<summary>重建玩家名单：一行一个玩家，勾选 = 管理员。程序改 Checked 会触发 Changed，用标志位挡住。</summary>
    Private IsRefreshingPlayers As Boolean = False

    Private Sub WhitelistRefresh()
        If PanPlayerList Is Nothing Then Return
        Try
            IsRefreshingPlayers = True
            PanPlayerList.Children.Clear()
            '远程优先：这时勾选是经 RCON 发到远端的（UseRconForPlayers），名单就不能显示本机文件 ——
            '否则看着是本机那几个玩家，一勾却把远端服的某人提成了管理员
            If UseRconForPlayers() Then
                PanPlayerList.Children.Add(New TextBlock With {
                    .Text = "（远程服务器：填好名字点「添加玩家 / 删除玩家」即可走 RCON 立即生效；管理员请在下面的命令框用 op / deop 设置 —— 远程读不到完整名单，所以这里不列出来）",
                    .Opacity = 0.6, .FontSize = 12, .TextWrapping = TextWrapping.Wrap, .Margin = New Thickness(4, 6, 4, 0)})
                Return
            End If
            If ServerDir() = "" Then
                PanPlayerList.Children.Add(New TextBlock With {
                    .Text = "（还没选服务端。在「已装服务端」里选一个，或在下面「远程服务器（RCON）」连上远程服）",
                    .Opacity = 0.6, .FontSize = 12, .TextWrapping = TextWrapping.Wrap, .Margin = New Thickness(4, 6, 4, 0)})
                Return
            End If
            Dim Ops As List(Of String) = OpsNames()
            Dim Names As New List(Of String)(WhitelistNames())
            '没开白名单时，管理员也得能看到，否则名单是空的
            For Each O In Ops
                If Not Names.Any(Function(N) N.Equals(O, StringComparison.OrdinalIgnoreCase)) Then Names.Add(O)
            Next
            If Not Names.Any() Then
                PanPlayerList.Children.Add(New TextBlock With {
                    .Text = "（还没有玩家。用上面的输入框添加白名单玩家。）",
                    .Opacity = 0.6, .FontSize = 12, .Margin = New Thickness(4, 6, 0, 0)})
                Return
            End If
            For Each N In Names
                Dim Item As New MyListItem With {
                    .Title = N, .Type = MyListItem.CheckType.CheckBox, .Height = 28,
                    .Tag = N, .MinPaddingRight = 35}
                Item.SetChecked(Ops.Any(Function(O) O.Equals(N, StringComparison.OrdinalIgnoreCase)), False, False)
                AddHandler Item.Changed, AddressOf PlayerCheck_Changed
                PanPlayerList.Children.Add(Item)
            Next
        Catch ex As Exception
            Logger.Warn(ex, "刷新玩家名单失败")
        Finally
            IsRefreshingPlayers = False
        End Try
    End Sub

    '''<summary>勾选 / 取消勾选 = 设为 / 取消管理员。只在用户操作时响应，重建列表不算。</summary>
    Private Sub PlayerCheck_Changed(sender As Object, e As RouteEventArgs)
        If IsRefreshingPlayers OrElse Not e.RaiseByMouse Then Return
        Dim Item As MyListItem = TryCast(sender, MyListItem)
        If Item Is Nothing Then Return
        Dim Name As String = If(Item.Tag, "").ToString
        If Name = "" Then Return
        ApplyOpChange(New List(Of String) From {Name}, Item.Checked)
    End Sub

    ' ── 管理员（OP）────────────────────────────────────
    '''<summary>读取 ops.json 里的管理员名字。</summary>
    Private Function OpsNames() As List(Of String)
        Dim Names As New List(Of String)
        Dim Dir As String = ServerDir()
        If Dir = "" Then Return Names
        Dim P As String = Dir & "\ops.json"
        If Not FileUtils.Exists(P) Then Return Names
        Try
            Dim Arr As Newtonsoft.Json.Linq.JArray = Newtonsoft.Json.Linq.JArray.Parse(System.IO.File.ReadAllText(P))
            For Each Item In Arr
                Dim N As String = Item("name")?.ToString
                If Not String.IsNullOrWhiteSpace(N) Then Names.Add(N)
            Next
        Catch ex As Exception
            Logger.Warn(ex, "读取管理员名单失败")
        End Try
        Return Names
    End Function

    '''<summary>写 ops.json。管理员权限等级统一按 4（服主级）。</summary>
    Private Sub OpsWrite(Names As List(Of String))
        Dim Dir As String = ServerDirOrHint()
        If Dir = "" Then Return
        Dim Arr As New Newtonsoft.Json.Linq.JArray
        For Each N In Names
            Arr.Add(New Newtonsoft.Json.Linq.JObject From {
                {"uuid", OfflineUuid(N)}, {"name", N}, {"level", 4}, {"bypassesPlayerLimit", False}})
        Next
        FileUtils.Write(Dir & "\ops.json", Arr.ToString(Newtonsoft.Json.Formatting.Indented) & vbCrLf,
                        encoding:=New System.Text.UTF8Encoding(False))
        WhitelistRefresh()
    End Sub

    Private Function ServerRunning() As Boolean
        Try
            Return ServerProc IsNot Nothing AndAlso Not ServerProc.HasExited
        Catch
            Return False
        End Try
    End Function

    ' ── 运行状态徽标 ────────────────────────────────────
    '两张卡片各挂一个圆点 + 一句话。等级只有四档，颜色写死（不跟主题走）：
    '灰 = 没跑、绿 = 跑着、黄 = 正在变（启动中 / 关闭中）、红 = 出了岔子。
    '不用主题画刷是因为 ColorBrush* 全是蓝灰系，绿和黄没有对应键，硬套会看不出区别。
    Private Const StateIdle As Integer = 0
    Private Const StateRunning As Integer = 1
    Private Const StateBusy As Integer = 2
    Private Const StateError As Integer = 3

    Private Sub SetStateBadge(Dot As System.Windows.Shapes.Ellipse, Lab As TextBlock, Level As Integer, Text As String)
        Try
            Dim C As System.Windows.Media.Color
            Select Case Level
                Case StateRunning : C = System.Windows.Media.Color.FromRgb(&H22, &HA5, &H59)
                Case StateBusy : C = System.Windows.Media.Color.FromRgb(&HE0, &HA3, &H3E)
                Case StateError : C = System.Windows.Media.Color.FromRgb(&HD9, &H53, &H4F)
                Case Else : C = System.Windows.Media.Color.FromRgb(&H9A, &HA0, &HA6)
            End Select
            If Dot IsNot Nothing Then
                Dot.Fill = New System.Windows.Media.SolidColorBrush(C)
                '没跑的时候点暗一点，跑起来才亮 —— 扫一眼就能分出来
                Dot.Opacity = If(Level = StateRunning, 1.0, 0.7)
            End If
            If Lab IsNot Nothing AndAlso Text <> "" Then Lab.Text = Text
        Catch ex As Exception
            Logger.Warn(ex, "更新运行状态徽标失败")
        End Try
    End Sub

    '''<summary>本机服务端的状态：徽标 + 下面那行细节（PID / 端口 / 版本）。</summary>
    Private Sub SetLocalState(Level As Integer, StateText As String, Optional Detail As String = Nothing)
        SetStateBadge(DotLocalState, LabLocalState, Level, StateText)
        '不给 Detail 就是「这个状态没有附加信息」—— 必须清空，
        '否则从「启动中（PID …）」回到「未运行」时，那行旧字会一直挂在那儿
        If LabServerProc IsNot Nothing Then LabServerProc.Text = If(Detail, "")
        _LocalStateLevel = Level
        SyncLocalDetail()
    End Sub

    '''<summary>
    ''' 细节行只在「带徽标里没有的信息」时才露出来。
    ''' 没启动时徽标写「未运行」、细节也写着「未运行」，同一句话并排出现两次，看着就像状态没对上；
    ''' 跑起来后细节是端口 / PID / 版本，跟徽标不重样，那才是它该出现的时候。
    ''' </summary>
    Private Sub SyncLocalDetail()
        If LabServerProc Is Nothing Then Return
        Dim D As String = If(LabServerProc.Text, "").Trim()
        LabServerProc.Visibility = If(D = "" OrElse D = If(LabLocalState.Text, "").Trim(), Visibility.Collapsed, Visibility.Visible)
    End Sub

    '''<summary>远程服务端的状态。只有 RCON 通道通着才判断得出它在跑，断了就说「未连接」。</summary>
    Private Sub SetRemoteState(Level As Integer, StateText As String)
        SetStateBadge(DotRemoteState, LabRemoteState, Level, StateText)
        '回到「未连接」时把「已连接 host:port」这类旧细节清掉，别让两句话互相打架
        If Level = StateIdle AndAlso LabRconState IsNot Nothing Then LabRconState.Text = ""
        _RemoteStateLevel = Level
        SyncRemoteDetail()
    End Sub

    Private Sub SetRemoteDetail(Text As String)
        If LabRconState IsNot Nothing Then LabRconState.Text = Text
        SyncRemoteDetail()
    End Sub

    '''<summary>跟本机同理：细节和徽标写同一句话（都「未连接」）就只留徽标那一处。</summary>
    Private Sub SyncRemoteDetail()
        If LabRconState Is Nothing Then Return
        Dim D As String = If(LabRconState.Text, "").Trim()
        LabRconState.Visibility = If(D = "" OrElse D = If(LabRemoteState.Text, "").Trim(), Visibility.Collapsed, Visibility.Visible)
    End Sub

    ' ── 状态自检 ──────────────────────────────────────
    '状态文本靠事件改，事件一旦丢了（线程抛异常、进程被外部杀掉、RCON 先断），
    '界面就会停在「关闭中」「启动中」上不动 —— 服明明关了，界面还写着在关。
    '这里每 2 秒对一次真实现状：进程没了就点灭，RCON 通道断了就回「未连接」。

    Private _StateWatch As System.Windows.Threading.DispatcherTimer
    Private _LocalStateLevel As Integer = 0
    Private _RemoteStateLevel As Integer = 0

    Private Sub StartStateWatch()
        Try
            If _StateWatch Is Nothing Then
                _StateWatch = New System.Windows.Threading.DispatcherTimer With {
                    .Interval = TimeSpan.FromSeconds(2)
                }
                AddHandler _StateWatch.Tick, AddressOf StateWatchTick
            End If
            If Not _StateWatch.IsEnabled Then _StateWatch.Start()
        Catch ex As Exception
            Logger.Warn(ex, "启动运行状态自检失败")
        End Try
    End Sub

    Private Sub StopStateWatch()
        Try
            If _StateWatch IsNot Nothing Then _StateWatch.Stop()
        Catch
        End Try
    End Sub

    Private Sub StateWatchTick(sender As Object, e As EventArgs)
        Try
            '① 本机：进程已经没了，界面却还挂着「启动中 / 运行中 / 关闭中」
            If _LocalStateLevel <> StateIdle Then
                Dim ProcGone As Boolean
                If ServerProc IsNot Nothing Then
                    ProcGone = ServerProc.HasExited
                Else
                    Dim Pid As Integer = Settings.Get(Of Integer)("LinkServerProcPid")
                    ProcGone = (Pid <= 0) OrElse (FindServerProcess(Pid) Is Nothing)
                End If
                If ProcGone Then
                    ClearServerProcRecord()
                    CloseLocalRcon()
                    HideTakeover()
                    SetLocalState(StateIdle, "未运行")
                    ServerConsoleAppend("[本机] 服务端已退出（自动检测）")
                End If
            End If
            '② 远程：通道悄悄断了（对面重启 / 网络断了），别一直亮着「运行中」
            If _RemoteStateLevel <> StateIdle AndAlso _RemoteStateLevel <> StateError Then
                If Rcon Is Nothing OrElse Not Rcon.IsOpen Then
                    SetRemoteState(StateIdle, "未连接")
                End If
            End If
        Catch ex As Exception
            Logger.Warn(ex, "运行状态自检失败")
        End Try
    End Sub

    '''<summary>进页面时校准本机状态：本进程起的 / 上次留下的 / 都没有。</summary>
    Private Sub LocalStateRefresh()
        Try
            If ServerRunning() Then
                SetLocalState(StateRunning, "运行中", $"PID {ServerProc.Id}")
                Return
            End If
            Dim Pid As Integer = Settings.Get(Of Integer)("LinkServerProcPid")
            If Pid > 0 AndAlso FindServerProcess(Pid) IsNot Nothing Then Return 'ServerLeftoverRefresh 已经写过
            SetLocalState(StateIdle, "未运行")
        Catch ex As Exception
            Logger.Warn(ex, "刷新本机运行状态失败")
        End Try
    End Sub

    ' ── 遗留服务端进程 ──────────────────────────────────
    '''<summary>记下这次启动的服务端 PID，启动器重启后才能认出它。</summary>
    Private Sub SaveServerProcRecord(P As System.Diagnostics.Process, Dir As String)
        Try
            Settings.Set("LinkServerProcPid", P.Id)
            Settings.Set("LinkServerProcDir", Dir)
            Settings.Set("LinkServerProcTime", Now.ToString("HH:mm"))
        Catch ex As Exception
            Logger.Warn(ex, "记录服务端 PID 失败")
        End Try
    End Sub

    Private Sub ClearServerProcRecord()
        Try
            Settings.Set("LinkServerProcPid", 0)
            Settings.Set("LinkServerProcDir", "")
            Settings.Set("LinkServerProcTime", "")
        Catch
        End Try
    End Sub

    '''<summary>
    ''' 开服前确保 server.properties 里有可用的 RCON 配置。
    ''' 这是「启动器重启后还能接管」的关键：stdin 管道断了，RCON 这条 TCP 通道还在。
    ''' 密码随机生成一次就写进文件（文件才是权威源）；不动远程卡片的设置，也不回填它的输入框。
    ''' </summary>
    Private Sub EnsureRconConfigured(Dir As String)
        Try
            Dim Props As String = Dir.TrimEnd("\"c) & "\server.properties"
            If Not FileUtils.Exists(Props) Then Return
            Dim Lines As New List(Of String)(System.IO.File.ReadAllLines(Props))
            Dim Port As Integer = DefaultRconPort
            Dim Pass As String = ""
            Dim HasEnable As Boolean = False, HasPort As Boolean = False, HasPass As Boolean = False
            For Each L As String In Lines
                If L.StartsWith("enable-rcon=") Then HasEnable = True
                If L.StartsWith("rcon.port=") Then
                    Dim P As Integer
                    If Integer.TryParse(L.Substring("rcon.port=".Length).Trim(), P) AndAlso P > 0 AndAlso P <= 65535 Then Port = P
                    HasPort = True
                End If
                If L.StartsWith("rcon.password=") Then
                    Dim Old As String = L.Substring("rcon.password=".Length).Trim()
                    If Old <> "" Then Pass = Old
                    HasPass = True
                End If
            Next
            Dim Generated As Boolean = False
            If Pass = "" Then
                Pass = Guid.NewGuid().ToString("N").Substring(0, 16)
                Generated = True
            End If
            Dim Out As New List(Of String)
            For Each L As String In Lines
                If L.StartsWith("enable-rcon=") Then
                    Out.Add("enable-rcon=true")
                ElseIf L.StartsWith("rcon.port=") Then
                    Out.Add("rcon.port=" & Port)
                ElseIf L.StartsWith("rcon.password=") Then
                    Out.Add("rcon.password=" & Pass)
                Else
                    Out.Add(L)
                End If
            Next
            If Not HasEnable Then Out.Add("enable-rcon=true")
            If Not HasPort Then Out.Add("rcon.port=" & Port)
            If Not HasPass Then Out.Add("rcon.password=" & Pass)
            FileUtils.Write(Props, String.Join(vbCrLf, Out) & vbCrLf, encoding:=New System.Text.UTF8Encoding(False))
            If Generated Then
                ServerConsoleAppend("[本机] 已在这个服务端的 server.properties 里生成 RCON 密码（重启启动器后也能靠它接管）。")
            End If
            Logger.Info($"已确保本机 RCON 配置：127.0.0.1:{Port}")
        Catch ex As Exception
            Logger.Warn(ex, "配置 RCON 失败")
        End Try
    End Sub

    '''<summary>
    ''' 启动器重启后接管本机服务端：读服务端目录里的 server.properties（端口 / 密码），连 127.0.0.1。
    ''' 全程不碰远程卡片 —— 地址框、密码框、远程控制台一个都不写，接管成功的消息打到本机控制台。
    ''' </summary>
    Private Sub ConnectLocalRcon()
        If RconLocal IsNot Nothing AndAlso RconLocal.IsOpen Then Return
        Dim Dir As String = ServerDir()
        If Dir = "" Then Dir = If(Settings.Get(Of String)("LinkServerProcDir"), "").Trim()
        If Dir = "" Then Return
        Dim Port As Integer = DefaultRconPort
        Dim Pass As String = ""
        Dim Enabled As Boolean = False
        Try
            Dim Props As String = Dir.TrimEnd("\"c) & "\server.properties"
            If Not FileUtils.Exists(Props) Then Return
            For Each L As String In System.IO.File.ReadAllLines(Props)
                If L.StartsWith("enable-rcon=") Then
                    Enabled = L.Substring("enable-rcon=".Length).Trim().Equals("true", StringComparison.OrdinalIgnoreCase)
                ElseIf L.StartsWith("rcon.port=") Then
                    Integer.TryParse(L.Substring("rcon.port=".Length).Trim(), Port)
                ElseIf L.StartsWith("rcon.password=") Then
                    Pass = L.Substring("rcon.password=".Length).Trim()
                End If
            Next
        Catch ex As Exception
            Logger.Warn(ex, "读取本机 RCON 配置失败")
            Return
        End Try
        If Not Enabled OrElse Pass = "" Then
            ServerConsoleAppend("[本机] 这个服务端没开 RCON（enable-rcon 或 rcon.password 为空），接管不了命令通道；要关它只能用「关闭服务器」强制结束。")
            Return
        End If
        Dim UsePort As Integer = Port
        RunInNewThread(Sub()
            Dim Client As New RconClient()
            Dim Err As String = ""
            If Client.Connect("127.0.0.1", UsePort, Pass, Err) Then
                RunInUi(Sub()
                    RconLocal = Client
                    SetLocalState(StateRunning, "后台运行", $"已接管 127.0.0.1:{UsePort}")
                    ShowTakeover($"PID {Settings.Get(Of Integer)("LinkServerProcPid")} 已通过 RCON 接管（127.0.0.1:{UsePort}）。" & vbCrLf &
                                 "实时输出拿不到，命令照发；要关它点上面的「关闭后台服务端」。")
                    ServerConsoleAppend($"[本机] 已通过 RCON 接管（127.0.0.1:{UsePort}）。实时输出拿不到了，但命令和「关闭服务器」都能用。")
                End Sub)
            Else
                Client.Close()
                Logger.Info($"本机 RCON 接管失败：{Err}")
                RunInUi(Sub()
                            ServerConsoleAppend($"[本机] RCON 接管失败：{Err}。命令发不进去，要关它只能用「关闭服务器」强制结束。")
                            ShowTakeover($"PID {Settings.Get(Of Integer)("LinkServerProcPid")} 还在后台跑，但 RCON 没接上：{Err}" & vbCrLf &
                                         "命令发不进去，只能强制结束（会丢掉未保存的进度）。点「重新接管」再试一次。")
                        End Sub)
            End If
        End Sub, "Local Rcon Connect")
    End Sub

    '''<summary>走本机接管通道发一条命令，回显仍留在本地控制台。</summary>
    Private Sub RconRunLocal(Cmd As String, Optional OnDone As Action = Nothing)
        If RconLocal Is Nothing OrElse Not RconLocal.IsOpen Then Return
        Dim Client As RconClient = RconLocal
        ServerConsoleAppend("> " & Cmd & "  [RCON]")
        RunInNewThread(Sub()
            Dim Err As String = ""
            Dim Res As String = Client.Command(Cmd, Err)
            RunInUi(Sub()
                If Err <> "" Then
                    ServerConsoleAppend("! " & Err)
                ElseIf If(Res, "").Trim() <> "" Then
                    ServerConsoleAppend(Res.TrimEnd())
                End If
                If OnDone IsNot Nothing Then OnDone()
            End Sub)
        End Sub, "Local Rcon Run")
    End Sub

    '''<summary>
    ''' 按 PID 取进程；不存在、已退出、或不是 java / cmd 就返回 Nothing。
    ''' 必须校验进程名 —— PID 会被系统复用，只按数字判断会误杀别的程序。
    ''' </summary>
    Private Function FindServerProcess(Pid As Integer) As System.Diagnostics.Process
        If Pid <= 0 Then Return Nothing
        Try
            Dim P As System.Diagnostics.Process = System.Diagnostics.Process.GetProcessById(Pid)
            If P.HasExited Then Return Nothing
            Dim Name As String = P.ProcessName
            'Java 直启记录的是 java；run.bat 分支记录的是 cmd（java 是它的子进程）
            If Name.StartsWith("java", StringComparison.OrdinalIgnoreCase) OrElse
               Name.StartsWith("cmd", StringComparison.OrdinalIgnoreCase) Then Return P
            Return Nothing
        Catch
            Return Nothing
        End Try
    End Function

    '''<summary>启动器重启后接管不了 stdin，只能强杀。用 taskkill /T 连子进程一起结束。</summary>
    Private Function ForceKillServer(Pid As Integer) As Boolean
        Try
            Dim Psi As New System.Diagnostics.ProcessStartInfo("taskkill.exe", $"/F /T /PID {Pid}") With {
                .UseShellExecute = False, .CreateNoWindow = True, .RedirectStandardOutput = True, .RedirectStandardError = True}
            Dim P As System.Diagnostics.Process = System.Diagnostics.Process.Start(Psi)
            P.WaitForExit(8000)
            Return P.ExitCode = 0
        Catch ex As Exception
            Logger.Warn(ex, "强制结束服务端失败")
            Return False
        End Try
    End Function

    '''<summary>
    ''' 让服务端 stop 并等它退出；超时再强杀。**同步**执行 —— 只在退出流程里用，
    ''' 异步的话启动器先退了、服务端还在，等于没关。
    ''' </summary>
    Private Sub StopServerGracefully(P As System.Diagnostics.Process)
        Try
            If P Is Nothing OrElse P.HasExited Then
                ClearServerProcRecord()
                Return
            End If
            If ServerProc Is P Then
                WriteServerLine("stop")
                If Not P.WaitForExit(10000) Then ForceKillServer(P.Id)
            Else
                '不是本进程起的（启动器重启过），stdin 断了，只能强杀
                ForceKillServer(P.Id)
            End If
            ClearServerProcRecord()
        Catch ex As Exception
            Logger.Warn(ex, "停止服务端失败")
        End Try
    End Sub

    '''<summary>
    ''' 统一处理加/取消管理员。服务端在跑必须走控制台：直接改 ops.json 会被内存里的名单盖回去。
    ''' </summary>
    Private Sub ApplyOpChange(Names As List(Of String), MakeOp As Boolean)
        If Names Is Nothing OrElse Names.Count = 0 Then Return
        '远程服务器：本地没有 ops.json 可写，让服务端自己改
        If UseRconForPlayers() Then
            For Each N In Names
                RconExec(If(MakeOp, "op ", "deop ") & N)
            Next
            Hint($"已通过 RCON {(If(MakeOp, "设为管理员", "取消管理员"))}：{Names.Join("、")}，立即生效。", HintType.Green, False)
            Return
        End If
        If ServerRunning() Then
            Try
                For Each N In Names
                    WriteServerLine(If(MakeOp, "op ", "deop ") & N)
                Next
                Hint($"已通过控制台{(If(MakeOp, "设为管理员", "取消管理员"))}：{Names.Join("、")}，立即生效。", HintType.Green, False)
                '服务端回写 ops.json 有延迟，等一下再刷新名单
                RunInNewThread(Sub()
                    System.Threading.Thread.Sleep(1500)
                    RunInUi(Sub() WhitelistRefresh())
                End Sub, "Refresh Ops")
            Catch ex As Exception
                Hint("发送命令失败：" & ex.Message, HintType.Red, False)
            End Try
            Return
        End If
        Dim Ops As List(Of String) = OpsNames()
        If MakeOp Then
            For Each N In Names
                If Not Ops.Any(Function(O) O.Equals(N, StringComparison.OrdinalIgnoreCase)) Then Ops.Add(N)
            Next
        Else
            For Each N In Names
                Ops.RemoveAll(Function(O) O.Equals(N, StringComparison.OrdinalIgnoreCase))
            Next
        End If
        OpsWrite(Ops)
        Hint($"已写入 ops.json（当前 {Ops.Count} 个管理员），下次开服生效。", HintType.Green, False)
    End Sub

    Private Sub WhitelistWrite(Names As List(Of String))
        Dim Dir As String = ServerDirOrHint()
        If Dir = "" Then Return
        Dim Arr As New Newtonsoft.Json.Linq.JArray
        For Each N In Names
            Arr.Add(New Newtonsoft.Json.Linq.JObject From {{"uuid", OfflineUuid(N)}, {"name", N}})
        Next
        FileUtils.Write(Dir & "\whitelist.json", Arr.ToString(Newtonsoft.Json.Formatting.Indented) & vbCrLf, encoding:=New System.Text.UTF8Encoding(False))
        Dim Props As String = Dir & "\server.properties"
        If FileUtils.Exists(Props) Then
            Dim Lines As New List(Of String)
            For Each L In System.IO.File.ReadAllLines(Props)
                Lines.Add(If(L.StartsWith("white-list="), "white-list=true", L))
            Next
            FileUtils.Write(Props, String.Join(vbCrLf, Lines) & vbCrLf, encoding:=New System.Text.UTF8Encoding(False))
        End If
        '上面顺手把 white-list 打开过，界面得跟着变，否则勾选框和文件对不上
        ServerConfigRefresh()
        WhitelistRefresh()
    End Sub

    '''<summary>把输入框按空格 / 英文逗号 / 换行拆成名字列表。</summary>
    Private Function ParseWhitelistInput() As List(Of String)
        Dim Names As New List(Of String)
        For Each Part In TextWhitelistAdd.Text.Split({","c, " "c, ChrW(9), vbCr, vbLf}, StringSplitOptions.RemoveEmptyEntries)
            Dim N As String = Part.Trim()
            If N <> "" AndAlso Not Names.Contains(N) Then Names.Add(N)
        Next
        Return Names
    End Function

    '''<summary>
    ''' 玩家管理要不要走 RCON：连上了、而且目标不是「本机 + 有目录」的情况就走远程命令。
    ''' 本机有目录时优先走文件 / stdin —— 更准，也能顺带拿到实时输出。
    ''' </summary>
    Private Function UseRconForPlayers() As Boolean
        If Rcon Is Nothing OrElse Not Rcon.IsOpen Then Return False
        If IsLocalHost(If(TextRconHost.Text, "").Trim()) AndAlso ServerDir() <> "" Then Return False
        Return True
    End Function

    '''<summary>后台发一条 RCON 命令，回显写进**远程**控制台（白名单 / OP 这类远端操作都走它）。</summary>
    Private Sub RconExec(Cmd As String, Optional OnDone As Action = Nothing)
        RconRun(Cmd, OnDone)
    End Sub

    Private Sub BtnWhitelistAdd_Click(sender As Object, e As MouseButtonEventArgs) Handles BtnWhitelistAdd.Click
        Dim Add As List(Of String) = ParseWhitelistInput()
        If Add.Count = 0 Then
            Hint("先在输入框里写玩家名，空格或英文逗号分隔。", HintType.Blue, False)
            Return
        End If
        '远程服务器：本地没有目录可写，直接让服务端自己加
        If UseRconForPlayers() Then
            For Each N In Add
                RconExec("whitelist add " & N)
            Next
            TextWhitelistAdd.Text = ""
            Hint($"已通过 RCON 添加 {Add.Count} 个玩家到白名单，立即生效。" & vbCrLf &
                 "如果这台服务器还没启用白名单，需要管理员执行一次 whitelist on。", HintType.Green, False)
            Return
        End If
        If ServerDir() = "" Then
            Hint("没有可用的服务端：先在「已装服务端」里选一个，或者在下面「远程服务器（RCON）」连上远程服。", HintType.Red, False)
            Return
        End If
        Dim Names As List(Of String) = WhitelistNames()
        For Each N In Add
            If Not Names.Contains(N) Then Names.Add(N)
        Next
        '服务端在跑时必须走控制台命令：直接改 whitelist.json 不会更新它的内存，
        '玩家照样进不来；而且服务端下次自己写文件时会把我们的改动冲掉
        If ServerRunning() Then
            For Each N In Add
                WriteServerLine("whitelist add " & N)
            Next
            TextWhitelistAdd.Text = ""
            Hint($"已通过控制台把 {Add.Count} 个玩家加入白名单，立即生效（当前共 {Names.Count} 人）。" &
                 If(CheckServerWhitelist.Checked, "", vbCrLf &
                    "注意：「启用白名单」没勾，现在加了也不会拦人。要启用请勾上后重启服务端，或直接在控制台发 whitelist on。"),
                 HintType.Green, False)
            '服务端回写 whitelist.json 有延迟，等一下再刷新名单，否则读到的还是旧的
            RunInNewThread(Sub()
                System.Threading.Thread.Sleep(1200)
                RunInUi(Sub() WhitelistRefresh())
            End Sub, "Refresh Whitelist")
            Return
        End If
        WhitelistWrite(Names)
        TextWhitelistAdd.Text = ""
        Hint($"已添加 {Add.Count} 个玩家，当前共 {Names.Count} 人；重启服务端后生效。", HintType.Green, False)
    End Sub

    Private Sub BtnWhitelistRemove_Click(sender As Object, e As MouseButtonEventArgs) Handles BtnWhitelistRemove.Click
        If UseRconForPlayers() Then
            '远程：名字从输入框读，白名单和管理员一起撤
            Dim RemoteDel As List(Of String) = ParseWhitelistInput()
            If RemoteDel.Count = 0 Then
                Hint("在输入框里写上要删除的玩家名（空格或英文逗号分隔），远程服务器读不到完整名单。", HintType.Blue, False)
                Return
            End If
            If MyMsgBox($"会从白名单移除并撤销管理员：{RemoteDel.Join("、")}" & vbCrLf & vbCrLf & "立即生效，继续？",
                        "删除玩家", "删除", "取消", IsWarn:=True) <> 1 Then Return
            For Each N In RemoteDel
                RconExec("whitelist remove " & N)
                RconExec("deop " & N)
            Next
            TextWhitelistAdd.Text = ""
            Hint($"已通过 RCON 移除 {RemoteDel.Count} 个玩家（同时撤销了管理员）。", HintType.Green, False)
            Return
        End If
        If ServerDir() = "" Then
            Hint("没有可用的服务端：先在「已装服务端」里选一个，或者在下面「远程服务器（RCON）」连上远程服。", HintType.Red, False)
            Return
        End If
        Dim All As List(Of String) = WhitelistNames()
        If All.Count = 0 Then
            Hint("白名单还是空的。", HintType.Blue, False)
            Return
        End If
        Dim Del As List(Of String) = WindowWhitelistRemove.Pick(All)
        If Del Is Nothing OrElse Del.Count = 0 Then Return
        Dim Names As List(Of String) = WhitelistNames()
        Dim Removed As Integer = 0
        For Each N In Del
            If Names.Remove(N) Then Removed += 1
        Next
        '服务端在跑就走控制台，理由同「添加」：改文件不更新内存，还会被覆盖
        If ServerRunning() Then
            For Each N In Del
                WriteServerLine("whitelist remove " & N)
            Next
        Else
            WhitelistWrite(Names)
        End If
        '删玩家要连管理员身份一起撤：白名单没了但 ops.json 还留着他，下次开服他还是管理员
        Dim OpsNow As List(Of String) = OpsNames()
        Dim HadOp As Boolean = False
        For Each N In Del
            If OpsNow.RemoveAll(Function(O) O.Equals(N, StringComparison.OrdinalIgnoreCase)) > 0 Then HadOp = True
        Next
        If HadOp Then
            ApplyOpChange(Del, False)
            Hint($"已删除 {Removed} 个玩家（同时撤销了管理员），当前共 {Names.Count} 人。", HintType.Green, False)
        Else
            Hint($"已删除 {Removed} 个玩家，当前共 {Names.Count} 人。", HintType.Green, False)
        End If
    End Sub

'''<summary>离线模式 UUID：MD5("OfflinePlayer:" + 名字)，版本 3。</summary>
    Private Function OfflineUuid(Name As String) As String
        Using Md5 As System.Security.Cryptography.MD5 = System.Security.Cryptography.MD5.Create()
            Dim H As Byte() = Md5.ComputeHash(System.Text.Encoding.UTF8.GetBytes("OfflinePlayer:" & Name))
            H(6) = CByte((H(6) And &H0F) Or &H30)
            H(8) = CByte((H(8) And &H3F) Or &H80)
            Dim S As String = BitConverter.ToString(H).Replace("-", "").ToLowerInvariant()
            Return S.Substring(0, 8) & "-" & S.Substring(8, 4) & "-" & S.Substring(12, 4) & "-" & S.Substring(16, 4) & "-" & S.Substring(20, 12)
        End Using
    End Function

    Private Sub BtnModsOpen_Click(sender As Object, e As MouseButtonEventArgs) Handles BtnModsOpen.Click
        Dim Dir As String = ServerDirOrHint()
        If Dir = "" Then Return
        Try
            Dim Mods As String = Dir & "\mods\"
            DirectoryUtils.Create(Mods)
            Process.Start("explorer.exe", $"""{Mods}""")
        Catch ex As Exception
            Logger.Error(ex, "打开 mods 目录失败")
            Hint("打开失败：" & ex.Message, HintType.Red, False)
        End Try
    End Sub


    Private Sub BtnModsAdd_Click(sender As Object, e As MouseButtonEventArgs) Handles BtnModsAdd.Click
        Dim Dir As String = ServerDirOrHint()
        If Dir = "" Then Return
        Try
            Dim Files As List(Of String) = Dialogs.SelectFile("选择要放进 mods 的 jar", True, filter:={({"jar"}, "模组 jar")})
            If Files Is Nothing OrElse Files.Count = 0 Then Return
            Dim Mods As String = Dir & "\mods\"
            DirectoryUtils.Create(Mods)
            For Each F In Files
                System.IO.File.Copy(F, Mods & System.IO.Path.GetFileName(F), True)
            Next
            Hint($"已复制 {Files.Count} 个 mod 到 mods 目录。", HintType.Green, False)
        Catch ex As Exception
            Logger.Error(ex, "添加 mod 失败")
            Hint("添加失败：" & ex.Message, HintType.Red, False)
        End Try
    End Sub

    ' ── 已装服务端（联机页）───────────────────────────
    '''<summary>读「已装服务端」记录（Settings 里的 JSON 数组）；坏数据按空列表处理，别让整个页面炸掉。</summary>
    Private Function ReadServerRecords() As Newtonsoft.Json.Linq.JArray
        Dim Raw As String = If(Settings.Get(Of String)("LinkServerList"), "")
        If String.IsNullOrWhiteSpace(Raw) Then Return New Newtonsoft.Json.Linq.JArray()
        Try
            Return Newtonsoft.Json.Linq.JArray.Parse(Raw)
        Catch ex As Exception
            Logger.Warn(ex, "已装服务端记录解析失败，按空列表处理")
            Return New Newtonsoft.Json.Linq.JArray()
        End Try
    End Function

    Private Sub WriteServerRecords(Arr As Newtonsoft.Json.Linq.JArray)
        Settings.Set("LinkServerList", If(Arr, New Newtonsoft.Json.Linq.JArray()).ToString(Newtonsoft.Json.Formatting.None))
    End Sub

    '''<summary>从记录里剔掉指定目录（大小写不敏感，Windows 路径不分大小写）。</summary>
    Private Sub RemoveServerRecord(Dir As String)
        Try
            Dim Kept As New Newtonsoft.Json.Linq.JArray()
            For Each It In ReadServerRecords()
                Dim D As String = If(It("dir")?.ToString, "").Trim()
                If D <> "" AndAlso Not String.Equals(D, Dir, StringComparison.OrdinalIgnoreCase) Then Kept.Add(It)
            Next
            WriteServerRecords(Kept)
        Catch ex As Exception
            Logger.Warn(ex, "移除已装服务端记录失败：" & Dir)
        End Try
    End Sub

    '''<summary>
    ''' 刷新「已装服务端」下拉。
    ''' 顺带自愈：目录已经不在的记录直接剔掉 —— 之前删服务端时目录被占用、删除失败，
    ''' 记录留在原地，下拉里就多一个「选中必定报错」的死项，清也清不掉。只有目录确实不存在才剔除，
    ''' 外接盘没插、权限暂时读不到这类情况先留着，免得把记录误删。
    ''' </summary>
    Private Sub InstalledServersRefresh(Optional ReportCleanup As Boolean = False)
        Try
            If ComboInstalledServer Is Nothing Then Return
            '优先保持用户手上的选择；没有就回到「最近一次安装 / 添加」的那个，
            '否则刚装完 Forge、列表却默认停在第 0 项，点「开服」会开到别的服上去
            Dim KeepSelected As String = SelectedInstalledDir()
            If KeepSelected = "" Then KeepSelected = If(Settings.Get(Of String)("LinkServerDir"), "").Trim()
            ComboInstalledServer.Items.Clear()
            Dim Kept As New Newtonsoft.Json.Linq.JArray()
            Dim Dropped As Integer = 0
            Dim RestoreIndex As Integer = -1
            For Each It In ReadServerRecords()
                Dim D As String = If(It("dir")?.ToString, "").Trim()
                Dim V As String = If(It("ver")?.ToString, "").Trim()
                If D = "" Then Continue For
                If Not DirectoryUtils.Exists(D) Then
                    Dropped += 1
                    Continue For
                End If
                Kept.Add(It)
                ComboInstalledServer.Items.Add(New MyComboBoxItem With {.Content = If(V = "", D, V & "  " & D), .Tag = D})
                If String.Equals(KeepSelected, D, StringComparison.OrdinalIgnoreCase) Then RestoreIndex = ComboInstalledServer.Items.Count - 1
            Next
            If Dropped > 0 Then WriteServerRecords(Kept)
            If ComboInstalledServer.Items.Count = 0 Then
                If ReportCleanup Then Hint("已装服务端列表是空的（磁盘上找不到任何记录里的目录）。", HintType.Blue, False)
                Return
            End If
            ComboInstalledServer.SelectedIndex = If(RestoreIndex >= 0, RestoreIndex, 0)
            If ReportCleanup Then
                Dim Extra As String = If(Dropped > 0, $"；清掉了 {Dropped} 条目录已不存在的记录", "")
                Hint($"已刷新：{ComboInstalledServer.Items.Count} 个可用{Extra}。", HintType.Blue, False)
            End If
        Catch ex As Exception
            Logger.Warn(ex, "刷新已装服务端失败")
        End Try
    End Sub

    Private Sub BtnRefreshInstalled_Click(sender As Object, e As MouseButtonEventArgs) Handles BtnRefreshInstalled.Click
        InstalledServersRefresh(True)
    End Sub

    Private Function SelectedInstalledDir() As String
        Dim Item As MyComboBoxItem = TryCast(ComboInstalledServer.SelectedItem, MyComboBoxItem)
        If Item Is Nothing OrElse Item.Tag Is Nothing Then Return ""
        Return Item.Tag.ToString()
    End Function

    '''<summary>
    ''' 从「已装服务端」记录里推断目录的 MC 版本与加载器。记录的 ver 形如
    ''' `1.20.1-forge-47.0.1` / `1.20.1-paper-133` / `26.3`。
    ''' 认不出（比如「添加已有」用目录名当标签）就返回 Nothing，别硬猜。
    ''' </summary>
    Private Function ServerRecordInfo(Dir As String) As Tuple(Of String, String)
        If String.IsNullOrWhiteSpace(Dir) Then Return Nothing
        '① 目录里有 server.json 就以它为准（安装时写进去的，最可靠，换机/换启动器也认得出）
        Dim Meta As Tuple(Of String, String, String) = PageServerInstall.ReadServerMeta(Dir)
        If Meta IsNot Nothing Then Return New Tuple(Of String, String)(Meta.Item1, If(Meta.Item2, "").ToLowerInvariant())
        '② 退回「已装服务端」记录里的 ver 字符串
        Try
            For Each It In ReadServerRecords()
                Dim D As String = If(It("dir")?.ToString, "").Trim()
                If Not String.Equals(D.TrimEnd("\"c), Dir.TrimEnd("\"c), StringComparison.OrdinalIgnoreCase) Then Continue For
                Dim V As String = If(It("ver")?.ToString, "").Trim()
                If V = "" Then Return Nothing
                Dim Parts As String() = V.Split("-"c)
                Dim Mc As String = Parts(0)
                '必须是 1.20.1 / 26.3 这种形状；否则是拿目录名当标签，认不出来
                If Not Mc.RegexCheck("^\d+(\.\d+)+$") Then Return Nothing
                Dim Ld As String = If(Parts.Length >= 2, Parts(1).ToLowerInvariant(), "")
                If Array.IndexOf({"forge", "neoforge", "fabric", "paper", "purpur", "folia"}, Ld) < 0 Then Ld = ""
                Return New Tuple(Of String, String)(Mc, Ld)
            Next
        Catch ex As Exception
            Logger.Warn(ex, "读取服务端版本记录失败")
        End Try
        Return Nothing
    End Function

    '''<summary>把别处已经装好的服务端目录加进列表（自己搭的、拷来的、别的启动器装的）。</summary>
    Private Sub BtnAddInstalled_Click(sender As Object, e As MouseButtonEventArgs) Handles BtnAddInstalled.Click
        Try
            Dim D As String = Dialogs.SelectFolder("选择已有的服务端目录（里面要有 server.jar 或 run.bat）", False).FirstOrDefault()
            If String.IsNullOrWhiteSpace(D) Then Return
            D = D.TrimEnd("\"c)
            Dim Marks As New List(Of String)
            For Each N In {"server.jar", "fabric-server-launch.jar", "run.bat"}
                If FileUtils.Exists(D & "\" & N) Then Marks.Add(N)
            Next
            If Marks.Count = 0 Then
                Hint("这个目录里没找到 server.jar / fabric-server-launch.jar / run.bat，不像服务端目录。" & vbCrLf &
                     "确认一下选的是不是服务端根目录（不是里面的 world 或 plugins）。", HintType.Red, False)
                Return
            End If
            '目录里没有可靠的版本信息，就用目录名当标签
            Dim Label As String = IO.Path.GetFileName(D)
            If String.IsNullOrWhiteSpace(Label) Then Label = D
            PageServerInstall.AddServerRecord(D, Label)
            InstalledServersRefresh()
            Hint($"已加入列表：{Label}（{Marks.Join("、")}）。" & vbCrLf &
                 "它已设为当前服务端，直接点上面的「开服」就能启动。", HintType.Green, False)
        Catch ex As Exception
            Hint("添加失败：" & ex.Message, HintType.Red, False)
        End Try
    End Sub

    '''<summary>用目录里的 run.bat 启动（Forge / NeoForge 装出来的就是它）。</summary>
    Private Sub StartServerWithRunBat(D As String, Bat As String)
        '同开服那条路：没 Java 就自动下一个，别让用户为了开服先去装 JDK
        Dim Java As String = PageServerInstall.GetJavaPath(ServerRecordInfo(D)?.Item1)
        If Java = "" Then
            Hint("没有可用的 Java，自动下载也没成功。可以到「设置 → 启动 → Java」里手动添加一个。", HintType.Red, False)
            Return
        End If
        ServerVersion = ""
        'run.bat 的 JVM 参数全在 user_jvm_args.txt 里，先把内存和 UTF-8 编码补上，
        '否则这条路径下控制台发中文命令一样会乱码
        PageServerInstall.EnsureServerJvmArgs(D)
        ServerProc = System.Diagnostics.Process.Start(New System.Diagnostics.ProcessStartInfo("cmd.exe", $"/c ""{Bat}"" nogui") With {
            .WorkingDirectory = D, .UseShellExecute = False, .CreateNoWindow = True,
            .RedirectStandardInput = True, .RedirectStandardOutput = True, .RedirectStandardError = True,
            .StandardOutputEncoding = New System.Text.UTF8Encoding(False),
            .StandardErrorEncoding = New System.Text.UTF8Encoding(False)
        })
        SaveServerProcRecord(ServerProc, D)
        AddHandler ServerProc.OutputDataReceived, AddressOf ServerOutput
        AddHandler ServerProc.ErrorDataReceived, AddressOf ServerOutput
        WatchServerExit(ServerProc)
        ServerProc.BeginOutputReadLine()
        ServerProc.BeginErrorReadLine()
        SetLocalState(StateBusy, "启动中", $"PID {ServerProc.Id}（run.bat）")
        Hint("服务端已启动（run.bat）：首次启动要生成世界，可能需要 1–2 分钟。", HintType.Blue, False)
    End Sub

    '''<summary>清空「加入服务器」那两行，并取消「启动时默认进入」——填过一次想回到没填的状态就靠它。</summary>
    Private Sub BtnServerAddrClear_Click(sender As Object, e As MouseButtonEventArgs) Handles BtnServerAddrClear.Click
        TextServerAddr.Text = ""
        TextServerName.Text = ""
        CheckServerDefault.SetChecked(False, False)
        Settings.Set("LinkServerAddress", "")
        Settings.Set("LinkServerName", "")
        Settings.Set("LinkServerDefault", False)
        LabServerStatus.Text = "未填写服务器地址"
        Hint("已清空服务器地址。", HintType.Blue, False)
    End Sub

    ' ── 服务端日志 ────────────────────────────────────
    Private Sub BtnOpenServerLog_Click(sender As Object, e As MouseButtonEventArgs) Handles BtnOpenServerLog.Click
        Dim D As String = SelectedInstalledDir()
        If D = "" Then
            '没在列表里选，就退回到「开本地服务端」用过的那个 jar 所在的目录
            Dim Jar As String = If(Settings.Get(Of String)("LinkServerJar"), "").Trim()
            If Jar <> "" Then D = IO.Path.GetDirectoryName(Jar)
        End If
        If D = "" OrElse Not DirectoryUtils.Exists(D) Then
            Hint("先在「已装服务端」里选一个。", HintType.Blue, False)
            Return
        End If
        Dim LogPath As String = FindServerLog(D)
        If LogPath = "" Then
            Hint("这个目录里还没有日志 —— 服务端至少启动过一次才会生成 logs\latest.log。", HintType.Blue, False)
            Return
        End If
        OpenExplorer(LogPath)
    End Sub

    '''<summary>找服务端日志：优先 logs\latest.log，否则取 logs 下最新的 .log*；返回空串表示没有。</summary>
    Private Function FindServerLog(Dir As String) As String
        Try
            Dim Cleaned As String = Dir.TrimEnd("\"c)
            Dim Latest As String = Cleaned & "\logs\latest.log"
            If FileUtils.Exists(Latest) Then Return Latest
            Dim LogDir As String = Cleaned & "\logs"
            If Not DirectoryUtils.Exists(LogDir) Then Return ""
            'Fabric / Forge 的日志名不一定是 latest.log，兜底取最近写过的那个
            Dim Best As String = ""
            Dim BestTime As Date = Date.MinValue
            For Each F In DirectoryUtils.EnumerateFiles(LogDir, False, "*.log*")
                Try
                    Dim T As Date = IO.File.GetLastWriteTime(F)
                    If T > BestTime Then
                        BestTime = T
                        Best = F
                    End If
                Catch
                End Try
            Next
            Return Best
        Catch ex As Exception
            Logger.Warn(ex, "查找服务端日志失败：" & Dir)
            Return ""
        End Try
    End Function

    Private Sub BtnOpenServerDir_Click(sender As Object, e As MouseButtonEventArgs) Handles BtnOpenServerDir.Click
        Dim D As String = SelectedInstalledDir()
        If D = "" Then
            Hint("先在「已装服务端」里选一个。", HintType.Blue, False)
            Return
        End If
        Try
            Process.Start("explorer.exe", $"""{D}""")
        Catch ex As Exception
            Hint("打开失败：" & ex.Message, HintType.Red, False)
        End Try
    End Sub

    '''<summary>当前正在跑的服务端目录（本进程起的，或者重启前留下、现在仍活着的那个）。</summary>
    Private Function RunningServerDir() As String
        Try
            Dim Running As Boolean = False
            If ServerProc IsNot Nothing AndAlso Not ServerProc.HasExited Then
                Running = True
            Else
                Dim OldPid As Integer = Settings.Get(Of Integer)("LinkServerProcPid")
                If OldPid > 0 AndAlso FindServerProcess(OldPid) IsNot Nothing Then Running = True
            End If
            If Not Running Then Return ""
            '进程记录里的目录优先；老记录没有它时退回当前选中的 server.jar 所在目录
            Dim Dir As String = If(Settings.Get(Of String)("LinkServerProcDir"), "").Trim()
            If Dir = "" Then Dir = ServerDir()
            Return Dir
        Catch
            Return ""
        End Try
    End Function

    Private Sub BtnDeleteInstalled_Click(sender As Object, e As MouseButtonEventArgs) Handles BtnDeleteInstalled.Click
        Dim D As String = SelectedInstalledDir()
        If D = "" Then
            Hint("先在「已装服务端」里选一个。", HintType.Blue, False)
            Return
        End If
        '正在跑的服务端会锁住 jar / 世界文件，删到一半必然失败，还会留下半截目录
        Dim RunDir As String = RunningServerDir()
        If RunDir <> "" AndAlso String.Equals(RunDir.TrimEnd("\"c), D.TrimEnd("\"c), StringComparison.OrdinalIgnoreCase) Then
            Hint("这个服务端还在运行，先把「关闭服务器」点了再删。" & vbCrLf &
                 "（直接删会因为文件被 java 占用而失败，还可能删坏世界存档。）", HintType.Red, False)
            Return
        End If
        If MyMsgBox($"确定删除服务端目录吗？{vbCrLf}{D}{vbCrLf}{vbCrLf}会删除该目录下所有文件（含 world），无法恢复。", "删除服务端", "删除", "取消", IsWarn:=True) <> 1 Then Return
        Try
            '走回收站：这个目录里有 world，误删就是存档没了。回收站还能捞回来
            If DirectoryUtils.Exists(D) Then DirectoryUtils.Delete(D, True)
            RemoveServerRecord(D)
            '顺手把「当前服务端」的引用清掉，否则下次开服/读配置还会指到这个已删目录
            For Each Key In {"LinkServerDir", "LinkServerProcDir", "LinkServerJar"}
                If String.Equals(If(Settings.Get(Of String)(Key), "").Trim().TrimEnd("\"c), D.TrimEnd("\"c), StringComparison.OrdinalIgnoreCase) OrElse
                   If(Settings.Get(Of String)(Key), "").Trim().StartsWith(D.TrimEnd("\"c) & "\", StringComparison.OrdinalIgnoreCase) Then
                    Settings.Set(Key, "")
                End If
            Next
            InstalledServersRefresh()
            Hint("已删除服务端目录（已放入回收站，需要时可以从回收站还原）。", HintType.Green, False)
        Catch ex As Exception
            Logger.Error(ex, "删除服务端失败：" & D)
            '目录删不掉（文件被占用、权限不足）时，列表里那一项也必须能清掉，
            '否则每次打开联机页都留着一条点开就报错的死记录
            If MyMsgBox($"目录没删干净：{vbCrLf}{ex.Message}{vbCrLf}{vbCrLf}要只把这一条从「已装服务端」列表里移除吗？（磁盘上的目录保持原样，可以手动删）",
                        "删除服务端", "从列表移除", "保留", IsWarn:=True) = 1 Then
                RemoveServerRecord(D)
                InstalledServersRefresh()
                Hint($"已从列表移除。目录还在：{D}", HintType.Blue, False)
            End If
        End Try
    End Sub

End Class
