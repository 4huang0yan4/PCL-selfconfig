'自用分支：本分支（GitHub 仓库）的自动更新。
' Release 约定（2026-11-03 起）：
'   tag  = PCL-selfconfig-custom.N（对外发布号）
'   资产 = PCL-selfconfig-custom.N.zip，内含 Plain Craft Launcher 2.exe 与 .exe.config
'   正文 = 必含一行「本地构建号：2026.11.03-custom.3」，用于和 VersionBaseName 比较
' 设置项 SystemSelfUpdate 控制启动时静默检查；「检查更新」按钮始终可用。
Imports Newtonsoft.Json.Linq
Imports System.IO.Compression
Imports System.Text.RegularExpressions

Public Module UpdateSelf

    Private Const Repo As String = "4huang0yan4/PCL-selfconfig"

    '''<summary>把 2026.11.03-custom.3 规范成 2026.11.03.3，便于 CompareVersion 比较。</summary>
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
        Friend Build As String
        Friend Url As String
        Friend Body As String
    End Class

    '''<summary>从 Release 正文里取「本地构建号：2026.11.03-custom.3」。</summary>
    Friend Function ParseBuildFromBody(Body As String) As String
        If String.IsNullOrWhiteSpace(Body) Then Return ""
        For Each Line As String In Body.Split({vbCrLf, vbLf}, StringSplitOptions.RemoveEmptyEntries)
            Dim T As String = Line.Trim()
            If Not T.StartsWith("本地构建号") Then Continue For
            Dim M As Match = Regex.Match(T, "\d{4}\.\d{2}\.\d{2}-custom\.\d+")
            If M.Success Then Return M.Value
        Next
        Return ""
    End Function

    '''<summary>有比当前更新的版本时返回 UpdateInfo，否则 Nothing。</summary>
    Friend Function GetNewerRelease() As UpdateInfo
        Dim Rel As JObject = FetchLatestRelease()
        If Rel Is Nothing Then Return Nothing
        Dim Tag As String = If(Rel("tag_name")?.ToString, "").Trim()
        If Tag = "" Then Return Nothing
        Dim Body As String = If(Rel("body")?.ToString, "")
        Dim Url As String = Nothing
        Dim Assets As JArray = TryCast(Rel("assets"), JArray)
        If Assets IsNot Nothing Then
            For Each A As JToken In Assets
                Dim Name As String = A("name")?.ToString
                If Name Is Nothing Then Continue For
                If Name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) OrElse Name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) Then
                    Url = A("browser_download_url")?.ToString
                    Exit For
                End If
            Next
        End If
        If String.IsNullOrWhiteSpace(Url) Then Return Nothing
        Dim Build As String = ParseBuildFromBody(Body)
        If Build = "" Then
            ' 没有构建号就无法比较新旧，只认「已经提示过同一个 tag」
            If Settings.Get(Of String)("SelfUpdateTag") = Tag Then Return Nothing
            Return New UpdateInfo With {.Tag = Tag, .Build = "", .Url = Url, .Body = Body}
        End If
        If CompareVersion(NormalizeVersion(Build), NormalizeVersion(VersionBaseName)) <= 0 Then Return Nothing
        Return New UpdateInfo With {.Tag = Tag, .Build = Build, .Url = Url, .Body = Body}
    End Function

    Private Function Describe(Info As UpdateInfo) As String
        Dim Text As String = $"发现新版本：{Info.Tag}"
        If Info.Build <> "" Then Text &= $"（构建 {Info.Build}）"
        Text &= vbCrLf & $"当前版本：{VersionBaseName}"
        Dim B As String = If(Info.Body, "").Trim()
        If B.Length > 500 Then B = B.Substring(0, 500) & "…"
        If B <> "" Then Text &= vbCrLf & vbCrLf & B
        Return Text
    End Function

    '''<summary>「检查更新」按钮入口。</summary>
    Friend Sub CheckByButton()
        RunInNewThread(Sub()
            Try
                Dim Newer = GetNewerRelease()
                If Newer Is Nothing Then
                    RunInUi(Sub() MyMsgBox($"当前已是最新版本（{VersionBaseName}）。", "检查更新"))
                    Return
                End If
                RunInUi(Sub()
                    If MyMsgBox(Describe(Newer) & vbCrLf & vbCrLf & "是否下载并重启更新？", "发现新版本", "下载并更新", "取消") = 1 Then
                        DownloadAndApply(Newer)
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
            If Settings.Get(Of String)("SelfUpdateTag") = Newer.Tag Then Return
            Settings.Set("SelfUpdateTag", Newer.Tag)
            RunInUi(Sub()
                If MyMsgBox(Describe(Newer) & vbCrLf & vbCrLf & "是否现在下载并重启更新？", "本分支更新", "下载并更新", "以后再说") = 1 Then
                    DownloadAndApply(Newer)
                End If
            End Sub)
        Catch ex As Exception
            Logger.Warn(ex, "后台检查本分支更新失败")
        End Try
    End Sub

    Private Sub DownloadAndApply(Info As UpdateInfo)
        RunInNewThread(Sub()
            Try
                Dim Dir As String = PathTemp & "Update\"
                Directory.CreateDirectory(Dir)
                Dim Pack As String = Dir & "pack.tmp"
                NetDownloadByLoader(Info.Url, Pack)
                Dim NewExe As String = Dir & "PCL-new.exe"
                Dim NewCfg As String = Dir & "PCL-new.exe.config"
                If Info.Url.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) Then
                    Using Zip As ZipArchive = ZipFile.OpenRead(Pack)
                        For Each E As ZipArchiveEntry In Zip.Entries
                            Select Case E.Name.ToLowerInvariant()
                                Case "plain craft launcher 2.exe"
                                    E.ExtractToFile(NewExe, True)
                                Case "plain craft launcher 2.exe.config"
                                    E.ExtractToFile(NewCfg, True)
                            End Select
                        Next
                    End Using
                Else
                    File.Copy(Pack, NewExe, True)
                End If
                If Not FileUtils.Exists(NewExe) Then Throw New Exception("下载包里没有找到启动器主程序")
                RunInUi(Sub() ApplyUpdate(NewExe, NewCfg, Info.Tag))
            Catch ex As Exception
                RunInUi(Sub() Hint("下载更新失败：" & ex.Message, HintType.Red, False))
            End Try
        End Sub, "Self Update Download")
    End Sub

    Private Sub ApplyUpdate(NewExe As String, NewCfg As String, Tag As String)
        Try
            Dim Bat As String = PathTemp & "Update\apply.cmd"
            Dim Old As String = PathExe
            Dim OldCfg As String = Old & ".config"
            Dim L As New List(Of String) From {
                "@echo off",
                "timeout /t 2 >nul",
                $"copy /y ""{NewExe}"" ""{Old}"" >nul"
            }
            If FileUtils.Exists(NewCfg) Then L.Add($"copy /y ""{NewCfg}"" ""{OldCfg}"" >nul")
            L.Add($"start """" ""{Old}""")
            L.Add("del ""%~f0""")
            FileUtils.Write(Bat, String.Join(vbCrLf, L), encoding:=Encoding.Default)
            Process.Start(New ProcessStartInfo("cmd.exe", $"/c ""{Bat}""") With {.CreateNoWindow = True, .UseShellExecute = False})
            Logger.Info($"已下载 {Tag}，等待替换并重启")
            FormMain.EndProgramForce(ProcessReturnValues.TaskDone)
        Catch ex As Exception
            Logger.Error(ex, "应用更新失败")
            Hint("应用更新失败：" & ex.Message, HintType.Red, False)
        End Try
    End Sub

End Module
