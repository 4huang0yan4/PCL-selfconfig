Public Class PageOtherHelp
    Implements IRefreshable

#Region "初始化"

    '滚动条
    Private Sub PageOther_Loaded(sender As Object, e As RoutedEventArgs) Handles Me.Loaded
        PanBack.ScrollToHome()
        '顶部长驻按钮用代码挂事件：XAML 里写 Click 是运行时反射绑定，委托签名对不上会直接弹「创建 Click 失败」，
        '这里用 AddressOf 让编译器在编译期就检查，签名不对当场报错，不会拖到用户点开页面才炸。
        RemoveHandler BtnQuickServer.Click, AddressOf OpenServerGuide
        RemoveHandler BtnQuickNet.Click, AddressOf OpenNetGuide
        AddHandler BtnQuickServer.Click, AddressOf OpenServerGuide
        AddHandler BtnQuickNet.Click, AddressOf OpenNetGuide
    End Sub
    '初始化加载器信息
    Private Sub PageOther_Inited(sender As Object, e As EventArgs) Handles Me.Initialized
        PageLoaderInit(Load, PanLoad, PanBack, Nothing, HelpLoader, AddressOf HelpListLoad)
    End Sub

#End Region

    ''' <summary>
    ''' 将帮助列表对象实例化为主页 UI。
    ''' </summary>
    Private Sub HelpListLoad(Loader As LoaderTask(Of Integer, List(Of HelpEntry)))
        Try

            '初始化
            PanList.Children.Clear()
            PanBack.ScrollToHome()
            Dim HelpItems = Loader.Output
            '获取全部分类
            Dim Types As New List(Of String)
            For Each Item As HelpEntry In HelpItems
                If BuildType = BuildTypes.Release AndAlso Not Item.ShowInPublic Then Continue For
                If BuildType <> BuildTypes.Release AndAlso Not Item.ShowInSnapshot Then Continue For
                For Each Type In Item.Types
                    If Not Types.Contains(Type) Then Types.Add(Type)
                Next
            Next
            '将指南页面置顶
            If Types.Contains("指南") Then
                Types.Remove("指南")
                Types.Insert(0, "指南")
            End If
            '转化为 UI
            For Each Type As String In Types
                '确认所属该分类的项目
                Dim TypeItems As New List(Of HelpEntry)
                For Each Item In HelpItems
                    If BuildType = BuildTypes.Release AndAlso Not Item.ShowInPublic Then Continue For
                    If BuildType <> BuildTypes.Release AndAlso Not Item.ShowInSnapshot Then Continue For
                    If Item.Types.Contains(Type) Then TypeItems.Add(Item)
                Next
                '自用分支：指南里把本版补写的两篇提到最前面（列表顺序来自 zip 的扫描顺序，不排就会夹在中间）
                If Type = "指南" Then TypeItems = PinGuideFirst(TypeItems)
                '增加卡片
                Dim NewCard As New MyCard With {.Title = Type, .Margin = New Thickness(0, 0, 0, 15)}
                Dim NewStack As New StackPanel With {.Margin = New Thickness(20, MyCard.SwapedHeight, 18, 0), .VerticalAlignment = VerticalAlignment.Top, .RenderTransform = New TranslateTransform(0, 0)}
                NewCard.Children.Add(NewStack)
                NewCard.SwapControl = NewStack
                NewCard.SetContentBuilder(TypeItems, Function(Entry) Entry.ToListItem)
                If Type = "指南" Then
                    NewCard.BuildContent()
                Else
                    NewCard.IsSwapped = True
                End If
                PanList.Children.Add(NewCard)
            Next

        Catch ex As Exception
            Logger.Error(ex, "加载帮助列表 UI 失败")
        End Try
    End Sub

    ''' <summary>
    ''' 自用分支：把本版补写的指南排到分类最前，其余保持原顺序。
    ''' </summary>
    Private Shared Function PinGuideFirst(Items As List(Of HelpEntry)) As List(Of HelpEntry)
        Dim PinnedTitles As String() = {"服务器使用方法", "联机使用方法"}
        Dim Result As New List(Of HelpEntry)
        For Each Title In PinnedTitles
            For Each Item In Items
                If Item.Title = Title AndAlso Not Result.Contains(Item) Then Result.Add(Item)
            Next
        Next
        For Each Item In Items
            If Not Result.Contains(Item) Then Result.Add(Item)
        Next
        Return Result
    End Function

    ''' <summary>
    ''' 帮助项目的点击事件。
    ''' </summary>
    Public Shared Sub OnItemClick(Entry As HelpEntry)
        Try
            If Entry.IsEvent Then
                CustomEvent.Raise(Entry.EventType, Entry.EventData)
            Else
                EnterHelpPage(Entry)
            End If
        Catch ex As Exception
            Logger.Error(ex, "处理帮助项目点击时发生意外错误")
        End Try
    End Sub

    '自用分支：顶部长驻按钮，直接落到详情页
    '注意：MyButton 的 Click 是自建的 MouseButtonEventArgs，不能用 EventArgs，否则 XAML 绑定报「签名不兼容」
    Private Sub OpenServerGuide(sender As Object, e As MouseButtonEventArgs)
        EnterHelpPage("指南/服务器使用方法.json")
    End Sub
    Private Sub OpenNetGuide(sender As Object, e As MouseButtonEventArgs)
        EnterHelpPage("指南/联机使用方法.json")
    End Sub

    Public Shared Sub EnterHelpPage(Location As String)
        RunInThread(
        Sub()
            Try
                If Not HelpLoader.State = LoadState.Finished Then HelpLoader.WaitForExit(GetUuid)
                '自用分支：调用处传的是相对路径（如 "指南/服务器使用方法.json"），这里补成内置帮助目录下的绝对路径。
                '从帮助列表点进来的情况传的本来就是绝对路径，原样使用。
                Dim RealPath As String = Location
                Dim RelPath As String = If(Location, "").Replace("/", "\").TrimStart("\"c)
                If Not FileUtils.Exists(RealPath) Then RealPath = PathTemp & "Help\" & RelPath
                '第三层兜底：从已加载的条目里按结尾反查真实路径（兼容用户放在 PCL\Help 下的自定义帮助）
                If Not FileUtils.Exists(RealPath) AndAlso HelpLoader.Output IsNot Nothing Then
                    Dim Hit = HelpLoader.Output.FirstOrDefault(Function(x) x.RawPath.Replace("/", "\").EndsWith(RelPath, StringComparison.OrdinalIgnoreCase))
                    If Hit IsNot Nothing Then RealPath = Hit.RawPath
                End If
                If Not FileUtils.Exists(RealPath) Then
                    Logger.Info($"未找到帮助文件：{Location}（已尝试 {RealPath}）")
                    Hint($"没找到这篇指南：{If(Location, "")}，请到 更多 → 帮助 的列表里点开", HintType.Red)
                    Return
                End If
                Dim Entry As New HelpEntry(RealPath)
                RunInUi(
                Sub()
                    Dim FrmHelpDetail As New PageOtherHelpDetail
                    If FrmHelpDetail.Init(Entry) Then
                        FrmMain.PageChange(New FormMain.PageStackData With {.Page = FormMain.PageType.HelpDetail, .Additional = {Entry, FrmHelpDetail}})
                    Else
                        Logger.Warn("已取消进入帮助项目，这一般是由于 xaml 初始化失败，且用户在弹窗中手动放弃")
                    End If
                End Sub)
            Catch ex As Exception
                Logger.Error(ex, $"打开帮助失败：{Location}")
                Hint("打开指南失败了，请到 更多 → 帮助 的列表里点开其中一篇", HintType.Red)
            End Try
        End Sub)
    End Sub
    Public Shared Sub EnterHelpPage(Entry As HelpEntry)
        RunInThread(
        Sub()
            If Not HelpLoader.State = LoadState.Finished Then HelpLoader.WaitForExit(GetUuid)
            RunInUi(
            Sub()
                Dim FrmHelpDetail As New PageOtherHelpDetail
                If FrmHelpDetail.Init(Entry) Then
                    FrmMain.PageChange(New FormMain.PageStackData With {.Page = FormMain.PageType.HelpDetail, .Additional = {Entry, FrmHelpDetail}})
                Else
                    Logger.Warn("已取消进入帮助项目，这一般是由于 xaml 初始化失败，且用户在弹窗中手动放弃")
                End If
            End Sub)
        End Sub)
    End Sub

    ''' <summary>
    ''' 搜索帮助。
    ''' </summary>
    Public Sub SearchRun() Handles SearchBox.TextChanged
        If String.IsNullOrWhiteSpace(SearchBox.Text) Then
            '隐藏
            AniStart({
                 AaOpacity(PanSearch, -PanSearch.Opacity, 100),
                 AaCode(
                 Sub()
                     PanSearch.Height = 0
                     PanSearch.Visibility = Visibility.Collapsed
                     PanList.Visibility = Visibility.Visible
                     CardQuickGuide.Visibility = Visibility.Visible
                 End Sub,, True),
                 AaOpacity(PanList, 1 - PanList.Opacity, 150, 30)
            }, "FrmOtherHelp Search Switch")
        Else
            '构造请求
            Dim QueryList As New List(Of SearchEntry(Of HelpEntry))
            For Each Entry As HelpEntry In HelpLoader.Output
                If Not Entry.ShowInSearch OrElse (BuildType = BuildTypes.Release AndAlso Not Entry.ShowInPublic) Then Continue For
                If Not Entry.ShowInSearch OrElse (BuildType <> BuildTypes.Release AndAlso Not Entry.ShowInSnapshot) Then Continue For
                QueryList.Add(New SearchEntry(Of HelpEntry) With {
                    .Item = Entry,
                    .SearchSource = New List(Of SearchSource) From {
                        New SearchSource(Entry.Title, 1),
                        New SearchSource(Entry.Desc, 0.5),
                        New SearchSource(Entry.Search, 1.5)
                    }
                })
                'New SearchSource(If(Entry.IsEvent, If(Entry.EventData, ""), Entry.XamlContent), 0.2)
            Next
            '进行搜索，构造列表
            Dim SearchResult = Search(QueryList, SearchBox.Text, MaxBlurCount:=5, MinBlurSimilarity:=0.08)
            PanSearchList.Children.Clear()
            If Not SearchResult.Any() Then
                PanSearch.Title = "无搜索结果"
                PanSearchList.Visibility = Visibility.Collapsed
            Else
                PanSearch.Title = "搜索结果"
                For Each Result In SearchResult
                    Dim Item = Result.Item.ToListItem
                    If ModeDebug Then Item.Info = If(Result.AbsoluteRight, "完全匹配，", "") & "相似度：" & Math.Round(Result.Similarity, 3) & "，" & Item.Info
                    PanSearchList.Children.Add(Item)
                Next
                PanSearchList.Visibility = Visibility.Visible
            End If
            '显示
            AniStart({
                 AaOpacity(PanList, -PanList.Opacity, 100),
                 AaCode(
                 Sub()
                     PanList.Visibility = Visibility.Collapsed
                     '搜索时把顶部的常用指南收起来，把结果留在第一屏
                     CardQuickGuide.Visibility = Visibility.Collapsed
                     PanSearch.Visibility = Visibility.Visible
                     PanSearch.TriggerForceResize()
                 End Sub,, True),
                 AaOpacity(PanSearch, 1 - PanSearch.Opacity, 150, 30)
            }, "FrmOtherHelp Search Switch")
        End If
    End Sub

    Public Sub Refresh() Implements IRefreshable.Refresh
        PageOtherLeft.RefreshHelp()
    End Sub
End Class
