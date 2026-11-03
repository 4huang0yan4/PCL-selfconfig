#Requires -Version 5.1
<#
.SYNOPSIS
    回填历史归档的 build-info.txt：补 sha256 字段，并把 built 规范成 ASCII ISO 8601。

.DESCRIPTION
    2026-10-27 及更早的归档由旧版 build.cmd 生成，build-info.txt 只有 5 个字段，
    且 built 行形如 "2026/10/27 周二 15:44:14.59"——星期是 GBK 字节，任何 UTF-8
    读取都是乱码，也无法按时间排序。本脚本按目录内 exe 复算 SHA256 并重写该文件。

    只改 build-info.txt，不动 exe，不重编译。可反复执行：已是六字段且 built 为
    ISO 的目录会被跳过。

.PARAMETER Root
    versions 目录。默认取脚本所在位置推导：pcl-src\tools\..\..\PCL-custom\versions

.PARAMETER Apply
    真正写入。不带此开关时只打印将要做的改动（dry-run）。

.EXAMPLE
    powershell -NoProfile -File tools\backfill-build-info.ps1
    powershell -NoProfile -File tools\backfill-build-info.ps1 -Apply

.NOTES
    退出码：0 成功（含全部跳过）/ 1 有目录处理失败
    依赖：Windows PowerShell 5.1、Get-FileHash
    时区：历史时间统一按 +08:00 标注（本机为 UTC+8，中国无夏令时）
#>
[CmdletBinding()]
param(
    [string]$Root = '',
    [switch]$Apply
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Get-DefaultRoot {
    $tools = $PSScriptRoot
    $src = Split-Path -Parent $tools
    $gt = Split-Path -Parent $src
    return (Join-Path $gt 'PCL-custom\versions')
}

if ([string]::IsNullOrWhiteSpace($Root)) { $Root = Get-DefaultRoot }
if (-not (Test-Path -LiteralPath $Root)) {
    Write-Output "[ERROR] versions 目录不存在: $Root"
    exit 1
}

Write-Output ("Root : {0}" -f $Root)
Write-Output ("Mode : {0}" -f $(if ($Apply) { 'APPLY（真正写入）' } else { 'DRY-RUN（只预览，加 -Apply 才写）' }))
Write-Output ''

$ansi = [System.Text.Encoding]::Default
$utf8NoBom = New-Object System.Text.UTF8Encoding($false)

$rows = @()
$failed = 0
$updated = 0
$skipped = 0

foreach ($dir in (Get-ChildItem -LiteralPath $Root -Directory | Sort-Object Name)) {
    $label = $dir.Name
    $info = Join-Path $dir.FullName 'build-info.txt'
    $exe = Join-Path $dir.FullName 'Plain Craft Launcher 2.exe'

    if (-not (Test-Path -LiteralPath $info)) {
        $rows += [pscustomobject]@{ Label = $label; Action = 'SKIP'; Reason = '无 build-info.txt' }
        $skipped++
        continue
    }
    if (-not (Test-Path -LiteralPath $exe)) {
        $rows += [pscustomobject]@{ Label = $label; Action = 'FAIL'; Reason = '缺少 exe，无法算哈希' }
        $failed++
        continue
    }

    $text = [System.IO.File]::ReadAllText($info, $ansi)

    # 已是新格式则跳过（幂等）
    $hasSha = $text -match '(?m)^sha256='
    $isoBuilt = $text -match '(?m)^built=\d{4}-\d{2}-\d{2}T'
    if ($hasSha -and $isoBuilt) {
        $rows += [pscustomobject]@{ Label = $label; Action = 'SKIP'; Reason = '已是六字段/ISO' }
        $skipped++
        continue
    }

    # 取原字段（built 里的星期是 GBK 乱码，用 \D+ 跳过，只取日期与时间）
    $mLabel = [regex]::Match($text, '(?m)^label=(.+)$')
    $mAsm = [regex]::Match($text, '(?m)^assembly=(.+)$')
    $mBuilt = [regex]::Match($text, '(?m)^built=(\d{4})/(\d{1,2})/(\d{1,2})\D+(\d{1,2}:\d{2}:\d{2})')
    $mSrc = [regex]::Match($text, '(?m)^source=(.+)$')
    $mNote = [regex]::Match($text, '(?m)^note=(.+)$')

    if (-not $mBuilt.Success) {
        $rows += [pscustomobject]@{ Label = $label; Action = 'FAIL'; Reason = 'built 行无法解析' }
        $failed++
        continue
    }

    $newLabel = if ($mLabel.Success) { $mLabel.Groups[1].Value.Trim() } else { $label }
    $newAsm = if ($mAsm.Success) { $mAsm.Groups[1].Value.Trim() } else { '' }
    $newSrc = if ($mSrc.Success) { $mSrc.Groups[1].Value.Trim() } else { 'Plain Craft Launcher 2 open-source edition' }
    $newNote = if ($mNote.Success) { $mNote.Groups[1].Value.Trim() } else { '' }

    $d = $mBuilt.Groups
    $iso = ('{0}-{1:d2}-{2:d2}T{3}+08:00' -f $d[1].Value, [int]$d[2].Value, [int]$d[3].Value, $d[4].Value)
    $hash = (Get-FileHash -LiteralPath $exe -Algorithm SHA256).Hash.ToLowerInvariant()

    $lines = @(
        "label=$newLabel"
        "assembly=$newAsm"
        "built=$iso"
        "source=$newSrc"
        "note=$newNote"
        "sha256=$hash"
    )
    $outText = ($lines -join "`r`n") + "`r`n"

    if ($Apply) {
        [System.IO.File]::WriteAllText($info, $outText, $utf8NoBom)
    }

    $rows += [pscustomobject]@{
        Label  = $label
        Action = $(if ($Apply) { 'UPDATED' } else { 'PLAN' })
        Reason = ("built {0}  sha256 {1}..." -f $iso, $hash.Substring(0, 12))
    }
    $updated++
}

$rows | Format-Table -AutoSize -Property Label, Action, Reason | Out-String -Width 200 | Write-Output

Write-Output ("共 {0} 个归档：{1} {2}，跳过 {3}，失败 {4}" -f `
    $rows.Count, $(if ($Apply) { '已更新' } else { '待更新' }), $updated, $skipped, $failed)
if (-not $Apply -and $updated -gt 0) {
    Write-Output '确认无误后加 -Apply 执行写入。'
}
if ($failed -gt 0) { exit 1 }
exit 0
