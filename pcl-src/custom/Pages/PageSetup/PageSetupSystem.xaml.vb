Public Class PageSetupSystem


    '允许旧版 TLS：默认关闭；开启要二次确认，改完立即生效。
    Private Sub ChkLegacyTls_Change(sender As Object, user As Boolean) Handles ChkLegacyTls.Change
        If Not user Then Return
        If ChkLegacyTls.Checked Then
            Dim R As Integer = MyMsgBox(
                "开启后会允许 TLS 1.0 / 1.1。" & vbCrLf & vbCrLf &
                "这能救回一些只支持旧协议的老镜像站，但连接安全性会降低（旧协议已有已知攻击面）。" & vbCrLf &
                "只在「下载源连不上、报 TLS/SSL 错误」时才建议开启；平时请保持关闭。" & vbCrLf & vbCrLf &
                "确定要开启吗？", "降低连接安全性", "开启", "取消", IsWarn:=True)
            If R <> 1 Then
                AniControlEnabled += 1
                ChkLegacyTls.SetChecked(False, False)
                AniControlEnabled -= 1
                Return
            End If
        End If
        Settings.Set("ToolDownloadLegacyTls", ChkLegacyTls.Checked)
        ModCustom.ApplySecurityProtocol()
        Hint(If(ChkLegacyTls.Checked, "已允许旧版 TLS：老镜像站可能可以连上了。", "已恢复默认 TLS。"), HintType.Blue, False)
    End Sub

    Private Sub PageSetupSystem_Loaded(sender As Object, e As RoutedEventArgs) Handles Me.Loaded

        '重复加载部分
        PanBack.ScrollToHome()
        CredentialRefresh()
        CredentialMaskRefresh()
        MirrorRefresh()
        ChkLegacyTls.SetChecked(ModCustom.LegacyTlsEnabled, False)

        '自用分支：已删除「启动器」卡片（更新、公告、缓存目录、匿名上报、土豆码等）
        '非重复加载部分
        Static IsLoaded As Boolean = False
        If IsLoaded Then Return
        IsLoaded = True
        AttachSecretInputs()
        AttachCredentialGuards()

        AniControlEnabled += 1
        Reload()
        SliderLoad()
        AniControlEnabled -= 1

    End Sub
    Public Sub Reload()
        SettingService.RefreshSettings(Me)
    End Sub
    Public Sub Reset()
        Try
            SettingService.ResetSettings(Me)
            Logger.Info("已初始化启动器页设置")
            Hint("已初始化启动器页设置！", HintType.Green, False)
        Catch ex As Exception
            Logger.Error(ex, "初始化启动器页设置失败", LogBehavior.Alert)
        End Try
        Reload()
    End Sub

    '滑动条
    Private Sub SliderLoad()
        SliderDownloadThread.GetHintText = Function(v) v + 1
        SliderDownloadSpeed.GetHintText =
        Function(v)
            Select Case v
                Case Is <= 14
                    Return (v + 1) * 0.1 & " M/s"
                Case Is <= 31
                    Return (v - 11) * 0.5 & " M/s"
                Case Is <= 41
                    Return (v - 21) & " M/s"
                Case Else
                    Return "无限制"
            End Select
        End Function
        SliderDebugAnim.GetHintText = Function(v) If(v > 29, "关闭", (v / 10 + 0.1) & "x")
    End Sub
    Private Sub SliderDownloadThread_PreviewChange(sender As Object, e As RouteEventArgs) Handles SliderDownloadThread.PreviewChange
        If SliderDownloadThread.Value < 100 Then Return
        If Not Settings.Get(Of Boolean)("HintDownloadThread") Then
            Settings.Set("HintDownloadThread", True)
            MyMsgBox("如果设置过多的下载线程，可能会导致下载时出现非常严重的卡顿。" & vbCrLf &
                     "一般设置 64 线程即可满足大多数下载需求，除非你知道你在干什么，否则不建议设置更多的线程数！", "警告", "我知道了", IsWarn:=True)
        End If
    End Sub

    '调试模式
    Private Sub CheckDebugMode_Change() Handles CheckDebugMode.Change
        If AniControlEnabled = 0 Then Hint("部分调试信息将在刷新或启动器重启后切换显示！",, False)
    End Sub

#Region "官方服务凭据"

    ''' <summary>
    ''' 把已保存的凭据填回输入框，并刷新到期状态。
    ''' </summary>
    Private Sub CredentialRefresh()
        Try
            AniControlEnabled += 1
            TextCredentialMsId.Text = ModCustom.CredentialMsId
            TextCredentialCfKey.Text = ModCustom.CredentialCfKey
            TextCredentialCfDays.Text = ModCustom.CredentialCfDays.ToString
            CheckCredentialMute.Checked = ModCustom.CredentialMuted
            Dim Left As Integer = ModCustom.CredentialCfDaysLeft
            If Left < 0 Then
                LabCredentialExpire.Text = "尚未填写 CurseForge 密钥，当前只走 MCIM 镜像搜索，功能不受影响。"
            ElseIf Left <= 0 Then
                LabCredentialExpire.Text = $"CurseForge 密钥已过期（到期日 {ModCustom.CredentialCfExpireText}），直连官方源会返回 403，请更新密钥。启动时会强制弹窗提醒（每天一次）。"
            ElseIf Left <= 3 Then
                LabCredentialExpire.Text = $"CurseForge 密钥还剩 {Left} 天到期（到期日 {ModCustom.CredentialCfExpireText}），启动时会强制弹窗提醒（每天一次，不受「不再提醒」影响）。"
            ElseIf Left <= 10 Then
                LabCredentialExpire.Text = $"CurseForge 密钥还剩 {Left} 天到期（到期日 {ModCustom.CredentialCfExpireText}），届时启动会弹小提示框，建议尽快更新。"
            Else
                LabCredentialExpire.Text = $"CurseForge 密钥还剩 {Left} 天，到期日 {ModCustom.CredentialCfExpireText}。剩余 10 天起开始提示。"
            End If
        Catch ex As Exception
            Logger.Error(ex, "刷新凭据显示失败")
        Finally
            AniControlEnabled -= 1
        End Try
    End Sub

    '==== 自用分支：凭据输入框的「按住显示」与「禁止复制」 ====
    '凭据是私密内容，默认用同色遮罩盖住；只有按住「按住显示」按钮时才揭开，松开立即恢复。
    '复制通道（Ctrl+C / Shift+Insert / 右键菜单）全部禁掉，只保留粘贴（Ctrl+V）。

    '''<summary>当前正被按住揭开的框（"Ms"/"Cf"），空表示都没按住。</summary>
    Private ActiveReveal As String = ""

    Private Sub CredentialSecret_TextChanged(sender As Object, e As TextChangedEventArgs)
        CredentialMaskRefresh()
    End Sub

    '''<summary>有内容且没被按住时显示遮罩；空框显示 Hint，不盖。</summary>
    Private Sub CredentialMaskRefresh()
        If MaskCredentialMsId Is Nothing Then Return
        Dim ShowMs As Boolean = ActiveReveal <> "Ms" AndAlso Not String.IsNullOrEmpty(TextCredentialMsId.Text)
        Dim ShowCf As Boolean = ActiveReveal <> "Cf" AndAlso Not String.IsNullOrEmpty(TextCredentialCfKey.Text)
        MaskCredentialMsId.Visibility = If(ShowMs, Visibility.Visible, Visibility.Collapsed)
        MaskCredentialCfKey.Visibility = If(ShowCf, Visibility.Visible, Visibility.Collapsed)
        If ShowMs Then LabMaskCredentialMsId.Text = MaskText(TextCredentialMsId.Text)
        If ShowCf Then LabMaskCredentialCfKey.Text = MaskText(TextCredentialCfKey.Text)
    End Sub

    '''<summary>用圆点代替明文，数量与实际长度一致（超过 40 截断，免得超长串把布局撑开）。</summary>
    Private Function MaskText(Secret As String) As String
        If String.IsNullOrEmpty(Secret) Then Return ""
        Return New String("●"c, Math.Min(Secret.Length, 40))
    End Function

    '''<summary>切走窗口或切走页面时立即收起遮挡，避免按住后 Alt+Tab / 点别处一直露着明文。</summary>
    Private Sub CredentialRevealReset()
        If ActiveReveal = "" Then Return
        ActiveReveal = ""
        CredentialMaskRefresh()
        Try
            '强制立刻重绘一帧，让任务栏缩略图拿到的也是遮罩后的画面
            Me.InvalidateVisual()
            Me.UpdateLayout()
        Catch
        End Try
    End Sub

    '''<summary>窗口失焦与页面不可见都要复位；只挂一次（由 Loaded 的 Static IsLoaded 保护）。</summary>
    Private Sub AttachCredentialGuards()
        Try
            Dim W As System.Windows.Window = System.Windows.Window.GetWindow(Me)
            If W IsNot Nothing Then
                AddHandler W.Deactivated, Sub(s, e) CredentialRevealReset()
                AddHandler W.StateChanged, Sub(s, e) CredentialRevealReset()
                '最小化前先复位：DWM 会把最小化那一刻的画面冻成缩略图，事后改控件已经晚了
                Dim Src As System.Windows.Interop.HwndSource = TryCast(System.Windows.PresentationSource.FromVisual(W), System.Windows.Interop.HwndSource)
                If Src IsNot Nothing Then Src.AddHook(AddressOf WindowSysCommandHook)
            End If
            AddHandler Me.IsVisibleChanged, Sub(s, e) CredentialRevealReset()
        Catch ex As Exception
            Logger.Warn(ex, "挂载凭据遮挡复位失败")
        End Try
    End Sub

    '''<summary>拦到 SC_MINIMIZE 时先把遮罩收起来再放行，缩略图就不会冻在明文帧上。</summary>
    Private Function WindowSysCommandHook(hwnd As IntPtr, msg As Integer, wParam As IntPtr, lParam As IntPtr, ByRef handled As Boolean) As IntPtr
        If msg = &H112 AndAlso (wParam.ToInt32() And &HFFF0) = &HF020 Then
            CredentialRevealReset()
            Me.UpdateLayout()
        End If
        Return IntPtr.Zero
    End Function

    Private Sub BtnReveal_MouseDown(sender As Object, e As MouseButtonEventArgs)
        Dim Btn As FrameworkElement = TryCast(sender, FrameworkElement)
        ActiveReveal = If(Btn Is Nothing OrElse Btn.Tag Is Nothing, "", Btn.Tag.ToString())
        CredentialMaskRefresh()
    End Sub

    Private Sub BtnReveal_MouseUp(sender As Object, e As MouseButtonEventArgs)
        ActiveReveal = ""
        CredentialMaskRefresh()
    End Sub

    '''<summary>按住后把鼠标拖出按钮、或在按钮上松开丢失捕获时，也要恢复遮挡，避免一直露着。</summary>
    Private Sub BtnReveal_MouseLeave(sender As Object, e As MouseEventArgs)
        If ActiveReveal = "" Then Return
        ActiveReveal = ""
        CredentialMaskRefresh()
    End Sub

    '''<summary>挂上「禁复制、禁剪切、只留粘贴」的三层拦截。
    '''注意：PCL 的 Style 里 ClipboardInterceptor.EnableSafeClipboard=True 已经给每个输入框加了 Copy/Cut
    '''的 CommandBinding，后加的实例级绑定抢不过它（路由命中的是第一个匹配项），所以必须在
    '''PreviewExecuted —— 早于所有 CommandBinding —— 的阶段拦，另外再兜一层 PreviewKeyDown。</summary>
    Private Sub AttachSecretInputs()
        AttachSecretInput(TextCredentialMsId)
        AttachSecretInput(TextCredentialCfKey)
    End Sub

    Private Sub AttachSecretInput(Box As TextBox)
        If Box Is Nothing Then Return
        '① 命令级：PreviewExecuted 早于所有 CommandBinding，能压过 PCL 自带的 ClipboardInterceptor
        Box.AddHandler(CommandManager.PreviewExecutedEvent, New ExecutedRoutedEventHandler(AddressOf SecretCommandPreview), True)
        '② 键盘级：Ctrl+C / Ctrl+X / Ctrl+Insert
        AddHandler Box.PreviewKeyDown, New KeyEventHandler(AddressOf SecretPreviewKeyDown)
        '③ 菜单级：右键一律吞掉。WPF 的 TextBox 系统编辑菜单会绕过 ContextMenuOpening，
        '   必须在鼠标右键按下的隧道阶段拦，否则菜单里仍会出现「复制/剪切」（虽然点了也复制不出来）。
        AddHandler Box.PreviewMouseRightButtonDown, AddressOf SecretRightButtonBlocked
        AddHandler Box.PreviewMouseRightButtonUp, AddressOf SecretRightButtonBlocked
        AddHandler Box.ContextMenuOpening, AddressOf SecretContextMenuOpening
        '④ 消息级：直接吃掉 WM_COPY / WM_CUT，兜住从别的程序发过来的复制请求
        Dim Source As System.Windows.Interop.HwndSource = TryCast(PresentationSource.FromVisual(Box), System.Windows.Interop.HwndSource)
        If Source IsNot Nothing Then Source.AddHook(AddressOf SecretWndProc)
        '⑤ 拖拽级：选中文字拖到外部是独立于剪贴板的复制通道，命令拦截管不到
        AddHandler Box.PreviewMouseMove, AddressOf SecretPreviewMouseMove
        DataObject.AddCopyingHandler(Box, AddressOf SecretDataObjectCopying)
        '⑥ 鼠标级：WPF 的拖拽在 MouseDown 之后由 OLE 接管，只能从源头拦。
        '   按下即全选并吞掉事件 → 没有选区、拖拽也不会启动；Ctrl+V 仍可整体替换。
        AddHandler Box.PreviewMouseLeftButtonDown, AddressOf SecretMouseDownBlocked
    End Sub

    '''<summary>左键在框内按下时不再派发 MouseMove：WPF 的拖拽启动阈值只有 4px，
    '''等我们能判断"是否已拖出输入框"时 OLE 已接管，所以只能整段拦住。
    '''代价：不能用鼠标拖动选择；点击定位、双击选词、Ctrl+A、Shift+方向键/Shift+点击都正常。</summary>
    Private Sub SecretPreviewMouseMove(sender As Object, e As MouseEventArgs)
        If e.LeftButton = MouseButtonState.Pressed Then e.Handled = True
    End Sub

    '''<summary>兜底：把这两个框内容打包成 DataObject（拖拽/复制）时一律取消。</summary>
    Private Sub SecretDataObjectCopying(sender As Object, e As DataObjectCopyingEventArgs)
        e.CancelCommand()
    End Sub

    '''<summary>鼠标左键按下：只给焦点并全选，随后吞掉事件——没有「按住拖动」这一步，
    '''也就不存在把明文拖到搜索框之类的通道。Ctrl+A / Ctrl+V / 键盘输入不受影响。</summary>
    Private Sub SecretMouseDownBlocked(sender As Object, e As MouseButtonEventArgs)
        Dim Box As TextBox = TryCast(sender, TextBox)
        If Box IsNot Nothing Then
            Box.Focus()
            Box.SelectAll()
        End If
        e.Handled = True
    End Sub

    '''<summary>剪贴板指令总闸：复制、剪切一律拦下，粘贴放行。</summary>
    Private Sub SecretCommandPreview(sender As Object, e As ExecutedRoutedEventArgs)
        If e.Command Is ApplicationCommands.Copy OrElse e.Command Is ApplicationCommands.Cut Then e.Handled = True
    End Sub

    '''<summary>键盘兜底：Ctrl+C / Ctrl+X / Ctrl+Insert 直接吃掉；Ctrl+V 与 Shift+Insert（粘贴）放行。</summary>
    Private Sub SecretPreviewKeyDown(sender As Object, e As KeyEventArgs)
        If Keyboard.Modifiers <> ModifierKeys.Control Then Return
        If e.Key = Key.C OrElse e.Key = Key.X OrElse e.Key = Key.Insert Then e.Handled = True
    End Sub

    Private Sub SecretContextMenuOpening(sender As Object, e As ContextMenuEventArgs)
        e.Handled = True
    End Sub

    '''<summary>右键直接吞掉：不弹菜单，也就不会看到「复制/剪切」。粘贴仍可用 Ctrl+V。</summary>
    Private Sub SecretRightButtonBlocked(sender As Object, e As MouseButtonEventArgs)
        e.Handled = True
    End Sub

    '''<summary>窗口消息兜底：焦点在这两个框里时，WM_COPY(0x0301)/WM_CUT(0x0300) 直接吞掉。</summary>
    Private Function SecretWndProc(hwnd As IntPtr, msg As Integer, wParam As IntPtr, lParam As IntPtr, ByRef handled As Boolean) As IntPtr
        If msg <> &H301 AndAlso msg <> &H300 Then Return IntPtr.Zero
        Dim Focused As IInputElement = Keyboard.FocusedElement
        If Focused Is TextCredentialMsId OrElse Focused Is TextCredentialCfKey Then
            handled = True
        End If
        Return IntPtr.Zero
    End Function

    '''<summary>微软登录 Client ID 单独保存（不动 CurseForge 那两行）。</summary>
    Private Sub BtnCredentialMsSave_Click(sender As Object, e As MouseButtonEventArgs) Handles BtnCredentialMsSave.Click
        Try
            Dim MsId As String = TextCredentialMsId.Text.Trim()
            If MsId = ModCustom.CredentialMsId Then
                Hint("微软登录 Client ID 没有变化，无需重启。", HintType.Blue, False)
                Return
            End If
            If MsId <> "" AndAlso MsId.Length < 30 AndAlso
               MyMsgBox($"这个 Client ID 只有 {MsId.Length} 位，看起来不像完整的应用程序 ID（通常为 36 位）。{vbCrLf}仍要保存吗？", "格式确认", "仍然保存", "返回检查") = 2 Then Return
            ModCustom.CredentialSave(MsId, ModCustom.CredentialCfKey, ModCustom.CredentialCfDays)
            ModCustom.ApplyAndRestart("微软登录 Client ID 已保存")
        Catch ex As Exception
            Logger.Error(ex, "保存微软登录 Client ID 失败")
            Hint("保存失败：" & ex.Message, HintType.Red, False)
        End Try
    End Sub
    Private Sub BtnCredentialMsReset_Click(sender As Object, e As MouseButtonEventArgs) Handles BtnCredentialMsReset.Click
        Try
            AniControlEnabled += 1
            TextCredentialMsId.Text = ""
        Catch ex As Exception
            Logger.Error(ex, "重置微软登录 Client ID 失败")
        Finally
            AniControlEnabled -= 1
        End Try
        Hint("已清空微软登录 Client ID，改完后点这一行的「确认」保存（留空即不使用微软登录）。",, False)
    End Sub

    '''<summary>CurseForge 密钥与有效期一起保存。</summary>
    Private Sub BtnCredentialCfSave_Click(sender As Object, e As MouseButtonEventArgs) Handles BtnCredentialCfSave.Click
        Try
            Dim CfKey As String = TextCredentialCfKey.Text.Trim()
            Dim DaysText As String = TextCredentialCfDays.Text.Trim()
            Dim Days As Integer = 0
            If DaysText <> "" AndAlso Not Integer.TryParse(DaysText, Days) Then
                Hint("有效期天数必须是数字！", HintType.Red, False)
                Return
            End If
            If Days < 0 Then
                Hint("有效期天数不能为负数！", HintType.Red, False)
                Return
            End If
            If Days = 0 Then Days = 90
            If CfKey = ModCustom.CredentialCfKey AndAlso Days = ModCustom.CredentialCfDays Then
                Hint("CurseForge 密钥与有效期都没有变化，无需重启。", HintType.Blue, False)
                Return
            End If
            ModCustom.CredentialSave(ModCustom.CredentialMsId, CfKey, Days)
            ModCustom.ApplyAndRestart("CurseForge 密钥已保存")
        Catch ex As Exception
            Logger.Error(ex, "保存 CurseForge 密钥失败")
            Hint("保存失败：" & ex.Message, HintType.Red, False)
        End Try
    End Sub
    Private Sub BtnCredentialCfReset_Click(sender As Object, e As MouseButtonEventArgs) Handles BtnCredentialCfReset.Click
        Try
            AniControlEnabled += 1
            TextCredentialCfKey.Text = ""
            TextCredentialCfDays.Text = ModCustom.CredentialCfDays.ToString '有效期回到当前设置的值（默认 90 天）
        Catch ex As Exception
            Logger.Error(ex, "重置 CurseForge 密钥失败")
        Finally
            AniControlEnabled -= 1
        End Try
        Hint("已清空 CurseForge 密钥，改完后点这一行的「确认」保存（留空即只用 MCIM 镜像）。",, False)
    End Sub

    Private Sub BtnCredentialEntra_Click(sender As Object, e As MouseButtonEventArgs) Handles BtnCredentialEntra.Click
        OpenWebsite(ModCustom.EntraUrl)
    End Sub
    Private Sub BtnCredentialCf_Click(sender As Object, e As MouseButtonEventArgs) Handles BtnCredentialCf.Click
        OpenWebsite("https://console.curseforge.com/")
    End Sub
    Private Sub CheckCredentialMute_Change(sender As Object, user As Boolean) Handles CheckCredentialMute.Change
        If AniControlEnabled <> 0 Then Return
        ModCustom.CredentialSetMuted(CheckCredentialMute.Checked)
    End Sub

#End Region

#Region "自定义下载源"

    Private Sub MirrorRefresh()
        Try
            AniControlEnabled += 1
            TextCustomMirror.Text = ModCustom.CustomMirrorSetting
        Catch ex As Exception
            Logger.Error(ex, "刷新下载源显示失败")
        Finally
            AniControlEnabled -= 1
        End Try
        LabMirrorStatus.Text = If(ModCustom.MirrorIsCustom,
            $"当前使用自定义镜像：{ModCustom.MirrorBase()}（重启启动器后生效）",
            $"当前使用默认镜像：{ModCustom.MirrorBase()}")
    End Sub

    Private Sub BtnMirrorSave_Click(sender As Object, e As MouseButtonEventArgs) Handles BtnMirrorSave.Click
        Try
            Dim Before As String = ModCustom.CustomMirrorSetting
            ModCustom.CustomMirrorSave(TextCustomMirror.Text)
            MirrorRefresh()
            If ModCustom.CustomMirrorSetting = Before Then
                Hint("下载源没有变化，无需重启。", HintType.Blue, False)
                Return
            End If
            ModCustom.ApplyAndRestart(If(ModCustom.MirrorIsCustom, "下载源已保存", "已恢复默认镜像"))
        Catch ex As Exception
            Logger.Error(ex, "保存下载源失败")
            Hint("保存失败：" & ex.Message, HintType.Red, False)
        End Try
    End Sub
    Private Sub BtnMirrorReset_Click(sender As Object, e As MouseButtonEventArgs) Handles BtnMirrorReset.Click
        Try
            AniControlEnabled += 1
            TextCustomMirror.Text = ""
        Catch ex As Exception
            Logger.Error(ex, "重置下载源失败")
        Finally
            AniControlEnabled -= 1
        End Try
        LabMirrorStatus.Text = "已清空，点「确认」保存后回到默认镜像 https://bmclapi2.bangbang93.com（重启启动器后生效）。"
        Hint("已清空镜像地址，点「确认」保存后即回到默认镜像。",, False)
    End Sub

    '==== 彻底删除（卸载）====
    '''<summary>删除程序文件、PCL 配置与注册表 PCLDebug；不碰 .minecraft 与用户文件。</summary>
    Private Sub BtnUninstall_Click(sender As Object, e As MouseButtonEventArgs) Handles BtnUninstall.Click
        Try
            Dim BasePath As String = Paths.Base
            If Not FileUtils.Exists(BasePath & "Plain Craft Launcher 2.exe") Then
                Hint("没有找到启动器主程序，已中止删除。", HintType.Red, False)
                Return
            End If
            If BasePath.TrimEnd("\"c).Length < 4 Then
                Hint("程序目录异常，已中止删除。", HintType.Red, False)
                Return
            End If
            Dim R1 As Integer = MyMsgBox(
                "这会删除以下内容：" & vbCrLf &
                "· PCL 文件夹（配置、日志、音乐、图片）" & vbCrLf &
                "· 启动器主程序与运行库" & vbCrLf &
                "· 注册表 HKCU\Software\PCLDebug（本分支的全部设置）" & vbCrLf & vbCrLf &
                "不会删除：" & vbCrLf &
                "· .minecraft 游戏目录、存档" & vbCrLf &
                "· 你自己放进这个文件夹的其他文件" & vbCrLf & vbCrLf &
                "删除直接从磁盘进行，不经过回收站，删完无法恢复。启动器会立即退出。",
                "彻底删除启动器", "我了解，继续", "取消", IsWarn:=True)
            If R1 <> 1 Then Return
            Dim R2 As Integer = MyMsgBox(
                "最后确认：真的要删除吗？" & vbCrLf & vbCrLf &
                "删除后本分支的配置与设置会全部消失（包括已填的 Client ID、主题、内存设置），无法恢复。",
                "再次确认", "删除并退出", "取消", IsWarn:=True)
            If R2 <> 1 Then Return

            Dim Dir As String = PathTemp & "Uninstall\"
            DirectoryUtils.Create(Dir)
            Dim ScriptPath As String = Dir & "PCL-Uninstall.ps1"
            Dim BaseLit As String = BasePath.TrimEnd("\"c).Replace("'", "''")
            Dim Script As String =
                "$ErrorActionPreference = 'SilentlyContinue'" & vbCrLf &
                "Start-Sleep -Seconds 3" & vbCrLf &
                "$base = '" & BaseLit & "'" & vbCrLf &
                "$targets = @((Join-Path $base 'PCL'), (Join-Path $base 'Plain Craft Launcher 2.exe'), (Join-Path $base 'Plain Craft Launcher 2.exe.config'), (Join-Path $base 'libwebp.dll'))" & vbCrLf &
                "foreach ($t in $targets) { if (Test-Path -LiteralPath $t) { Remove-Item -LiteralPath $t -Recurse -Force } }" & vbCrLf &
                "if (Test-Path -LiteralPath 'HKCU:\Software\PCLDebug') { Remove-Item -LiteralPath 'HKCU:\Software\PCLDebug' -Recurse -Force }" & vbCrLf &
                "Remove-Item -LiteralPath $PSCommandPath -Force" & vbCrLf
            FileUtils.Write(ScriptPath, Script, encoding:=New System.Text.UTF8Encoding(False))
            Process.Start(New System.Diagnostics.ProcessStartInfo("powershell.exe",
                $"-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File ""{ScriptPath}""") With {
                .UseShellExecute = False,
                .CreateNoWindow = True,
                .WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden
            })
            Logger.Info($"已请求彻底删除：{BasePath}")
            FormMain.EndProgramForce(ProcessReturnValues.TaskDone)
        Catch ex As Exception
            Logger.Error(ex, "彻底删除失败")
            Hint("删除请求失败：" & ex.Message, HintType.Red, False)
        End Try
    End Sub

#End Region

''' <summary>
    ''' 启动器是否已经是最新版？
    ''' 若返回 Nothing，则代表无更新缓存文件或出错。
    ''' </summary>
    Public Shared Function IsLauncherNewest() As Boolean?
        Try
            '确认服务器公告是否正常
            Dim ServerContent As String = If(FileUtils.TryReadAsString(PathTemp & "Cache\Notice.cfg"), "")
            If ServerContent.Split("|").Count < 3 Then Return Nothing
            '确认是否为最新
            Return ServerContent.Split("|")(If(BuildType = BuildTypes.Release, 2, 1)) <= VersionCode
        Catch ex As Exception
            Logger.Error(ex, "确认启动器更新失败")
            Return Nothing
        End Try
    End Function

End Class
