<#
.SYNOPSIS
    Write the keys from secrets.env into ModSecret.vb as inline literals.

.DESCRIPTION
    把 PCL-custom\secrets.env 里的 PCL_MS_CLIENT_ID / PCL_CURSEFORGE_API_KEY
    写进源码 Modules\ModSecret.vb 的内嵌字面量。重新编译后双击 exe 即生效，
    不再依赖任何启动脚本。

    幂等：值与源码中已有的相同则不改动文件。
    前置：源码需已应用 apply-patches.ps1 的第 7 项（Set-Keys.cmd 会自动调用）。

.PARAMETER EnvFile
    secrets.env 路径，默认 <工作区>\PCL-custom\secrets.env

.PARAMETER BuildRoot
    源码树根，默认 <脚本上级>\build

.PARAMETER Check
    只报告将要发生的改动，不写入

.OUTPUTS
    exit 0 = 已写入，需要重新编译
    exit 3 = 无变化，无需重编
    exit 2 = 两项密钥都为空，无可写入的值
    exit 1 = 出错
#>
[CmdletBinding()]
param(
    [string]$EnvFile = '',
    [string]$BuildRoot = '',
    [switch]$Check
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$projRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
if ([string]::IsNullOrWhiteSpace($EnvFile)) {
    $EnvFile = Join-Path $projRoot 'PCL-custom\secrets.env'
}
if ([string]::IsNullOrWhiteSpace($BuildRoot)) {
    $BuildRoot = Join-Path (Split-Path -Parent $PSScriptRoot) 'build'
}

$fSecret = Join-Path $BuildRoot 'Plain Craft Launcher 2\Modules\ModSecret.vb'
if (-not (Test-Path -LiteralPath $fSecret)) {
    Write-Output "[ERR] source not found: $fSecret"
    exit 1
}
if (-not (Test-Path -LiteralPath $EnvFile)) {
    Write-Output "[ERR] env file not found: $EnvFile"
    exit 1
}

# ---------------------------------------------------------------------------
# 解析 KEY=VALUE
# ---------------------------------------------------------------------------
$kv = @{}
foreach ($line in [System.IO.File]::ReadAllLines($EnvFile)) {
    $s = $line.Trim()
    if ($s.Length -eq 0) { continue }
    if ($s.StartsWith('#')) { continue }
    $i = $s.IndexOf('=')
    if ($i -lt 1) { continue }
    $k = $s.Substring(0, $i).Trim()
    $v = $s.Substring($i + 1).Trim()
    if (-not $k.StartsWith('PCL_')) { continue }
    if ($v.Length -ge 2 -and $v.StartsWith('"') -and $v.EndsWith('"')) {
        $v = $v.Substring(1, $v.Length - 2)
    }
    $kv[$k] = $v
}

$vOAuth = ''
if ($kv.ContainsKey('PCL_MS_CLIENT_ID')) { $vOAuth = [string]$kv['PCL_MS_CLIENT_ID'] }
$vCf = ''
if ($kv.ContainsKey('PCL_CURSEFORGE_API_KEY')) { $vCf = [string]$kv['PCL_CURSEFORGE_API_KEY'] }

if ($vOAuth.Length -eq 0 -and $vCf.Length -eq 0) {
    Write-Output '[SKIP] both keys are empty in secrets.env'
    Write-Output "       file: $EnvFile"
    exit 2
}

# ---------------------------------------------------------------------------
# 读写源码（保留 BOM 状态）
# ---------------------------------------------------------------------------
$bytes = [System.IO.File]::ReadAllBytes($fSecret)
$hasBom = ($bytes.Length -ge 3 -and $bytes[0] -eq 0xEF -and $bytes[1] -eq 0xBB -and $bytes[2] -eq 0xBF)
$text = [System.Text.Encoding]::UTF8.GetString($bytes)
if ($hasBom) { $text = $text.TrimStart([char]0xFEFF) }

function Get-FieldMatch {
    param([string]$Text, [string]$Field)
    $pat = 'Public ' + $Field + ' As String\s*=\s*"((?:[^"]|"")*)"'
    return [regex]::Match($Text, $pat)
}

function Format-Mask {
    param([string]$Value, [string]$Name)
    if ($Value.Length -eq 0) { return ('{0,-22} : (empty, skipped)' -f $Name) }
    $head = $Value.Substring(0, [Math]::Min(6, $Value.Length))
    return ('{0,-22} : {1}***  (len={2})' -f $Name, $head, $Value.Length)
}

$plan = New-Object System.Collections.Generic.List[object]
$changed = $false
$work = $text

foreach ($pair in @(
    @{ Field = 'OAuthClientId';    Value = $vOAuth; Name = 'PCL_MS_CLIENT_ID' },
    @{ Field = 'CurseForgeAPIKey'; Value = $vCf;    Name = 'PCL_CURSEFORGE_API_KEY' }
)) {
    $m = Get-FieldMatch -Text $work -Field $pair.Field
    if (-not $m.Success) {
        Write-Output ("[ERR] cannot find field " + $pair.Field + " in ModSecret.vb")
        Write-Output '      run apply-patches.ps1 first (patch 7 rewrites it to a literal)'
        exit 1
    }
    if ($pair.Value.Length -eq 0) {
        $plan.Add([pscustomobject]@{ Field = $pair.Field; Status = 'skip (empty)' })
        continue
    }
    $cur = $m.Groups[1].Value -replace '""', '"'
    if ($cur -eq $pair.Value) {
        $plan.Add([pscustomobject]@{ Field = $pair.Field; Status = 'unchanged' })
        continue
    }
    $escaped = $pair.Value -replace '"', '""'
    $newLine = 'Public ' + $pair.Field + ' As String = "' + $escaped + '"'
    $work = $work.Substring(0, $m.Index) + $newLine + $work.Substring($m.Index + $m.Length)
    $plan.Add([pscustomobject]@{ Field = $pair.Field; Status = 'update' })
    $changed = $true
}

Write-Output '[KEYS]'
Write-Output (Format-Mask $vOAuth 'PCL_MS_CLIENT_ID')
Write-Output (Format-Mask $vCf 'PCL_CURSEFORGE_API_KEY')
Write-Output ''
foreach ($p in $plan) {
    Write-Output ('  {0,-22} -> {1}' -f $p.Field, $p.Status)
}

if (-not $changed) {
    Write-Output ''
    Write-Output 'Result: no change, rebuild not needed.'
    exit 3
}

if ($Check) {
    Write-Output ''
    Write-Output 'Result: changes pending (-Check, nothing written).'
    exit 0
}

$enc = New-Object System.Text.UTF8Encoding($hasBom)
[System.IO.File]::WriteAllText($fSecret, $work, $enc)

Write-Output ''
Write-Output "Result: ModSecret.vb updated. Rebuild required."
exit 0
