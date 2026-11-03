<#
.SYNOPSIS
    给 PCL 源码树打上可识别的构建版本号（编译前调用，编译后还原）。

.DESCRIPTION
    修改两处文件：

      Modules\Base\ModBase.vb
        - VersionBaseName   -> 显示用版本名，如 2026.10.25-custom.1
        - BuildTypeDisplay  -> 固定为「测试版」，与官方「正式版/快照版」区分

      My Project\AssemblyInfo.vb
        - AssemblyVersion / AssemblyFileVersion -> 纯数字四段，如 26.10.25.1

    首次运行时把原文件备份为 *.verorig，之后每次都从备份出发重新生成，
    因此重复调用结果一致（幂等）。加 -Restore 即完全还原。

.PARAMETER Label
    显示用版本名。留空则自动生成 <yyyy.MM.dd>-custom.<序号>。

.PARAMETER VerNum
    程序集版本号，四段纯数字。留空则自动生成 <yy>.<MM>.<dd>.<序号>。

.PARAMETER Restore
    还原为 .verorig 备份。

.PARAMETER ReuseSeq
    不推进当日序号，沿用已归档的最大序号。build.cmd -NoArchive 时使用，
    避免「不归档却跳版本号」造成产物与归档目录对不上。

.PARAMETER BuildRoot
    源码树根目录，默认为脚本上一级目录下的 build。

.OUTPUTS
    成功时输出单行：<Label>|<VerNum>
    还原时输出单行：RESTORED

.EXAMPLE
    powershell -NoProfile -File tools\version-apply.ps1

.EXAMPLE
    powershell -NoProfile -File tools\version-apply.ps1 -Restore
#>
[CmdletBinding()]
param(
    [string]$Label = '',
    [string]$VerNum = '',
    [switch]$Restore,
    [switch]$ReuseSeq,
    [string]$BuildRoot = ''
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# ---------------------------------------------------------------------------
# 路径解析
# ---------------------------------------------------------------------------
if ([string]::IsNullOrWhiteSpace($BuildRoot)) {
    $BuildRoot = Join-Path (Split-Path -Parent $PSScriptRoot) 'build'
}
if (-not (Test-Path -LiteralPath $BuildRoot)) {
    throw "源码树不存在: $BuildRoot"
}

$modBase = Join-Path $BuildRoot 'Plain Craft Launcher 2\Modules\Base\ModBase.vb'
$asmInfo = Join-Path $BuildRoot 'Plain Craft Launcher 2\My Project\AssemblyInfo.vb'

foreach ($f in @($modBase, $asmInfo)) {
    if (-not (Test-Path -LiteralPath $f)) { throw "源文件不存在: $f" }
}

$targets = @($modBase, $asmInfo)

# ---------------------------------------------------------------------------
# 编码安全的读写：保留原文件是否带 BOM
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

# ---------------------------------------------------------------------------
# 还原
#   注意：这里刻意不删除 *.verorig。它们是源码的原始快照，保留下来既方便
#   反复 apply / restore，也避免依赖 Remove-Item（本机的安全删除垫片会在
#   实际成功入回收站之后仍然抛出异常，从而中断脚本）。
# ---------------------------------------------------------------------------
if ($Restore) {
    $restored = 0
    foreach ($f in $targets) {
        $bak = "$f.verorig"
        if (Test-Path -LiteralPath $bak) {
            Copy-Item -LiteralPath $bak -Destination $f -Force
            $restored++
        }
    }
    Write-Output ("RESTORED {0}/{1}" -f $restored, $targets.Count)
    exit 0
}

# ---------------------------------------------------------------------------
# 生成版本号
# ---------------------------------------------------------------------------
$now = Get-Date
$dayTag = '{0:yyyy.MM.dd}' -f $now

# 工作空间根：向上查找第一个含 PCL-custom 子目录的祖先（最多 6 层）。
# 不写死层数，这样源码树放在任意位置（含被别的智能体挪走）都能正确定位归档目录。
$wb = $null
$probe = $BuildRoot
for ($i = 0; $i -lt 6; $i++) {
    $probe = Split-Path -Parent $probe
    if ([string]::IsNullOrWhiteSpace($probe)) { break }
    if (Test-Path -LiteralPath (Join-Path $probe 'PCL-custom')) { $wb = $probe; break }
}
if ($wb -eq $null) { $wb = Split-Path -Parent (Split-Path -Parent $BuildRoot) }
$versionsRoot = Join-Path $wb 'PCL-custom\versions'

$seq = 1
if (Test-Path -LiteralPath $versionsRoot) {
    $nums = @()
    $patDay = '^' + [regex]::Escape($dayTag) + '-custom\.(\d+)$'
    foreach ($d in @(Get-ChildItem -LiteralPath $versionsRoot -Directory -ErrorAction SilentlyContinue)) {
        if ($d.Name -match $patDay) { $nums += [int]$Matches[1] }
    }
    if ($nums.Count -gt 0) {
        $maxSeq = [int](($nums | Measure-Object -Maximum).Maximum)
        if ($ReuseSeq) { $seq = $maxSeq } else { $seq = $maxSeq + 1 }
    }
}

if ([string]::IsNullOrWhiteSpace($Label)) {
    $Label = '{0}-custom.{1}' -f $dayTag, $seq
}
if ([string]::IsNullOrWhiteSpace($VerNum)) {
    $VerNum = '{0:yy}.{0:MM}.{0:dd}.{1}' -f $now, $seq
}

# 四段纯数字校验
if ($VerNum -notmatch '^\d{1,5}\.\d{1,5}\.\d{1,5}\.\d{1,5}$') {
    throw "VerNum 必须是四段纯数字（如 26.10.25.1），收到: $VerNum"
}

# ---------------------------------------------------------------------------
# 备份原文件（只备份一次，后续始终从备份出发）
# ---------------------------------------------------------------------------
foreach ($f in $targets) {
    $bak = "$f.verorig"
    if (-not (Test-Path -LiteralPath $bak)) {
        Copy-Item -LiteralPath $f -Destination $bak -Force
    }
}

# ---------------------------------------------------------------------------
# ModBase.vb
# ---------------------------------------------------------------------------
$info = Read-SourceText "$modBase.verorig"
$t = $info.Text

$patName = 'Public Const VersionBaseName As String = "[^"]*"'
if ($t -notmatch $patName) { throw '未找到 VersionBaseName 声明，上游可能已改动该常量名' }
$t = $t -replace $patName, ('Public Const VersionBaseName As String = "' + $Label + '"')

$patType = 'Public Const BuildTypeDisplay As String = "[^"]*"'
if ($t -match $patType) {
    $t = $t -replace $patType, 'Public Const BuildTypeDisplay As String = "测试版"'
}

if ($t -notmatch [regex]::Escape($Label)) { throw 'VersionBaseName 替换未生效' }
Write-SourceText $modBase $t $info.HasBom

# ---------------------------------------------------------------------------
# AssemblyInfo.vb
# ---------------------------------------------------------------------------
$infoA = Read-SourceText "$asmInfo.verorig"
$t2 = $infoA.Text

$patAsm = 'AssemblyVersion\("[^"]*"\)'
$patFile = 'AssemblyFileVersion\("[^"]*"\)'
if ($t2 -notmatch $patAsm) { throw '未找到 AssemblyVersion 特性' }
if ($t2 -notmatch $patFile) { throw '未找到 AssemblyFileVersion 特性' }

$t2 = $t2 -replace $patAsm, ('AssemblyVersion("' + $VerNum + '")')
$t2 = $t2 -replace $patFile, ('AssemblyFileVersion("' + $VerNum + '")')

if ($t2 -notmatch [regex]::Escape($VerNum)) { throw 'AssemblyVersion 替换未生效' }
Write-SourceText $asmInfo $t2 $infoA.HasBom

Write-Output ('{0}|{1}' -f $Label, $VerNum)
