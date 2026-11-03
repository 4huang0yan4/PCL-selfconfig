<#
.SYNOPSIS
    把上游新版本同步到本地构建树：拉源码 -> 解子模块 -> 套补丁。

.DESCRIPTION
    流程：

      1. 获取主仓库源码（网络：codeload tarball；离线：-From 指定本地包）
      2. 获取 MeloongCore 子模块（网络：按 tag 查出 commit 后下载；离线：-Core）
      3. 解压到 staging\<tag>\tree
      4. 调 apply-patches.ps1 应用 5 处本地改动
      5. 加 -Force 时，把 staging 的源码树替换到 build\（旧树改名为 build.bak-<时间戳>）

    默认只做 1-4，不碰 build\。确认 staging 内容无误后再加 -Force。

.PARAMETER Tag
    上游 tag，例如 2.99.0.0。必填。

.PARAMETER From
    本地源码包（tar.gz）。给出则跳过网络下载。

.PARAMETER Core
    本地 MeloongCore 子模块包（tar.gz）。离线时必须给，否则复用当前 build\MeloongCore。

.PARAMETER Force
    把 staging 的源码树替换到 build\，旧树保留为 build.bak-<时间戳>。

.PARAMETER SkipPatch
    不自动应用本地补丁（只准备源码）。

.EXAMPLE
    powershell -NoProfile -File tools\sync.ps1 -Tag 2.99.0.0 -From ..\pcl-2.99.0.0.tar.gz -Core ..\core.tar.gz

.EXAMPLE
    powershell -NoProfile -File tools\sync.ps1 -Tag 2.99.0.0 -Force

.NOTES
    codeload 直连较慢（实测约 50 KB/s，15 MB 需 12 分钟）。
    若已有本地包，优先用 -From 避免等待。
#>
[CmdletBinding()]
param(
    [string]$Tag = '',
    [string]$From = '',
    [string]$Core = '',
    [switch]$Force,
    [switch]$SkipPatch
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$toolDir = $PSScriptRoot
$rootDir = Split-Path -Parent $toolDir
$buildDir = Join-Path $rootDir 'build'
$stagingRoot = Join-Path $rootDir 'staging'

if ([string]::IsNullOrWhiteSpace($Tag)) {
    throw '必须指定 -Tag，例如 -Tag 2.99.0.0'
}

$stagingDir = Join-Path $stagingRoot $Tag
$treeDir = Join-Path $stagingDir 'tree'
$tarMain = Join-Path $stagingDir "PCL-$Tag.tar.gz"
$tarCore = Join-Path $stagingDir "MeloongCore-$Tag.tar.gz"

if (-not (Test-Path -LiteralPath $stagingDir)) {
    New-Item -ItemType Directory -Path $stagingDir -Force | Out-Null
}

$tarExe = Join-Path $env:SystemRoot 'System32\tar.exe'
if (-not (Test-Path -LiteralPath $tarExe)) { throw "找不到 tar.exe: $tarExe" }

function Get-RemoteFile {
    param([string]$Url, [string]$OutFile)
    Write-Output ("       下载: " + $Url)
    $wc = New-Object System.Net.WebClient
    $wc.Headers.Add('User-Agent', 'PCL-Custom-Build')
    $wc.DownloadFile($Url, $OutFile)
    $size = (Get-Item -LiteralPath $OutFile).Length
    Write-Output ("       完成: {0:N1} MB" -f ($size / 1MB))
}

# ---------------------------------------------------------------------------
# 1. 主仓库源码
# ---------------------------------------------------------------------------
Write-Output "[1/4] 主仓库源码 ($Tag)"

if (-not [string]::IsNullOrWhiteSpace($From)) {
    $src = if ([System.IO.Path]::IsPathRooted($From)) { $From } else { Join-Path (Get-Location).Path $From }
    if (-not (Test-Path -LiteralPath $src)) { throw "本地包不存在: $src" }
    if (-not (Test-Path -LiteralPath $tarMain)) { Copy-Item -LiteralPath $src -Destination $tarMain }
    Write-Output ("       使用本地包: " + $src)
}
elseif (-not (Test-Path -LiteralPath $tarMain)) {
    Get-RemoteFile "https://codeload.github.com/Meloong-Git/PCL/tar.gz/refs/tags/$Tag" $tarMain
}
else {
    Write-Output "       已存在源码包，跳过下载"
}

# ---------------------------------------------------------------------------
# 2. MeloongCore 子模块
# ---------------------------------------------------------------------------
Write-Output "[2/4] MeloongCore 子模块"

$coreSha = ''
if (-not [string]::IsNullOrWhiteSpace($Core)) {
    $srcCore = if ([System.IO.Path]::IsPathRooted($Core)) { $Core } else { Join-Path (Get-Location).Path $Core }
    if (-not (Test-Path -LiteralPath $srcCore)) { throw "本地子模块包不存在: $srcCore" }
    if (-not (Test-Path -LiteralPath $tarCore)) { Copy-Item -LiteralPath $srcCore -Destination $tarCore }
    Write-Output ("       使用本地包: " + $srcCore)
}
else {
    $apiUrl = "https://api.github.com/repos/Meloong-Git/PCL/contents/MeloongCore?ref=$Tag"
    Write-Output ("       查询子模块 commit: " + $apiUrl)
    $resp = Invoke-RestMethod -Uri $apiUrl -Headers @{ 'User-Agent' = 'PCL-Custom-Build' } -TimeoutSec 60
    $coreSha = $resp.sha
    if ([string]::IsNullOrWhiteSpace($coreSha)) { throw '未能从 GitHub API 取得子模块 commit' }
    Write-Output ("       commit: " + $coreSha)
    if (-not (Test-Path -LiteralPath $tarCore)) {
        Get-RemoteFile "https://codeload.github.com/Meloong-Git/MeloongCore/tar.gz/$coreSha" $tarCore
    }
    else {
        Write-Output "       已存在子模块包，跳过下载"
    }
}

# ---------------------------------------------------------------------------
# 3. 解压到 staging\<tag>\tree
# ---------------------------------------------------------------------------
Write-Output "[3/4] 解压到 staging"

if (Test-Path -LiteralPath $treeDir) {
    Write-Output "       已存在 tree，跳过解压（如需重来请先手工删除 staging\$Tag）"
}
else {
    & $tarExe -xzf $tarMain -C $stagingDir
    if ($LASTEXITCODE -ne 0) { throw "解压主仓库失败（tar 退出码 $LASTEXITCODE）" }

    $extracted = Join-Path $stagingDir "PCL-$Tag"
    if (-not (Test-Path -LiteralPath $extracted)) { throw "解压后未找到目录: $extracted" }
    Move-Item -LiteralPath $extracted -Destination $treeDir

    # 子模块：解开并放到 tree\MeloongCore
    $coreTarget = Join-Path $treeDir 'MeloongCore'
    $coreStaging = Join-Path $stagingDir '_core'
    if (-not (Test-Path -LiteralPath $coreStaging)) {
        New-Item -ItemType Directory -Path $coreStaging -Force | Out-Null
    }
    & $tarExe -xzf $tarCore -C $coreStaging
    if ($LASTEXITCODE -ne 0) { throw "解压子模块失败（tar 退出码 $LASTEXITCODE）" }

    $coreInner = @(Get-ChildItem -LiteralPath $coreStaging -Directory)
    if ($coreInner.Count -ne 1) { throw "子模块包内目录数量异常: $($coreInner.Count)" }

    if (Test-Path -LiteralPath $coreTarget) {
        [System.IO.Directory]::Delete($coreTarget, $true)
    }
    Move-Item -LiteralPath $coreInner[0].FullName -Destination $coreTarget
    Write-Output ("       tree 文件数: " + @(Get-ChildItem -LiteralPath $treeDir -Recurse -File).Count)
}

# ---------------------------------------------------------------------------
# 4. 应用本地补丁
# ---------------------------------------------------------------------------
if ($SkipPatch) {
    Write-Output "[4/4] 已跳过补丁（-SkipPatch）"
}
else {
    Write-Output "[4/4] 应用本地补丁"
    & (Join-Path $toolDir 'apply-patches.ps1') -BuildRoot $treeDir
    if ($LASTEXITCODE -ne 0) { throw '应用补丁失败，见上面的 FAIL 行' }
}

# ---------------------------------------------------------------------------
# 可选：替换 build
# ---------------------------------------------------------------------------
if ($Force) {
    $stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
    if (Test-Path -LiteralPath $buildDir) {
        $bak = "$buildDir.bak-$stamp"
        Write-Output ("       旧树备份: " + $bak)
        Move-Item -LiteralPath $buildDir -Destination $bak
    }
    Copy-Item -LiteralPath $treeDir -Destination $buildDir -Recurse -Force
    Write-Output ("       已替换: " + $buildDir)
    Write-Output ''
    Write-Output '下一步：运行 build.cmd 全量编译。'
}
else {
    Write-Output ''
    Write-Output ("源码已准备: " + $treeDir)
    Write-Output '确认无误后加 -Force 替换 build\，或直接运行 build.cmd（仍用当前 build 树）。'
}
