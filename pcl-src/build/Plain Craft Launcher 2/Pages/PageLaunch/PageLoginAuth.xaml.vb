Public Class PageLoginAuth
    ''' <summary>LittleSkin 的 Yggdrasil API 地址，作为外置登录的默认值。</summary>
    Private Const LittleSkinApi As String = "https://littleskin.cn/api/yggdrasil"
    ''' <summary>LittleSkin 的注册页不在 api 路径下，需要单独给。</summary>
    Private Const LittleSkinRegister As String = "https://littleskin.cn/auth/register"

    Private IsFirstLoad As Boolean = True

    ''' <summary>
    ''' 当前生效的验证服务器地址：优先取输入框，其次取版本设置 / 全局缓存。
    ''' </summary>
    Private Shared Function GetCurrentServer() As String
        Dim FromBox As String = ""
        '控件可能还没初始化（Me.Loaded 时就会调到这里），逐层判空
        If FrmLoginAuth IsNot Nothing AndAlso FrmLoginAuth.ComboServer IsNot Nothing Then
            FromBox = If(FrmLoginAuth.ComboServer.Text, "").Trim
        End If
        Dim Result As String
        If Not String.IsNullOrWhiteSpace(FromBox) Then
            Result = FromBox
        ElseIf McInstanceSelected Is Nothing Then
            Result = If(Settings.Get(Of String)("CacheAuthServerServer"), "")
        Else
            Result = If(Settings.Get(Of String)("VersionServerAuthServer", Instance:=McInstanceSelected), "")
        End If
        Return Result.Trim.TrimEnd("/"c)
    End Function

    ''' <summary>
    ''' 由验证服务器地址推出注册页。常规约定 server 地址形如 &lt;站点&gt;/api/yggdrasil。
    ''' </summary>
    Private Shared Function GetRegisterUrl(ServerUrl As String) As String
        Dim Cleaned As String = If(ServerUrl, "").Trim.TrimEnd("/"c)
        If Cleaned = "" Then Return ""
        If Cleaned.Equals(LittleSkinApi, StringComparison.OrdinalIgnoreCase) Then Return LittleSkinRegister
        If Cleaned.EndsWithF("/api/yggdrasil", True) Then
            Return Cleaned.Substring(0, Cleaned.Length - "/api/yggdrasil".Length) & "/auth/register"
        End If
        Return Cleaned & "/auth/register"
    End Function

    ''' <summary>
    ''' 刷新页面显示的所有信息。
    ''' </summary>
    Public Sub Reload(KeepInput As Boolean)
        '记住密码
        CheckRemember.Checked = Settings.Get(Of Boolean)("LoginRemember")
        If KeepInput AndAlso Not IsFirstLoad Then '避免第一次就以 KeepInput 的方式加载，导致文本框里没东西
            '保留输入，只刷新下拉框列表
            Dim Input As String = ComboName.Text
            ComboName.ItemsSource = If(Settings.Get(Of String)("LoginAuthEmail") = "", Nothing, Settings.Get(Of String)("LoginAuthEmail").Split("¨"))
            ComboName.Text = Input
        Else
            '不保留输入，刷新列表后自动选择第一项
            If Settings.Get(Of String)("LoginAuthEmail") = "" Then
                ComboName.ItemsSource = Nothing
            Else
                ComboName.ItemsSource = Settings.Get(Of String)("LoginAuthEmail").Split("¨")
                ComboName.Text = Settings.Get(Of String)("LoginAuthEmail").BeforeFirst("¨")
                If Settings.Get(Of Boolean)("LoginRemember") Then TextPass.Password = Settings.Get(Of String)("LoginAuthPass").BeforeFirst("¨").Trim
            End If
        End If
        '服务器地址：当前版本没配过就默认 LittleSkin，让外置登录开箱能用
        AniControlEnabled += 1
        Dim ServerUrl As String = GetCurrentServer()
        If String.IsNullOrWhiteSpace(ServerUrl) Then ServerUrl = LittleSkinApi
        ComboServer.ItemsSource = New String() {LittleSkinApi}
        ComboServer.Text = ServerUrl
        AniControlEnabled -= 1
        ReloadRegisterButton()
        IsFirstLoad = False
    End Sub

    ''' <summary>
    ''' 获取当前页面的登录信息。
    ''' </summary>
    Public Shared Function GetLoginData() As McLoginServer
        Dim ServerUrl As String = GetCurrentServer()
        If String.IsNullOrWhiteSpace(ServerUrl) Then ServerUrl = LittleSkinApi
        Dim Server As String = ServerUrl & "/authserver"
        If FrmLoginAuth Is Nothing Then
            Return New McLoginServer(McLoginType.Auth) With {.Token = "Auth", .BaseUrl = Server, .UserName = "", .Password = "", .Description = "Authlib-Injector", .Type = McLoginType.Auth}
        Else
            Return New McLoginServer(McLoginType.Auth) With {.Token = "Auth", .BaseUrl = Server, .UserName = FrmLoginAuth.ComboName.Text.Replace("¨", "").Trim, .Password = FrmLoginAuth.TextPass.Password.Replace("¨", "").Trim, .Description = "Authlib-Injector", .Type = McLoginType.Auth}
        End If
    End Function
    ''' <summary>
    ''' 当前页面的登录信息是否有效。
    ''' </summary>
    Public Shared Function IsVaild(LoginData As McLoginServer) As String
        If LoginData.UserName = "" Then Return "账号不能为空！"
        If LoginData.Password = "" Then Return "密码不能为空！"
        Return ""
    End Function
    Public Function IsVaild() As String
        Return IsVaild(GetLoginData())
    End Function

    '服务器地址变化：写回设置，并同步注册链接
    Private Sub ComboServer_TextChanged(sender As Object, e As TextChangedEventArgs) Handles ComboServer.TextChanged
        If AniControlEnabled <> 0 Then Return
        Dim Value As String = ComboServer.Text.Trim.TrimEnd("/"c).Trim
        If Value = "" Then Return
        If McInstanceSelected Is Nothing Then
            If Value <> Settings.Get(Of String)("CacheAuthServerServer") Then
                Settings.Set("CacheAuthServerServer", Value)
                Settings.Set("CacheAuthServerRegister", GetRegisterUrl(Value))
            End If
        Else
            If Value <> Settings.Get(Of String)("VersionServerAuthServer", Instance:=McInstanceSelected) Then
                Settings.Set("VersionServerAuthServer", Value, Instance:=McInstanceSelected)
                Settings.Set("VersionServerAuthRegister", GetRegisterUrl(Value), Instance:=McInstanceSelected)
            End If
        End If
        ReloadRegisterButton()
    End Sub

    '保存输入信息
    Private Sub ComboName_TextChanged(sender As Object, e As TextChangedEventArgs) Handles ComboName.TextChanged
        If sender.Text = "" Then TextPass.Password = ""
        If AniControlEnabled = 0 Then Settings.Set("CacheAuthAccess", "")  '迫使其不进行 Validate
    End Sub
    Private Sub TextPass_PasswordChanged(sender As Object, e As RoutedEventArgs) Handles TextPass.PasswordChanged
        If AniControlEnabled = 0 Then Settings.Set("CacheAuthAccess", "")
    End Sub
    Private Sub ComboName_SelectionChanged(sender As MyComboBox, e As SelectionChangedEventArgs) Handles ComboName.SelectionChanged
        If sender.SelectedIndex = -1 OrElse Not Settings.Get(Of Boolean)("LoginRemember") Then
            TextPass.Password = ""
        Else
            TextPass.Password = Settings.Get(Of String)("LoginAuthPass").ToString.Split("¨")(sender.SelectedIndex).Trim
        End If
    End Sub
    Private Sub CheckBoxChange(sender As MyCheckBox, e As Object) Handles CheckRemember.Change
        If AniControlEnabled = 0 Then Settings.Set(sender.Tag, sender.Checked)
    End Sub

    '链接处理
    Private Sub ComboName_TextChanged() Handles ComboName.TextChanged
        BtnLink.Content = If(ComboName.Text = "", "注册账号", "找回密码")
    End Sub
    Private Sub Btn_Click(sender As Object, e As EventArgs) Handles BtnLink.Click
        Dim RegisterUrl As String = GetRegisterUrl(GetCurrentServer())
        If BtnLink.Content = "注册账号" Then
            OpenWebsite(RegisterUrl)
        Else
            OpenWebsite(RegisterUrl.Replace("/auth/register", "/auth/forgot"))
        End If
    End Sub
    '切换注册按钮可见性
    Private Sub ReloadRegisterButton() Handles Me.Loaded
        Dim Address As String = GetRegisterUrl(GetCurrentServer())
        BtnLink.Visibility = If(String.IsNullOrEmpty(New ValidateHttp().Validate(Address)), Visibility.Visible, Visibility.Collapsed)
    End Sub

End Class
