#requires -Version 5.1
<#
.SYNOPSIS
    PCL-拾光启动器 安全扫描：依赖漏洞 / 敏感信息落盘 / 产物完整性。

.DESCRIPTION
    三段检查，各自独立可单独跑：

      1. deps  依赖漏洞 —— 从 vbproj 的 <Reference Include="...Version=x.y.z"> 提取组件，
               逐个查 OSV 漏洞库（https://api.osv.dev，NuGet 生态，免密钥）。
               本工程是「老式非 SDK 工程 + 手工 dll 引用」，dotnet list package --vulnerable
               对它无效，所以改走这条路径。
      2. code  敏感信息落盘 —— 参考 PCL CE 的 CVE-2025-54120（凭据被写进日志，CVSS 9.3）
               与常见误配：证书校验被关、弱 TLS、密钥硬编码。
      3. hash  产物完整性 —— 对 PCL-custom 下的 exe 算 SHA-256，便于区分「我编的」与「别人给的」。

.PARAMETER Only
    all（默认）| deps | code | hash

.PARAMETER Offline
    不联网，依赖检查改用脚本内置的已知漏洞表（覆盖不了全量，只兜底）。

.PARAMETER Json
    以 JSON 输出结果，便于其它工具消费。

.EXAMPLE
    .\tools\scan.ps1
    .\tools\scan.ps1 -Only deps
    .\tools\scan.ps1 -Offline -Json

.NOTES
    退出码：0 = 未发现高危；1 = 发现高危（deps 有 Critical/High 或 code 命中）；
            2 = 脚本自身出错（找不到工程文件等）。
#>

[CmdletBinding()]
param(
    [ValidateSet('all', 'deps', 'code', 'hash')]
    [string]$Only = 'all',
    [switch]$Offline,
    [switch]$Json
)

Set-StrictMode -Version 1.0   # 只查未初始化变量；OSV 返回的 JSON 结构不固定，属性级严格模式会误报
$ErrorActionPreference = 'Stop'

# ── 路径：向上找含 PCL-custom 的祖先作工作空间根，源码放在哪都能跑 ──────────────
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$ProjRoot = Split-Path -Parent $ScriptDir          # pcl-src
$WS = $null
$p = $ProjRoot
for ($i = 0; $i -lt 6 -and $p; $i++) {
    if (Test-Path (Join-Path $p 'PCL-custom')) { $WS = $p; break }
    $p = Split-Path -Parent $p
}
if (-not $WS) { Write-Error '未找到含 PCL-custom 的工作空间根目录；请用 -ProjRoot 指定或检查目录结构。'; exit 2 }

$ProjFile = Join-Path $ProjRoot 'build\Plain Craft Launcher 2\Plain Craft Launcher 2.vbproj'
if (-not (Test-Path $ProjFile)) { Write-Error "找不到工程文件：$ProjFile"; exit 2 }

$SrcDir = Join-Path $ProjRoot 'build\Plain Craft Launcher 2'
$CustomDir = Join-Path $ProjRoot 'custom'
$ExePath = Join-Path $WS 'PCL-custom\Plain Craft Launcher 2.exe'

$results = [ordered]@{
    timestamp = (Get-Date).ToString('yyyy-MM-dd HH:mm:ss')
    workspace = $WS
    deps      = @()
    code      = @()
    hash      = $null
}
$highRisk = 0

# ══════════════════════════════════════════════════════════════════════════════
# 1. 依赖漏洞
# ══════════════════════════════════════════════════════════════════════════════

# 系统/框架程序集不是 NuGet 包，查了也是白查
$SystemPrefixes = @(
    'System', 'Microsoft', 'mscorlib', 'Windows', 'PresentationCore', 'PresentationFramework',
    'UIAutomation', 'Accessibility', 'ReachFramework', 'ISymWrapper', 'MeloongCore', 'PCLCS'
)

# 离线兜底表：只放「已确认存在漏洞且本项目确实引用」的条目，宁缺毋滥
$OfflineVulns = @(
    @{ name = 'Newtonsoft.Json'; below = '13.0.1'; id = 'GHSA-5crp-9r3c-p9vr'; severity = 'High'
       summary = '嵌套层级过高时栈溢出/CPU 与内存耗尽（DoS），13.0.1 修复。' }
)

function Get-PackageRefs {
    param([string]$Path)

    $xml = [xml](Get-Content -Raw -Encoding UTF8 $Path)
    $ns = New-Object System.Xml.XmlNamespaceManager($xml.NameTable)
    $ns.AddNamespace('m', 'http://schemas.microsoft.com/developer/msbuild/2003')

    $refs = @()
    foreach ($node in $xml.SelectNodes('//m:Reference', $ns)) {
        $include = $node.GetAttribute('Include')
        if (-not $include) { continue }
        # 形如 "Newtonsoft.Json, Version=13.0.0.0, Culture=neutral, PublicKeyToken=..."
        $parts = $include -split ','
        $name = $parts[0].Trim()
        $verRaw = ($parts | Where-Object { $_ -match '^\s*Version\s*=' } | Select-Object -First 1)
        if (-not $verRaw) { continue }
        $ver = ($verRaw -split '=')[1].Trim()

        if ($SystemPrefixes | Where-Object { $name -like "$_*" }) { continue }
        if ($name -in $SystemPrefixes) { continue }

        # 程序集版本 13.0.0.0 → NuGet 版本 13.0.0（去掉末尾多余的 .0，但保留三段）
        $seg = @($ver -split '\.' | Where-Object { $_ -ne '' })
        while ($seg.Count -gt 3 -and $seg[-1] -eq '0') { $seg = $seg[0..($seg.Count - 2)] }
        $nugetVer = $seg -join '.'

        # 有些包把程序集版本永久钉住（Newtonsoft.Json 13.0.3 的程序集仍是 13.0.0.0），
        # 只按程序集版本查 OSV 会一直报旧版本的漏洞。Resources 下有同名 dll 时以文件版本为准。
        $dll = Join-Path (Split-Path -Parent $Path) ("Resources\" + $name + ".dll")
        if (Test-Path -LiteralPath $dll) {
            try {
                $fv = [System.Diagnostics.FileVersionInfo]::GetVersionInfo($dll).FileVersion
                $fvSeg = @(($fv -split '\.') | Where-Object { $_ -ne '' })
                if ($fvSeg.Count -ge 3) {
                    $fileVer = ($fvSeg[0..2] -join '.')
                    if ((Compare-VersionPart $fileVer $nugetVer) -gt 0) { $nugetVer = $fileVer }
                }
            } catch { }
        }

        $refs += [pscustomobject]@{ Name = $name; AssemblyVersion = $ver; NuGetVersion = $nugetVer }
    }
    return $refs
}

function Compare-VersionPart {
    param([string]$A, [string]$B)
    $pa = @($A -split '\.' | ForEach-Object { [int]($_ -replace '[^0-9]', '0') })
    $pb = @($B -split '\.' | ForEach-Object { [int]($_ -replace '[^0-9]', '0') })
    for ($i = 0; $i -lt [Math]::Max($pa.Count, $pb.Count); $i++) {
        $x = if ($i -lt $pa.Count) { $pa[$i] } else { 0 }
        $y = if ($i -lt $pb.Count) { $pb[$i] } else { 0 }
        if ($x -lt $y) { return -1 }
        if ($x -gt $y) { return 1 }
    }
    return 0
}

function Query-Osv {
    param([string]$Package, [string]$Version)

    $body = @{ package = @{ name = $Package; ecosystem = 'NuGet' }; version = $Version } | ConvertTo-Json -Depth 5
    try {
        $resp = Invoke-RestMethod -Method Post -Uri 'https://api.osv.dev/v1/query' `
            -ContentType 'application/json' -Body $body -TimeoutSec 20
    } catch {
        return $null      # 网络失败交由调用方降级
    }
    if ($null -eq $resp -or $null -eq $resp.vulns) { return @() }

    $list = @()
    foreach ($v in $resp.vulns) {
        $sev = 'Unknown'
        if ($v.database_specific -and $v.database_specific.severity) { $sev = $v.database_specific.severity }
        elseif ($v.aliases -and ($v.aliases | Where-Object { $_ -like 'CVE-*' })) { $sev = 'CVE' }
        $fixed = @()
        foreach ($aff in $v.affected) {
            foreach ($r in $aff.ranges) {
                foreach ($e in $r.events) { if ($e.fixed) { $fixed += $e.fixed } }
            }
        }
        $list = @($list) + @([pscustomobject]@{
            Id       = $v.id
            Severity = $sev
            Fixed    = ($fixed | Select-Object -Unique) -join ', '
            Summary  = ($v.summary -replace '\s+', ' ')
            Aliases  = ($v.aliases -join ', ')
        })
    }
    return $list
}

if ($Only -eq 'all' -or $Only -eq 'deps') {
    $refs = Get-PackageRefs -Path $ProjFile
    $anyOffline = $false
    foreach ($r in $refs) {
        $vulns = @()
        $useOffline = $Offline            # 是否改用内置表：按包独立判断，避免一个包失败就让全表叠加
        if (-not $useOffline) {
            $got = Query-Osv -Package $r.Name -Version $r.NuGetVersion
            if ($null -eq $got) { $useOffline = $true; $anyOffline = $true } else { $vulns = $got }
        }
        if ($useOffline) {
            foreach ($ov in $OfflineVulns) {
                if ($r.Name -eq $ov.name -and (Compare-VersionPart $r.NuGetVersion $ov.below) -lt 0) {
                    $vulns = @($vulns) + @([pscustomobject]@{ Id = $ov.id; Severity = 'High'; Fixed = $ov.below; Summary = $ov.summary; Aliases = '' })
                }
            }
        }
        foreach ($v in $vulns) {
            if ($v.Severity -in @('CRITICAL', 'HIGH', 'Critical', 'High')) { $highRisk++ }
            $results.deps = @($results.deps) + @([pscustomobject]@{
                package = $r.Name; version = $r.NuGetVersion; id = $v.Id
                severity = $v.Severity; fixed = $v.Fixed; summary = $v.Summary
            })
        }
        if (-not $Json) {
            if ($vulns.Count -eq 0) {
                Write-Host ("  OK   {0,-28} {1}" -f $r.Name, $r.NuGetVersion) -ForegroundColor DarkGray
            } else {
                Write-Host ("  RISK {0,-28} {1}" -f $r.Name, $r.NuGetVersion) -ForegroundColor Yellow
            }
        }
    }
    if ($anyOffline -and -not $Json) {
        Write-Host '  （部分依赖查询失败，已用内置漏洞表兜底）' -ForegroundColor DarkYellow
    }
}

# ══════════════════════════════════════════════════════════════════════════════
# 2. 敏感信息落盘 / 危险配置
# ══════════════════════════════════════════════════════════════════════════════

if ($Only -eq 'all' -or $Only -eq 'code') {
    $rules = @(
        # 只报「真的把凭据变量拼进日志」的行；纯文字提到 AccessToken 不算（上游就有这种说明性日志）
        @{ id = 'SEC-001'; severity = 'High'
           desc = '日志里拼接了令牌 / 凭据变量'
           pattern = '(Log|ProfileLog|Hint)\s*\([^)]*(&|\$\{)[^)]*\b(AccessToken|ClientToken|RefreshToken|ClientSecret|CredentialCfKey|CredentialMsId)\b'
           exclude = 'MaskTokenForLog|FilterAccessToken'
           hint = '上游 FilterAccessToken 只给 AccessToken 留前 5 后 5 位，且完全不处理 ClientToken；应改为只输出长度。' }
        @{ id = 'SEC-002'; severity = 'High'
           desc = '请求头里硬编码密钥'
           pattern = 'Headers\.Add\s*\(\s*"[xX]-api-key"\s*,\s*"[^"]+"'
           hint = '密钥应来自设置/加密存储，不要写字面量。' }
        @{ id = 'SEC-003'; severity = 'Critical'
           desc = '关掉了 TLS 证书校验'
           pattern = 'ServerCertificateValidationCallback\s*=\s*(Function|AddressOf|\{[^}]*Return\s+True)'
           hint = '禁止无条件返回 True；测试代码不要进主干。' }
        @{ id = 'SEC-004'; severity = 'Medium'
           desc = '仍启用了 Ssl3 / Tls1.0 / Tls1.1 等已废弃协议'
           pattern = 'SecurityProtocol[^\r\n]*(Ssl3|Tls11|\bTls\b)'
           hint = '评估后再收紧：删掉 Ssl3/Tls 可能让老镜像站连不上，建议保留 Tls12 起、逐步移除 Ssl3。' }
        @{ id = 'SEC-005'; severity = 'High'
           desc = '内置密钥未留空（ModSecret 应保持空壳）'
           pattern = '(OAuthClientId|CurseForgeAPIKey)\s*=\s*"[^"]+"'
           hint = '密钥走界面填写或 secrets.env，不进源码。' }
    )

    $files = @()
    $files += Get-ChildItem -Path $SrcDir -Filter '*.vb' -Recurse -File -ErrorAction SilentlyContinue
    if (Test-Path $CustomDir) {
        $files += Get-ChildItem -Path $CustomDir -Filter '*.vb' -Recurse -File -ErrorAction SilentlyContinue
    }
    $files = $files | Sort-Object FullName -Unique

    # custom\ 下的文件会被 apply-custom 覆盖到 build\ 树，同一处问题会命中两次；
    # 用「去掉 build/custom 前缀后的相对路径 + 行号 + 规则」做去重键。
    $seen = @{}
    foreach ($f in $files) {
        $text = Get-Content -Raw -Encoding UTF8 $f.FullName -ErrorAction SilentlyContinue
        if (-not $text) { continue }
        $lines = $text -split "`r?`n"
        $rel = $f.FullName.Substring($WS.Length + 1)
        $relKey = $rel -replace '^pcl-src\\(build\\Plain Craft Launcher 2|custom)\\', ''
        foreach ($rule in $rules) {
            for ($i = 0; $i -lt $lines.Count; $i++) {
                if ($lines[$i] -match $rule.pattern) {
                    if ($rule.exclude -and $lines[$i] -match $rule.exclude) { continue }
                    $key = "$relKey|$($i + 1)|$($rule.id)"
                    if ($seen.ContainsKey($key)) { continue }
                    $seen[$key] = 1
                    $results.code = @($results.code) + @([pscustomobject]@{
                        id = $rule.id; severity = $rule.severity; file = $rel
                        line = ($i + 1); snippet = $lines[$i].Trim(); hint = $rule.hint
                    })
                    if ($rule.severity -in @('Critical', 'High')) { $highRisk++ }
                }
            }
        }
    }

    if (-not $Json) {
        if ($results.code.Count -eq 0) {
            Write-Host '  OK   未发现敏感信息落盘 / 危险配置' -ForegroundColor DarkGray
        } else {
            foreach ($c in $results.code) {
                Write-Host ("  RISK [{0}] {1}:{2}  {3}" -f $c.id, $c.file, $c.line, $c.snippet) -ForegroundColor Yellow
            }
        }
    }
}

# ══════════════════════════════════════════════════════════════════════════════
# 3. 产物哈希
# ══════════════════════════════════════════════════════════════════════════════

if ($Only -eq 'all' -or $Only -eq 'hash') {
    if (Test-Path $ExePath) {
        $h = (Get-FileHash -Path $ExePath -Algorithm SHA256).Hash
        $len = (Get-Item $ExePath).Length
        $results.hash = [pscustomobject]@{ path = $ExePath; size = $len; sha256 = $h }
        if (-not $Json) {
            Write-Host ("  SHA256 {0}" -f $h) -ForegroundColor DarkGray
            Write-Host ("  大小   {0:N0} 字节" -f $len) -ForegroundColor DarkGray
        }
    } elseif (-not $Json) {
        Write-Host '  （未找到 exe，跳过哈希）' -ForegroundColor DarkYellow
    }
}

# ── 汇总 ──────────────────────────────────────────────────────────────────────
# 高危数按结果重算，不依赖循环里的累加（累加容易因去重/降级分支算歪）
$highRisk = 0
$highRisk += @($results.deps | Where-Object { $_.severity -in @('CRITICAL', 'HIGH', 'Critical', 'High') }).Count
$highRisk += @($results.code | Where-Object { $_.severity -in @('Critical', 'High') }).Count

if ($Json) {
    $results | ConvertTo-Json -Depth 6
} else {
    Write-Host ''
    if ($highRisk -gt 0) {
        Write-Host "结果：发现 $highRisk 项高危，建议处理后再归档。" -ForegroundColor Red
    } else {
        Write-Host '结果：未发现高危项。' -ForegroundColor Green
    }
    Write-Host '提示：deps 查的是「组件是否有已知 CVE」，不代表一定可利用；code 是正则匹配，需人工复核误报。' -ForegroundColor DarkGray
}

exit $(if ($highRisk -gt 0) { 1 } else { 0 })
