'P3：实例只读视图（2026-10-29）
'目的：导出 / 导入 / 批量操作这些新功能只依赖这一层，不直接摸 McInstance。
'PCL 的实例存储是分散的（版本目录 + 版本 json + PCL\Setup.ini + 注册表），
'先把"读"的边界定住，后面接第二种启动器格式或做批量导入时不必改业务代码。
'第一版只覆盖导出真正用到的字段，不求一次抽全；要加字段时先问"导出/导入是否真的需要"。

''' <summary>实例的只读视图。实现方负责把 PCL 的分散存储映射成稳定字段。</summary>
Public Interface IInstanceView

    ''' <summary>实例名（versions 下的目录名）。</summary>
    ReadOnly Property Name As String

    ''' <summary>版本文件夹路径，以 \ 结尾。</summary>
    ReadOnly Property PathVersion As String

    ''' <summary>应用版本隔离后的游戏根目录，以 \ 结尾。</summary>
    ReadOnly Property PathIndie As String

    ''' <summary>界面上显示的版本名（含加载器描述）。</summary>
    ReadOnly Property DisplayName As String

    ''' <summary>标准 MC 版本号（如 1.20.1 / 26.3）。导出清单必须用它，不能用显示名。</summary>
    ReadOnly Property VanillaName As String

    ''' <summary>版本图标路径，可能为空串。</summary>
    ReadOnly Property Logo As String

    ''' <summary>该实例是否可以安装模组。</summary>
    ReadOnly Property Modable As Boolean

End Interface

''' <summary>把 PCL 的 McInstance 适配成只读视图。所有成员都是转发，不缓存、不改状态。</summary>
Public Class McInstanceView
    Implements IInstanceView

    Private ReadOnly Inner As McInstance

    Public Sub New(Instance As McInstance)
        If Instance Is Nothing Then Throw New ArgumentNullException(NameOf(Instance))
        Inner = Instance
    End Sub

    Public ReadOnly Property Name As String Implements IInstanceView.Name
        Get
            Return Inner.Name
        End Get
    End Property

    Public ReadOnly Property PathVersion As String Implements IInstanceView.PathVersion
        Get
            Return Inner.PathVersion
        End Get
    End Property

    Public ReadOnly Property PathIndie As String Implements IInstanceView.PathIndie
        Get
            Return Inner.PathIndie
        End Get
    End Property

    Public ReadOnly Property DisplayName As String Implements IInstanceView.DisplayName
        Get
            Return Inner.VersionDisplayName()
        End Get
    End Property

    Public ReadOnly Property VanillaName As String Implements IInstanceView.VanillaName
        Get
            Return Inner.Version.VanillaName
        End Get
    End Property

    Public ReadOnly Property Logo As String Implements IInstanceView.Logo
        Get
            Return Inner.Logo
        End Get
    End Property

    Public ReadOnly Property Modable As Boolean Implements IInstanceView.Modable
        Get
            Return Inner.Modable
        End Get
    End Property

End Class
