Public Class PageInstanceOverall

    Private IsLoad As Boolean = False
    Private Sub PageSetupLaunch_Loaded(sender As Object, e As RoutedEventArgs) Handles Me.Loaded

        '重复加载部分
        PanBack.ScrollToHome()

        '更新设置
        Reload()

        '非重复加载部分
        If IsLoad Then Return
        IsLoad = True
        PanDisplay.TriggerForceResize()

    End Sub

    Public InstanceHeaderItem As MyListItem
    ''' <summary>
    ''' 确保当前页面上的信息已正确显示。
    ''' </summary>
    Private Sub Reload()
        AniControlEnabled += 1

        '刷新设置项目
        ComboDisplayType.SelectedIndex = ReadIni(PageInstanceLeft.Instance.PathVersion & "PCL\Setup.ini", "DisplayType", McInstanceCardType.Auto)
        BtnDisplayStar.Text = If(PageInstanceLeft.Instance.IsStar, "从收藏夹中移除", "加入收藏夹")
        BtnFolderMods.Visibility = If(PageInstanceLeft.Instance.Modable, Visibility.Visible, Visibility.Collapsed)
        '刷新版本显示
        PanDisplayItem.Children.Clear()
        InstanceHeaderItem = PageInstanceLeft.Instance.ToListItem().Init()
        InstanceHeaderItem.IsHitTestVisible = False
        PanDisplayItem.Children.Add(InstanceHeaderItem)
        FrmMain.PageNameRefresh()
        '刷新版本图标
        ComboDisplayLogo.SelectedIndex = 0
        Dim Logo As String = ReadIni(PageInstanceLeft.Instance.PathVersion & "PCL\Setup.ini", "Logo", "")
        Dim LogoCustom As Boolean = ReadIni(PageInstanceLeft.Instance.PathVersion & "PCL\Setup.ini", "LogoCustom", "False")
        If LogoCustom Then
            For Each Selection As MyComboBoxItem In ComboDisplayLogo.Items
                If Selection.Tag = Logo OrElse (Selection.Tag = "PCL\Logo.png" AndAlso Logo.EndsWithF("PCL\Logo.png")) Then
                    ComboDisplayLogo.SelectedItem = Selection
                    Exit For
                End If
            Next
        End If

        AniControlEnabled -= 1
    End Sub

#Region "卡片：个性化"

    '版本分类
    Private Sub ComboDisplayType_SelectionChanged(sender As Object, e As SelectionChangedEventArgs) Handles ComboDisplayType.SelectionChanged
        If Not (IsLoad AndAlso AniControlEnabled = 0) Then Return
        If ComboDisplayType.SelectedIndex <> 1 Then
            '改为不隐藏
            Try
                '若设置分类为可安装 Mod，则显示正常的 Mod 管理页面
                WriteIni(PageInstanceLeft.Instance.PathVersion & "PCL\Setup.ini", "DisplayType", ComboDisplayType.SelectedIndex)
                PageInstanceLeft.Instance.DisplayType = ReadIni(PageInstanceLeft.Instance.PathVersion & "PCL\Setup.ini", "DisplayType", McInstanceCardType.Auto)
                FrmInstanceLeft.RefreshModDisabled()

                WriteIni(McFolderSelected & "PCL.ini", "InstanceCache", "") '要求刷新缓存
                LoaderFolderRun(McInstanceListLoader, McFolderSelected, LoaderFolderRunType.ForceRun, MaxDepth:=1, ExtraPath:="versions\")
            Catch ex As Exception
                Logger.Error(ex, $"修改版本分类失败（{PageInstanceLeft.Instance.Name}）")
            End Try
            Reload() '更新 “打开 Mod 文件夹” 按钮
        Else
            '改为隐藏
            Try
                If Not Settings.Get(Of Boolean)("HintHide") Then
                    If MyMsgBox("确认要从版本列表中隐藏该版本吗？隐藏该版本后，它将不再出现于 PCL 显示的版本列表中。" & vbCrLf & "此后，在版本列表页面按下 F11 才可以查看被隐藏的版本。", "隐藏版本提示",, "取消") <> 1 Then
                        ComboDisplayType.SelectedIndex = 0
                        Return
                    End If
                    Settings.Set("HintHide", True)
                End If
                WriteIni(PageInstanceLeft.Instance.PathVersion & "PCL\Setup.ini", "DisplayType", McInstanceCardType.Hidden)
                WriteIni(McFolderSelected & "PCL.ini", "InstanceCache", "") '要求刷新缓存
                LoaderFolderRun(McInstanceListLoader, McFolderSelected, LoaderFolderRunType.ForceRun, MaxDepth:=1, ExtraPath:="versions\")
            Catch ex As Exception
                Logger.Error(ex, $"隐藏版本 {PageInstanceLeft.Instance.Name} 失败")
            End Try
        End If
    End Sub

    '更改描述
    Private Sub BtnDisplayDesc_Click(sender As Object, e As EventArgs) Handles BtnDisplayDesc.Click
        Try
            Dim OldInfo As String = ReadIni(PageInstanceLeft.Instance.PathVersion & "PCL\Setup.ini", "CustomInfo")
            Dim NewInfo As String = MyMsgBoxInput("更改描述", "修改版本的描述文本，留空则使用 PCL 的默认描述。", OldInfo, New ObjectModel.Collection(Of Validate), "默认描述")
            If NewInfo IsNot Nothing AndAlso OldInfo <> NewInfo Then WriteIni(PageInstanceLeft.Instance.PathVersion & "PCL\Setup.ini", "CustomInfo", NewInfo)
            PageInstanceLeft.Instance = New McInstance(PageInstanceLeft.Instance.Name).Load()
            Reload()
            LoaderFolderRun(McInstanceListLoader, McFolderSelected, LoaderFolderRunType.ForceRun, MaxDepth:=1, ExtraPath:="versions\")
        Catch ex As Exception
            Logger.Error(ex, $"版本 {PageInstanceLeft.Instance.Name} 描述更改失败", LogBehavior.Alert)
        End Try
    End Sub

    '重命名版本
    Private Sub BtnDisplayRename_Click(sender As Object, e As EventArgs) Handles BtnDisplayRename.Click
        Try
            If McInstanceLock.IsLocked(PageInstanceLeft.Instance) Then
                Hint("该版本正在被修改，请稍后再重命名！", HintType.Red)
                Return
            End If
            '确认输入的新名称
            Dim OldName As String = PageInstanceLeft.Instance.Name
            '修改此部分的同时修改快速安装的版本名检测*
            Dim NewName As String = MyMsgBoxInput("重命名版本", "", OldName, New ObjectModel.Collection(Of Validate) From {
                New ValidateFolderName(McFolderSelected & "versions", IgnoreList:=New List(Of String) From {OldName})
            })
            If String.IsNullOrWhiteSpace(NewName) Then Return
            Dim NewInstance = PageInstanceLeft.Instance.Rename(NewName)
            '刷新与提示
            Hint("重命名成功！", HintType.Green)
            PageInstanceLeft.Instance = NewInstance
            If Not IsNothing(McInstanceSelected) AndAlso McInstanceSelected.Equals(PageInstanceLeft.Instance) Then WriteIni(McFolderSelected & "PCL.ini", "Version", NewName)
            Reload()
            LoaderFolderRun(McInstanceListLoader, McFolderSelected, LoaderFolderRunType.ForceRun, MaxDepth:=1, ExtraPath:="versions\")
        Catch ex As Exception
            Logger.Error(ex, "重命名版本失败", LogBehavior.Alert)
        End Try
    End Sub

    '版本图标
    Private Sub ComboDisplayLogo_SelectionChanged() Handles ItemDisplayLogoCustom.PreviewMouseLeftButtonUp, ComboDisplayLogo.SelectionChanged
        If Not (IsLoad AndAlso AniControlEnabled = 0) Then Return
        '选择 自定义 时修改图片
        Try
            If ComboDisplayLogo.SelectedItem Is ItemDisplayLogoCustom Then
                Dim FileName As String = Dialogs.SelectFile("选择图片", False, filter:={({"png", "jpeg", "jpg", "gif", "webp"}, "常用图片文件")}).FirstOrDefault()
                If String.IsNullOrEmpty(FileName) Then
                    Reload() '还原选项
                    Return
                End If
                FileUtils.Copy(FileName, PageInstanceLeft.Instance.PathVersion & "PCL\Logo.png")
            Else
                FileUtils.Delete(PageInstanceLeft.Instance.PathVersion & "PCL\Logo.png")
            End If
        Catch ex As Exception
            Logger.Error(ex, $"更改自定义版本图标失败（{PageInstanceLeft.Instance.Name}）")
        End Try
        '进行更改
        Try
            Dim NewLogo As String = ComboDisplayLogo.SelectedItem.Tag
            WriteIni(PageInstanceLeft.Instance.PathVersion & "PCL\Setup.ini", "Logo", NewLogo)
            WriteIni(PageInstanceLeft.Instance.PathVersion & "PCL\Setup.ini", "LogoCustom", Not NewLogo = "")
            '刷新显示
            WriteIni(McFolderSelected & "PCL.ini", "InstanceCache", "") '要求刷新缓存
            PageInstanceLeft.Instance = New McInstance(PageInstanceLeft.Instance.Name).Load()
            Reload()
            LoaderFolderRun(McInstanceListLoader, McFolderSelected, LoaderFolderRunType.ForceRun, MaxDepth:=1, ExtraPath:="versions\")
        Catch ex As Exception
            Logger.Error(ex, $"更改版本图标失败（{PageInstanceLeft.Instance.Name}）")
        End Try
    End Sub

    '收藏夹
    Private Sub BtnDisplayStar_Click(sender As Object, e As EventArgs) Handles BtnDisplayStar.Click
        Try
            WriteIni(PageInstanceLeft.Instance.PathVersion & "PCL\Setup.ini", "IsStar", Not PageInstanceLeft.Instance.IsStar)
            PageInstanceLeft.Instance = New McInstance(PageInstanceLeft.Instance.Name).Load()
            Reload()
            McInstanceListForceRefresh = True
            LoaderFolderRun(McInstanceListLoader, McFolderSelected, LoaderFolderRunType.ForceRun, MaxDepth:=1, ExtraPath:="versions\")
        Catch ex As Exception
            Logger.Error(ex, $"版本 {PageInstanceLeft.Instance.Name} 收藏状态更改失败", LogBehavior.Alert)
        End Try
    End Sub

#End Region

#Region "卡片：快捷方式"

    '版本文件夹
    Private Sub BtnFolderVersion_Click() Handles BtnFolderVersion.Click
        OpenInstanceFolder(PageInstanceLeft.Instance)
    End Sub
    Public Shared Sub OpenInstanceFolder(Instance As McInstance)
        OpenExplorer(Instance.PathVersion)
    End Sub

    '存档文件夹
    Private Sub BtnFolderSaves_Click() Handles BtnFolderSaves.Click
        Dim Folder As String = PageInstanceLeft.Instance.PathIndie & "saves\"
        DirectoryUtils.Create(Folder)
        OpenExplorer(Folder)
    End Sub

    'Mod 文件夹
    Private Sub BtnFolderMods_Click() Handles BtnFolderMods.Click
        Dim Folder As String = PageInstanceLeft.Instance.PathIndie & "mods\"
        DirectoryUtils.Create(Folder)
        OpenExplorer(Folder)
    End Sub

    '截图文件夹
    Private Sub BtnFolderScreen_Click(sender As Object, e As MouseButtonEventArgs) Handles BtnFolderScreen.Click
        Dim Folder As String = PageInstanceLeft.Instance.PathIndie & "screenshots\"
        DirectoryUtils.Create(Folder)
        OpenExplorer(Folder)
    End Sub

    '占用空间
    Private Sub BtnFolderSize_Click(sender As Object, e As EventArgs) Handles BtnFolderSize.Click
        Dim Instance As McInstance = PageInstanceLeft.Instance
        If Instance Is Nothing Then Return
        '大版本目录动辄几万个文件，必须在后台线程数，否则界面会卡住几秒
        BtnFolderSize.IsEnabled = False
        Hint("正在统计占用空间…")
        RunInNewThread(
        Sub()
            Try
                Dim Report As String = BuildSizeReport(Instance)
                RunInUi(
                Sub()
                    BtnFolderSize.IsEnabled = True
                    If MyMsgBox(Report, "占用空间 · " & Instance.Name, "打开版本文件夹", "关闭") = 1 Then
                        OpenInstanceFolder(Instance)
                    End If
                End Sub)
            Catch ex As Exception
                Logger.Error(ex, "统计版本占用空间失败")
                RunInUi(
                Sub()
                    BtnFolderSize.IsEnabled = True
                    Hint("统计失败：" & ex.Message, HintType.Red, False)
                End Sub)
            End Try
        End Sub, "Instance Size")
    End Sub

    ''' <summary>统计一个实例各文件夹的占用，返回可直接显示的文本。</summary>
    Private Shared Function BuildSizeReport(Instance As McInstance) As String
        Dim Sizes As New List(Of KeyValuePair(Of String, Long))
        Dim Total As Long = 0
        Dim Indie As String = Instance.PathIndie
        '按一级子目录分类统计；不写死清单，mods / saves / resourcepacks 之外的目录也能看到
        If DirectoryUtils.Exists(Indie) Then
            Dim Dirs As String() = {}
            Try
                Dirs = Directory.GetDirectories(Indie)
            Catch ex As Exception
                Logger.Warn(ex, "枚举版本目录失败：" & Indie)
            End Try
            For Each SubDir In Dirs
                Dim Size As Long = GetFolderSize(SubDir)
                Total += Size
                Sizes.Add(New KeyValuePair(Of String, Long)(IO.Path.GetFileName(SubDir), Size))
            Next
            '根目录下的散装文件（options.txt、日志等）
            Dim FilesSize As Long = 0
            Try
                For Each F As FileInfo In New DirectoryInfo(Indie).EnumerateFiles()
                    FilesSize += F.Length
                Next
            Catch ex As Exception
                Logger.Warn(ex, "统计根目录文件大小失败")
            End Try
            If FilesSize > 0 Then
                Total += FilesSize
                Sizes.Add(New KeyValuePair(Of String, Long)("（根目录文件）", FilesSize))
            End If
        End If
        '版本隔离关闭时，PathVersion 是独立的 versions\<名称> 目录，里面是 jar / json
        Dim VerPath As String = Instance.PathVersion
        If Not String.IsNullOrWhiteSpace(VerPath) AndAlso
           Not VerPath.TrimEnd("\"c).Equals(Indie.TrimEnd("\"c), StringComparison.OrdinalIgnoreCase) Then
            Dim VerSize As Long = GetFolderSize(VerPath)
            Total += VerSize
            Sizes.Add(New KeyValuePair(Of String, Long)("versions\" & IO.Path.GetFileName(VerPath), VerSize))
        End If

        Dim Sb As New System.Text.StringBuilder
        Sb.AppendLine($"共占用 {FormatSize(Total)}")
        Sb.AppendLine()
        For Each Pair In Sizes.OrderByDescending(Function(p) p.Value)
            If Pair.Value = 0 AndAlso Pair.Key <> "（根目录文件）" Then Continue For
            '目录名对齐，方便竖着看
            Sb.AppendLine($"  {Pair.Key.PadRight(18)} {FormatSize(Pair.Value)}")
        Next
        '顺手提示可清理的部分，不与百宝箱的垃圾清理重复统计
        Dim Junk As Long = GetFolderSize(IO.Path.Combine(Indie, "logs")) + GetFolderSize(IO.Path.Combine(Indie, "crash-reports"))
        If Junk > 0 Then
            Sb.AppendLine()
            Sb.AppendLine($"其中日志与崩溃报告 {FormatSize(Junk)}，可在「设置 → 启动器 → 百宝箱 → 垃圾清理」里删掉。")
        End If
        Return Sb.ToString.TrimEnd
    End Function

    ''' <summary>递归计算文件夹大小；无权限的子目录会跳过而不是中断整个统计。</summary>
    Private Shared Function GetFolderSize(Folder As String) As Long
        If Not DirectoryUtils.Exists(Folder) Then Return 0
        Dim Size As Long = 0
        Try
            '用 DirectoryInfo.EnumerateFiles 而不是逐个 FileInfo.Length：
            '前者在枚举时就带上了大小，几万文件能快一个数量级
            For Each F As FileInfo In New DirectoryInfo(Folder).EnumerateFiles("*", SearchOption.AllDirectories)
                Size += F.Length
            Next
        Catch ex As Exception
            Logger.Warn(ex, "统计文件夹大小失败：" & Folder)
        End Try
        Return Size
    End Function

    Private Shared Function FormatSize(Bytes As Long) As String
        If Bytes >= 1024L * 1024 * 1024 Then Return (Bytes / 1024 / 1024 / 1024).ToString("0.00") & " GB"
        If Bytes >= 1024L * 1024 Then Return (Bytes / 1024 / 1024).ToString("0.0") & " MB"
        If Bytes >= 1024 Then Return (Bytes / 1024).ToString("0.0") & " KB"
        Return Bytes & " B"
    End Function

#End Region

#Region "卡片：管理"

    '导出启动脚本
    Private Sub BtnManageScript_Click() Handles BtnManageScript.Click
        Try
            '弹窗要求指定脚本的保存位置
            Dim SavePath As String = Dialogs.SaveFile("选择脚本保存位置", "启动 " & PageInstanceLeft.Instance.Name & ".bat", filter:={("bat", "批处理文件")})
            If SavePath Is Nothing Then Return
            '检查中断（等玩家选完弹窗指不定任务就结束了呢……）
            If McLaunchLoader.State = LoadState.Loading Then
                Hint("请在当前启动任务结束后再试！", HintType.Red)
                Return
            End If
            '生成脚本
            If McLaunchStart(New McLaunchOptions With {.SaveBatch = SavePath, .Instance = PageInstanceLeft.Instance}) Then
                If Settings.Get(Of McLoginType)("LoginType") = McLoginType.Legacy Then
                    Hint("正在导出启动脚本……")
                Else
                    Hint("正在导出启动脚本……（注意，使用脚本启动可能会导致登录失效！）")
                End If
            End If
        Catch ex As Exception
            Logger.Error(ex, $"导出启动脚本失败（{PageInstanceLeft.Instance.Name}）", LogBehavior.Alert)
        End Try
    End Sub

    '补全文件
    Private Sub BtnManageCheck_Click(sender As Object, e As EventArgs) Handles BtnManageCheck.Click
        Try
            If McInstanceLock.IsLocked(PageInstanceLeft.Instance) Then
                Hint("该版本正在被修改，请稍后再补全文件！", HintType.Red)
                Return
            End If
            '忽略文件检查提示
            If ShouldIgnoreFileCheck(PageInstanceLeft.Instance) Then
                Hint("请先关闭 [版本设置 → 设置 → 高级启动选项 → 关闭文件校验]，然后再尝试补全文件！", HintType.Blue)
                Return
            End If
            '重复任务检查
            For Each OngoingLoader In LoaderTaskbar
                If OngoingLoader.Name <> PageInstanceLeft.Instance.Name & " 文件补全" Then Continue For
                Hint("正在处理中，请稍候！", HintType.Red)
                Return
            Next
            '启动
            Dim Loader As New LoaderCombo(Of String)(PageInstanceLeft.Instance.Name & " 文件补全", DlClientFix(PageInstanceLeft.Instance, True, False))
            Loader.OnStateChanged =
            Sub()
                Select Case Loader.State
                    Case LoadState.Finished
                        Hint(Loader.Name & "成功！", HintType.Green)
                    Case LoadState.Failed
                        Hint(Loader.Name & "失败：" & Loader.Error.GetDisplay(False), HintType.Red)
                    Case LoadState.Canceled
                        Hint(Loader.Name & "已取消！", HintType.Blue)
                End Select
            End Sub
            Loader.Start(PageInstanceLeft.Instance.Name)
            LoaderTaskbarAdd(Loader)
            FrmMain.BtnExtraDownload.ShowRefresh()
            FrmMain.BtnExtraDownload.Ribble()
        Catch ex As Exception
            Logger.Error(ex, $"尝试补全文件失败（{PageInstanceLeft.Instance.Name}）", LogBehavior.Alert)
        End Try
    End Sub

    '删除版本
    '修改此代码时，同时修改 PageSelectRight 中的代码
    '迁移数据到其他版本
    Private Sub BtnMigrate_Click(sender As Object, e As EventArgs) Handles BtnMigrate.Click
        Dim Source As McInstance = PageInstanceLeft.Instance
        If Source Is Nothing Then Return
        '可接收数据的其他版本
        Dim Others As List(Of McInstance) = McInstanceList.Values.SelectMany(Function(L) L).
            Where(Function(I) I IsNot Nothing AndAlso Not String.IsNullOrWhiteSpace(I.PathIndie) AndAlso
                             Not PathUtils.IsSame(I.PathIndie, Source.PathIndie)).ToList()
        If Not Others.Any() Then
            Hint("没有其他版本可以接收数据。先在下载页装一个目标版本，再回来迁移。", HintType.Blue)
            Return
        End If
        Dim TargetChoice As Integer? = Nothing
        '必须显式给 Button2：MyMsgBoxSelect 的 Button2 默认为空，为空时取消按钮会被折叠，
        '而没有选中项时「确定」是禁用的，等于弹窗关不掉
        RunInUiWait(Sub() TargetChoice = MyMsgBoxSelect(
            Others.Select(Function(I) CType(New MyRadioBox With {
                .Text = I.Name & If(I.Version Is Nothing, "", "（" & I.Version.VanillaName & "）")}, IMyRadio)),
            "迁移到哪个版本", "确定", "取消"))
        If Not TargetChoice.HasValue Then Return
        Dim Target As McInstance = Others(TargetChoice.Value)

        Dim ScopeChoices As New List(Of IMyRadio) From {
            CType(New MyRadioBox With {.Text = "仅存档（saves）"}, IMyRadio),
            CType(New MyRadioBox With {.Text = "存档 + 设置（再加 options.txt、servers.dat、config）"}, IMyRadio),
            CType(New MyRadioBox With {.Text = "全部游戏数据（再加 mods、资源包、光影包、脚本）"}, IMyRadio)}
        Dim ScopeChoice As Integer? = Nothing
        RunInUiWait(Sub() ScopeChoice = MyMsgBoxSelect(ScopeChoices, "迁移哪些内容", "确定", "取消"))
        If Not ScopeChoice.HasValue Then Return

        Dim DirNames As New List(Of String) From {"saves"}
        Dim FileNames As New List(Of String)
        If ScopeChoice.Value >= 1 Then
            DirNames.AddRange({"config", "defaultconfigs"})
            FileNames.AddRange({"options.txt", "servers.dat"})
        End If
        If ScopeChoice.Value >= 2 Then
            DirNames.AddRange({"mods", "resourcepacks", "shaderpacks", "scripts", "datapacks"})
        End If

        Dim ListText As String = "  " & DirNames.Join("、") & If(FileNames.Any, vbCrLf & "  " & FileNames.Join("、"), "")
        If MyMsgBox($"将从 {Source.Name} 复制到 {Target.Name}：{vbCrLf}{ListText}{vbCrLf}{vbCrLf}" &
                    "只复制、不删除源文件。同名文件会被覆盖，所以目标版本最好是还没开始玩的。" &
                    "整合包的 mod 跨大版本通常不能直接用，迁移后可能需要重新配 mod。",
                    "确认迁移", "开始迁移", "取消", IsWarn:=True) <> 1 Then Return

        Hint("正在迁移，文件多的时候要等一会儿…")
        RunInNewThread(
        Sub()
            Try
                Dim Copied As Integer = 0
                Dim SourceIndie As String = Source.PathIndie
                Dim TargetIndie As String = Target.PathIndie
                DirectoryUtils.Create(TargetIndie)
                For Each DirName In DirNames
                    Dim SrcDir As String = IO.Path.Combine(SourceIndie, DirName)
                    If Not DirectoryUtils.Exists(SrcDir) Then Continue For
                    DirectoryUtils.Copy(SrcDir, IO.Path.Combine(TargetIndie, DirName))
                    Copied += 1
                Next
                For Each FileName In FileNames
                    Dim SrcFile As String = IO.Path.Combine(SourceIndie, FileName)
                    If Not FileUtils.Exists(SrcFile) Then Continue For
                    FileUtils.Copy(SrcFile, IO.Path.Combine(TargetIndie, FileName))
                    Copied += 1
                Next
                RunInUi(Sub()
                    If Copied = 0 Then
                        Hint("源版本里没有找到这些内容，什么都没复制。", HintType.Blue)
                    Else
                        Hint($"已迁移 {Copied} 项到 {Target.Name}。", HintType.Green)
                    End If
                End Sub)
            Catch ex As Exception
                Logger.Error(ex, "迁移版本数据失败")
                RunInUi(Sub() Hint("迁移失败：" & ex.Message, HintType.Red, False))
            End Try
        End Sub, "Migrate Instance")
    End Sub

    Private Sub BtnManageDelete_Click(sender As Object, e As EventArgs) Handles BtnManageDelete.Click
        Try
            If McInstanceLock.IsLocked(PageInstanceLeft.Instance) Then
                Hint("该版本正在被修改，请稍后再删除！", HintType.Red)
                Return
            End If
            Dim IsShiftPressed As Boolean = My.Computer.Keyboard.ShiftKeyDown
            Dim IsHintIndie As Boolean = PageInstanceLeft.Instance.State <> McInstance.McInstanceState.Error AndAlso PageInstanceLeft.Instance.PathIndie <> McFolderSelected
            Dim SavesFolder = DirectoryUtils.GetInfo(PageInstanceLeft.Instance.PathIndie & "saves\")
            Dim SaveEntries = If(IsHintIndie AndAlso SavesFolder.Exists, SavesFolder.EnumerateDirectories.OrderByDescending(Function(Save) Save.LastWriteTime).Select(Function(Save) $"{Save.Name}（上次修改：{StringUtils.FormatTimeSpan(Save.LastWriteTime - Date.Now, False)}）").ToList, New List(Of String))
            Select Case MyMsgBox($"你确定要{If(IsShiftPressed, "永久", "")}删除版本 {PageInstanceLeft.Instance.Name} 吗？" &
                        If(IsHintIndie, vbCrLf & "该版本对应的存档、资源包、Mod 等文件也将被一并删除！" &
                            If(SaveEntries.Any, vbCrLf & vbCrLf & "这会删除以下存档：" & vbCrLf & "· " & SaveEntries.Join(vbCrLf & "· "), ""), ""),
                        "版本删除确认", , "取消",, IsHintIndie OrElse IsShiftPressed)
                Case 1
                    PageInstanceLeft.Instance.ResetSettingsCache()
                    DirectoryUtils.Delete(PageInstanceLeft.Instance.PathVersion, Not IsShiftPressed)
                    Hint("已删除版本 " & PageInstanceLeft.Instance.Name & "！", HintType.Green)
                Case 2
                    Return
            End Select
            LoaderFolderRun(McInstanceListLoader, McFolderSelected, LoaderFolderRunType.ForceRun, MaxDepth:=1, ExtraPath:="versions\")
            FrmMain.PageBack()
        Catch ex As OperationCanceledException
            Logger.Warn(ex, $"删除版本 {PageInstanceLeft.Instance.Name} 被主动取消")
        Catch ex As Exception
            Logger.Error(ex, $"删除版本 {PageInstanceLeft.Instance.Name} 失败", LogBehavior.Alert)
        End Try
    End Sub

#End Region

End Class
