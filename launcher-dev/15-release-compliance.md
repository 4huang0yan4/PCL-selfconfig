# 15 公开发布条件（PCL 分支）

状态：`[已发布]`（2026-10-31）。仓库 <https://github.com/4huang0yan4/pcl-shiguangxiugaiban> 已转 **Public**，
五条逐条满足并经「发布前自检」复核；**Mojang AppID 审核亦已通过**，正版登录可用。
本文件转为**发布后台账**：以后每次往仓库同步代码，仍按本文五条核对（尤其第 5 条）。

依据：上游 `Meloong-Git/PCL` 的 `LICENCE`（3168 字节），内容为《PCL 分发有限许可》+《PCL 存储库合理使用指南》。

> **2026-10-31 补充**：原文另存为 `gongtong\LICENCE.txt`（3,168 字节，LF）；与仓库根 `LICENCE`（3,218 字节，CRLF）**内容逐字一致，仅换行符不同**，`diff --strip-trailing-cr` 为空。
本分支**基于上游源码修改**，属指南定义的「**重度使用**」，需同时满足 5 条。

原文地址：`https://github.com/Meloong-Git/PCL/blob/main/LICENCE`
（本机 `curl` 走 schannel 握手失败、`github.com` 直连超时，需在浏览器里另存后放进仓库，**不要自己重打**）

---

## 一、五条要求逐条核对

### 第 1 条 明确表示是第三方二次创作 ✅

- 关于页第 3 位写着「PCL-拾光启动器 / 当前版本：…」，前两位是龙腾猫跃与 HerobrineXia
  （`Pages/PageOther/PageOtherAbout.xaml:21-24`）
- 界面标题栏与版本信息用 `PCL-拾光启动器`
- **待补**：仓库 README 与发布页需再写一句「非官方，第三方基于 PCL 的二次创作」

### 第 2 条 名称以 PCL 开头 + 表明第三方修改的后缀 ⚠️ 建议调整

指南允许：`PCL 某服务器魔改版`、`PCL PR Collection`、`PCL (XX Bug Fix)`、`PCL XXX修改版`
指南禁止：`PCL 3`、`PCL 手机版`、`PCL++`、`PCL Pro`（像官方后续作品）

当前名称 `PCL-拾光启动器` —— 以 PCL 开头、`拾光` 不像官方，**不算禁止项**，但后缀写的是「启动器」而非「修改版」，
严格对照允许示例偏弱。建议改为 **`PCL 拾光修改版`**，界面上可缩称「PCL 拾光」。

### 第 3 条 关于页首位署名 + 赞助链接、公开源码、继续用本文件 ⚠️ 部分满足

- ✅ 署名：`PageOtherAbout.xaml:21` 龙腾猫跃在 **Grid.Row="0"**（首位），头像用官方 `LTCat.jpg`
- ✅ 赞助链接：同页 `:39` 「赞助 PCL !」按钮（Row 0 同排）指向 `https://meloong.com/afd/a/LTCat`；
  另有 `:527` 「赞助作者」按钮
- ✅ 版权与免责：`:540` 保留「Copyright © 成都瓜皮龙科技有限公司 / 计算机软件著作权登记号 / 非 MINECRAFT 官方产品」
- ✅ **`LICENCE` 已放入仓库根**（3,168 字节，无扩展名，与上游一致）

### 第 4 条 正版购买劝导与赞助劝导（与 `McLaunchPrecheck` 一致或更激进）✅

- `Modules/Minecraft/ModLaunch.vb:293` 的 `McLaunchPrecheck` **完整保留**：
  `HintBuy` 分支、按启动次数（3/8/15/30…）弹正版劝导，与上游逻辑一致
- 赞助劝导保留，且**比官方更早触发**（官方只在 `BuildType=Release` 弹，本分支测试版也弹），符合"或更激进"

### 第 5 条 Windows 下不得有类似赞助解锁的功能或表现 ✅ 已修（2026-10-30 21:25）

条款原文：

> 例如：界面的主要色相必须为蓝色（即使没有修改功能，改为其他色相也和需要赞助解锁的主题功能类似）。

**处理方式：走方案 A —— 发布副本完整恢复上游行为，自用版保持原样**（双轨维护见 `16-dual-track.md`）。

改造明细（只动发布副本 `pcl-source-github\`）：

| 文件 | 改法 |
|---|---|
| `PageSetupUI.xaml` | 预设区 `IsEnabled="False"` 全部恢复；4 个配色滑块恢复 `Visibility="Collapsed"`；删掉"MyHint 说明"那句 |
| `ModSecret.vb` | `ThemeCheckAll` 改回**空实现**（上游行为） |
| `ModCustom.vb` | 删除 `EnsureCustomTheme` 与 `ThemeCheckAllImpl` |
| `FormMain.xaml.vb` | 删除 `ModCustom.EnsureCustomTheme()` 调用 |
| `custom\` 覆盖层 | `ModSecret.vb` / `ModCustom.vb` 同步改（否则 sync 会覆盖回解锁版） |

**验证（2026-10-30 21:25）**：
- 用 `WB_ROOT` + `PCL_TREE` 指向外部工具链编译发布副本：**4 个工程全部生成成功**，产物 5,926,400 字节
- 产物内 `本版本去掉了全部预设配色` / `解锁主题选项失败` / `EnsureCustomTheme` / `ThemeCheckAllImpl` **均 0 次命中**
- XAML `Visibility="Collapsed"` 22 处、`IsEnabled="False"` 10 处，与上游一致
  （差的 2 处是已删除的「联机页」隐藏复选框，与解锁无关）

**复验（2026-11-03 11:00，发布副本因 4 文件失同步修好后重跑，详见 `16-dual-track.md` 4.0.1）**：
- 4 个工程 `0 个错误`，产物 **6,124,544 字节**（比 10-30 大，因补回了 `DownloadPlugin` /
  `SpaceAnalyzeImpl` / `SystemSelfUpdate` 等功能，均与主题无关）
- exe 内四个标记字符串仍**全部 0 次命中**
- 两棵树（`build\` 与 `custom\`）解锁关键字 0 命中、`IsEnabled="False"` 10 处、
  `PanLauncherHide` 赞助遮罩保留 1 处
- `Visibility="Collapsed"` 现为 **23 处**（比 10-30 多 1 处，来自新加的隐藏项，方向是更隐蔽、不影响合规）

本分支现状（均有代码证据）：

| 位置 | 现状 | 问题 |
|---|---|---|
| `ModSecret.vb:137-140` | `ThemeCheckAll` 改为调用 `ModCustom.ThemeCheckAllImpl`（注释写着"放开所有主题选项，官方此处为空实现，是赞助解锁功能"） | 主题锁全部解除 |
| `ModCustom.vb:88-101` | 逐个 `IsEnabled = True`、隐藏 `LabLauncherTheme5Unlock` / `Theme8Copy` / `Theme9Copy` / `Theme11Click`、`RadioLauncherTheme5` 遮罩设为 `Opacity=1` | 解锁提示与遮罩被抹掉 |
| `ModCustom.vb:101` | 注释："赞助遮罩（`PanLauncherHide`）已整体删除" | 赞助遮罩没了 |
| `PageSetupUI.xaml:13` | 文案："**本版本去掉了全部预设配色**，直接用下面四个滑块自己调：**色调 0–360** 是色相环上的位置……" | 允许任意色相，等于把"赞助解锁的自定义主题"免费开放 |
| `ModCustom.vb:26-34` | 预设色相的 `ColorHue` 仍指向多套配色（210/175/130/42/28…） | 说明预设被移除但色板还在 |

**判定**：第 5 条的核心是"不要把赞助才能用的外观能力免费放开"。即使本分支没有商业目的，
只要允许用户把主要色相改成任意值，就与条款直接冲突。

**两条改造路径**（二选一）：

**A. 完整恢复上游行为（最稳，推荐）**
- `ModSecret.ThemeCheckAll` 恢复为空实现
- 删除 `ModCustom.ThemeCheckAllImpl` 的解锁逻辑（或整个不再调用）
- 恢复设置页的预设配色列表与赞助遮罩
- 代价：失去"自由调色相"这个自用功能

**B. 保留滑块但锁死在蓝色区间（折中）**
- 色相滑块范围从 `0–360` 收窄到 `190–230`（蓝色系），其余三项（饱和度/渐变/亮度）保留
- 移除预设配色列表里的非蓝色项
- 保留赞助遮罩与解锁提示
- 风险：条款写的是"主要色相必须为蓝色"，只要最终色相在蓝色内就算满足，但边界靠自行解释，
  **不如 A 稳**

---

## 二、其他发布前必做

| 项 | 说明 | 状态 |
|---|---|---|
| 仓库根放上游 `LICENCE` 原文 | 3168 字节，浏览器另存，不要改字 | ✅ 已放 |
| 第三方组件声明 | 见下表，MIT/BSD 类需保留版权声明 | ✅ `THIRD-PARTY-NOTICES.md` 已写 |
| 发布声明 | 对外说明身份、许可承诺、免责 | ✅ `声明.md` 已写 |
| **不要分发 `terracotta.exe`** | 陶瓦是 **AGPL-3.0**，一旦随包分发就要附源码与许可证。现状是运行时由启动器下载，**保持这样** | ✅ 现状合规 |
| 干净机编译验证（T-10） | 别人 clone 后能否编出来。`build.cmd` 硬依赖 `_tools\dotnet`，已在 `pcl-source-github-上传说明.md` 写明两种解法；**用 `WB_ROOT`+`PCL_TREE` 指向外部工具链已编译通过** | ✅ 2026-11-03 已在干净 Win10 虚拟机实测：T-B3 从零编译通过、T-B4 断网删 `obj`/`bin` 后仍通过 |
| 不含密钥 | `secrets.env` 等已在导出副本中物理剔除 | ✅ |
| 不含 MC 本体与资源 | 发布物只有 exe | ✅ |

### 第三方组件清单（`Resources\`）

| 组件 | 许可 | 处理 |
|---|---|---|
| Newtonsoft.Json | MIT | 保留版权声明 |
| NAudio | MIT | 同上 |
| CacheCow.Client / Common / Client.FileStore | MIT | 同上 |
| Imazen.WebP、`libwebp64.dll` | MIT / BSD | 同上 |
| Ookii.Dialogs.Wpf | BSD | 同上 |
| ThrottleDebounce | MIT | 同上 |
| System.Net.Http.Formatting | MS-PL | 同上 |
| MeloongCore（PCL 子模块） | 随上游 `LICENCE` | 同第 3 条 |
| **Terracotta（陶瓦）** | **AGPL-3.0** | **不随包分发**；在关于/联机页注明由第三方提供 |

---

## 三、发布顺序建议

1. 先做第 5 条改造（A 或 B），编译验证
2. 把上游 `LICENCE` 与 `声明.md` 放进仓库根目录，补 `THIRD-PARTY-NOTICES.md`
3. README 补「非官方 / 第三方二次创作」与名称说明
4. 干净机编译走一遍
5. 发 GitHub Release 时**只放 exe**，不放 terracotta、不放游戏资源

---

## 四、口径提醒

- 指南里有句"说人话的备注"：规则是为了阻止恶意使用、倒卖和山寨版，**常规善意的使用都没啥问题**。
  但第 5 条是明确的行为约束，不是态度问题，**不能靠"我没恶意"绕过**。
- 若对具体细节有疑问，官方给了提问入口：
  `https://github.com/Meloong-Git/PCL/discussions/new?category=提问`
  —— 真打算公开发布，**建议先去问一句**，比事后被要求下线省事。
