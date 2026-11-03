# AGENTS.md — 给智能体与后续维护者的工作说明

> 这份文件是**入口**。若你是被指派来改这个仓库的智能体，先读完本节再动手。
> 本仓库是 **PCL2（Plain Craft Launcher 2）的个人分支源码快照**，私有仓库，单人维护，
> **不对接上游发布流程**，不接收 PR。所有判断以"本人实际使用"为准，不以"符合社区惯例"为准。
>
> 远端：`git@github.com:4huang0yan4/pcl-shiguangxiugaiban.git`（**走 SSH，不要改回 HTTPS**——
> 本机 HTTPS 经代理时 `git` 的 CONNECT 隧道会被 502 拒绝，SSH 才通）。
> 密钥在 `~/.ssh/id_ed25519_pcl`，已配进 `~/.ssh/config`。`git push` 直接可用，无需输入凭据。

---

## 一、先读这三份，再改代码

| 顺序 | 文件 | 为什么 |
|---|---|---|
| 1 | `launcher-dev/INDEX.md` | 文档索引，共 20+ 份，按需展开 |
| 2 | `launcher-dev/08-todo-risks.md` | **事实源**。最上面那条是最新续工锚点，写清了当前进度与坑 |
| 3 | `声明.md` + `LICENCE` | 许可边界。有几条是**硬约束**，不是建议 |

---

## 二、哪个才是真的源码（最容易搞错）

| 路径 | 作用 | 能改吗 |
|---|---|---|
| `pcl-src/build/Plain Craft Launcher 2/` | **实际编译的源码树** | ✅ 改这里 |
| `pcl-src/custom/` | 覆盖层：37 个完整文件的副本 | ✅ 必须与上行**成对修改** |
| `pcl-src/src/PCL-full/tree/` | 上游 2.99.0.0 原始快照，**仅供 diff 对照** | ❌ 只读，不参与编译 |
| `pcl-src/build/patches/` | 构建时打的上游补丁 | ⚠️ 动了会连锁 |
| `pcl-src/help-src/` | 自定义帮助（5 篇「指南」）的**可编辑源**，改完用 `tools\help-pack.py` 打进 `Resources\Help.zip` | ✅ |
| `pcl-src/tools/help-pack.py` | 帮助打包脚本（`py -3 tools\help-pack.py`） | ✅ |
| `pcl-src/_deprecated/` | 历史备份（Newtonsoft 版本、旧 Help.zip） | ⚠️ 只作参照，勿复制回 build |
| `pcl-src/build.cmd` | 构建入口 | ⚠️ |
| `launcher-dev/` | 项目文档 | ✅ |

**`custom/` 是目录，不是文件。** 同步时写成 `cp 文件 custom/Pages/PageLaunch/`，
路径少一层会生成一个同名**文件**，编译时才炸，且报错位置与真实原因无关。

---

## 三、编译

- 技术栈：**VB.NET + WPF**，`TargetFrameworkVersion v4.8`。
- `build.cmd` **硬依赖** `..\_tools\dotnet\dotnet.exe`（SDK 8.0.404 便携版）与
  `..\_tools\nuget`（net48 引用程序集缓存）。`_tools/` 约 916 MB，**不进仓库**（`.gitignore` 已排除）。
- 所以 **clone 下来直接跑会打印 `[ERROR] dotnet SDK not found`**，这不是仓库坏了。两条解法：
  1. 把 `_tools\` 整个放回 `pcl-src` 同级（最省事，与本人环境一致）；
  2. 自行装 .NET SDK 8 + net48 引用程序集，改 `build.cmd` 第 57–60 行的四个路径变量。
- 两个可覆盖变量：`WB_ROOT`（`_tools` 所在根）、`PCL_TREE`（build 目录）。
  编译**仓库外的副本**时用它们，不必复制工具链。
- **编译前先关掉正在运行的启动器**，否则输出被占用，日志报 `[FAIL]` 但错误数为 0。

---

## 四、硬约束（都是踩过的，别重犯）

1. **新增设置项必须先注册**：在 `Pages\PageSetup\Settings.vb` 里加键，否则 `Settings.Get`
   抛 `KeyNotFoundException`。若抛在控件 `Loaded` 里，症状是**启动器一打开就报「未知错误」然后退出**，
   与设置毫无关联，极难定位。
2. **联机代码只放顶部联机页**（`PageLinkMain` / `CompTerracotta`）。「设置 → 联机」整页已删除，
   `PageSetupLink.xaml` **不存在**，别再往里加东西。
3. `PageLinkMain.xaml.vb` 只有 ~91 行是**正常的**，不是被截断。官方 EasyTier 联机的死代码已全部清除，
   需要找回时看 `launcher-dev/13-cleanup-linkpage.md`。
4. **陶瓦守护进程刚拉起时会先报一次空异常**（日志形如 `（无） → exception`）。
   在没有房间码/地址的阶段属正常，**不要**译成"连接失败"。
5. 陶瓦的 `url` 只在 `guest-ok` 事件里出现，且是**游客本机的回环地址**；
   房主没有可以发给别人的直连地址。别照着实现"房主分享链接"。
6. **不得提供与上游赞助解锁相类似的功能。** `LICENCE` 第 5 条把「界面主要色相可自由更改」
   也视为赞助解锁的等价物。`ModSecret.ThemeCheckAll` 必须保持空实现，不要给它接任何
   自定义主题实现。详见 `声明.md` 第 5 条。
7. **不要提交密钥。** `Modules\ModSecret.vb` 里 `OAuthClientId` / `CurseForgeAPIKey` 保持空串。
   真值只走环境变量（`PCL_MS_CLIENT_ID` / `PCL_CURSEFORGE_API_KEY`）或本地 `secrets.env`
   （已在 `.gitignore`）。提交前自查：`git diff --cached | grep -iE "api.?key|client.?id|secret|token"`。
8. **不要提交运行物**：`_tools/`、`PCL-custom/`、`.minecraft/`、`PCL/`、`obj/`、`bin/`。
9. **`robocopy /E` 只增不删。** 做同步/备份时目标端多余文件不会自动清理，必须显式处理。

---

## 五、必须与上游保持一致的 4 个文件

这 4 个文件在本仓库里**与上游 2.99.0.0 完全一致**，是 `LICENCE` 第 5 条的直接落点，
动之前先想清楚：

| 文件 | 当前状态（= 上游） |
|---|---|
| `Pages/PageSetup/PageSetupUI.xaml` | 预设配色按赞助等级锁定、自定义配色滑块 `Visibility="Collapsed"` |
| `Modules/ModSecret.vb` | `ThemeCheckAll` 空实现 |
| `Modules/ModCustom.vb` | 无 `ThemeCheckAllImpl` / `EnsureCustomTheme` |
| `FormMain.xaml.vb` | 启动时不调用 `EnsureCustomTheme()` |

**不要把它们改成"解开主题、色相 0–360 随便调"的版本。** 上游把「界面主要色相可自由更改」
明确列为与赞助解锁类似的表现，放开即违背第 5 条。其余功能（联机、整合包、导出、下载、百宝箱）
与上游的差异不受此限。

**帮助内容**：本仓库在 `Resources\Help.zip` 内内置了 5 篇自定义「指南」（70 → 80 条），
源文件在 `pcl-src/help-src/`，用 `tools\help-pack.py` 打包。**不要再往 `<exe 同级>\PCL\Help\` 放同名文件**——
外部与内置不会互相覆盖，会并列出现两条。

---

## 六、当前状态（截至 2026-10-30）

- 版本基线：`2026.11.02-custom.31`（本地归档在备份盘，不在本仓库）；2026-11-02 已并入服务端安装重构与实机验收。
- **联机结论（已验证）**：以陶瓦（Terracotta）为主的启动器都能互相加——自编版 ↔ 自编版、
  自编版 ↔ HMCL 均跨公网直连通过；HMCL / FCL / ZL2 / FMCL / MoLaunch / Axolotl 都直接内置陶瓦，
  同一内核。**PCL CE 是唯一例外**：它用自研 EasyTier 客户端接陶瓦房间，受 `--p2p-only` 限制，
  **只有同一局域网内能通，跨公网不行**。别把这条写成"与 CE 互通"就完事——会误导人。
- **仍缺**：游戏内互相可见性没有截图，现有证据都是启动器侧成员列表。
- 第三方组件与许可：见 `THIRD-PARTY-NOTICES.md`。**陶瓦是 AGPL-3.0，不要打进分发包**，
  现状是运行时按需下载，保持这样。

---

## 七、改完怎么验

1. 编译通过后，核对 exe 里**不该出现**的字符串确实消失（例如被删功能的提示文案）。
   中文字符串在 .NET 里可能是 UTF-16LE，`grep` 直接搜 UTF-8 会漏。
2. 涉及界面的改动，实机看一眼，不要只看代码。本人对"界面说的和事实不一致"零容忍：
   **标签必须预告真实结果**，不要用兜底逻辑篡改用户刚做的选择。
3. 改完在 `launcher-dev/08-todo-risks.md` 顶部补一条锚点：改了什么、为什么、怎么验、
   哪些**只到编译没实机跑过**。

---

## 八、跟进更新（给智能体的操作单）

**本仓库不发布二进制（exe / zip 都不发）。** 需要更新时，由智能体按下面顺序做；推完源码与 tag 后还要建 Release（只写变更，见本节末尾）。

1. `git pull`（或重新 clone）拿到最新源码，先读 `CHANGELOG.md` 确认这次改了哪些文件；
2. 同步到本地**自用树**时，**不要整拷这 4 个文件**：
   `Pages/PageSetup/PageSetupUI.xaml`、`Modules/ModSecret.vb`、`Modules/ModCustom.vb`、`FormMain.xaml.vb`。
   仓库版是合规版（预设锁回、`ThemeCheckAll` 空实现），整拷会把发布版行为带回自用版；
3. 其余文件可按相对路径覆盖；新增文件记得同步 `Plain Craft Launcher 2.vbproj` 的 `<Compile Include>`；
4. 编译 `pcl-src\build.cmd`（依赖未入库的 `_tools\`，见第三节）；
5. 部署到运行目录，回读 `sha256` 与 `versions\<label>\build-info.txt` 核对；
6. 自用版改动不写进本仓库；只有确定要公开的改动才更新 `CHANGELOG.md` 并推。

**发布版（Release）规则（2026-11-02 用户改定，取代 10-31 的「永久不建」）**：每次功能更新推源码 + 打 tag 后，**同时建一个 GitHub Release**——标题用 tag 名（版本号），正文直接用 `CHANGELOG.md` 对应小节；**不上传任何二进制附件**（exe / zip 都不传），GitHub 自动附的 Source code zip 就是发布物，符合开源分发。`Modules/UpdateSelf.vb`（默认关闭，设置 → 其他 →「更新」）查 `releases/latest` 因此能读到最新版信息。

**tag 命名（2026-11-01 起改）**：不再用 `1` / `2` / `3` 这种序号——光看 tag 名不知道改了什
么。现在 **tag 名 = 完整版本号**，与 `Plain Craft Launcher 2.exe` 启动日志里的版本行、以及
`CHANGELOG.md` 的小节标题一致，例如 `2026.11.01-custom.22`。日期在前，GitHub 的 tag 列表按
名称排序即按时间排序。

打 tag 时必须同时满足两条，否则「一眼看不出更新了什么」：

1. **annotated tag，注释写一行摘要**：`git tag -a 2026.11.01-custom.22 -m "一句话说清这版干了什么"`；
2. **`CHANGELOG.md` 顶部「版本索引」表加一行**：tag / 日期 / 一句话内容 / 含哪些功能点，
   这张表就是发布目录，按版本倒序。

历史 tag `1` / `2` / `3` 保留不动（已公开，不重写），它们的含义记在同一张索引表里。

**什么时候提交**（2026-10-31 定）——不要每改一点就 commit，否则历史里全是半截提交：

| 触发 | 提交 | 推送 | tag |
|---|---|---|---|
| 大幅更新功能 | 代码 + 同步文档，**合为一笔** | ✅ | ✅ 打**版本号** tag（见上） |
| 修补漏洞 / 明显 bug | 同上 | ✅ | ✅ |
| 说明文档（CHANGELOG / README / 使用说明） | **跟随功能一起提交，不单独成笔** | 随功能 | ❌ |
| 小改动、措辞、注释、规则说明 | 攒着，等下次功能更新一并提交 | 随功能 | ❌ |

推送由维护者拍板；未推送的改动可 `--amend` 合并，已推送的历史不重写。
