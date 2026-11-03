Imports System.Windows.Forms

Public Class PageLoginLegacy

    Public Sub New()
        InitializeComponent()
        Skin.Loader = PageLaunchLeft.SkinLegacy
    End Sub
    Private Sub PageLoginLegacy_Loaded(sender As Object, e As RoutedEventArgs) Handles Me.Loaded
        Skin.Loader.Start()
    End Sub

    Public IsReloaded As Boolean = False
    ''' <summary>
    ''' 刷新页面显示的所有信息。
    ''' </summary>
    Public Sub Reload(KeepInput As Boolean)
        If KeepInput AndAlso IsReloaded Then '避免第一次就以 KeepInput 的方式加载，导致文本框里没东西
            '保留输入，只刷新下拉框列表
            Dim Input As String = ComboName.Text.Trim
            ComboName.ItemsSource = If(Settings.Get(Of String)("LoginLegacyName") = "", Nothing, Settings.Get(Of String)("LoginLegacyName").Split("¨"))
            ComboName.Text = Input
        Else
            '不保留输入，刷新列表后自动选择第一项
            If Settings.Get(Of String)("LoginLegacyName") = "" Then
                ComboName.ItemsSource = Nothing
            Else
                ComboName.ItemsSource = Settings.Get(Of String)("LoginLegacyName").ToString.Split("¨")
                ComboName.Text = Settings.Get(Of String)("LoginLegacyName").ToString.BeforeFirst("¨").Trim
            End If
        End If
        IsReloaded = True
        '回填自定义 UUID（存的是规范化后的 32 位）
        Dim SavedUuid As String = Settings.Get(Of String)("LoginLegacyUuid")
        CheckUuid.Checked = SavedUuid <> ""
        TextUuid.Text = SavedUuid
        PanUuid.Visibility = If(CheckUuid.Checked, Visibility.Visible, Visibility.Collapsed)
        RefreshUuidState()
    End Sub

    ' ── 自定义 UUID ──────────────────────────────────
    '''<summary>去掉连字符、空格并转大写，不做合法性判断。</summary>
    Public Shared Function NormalizeUuid(Raw As String) As String
        If Raw Is Nothing Then Return ""
        Return Raw.Replace("-", "").Replace(" ", "").Trim.ToUpperInvariant()
    End Function
    '''<summary>是否为合法的 32 位十六进制 UUID。</summary>
    Public Shared Function IsValidUuid(Raw As String) As Boolean
        Return NormalizeUuid(Raw).RegexCheck("^[0-9A-F]{32}$")
    End Function
    '''<summary>
    ''' 按标准离线算法算出 UUID：MD5("OfflinePlayer:" + 名字)，再把版本位改成 3、变体位改成 10xx。
    ''' Java 版服务端离线模式、HMCL 等都用这个；PCL 自己的算法不是这个。
    ''' </summary>
    Public Shared Function CalcOfflineUuid(UserName As String) As String
        If UserName Is Nothing OrElse UserName.Trim = "" Then Return ""
        Using Md As System.Security.Cryptography.MD5 = System.Security.Cryptography.MD5.Create()
            Dim Raw As Byte() = Md.ComputeHash(System.Text.Encoding.UTF8.GetBytes("OfflinePlayer:" & UserName.Trim))
            Raw(6) = CByte((Raw(6) And &HF) Or &H30) '版本 3
            Raw(8) = CByte((Raw(8) And &H3F) Or &H80) 'RFC 4122 变体
            Dim Sb As New System.Text.StringBuilder(32)
            For Each B As Byte In Raw
                Sb.Append(B.ToString("x2"))
            Next
            Return Sb.ToString().ToUpperInvariant()
        End Using
    End Function

    Private Sub BtnCalcUuid_Click(sender As Object, e As MouseButtonEventArgs) Handles BtnCalcUuid.Click
        Dim Name As String = If(ComboName.Text, "").Replace("¨", "").Trim
        If Name = "" Then
            Hint("先在上面填玩家名。", HintType.Red, False)
            Return
        End If
        TextUuid.Text = CalcOfflineUuid(Name)
        Hint("已按标准离线算法生成。", HintType.Green, False)
    End Sub

    Private Sub CheckUuid_Change(sender As Object, user As Boolean) Handles CheckUuid.Change
        If PanUuid Is Nothing OrElse TextUuid Is Nothing Then Return '构造期间可能还没初始化完
        PanUuid.Visibility = If(CheckUuid.Checked, Visibility.Visible, Visibility.Collapsed)
        '不勾就清空，避免"关了其实还在生效"的误会
        If Not CheckUuid.Checked Then TextUuid.Text = ""
        RefreshUuidState()
    End Sub

    Private Sub TextUuid_TextChanged(sender As Object, e As TextChangedEventArgs) Handles TextUuid.TextChanged
        RefreshUuidState()
    End Sub

    '''<summary>保存并更新输入框下方的状态说明。</summary>
    Private Sub RefreshUuidState()
        If PanUuid Is Nothing OrElse LabUuidState Is Nothing Then Return
        Dim Raw As String = If(TextUuid.Text, "").Trim
        Dim Value As String = If(CheckUuid.Checked, NormalizeUuid(Raw), "")
        Settings.Set("LoginLegacyUuid", Value)
        LabUuidState.ClearValue(TextBlock.ForegroundProperty)
        If Value = "" Then
            LabUuidState.Text = "留空则按玩家名自动生成。"
        ElseIf Value.RegexCheck("^[0-9A-F]{32}$") Then
            LabUuidState.Text = "将使用：" & Value
        Else
            LabUuidState.Text = $"格式不对：需要 32 个 0-9 / A-F 字符，现在填的是 {Value.Length} 个。"
            LabUuidState.SetResourceReference(TextBlock.ForegroundProperty, "ColorBrushRedLight")
        End If
    End Sub
    ''' <summary>
    ''' 获取当前页面的登录信息。
    ''' </summary>
    Public Shared Function GetLoginData() As McLoginData
        Dim UserName As String = If(FrmLoginLegacy Is Nothing, "", FrmLoginLegacy.ComboName.Text.Replace("¨", "").Trim)
        Dim CustomUuid As String = ""
        If FrmLoginLegacy IsNot Nothing AndAlso FrmLoginLegacy.CheckUuid.Checked Then
            CustomUuid = NormalizeUuid(FrmLoginLegacy.TextUuid.Text)
        End If
        Return New McLoginLegacy With {.UserName = UserName, .CustomUuid = CustomUuid, .SkinType = Settings.Get(Of Integer)("LaunchSkinType"), .SkinName = Settings.Get(Of String)("LaunchSkinID")}
    End Function
    ''' <summary>
    ''' 当前页面的登录信息是否有效。
    ''' </summary>
    Public Shared Function IsVaild(LoginData As McLoginLegacy) As String
        If LoginData.UserName.Trim = "" Then Return "玩家名不能为空！"
        If LoginData.UserName.Contains("""") Then Return "玩家名不能包含英文引号！"
        '填了自定义 UUID 就必须能用，格式错不能放行（错误的值会被当成另一个玩家）
        If LoginData.CustomUuid <> "" AndAlso Not IsValidUuid(LoginData.CustomUuid) Then
            Return "自定义 UUID 必须是 32 位十六进制（0-9、A-F），现在填的是 " & LoginData.CustomUuid.Length & " 个字符！"
        End If
        If McInstanceSelected IsNot Nothing AndAlso
           McInstanceSelected.Version.Valid AndAlso McInstanceSelected.Version.Vanilla >= New Version(20, 0, 3) AndAlso
           LoginData.UserName.Trim.Length > 16 Then
            Return "自 1.20.3 起，玩家名至多只能包含 16 个字符！"
        End If
        Return ""
    End Function
    Public Function IsVaild() As String
        Return IsVaild(GetLoginData())
    End Function

    Private Sub ComboName_PreviewKeyDown(sender As Object, e As Input.KeyEventArgs) Handles ComboName.PreviewKeyDown
        If e.Key = Key.Space Then
            Dim Index = CType(ComboName.Template.FindName("PART_EditableTextBox", ComboName), MyTextBox).CaretIndex
            If Index = ComboName.Text.Length OrElse Index = 0 Then e.Handled = True
        End If
    End Sub
    Private Sub ComboLegacy_TextChanged(sender As Object, e As TextChangedEventArgs) Handles ComboName.TextChanged
        If Settings.Get(Of Integer)("LaunchSkinType") = 0 Then PageLaunchLeft.SkinLegacy.Start(IsForceRestart:=True)
        HintChinese.Visibility = If(ComboName.Text.RegexCheck("^[0-9A-Za-z_]*$"), Visibility.Collapsed, Visibility.Visible)
        RunInUi(Sub() PanMain.InvalidateMeasure(), True) '由于 WPF 的 Bug，它不一定会自动更新 HintChinese 的大小（#6627）
    End Sub
    Private Sub Skin_Click() Handles Skin.Click
        If (Settings.Get(Of Boolean)("UiHiddenPageSetup") OrElse Settings.Get(Of Boolean)("UiHiddenSetupLaunch")) AndAlso Not PageSetupUI.HiddenForceShow Then
            Hint("启动设置已被禁用！", HintType.Red)
        Else
            FrmMain.PageChange(FormMain.PageType.Setup, FormMain.PageSubType.SetupLaunch) '切换到皮肤设置页面
        End If
    End Sub

End Class
