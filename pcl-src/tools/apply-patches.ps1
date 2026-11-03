<#
.SYNOPSIS
    把「在本机构建 PCL 所必需的源码改动」幂等地应用到源码树。

.DESCRIPTION
    一共 5 处改动，全部与程序行为无关（唯一的例外是第 5 条，那是我们要的功能）：

      1-3. MeloongCore.csproj / MeloongCore.Wpf.csproj / PCLCS.csproj
           LangVersion 13.0 -> 12.0
           （本机只能用 .NET SDK 8，其 Roslyn 最高支持 C# 12）

      4.   Plain Craft Launcher 2.vbproj
           3 个 ProjectReference -> 直接 Reference（HintPath 指向 PCLCS\bin）
           原因：ProjectReference 会把 netstandard2.0 的 facade 程序集带进
           XAML 编译任务的引用清单，导致 MC1000。构建顺序由 build.cmd 保证。

      5.   Plain Craft Launcher 2.vbproj
           注入 AddCoreLibToReferencePath 目标，把 mscorlib / Microsoft.VisualBasic
           补进 @(ReferencePath)。原因：旧式 VB 项目靠 vbc 隐式引用这两个程序集，
           而 SDK 的 MarkupCompilePass1 用 MetadataLoadContext 解析引用清单，
           找不到就会报 MC1000。

      6.   Modules\ModSecret.vb
           RestrictMcDownload: True -> False
           这是唯一的功能改动：恢复 Minecraft 下载入口。

      7.   Modules\ModSecret.vb
           OAuthClientId / CurseForgeAPIKey：
           上游从环境变量读 -> 改为内嵌字面量（值由 set-keys.ps1 写入）
           目的：个人自用，双击 exe 即生效，不依赖启动脚本。

    本脚本可重复执行：已经应用过的改动会被识别并跳过，不会叠加。

.PARAMETER Check
    只检查不写入。输出每一项当前是否已应用。

.PARAMETER BuildRoot
    源码树根目录，默认为脚本上一级目录下的 build。

.OUTPUTS
    每行一项：<状态> <项名> <说明>
    状态取值：APPLIED（本次写入）/ ALREADY（早已应用）/ CHECK-OK / CHECK-TODO / FAIL
#>
[CmdletBinding()]
param(
    [switch]$Check,
    [string]$BuildRoot = ''
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if ([string]::IsNullOrWhiteSpace($BuildRoot)) {
    $BuildRoot = Join-Path (Split-Path -Parent $PSScriptRoot) 'build'
}
if (-not (Test-Path -LiteralPath $BuildRoot)) {
    throw "源码树不存在: $BuildRoot"
}

$fCore   = Join-Path $BuildRoot 'MeloongCore\Shared\MeloongCore.csproj'
$fWpf    = Join-Path $BuildRoot 'MeloongCore\Wpf\MeloongCore.Wpf.csproj'
$fClcs   = Join-Path $BuildRoot 'PCLCS\PCLCS.csproj'
$fProj   = Join-Path $BuildRoot 'Plain Craft Launcher 2\Plain Craft Launcher 2.vbproj'
$fSecret = Join-Path $BuildRoot 'Plain Craft Launcher 2\Modules\ModSecret.vb'

foreach ($f in @($fCore, $fWpf, $fClcs, $fProj, $fSecret)) {
    if (-not (Test-Path -LiteralPath $f)) { throw "源文件不存在: $f" }
}

# ---------------------------------------------------------------------------
# 编码安全的读写（保留原文件是否带 BOM）
# ---------------------------------------------------------------------------
function Read-SourceText {
    param([string]$Path)
    $bytes = [System.IO.File]::ReadAllBytes($Path)
    $hasBom = ($bytes.Length -ge 3 -and $bytes[0] -eq 0xEF -and $bytes[1] -eq 0xBB -and $bytes[2] -eq 0xBF)
    $text = [System.Text.Encoding]::UTF8.GetString($bytes)
    if ($hasBom) { $text = $text.TrimStart([char]0xFEFF) }
    return [pscustomobject]@{ Text = $text; HasBom = $hasBom }
}

function Write-SourceText {
    param([string]$Path, [string]$Text, [bool]$HasBom)
    $enc = New-Object System.Text.UTF8Encoding($HasBom)
    [System.IO.File]::WriteAllText($Path, $Text, $enc)
}

$results = New-Object System.Collections.Generic.List[string]
$fail = 0

function Note {
    param([string]$Status, [string]$Item, [string]$Detail)
    $results.Add(('{0,-13} {1,-22} {2}' -f $Status, $Item, $Detail))
}

# ---------------------------------------------------------------------------
# 补丁 1-3：LangVersion 13.0 -> 12.0
# ---------------------------------------------------------------------------
foreach ($pair in @(
    @{ Path = $fCore; Name = 'MeloongCore.csproj' },
    @{ Path = $fWpf;  Name = 'MeloongCore.Wpf.csproj' },
    @{ Path = $fClcs; Name = 'PCLCS.csproj' }
)) {
    $info = Read-SourceText $pair.Path
    $t = $info.Text
    if ($t -match '<LangVersion>\s*12\.0\s*</LangVersion>') {
        Note 'ALREADY' $pair.Name 'LangVersion 已是 12.0'
        continue
    }
    $pat = '<LangVersion>\s*13\.0\s*</LangVersion>'
    if ($t -notmatch $pat) {
        Note 'FAIL' $pair.Name '未找到 <LangVersion>13.0</LangVersion>，上游可能已升级语法版本'
        $fail++
        continue
    }
    if ($Check) { Note 'CHECK-TODO' $pair.Name '待改 13.0 -> 12.0'; continue }
    $t = $t -replace $pat, '<LangVersion>12.0</LangVersion>'
    Write-SourceText $pair.Path $t $info.HasBom
    Note 'APPLIED' $pair.Name 'LangVersion 13.0 -> 12.0'
}

# ---------------------------------------------------------------------------
# 补丁 4：ProjectReference -> Reference
# ---------------------------------------------------------------------------
$infoP = Read-SourceText $fProj
$tP = $infoP.Text
$projChanged = $false

$newRefBlock = @'
  <ItemGroup>
    <!-- 本地构建适配：原为 ProjectReference。改为直接引用已编译 dll，
         避免 netstandard2.0 的引用程序集（facade mscorlib）进入 XAML 编译任务的引用清单。
         构建顺序由 build.cmd 保证：MeloongCore -> MeloongCore.Wpf -> PCLCS -> 主项目。 -->
    <Reference Include="MeloongCore">
      <HintPath>..\PCLCS\bin\MeloongCore.dll</HintPath>
      <Private>False</Private>
    </Reference>
    <Reference Include="MeloongCore.Wpf">
      <HintPath>..\PCLCS\bin\MeloongCore.Wpf.dll</HintPath>
      <Private>False</Private>
    </Reference>
    <Reference Include="PCLCS">
      <HintPath>..\PCLCS\bin\PCLCS.dll</HintPath>
      <Private>False</Private>
    </Reference>
  </ItemGroup>
'@

if ($tP -match '<Reference Include="PCLCS">') {
    Note 'ALREADY' 'vbproj/Reference' 'ProjectReference 已替换为 Reference'
} else {
    $patRef = '(?s)<ItemGroup>\s*<ProjectReference Include="\.\.\\MeloongCore\\Shared\\MeloongCore\.csproj">.*?</ItemGroup>'
    if ($tP -notmatch $patRef) {
        Note 'FAIL' 'vbproj/Reference' '未找到 MeloongCore 的 ProjectReference 块，上游结构可能已变'
        $fail++
    } elseif ($Check) {
        Note 'CHECK-TODO' 'vbproj/Reference' '待替换 3 个 ProjectReference'
    } else {
        $tP = $tP -replace $patRef, $newRefBlock
        $projChanged = $true
        Note 'APPLIED' 'vbproj/Reference' '3 个 ProjectReference -> Reference'
    }
}

# ---------------------------------------------------------------------------
# 补丁 5：注入 AddCoreLibToReferencePath 目标
# ---------------------------------------------------------------------------
$targetBlock = @'

  <!--
    本地构建适配（仅为在 dotnet SDK 的 MSBuild 下构建，不改动程序行为）：
    旧式 .NET Framework 项目不会把 mscorlib 放进 @(ReferencePath)（vbc 默认引用它），
    而 SDK 自带的 MarkupCompilePass1 用 MetadataLoadContext 解析引用，缺少 mscorlib 会报 MC1000。
    此处把它补进引用清单。用 VS 的 MSBuild 构建时本目标无副作用。
  -->
  <Target Name="AddCoreLibToReferencePath"
          AfterTargets="ResolveAssemblyReferences"
          BeforeTargets="MarkupCompilePass1">
    <PropertyGroup>
      <_MCLiteRefDir>$(TargetFrameworkRootPath)\.NETFramework\v4.8\</_MCLiteRefDir>
      <_MCLiteRefDir Condition="!Exists('$(_MCLiteRefDir)mscorlib.dll')">$(TargetFrameworkRootPath)/.NETFramework/v4.8/</_MCLiteRefDir>
    </PropertyGroup>
    <ItemGroup>
      <!-- vbc 会隐式引用这两个程序集，旧式项目因此不把它们放进 @(ReferencePath)，
           而 SDK 的 MarkupCompilePass1 需要在其引用清单中找到它们。 -->
      <ReferencePath Include="$(_MCLiteRefDir)mscorlib.dll"
                     Condition="Exists('$(_MCLiteRefDir)mscorlib.dll')" />
      <ReferencePath Include="$(_MCLiteRefDir)Microsoft.VisualBasic.dll"
                     Condition="Exists('$(_MCLiteRefDir)Microsoft.VisualBasic.dll')" />
    </ItemGroup>
  </Target>
'@

if ($tP -match 'Name="AddCoreLibToReferencePath"') {
    Note 'ALREADY' 'vbproj/RefPathTarget' 'AddCoreLibToReferencePath 已注入'
} else {
    $anchor = '<Import Project="$(MSBuildToolsPath)\Microsoft.VisualBasic.targets" />'
    if ($tP -notmatch [regex]::Escape($anchor)) {
        Note 'FAIL' 'vbproj/RefPathTarget' '未找到 VisualBasic.targets 导入行，无法定位注入点'
        $fail++
    } elseif ($Check) {
        Note 'CHECK-TODO' 'vbproj/RefPathTarget' '待注入 AddCoreLibToReferencePath 目标'
    } else {
        $tP = $tP.Replace($anchor, $anchor + $targetBlock)
        $projChanged = $true
        Note 'APPLIED' 'vbproj/RefPathTarget' '已注入 AddCoreLibToReferencePath 目标'
    }
}

if ($projChanged -and -not $Check) {
    Write-SourceText $fProj $tP $infoP.HasBom
}

# ---------------------------------------------------------------------------
# 补丁 6：RestrictMcDownload  True -> False
# ---------------------------------------------------------------------------
$infoS = Read-SourceText $fSecret
$tS = $infoS.Text
$secretChanged = $false

if ($tS -match 'Public Const RestrictMcDownload As Boolean = False') {
    Note 'ALREADY' 'ModSecret.vb' 'RestrictMcDownload 已是 False'
} else {
    $patSecret = 'Public Const RestrictMcDownload As Boolean = True'
    if ($tS -notmatch $patSecret) {
        Note 'FAIL' 'ModSecret.vb' '未找到 RestrictMcDownload 常量，上游可能已改名'
        $fail++
    } elseif ($Check) {
        Note 'CHECK-TODO' 'ModSecret.vb' '待改 RestrictMcDownload True -> False'
    } else {
        $tS = $tS -replace $patSecret, 'Public Const RestrictMcDownload As Boolean = False'
        $secretChanged = $true
        Note 'APPLIED' 'ModSecret.vb' 'RestrictMcDownload True -> False'
    }
}

# ---------------------------------------------------------------------------
# 补丁 7：密钥从「读环境变量」改为「内嵌字面量」
#   上游： Public OAuthClientId As String = If(Environment.GetEnvironmentVariable("PCL_MS_CLIENT_ID"), "")
#   目标： Public OAuthClientId As String = "<值>"     值由 set-keys.ps1 写入
#   目的： 个人自用，双击 exe 即生效，不再依赖启动脚本
# ---------------------------------------------------------------------------
$patOAuth = 'Public OAuthClientId As String = If\(Environment\.GetEnvironmentVariable\("PCL_MS_CLIENT_ID"\), ""\)'
$patCfKey = 'Public CurseForgeAPIKey As String = If\(Environment\.GetEnvironmentVariable\("PCL_CURSEFORGE_API_KEY"\), ""\)'

if ($tS -match 'Public OAuthClientId As String\s*=\s*"(?:[^"\\]|\\.)*"') {
    Note 'ALREADY' 'ModSecret.vb/keys' '密钥已是内嵌字面量'
} else {
    if ($tS -notmatch $patOAuth -or $tS -notmatch $patCfKey) {
        Note 'FAIL' 'ModSecret.vb/keys' '未找到环境变量形式的密钥定义，上游可能已改'
        $fail++
    } elseif ($Check) {
        Note 'CHECK-TODO' 'ModSecret.vb/keys' '待把环境变量读取改为内嵌字面量'
    } else {
        $tS = $tS -replace $patOAuth, 'Public OAuthClientId As String = ""'
        $tS = $tS -replace $patCfKey, 'Public CurseForgeAPIKey As String = ""'
        $secretChanged = $true
        Note 'APPLIED' 'ModSecret.vb/keys' '环境变量读取 -> 内嵌字面量（空值待填）'
    }
}

if ($secretChanged -and -not $Check) {
    Write-SourceText $fSecret $tS $infoS.HasBom
}

# ---------------------------------------------------------------------------
# 汇总
# ---------------------------------------------------------------------------
$results | ForEach-Object { Write-Output $_ }

if ($fail -gt 0) {
    Write-Output ''
    Write-Output "结果：$fail 项失败。上游结构变化较大，需人工核对后调整本脚本。"
    exit 1
}

if ($Check) {
    $todo = @($results | Where-Object { $_ -like 'CHECK-TODO*' }).Count
    if ($todo -eq 0) { Write-Output ''; Write-Output '结果：全部补丁已应用。' }
    else { Write-Output ''; Write-Output "结果：$todo 项待应用（去掉 -Check 执行写入）。" }
} else {
    Write-Output ''
    Write-Output '结果：补丁处理完成。'
}

exit 0
