'自用分支：本分支（GitHub 仓库）的自动更新。
' 默认关闭（设置项 SystemSelfUpdate），发布版用户在设置里打开；自用版保持关闭，避免被合规版覆盖。
' Release 约定：tag 形如 2026.10.31-custom.7，资产里第一个 .exe 就是新版本。
Imports Newtonsoft.Json.Linq

Public Module UpdateSelf

    Private Const Repo As String = "4huang0yan4/pcl-shiguangxiugaiban"

    '''<summary>把 2026.10.31-custom.7 规范成 2026.10.31.7，便于 CompareVersion 比较。</summary>
    Friend Function NormalizeVersion(S As String) As String
        If String.IsNullOrWhiteSpace(S) Then Return ""
        Return S.Trim().TrimStart("v"c, "V"c).Replace("-custom.", ".").Replace("-", ".")
    End Function

    '''<summary>查询最新 Release；失败返回 Nothing（不弹窗）。</summary>
    Friend Function FetchLatestRelease() As JObject
        Try
            Dim Urls As String() = {
                $"https://api.github.com/repos/{Repo}/releases/latest",
                $"https://gh-proxy.com/https://api.github.com/repos/{Repo}/releases/latest",
                $"https://ghfast.top/https://api.github.com/repos/{Repo}/releases/latest"
            }
            Dim Raw As String = NetRequestByLoader(Urls, Timeout:=12000, IsJson:=True)
            Return JObject.Parse(Raw)
        Catch ex As Exception
            Logger.Warn(ex, "查询本分支 Release 失败")
            Return Nothing
        End Try
    End Function

    '''<summary>Release 信息（引用类型，便于返回 Nothing）。</summary>
    Friend Class UpdateInfo
        Friend Tag As String
        Friend Url As String
        Friend Body As String
    End Class

    '''<summary>有比当前更新的版本时返回 UpdateInfo，否则 Nothing。</summary>
    Friend Function GetNewerRelease() As UpdateInfo
        Dim Rel As JObject = FetchLatestRelease()
        If Rel Is Nothing Then Return Nothing
        Dim Tag As String = If(Rel("tag_name")?.ToString, "").Trim()
        If Tag = "" Then Return Nothing
        If CompareVersion(NormalizeVersion(Tag), NormalizeVersion(VersionBaseName)) <= 0 Then Return Nothing
        Dim Assets As JArray = TryCast(Rel("assets"), JArray)
        If Assets Is Nothing OrElse Assets.Count = 0 Then Return Nothing
        Dim Url As String = Nothing
        For Each A As JToken In Assets
            Dim Name As String = A("name")?.ToString
            If Name IsNot Nothing AndAlso Name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) Then
                Url = A("browser_download_url")?.ToString
                Exit For
            End If
        Next
        If String.IsNullOrWhiteSpace(Url) Then Return Nothing
        Return New UpdateInfo With {.Tag = Tag, .Url = Url, .Body = If(Rel("body")?.ToString, "")}
    End Function

    '''<summary>「检查更新」按钮入口（开启自更新时由 UpdateCheckByButton 调用）。</summary>
    Friend Sub CheckByButton()
        RunInNewThread(Sub()
            Try
                Dim Newer = GetNewerRelease()
                If Newer Is Nothing Then
                    RunInUi(Sub() MyMsgBox($"当前已是最新版本（{VersionBaseName}）。", "检查更新"))
                    Return
                End If
                RunInUi(Sub()
                    Dim Body As String = If(Newer.Body, "").Trim()
                    If Body.Length > 600 Then Body = Body.Substring(0, 600) & "…"
                    If MyMsgBox($"发现新版本：{Newer.Tag}{vbCrLf}当前版本：{VersionBaseName}{vbCrLf}{vbCrLf}{Body}{vbCrLf}{vbCrLf}是否下载并重启更新？", "发现新版本", "下载并更新", "取消") = 1 Then
                        DownloadAndApply(Newer.Url, Newer.Tag)
                    End If
                End Sub)
            Catch ex As Exception
                RunInUi(Sub() Hint("检查更新失败：" & ex.Message, HintType.Red, False))
            End Try
        End Sub, "Self Update Check")
    End Sub

    '''<summary>启动时静默检查：同一个 tag 只提示一次。</summary>
    Friend Sub CheckBackground()
        Try
            Dim Newer = GetNewerRelease()
            If Newer Is Nothing Then Return
            Dim Cache As String = PathTemp & "Cache\SelfUpdate.tag"
            If FileUtils.Exists(Cache) AndAlso FileUtils.ReadAsString(Cache).Trim() = Newer.Tag Then Return
            FileUtils.Write(Cache, Newer.Tag)
            RunInUi(Sub()
                If MyMsgBox($"发现新版本：{Newer.Tag}{vbCrLf}当前版本：{VersionBaseName}{vbCrLf}{vbCrLf}是否现在下载并重启更新？", "本分支更新", "下载并更新", "以后再说") = 1 Then
                    DownloadAndApply(Newer.Url, Newer.Tag)
                End If
            End Sub)
        Catch ex As Exception
            Logger.Warn(ex, "后台检查本分支更新失败")
        End Try
    End Sub

    Private Sub DownloadAndApply(Url As String, Tag As String)
        RunInNewThread(Sub()
            Try
                Dim Dir As String = PathTemp & "Update\"
                DirectoryUtils.Create(Dir)
                Dim NewExe As String = Dir & "PCL-new.exe"
                NetDownloadByLoader(Url, NewExe)
                RunInUi(Sub() ApplyUpdate(NewExe, Tag))
            Catch ex As Exception
                RunInUi(Sub() Hint("下载更新失败：" & ex.Message, HintType.Red, False))
            End Try
        End Sub, "Self Update Download")
    End Sub

    Private Sub ApplyUpdate(NewExe As String, Tag As String)
        Try
            Dim Bat As String = PathTemp & "Update\apply.cmd"
            Dim Old As String = PathExe
            Dim Text As String =
                "@echo off" & vbCrLf &
                "timeout /t 2 >nul" & vbCrLf &
                $"move /y ""{NewExe}"" ""{Old}""" & vbCrLf &
                $"start """" ""{Old}""" & vbCrLf &
                "del ""%~f0""" & vbCrLf
            FileUtils.Write(Bat, Text, encoding:=Encoding.Default)
            Process.Start(New ProcessStartInfo("cmd.exe", $"/c ""{Bat}""") With {.CreateNoWindow = True, .UseShellExecute = False})
            Logger.Info($"已下载 {Tag}，等待替换并重启")
            FormMain.EndProgramForce(ProcessReturnValues.TaskDone)
        Catch ex As Exception
            Logger.Error(ex, "应用更新失败")
            Hint("应用更新失败：" & ex.Message, HintType.Red, False)
        End Try
    End Sub

End Module