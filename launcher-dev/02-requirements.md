# 02 需求与优先级

状态：`[已确认]`（优先级由用户 2026-10-27 指定）
最后更新：2026-10-30

---

## 优先级总表

用户指定顺序，不可随意调整：

| 序 | 需求 | 编号 | 现状 | 状态 |
|---|---|---|---|---|
| 1 | 版本隔离 + Java 分版本指定 | R1 | **已完成**（隔离 + Java 逐实例已验证） | `[已确认]` 2026-10-27 |
| 2 | MCBBS v2 整合包导出 | R2 | **已实现，四端导入全部验证通过**：PCL / HMCL 3.15.2 / Prism（2026-10-30），Forge·NeoForge·Fabric 三分支均验 | `[✅ 已确认 2026-10-30]` |
| 3 | Terracotta 联机集成 | R3 | **已在启动器内完成**（建房/加入/复位/关闭 + 自动安装） | `[已确认]` 单机全通；跨机 T-01/T-02 已实测通过 |
| 4 | Modrinth `.mrpack` | R4 | **导出已由上游实现**，只剩导入未验证 | `[部分完成]` |
| 5 | 安全扫描管线 | R5 | **已落地** `tools\scan.ps1`（依赖 OSV 查询 + 5 条规则 + 产物哈希） | `[已确认]` 2026-10-27 |
| — | 联机可插拔抽象 | R6 | **暂缓**（只有一个实现，抽接口是空转） | `[已决策]` 见 `06` 第十节 |
| — | Scaffolding-MC 互通 | R7 | **双向实测通过**：陶瓦 0.4.2 房间可被 PCL CE 2.15.1 加入，反之亦然，延迟 9 ms | `[✅ 已确认 2026-10-30]` |
| — | 自主修复能力（接管下载/安装） | R8 | **下载链路已实测通过**，另修 3 个 bug（环境变量冲突、令牌进日志、遮罩透光） | `[已确认]` 2026-10-27 |

---

## R1 版本隔离 + Java 分版本指定

**要什么**：每个实例独立的 mods / saves / config / 资源包，互不干扰；每个实例可指定不同 JDK。

**现状**：`[已完成 2026-10-27]`

**版本隔离**（PCL 原生）：每个 `versions\<名称>\` 就是该实例的游戏目录，mods/saves/config 都在里面；实例级配置写在同目录的 `PCL\Setup.ini`（UTF-8、`键:值`）。

**Java 逐实例 —— 已实测确认**。机制有两层，都按实例存储：

| 设置方式 | 键 | 存放位置 |
|---|---|---|
| `VersionArgumentJavaV2`（0 自动 / 1 按版本范围 / 2 用版本文件夹内 Java / 3 强制指定） | `VersionArgumentJavaV2` + `VersionArgumentJavaRange` | 实例目录的 `PCL\Setup.ini` |
| 方式 3 具体选了哪个 Java | `InstanceForcedJava` | `Configs.JavaForced`（`DynamicConfigEntry`，以实例为 provider） |

**验证过程**（不靠界面点击，直接改 ini 再读日志）：

1. 给 `26.3-Fabric 0.20.1` 的 ini 写 `VersionArgumentJavaV2:2` → 启动该实例，日志出现
   `[ModJava] 版本设置要求使用版本文件夹中的 Java：…\.minecraft\versions\26.3-Fabric 0.20.1\`
2. 还原，改给 `26.3` 写同样的键 → 启动 26.3，日志变成
   `…\.minecraft\versions\26.3\`，并提示"版本文件夹里没能找到 Java"

两条日志的路径各自精确指向自己的实例目录 → **逐实例读取成立，互不干扰**。测试后已还原两个 ini，无残留。

**关于"换启动器后是否生效"**：配置在**版本目录内部**（跟着实例走），所以文件不会丢；但只有 PCL 会读它，HMCL/Prism 有各自的 Java 设置。这不是缺陷，是各启动器的设置格式不同。

**未测**：多实例**并行**启动时内存/Java 参数是否互相影响（需同时跑两个游戏，内存压力大）。风险低——参数是启动时快照传给 java 进程的，进程之间无共享状态。

**验收**：`[已通过]` 两个实例各读自己的 Java 设置，日志路径一一对应。

## R2 MCBBS v2 整合包导出（国内主格式）

**要什么**：导出 `mcbbs.packmeta`，并同包含 `manifest.json`（CurseForge 格式），使 MultiMC/Prism 下游也能导入。

**现状**：`[✅ 已确认 2026-10-30，四端验证通过]` **PCL / HMCL 3.15.2 / Prism 都能识别并导入**，Forge · NeoForge · Fabric 三个分支均已验过。做法：导出页高级选项新增「额外写入 MCBBS v2 元数据」，勾选后在包根写入 `mcbbs.packmeta` 与 `manifest.json`，原 PCL 格式内容不变。

实测样本 `launcher-dev\evidence\sample-26.3-fabric.mcbbs.zip`（1.7 MB）：

```json
// mcbbs.packmeta
"addons": [ {"id":"game","version":"26.3"}, {"id":"fabric","version":"0.20.1"} ]
// manifest.json
"minecraft": { "version":"26.3", "modLoaders":[{"id":"fabric-0.20.1","primary":true}] }
```

包根：`mcbbs.packmeta` + `manifest.json`；模组实体在 `overrides/mods/fabric-api-0.165.0+26.3.jar`。

**2026-10-30 修正：包根不再写 `modrinth.index.json`。** 此前三种元数据混装，HMCL 会优先按 Modrinth 解析、再按 `dependencies` 找 loader 下载，加载器版本对不上就整个导入失败，真正对的 `mcbbs.packmeta` 反而读不到。排除实验（同一个包）：只剩 `mcbbs.packmeta` ✅ / 只剩 `manifest.json` ✅ / 三件共存 ❌。现在 `modrinth.index.json` 仅在**不勾 MCBBS**时写入。详见 `08` 的 T-07 条与 `05`。

**实现要点（踩过的坑）**：
1. `addons[].id` 必须是短名（`game`/`forge`/`neoforge`/`fabric`），不能写 `net.minecraftforge` 这类 patchId。
2. `manifest.json` 的 `minecraft.version` 用 `Version.VanillaName`，不能用显示名。
3. 复选框状态必须在 **UI 线程**缓存成局部变量，后台导出线程直接读会抛「调用线程无法访问此对象」（见 `08` 的 F-03）。
4. `files[]` 可以留空：PCL 的 `InstallPackMCBBS` 只复制 `overrides` 并按 `addons` 重建版本，不按 files 联网下载。

**跨启动器验收（2026-10-30 全部通过）**：HMCL 3.15.2 导入成功（T-07）、Prism 识别为实例（T-08）、
Forge / NeoForge 分支端到端通过（T-09）。PCL 侧此前已通过。
**修复前提**：`custom.5` 修掉两个导出缺陷——① `modrinth.index.json` 无条件写入会让 HMCL 按 Modrinth 解析而失败，
现改为 `If Not McbbsMode` 才写；② 「PCL 启动器程序」默认勾选会二次压缩，勾 MCBBS 时自动取消并置灰。

**与 P1=C 的关系（例外说明）**：R2 不走"新层"，理由：导出流程与 PCL 的实例模型、资源枚举、Loader 任务体系深度耦合，新层重写等于复制 700 行逻辑，违背 C 的"低成本增量"初衷。R2 属于"给既有功能加一种输出格式"，改动集中、可回退（新增选项默认不启用）。

**验收**：✅ 导出的包能被 HMCL 导入并启动；同包能被 Prism 识别为实例（双清单有效）——2026-10-30 三项全过。
**R4 导入侧**：`InstallPackModrinth` 本就完整，2026-10-30 实测 `.mrpack` 拖入即装，可用。

## R3 Terracotta 联机集成

**要什么**：用户点"联机"就能建房/加入，不手动配隧道。

**现状**：`[已确认]` **已完成并在本机实测通过**（2026-10-27，`2026.10.27-custom.10`）。

| 能力 | 实现位置 |
|---|---|
| 自动下载安装（4 个源轮询 + SHA-512 校验 + tar.gz 解压） | `ModCustom.TcInstall()` |
| 端口发现（`--hmcl <临时文件>`，实测 0.10s） | `ModCustom.TcEnsure()` |
| 复用已在运行的守护进程（读 `terracotta.lock` 探活） | `ModCustom.TcRunningPort()` |
| 建房 / 加入 / 复位 / 关闭 | `TcStartHost` / `TcJoin` / `TcReset` / `TcShutdown` |
| 状态文案与错误码映射（8 种状态 + exception 0–5） | `TcStatusTextFor` / `TcExceptionText` |
| 房间码校验（34 进制取模 7，与陶瓦解析一致） | `TcCheckCode` |
| 界面 | `CompTerracotta`（顶部「联机」标签与「设置 → 联机」共用） |

**单机实测结论**：安装 2.2s、建房 4.1s 拿到房间码并将码回填输入框、加入进入 `guest-starting`、
错误码被本地拦下不发请求、假房间最终报 `exception 0` 且文案正确、关闭后进程归零。

**跨机已验**：2026-10-27 本体（真实存档「对局域网开放」建房）+ 虚拟机加入，VM 经 USB 共享手机移动数据；T-01、T-02 通过。**后续进展**：T-03（樱花并存）、T-04（与 HMCL 并存）、T-05b/T-05c 已通过；T-05a（房主断网仍报就绪）已复现定性，`custom.19` 以「默认网关」为判据加断网提示（2026-10-29 实机验证通过）；T-06 有 15 分钟证据、未跑满 30。
后几项同机受限（全局文件锁使一台机器只能有一个守护进程、一份状态）。虚拟机游客端卡顿系 2GB 内存所致，非联机链路问题。

## R4 Modrinth `.mrpack`

**要什么**：导入与导出 `modrinth.index.json`。

**现状**：`[部分完成]` **导出已由上游实现**，2026-10-27 核实 `PageInstanceExport.xaml.vb`：

- 当 `IncludeLauncher=False`（不打包 PCL 本体）时，导出物扩展名就是 **`.mrpack`**；
- 已生成 `modrinth.index.json`，字段与 `05-formats.md` 的记录一致：`game`、`formatVersion`、`versionId`、`name`、`summary`、`files[]`（含 `path`/`hashes{sha1,sha512}`/`downloads`/`fileSize`）、`dependencies`（minecraft + forge/fabric-loader/neoforge）；
- 有 `ModrinthUploadMode` 选项：未在 Modrinth 托管的文件直接塞进包内，方便分辨哪些无法上传。

所以 R4 只剩两件事：
1. **导入**未验证（`.mrpack` 一键安装是否可用）。
2. 导出物能否过 Modrinth 官方校验未测。

**降级理由**：原计划把 R4 排在 R2 之后，现在发现导出侧"已经免费拿到"，只剩导入需要验证，优先级可以降为"顺手验一下"。

**验收**：从 Modrinth 下载的 `.mrpack` 能一键安装；导出的包能被 Modrinth 网页接受。

## R5 安全扫描管线

**要什么**：构建期就能发现危险 API 用法与已知漏洞依赖，而不是靠事后排查。

**现状**：`[已确认]` 2026-10-27 已落地 `pcl-src\tools\scan.ps1`，三段检查一条命令跑完：

- **依赖漏洞**：本工程是手工 dll 引用（非 NuGet），`dotnet list package --vulnerable` 对它无效；改为解析 vbproj 的 `Reference` 版本号后查 **OSV** 漏洞库，断网按包降级到内置表。
- **敏感信息落盘**：5 条规则（令牌进日志、密钥硬编码、关证书校验、弱 TLS、ModSecret 非空），参考 PCL CE 的 CVE-2025-54120。
- **产物哈希**：exe 的 SHA-256 + 字节数。

**实测结果**：首次运行即命中真问题——`ClientToken` 被完整明文写进启动日志（上游 `FilterAccessToken` 只处理 AccessToken 且保留前 5 后 5 位），已修为只输出长度。另发现 `Newtonsoft.Json 13.0.0` 有 GHSA-5crp-9r3c-p9vr（High，DoS），待决策是否升 13.0.3。

**验收**：`[已确认]` 本地一条命令跑完并给出高危清单，退出码可用于阻断；引入已知漏洞包能被列出（Newtonsoft 实测命中）。

**明确不做**：Roslyn `AnalysisLevel` —— 非 SDK 老式工程不生效，见 `07-security.md` 第四节。

## R6 联机可插拔抽象

**要什么**：不把主程序写死在某一个联机后端上，换方案不改主程序。

**现状**：`[已决策]` **暂缓抽取**。理由：目前只有陶瓦一个实现，抽 `IMultiplayerProvider`
只会得到一个只有单实现的接口，属于空转；而业务逻辑已经全部收在 `ModCustom.Tc*`
里，UI 层只调用这组函数、不出现后端特有概念，真正需要抽象时是一次机械重构。

**推翻条件**（出现即抽）：① 接入第二个组网后端（EasyTier 自建 / 樱花穿透内置）；
② 需要按实例选择不同联机后端；③ 需要给联机做自动化测试替身。

**验收**（将来）：新增一种联机方案只需加一个实现类 + 一行注册，不动 UI 与启动流程。

## R7 Scaffolding-MC 互通

**要什么**：与 HMCL / PCL CE 用户直接联机。

**现状**：`[✅ 已确认 2026-10-30，局域网双向互通；跨公网不通]`。V-08 结论：规范存在（`Scaffolding-MC/Scaffolding-MC`，MIT），**本分支在用的陶瓦 0.4.2 就是 Scaffolding 联机中心**（源码 `src/scaffolding/server.rs`，二进制含 `scaffolding-mc-server`），PCL CE 有完整客户端（`PCL.Core/Link/Scaffolding/`）。所以 R7 不是要自己实现协议，而是**验证陶瓦房间能否被第三方客户端加入**。

**T-13 实测结果（2026-10-30，双向）**：陶瓦当房主 → PCL CE 2.15.1 加入成功（延迟 9 ms）；CE 当房主 → 陶瓦加入成功。两端 EasyTier 版本不同（`v2.5.0-terracotta.2` ↔ `2.6.4`）也能通。**陶瓦这一侧不需要登录任何账户**，CE 侧需登录 Natayark 账户。详见 `14-t13-scaffolding.md`。

**验收**：✅ 同一局域网内，PCL CE 输入陶瓦房间码能进同一大厅，反之亦然；**跨公网不通**。
**跨启动器口径（2026-10-30 用户实测补齐）**：HMCL 跨公网已实测通过（自编版 ↔ HMCL），
自编版 ↔ 自编版跨网亦通；**因此「以陶瓦为主的启动器都能互加」成立**（内置陶瓦的 HMCL / FCL / ZL2 / FMCL / MoLaunch / Axolotl），
PCL CE 是唯一例外（走协议互通 + `--p2p-only`）。**仍缺**：游戏内互相可见性无截图。
**跨网限制（2026-10-30 实测）**：局域网通、**跨网不通**。根因在 CE 侧——它的 EasyTier 带 `--p2p-only`（只 P2P、不接受中继），而用户网络 UDP 是"严格"，打洞失败后无兜底。公共节点两端有交集，不是"找不到"。本分支陶瓦侧**没有**加 p2p-only 限制。详见 `14-t13-scaffolding.md` 第七节。

**不再做**：另写一套 Scaffolding 客户端或中心。规范无版本号、仓库 11 个月未提交，自研代价与风险都不值。

## R8 自主修复能力

**要什么**：PCL 官方砍掉的功能（如 MC 下载），在本分支由自有代码提供，且不随官方更新失效。

**现状**：`[已确认]` 第一步已达成——`RestrictMcDownload=False` 是源码常量，重编译即恢复下载入口，`InstallPack*` 等下游方法本就存活。这属于"改源码"，可长期维护。

**2026-10-27 实测闭环**：原版 26.3（586MB / 90 秒 / 15.9 MB/s）与 Fabric 0.20.1 + Fabric API 均下载成功，两个版本都能启动进主菜单（Fabric 版左下角显示「已修改」）。同时顺带修复了一个会让启动 100% 失败的环境变量冲突 bug，见 `08-todo-risks.md` 的 F-01。

**同类场景已验证**：CurseForge API 返回 403（无有效密钥）时自动回退 `mod.mcimirror.top` 镜像；NeoForge 官方源超时后回退 BMCLAPI。镜像回退链路在日志里可见。

**续传边界（2026-10-30 查实）**：PCL **不做跨任务续传**——临时文件名带随机数（`{Uuid}_{Th.Uuid}_{RandomInteger(0,999999)}.tmp`），
新任务找不到旧临时文件，失败后只能重下；所谓续传只在**单次任务内部**（线程级 `DownloadStart + DownloadDone`）。
下载中断后的恢复行为已实测：失败 → 任务管理页保留失败项 → 可点「重试」重新开始（`custom.12`，用户确认可用）。
**Forge / NeoForge 安装**：本机两个实例均由本启动器下载页装成，链路已验证（T-09 前后端到端通过）。

**明确不做**：对官方 exe 做 IL 重写（`fixpcl` 路线）。理由见 `01-scope.md`。

---

## 变更记录

| 日期 | 变更 |
|---|---|
| 2026-10-27 | 建立本文件；按用户给的优先级落表；标注各项现状与验收标准 |
