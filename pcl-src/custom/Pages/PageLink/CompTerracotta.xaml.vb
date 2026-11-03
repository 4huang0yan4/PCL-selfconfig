' 陶瓦联机（Terracotta）的界面上半部分：状态、房间码、动作按钮。
' 抽成独立控件是因为它在两个地方出现——顶部「联机」标签（PageLinkMain）与「设置 → 联机」（PageSetupLink），
' 业务逻辑全在 ModCustom.Tc* 里，这里只做界面绑定与后台调用。
Public Class CompTerracotta

    Private TcTimer As System.Windows.Threading.DispatcherTimer = Nothing
    Private TcTickBusy As Boolean = False
    Private TcInstalling As Boolean = False
    ''' <summary>上一次看到的阶段，用于识别"对端消失"与记录状态迁移。</summary>
    Private TcLastStage As String = ""
    ''' <summary>最近一次拿到的直连地址，供「复制直连地址」使用。</summary>
    Private TcLastUrl As String = ""
    ''' <summary>最近一次拿到的房间码，供房主「复制房间码」使用。</summary>
    Private TcLastRoom As String = ""
    ''' <summary>对端消失时的专用文案。必须跨轮询保持——只在切换那一秒显示会被下一次刷新覆盖回通用文案。</summary>
    Private TcOverrideText As String = ""
    ''' <summary>当前是不是房主态（host-scanning / host-starting / host-ok）。房主没有直连地址。</summary>
    Private TcIsHost As Boolean = False

    Private Sub CompTerracotta_Loaded(sender As Object, e As RoutedEventArgs) Handles Me.Loaded
        ChkTcAutoAddr.SetChecked(ModCustom.TcAutoAddrMode, False)
        ChkTcCopyAddr.SetChecked(ModCustom.TcCopyAddrMode, False)
        TcApplyCopyMode()
        TcStartTimer()
        TcRefreshUiAsync()
    End Sub

    Private Sub CompTerracotta_Unloaded(sender As Object, e As RoutedEventArgs) Handles Me.Unloaded
        If TcTimer IsNot Nothing Then
            TcTimer.Stop()
            RemoveHandler TcTimer.Tick, AddressOf TcTick
            TcTimer = Nothing
        End If
    End Sub

    ''' <summary>1 秒一次刷新。取状态放在后台线程，避免本地 HTTP 卡界面。</summary>
    Private Sub TcStartTimer()
        If TcTimer IsNot Nothing Then Return
        TcTimer = New System.Windows.Threading.DispatcherTimer With {.Interval = TimeSpan.FromSeconds(1)}
        AddHandler TcTimer.Tick, AddressOf TcTick
        TcTimer.Start()
    End Sub

    Private Sub TcTick(sender As Object, e As EventArgs)
        TcRefreshUiAsync()
    End Sub

    Private Sub TcRefreshUiAsync()
        If TcTickBusy Then Return
        TcTickBusy = True
        Threading.Tasks.Task.Run(
            Sub()
                Try
                    Dim Snapshot As ModCustom.TcSnapshot = ModCustom.TcReadState(False)
                    Dim Text As String
                    If ModCustom.TcInstalled Then
                        Text = ModCustom.TcStatusTextFor(Snapshot)
                    Else
                        Text = "状态：陶瓦未安装。点「安装 / 更新陶瓦」自动下载（约 8 MB）。"
                    End If
                    Dim Code As String = If(Snapshot Is Nothing, "", Snapshot.Room)
                    Dim CurStage As String = If(Snapshot Is Nothing, "", Snapshot.Stage)
                    Dim CurUrl As String = If(Snapshot Is Nothing, "", Snapshot.Url)
                    RunInUi(
                        Sub()
                            If LabTcStatus Is Nothing Then Return
                            '阶段迁移处理两件事：
                            '一、补状态日志——建房出码这段此前完全没有记录，测完只能靠 /state 反推。
                            '二、识别"对端消失"——房主关游戏后游客只看到 MC 原生「连接中断」，
                            '    界面上必须说清是房主走了，否则用户会反复去连一个已经死掉的房间。
                            If CurStage <> TcLastStage Then
                                Logger.Info($"[陶瓦联机] 状态 {(If(TcLastStage = "", "（无）", TcLastStage))} → {(If(CurStage = "", "（无）", CurStage))}")
                                TcOverrideText = ""
                                If CurStage = "exception" Then
                                    If TcLastStage = "guest-ok" Then
                                        '游客侧不清码：exception 可能只是本机网络抖动，房主的房间还在，同一个码仍能重连。
                                        '清掉等于逼用户重新找房主要码。
                                        TcOverrideText = "房主已断开（对方可能关掉了游戏或退出了世界）。可以再点一次「加入」重试；若仍失败，请向房主索要新的房间码。"
                                    ElseIf TcLastStage = "host-ok" Then
                                        '房主侧必须清：游戏进程没了，这个码定义上就是死的，留着只会被复制走。
                                        TcOverrideText = "房间已结束（游戏可能已关闭或退出了世界）。重新建房会生成新的房间码。"
                                        TcClearRoomField()
                                    End If
                                End If
                                TcLastStage = CurStage
                            End If
                            '停在 exception 期间持续显示专用文案，直到下一次状态迁移才清掉。
                            If TcOverrideText <> "" AndAlso CurStage = "exception" Then Text = TcOverrideText
                            '陶瓦要 ~27 秒才会从 host-ok 掉到 exception（2026-10-28 实测：17:04:30 关游戏、17:04:57 才变）。
                            '这半分钟界面一直说"房间已就绪"，房主以为一切正常。用游戏进程句柄提前给一句软提示，
                            '不改变陶瓦状态，只是别让界面在事实已变时继续报"正常"。
                            If CurStage = "host-ok" AndAlso TcGameGone() Then
                                Text = "游戏已关闭，正在等待陶瓦确认（通常 30 秒内）…之后这个房间码会失效。"
                            End If
                            TcLastUrl = CurUrl
                            If CurStage = "host-ok" Then TcLastRoom = Code Else TcLastRoom = ""
                            '房主态：直连地址在陶瓦里是**游客本机的回环地址**（127.0.0.1:端口），
                            '房主自己拿到也发不出去。所以房主态干脆不提供这个选项，见 TcApplyCopyMode。
                            Dim NowHost As Boolean = CurStage.StartsWith("host-", StringComparison.Ordinal)
                            If NowHost <> TcIsHost Then
                                TcIsHost = NowHost
                                TcApplyCopyMode()
                            Else
                                TcUpdateCopyButtonText()
                            End If
                            LabTcStatus.Text = Text
                            '只在房主真正建成房（host-ok）时才回填房间码，方便直接复制发给朋友。
                            '为什么不写成"有码就填"：复位后守护进程可能还会短暂返回旧码，
                            '那样作废的码会被重新写回输入框，看起来像仍然有效。
                            If Code <> "" AndAlso Snapshot IsNot Nothing AndAlso Snapshot.Stage = "host-ok" AndAlso TextTcRoom.Text <> Code Then
                                AniControlEnabled += 1
                                TextTcRoom.Text = Code
                                AniControlEnabled -= 1
                            End If
                        End Sub)
                Catch ex As Exception
                    Logger.Warn(ex, "刷新陶瓦联机状态失败")
                Finally
                    TcTickBusy = False
                End Try
            End Sub)
    End Sub

    ''' <summary>建房：先在游戏里对局域网开放，否则会停在「等待游戏开启局域网」。</summary>
    Private Sub BtnTcHost_Click(sender As Object, e As MouseButtonEventArgs) Handles BtnTcHost.Click
        If Not ModCustom.TcInstalled Then
            Hint("还没装陶瓦联机，先点「安装 / 更新陶瓦」。", HintType.Red, False)
            Return
        End If
        BtnTcHost.IsEnabled = False
        LabTcStatus.Text = "正在启动陶瓦并建房…"
        Threading.Tasks.Task.Run(
            Sub()
                Dim Message As String = ModCustom.TcStartHost()
                RunInUi(
                    Sub()
                        BtnTcHost.IsEnabled = True
                        Hint(Message, HintType.Blue, False)
                    End Sub)
            End Sub)
    End Sub

    Private Sub BtnTcJoin_Click(sender As Object, e As MouseButtonEventArgs) Handles BtnTcJoin.Click
        Dim Code As String = TextTcRoom.Text
        If ModCustom.TcNormalizeCode(Code) = "" Then
            Hint("房间码格式不对：应为 U/XXXX-XXXX-XXXX-XXXX，共 21 个字符。", HintType.Red, False)
            Return
        End If
        If Not ModCustom.TcInstalled Then
            Hint("还没装陶瓦联机，先点「安装 / 更新陶瓦」。", HintType.Red, False)
            Return
        End If
        BtnTcJoin.IsEnabled = False
        LabTcStatus.Text = "正在启动陶瓦并加入房间…"
        Threading.Tasks.Task.Run(
            Sub()
                Dim Message As String = ModCustom.TcJoin(Code)
                RunInUi(
                    Sub()
                        BtnTcJoin.IsEnabled = True
                        Hint(Message, HintType.Blue, False)
                    End Sub)
            End Sub)
    End Sub

    ''' <summary>清空房间码输入框与缓存。复位与关闭陶瓦都会让当前码作废，
    ''' 框里继续留着那串码会造成"界面说已作废、却还能复制走"的自相矛盾。</summary>
    Private Sub TcClearRoomField()
        AniControlEnabled += 1
        TextTcRoom.Text = ""
        AniControlEnabled -= 1
        TcLastRoom = ""
        TcLastUrl = ""
    End Sub

    ''' <summary>游戏进程是否已退出。只认 PCL 自己启动的那次；拿不到就当未知（返回 False），
    ''' 避免误报——这句提示是软提示，但说错比不说更糟。</summary>
    Private Function TcGameGone() As Boolean
        Try
            Dim P As System.Diagnostics.Process = ModLaunch.McLaunchProcess
            If P Is Nothing Then Return False
            Return P.HasExited
        Catch
            Return False
        End Try
    End Function

    Private Sub BtnTcCancel_Click(sender As Object, e As MouseButtonEventArgs) Handles BtnTcCancel.Click
        Threading.Tasks.Task.Run(
            Sub()
                ModCustom.TcReset()
                RunInUi(
                    Sub()
                        TcClearRoomField()
                        Hint("已复位，当前房间码作废。", HintType.Blue, False)
                    End Sub)
            End Sub)
    End Sub

    ''' <summary>算出"现在按下复制会得到什么"。按钮标签与实际复制共用这一份结果。</summary>
    Private Sub TcCopyTarget(ByRef WantAddr As Boolean, ByRef Payload As String, ByRef What As String)
        Dim Auto As Boolean = ModCustom.TcAutoAddrMode
        '房主态一律走房间码：直连地址是游客本机的回环地址（127.0.0.1:端口），
        '房主拿到也发不出去，让用户勾一个"永远拿不到"的选项只会被当成故障（实测反馈：
        '"房间已经创建好了，却提示暂无直连地址"）。
        If TcIsHost Then
            WantAddr = False
        Else
            '自动检测：优先房间码；只有房间码拿不到时才改用直连地址。
            '直连地址是大厅那一条连不上时的兜底，不该当成首选（2026-10-29 反馈纠正）。
            '手动：严格按勾选走，**勾的那种拿不到时不偷偷换成另一种**——
            '换了就等于勾选失效：勾着「直连地址」却拿到房间码，看起来像勾选框坏了（实测反馈就是"切换失败"）。
            If Auto Then
                WantAddr = (TcLastRoom = "") AndAlso (TcLastUrl <> "")
            Else
                WantAddr = ChkTcCopyAddr IsNot Nothing AndAlso ChkTcCopyAddr.Checked
            End If
        End If
        Payload = If(WantAddr, TcLastUrl, TcLastRoom)
        What = If(WantAddr, "直连地址", "房间码")
    End Sub

    ''' <summary>复制按钮文字始终预告"按下会发生什么"，与实际复制结果同源。</summary>
    Private Sub TcUpdateCopyButtonText()
        If BtnTcCopy Is Nothing Then Return
        Dim WantAddr As Boolean = False, Payload As String = "", What As String = ""
        TcCopyTarget(WantAddr, Payload, What)
        If Payload = "" Then
            '勾选/自动选中的那种当前不存在：标签照实写"要的那一种 + 暂无"并禁用。
            '两个好处：一勾一取消标签立刻跟着变（勾选框看起来是有效的）；
            '按不下去，就不会出现"标签说房间码、结果拿到地址"这种错位。
            BtnTcCopy.Text = "复制" & What & "（暂无）"
            BtnTcCopy.IsEnabled = False
            If ModCustom.TcAutoAddrMode Then
                BtnTcCopy.ToolTip = "当前还没有可复制的内容：房间码要建房成功之后才有，直连地址要作为游客连上之后才有。"
                Return
            End If
            BtnTcCopy.ToolTip = If(WantAddr,
                "当前没有直连地址 —— 直连地址只有作为游客连上之后才有；房主没有。" & vbCrLf &
                "想复制房间码的话，把下面那个勾选取消即可。",
                "当前没有房间码 —— 房主建房成功（房间已就绪）之后才会出现。")
        Else
            BtnTcCopy.Text = "复制" & What
            BtnTcCopy.IsEnabled = True
            BtnTcCopy.ToolTip = $"复制{What}：{Payload}"
        End If
    End Sub

    Private Sub ChkTcCopyAddr_Change(sender As Object, user As Boolean) Handles ChkTcCopyAddr.Change
        ModCustom.TcCopyAddrMode = ChkTcCopyAddr.Checked
        TcUpdateCopyButtonText()
    End Sub

    ''' <summary>按角色与模式刷好控件显隐与按钮文字。三种情况只留一种解释：
    ''' 房主态（没有直连地址这个概念）/ 自动检测开（替你挑）/ 手动（按勾选）。</summary>
    Private Sub TcApplyCopyMode()
        Dim Auto As Boolean = ModCustom.TcAutoAddrMode
        '房主态连勾选框都不出现：直连地址是游客本机的回环地址，房主勾了也永远拿不到，
        '留着就是一个"必定失败"的开关。这里换成一行说明，讲清为什么没有。
        If ChkTcCopyAddr IsNot Nothing Then
            ChkTcCopyAddr.Visibility = If(Auto OrElse TcIsHost, Visibility.Collapsed, Visibility.Visible)
        End If
        If LabTcAutoHint IsNot Nothing Then
            LabTcAutoHint.Visibility = If(Auto AndAlso Not TcIsHost, Visibility.Visible, Visibility.Collapsed)
        End If
        If LabTcHostNote IsNot Nothing Then
            LabTcHostNote.Visibility = If(TcIsHost AndAlso Not Auto, Visibility.Visible, Visibility.Collapsed)
        End If
        TcUpdateCopyButtonText()
    End Sub

    Private Sub ChkTcAutoAddr_Change(sender As Object, user As Boolean) Handles ChkTcAutoAddr.Change
        ModCustom.TcAutoAddrMode = ChkTcAutoAddr.Checked
        TcApplyCopyMode()
    End Sub

    ''' <summary>复制。复制什么由按钮标签预告，标签与实际结果同源（见 TcCopyTarget）。</summary>
    Private Sub BtnTcCopy_Click(sender As Object, e As MouseButtonEventArgs) Handles BtnTcCopy.Click
        Dim WantAddr As Boolean = False, Payload As String = "", What As String = ""
        TcCopyTarget(WantAddr, Payload, What)
        If Payload = "" Then
            '按钮此时是禁用的，正常点不到；留一句兜底，免得键盘/脚本触发时无声无息。
            Hint(If(WantAddr,
                    "还没有直连地址：只有作为游客连上之后才有，房主没有直连地址。",
                    "还没有房间码：房主建房成功（房间已就绪）之后才有。"), HintType.Red, False)
            Return
        End If
        Try
            System.Windows.Forms.Clipboard.SetDataObject(Payload, False, retryTimes:=10, retryDelay:=100)
            Hint($"已复制{What}：{Payload}", HintType.Green, False)
        Catch ex As Exception
            Logger.Warn(ex, "复制联机信息失败")
            Hint($"复制失败，请手动输入：{Payload}", HintType.Red, False)
        End Try
    End Sub

    Private Sub BtnTcInstall_Click(sender As Object, e As MouseButtonEventArgs) Handles BtnTcInstall.Click
        If TcInstalling Then Return
        TcInstalling = True
        BtnTcInstall.IsEnabled = False
        LabTcStatus.Text = "正在下载并校验陶瓦联机（约 8 MB）…"
        Hint("开始下载陶瓦联机，完成后自动解压并校验。", HintType.Blue, False)
        Threading.Tasks.Task.Run(
            Sub()
                Dim Message As String = ModCustom.TcInstall()
                RunInUi(
                    Sub()
                        TcInstalling = False
                        BtnTcInstall.IsEnabled = True
                        Hint(Message, HintType.Blue, False)
                    End Sub)
            End Sub)
    End Sub

    Private Sub BtnTcStop_Click(sender As Object, e As MouseButtonEventArgs) Handles BtnTcStop.Click
        Threading.Tasks.Task.Run(
            Sub()
                ModCustom.TcShutdown()
                RunInUi(
                    Sub()
                        '关闭陶瓦后房间码必然作废，必须同步清空——否则框里留着能复制的旧码，
                        '用户会把它发给朋友，而那个房间已经不存在了。
                        TcClearRoomField()
                        TcLastStage = ""
                        TcOverrideText = ""
                        Hint("已请求关闭陶瓦，房间码作废，游戏里的联机会随之断开。", HintType.Blue, False)
                        TcRefreshUiAsync()
                    End Sub)
            End Sub)
    End Sub


    ' ── 更多面板与删除陶瓦 ──────────────────────────────────────────────────

    Private Sub BtnTcMore_Click(sender As Object, e As MouseButtonEventArgs) Handles BtnTcMore.Click
        Dim Showing As Boolean = PanTcMore.Visibility = Visibility.Visible
        PanTcMore.Visibility = If(Showing, Visibility.Collapsed, Visibility.Visible)
        BtnTcMore.Text = If(Showing, "更多", "收起")
    End Sub

    Private Sub BtnTcDelete_Click(sender As Object, e As MouseButtonEventArgs) Handles BtnTcDelete.Click
        Dim Msg As String =
            "确定要删除陶瓦吗？" & vbCrLf & vbCrLf &
            "这会做两件事：" & vbCrLf &
            "1. 结束正在运行的陶瓦进程（联机中的房间会立即失效）；" & vbCrLf &
            "2. 删除 PCL\Terracotta 下已下载的程序（约 8 MB）。" & vbCrLf & vbCrLf &
            "删除后可以随时点「安装 / 更新陶瓦」重新下载。"
        If MyMsgBox(Msg, "删除陶瓦", "删除", "取消", IsWarn:=True) <> 1 Then Return
        Try
            ModCustom.TcShutdown()
            Dim Dir As String = ModCustom.TcDir
            If IO.Directory.Exists(Dir) Then IO.Directory.Delete(Dir, True)
            ModCustom.TcSetExePath("")
            ModCustom.TcClearLastRoom()
            Hint("已删除陶瓦：进程已结束，程序目录已清空。", HintType.Green, False)
            TcRefreshUiAsync()
        Catch ex As Exception
            Logger.Error(ex, "删除陶瓦失败")
            Hint("删除失败：" & ex.Message, HintType.Red, False)
        End Try
    End Sub

End Class
