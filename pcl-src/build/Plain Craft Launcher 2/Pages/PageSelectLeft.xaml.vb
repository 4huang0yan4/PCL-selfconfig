Public Class PageSelectLeft
    Implements IRefreshable

    Private Sub PageSelectLeft_Initialized(sender As Object, e As EventArgs) Handles Me.Initialized
        AddHandler McFolderListLoader.PreviewFinish, Sub() If FrmSelectLeft IsNot Nothing Then RunInUiWait(AddressOf McFolderListUI)
    End Sub
    Private IsFirstLoad As Boolean = True
    Private Sub PageSelectLeft_Loaded(sender As Object, e As RoutedEventArgs) Handles Me.Loaded
        If IsFirstLoad Then McFolderListUI() '若已经执行完成，触发首次加载
        IsFirstLoad = False
    End Sub
    Private Sub McFolderListUI()
        Try

            '确认数据有变化
            If McFolderListLast IsNot Nothing AndAlso McFolderListLast.SequenceEqual(McFolderList) Then
                Dim IsEqual As Boolean = True
                For i = 0 To McFolderListLast.Count - 1
                    If Not McFolderListLast(i).Equals(McFolderList(i)) Then
                        IsEqual = False
                        Exit For
                    End If
                Next
                If IsEqual Then Return
            End If
            McFolderListLast = McFolderList

            '创建 UI
            FrmSelectLeft.PanList.Children.Clear()

            '文件夹列表
            FrmSelectLeft.PanList.Children.Add(New TextBlock With {.Text = "文件夹列表", .Margin = New Thickness(13, 18, 5, 4), .Opacity = 0.6, .FontSize = 12})
            For Each Folder As McFolder In McFolderList.ToArray
                '添加控件
                Dim ContMenu As ContextMenu = Nothing
                Select Case Folder.Type
                    Case McFolder.Types.Vanilla
                        ContMenu = GetObjectFromXML(
                                <ContextMenu xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml" xmlns:local="clr-namespace:PCL;assembly=Plain Craft Launcher 2">
                                    <local:MyMenuItem x:Name="Rename" Header="重命名" Padding="0,2,0,0" Icon="F1 M 53.2929,21.2929L 54.7071,22.7071C 56.4645,24.4645 56.4645,27.3137 54.7071,29.0711L 52.2323,31.5459L 44.4541,23.7677L 46.9289,21.2929C 48.6863,19.5355 51.5355,19.5355 53.2929,21.2929 Z M 31.7262,52.052L 23.948,44.2738L 43.0399,25.182L 50.818,32.9601L 31.7262,52.052 Z M 23.2409,47.1023L 28.8977,52.7591L 21.0463,54.9537L 23.2409,47.1023 Z"/>
                                    <local:MyMenuItem x:Name="Open" Header="打开" Icon="F1 M 19,50L 28,34L 63,34L 54,50L 19,50 Z M 19,28.0001L 35,28C 36,25 37.4999,24.0001 37.4999,24.0001L 48.75,24C 49.3023,24 50,24.6977 50,25.25L 50,28L 54,28.0001L 54,32L 27,32L 19,46.4L 19,28.0001 Z"/>
                                    <local:MyMenuItem x:Name="Refresh" Header="刷新" Icon="F1 M 38,20.5833C 42.9908,20.5833 47.4912,22.6825 50.6667,26.046L 50.6667,17.4167L 55.4166,22.1667L 55.4167,34.8333L 42.75,34.8333L 38,30.0833L 46.8512,30.0833C 44.6768,27.6539 41.517,26.125 38,26.125C 31.9785,26.125 27.0037,30.6068 26.2296,36.4167L 20.6543,36.4167C 21.4543,27.5397 28.9148,20.5833 38,20.5833 Z M 38,49.875C 44.0215,49.875 48.9963,45.3932 49.7703,39.5833L 55.3457,39.5833C 54.5457,48.4603 47.0852,55.4167 38,55.4167C 33.0092,55.4167 28.5088,53.3175 25.3333,49.954L 25.3333,58.5833L 20.5833,53.8333L 20.5833,41.1667L 33.25,41.1667L 38,45.9167L 29.1487,45.9167C 31.3231,48.3461 34.483,49.875 38,49.875 Z"/>
                                    <local:MyMenuItem x:Name="Delete" Header="删除" Padding="0,0,0,2" Icon="F1 M 26.9166,22.1667L 37.9999,33.25L 49.0832,22.1668L 53.8332,26.9168L 42.7499,38L 53.8332,49.0834L 49.0833,53.8334L 37.9999,42.75L 26.9166,53.8334L 22.1666,49.0833L 33.25,38L 22.1667,26.9167L 26.9166,22.1667 Z "/>
                                </ContextMenu>
                        )
                    Case McFolder.Types.RenamedVanilla
                        ContMenu = GetObjectFromXML(
                                <ContextMenu xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml" xmlns:local="clr-namespace:PCL;assembly=Plain Craft Launcher 2">
                                    <local:MyMenuItem x:Name="Remove" Header="复原名称" Padding="0,2,0,0" Icon="F1 M 53.2929,21.2929L 54.7071,22.7071C 56.4645,24.4645 56.4645,27.3137 54.7071,29.0711L 52.2323,31.5459L 44.4541,23.7677L 46.9289,21.2929C 48.6863,19.5355 51.5355,19.5355 53.2929,21.2929 Z M 31.7262,52.052L 23.948,44.2738L 43.0399,25.182L 50.818,32.9601L 31.7262,52.052 Z M 23.2409,47.1023L 28.8977,52.7591L 21.0463,54.9537L 23.2409,47.1023 Z"/>
                                    <local:MyMenuItem x:Name="Rename" Header="重命名" Icon="F1 M 53.2929,21.2929L 54.7071,22.7071C 56.4645,24.4645 56.4645,27.3137 54.7071,29.0711L 52.2323,31.5459L 44.4541,23.7677L 46.9289,21.2929C 48.6863,19.5355 51.5355,19.5355 53.2929,21.2929 Z M 31.7262,52.052L 23.948,44.2738L 43.0399,25.182L 50.818,32.9601L 31.7262,52.052 Z M 23.2409,47.1023L 28.8977,52.7591L 21.0463,54.9537L 23.2409,47.1023 Z"/>
                                    <local:MyMenuItem x:Name="Open" Header="打开" Icon="F1 M 19,50L 28,34L 63,34L 54,50L 19,50 Z M 19,28.0001L 35,28C 36,25 37.4999,24.0001 37.4999,24.0001L 48.75,24C 49.3023,24 50,24.6977 50,25.25L 50,28L 54,28.0001L 54,32L 27,32L 19,46.4L 19,28.0001 Z"/>
                                    <local:MyMenuItem x:Name="Refresh" Header="刷新" Icon="F1 M 38,20.5833C 42.9908,20.5833 47.4912,22.6825 50.6667,26.046L 50.6667,17.4167L 55.4166,22.1667L 55.4167,34.8333L 42.75,34.8333L 38,30.0833L 46.8512,30.0833C 44.6768,27.6539 41.517,26.125 38,26.125C 31.9785,26.125 27.0037,30.6068 26.2296,36.4167L 20.6543,36.4167C 21.4543,27.5397 28.9148,20.5833 38,20.5833 Z M 38,49.875C 44.0215,49.875 48.9963,45.3932 49.7703,39.5833L 55.3457,39.5833C 54.5457,48.4603 47.0852,55.4167 38,55.4167C 33.0092,55.4167 28.5088,53.3175 25.3333,49.954L 25.3333,58.5833L 20.5833,53.8333L 20.5833,41.1667L 33.25,41.1667L 38,45.9167L 29.1487,45.9167C 31.3231,48.3461 34.483,49.875 38,49.875 Z"/>
                                    <local:MyMenuItem x:Name="Delete" Header="删除" Padding="0,0,0,2" Icon="F1 M 26.9166,22.1667L 37.9999,33.25L 49.0832,22.1668L 53.8332,26.9168L 42.7499,38L 53.8332,49.0834L 49.0833,53.8334L 37.9999,42.75L 26.9166,53.8334L 22.1666,49.0833L 33.25,38L 22.1667,26.9167L 26.9166,22.1667 Z "/>
                                </ContextMenu>
                        )
                    Case McFolder.Types.Custom
                        ContMenu = GetObjectFromXML(
                                <ContextMenu xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml" xmlns:local="clr-namespace:PCL;assembly=Plain Craft Launcher 2">
                                    <local:MyMenuItem x:Name="Rename" Header="重命名" Padding="0,2,0,0" Icon="F1 M 53.2929,21.2929L 54.7071,22.7071C 56.4645,24.4645 56.4645,27.3137 54.7071,29.0711L 52.2323,31.5459L 44.4541,23.7677L 46.9289,21.2929C 48.6863,19.5355 51.5355,19.5355 53.2929,21.2929 Z M 31.7262,52.052L 23.948,44.2738L 43.0399,25.182L 50.818,32.9601L 31.7262,52.052 Z M 23.2409,47.1023L 28.8977,52.7591L 21.0463,54.9537L 23.2409,47.1023 Z"/>
                                    <local:MyMenuItem x:Name="Open" Header="打开" Icon="F1 M 19,50L 28,34L 63,34L 54,50L 19,50 Z M 19,28.0001L 35,28C 36,25 37.4999,24.0001 37.4999,24.0001L 48.75,24C 49.3023,24 50,24.6977 50,25.25L 50,28L 54,28.0001L 54,32L 27,32L 19,46.4L 19,28.0001 Z"/>
                                    <local:MyMenuItem x:Name="Refresh" Header="刷新" Icon="F1 M 38,20.5833C 42.9908,20.5833 47.4912,22.6825 50.6667,26.046L 50.6667,17.4167L 55.4166,22.1667L 55.4167,34.8333L 42.75,34.8333L 38,30.0833L 46.8512,30.0833C 44.6768,27.6539 41.517,26.125 38,26.125C 31.9785,26.125 27.0037,30.6068 26.2296,36.4167L 20.6543,36.4167C 21.4543,27.5397 28.9148,20.5833 38,20.5833 Z M 38,49.875C 44.0215,49.875 48.9963,45.3932 49.7703,39.5833L 55.3457,39.5833C 54.5457,48.4603 47.0852,55.4167 38,55.4167C 33.0092,55.4167 28.5088,53.3175 25.3333,49.954L 25.3333,58.5833L 20.5833,53.8333L 20.5833,41.1667L 33.25,41.1667L 38,45.9167L 29.1487,45.9167C 31.3231,48.3461 34.483,49.875 38,49.875 Z"/>
                                    <local:MyMenuItem x:Name="Remove" Header="移出列表" Icon="F1 M 23.3428,25.205L 23.3805,25.4461C 23.9229,27.177 30.261,29.0992 38,29.0992C 45.7386,29.0992 52.0765,27.1771 52.6194,25.4463L 52.6571,25.205C 52.6571,23.3616 46.0949,21.3109 38,21.3109C 29.9051,21.3109 23.3428,23.3616 23.3428,25.205 Z M 23.3428,53.0204L 19.1571,26.2111C 19.0534,25.8817 19,25.5459 19,25.205C 19,20.9036 27.5066,17.4167 38,17.4167C 48.4934,17.4167 57,20.9036 57,25.205C 57,25.5459 56.9466,25.8818 56.8429,26.2112L 52.6571,53.0204L 52.5974,53.0204C 51.9241,56.1393 45.6457,58.5833 38,58.5833C 30.3543,58.5833 24.076,56.1393 23.4026,53.0204L 23.3428,53.0204 Z M 51.8228,30.5485C 48.3585,32.0537 43.4469,32.9933 38,32.9933C 32.5531,32.9933 27.6415,32.0537 24.1771,30.5484L 27.5988,52.464L 27.6857,52.464C 27.6857,53.3857 32.3036,54.6892 38,54.6892C 43.6964,54.6892 48.3143,53.3857 48.3143,52.464L 48.4011,52.464L 51.8228,30.5485 Z "/>
                                    <local:MyMenuItem x:Name="Delete" Header="删除" Padding="0,0,0,2" Icon="F1 M 26.9166,22.1667L 37.9999,33.25L 49.0832,22.1668L 53.8332,26.9168L 42.7499,38L 53.8332,49.0834L 49.0833,53.8334L 37.9999,42.75L 26.9166,53.8334L 22.1666,49.0833L 33.25,38L 22.1667,26.9167L 26.9166,22.1667 Z "/>
                                </ContextMenu>
                        )
                End Select
                If (Folder.Type = McFolder.Types.Vanilla OrElse Folder.Type = McFolder.Types.RenamedVanilla) AndAlso PathUtils.IsSame(Folder.Location, Paths.Base & ".minecraft\") AndAlso McFolderList.IsSingle Then CType(ContMenu.FindName("Delete"), MyMenuItem).Header = "清空"
                '注册事件
                If Not Folder.Type = McFolder.Types.Vanilla Then CType(ContMenu.FindName("Remove"), MyMenuItem).AddHandler(MyMenuItem.ClickEvent, New RoutedEventHandler(AddressOf FrmSelectLeft.Remove_Click))
                CType(ContMenu.FindName("Open"), MyMenuItem).AddHandler(MyMenuItem.ClickEvent, New RoutedEventHandler(AddressOf FrmSelectLeft.Open_Click))
                CType(ContMenu.FindName("Delete"), MyMenuItem).AddHandler(MyMenuItem.ClickEvent, New RoutedEventHandler(AddressOf FrmSelectLeft.Delete_Click))
                CType(ContMenu.FindName("Rename"), MyMenuItem).AddHandler(MyMenuItem.ClickEvent, New RoutedEventHandler(AddressOf FrmSelectLeft.Rename_Click))
                CType(ContMenu.FindName("Refresh"), MyMenuItem).AddHandler(MyMenuItem.ClickEvent, New RoutedEventHandler(AddressOf FrmSelectLeft.Refresh_Click))
                '构建框架与图表按钮
                Dim NewItem As New MyListItem With {.IsScaleAnimationEnabled = False, .Type = MyListItem.CheckType.RadioBox, .MinPaddingRight = 30, .Title = Folder.Name, .Info = Folder.Location, .Height = 40, .ContextMenu = ContMenu, .Tag = Folder}
                AddHandler NewItem.Changed, AddressOf FrmSelectLeft.Folder_Change
                Dim NewIconButton As New MyIconButton With {.Logo = Logo.IconButtonSetup, .LogoScale = 1.1}
                AddHandler NewIconButton.Click, Sub(sender, e)
                                                    ContMenu.PlacementTarget = NewItem
                                                    ContMenu.IsOpen = True
                                                End Sub
                NewItem.Buttons = {NewIconButton}
                FrmSelectLeft.PanList.Children.Add(NewItem)
                Logger.Info($"有效的 Minecraft 文件夹：{Folder.Name} > {Folder.Location}")
            Next

            '标题文本
            FrmSelectLeft.PanList.Children.Add(New TextBlock With {.Text = "添加或导入", .Margin = New Thickness(13, 18, 5, 4), .Opacity = 0.6, .FontSize = 12})

            '确认创建按钮状态
            If Not DirectoryUtils.Exists(Paths.Base & ".minecraft\") Then
                Dim ItemCreate As New MyListItem With {.IsScaleAnimationEnabled = False, .Type = MyListItem.CheckType.Clickable, .Title = "新建 .minecraft 文件夹", .Height = 34,
                    .ToolTip = "在 PCL 当前所在文件夹下创建新的 .minecraft 文件夹",
                    .LogoScale = 0.9,
                    .Logo = "M103.331925 384.978025l25.805736 0L129.137661 161.847132c0-18.313088 14.905478-33.718963 33.718963-33.718963l0.969071 0 253.006318 0c10.82044 0 20.218484 4.797259 26.500561 12.257162l117.579929 126.753869 297.819966 0c18.297738 0 33.736359 15.179724 33.736359 33.977859l0 0.952698 0 82.909292 25.547863 0c18.538215 0 34.187637 15.179724 34.187637 33.977859 0 2.163269-0.469698 3.617387-0.469698 5.539156l-54.437843 432.971086c-1.210571 10.382465-7.007601 19.056008-14.968923 24.352641-6.249331 5.765307-14.680351 9.624195-23.595394 9.624195l-0.969071 0-694.906773 0c-9.155521 0-17.344017-3.858888-23.626094-9.155521-8.67252-5.765307-14.453177-14.939247-15.389502-25.758664L69.597613 423.040922c-2.165316-18.313088 10.868535-35.414581 29.665647-38.062897L103.331925 384.978025 103.331925 384.978025zM196.576609 384.978025 196.576609 384.978025l627.938546 0 0-49.625234L546.461371 335.352791l0 0c-9.400091 0-18.329461-4.117784-25.048489-11.110035L402.363486 196.067514 196.576609 196.067514 196.576609 384.978025 196.576609 384.978025zM879.469767 452.916347 879.469767 452.916347l-20.267603 0-0.469698 0-0.969071 0-694.906773 0-0.984421 0-20.218484 0 45.781696 366.728382 646.218888 0L879.469767 452.916347 879.469767 452.916347z"}
                FrmSelectLeft.PanList.Children.Add(ItemCreate)
                AddHandler ItemCreate.Click, AddressOf FrmSelectLeft.Create_Click
            End If

            '添加按钮
            Dim ItemAdd As New MyListItem With {.IsScaleAnimationEnabled = False, .Type = MyListItem.CheckType.Clickable, .Title = "添加已有文件夹", .Height = 34,
                .ToolTip = "将一个已有的 Minecraft 文件夹添加到列表",
                .Logo = "M512.277 954.412c-118.89 0-230.659-46.078-314.73-129.73S67.12 629.666 67.12 511.222s46.327-229.744 130.398-313.427 195.82-129.73 314.73-129.73 230.659 46.078 314.72 129.73S957.397 392.81 957.397 511.183 911.078 740.96 826.97 824.642s-195.8 129.77-314.692 129.77z m0-822.784c-101.972 0-197.809 39.494-269.865 111.222s-111.7 166.997-111.7 268.373 39.653 196.695 111.67 268.335S410.246 890.78 512.248 890.78s197.809-39.484 269.865-111.222 111.7-166.998 111.67-268.374c-0.03-101.375-39.654-196.665-111.67-268.303S614.22 131.628 512.277 131.628z m222.585 347.8H544.073V288.64c-0.76-17.561-15.613-31.18-33.173-30.419-16.495 0.714-29.704 13.924-30.419 30.419v190.787H289.703c-17.56 0.761-31.179 15.614-30.419 33.174 0.715 16.494 13.924 29.703 30.42 30.418H480.48v190.788c0.761 17.56 15.614 31.179 33.174 30.419 16.494-0.715 29.703-13.925 30.418-30.42V543.02h190.788c17.56 0.762 32.413-12.857 33.173-30.418 0.762-17.561-12.858-32.414-30.419-33.174a31.683 31.683 0 0 0-2.753 0z"}
            FrmSelectLeft.PanList.Children.Add(ItemAdd)
            AddHandler ItemAdd.Click, AddressOf FrmSelectLeft.Add_Click

            '扫描按钮
            Dim ItemScan As New MyListItem With {.IsScaleAnimationEnabled = False, .Type = MyListItem.CheckType.Clickable, .Title = "扫描其他启动器", .Height = 34,
                .ToolTip = "自动查找官方启动器、HMCL、其他 PCL 等常见的 Minecraft 文件夹，确认后一次性添加",
                .LogoScale = 0.9,
                .Logo = Logo.IconButtonRefresh}
            FrmSelectLeft.PanList.Children.Add(ItemScan)
            AddHandler ItemScan.Click, AddressOf FrmSelectLeft.Scan_Click

            '安装按钮
            Dim ItemInstall As New MyListItem With {.IsScaleAnimationEnabled = False, .Type = MyListItem.CheckType.Clickable, .Title = "导入整合包", .Height = 34,
                .ToolTip = "在当前选择的 Minecraft 文件夹下安装整合包",
                .Logo = "M512 40.96C249.344 40.96 35.84 252.416 35.84 512s213.504 471.04 476.16 471.04c103.424 0 202.752-33.28 286.72-96.256l1.536-1.536c5.12-5.632 7.68-12.8 7.68-19.968 0-16.896-13.824-30.208-30.72-30.208-7.68 0-15.36 2.56-20.992 7.68h-0.512c-71.68 52.224-155.648 79.36-243.712 79.36-227.328 0-412.16-182.784-412.16-407.552 0-224.768 184.832-407.552 412.16-407.552s412.16 182.784 412.16 407.552c0 68.608-15.872 132.608-46.592 190.464-0.512 1.024-1.024 2.048-1.024 3.072-0.512 2.048-1.536 4.608-1.536 8.192 0 16.896 13.824 30.208 30.72 30.208 12.288 0 23.04-7.168 28.16-18.432 35.84-68.608 53.76-141.312 53.76-216.064 0.512-259.584-212.992-471.04-475.648-471.04z M812.032 483.328c-31.744-20.992-71.68 1.536-78.848 6.144-1.024 0.512-104.448 61.44-128 74.752-8.192 4.608-22.528-0.512-27.136-4.096-31.232-36.352-54.272-70.656-68.608-102.4-13.312-29.184 0.512-41.472 3.072-43.52 7.168-4.608 114.688-68.608 143.36-83.456 24.064-12.288 40.96-25.088 46.08-45.056 3.072-13.312 0-27.136-9.216-39.936-22.016-31.744-172.544-84.992-311.296-3.584-157.184 91.648-152.064 242.688-150.528 292.352v9.216c0 18.944-12.8 37.376-14.848 40.448l-20.992 21.504c-6.144 6.144-9.216 13.824-9.216 22.528 0 8.704 3.584 16.384 9.728 22.528 12.8 12.288 32.768 11.776 45.056-0.512l22.528-23.552 0.512-0.512c3.072-3.584 30.208-38.4 30.208-81.92l-0.512-11.264c-1.536-44.544-5.632-162.816 119.296-235.52 88.064-51.2 173.056-32.256 208.896-19.968-36.864 19.456-143.36 83.456-144.896 84.48-22.016 14.336-55.808 58.88-26.112 122.88 17.408 37.376 43.52 76.8 80.896 120.32 14.336 17.408 62.976 37.376 103.424 15.36 24.576-13.312 125.44-73.216 130.048-75.776 2.048-1.024 4.608-2.56 7.68-3.584 0 2.56-0.512 6.144-1.024 10.752-5.632 35.84-35.328 155.136-191.488 181.76-49.664 8.704-89.6 3.584-121.856-0.512h-0.512c-37.888-4.608-73.216-9.216-101.888 14.336-31.232 26.112-40.96 34.304-35.84 54.272 3.584 14.336 16.384 24.064 30.72 24.064 2.56 0 5.12-0.512 7.68-1.024 6.656-1.536 12.8-5.632 16.896-10.752 2.048-2.048 7.68-6.656 20.992-18.432 6.656-5.632 25.088-3.584 52.736 0 34.816 4.608 81.92 10.24 141.312 0.512 157.184-26.624 228.864-138.752 243.2-234.496 7.68-38.912 0-64.512-21.504-78.336z"}
            FrmSelectLeft.PanList.Children.Add(ItemInstall)
            AddHandler ItemInstall.Click, AddressOf ModpackInstall

            '边距
            FrmSelectLeft.PanList.Children.Add(New FrameworkElement With {.Height = 10, .IsHitTestVisible = False})

            '确认勾选状态
            For i = 0 To McFolderList.Count - 1
                If PathUtils.IsSame(McFolderList(i).Location, McFolderSelected) Then
                    CType(FrmSelectLeft.PanList.Children(i + 1), MyListItem).Checked = True '去掉第一个标题
                    Return
                End If
            Next
            If Not McFolderList.Any() Then
                Throw New ArgumentNullException("没有可用的 Minecraft 文件夹")
            Else
                McFolderSelected = McFolderList.First.Location
                CType(FrmSelectLeft.PanList.Children(1), MyListItem).Checked = True
            End If

        Catch ex As Exception
            Logger.Error(ex, "构建 Minecraft 文件夹列表 UI 出错")
        Finally
            LoaderFolderRun(McInstanceListLoader, McFolderSelected, LoaderFolderRunType.RunOnUpdated, MaxDepth:=1, ExtraPath:="versions\") '刷新版本列表
        End Try
    End Sub
    Private McFolderListLast As List(Of McFolder)

    '添加文件夹
    Public Sub Add_Click()
        Dim NewFolder As String = ""
        '下载或版本修改期间禁止更改游戏文件夹
        If McInstanceLock.IsAnyLocked OrElse HasDownloadingTask() Then
            Hint("请在其他任务完成后，再添加游戏文件夹！", HintType.Red)
            Return
        End If
        Try
            '获取输入
            NewFolder = Dialogs.SelectFolder("选择 Minecraft 文件夹", False).FirstOrDefault
            If NewFolder Is Nothing Then Return
            If NewFolder.Contains("!") OrElse NewFolder.Contains(";") Then Hint("Minecraft 文件夹路径中不能含有感叹号或分号！", HintType.Red) : Return
            '要求输入显示名称
            Dim DefaultName = PathUtils.GetLastPart(NewFolder)
            If DefaultName = ".minecraft" Then
                Dim ParentFolder = PathUtils.RemoveLastPart(NewFolder)
                DefaultName = If(ParentFolder.EndsWithF(":"), "", PathUtils.GetLastPart(ParentFolder))
            End If
            If DefaultName.Length > 40 Then DefaultName = DefaultName.Substring(0, 39)
            Dim NewName As String = MyMsgBoxInput("输入显示名称", "输入该文件夹在左边栏列表中显示的名称。", DefaultName,
                                              New ObjectModel.Collection(Of Validate) From {New ValidateNullOrWhiteSpace, New ValidateLength(1, 30), New ValidateExcept({">", "|"})})
            If String.IsNullOrWhiteSpace(NewName) Then Return
            '添加文件夹
            AddFolder(NewFolder, NewName, True)
        Catch ex As Exception
            Logger.Error(ex, $"添加文件夹失败（{NewFolder}）")
        End Try
    End Sub

    '扫描其他启动器的 Minecraft 文件夹
    Public Sub Scan_Click()
        '下载或版本修改期间禁止更改游戏文件夹（与「添加已有文件夹」一致）
        If McInstanceLock.IsAnyLocked OrElse HasDownloadingTask() Then
            Hint("请在其他任务完成后，再扫描游戏文件夹！", HintType.Red)
            Return
        End If
        Hint("正在扫描常见的 Minecraft 文件夹…")
        RunInNewThread(
        Sub()
            Dim Found As List(Of Tuple(Of String, Boolean))
            Try
                Found = ScanMcFolders()
            Catch ex As Exception
                Logger.Error(ex, "扫描 Minecraft 文件夹失败")
                RunInUi(Sub() Hint("扫描失败：" & ex.Message, HintType.Red, False))
                Return
            End Try
            '去掉已经添加过的
            Dim Existing As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)
            For Each Folder In McFolderList
                If Folder Is Nothing Then Continue For
                If String.IsNullOrEmpty(Folder.Location) Then Continue For
                Existing.Add(Folder.Location.TrimEnd("\"c))
            Next
            '美西螈目录即使已经添加过也保留：它可能是要重新转换（同步那边的改动）
            Found = Found.Where(Function(f) f.Item2 OrElse Not Existing.Contains(f.Item1.TrimEnd("\"c))).ToList()
            RunInUi(
            Sub()
                If Not Found.Any() Then
                    Hint("没有发现新的 Minecraft 文件夹。", HintType.Blue)
                    Return
                End If
                Dim Text As New System.Text.StringBuilder
                Text.AppendLine($"在常见位置发现 {Found.Count} 个 Minecraft 文件夹：")
                Text.AppendLine()
                Dim NeedConvert As Boolean = False
                Dim ConvertedCount As Integer = 0
                For Each Hit In Found
                    If Hit.Item2 Then
                        NeedConvert = True
                        Dim Total As Integer = CountAxolotlProfiles(Hit.Item1)
                        Dim Done As Integer = CountConvertedProfiles(Hit.Item1)
                        Dim Outdated As Integer = CountOutdatedProfiles(Hit.Item1)
                        ConvertedCount += Done
                        Text.AppendLine($"  · {Hit.Item1}（美西螈，{Total} 个实例" &
                                        If(Done > 0, $"，其中 {Done} 个之前转换过", "") &
                                        If(Outdated > 0, $"，{Outdated} 个的 mod 数量对不上，建议重新转换", "") & "）")
                    Else
                        Text.AppendLine($"  · {Hit.Item1}（{GetVersionCount(Hit.Item1)} 个版本）")
                    End If
                Next
                Text.AppendLine()
                Text.AppendLine("添加后不会移动或复制任何文件，只是把它列进左边栏。")
                If NeedConvert Then
                    Text.AppendLine()
                    Text.AppendLine("美西螈的实例会在它自己的目录里复制一份成 PCL 能读的格式（版本 json、jar、存档、Mod 一并带上），")
                    Text.AppendLine("原数据保持不变，之后两边各自独立；libraries 和 assets 只建目录联接，不占额外空间。")
                End If
                If MyMsgBox(Text.ToString.TrimEnd, "扫描结果", "全部添加", "取消") <> 1 Then Return
                '已转换过的可以重转一次，把美西螈那边新加的存档/Mod 同步过来
                Dim Overwrite As Boolean = False
                If ConvertedCount > 0 Then
                    If MyMsgBox($"其中 {ConvertedCount} 个实例之前已经转换过。" & vbCrLf & vbCrLf &
                                "要重新转换吗？" & vbCrLf &
                                "重新转换会用美西螈那边的当前内容覆盖 PCL 这边的同名实例 —— " &
                                "你在 PCL 里新产生的存档、加的 Mod 会被替换掉。" & vbCrLf & vbCrLf &
                                "选「保持不动」就只添加新发现的文件夹，已有的不动。",
                                "已有转换结果", "重新转换", "保持不动", IsWarn:=True) = 1 Then
                        Overwrite = True
                    End If
                End If
                '转换可能要复制文件，放到后台线程
                RunInNewThread(Sub()
                    Dim Folders As New List(Of String)(Settings.Get(Of String)("LaunchFolders").Split("|"))
                    Dim Added As Integer = 0
                    Dim LastPath As String = ""
                    Dim Errors As New List(Of String)
                    Dim Warnings As New List(Of String)
                    For Each Hit In Found
                        Dim Target As String = Hit.Item1
                        If Hit.Item2 Then
                            Try
                                Dim Warn As String = ConvertAxolotlFolder(Target, Overwrite)
                                If Warn <> "" Then Warnings.Add(Warn)
                            Catch ex As Exception
                                Logger.Error(ex, "转换美西螈实例失败：" & Target)
                                Errors.Add(IO.Path.GetFileName(Target.TrimEnd("\"c)) & "：" & ex.Message)
                                Continue For
                            End Try
                        End If
                        Dim FullPath As String = PathUtils.AddSlashSuffix(Target)
                        If Not CheckPermission(FullPath) Then Continue For
                        '同一路径已经存在就跳过
                        If Folders.Any(Function(x) x <> "" AndAlso x.Contains(">") AndAlso
                                       PathUtils.IsSame(x.Split(">")(1), FullPath)) Then Continue For
                        Folders.Add(SuggestFolderName(Target) & ">" & FullPath)
                        LastPath = FullPath
                        Added += 1
                    Next
                    If Added = 0 Then
                        RunInUi(Sub() Hint(If(Errors.Any, "添加失败：" & Errors.Join(vbCrLf), "这些文件夹已经都在列表里了。"), HintType.Blue, False))
                        Return
                    End If
                    Settings.Set("LaunchFolders", Folders.Join("|"))
                    RunInUi(Sub()
                        McFolderSelected = LastPath
                        McFolderListLoader.Start(IsForceRestart:=True)
                        Hint($"已添加 {Added} 个 Minecraft 文件夹。" & If(Errors.Any, vbCrLf & "其中部分失败：" & vbCrLf & Errors.Join(vbCrLf), "") & If(Warnings.Any, vbCrLf & Warnings.Join(vbCrLf), ""), HintType.Green)
                    End Sub)
                End Sub, "Convert And Add Folders")
            End Sub)
        End Sub, "Scan Minecraft Folders")
    End Sub

    ''' <summary>扫描常见位置下的 .minecraft 文件夹（含其他启动器的安装目录）。
    ''' 返回「路径, 是否为美西螈数据目录」。</summary>
    Private Shared Function ScanMcFolders() As List(Of Tuple(Of String, Boolean))
        Dim Result As New List(Of Tuple(Of String, Boolean))
        Dim Seen As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)
        Dim Candidates As New List(Of String)
        '官方启动器与用户目录下的固定位置
        Dim AppDataPath As String = Environment.GetEnvironmentVariable("APPDATA")
        If Not String.IsNullOrWhiteSpace(AppDataPath) Then Candidates.Add(IO.Path.Combine(AppDataPath, ".minecraft"))
        Dim UserProfilePath As String = Environment.GetEnvironmentVariable("USERPROFILE")
        If Not String.IsNullOrWhiteSpace(UserProfilePath) Then Candidates.Add(IO.Path.Combine(UserProfilePath, ".minecraft"))
        '每个本地固定盘的根目录
        Dim FixedRoots As New List(Of String)
        Try
            For Each Drive As IO.DriveInfo In IO.DriveInfo.GetDrives()
                If Drive.DriveType <> IO.DriveType.Fixed OrElse Not Drive.IsReady Then Continue For
                FixedRoots.Add(Drive.RootDirectory.FullName)
                Candidates.Add(IO.Path.Combine(Drive.RootDirectory.FullName, ".minecraft"))
            Next
        Catch ex As Exception
            Logger.Warn(ex, "枚举磁盘失败")
        End Try
        For Each Candidate In Candidates
            If IsMcFolder(Candidate) AndAlso Seen.Add(Candidate) Then Result.Add(Tuple.Create(Candidate, False))
        Next
        '再往盘里找三层，覆盖 D:\ruanjian\youxi\pcl2ziyong\.minecraft 这类位置
        '跳过系统目录，否则 C 盘会扫出几万个目录
        Dim SkipNames As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase) From {
            "windows", "program files", "program files (x86)", "programdata", "$recycle.bin",
            "system volume information", "windowsapps", "perflogs", "recovery", "msocache",
            "appdata", "intel", "amd", "nvidia", "drivers", "node_modules", ".git", ".cache"}
        For Each Root In FixedRoots
            Dim LevelDirs As New List(Of String) From {Root}
            For Depth = 1 To 3
                Dim ChildLevel As New List(Of String)
                For Each ScanDir In LevelDirs
                    Dim ChildDirs As String() = {}
                    Try
                        ChildDirs = IO.Directory.GetDirectories(ScanDir)
                    Catch
                        Continue For '无权限就跳过这一支
                    End Try
                    For Each SubDir In ChildDirs
                        Dim SubName As String = IO.Path.GetFileName(SubDir)
                        If SubName.Equals(".minecraft", StringComparison.OrdinalIgnoreCase) Then
                            '命中后不再深入，避免把 .minecraft 里的目录当成搜索路径
                            If IsMcFolder(SubDir) AndAlso Seen.Add(SubDir) Then Result.Add(Tuple.Create(SubDir, False))
                            Continue For
                        End If
                        If SkipNames.Contains(SubName) Then Continue For
                        '美西螈这类启动器：版本在 meta\versions，实例在 profiles，目录名也不固定
                        If IsAxolotlFolder(SubDir) Then
                            If Seen.Add(SubDir) Then Result.Add(Tuple.Create(SubDir, True))
                            Continue For
                        End If
                        ChildLevel.Add(SubDir)
                    Next
                Next
                LevelDirs = ChildLevel
                If Not LevelDirs.Any() Then Exit For
            Next
        Next
        Return Result
    End Function

    ' ── 美西螈（Axolotl）数据目录 ───────────────────────────
    ''' <summary>是否为美西螈的数据目录：版本放 meta\versions，实例放 profiles。</summary>
    Private Shared Function IsAxolotlFolder(FolderPath As String) As Boolean
        Try
            If Not Directory.Exists(FolderPath) Then Return False
            Return Directory.Exists(IO.Path.Combine(FolderPath, "meta\versions")) AndAlso
                   Directory.Exists(IO.Path.Combine(FolderPath, "profiles"))
        Catch
            Return False
        End Try
    End Function

    Private Shared Function CountAxolotlProfiles(FolderPath As String) As Integer
        Try
            Return Directory.GetDirectories(IO.Path.Combine(FolderPath, "profiles")).Length
        Catch
            Return 0
        End Try
    End Function

    ''' <summary>
    ''' 已转换、但美西螈那边的 mods 数量与 PCL 副本对不上的实例数（= 该重新转换了）。
    ''' 比目录时间可靠：PCL 侧只要启动过游戏就会刷新目录时间，用时间判会漏判。
    ''' </summary>
    Private Shared Function CountOutdatedProfiles(Root As String) As Integer
        Try
            Dim VersionsPath As String = IO.Path.Combine(Root, "versions")
            If Not Directory.Exists(VersionsPath) Then Return 0
            Dim Count As Integer = 0
            For Each ProfileDir In DirectoryUtils.EnumerateDirectories(IO.Path.Combine(Root, "profiles"), False)
                Dim Dst As String = IO.Path.Combine(VersionsPath, IO.Path.GetFileName(ProfileDir))
                If Not Directory.Exists(Dst) Then Continue For
                If CountModJars(ProfileDir) <> CountModJars(Dst) Then Count += 1
            Next
            Return Count
        Catch
            Return 0
        End Try
    End Function

    Private Shared Function CountModJars(Dir As String) As Integer
        Try
            Dim Mods As String = IO.Path.Combine(Dir, "mods")
            If Not Directory.Exists(Mods) Then Return 0
            Return IO.Directory.GetFiles(Mods, "*.jar").Length
        Catch
            Return 0
        End Try
    End Function

    ''' <summary>已转换过的实例数（versions 下已有同名目录）。</summary>
    Private Shared Function CountConvertedProfiles(Root As String) As Integer
        Try
            Dim VersionsPath As String = IO.Path.Combine(Root, "versions")
            If Not Directory.Exists(VersionsPath) Then Return 0
            Dim Count As Integer = 0
            For Each ProfileDir In DirectoryUtils.EnumerateDirectories(IO.Path.Combine(Root, "profiles"), False)
                If Directory.Exists(IO.Path.Combine(VersionsPath, IO.Path.GetFileName(ProfileDir))) Then Count += 1
            Next
            Return Count
        Catch
            Return 0
        End Try
    End Function

    ''' <summary>
    ''' 把美西螈的数据目录转成 PCL 能读的结构：在根目录下建 versions，把每个 profiles 实例复制一份进去。
    ''' libraries / assets 只建目录联接指回 meta 下，不复制那几百 MB。
    ''' Overwrite=False 时跳过已转换的实例，重复扫描不会反复复制；True 时先删掉 PCL 那份再重建。
    ''' </summary>
    Private Shared Function ConvertAxolotlFolder(Root As String, Overwrite As Boolean) As String
        Dim VersionsPath As String = IO.Path.Combine(Root, "versions")
        DirectoryUtils.Create(VersionsPath)
        Dim LinkFailed As Boolean = False
        If Not CreateJunction(IO.Path.Combine(Root, "libraries"), IO.Path.Combine(Root, "meta\libraries")) Then LinkFailed = True
        If Not CreateJunction(IO.Path.Combine(Root, "assets"), IO.Path.Combine(Root, "meta\assets")) Then LinkFailed = True
        For Each ProfileDir In DirectoryUtils.EnumerateDirectories(IO.Path.Combine(Root, "profiles"), False)
            Dim Name As String = IO.Path.GetFileName(ProfileDir)
            Dim Dst As String = IO.Path.Combine(VersionsPath, Name)
            Dim GameVer As String = ReadAxolotlGameVersion(ProfileDir)
            If GameVer = "" Then GameVer = Name.BeforeFirst("-").Trim
            Dim SrcJson As String = IO.Path.Combine(Root, "meta\versions\" & GameVer & "\" & GameVer & ".json")
            '先确认源文件在，再动 PCL 这边的副本，否则删完转换失败会连旧的那份也没了
            If Not FileUtils.Exists(SrcJson) Then
                Logger.Warn($"美西螈实例 {Name} 找不到版本 {GameVer} 的 json，跳过转换")
                Continue For
            End If
            If DirectoryUtils.Exists(Dst) Then
                If Not Overwrite Then Continue For '已经转换过，保持原样
                '只删 PCL 这边生成的副本，profiles 里的原始数据不动。走回收站，后悔还能捞
                DirectoryUtils.Delete(Dst, True)
            End If
            DirectoryUtils.Create(Dst)
            '版本 json：id 要改成实例名，否则 PCL 里显示的名字对不上
            Dim Tok = FileUtils.ReadAsJson(SrcJson)
            If Tok Is Nothing OrElse Not Tok.Type = Newtonsoft.Json.Linq.JTokenType.Object Then
                Logger.Warn($"美西螈实例 {Name} 的 json 不是对象，跳过转换")
                Continue For
            End If
            Dim J As Newtonsoft.Json.Linq.JObject = CType(Tok, Newtonsoft.Json.Linq.JObject)
            J("id") = Name
            FileUtils.Write(IO.Path.Combine(Dst, Name & ".json"), J.ToString(Newtonsoft.Json.Formatting.None),
                            encoding:=New System.Text.UTF8Encoding(False))
            Dim SrcJar As String = IO.Path.Combine(Root, "meta\versions\" & GameVer & "\" & GameVer & ".jar")
            If FileUtils.Exists(SrcJar) Then FileUtils.Copy(SrcJar, IO.Path.Combine(Dst, Name & ".jar"))
            '游戏数据（日志、崩溃报告不要，体积大且没意义）
            For Each SubName In {"saves", "mods", "config", "defaultconfigs", "resourcepacks", "shaderpacks", "datapacks", "schematics"}
                Dim SrcSub As String = IO.Path.Combine(ProfileDir, SubName)
                If DirectoryUtils.Exists(SrcSub) Then DirectoryUtils.Copy(SrcSub, IO.Path.Combine(Dst, SubName))
            Next
            For Each FileName In {"options.txt", "servers.dat"}
                Dim SrcFile As String = IO.Path.Combine(ProfileDir, FileName)
                If FileUtils.Exists(SrcFile) Then FileUtils.Copy(SrcFile, IO.Path.Combine(Dst, FileName))
            Next
            Logger.Info($"已转换美西螈实例：{Name}（{GameVer}）")
        Next
        '联接建不起来时，启动会缺库和资源，提示用户去补全而不是直接失败
        If LinkFailed Then Return "libraries/assets 没能建立目录联接，首次启动可能需要用「补全文件」下载缺失内容。"
        Return ""
    End Function

    ''' <summary>从 axolotl_config.json 里取实例对应的游戏版本号。</summary>
    Private Shared Function ReadAxolotlGameVersion(ProfileDir As String) As String
        Try
            Dim Cfg As String = IO.Path.Combine(ProfileDir, "axolotl_config.json")
            If Not FileUtils.Exists(Cfg) Then Return ""
            Dim Tok = FileUtils.ReadAsJson(Cfg)
            If Tok Is Nothing Then Return ""
            Return If(Tok("content_set")?("game_version")?.ToString, "").Trim
        Catch ex As Exception
            Logger.Warn(ex, "读取美西螈实例配置失败：" & ProfileDir)
            Return ""
        End Try
    End Function

    ''' <summary>建目录联接（目录联接不需要管理员权限）。已存在就跳过，返回是否可用。</summary>
    Private Shared Function CreateJunction(LinkPath As String, TargetPath As String) As Boolean
        Try
            If IO.Directory.Exists(LinkPath) OrElse IO.File.Exists(LinkPath) Then Return True
            If Not IO.Directory.Exists(TargetPath) Then Return True '没有可链接的源，交给 PCL 补全
            Dim P As System.Diagnostics.Process = System.Diagnostics.Process.Start(
                New System.Diagnostics.ProcessStartInfo("cmd.exe", $"/c mklink /J ""{LinkPath}"" ""{TargetPath}""") With {
                    .UseShellExecute = False, .CreateNoWindow = True, .RedirectStandardOutput = True, .RedirectStandardError = True})
            P.WaitForExit(10000)
            If P.ExitCode = 0 Then
                Logger.Info($"已建立目录联接：{LinkPath} → {TargetPath}")
                Return True
            End If
            Logger.Warn($"建立目录联接失败（退出码 {P.ExitCode}）：{LinkPath} → {TargetPath}")
            Return False
        Catch ex As Exception
            Logger.Warn(ex, "建立目录联接失败：" & LinkPath)
            Return False
        End Try
    End Function

    ''' <summary>判断是否为可用的 Minecraft 文件夹（versions 下至少有一个版本）。</summary>
    Private Shared Function IsMcFolder(FolderPath As String) As Boolean
        Try
            If Not Directory.Exists(FolderPath) Then Return False
            Dim VersionsPath As String = IO.Path.Combine(FolderPath, "versions")
            If Not Directory.Exists(VersionsPath) Then Return False
            Return Directory.GetDirectories(VersionsPath).Length > 0
        Catch
            Return False
        End Try
    End Function

    Private Shared Function GetVersionCount(FolderPath As String) As Integer
        Try
            Return Directory.GetDirectories(IO.Path.Combine(FolderPath, "versions")).Length
        Catch
            Return 0
        End Try
    End Function

    ''' <summary>给扫描到的文件夹起一个默认显示名（与「添加已有文件夹」的规则一致）。</summary>
    Private Shared Function SuggestFolderName(FolderPath As String) As String
        Dim Cleaned As String = FolderPath.TrimEnd("\"c)
        Dim Name As String = PathUtils.GetLastPart(Cleaned)
        If Name.Equals(".minecraft", StringComparison.OrdinalIgnoreCase) Then
            Dim ParentFolder As String = PathUtils.RemoveLastPart(Cleaned)
            Name = If(ParentFolder.EndsWithF(":"), ".minecraft", PathUtils.GetLastPart(ParentFolder))
        End If
        If Name.Length > 40 Then Name = Name.Substring(0, 39)
        Return Name
    End Function

    ''' <summary>
    ''' 将指定文件夹添加到 Minecraft 文件夹列表，并选中它。
    ''' </summary>
    Public Shared Sub AddFolder(FolderPath As String, DisplayName As String, ShowHint As Boolean)
        RunInThread(
        Sub()
            Try
                FolderPath = PathUtils.AddSlashSuffix(FolderPath)
                '检查文件夹权限
                If Not CheckPermission(FolderPath) Then
                    If ShowHint Then
                        Hint("添加文件夹失败：PCL 没有访问该文件夹的权限！", HintType.Red)
                        Return
                    Else
                        Throw New Exception("PCL 没有访问文件夹的权限：" & FolderPath)
                    End If
                End If
                '检查实际的 Minecraft 文件夹位置（没有问题，或是在子文件夹中）
                If Not CheckPermission(FolderPath & "versions\") Then
                    For Each Folder In DirectoryUtils.EnumerateDirectories(FolderPath, True)
                        If CheckPermission(Folder & "\versions\") Then
                            FolderPath = Folder & "\"
                            Exit For
                        End If
                    Next
                End If
                '判断是否已经添加过，若添加过则直接修改自定义名
                Dim Folders As New List(Of String)(Settings.Get(Of String)("LaunchFolders").Split("|"))
                Dim IsAdded As Boolean = False
                Dim IsReplace As Boolean = False
                For i = 0 To Folders.Count - 1
                    Dim Folder As String = Folders(i)
                    If Folder = "" Then Continue For
                    If PathUtils.IsSame(Folder.Split(">")(1), FolderPath) Then
                        IsAdded = True
                        If Folder.Split(">")(0) = DisplayName Then
                            If ShowHint Then Hint("此文件夹已在列表中！", HintType.Blue)
                            Return
                        Else
                            Folders(i) = DisplayName & ">" & FolderPath
                            IsReplace = True
                            If ShowHint Then Hint("文件夹名称已更新为 " & DisplayName & " ！", HintType.Green)
                        End If
                        Exit For
                    End If
                Next
                '如果没有添加过，则添加进去
                If Not IsAdded Then Folders.Add(DisplayName & ">" & FolderPath)
                '保存
                Settings.Set("LaunchFolders", Folders.Join("|"))
                '切换选择并更新列表
                McFolderSelected = FolderPath
                McFolderListLoader.Start(IsForceRestart:=True)
                '提示
                If IsReplace Then Return
                If ShowHint Then Hint("文件夹 " & DisplayName & " 已添加！", HintType.Green)
                '检查是否为根目录整合包，自动关闭版本隔离
                '1. 根目录中存在数个 Mod
                Dim ModFolder = DirectoryUtils.GetInfo(FolderPath & "mods\")
                If Not (ModFolder.Exists AndAlso ModFolder.EnumerateFiles.Count >= 3) Then Return
                '2. 版本数较少，可能为整合包
                Dim VersionFolderInfo = DirectoryUtils.GetInfo(FolderPath & "versions\")
                If Not (VersionFolderInfo.Exists AndAlso VersionFolderInfo.EnumerateDirectories.Count <= 3) Then Return
                '3. 能够找到可安装 Mod 的版本
                For Each VersionFolder In VersionFolderInfo.EnumerateDirectories
                    Dim Instance As New McInstance(VersionFolder.FullName)
                    Instance.Load()
                    If Not Instance.Modable Then Continue For
                    '4. 该版本的隔离文件夹下不存在 mods
                    Dim ModIndieFolder = DirectoryUtils.GetInfo(Instance.PathVersion & "mods\")
                    If ModIndieFolder.Exists AndAlso ModIndieFolder.EnumerateFiles.Any Then Return
                    '满足以上全部条件则视为根目录整合包
                    Settings.Set("VersionArgumentIndie", 2, Instance:=Instance)
                    Settings.Set("VersionArgumentIndieV2", False, Instance:=Instance)
                    Logger.Warn($"已自动关闭单版本隔离：{Instance.Name}")
                Next
            Catch ex As Exception
                Logger.Error(ex, "向文件夹列表中添加新文件夹失败")
            End Try
        End Sub)
    End Sub

    '创建文件夹
    Public Sub Create_Click()
        '下载或版本修改期间禁止更改游戏文件夹
        If McInstanceLock.IsAnyLocked OrElse HasDownloadingTask() Then
            Hint("请在其他任务完成后，再创建游戏文件夹！", HintType.Red)
            Return
        End If
        CreateMcFolderInCurrentPath()
        Hint("新建 .minecraft 文件夹成功！", HintType.Green)
    End Sub
    Public Shared Sub CreateMcFolderInCurrentPath()
        McFolderSelected = Paths.Base & ".minecraft\"
        If Not DirectoryUtils.Exists(McFolderSelected) Then
            DirectoryUtils.Create(McFolderSelected & "versions\")
            McFolderLauncherProfilesJsonCreate(McFolderSelected)
        End If
        AddFolder(McFolderSelected, PathUtils.GetLastPart(Paths.Base), False)
    End Sub

    '右键菜单
    Public Sub Remove_Click(sender As Object, e As RoutedEventArgs)
        Try

            Dim Target As McFolder = CType(CType(CType(sender.Parent, ContextMenu).Parent, Primitives.Popup).PlacementTarget, MyListItem).Tag
            '若为 “移除”，则提醒是否删除 PCL 的配置文件
            If Target.Type = McFolder.Types.Custom Then
                If McInstanceLock.IsAnyLocked OrElse HasDownloadingTask() Then
                    Hint("请在其他任务完成后，再移除游戏文件夹！", HintType.Red)
                    Return
                End If
                Select Case MyMsgBox("是否需要清理 PCL 在该文件夹中的配置文件？" & vbCrLf & "这包括各个版本的独立设置（如自定义图标、第三方登录配置）等，对游戏本身没有影响。", "配置文件清理", "删除", "保留", "取消")
                    Case 1
                        '删除配置文件
                        FileUtils.Delete(Target.Location & "PCL.ini")
                        For Each Instance In DirectoryUtils.EnumerateDirectories(Target.Location & "versions\")
                            DirectoryUtils.Delete(Path.Combine(Instance, "PCL"), True)
                        Next
                    Case 2
                        '不删除
                    Case 3
                        '取消
                        Return
                End Select
            End If
            Dim DeletedName As String = RemoveFolderFromSetup(Target)
            Hint(If(Target.Type = McFolder.Types.Custom, "文件夹 " & DeletedName & " 已从列表中移除！", "文件夹名称已复原！"), HintType.Green)
            McFolderListLoader.Start(IsForceRestart:=True)
        Catch ex As Exception
            Logger.Error(ex, "从列表中移除游戏文件夹失败")
        End Try
    End Sub
    Public Sub Delete_Click(sender As Object, e As RoutedEventArgs)
        If McInstanceLock.IsAnyLocked OrElse HasDownloadingTask() Then
            Hint("请在其他任务完成后，再删除游戏文件夹！", HintType.Red)
            Return
        End If
        Dim Target As McFolder = CType(CType(CType(sender.Parent, ContextMenu).Parent, Primitives.Popup).PlacementTarget, MyListItem).Tag
        Dim DeleteText As String = If((Target.Type = McFolder.Types.Vanilla OrElse Target.Type = McFolder.Types.RenamedVanilla) AndAlso Target.Location = Paths.Base & ".minecraft\" AndAlso McFolderList.IsSingle, "清空", "删除")
        If MyMsgBox("你确定要" & DeleteText & "这个文件夹吗？" & vbCrLf &
                    "目标文件夹：" & Target.Location & vbCrLf & vbCrLf &
                    "该文件夹中的游戏存档、游戏版本，以及 MC 之外的其他文件，都会永久丢失，不可恢复！", "警告", "取消", "确认", "取消") <> 2 Then Return
        If MyMsgBoxInput("删除确认",
                         "该文件夹中的游戏存档、游戏版本，以及 MC 之外的其他文件，都会永久丢失，不可恢复！" & vbCrLf &
                         "目标文件夹：" & Target.Location & vbCrLf & vbCrLf &
                         "如果确实要删除，请在下面输入【Potato】这个单词以继续。",
                         ValidateRules:=New ObjectModel.Collection(Of Validate) From {New ValidateSame("Potato", $"请输入 {vbLQ}Potato{vbRQ}", IgnoreCase:=True)},
                         Button1:="永久删除文件夹", Button2:="取消", IsWarn:=True) Is Nothing Then Return
        RemoveFolderFromSetup(Target)
        RunInNewThread(
        Sub()
            '删除文件夹
            Try
                Hint("正在" & DeleteText & "文件夹 " & Target.Name & "！", HintType.Blue)
                DirectoryUtils.Delete(Target.Location)
                If DeleteText = "清空" Then DirectoryUtils.Create(Target.Location)
                Hint("已" & DeleteText & "文件夹 " & Target.Name & "！", HintType.Green)
            Catch ex As Exception
                Logger.Error(ex, $"{DeleteText}文件夹 {Target.Name} 失败", LogBehavior.Toast)
            Finally
                '刷新列表
                McFolderListLoader.Start(IsForceRestart:=True)
            End Try
        End Sub, "Folder Delete " & GetUuid(), ThreadPriority.BelowNormal)
    End Sub
    ''' <summary>
    ''' 从设置的文件夹列表中删除指定的文件夹。
    ''' 返回被删除的文件夹的显示名称。如果没有，返回 Nothing。
    ''' </summary>
    Private Shared Function RemoveFolderFromSetup(Target As McFolder) As String
        Dim DeletedName As String = Nothing
        Dim Folders = Settings.Get(Of String)("LaunchFolders").Split("|"c, True).ToList
        For Each Folder In Folders.ToList
            If Not Folder.Contains(">") OrElse Not PathUtils.IsSame(Folder.AfterFirst(">"), Target.Location) Then Continue For
            DeletedName = Folder.BeforeFirst(">")
            Folders.Remove(Folder)
            Exit For
        Next
        Settings.Set("LaunchFolders", Folders.Join("|"))
        Return DeletedName
    End Function

    Public Sub Open_Click(sender As Object, e As RoutedEventArgs)
        OpenExplorer(CType(CType(CType(sender.Parent, ContextMenu).Parent, Primitives.Popup).PlacementTarget, MyListItem).Info)
    End Sub
    Public Sub Refresh_Click(sender As Object, e As RoutedEventArgs)
        Dim Data As McFolder = CType(CType(CType(sender.Parent, ContextMenu).Parent, Primitives.Popup).PlacementTarget, MyListItem).Tag
        RefreshCurrent(Data.Location)
    End Sub
    Public Sub RefreshCurrent() Implements IRefreshable.Refresh
        RefreshCurrent(McFolderSelected)
    End Sub
    Public Shared Sub RefreshCurrent(Folder As String)
        WriteIni(Folder & "PCL.ini", "InstanceCache", "") '删除缓存以强制要求下一次加载时更新列表
        If PathUtils.IsSame(Folder, McFolderSelected) Then LoaderFolderRun(McInstanceListLoader, McFolderSelected, LoaderFolderRunType.ForceRun, MaxDepth:=1, ExtraPath:="versions\")
    End Sub
    Public Sub Rename_Click(sender As Object, e As RoutedEventArgs)
        Dim Folder As McFolder = CType(CType(CType(sender.Parent, ContextMenu).Parent, Primitives.Popup).PlacementTarget, MyListItem).Tag
        Try
            '获取输入
            Dim NewName As String =
                MyMsgBoxInput("输入新名称", "", Folder.Name,
                              New ObjectModel.Collection(Of Validate) From {New ValidateNullOrWhiteSpace, New ValidateLength(1, 30), New ValidateExcept({">", "|"})})
            If String.IsNullOrWhiteSpace(NewName) Then Return
            '修改自定义名
            Dim Folders As New List(Of String)(Settings.Get(Of String)("LaunchFolders").Split("|"))
            Dim IsAdded As Boolean = False
            For i = 0 To Folders.Count - 1
                Dim FolderCurrent As String = Folders(i)
                If FolderCurrent = "" Then Continue For
                If PathUtils.IsSame(FolderCurrent.Split(">")(1), Folder.Location) Then
                    IsAdded = True
                    If FolderCurrent.Split(">")(0) = NewName Then
                        '名称未修改
                        Return
                    Else
                        Folders(i) = NewName & ">" & Folder.Location
                    End If
                    Exit For
                End If
            Next
            '如果没有添加过，则添加进去（因为修改了默认项的名称）
            If Not IsAdded Then Folders.Add(NewName & ">" & Folder.Location)
            Hint("文件夹名称已更新为 " & NewName & " ！", HintType.Green)
            '保存
            Settings.Set("LaunchFolders", Folders.Join("|"))
            McFolderListLoader.Start(IsForceRestart:=True)
        Catch ex As Exception
            Logger.Error(ex, "重命名文件夹失败")
        End Try
    End Sub

    '点击选项
    Public Sub Folder_Change(sender As MyListItem, e As RouteEventArgs)
        If Not e.RaiseByMouse OrElse Not sender.Checked Then Return
        '下载或版本修改期间禁止更改游戏文件夹
        If McInstanceLock.IsAnyLocked OrElse HasDownloadingTask(True) Then
            Hint("请在其他任务完成后，再切换游戏文件夹！", HintType.Red)
            e.Handled = True
            Return
        End If
        '更换
        McFolderSelected = CType(sender.Tag, McFolder).Location
        McFolderListLoader.Start(IsForceRestart:=True)
        LoaderFolderRun(McInstanceListLoader, McFolderSelected, LoaderFolderRunType.RunOnUpdated, MaxDepth:=1, ExtraPath:="versions\") '刷新版本列表
    End Sub

End Class
