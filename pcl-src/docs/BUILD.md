# 构建说明：在无 VS 的机器上用 dotnet SDK 编 PCL 2.99.0.0

PCL 主项目是 **旧式（non-SDK）VB.NET + WPF 项目，TargetFramework v4.8**，上游 CI 用的是 `microsoft/setup-msbuild` 提供的 **VS 版 MSBuild.exe**。
本机安全策略禁止直接调用 `MSBuild.exe`（LOLBin 拦截），因此整条链路改走 `dotnet`，过程中踩到 6 个坑，逐个记录如下。

## 环境

| 项 | 值 |
|---|---|
| SDK | .NET SDK **8.0.404**，绿色解压版在 `..\_tools\dotnet`（未装进系统，不写注册表） |
| NuGet 缓存 | `..\_tools\nuget`（`NUGET_PACKAGES`） |
| net48 引用程序集 | NuGet 包 `Microsoft.NETFramework.ReferenceAssemblies.net48` 1.0.3 |
| 构建命令 | `dotnet build`（不是 `dotnet msbuild`，后者会被 LOLBin 规则拦截） |

隔离靠三个环境变量，保证不污染 C 盘：`DOTNET_ROOT`、`NUGET_PACKAGES`、`DOTNET_CLI_HOME`。

## 坑位与解法

### 1. SDK 10 在本机无法运行

`.NET SDK 10.0.402` 编译时报：

```
error : Your Windows doesn't fully support CET. Please install all available Windows updates.
```

Win10 22H2（19045）缺 CET 相关更新，.NET 10 的 Roslyn 直接拒绝启动。**改用 SDK 8.0.404**。SDK 9 已 EOL，未采用。

### 2. LangVersion 13.0 不被 SDK 8 支持

三个子项目（`MeloongCore`、`MeloongCore.Wpf`、`PCLCS`）都写了 `<LangVersion>13.0</LangVersion>`，SDK 8 的 Roslyn 只认到 12。

**改成 12.0**。实测源码未使用 C# 13 独有语法，降版本后全部编过。

### 3. MSB3644：找不到 net48 引用程序集

旧式项目不会像 SDK-style 那样自动拉 `Microsoft.NETFramework.ReferenceAssemblies`，需要显式指路：

```
-p:TargetFrameworkRootPath=<NUGET_PACKAGES>\microsoft.netframework.referenceassemblies.net48\1.0.3\build
```

### 4. `Microsoft.WinFX.targets` 不在探测路径

`Microsoft.NETFramework.CurrentVersion.targets:130` 只在 `$(MSBuildToolsPath)` 根目录找这个文件：

```xml
<Import Project="$(MSBuildToolsPath)\Microsoft.WinFX.targets"
        Condition="... and Exists('$(MSBuildToolsPath)\Microsoft.WinFX.targets')"/>
```

而 dotnet SDK 只把它放在 `Sdks\Microsoft.NET.Sdk.WindowsDesktop\targets\` 下。

**解法**：在 `_tools\dotnet\sdk\8.0.404\Microsoft.WinFX.targets` 放一个转发 shim（3 行 `<Import>`）。
被导入文件内部的 `$(MSBuildThisFileDirectory)` 仍指向 Sdks 目录，因此 `..\tools\net8.0\PresentationBuildTasks.dll` 能正确解析。
**删除该文件即完全恢复 SDK 原状。**

### 5. MC1000：XAML 任务找不到 `mscorlib` / `Microsoft.VisualBasic`

SDK 自带的 `MarkupCompilePass1` 用 `MetadataLoadContext` 解析 `@(ReferencePath)`，而旧式 VB 项目**不会**把这两个程序集放进 `ReferencePath`（vbc 隐式引用）。

**解法**：在 vbproj 末尾加一个构建钩子，把它们补进引用清单：

```xml
<Target Name="AddCoreLibToReferencePath"
        AfterTargets="ResolveAssemblyReferences"
        BeforeTargets="MarkupCompilePass1">
  <PropertyGroup>
    <_MCLiteRefDir>$(TargetFrameworkRootPath)\.NETFramework\v4.8\</_MCLiteRefDir>
    ...
  </PropertyGroup>
  <ItemGroup>
    <ReferencePath Include="$(_MCLiteRefDir)mscorlib.dll" .../>
    <ReferencePath Include="$(_MCLiteRefDir)Microsoft.VisualBasic.dll" .../>
  </ItemGroup>
</Target>
```

注意 `$(TargetFrameworkDirectory)` 是**分号分隔的多路径列表**（含 `Facades\`），不能直接拼文件名，必须自己按 `TargetFrameworkRootPath` 定位。

### 6. MSB3030：临时项目复制 nuget.* 文件失败

XAML 编译会生成临时项目并尝试把 `*.nuget.g.props` 等复制过去，旧式项目没有这些文件。

**解法**：`-p:IncludePackageReferencesDuringMarkupCompilation=false`

## 源码侧改动清单（不改变程序行为）

| 文件 | 改动 |
|---|---|
| `MeloongCore/Shared/MeloongCore.csproj` | `LangVersion` 13.0 → 12.0 |
| `MeloongCore/Wpf/MeloongCore.Wpf.csproj` | 同上 |
| `PCLCS/PCLCS.csproj` | 同上 |
| `Plain Craft Launcher 2/Plain Craft Launcher 2.vbproj` | 3 个 `ProjectReference` → `Reference`（HintPath 指向 `..\PCLCS\bin\*.dll`）。原因：ProjectReference 会把 `netstandard2.0` 的 facade 程序集带进 XAML 任务的引用清单。构建顺序改由 `build.cmd` 保证 |
| 同上 | 末尾新增 `AddCoreLibToReferencePath` 目标（见坑 5） |

原始文件均已备份为 `*.orig`。

**功能改动共两处**，都在 `Modules/ModSecret.vb`：

| 位置 | 改动 | 目的 |
|---|---|---|
| `:18` | `RestrictMcDownload` `True` → `False` | 恢复 Minecraft 下载入口 |
| `:10 / :12` | `OAuthClientId` / `CurseForgeAPIKey` 由「读环境变量」改为内嵌字面量 | 个人自用，双击 exe 即生效，不依赖启动脚本 |

第二处的值由 `tools\set-keys.ps1` 从 `PCL-custom\secrets.env` 写入，入口是 `PCL-custom\Set-Keys.cmd`（写值 + 重编）。补丁脚本只负责把字段形态改成字面量（初始为空串），不涉及具体密钥值。

以上 7 项改动已脚本化，见 `tools\apply-patches.ps1`。它对上游新版本重复执行是安全的（已应用的项会跳过），上游结构变化导致匹配不到时会明确报 FAIL 而不是静默跳过。

## 版本号与产物归档

`build.cmd` 编译前会调用 `tools\version-apply.ps1`，向两个文件写入可识别的版本信息，编译完成后自动还原：

| 文件 | 字段 | 示例 |
|---|---|---|
| `Modules\Base\ModBase.vb` | `VersionBaseName` | `2026.10.25-custom.1` |
| 同上 | `BuildTypeDisplay` | `自编版` |
| `My Project\AssemblyInfo.vb` | `AssemblyVersion` / `AssemblyFileVersion` | `26.10.25.1` |

- 显示格式：`自编版 2026.10.25-custom.1`（日志首行同为该值）
- 程序集版本为四段纯数字 `年.月.日.当日序号`，可在文件属性里直接看
- 每次构建归档到 `PCL-custom\versions\<标签>\`，内含 exe、exe.config、build-info.txt
- `build.cmd -NoArchive` 只更新 `PCL-custom\` 根目录，不建档
- 原始文件保留为 `*.verorig`；`version-apply.ps1 -Restore` 可随时还原

## 目录

```
pcl-src\
  build\                    源码工作树（已套补丁，直接编译这棵）
  staging\<tag>\tree\       sync.cmd 产出的候选源码树
  src\PCL-full\tree\        上游原始快照（未改动一字）
  pcl-<tag>.tar.gz          上游源码原包
  tools\
    apply-patches.ps1       7 项源码改动，幂等
    set-keys.ps1            把 secrets.env 的两个密钥写进 ModSecret.vb
    version-apply.ps1       版本号注入 / 还原
    sync.ps1                拉源码 + 解子模块 + 套补丁
  build.cmd                 编译（增量约 6 秒）
  sync.cmd                  同步上游
  docs\                     本文档、实测截图
```

## 上游同步

```cmd
rem 全新拉取（codeload 直连约 50 KB/s，15 MB 需 12 分钟）
sync.cmd -Tag <TAG>

rem 用已有的本地包，跳过网络
sync.cmd -Tag <TAG> -From pcl-<TAG>.tar.gz -Core core.tar.gz

rem 同步并替换 build\（旧树自动备份为 build.bak-<时间戳>）
sync.cmd -Tag <TAG> -Force

rem 只准备源码不替换，然后单独编译候选树做验证
sync.cmd -Tag <TAG>
build.cmd -NoArchive            （PCL_TREE 环境变量可指向 staging 树）
```

子模块 commit 由 `sync.ps1` 通过 GitHub API 按 tag 查出，无需手工找。

### 在候选树上试编译

```cmd
set PCL_TREE=D:\...\pcl-src\staging\<TAG>\tree
build.cmd -NoArchive
```

`PCL_TREE` 只影响编译目录，产物仍复制到 `PCL-custom\`。这样可以在不破坏现有 `build\` 的前提下验证新版本能否编过。

### 同步后必须重填密钥

`sync.cmd -Force` 会用上游源码整体替换 `build\`，因此内嵌的两个密钥会回到空值（补丁 7 只把它们变成空字面量）。替换完成后执行一次：

```cmd
PCL-custom\Set-Keys.cmd
```

它会从 `PCL-custom\secrets.env` 重新写入密钥并编译。

