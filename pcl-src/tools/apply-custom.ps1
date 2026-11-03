<#
.SYNOPSIS
    把「自用功能」改动覆盖回 PCL 源码树。

.DESCRIPTION
    pcl-src\tools 下的补丁分两层：

      tools\apply-patches.ps1   让上游源码能在本机（dotnet SDK 8）编得过，不带功能
      tools\apply-custom.ps1    本脚本，自用功能

    顺序不能反：先 apply-patches.ps1，再 apply-custom.ps1。

    custom\ 目录里保存的是改完之后的**完整文件**，按相对路径整份覆盖到 build\，
    不做文本替换。这样处理最省心，代价是以后若从上游同步新源码，
    这些文件需要人工重新比对一次上游改动。

    custom\ 中现存文件对应以下改动：
      Modules\ModCustom.vb            新增：主题预设、上游更新提示、内存优化、垃圾清理、官方服务凭据与到期提醒、自定义镜像、倒计时重启
      Modules\ModSecret.vb            转调 ModCustom；ServerLoader 改为检查上游 tag
      Modules\Base\ModNet.vb          镜像判断改用 ModCustom.IsMirrorUrl（支持自定义镜像）
      Modules\Minecraft\ModDownload.vb 镜像地址改用 ModCustom.MirrorBase()（27 处）
      FormMain.xaml                   窗口标题改为 pcl-lz
      FormMain.xaml.vb                去启动弹窗；凭据初始化；标题栏默认显示 pcl-lz 文本
      Application.xaml.vb             单例找回窗口时匹配新标题 pcl-lz
      Pages\PageSetup\Settings.vb               登记 Credential* 七条与 CustomDownloadMirror
      Pages\PageSetup\PageSetupUI.xaml          去掉 10 个主题单选按钮的 IsEnabled="False"
      Pages\PageSetup\PageSetupSystem.xaml      删「启动器」卡；每行独立确认/重置；新增「下载源（自定义镜像）」卡
      Pages\PageSetup\PageSetupSystem.xaml.vb   凭据与下载源的读写、到期状态刷新
      Pages\PageSetup\PageSetupLink.xaml(.vb)   重写「设置 → 联机」：第三方联机方案（默认陶瓦）
      Pages\PageSetup\PageSetupUI.xaml          两个「联机」勾选框去掉 IsEnabled="False"；删掉赞助遮罩，预设配色整块隐藏、调色滑块常显
      Modules\Minecraft\ModLaunch.vb            求赞助提示：测试版也弹，加「不再提示」（HintDonate）
      Pages\PageSetup\PageSetupUI.xaml.vb       恢复「联机」标签与栏目显示、两个勾选框联动
      Pages\PageLink\PageLinkMain.xaml(.vb)     联机页改为「联机方案」主体：名称 / 程序路径 / 一键启动 / 常用工具链接 / 自动启动开关
      Modules\Minecraft\ModLaunch.vb            游戏启动前按设置顺带拉起联机工具（McLaunchPrerun）
      Pages\PageOther\PageOtherTest.xaml(.vb)   重写百宝箱：内存优化 + 清理游戏垃圾 + 快捷文件夹（游戏目录/日志/崩溃/存档/模组/截图/启动器日志）
      Pages\PageOther\PageOtherLeft.xaml(.vb)    删掉「反馈」「新功能投票」（没有独立页面）两个入口
      Pages\PageOther\PageOtherAbout.xaml       关于卡片名称改为 pcl-lz
      Pages\PageLink\PageLinkMain.xaml.vb       联机报错改为陶瓦联机引导

.PARAMETER Check
    只比对不写入，列出哪些文件与 custom\ 不一致。

.PARAMETER BuildRoot
    源码树根目录，默认为脚本上一级目录下的 build。

.OUTPUTS
    每行一项：<状态> <相对路径>
    状态取值：COPIED（本次覆盖）/ SAME（内容一致，跳过）/ CHECK-DIFF / CHECK-SAME / FAIL

.EXAMPLE
    powershell -NoProfile -File tools\apply-custom.ps1 -Check
    powershell -NoProfile -File tools\apply-custom.ps1
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
$customRoot = Join-Path (Split-Path -Parent $PSScriptRoot) 'custom'

if (-not (Test-Path -LiteralPath $BuildRoot)) { throw "源码树不存在: $BuildRoot" }
if (-not (Test-Path -LiteralPath $customRoot)) { throw "自定义文件目录不存在: $customRoot" }

$targetBase = Join-Path $BuildRoot 'Plain Craft Launcher 2'
if (-not (Test-Path -LiteralPath $targetBase)) { throw "主项目目录不存在: $targetBase" }

function Get-FileHashSafe {
    param([string]$Path)
    if (-not (Test-Path -LiteralPath $Path)) { return '' }
    return (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash
}

$diff = 0
$same = 0
$fail = 0

$files = Get-ChildItem -LiteralPath $customRoot -Recurse -File | Sort-Object FullName
if ($files.Count -eq 0) {
    Write-Output "custom\ 下没有文件，无需处理。"
    exit 0
}

foreach ($f in $files) {
    $rel = $f.FullName.Substring($customRoot.Length).TrimStart('\')
    $target = Join-Path $targetBase $rel

    try {
        $srcHash = Get-FileHashSafe $f.FullName
        $dstHash = Get-FileHashSafe $target

        if ($srcHash -eq $dstHash) {
            Write-Output ('SAME         {0}' -f $rel)
            $same++
            continue
        }

        if ($Check) {
            $what = if ($dstHash -eq '') { '目标不存在' } else { '内容不同' }
            Write-Output ('CHECK-DIFF   {0}  ({1})' -f $rel, $what)
            $diff++
            continue
        }

        $parent = Split-Path -Parent $target
        if (-not (Test-Path -LiteralPath $parent)) {
            New-Item -ItemType Directory -Path $parent -Force | Out-Null
        }
        Copy-Item -LiteralPath $f.FullName -Destination $target -Force
        Write-Output ('COPIED       {0}' -f $rel)
        $diff++
    }
    catch {
        Write-Output ('FAIL         {0}  {1}' -f $rel, $_.Exception.Message)
        $fail++
    }
}

Write-Output ''
if ($fail -gt 0) {
    Write-Output "结果：$fail 项失败，$diff 项需覆盖，$same 项已一致。"
    exit 1
}
if ($Check) {
    if ($diff -eq 0) { Write-Output "结果：全部 $same 个文件已与 custom\ 一致。" }
    else { Write-Output "结果：$diff 项待覆盖，$same 项已一致（去掉 -Check 执行写入）。" }
}
else {
    Write-Output "结果：覆盖 $diff 项，跳过 $same 项。"
}
exit 0
