# 08 待办、未验证项与风险

状态：滚动更新（**收工必改这里**）
最后更新：2026-10-31

---

## 一、待办（按优先级）

### 立即可做（半天内）

- [x] ~~**R8 下载链路实测**~~：**2026-10-27 已通过**。原版 26.3（586MB，约 90 秒，15.9 MB/s）+ Fabric 0.20.1 + Fabric API 全部下载成功，两个版本都能启动进主菜单。过程中发现并修复了一个环境变量冲突 bug（见第六节）。
- [x] ~~**依赖漏洞扫描接入**~~：2026-10-27 已完成，见 `pcl-src\tools\scan.ps1`。首次运行即命中两处真问题（ClientToken 日志泄漏已修；Newtonsoft.Json 13.0.0 漏洞待决策）。
- [x] ~~**分析器试跑**~~：结论是**不适用**——`AnalysisLevel` 只对 SDK 风格工程有效，本工程是 `ToolsVersion=15.0` + net48。已改用正则规则（`07-security.md` 第三节）。
- [x] ~~**PCL 现有导出格式确认**~~：2026-10-27 已查清。上游有两种输出：PCL 自有 `.zip`（含 `PCL\Setup.ini` 与导出配置）与 **Modrinth `.mrpack`**（`IncludeLauncher=False` 时，已生成 `modrinth.index.json`）。**没有** MCBBS v2（全库无 `packmeta`）。
- [x] ~~**R2 第 1 步：保存 CF 的 projectID/fileID**~~：2026-10-27 已实现（`PageInstanceExport.xaml.vb` 旁挂 `CfModIds` 字典，未改 `Loader.Output` 类型），编译通过，日志会输出"已记录 N 个 projectID/fileID"。**尚未实机导出验证**。
- [x] ~~**R2 第 2 步：核实 `mcbbs.packmeta` 字段**~~：2026-10-27 已从源码拿到权威定义（HMCL `McbbsModpackManifest.java` + PCL `InstallPackMCBBS`），**无需导包实测**。关键结论：`addons[].id` 用短名（`game`/`forge`/`neoforge`/`fabric`/`quilt`/`optifine`）；PCL 导入时**只复制 `overrides/`，不按 `files[]` 下载模组** → 要兼容 PCL，模组必须实体入包。
- [x] ~~**R2 第 3 步：生成双清单 + UI 选项**~~：2026-10-27 已实现。导出页高级选项新增「额外写入 MCBBS v2 元数据」，勾选后包根写入 `mcbbs.packmeta` + `manifest.json`，`addons` 用短名、`manifest` 用 `VanillaName`；`files[]` 留空（PCL 只认 overrides）。
- [x] ~~**R2 验收（PCL 侧）**~~：用户实测导出的包 **PCL 能识别为 MCBBS 包并安装成功**。样本存 `evidence\sample-26.3-fabric.mcbbs.zip`。
- [x] ~~**R2 跨启动器验收**~~ → **2026-10-30 全部通过**：T-07 HMCL 3.15.2 导入成功、T-08 Prism 识别成功、T-09 Forge / NeoForge 分支端到端通过。修复过程见 `08c` 的 15:25 锚点与 `08d`。
- [x] ~~**归档补 SHA256 + built 时间戳改 ISO**~~：2026-10-28 完成并隔离验证。`build-info.txt` 现为六字段（label / assembly / built / source / note / sha256），`sha256` 取归档目录内 exe 的 SHA256，与 `sha256sum` 独立复算逐字符一致；`built` 由 `%DATE% %TIME%` 改为 ASCII ISO 8601（原写法在中文 Windows 落 GBK 星期字节，UTF-8 读取即乱码、无法排序）。
      注意：`versions\2026.10.27-custom.10` 及更早归档仍是五字段。exe 本体都在，需要时可单独补算，不必重编译。
      **2026-10-28 已回填**：`tools\backfill-build-info.ps1` 把 14 个历史归档补成六字段（`sha256` + ISO `built`），
      脚本默认 dry-run、`-Apply` 才写、可重复执行（已六字段的目录自动跳过）。
- [x] ~~**`05-formats.md` 超长，待拆分**~~：**2026-10-30 已拆** → `05` 157 行 + `05b-formats-mrpack-prism.md` 81 行，`INDEX.md` 已同步。
- [x] ~~**`06` / `08` 也超长**~~：**2026-10-30 已拆** → `06` 172 + `06b-terracotta-api.md` 100；`08` 188 + `08b-testlog.md` + `08c-anchors.md`（归档，允许超长）+ `08d-fixes.md`。
- [x] **验证"断网失败后可直接重试"**（2026-10-28 通过）：`custom.3` 的 `TcEnsureIdle` 生效。
      VM 断网加入失败 → 恢复网络 → **直接再点加入**（未点关闭陶瓦）→ 成功进入房间，无需重启守护进程。
- [x] **接管房间时说明"码已更换"**（2026-10-29 `custom.2` 已改）：`TcStartHost` 在复位前先读一次快照，
      若原本已是 `host-ok`，建房后比对新旧码，变了就返回「已接管原有房间，房间码已更换为 xxx；旧码已失效」。
      该判断放在状态文案之前——它改变的是"要发给别人的那串字符"，优先级高于状态描述。
- [x] **「直接连接」地址独立成行 + 可复制**（2026-10-29 `custom.2` 已改）：`guest-ok` 文案改为两行，
      地址单独一行；新增「复制直连地址」按钮（`BtnTcCopyAddr`），复制 `Snapshot.Url`（`127.0.0.1:端口`），
      无地址时提示"先加入房间"。理由：这是联机大厅条目点不进去时的唯一退路，混在长句里会被忽略。
- [x] **房主断开时给出可读提示**（2026-10-29 `custom.2` 已改）：控件内跟踪上一次阶段，
      `guest-ok → exception` 显示「房主已断开（对方可能关掉了游戏或退出了世界）。这个房间码已失效，请向房主索要新的房间码。」；
      `host-ok → exception` 显示「房间已结束…重新建房会生成新的房间码」。
      文案用 `TcOverrideText` 跨轮询保持——只在切换那一秒显示会被下一次刷新覆盖回通用文案（这个坑是同一次改出来的）。
- [x] **建房状态迁移补日志**（2026-10-29 `custom.2` 已补）：控件在阶段变化时写
      `[陶瓦联机] 状态 旧 → 新`，**只记状态名、不记房间码**（房间码算短期凭据）。此前出码与 `host-ok` 完全没有记录。
- [x] **复制按钮要按角色自适应**（2026-10-29 `custom.3` 已改，用户实测反馈）：原先固定成「复制直连地址」，
      房主刚建好房点一下反被提示"先加入房间"，与事实相反。改为 `host-ok` → 「复制房间码」、
      `guest-ok` → 「复制直连地址」，都没有时给"建房后才有码、加入后才有地址"的说明。
      `host-ok` 文案同步改为「房间已就绪，点「复制房间码」发给朋友：…」。
- [x] **关闭陶瓦要清房间码**（2026-10-29 `custom.3` 已改，用户实测反馈）：`BtnTcStop` 原先不清输入框，
      关掉后旧码还留在框里能复制走，而房间已经不存在。现与复位共用 `TcClearRoomField()`，
      同时清掉缓存的房间码/地址/阶段，提示改为「已请求关闭陶瓦，房间码作废…」。
- [x] **复制目标改为显式勾选**（2026-10-29 `custom.4`，用户要求）：原先按状态自动切换，
      用户在按下之前无法确定会复制到哪一个。现为按钮右侧一个「复制直连地址」勾选框（默认不勾 = 复制房间码），
      按钮文字跟随勾选变化，选择持久化到 `Settings("TcCopyAddr")`。
      空内容时的提示也分角色说明（房主缺码 / 游客缺地址各给对应指引）。
- [x] **自动检测做成可关的开关**（2026-10-29 `custom.8`，用户要求）：有些用户习惯固定用直连地址，不希望系统替他换目标。
      新增 `Settings("TcAutoAddr")`（默认 True），开关放在**顶部联机页**复制按钮下方（不是设置页——见下条）。
      开启时手动勾选框隐藏并显示说明（避免两套机制互相解释），按钮文字随状态预告"会复制什么"；
      关闭时手动勾选框恢复，严格按勾选复制、拿不到就提示没有。
      ⚠️ `custom.7` 曾把这个开关放进 `PageSetupLink.xaml`，但该页在本分支被强制隐藏，用户根本看不到，已删该归档。
- [x] **「房间已结束」时清除房间码**（2026-10-29 `custom.4`，用户要求）：`host-ok → exception` 视为码已死，
      显示结束文案的同时清空输入框。**游客侧刻意不清**——`guest-ok → exception` 可能只是本机网络抖动，
      房主房间还在、同一个码仍能重连，清掉等于逼用户重新找码；文案改为「可以再点一次「加入」重试」。
- [x] **游戏关闭后 27 秒界面仍报"房间已就绪"**（2026-10-29 `custom.3` 已缓解）：陶瓦自身要 ~27 秒才从
      `host-ok` 掉到 `exception`（实测 17:04:30 关游戏 → 17:04:57 才变），不是轮询问题。
      现借用 `ModLaunch.McLaunchProcess` 判断游戏进程是否已退出，`host-ok` 期间提前显示
      「游戏已关闭，正在等待陶瓦确认（通常 30 秒内）…」。句柄取不到时不提示——宁可不说也不误报。
      **实机待确认**：该提前提示是否真的出现（若 `McLaunchProcess` 在你场景里为 Nothing，此路不通，需换信号源）。

- [x] **房主断网时的界面提示**（2026-10-29 `custom.19`）：`host-ok` 分支调 `TcLocalNetworkAvailable()`，判据是「有没有已启用、非回环、带默认网关的网卡」——`custom.18` 用 `GetIsNetworkAvailable()` 在 VMnet 环境恒为 True、实测不触发，`custom.19` 已改。不可用时追加「（注意：当前网络不可用。本机房间已建好，但其他人现在可能连不上。）」；检测异常按可用处理避免误报；状态变化写一行日志 `联机断网检测：…`。**2026-10-29 实机验证通过**：断网建房时提示出现，日志 `联机断网检测：本机网络不可用（没有带默认网关的网卡）`；网络恢复后自动转「可用（网关网卡：WLAN）」。
- [x] ~~**Newtonsoft.Json 13.0.0 → 13.0.3**~~：2026-10-29 核实为**误报**——`Resources\Newtonsoft.Json.dll` 本来就是 13.0.3（文件版本 13.0.3.27908），vbproj 的 13.0.0.0 是程序集版本、不能改。`scan.ps1` 已改为优先用 dll 文件版本查 OSV，复跑显示 `OK Newtonsoft.Json 13.0.3`、退出码 0。

### 中期（选型/设计后）

- [x] **P3 实例视图抽象（第一步）**（2026-10-29 `custom.20`）：`Modules\Base\InstanceView.vb` 定义 `IInstanceView` + `McInstanceView` 适配器，导出页 3 处版本号改经视图读取；接口只有 7 个只读成员，不缓存不写状态。导入/批量仍直连 `McInstance`，等真正要用时再扩。
- [x] ~~**R2 MCBBS v2 导入**~~：**不单独开发**。上游 `InstallPackMCBBS`（`ModModpack.vb`）已完整实现，`custom.5` 的修复也没误伤它；2026-10-30 实测 PCL / HMCL / Prism 三端都能导入本分支导出的包，说明写入侧正确，读入侧走上游即可。
- [x] ~~**陶瓦联机实机验证**~~：**全部通过**。T-01、T-02（2026-10-27 本体 + 虚拟机）、T-03 / T-04 / T-05b / T-05c（2026-10-28）、T-05a（2026-10-29 `custom.19`，断网建房提示出现、恢复后消失）、**T-13 与 PCL CE 双向互通（2026-10-30）**。
      T-06 长时补测**用户决定不做**（联机已收口）；`custom.9`~`.12` 又补了断网失败后的入口与重试，见顶部锚点。
- [x] **R6 抽象**：已决策**暂缓**（单实现抽接口是空转）。业务逻辑收在 `ModCustom.Tc*`，UI 只调这组函数。
      推翻条件见 `02-requirements.md` R6。

### 服务端功能（2026-10-31 起，未完成项）

已做：原版 / Forge / NeoForge / Fabric / Paper / Purpur / Folia 的下载与安装、构建号下拉、
目录冲突检测、Java 版本校验、server.properties 初写、失败不当成功报。
**未做**（按价值排序，都是"想到了但没动"）：

- [x] ~~**管理员（OP）管理**~~（2026-11-01 `custom.1` 已实现，见第七节锚点）：现在能开服 / 停服 / 存配置 / 看日志，
      但**没法指定谁是管理员**。MC 的 OP 在 `ops.json`：`[{"uuid":"…","name":"…","level":4,"bypassesPlayerLimit":false}]`，
      `level` 1–4（4 = 服主级）。两条路：
      ① **服务端在跑**：走 stdin 发 `op <名字>` / `deop <名字>` —— 已有先例
      `PageLinkMain` 的 `ServerProc.StandardInput.WriteLine("stop")`（`RedirectStandardInput = True`，约 368 行），
      这条路不用自己造 UUID，服务端会写 `ops.json`；
      ② **服务端没跑**：直接改 `ops.json`，**必须自己备 UUID** —— 离线服可用
      `PageLoginLegacy.CalcOfflineUuid(名字)`（`MD5("OfflinePlayer:"+名字)` v3），正版服走正版 UUID。
      **两条都注意**：`ops.json` 改动要**下次启动才生效**，运行中改会被内存里的名单盖回去。
      「玩家名单里显示谁是管理员」：白名单 `whitelist.json` 也是 uuid+name，与 `ops.json` 按 uuid 对照即可标注。
      **实际做法**：按 `name` 对照（不用 uuid，因为离线/正版混着时 uuid 各算各的），
      名单 = 白名单 ∪ 管理员（没开白名单时管理员也能看见），管理员行尾加 `← 管理员`。
- [ ] **Velocity / Waterfall（代理端）**：和"装服务端"不是一回事——不绑 MC 版本、没有
      `server.properties` 那套、配置在 `velocity.toml` / `config.yml`，启动参数也不同。
      要做就得在安装页单开一个"代理端"入口，别硬塞进现有加载器按钮里。
- [ ] **安装器网络（重方案）**：现在只给 installer 加了 `-Djava.net.useSystemProxies=true`
      走系统代理。彻底解决要自己解析 installer jar 里的 version json、按镜像逐个下依赖、
      生成启动脚本 —— 即 PCL 装**客户端** Forge 的那整套逻辑（`ForgeInstaller`）。
      成本约等于前面六项之和，非必要不做。
- [x] ~~**server.properties 编辑器**~~（**原先记错了**）：联机页「服务器」卡片本来就有表单，
      端口 / 最大人数 / 白名单 / 正版验证 / MOTD 早就能改。2026-11-01 `custom.5`~`.6` 又补了
      **难度 / 游戏模式 / 视距 / PvP**，常用项基本齐了；剩下的 `spawn-protection`、
      `allow-flight`、`spawn-monsters` 之类低频项暂不做。
- [x] ~~**服务端启动/停止入口**~~（**原先记错了**）：`BtnServerStart` / `BtnServerStop` 本来就有，
      是 PCL 托管进程（`RedirectStandardInput` + 发 `stop`）。实际缺的是**控制台输出面板**，
      不是启停按钮；要看输出目前靠「打开日志」。
- [x] ~~**Forge / NeoForge 的 `user_jvm_args.txt` 内存**~~：2026-11-01 `custom.5` 已做。
      安装成功后 `EnsureServerJvmArgs` 把模板里那行 `# -Xmx4G` 换成实际值：
      物理内存的 1/4，夹在 2G–8G，按 512M 取整（16G 机器 → `-Xmx4096M`）。
      **已经手动设过 `-Xmx` 就绝不动**。

### 剩余（都需要新建入口或大改）

- [x] ~~**服务端控制台面板**~~：2026-11-01 `custom.8` 已完成。联机页加了实时输出框 +
      命令输入行 + 清空按钮。`ServerOutput`（本来就挂着 `OutputDataReceived`，只是都写进
      `server-run.log` 没给人看）现在同时 `RunInUi` 追加到界面。命令走 `StandardInput.WriteLine`，
      自动去掉玩家习惯的 `/` 前缀。
- [ ] **Velocity / Waterfall 代理端**、**安装器网络重方案**：见上两条。

### 远期

- [x] ~~R4 `.mrpack` **导入**~~：**2026-10-30 实测可用**（拖入即装）。代码本就在 `InstallPackModrinth`（`ModModpack.vb:366`），此前只是从没跑过。
- [x] **R7 Scaffolding-MC**（2026-10-30 核实）：陶瓦 0.4.2 就是 Scaffolding 中心（二进制含 `scaffolding-mc-server`，源码 `src/scaffolding/server.rs`），PCL CE 有完整客户端；不再需要「预留位」，改为 T-13 实机互通验证。详见 `03-references.md`。
- [ ] `[待测试]` **干净机器编译 / 断网编译** → 见 **T-10 / T-11**（同时覆盖风险 R-02）。**这是 08 里唯一未完成的实测项**，需一台干净机或断网环境，不阻塞功能。
- [ ] **美西螈实例单向转换**：扫描时复制一份成 PCL 结构，之后两边各自独立；
      已加「重新转换（覆盖）」，但仍是手动、单向。要做双向共享得用目录联接挂 `saves`/`mods`，
      风险是两边同时开游戏写同一份文件，暂不做。

---

## 二、待测试清单（功能已实现，缺一次真实环境跑）

> **`[待测试]` 与 `[待验证]` 的区别**：前者代码写完、单机能跑，只差实机确认；
> 后者事实本身未知，需要查资料或读源码。**两者都不能当成已通过。**

| 编号 | 事项 | 现状 | 怎么测 |
|---|---|---|---|
| T-01 | 陶瓦联机：游客端跨机加入 | ✅ **2026-10-27 实测通过**：本体建房 + 虚拟机加入（VM 经 USB 共享手机移动数据上网）成功联机；同机仍因全局文件锁无法自测 | 已做；日后换网络环境（真实宽带 / 异地）可补跑 |
| T-02 | 陶瓦联机：**真实游戏世界**建房 | ✅ **2026-10-27 实测通过**：本体在真实存档「对局域网开放」→ 启动器建房出码，虚拟机成功加入；VM 只给 2GB 内存故游客端卡顿，本体无卡顿 | 已做；复测可给 VM 加内存观察 |
| T-03 | 陶瓦联机：与樱花穿透并存 | ✅ **2026-10-28 实测通过**：樱花隧道连上时 `frpc` 绑了 UDP `4445`，但陶瓦 0.4.2 用 EasyTier 组网（`13448`+`17034`）**不独占 4445**，建房照常出码；反向顺序（先建房后连隧道）房间码也不失效 | 已做，详见 `10-t03-sakura.md` 第四节 |
| T-04 | 陶瓦联机：与 HMCL 同时开 | **2026-10-28 通过**：HMCL 先建房 → PCL 建房，全程只 1 个守护进程、锁端口不变、状态单份（后建覆盖并换码）；PCL 复位后 HMCL 同步回到「创建/加入」选择页 | 见 `11-t04-hmcl.md` 第五、六节；唯一问题是提示「已经创建好了」未说明码已更换（已记改进项） |
| T-05 | 陶瓦联机：弱网 / 断网文案 | **T-05b 通过**（VM 断网加入 ≈10 秒给出「连不上房主…也可能是网络被限制」）；**T-05c 通过但文案不足**（房主直接关游戏，游客数秒内见 MC 原生「连接已丢失 / 连接中断」，未说明房主已关）；**T-05a 通过**（2026-10-29 `custom.19`：断网建房时出现「当前网络不可用…」提示、日志有判据；陶瓦「房间已就绪」只代表本机房间成立，不校验外人可达——这是产品边界，不是缺陷） | 详见 `09-vm-tests.md` 第七节 |
| T-06 | 陶瓦联机：长时稳定性 | **2026-10-28 15 分钟通过（未跑满 30）**：零新增 `Removing guest`、零 `10060`、P2P 恒 1 条、四进程内存持平或下降 | 中止原因是 VM 资源不足致游客端游戏关闭（环境限制）。有条件时降低游客端负载补满 30 分钟，明细见 `09` 7.2；2026-10-29 复核仍为 15 分钟以上稳定（未跑满 30） |
| T-07 | R2：HMCL 侧导入 | **✅ 2026-10-30 通过**：`custom.5` 修复后重新导出，**HMCL 3.15.2 识别并导入成功**（用户确认「可用」）。此前失败的两个原因已定位并修掉（见下方两条事故记录） | 已验证；Prism 见 T-08 |
| T-08 | R2：Prism 侧识别 | **✅ 2026-10-30 通过**：Prism 导入 `custom.5` 导出的包，正常识别为实例 | 已验证；R2 跨启动器三项全部闭环 |
| T-09 | R2：Forge / NeoForge 分支导出 | **✅ 2026-10-30 通过（元数据 + 端到端）**：解包确认 Forge `addons=[{game,26.3},{forge,66.1.0}]`、`modLoaders=[{forge-66.1.0}]`；NeoForge `[{game,26.3},{neoforge,26.3.0.99}]`、`[{neoforge-26.3.0.99}]`；**版本号均为真实值，没踩「未知版本」占位符**。且同样走通了 HMCL 导入 | 已验证 |

> **2026-10-30 导出结构事故（已定性）**：用户导出的三个加载器包，外层根目录只有 `modpack.mrpack` + `Plain Craft Launcher.exe`，HMCL/Prism 读不了；而同批的**原版包**根目录直接是 `manifest.json`/`mcbbs.packmeta`，能导入。
> 根因是 `CheckOptionsPcl`（界面名「PCL 启动器程序」，`DefaultChecked="True"`）勾着 → `IncludePCL=True` → 二次压缩。
> **两个选项语义互斥**：「额外写入 MCBBS v2 元数据」是给第三方启动器看的，「打包 PCL 程序」会让第三方读不到。界面没有任何提示，且后者默认勾选。

> **2026-10-30 第二个缺陷：`modrinth.index.json` 无条件写入，导致 HMCL 抢注解析失败**（结构修好后仍导入不了的真因）。
> `PageInstanceExport.xaml.vb:726` 是**无条件**写入 `modrinth.index.json`，714–716 行还会把 `forge` / `fabric-loader` / `neoforge` 塞进 `dependencies`。
> HMCL 看到 `modrinth.index.json` 就优先按 **Modrinth 包**解析，再去按 `dependencies` 找 loader 下载 —— 加载器版本对不上（或 `.zip` 并非 `.mrpack`）就整个导入失败。
> **排除实验（用户实测，HMCL 3.15.2）**：从同一个包删到只剩 `mcbbs.packmeta` → ✅ 能导入；只剩 `manifest.json` → ✅ 能导入；三个文件共存 → ❌ 不识别。
> **修复方向**：MCBBS 模式下不写 `modrinth.index.json`（MCBBS 包本来是给 HMCL/Prism/PCL 用的，不需要 Modrinth 元数据）。
| T-10 | 干净 Win10 从零编译 | 只在本机验过 | 另一台机/虚拟机，只给 `gongtong\` |
| T-11 | 断网编译 | 判断工具链是否真自足 | 断网后 `build.cmd -NoArchive` |
| T-12 | 多实例并行启动 | R1 的残留项；Java 参数是启动时快照，风险低 | 同时启动两个实例，看内存与 Java 参数是否各用各的 |
| T-13 | R7：PCL CE 侧加入陶瓦房间 | **✅ 2026-10-30 局域网双向通过**：陶瓦当房主 → CE 2.15.1 加入（延迟 9 ms）；CE 当房主 → 陶瓦加入。**跨网不通**，根因是 CE 的 EasyTier 带 `--p2p-only`（无中继兜底）+ 用户 UDP 严格 NAT，非本分支缺陷 | 详见 `14-t13-scaffolding.md` 第五~七节；**未测**：HMCL、游戏内实际可见性 |

## 三、待验证清单（信息未知，需要查证）

| 编号 | 事项 | 为什么重要 | 怎么查 |
|---|---|---|---|
| V-08 | Scaffolding-MC 是否存在稳定规范 | R7 是否值得预留 | **✅ 2026-10-30 已查**：规范存在但无版本号/无 release（仓库约 11 个月未提交）；实现方是陶瓦（中心）与 PCL CE（客户端），R7 改为 T-13 实测，详见 `03-references.md` |
| V-09 | PCL CE 仓库地址与许可分布 | 移植前必查 | 逐个仓库看 LICENSE |

**已解决的旧编号**（保留以便对照，不要再引用）：
V-01 下载链路 ✅、V-04 Java 逐实例 ✅、V-05 / V-06 导出格式与字段 ✅（已按源码核实并实测导出）、
V-07 陶瓦 API 与端口 ✅、V-11 转 T-01、V-12 本地回环代理 ✅（已查明并处理）。
V-10 已查明但**结论是没做**（2026-10-28 已补上）：`versions\<label>\build-info.txt` 当时只有 `label` / `assembly` / `built` /
`source` / `note` 五项，**不含产物哈希**。现已在 `build.cmd` 补 `sha256` 一行并把 `built` 改为 ISO 8601；
历史 14 个归档（`2026.10.26-custom.30` ~ `2026.10.27-custom.10`）已由 `tools\backfill-build-info.ps1` 回填为六字段。
`tools\scan.ps1` 仍只算当前根目录 exe，不覆盖历史归档。

---

## 四、风险登记

| 编号 | 风险 | 影响 | 概率 | 缓解 |
|---|---|---|---|---|
| R-01 | 上游 23 个覆盖文件随版本漂移，同步后行为静默改变 | 高（功能莫名失效） | 中 | 每次 sync 后跑一次冒烟：启动、下载列表、帮助、凭据卡、联机页；`apply-custom.ps1` 输出逐文件差异 |
| R-02 | 构建只在当前机器成立（SDK 8.0.404 + WinFX shim + net48 引用包） | 中（换机即瘫） | **已降级** | **2026-11-03 已验证**：干净 VM 拷 `pcl-src`+`_tools` 即可编（T-B3），断网删 `obj`/`bin` 后仍能编（T-B4），全程无缺包；剩余未知仅限「新增了 `_tools\nuget` 里没有的包」 |
| R-03 | VB.NET 下分析器命中率低，安全扫描给人虚假安全感 | 中 | 高 | 明确记录"能覆盖什么、不能覆盖什么"，别当门禁 |
| R-04 | 联机方案锁死单一后端，后端改接口即全瘫 | 高 | 中 | R6 抽象 + 保留樱花穿透降级路径 |
| R-05 | 导出格式字段理解错误，包"能导出但别人读不了" | 高 | 高 | 先导入后导出；导出物必须用 HMCL 实际导入验证 |
| R-06 | 本机 Win10 19045 已停止支持，新 SDK/运行时逐步不兼容 | 中 | 高 | 锁 SDK 8.0.404；不追新编译器特性 |
| R-07 | 自用不分发的定调改变，触发 AGPL 与 PCL 许可义务 | 中 | 低 | 改变定调时先读 `01-scope.md` 许可节与 `03-references.md` |
| R-08 | 文档与代码脱节（改了代码没改文档） | 中 | 高 | 收工例行更新本文件；`INDEX.md` 状态列同步 |
| R-09 | `build.cmd` 调用 PowerShell 时未带 `-ExecutionPolicy Bypass`，依赖宿主已有策略 | 中（干净机直接编不出，首次卡在打版本号） | 已兑现 | **2026-11-03 已修**：第 99/103/157/187 行四处补参数；干净 VM 首跑复现（`Version stamp failed.`），改后待 VM 复跑验证 |

---

## 五、实测记录

> 已移到 `08b-testlog.md`（含全部实测记录与已修复问题 F-01~）。想确认"当初真跑通过吗"去那里。

## 六、已修复的问题

> 同上，见 `08b-testlog.md` 的 F 编号条目。

## 七、下次开工的第一句话

> 先读 `gongtong\launcher-dev\INDEX.md`，再看本文件的**待办（第一节）→ 待测试（第二节）→ 待验证（第三节）**，然后挑一件做。

如果距上次超过一周：先确认 `pcl-src` 的版本号与 `versions\` 最新归档，避免在错误基线上继续。

**续工锚点（2026-11-02 16:25 · Java 自动下载 + 只删自带的 + 「加入时用」跟随启动页 · 最新，以此为准）**

代码基线 **`2026.11.02-custom.30`**（0 错误，`sha256=f2200293…582a`，已部署 `pcl2ziyong\`）。

### ① Java：缺了就自动下载（不再让用户自己去装）

PCL 本体是单文件 exe（约 6 MB），**不可能把 JDK 打进去**。但 PCL 本来就有
`SelectOrDownloadJava`（`ModJava`，从 Mojang `java-runtime` 拉，带 bmclapi 镜像），
原版启动游戏时用的就是它 —— 服务端这条路之前绕过了它，直接报「找不到 Java」。

改动：
- `PageServerInstall.GetJavaPath(Optional McVer)` 改成 Public Shared，三级回退：
  **用户选中的 → Java 列表第一个 → `SelectOrDownloadJava(ServerJavaRange(McVer), True)` 自动下载**
- `ServerJavaRange(McVer)`：1.21+ / 1.20.5+ → Java 21；1.17~1.20.4 → 17；更早 → 8；
  26.x 这类新版本号（Major ≥ 22）按 21 处理
- 联机页 `BtnServerStart_Click` / `StartServerWithBat` 都改成调它
- 安装前的 Java 预检查文案改写：说明「把低版本从列表去掉，安装时会自动下一个」

### ② Java：能删，但只删启动器下载的那份

自动下载的 Java 落在 **`PCL\.minecraft\runtime\<component>\`**，和用户自己装的 JDK 天然隔离。
「设置 → 启动 → 高级选项 → Java 列表」右侧加 **「清理」** 按钮：
列出 runtime 下的目录、算总大小、确认后只删这些，删完触发 `JavaListRefreshWorker`。
ToolTip 写明「你自己安装的 JDK 不在这个目录里，完全不受影响」—— 有开发需求也能放心点。

### ③ 「加入时用」跟随启动页

新增设置 `LinkServerLoginFollow`（默认 True）+ 复选框 **「跟随启动页」**：
勾上时把 `ComboServerLogin` 同步成 `LoginType` 对应的值并禁用下拉，旁边那行灰字变成
「跟随启动页：离线登录（去「启动」页切换）」；取消勾选才恢复成可单独指定。

**续工锚点（2026-11-02 15:35 · processor 必须按 install_profile 的 classpath 走 · 已被上一条取代，留作背景）**

代码基线 **`2026.11.02-custom.22`**（0 错误，`sha256=f4c87825…d80f`，已部署 `pcl2ziyong\`）。

### 现象

NeoForge 26.3 走 UI 安装，PROCESS_MINECRAFT_JAR 抛
`java.lang.NoSuchMethodError: 'void net.neoforged.accesstransformer.parser.AtParser.<init>(java.lang.String[])'`
→ 镜像路径失败 → 回退官方安装器成功（用户：**「安装完成有 run ser」**，所以目录是可用的）。

### 根因

`RunProcessor` 用**整个 libraries** 当 `-cp`。但 install_profile 里每个 processor 都带
`classpath` 字段，**逐步骤声明精确依赖**：

| 工具 | 它需要的 classpath |
|---|---|
| NeoForge `installertools:4.0.17:fatjar` | **只有它自己**（fatjar 已打包一切） |
| Forge `jarsplitter:1.1.4` | jopt-simple **5.0.4** + srgutils **0.4.3** |
| Forge `ForgeAutoRenamingTool` | asm **9.2** 系列 + srgutils 0.4.9 |
| Forge `binarypatcher:1.1.1` | guava **25.1** + lzma-java + javaxdelta… |

把几十个版本一起塞进 `-cp`，等于让类加载器抽签。NeoForge 就抽到了
`at-parser-11.0.2.jar` 里的 `AtParser`，而 fatjar 里那个才是带 `(String[])` 构造的。

### 修法

1. 新增 `BuildProcessorClasspath`：**有 `classpath` 字段就只用它**（坐标转路径、缺文件直接报错），
   没有才退回「全量 libraries 减 Exclude」
2. 顺带修**路径含空格**：安装目录在 `新建文件夹 (2)` 这种带空格路径下时，
   参数不加引号会被 java 拆成两个。新增 `QuoteArg`，`Resolved` 逐项加引号

### 实测

在用户那个目录（`<下载目录>\ceshi\新建文件夹 (2)`）手工重跑：
- 全量 classpath → `NoSuchMethodError`
- 精确 classpath（只有 fatjar）→ **退出码 0**，产出 `minecraft-server-patched-26.3.0.101.jar`（25 MB）

**用户实机确认（2026-11-02 15:35）**：custom.22 下 NeoForge 26.3 走 UI 装通，镜像路径不再回退官方安装器。

**续工锚点（2026-11-02 15:30 · 清完遗留 bug（含删服进回收站 + 解压保留子目录），转入 VM 验证 · 已被上一条取代，留作背景）**

代码基线 **`2026.11.02-custom.20`**（0 错误，`sha256=ae37c17c…1131`，已部署 `pcl2ziyong\`）。

| # | 问题 | 根因 | 处置 |
|---|---|---|---|
| 1 | 本机有服务端 + 连了远程 RCON 时，玩家名单显示**本机**玩家，勾选却发到**远程** | `WhitelistRefresh` 用 `ServerDir() = ""` 判远程，而 `ApplyOpChange` 用 `UseRconForPlayers()` —— 两个判据不一致 | 名单改用 `UseRconForPlayers()`，远程优先显示说明、不读本地文件 |
| 2 | 连上 / 断开 / 重置 / 刷新 RCON 后，名单不跟着切 | 四处都没调 `WhitelistRefresh()` | 四处补上 |
| 3 | 回退官方安装器失败时弹两个内容不同的红框 | `InstallLoaderServer` 的 Combo 挂了 `LoaderStateChangedHintOnly`，Catch 又报一次 | 去掉该回调，统一由 Catch（带日志摘要）报 |
| 4 | 原版 / Paper 下载点取消后显示「下载失败：已取消」 | 取消抛的 `InstallCanceledException` 落进了通用 Catch | 两处补前置 Catch，只写「已取消」 |
| 5 | **删服务端目录是永久删除**（含 `world` 存档） | `DirectoryUtils.Delete(D)` 默认 `toRecycleBin=False` | 改 `Delete(D, True)` 走回收站；同时清掉 `LinkServerDir` / `LinkServerProcDir` / `LinkServerJar` 三个指向它的引用 |

**仍存疑（未改）**：~~`ExtractZipFolder` 只取 `IO.Path.GetFileName`~~ →
**2026-11-02 `custom.21` 已修**：改为保留 `E.FullName` 的相对路径（`Substring(Prefix)` + 建父目录），
并加了 zip slip 防护（条目带 `..` 直接跳过）。当前 data/ 是平铺的，所以这是预防性修复。

**同轮记下的两个待办（未做）**：
- `EnsureRconConfigured` 固定写 `rcon.port=25575`。**同时开两个服时第二个的 RCON 会绑定失败**
  （服务端照常运行，只是接管不了）。可改成「端口被占就顺延」。场景少，暂不改。
- 本轮待验项全部整理成 **`19-tests-2026-11-02.md`**（A 组本机 11 项 / B 组 VM 4 项，取代 `17-tests`）；
  原 `17-tests` 的 C 组（T-C1 局域网连服 /
  T-C2 VM 连主机 RCON / T-C3 干净机编译 / T-C4 断网编译），用户用 VM 测。

**续工锚点（2026-11-02 15:25 · 清掉 5 个遗留小 bug（含删服不走回收站） · 已被上一条取代，留作背景）**

代码基线 **`2026.11.02-custom.17`**（0 错误，`sha256=41c8dcb3…d2f0`，已部署 `pcl2ziyong\`）。

### ① jarsplitter 报 `Could not delete file`

**现象**：Forge 1.20.1 装到 jarsplitter 抛
`java.io.IOException: Could not delete file: ...server-1.20.1-...-slim.jar`
（`ConsoleTool.checkOutput` 删旧输出失败）。

**根因**：`BuildLibraryClasspath` 把 `libraries\` 下**所有** jar 都塞进 `-cp`，
其中就包含本轮要写的 `server-<ver>-slim.jar` / `-extra.jar`。
JVM 打开 classpath 里的 jar 会在 Windows 上锁住它，jarsplitter 重建前删不掉。

**已实测复现与验证**（`<下载目录>\cs`，那儿正好留着上次的产物可触发）：
- 旧 classpath（全量）→ 退出码 1，同样报 Could not delete file
- 新 classpath（排除）→ 退出码 0，slim 恢复 9,414,196 字节（与正常产物一致）

**修法**：
1. `BuildLibraryClasspath(Dir, Exclude)` 新增排除集；
   并硬性跳过 `libraries\net\minecraft\server\` 下所有 jar（那是 jarsplitter 的输入输出，是数据不是依赖）
2. `RunProcessor` 把 `Resolved` 里所有 `.jar` / `.zip` 加进排除 ——
   通用规则：**参数里出现的 jar 要么是输入要么是输出，一律不进 classpath**
3. processor 失败时按关键字直接给处置：
   `Could not delete file` → 残留 java.exe 占用；`NoClassDefFoundError` → libraries 没下全

### ② 点「取消」反而开始装第二遍

查 `PCL\Log1.txt` 看到：15:06:06 镜像组装失败 → 自动回退官方安装器 → **15:06:29 用户点了取消**
→ 于是 `EnsureServerJvmArgs` / `WriteServerMeta` 都没跑，目录里缺 `server.json`、
`user_jvm_args.txt` 还是原样注释（这就是「装完了但参数没写」的真相）。

两个体验缺陷一起修：
- 镜像失败时 `LoaderCombo` 仍挂着 `LoaderStateChangedHintOnly`，先弹红框「失败」，
  紧接着又开始回退重试 —— 用户看着像整个安装挂了，于是取消。现在镜像路径**不挂提示回调**，
  失败统一由外层 `Catch` 说话
- 取消原本抛普通 `Exception`，被外层当成失败 → **回退重装**。
  新增 `InstallCanceledException`，Forge / NeoForge / Fabric / 原版下载四处 `Catch` 前置拦截：
  只写状态行「已取消」，不回退、不弹错

**产物实测**：cs 目录的 Forge 1.20.1 服务端**已成功启动**
（`Done (20.173s)!`，58 个 `win_args.txt` 引用路径零缺失），说明组装产物的文件构成是对的。

**用户实机确认（2026-11-02 15:15）**：custom.17 下重装 Forge 1.20.1 通过，镜像路径不再回退官方安装器。

**通用教训**：把「库目录全量」当 classpath 有双重代价 —— 既是类冲突源
（guava 25/31、asm 9.2/9.6/9.9 同时在），也会锁死即将改写的产物。
凡要读写的文件都不能出现在 `-cp` 里。

**续工锚点（2026-11-02 15:12 · jarsplitter 输出被 classpath 锁住 → Could not delete file · 已被上一条取代，留作背景）**

代码基线 **`2026.11.02-custom.16`**（0 错误，`sha256=87b8278a…b4d9`，6,111,232 字节，已部署 `pcl2ziyong\`）。

**现象**：Forge 1.20.1 装到 jarsplitter 那步抛
`java.io.IOException: Could not delete file: ...server-1.20.1-...-slim.jar`
（`ConsoleTool.checkOutput` → 删旧输出失败），整条链路回退官方安装器。

**根因**：`BuildLibraryClasspath` 把 `libraries\` 下**所有** jar 都塞进 `-cp`，
其中就包含本轮要写的 `server-<ver>-slim.jar` / `-extra.jar`。
JVM 打开 classpath 里的 jar 会在 Windows 上锁住它，jarsplitter 重建前删不掉。

**已实测复现与验证**（`<下载目录>\cs`）：
- 旧 classpath（全量）→ 退出码 1，同样报 Could not delete file
- 新 classpath（排除）→ 退出码 0，slim 恢复到 9,414,196 字节

**修法**：
1. `BuildLibraryClasspath(Dir, Exclude)` 新增排除集；
   还硬性跳过 `libraries\net\minecraft\server\` 下所有 jar（那是 jarsplitter 的输入输出，只是数据不是依赖）
2. `RunProcessor` 把 `Resolved` 里所有 `.jar` / `.zip` 路径加入排除 ——
   通用规则：**参数里出现的 jar 要么是输入要么是输出，一律不进 classpath**
3. processor 失败时识别两类高频错误并直接给处置：
   `Could not delete file` → 残留 java.exe 占用；`NoClassDefFoundError` → libraries 没下全

**通用教训**：把「库目录全量」当 classpath 是有代价的 —— 既是类冲突源（guava 25/31、asm 9.2/9.6/9.9 同时在），
也会锁住即将改写的产物。凡是要读写的文件，都不能出现在 `-cp` 里。

**续工锚点（2026-11-02 15:05 · 镜像组装支持 Forge 1.20.1（10 步链路已实测） · 已被上一条取代，留作背景）**

代码基线 **`2026.11.02-custom.15`**（0 错误，`sha256=13d47dd0…32af`，已部署 `pcl2ziyong\`）。

用户贴的日志是**官方安装器**在 `D://xiazai//cs3` 跑 Forge 1.20.1 —— 说明镜像路径先抛异常、
自动回退了。查 Forge 1.20.1 的 install_profile：**10 个 processor**，data 键 13 个
（MAPPINGS/MOJMAPS/MERGED_MAPPINGS/BINPATCH/MC_UNPACKED/MC_SLIM/MC_EXTRA/MC_SRG/PATCHED/SIDE…），
比 NeoForge 的「1 个 BINPATCH」复杂得多。三个真 bug：

1. **`{BINPATCH}` 写死成 `data\client.lzma`** —— Forge 服务端用的是 **server.lzma**。
   改成按 `install_profile.data[key].server` 现解析：`[coord]` → libraries 路径、
   `/data/x` → data 目录、`'文本'` → 去引号；未知 `{X}` 直接抛错，不带着花括号执行。
2. **`MavenCoordToPath` 不认 `@ext` / classifier** —— `mcp_config:…@zip`、`:mappings@txt`、
   `forge:…:server` 全算错路径。已补。
3. **Forge 的工具是瘦 jar** —— `java -jar installertools-1.4.1.jar` 直接
   `NoClassDefFoundError: com/google/gson/GsonBuilder`。改成
   `java -cp "<libraries 下全部 jar>" <jar 的 Main-Class> <args>`（Main-Class 从 jar 的
   MANIFEST 读，读不到退回 -jar）。

**实测结果**（`D://xiazai//fgtest`，跑完即删）：Forge 1.20.1-47.4.28 的 10 个 processor 全部 exit 0，
产出 `libraries/net/minecraftforge/forge/1.20.1-47.4.28/...-server.jar`（4.8 MB）。
其中 `DOWNLOAD_MOJMAPS` 连的是 Mojang（可达），`MCP_DATA` 读本地 mcp_config zip —— 全程不碰
forge/neoforged 官方仓库。

**未实机验**：UI 里点 Forge 1.20.1 装一次，`run.bat` 能起服。

**续工锚点（2026-11-02 14:52 · 镜像自建安装：Forge/NeoForge 不再依赖官方 maven · 已被上一条取代，留作背景）**

代码基线 **`2026.11.02-custom.13`**（0 错误，`sha256=58beed11…cca9`，已部署 `pcl2ziyong\`）。

**做了什么**：把「安装器网络重方案」实现了 —— 不再靠官方 maven，改用 bmclapi 镜像 +
安装器自带的 installertools 在本地组装。**先手工验证算法跑通才写代码**。

**验证过程**（在 `<下载目录>\spike` 手工做了一遍，跑完即删）：
1. 从安装器 jar 里解 `data/`（run.bat、win_args.txt、user_jvm_args.txt、**client.lzma 补丁包**）
2. 按 `install_profile.json` + `version.json` 的 `downloads.artifact.path` 从镜像下 30 个库
   （只有 universal jar 首次 403 —— bmclapi 那它是 302 跳 CDN，**不带 UA 会被拒**）
3. `java -jar installertools-4.0.17-fatjar.jar --task PROCESS_MINECRAFT_JAR --no-mod-manifest
   --input server.jar --output libraries/net/neoforged/minecraft-server-patched/26.3.0.101/...
   --extract-libraries-to libraries/ --apply-patches data/client.lzma`
4. 摆好 win_args.txt 后直接 `java @user_jvm_args.txt @.../win_args.txt nogui`
   → **`Done (3.031s)!`**，全程没访问 `maven.neoforged.net`。libraries 共 79 MB。

**代码里的关键点**（`PageServerInstall`）：
- `MirrorMavenUrl` 改写三个官方域 → `bmclapi2.bangbang93.com/maven/`
- `ExtractByArgs` 按 processor 的 `--from/--to` 成对搬文件；`user_jvm_args.txt` 已存在不覆盖
- `RunProcessor` 用 `java -jar`（processor 的 `class` 为空时）跑 installertools；
  **stdout/stderr 必须并发 ReadToEndAsync**，否则 stderr 写满 4KB 会卡死
- `ResolvePlaceholder` 处理 `{ROOT}/{INSTALLER}/{MINECRAFT_JAR}/{LIBRARY_DIR}/{BINPATCH}`
  和 `[group:artifact:version]` 坐标；输出路径先建父目录
- **保留回退**：镜像路径抛异常就改用官方安装器（老版本 Forge 的 processors 更多，可能不支持）

**未实机验**（重要）：UI 里点一次 NeoForge 26.3 安装，确认任务栏一条、进度走完、
目录里有 `run.bat` + `server.json` + `libraries`（约 79 MB），并且「开服」能进世界。

**续工锚点（2026-11-02 14:38 · NeoForge 安装失败的真正原因 + 安装器代理 · 已被上一条取代，留作背景）**

代码基线 **`2026.11.02-custom.10`**（0 错误，`sha256=ba6fcd09…e333`，已部署 `pcl2ziyong\`）。

用户报「安装 NeoForge 服务端 26.3-26.3.0.101 失败，安装器退出码 1」。**实测定位到根因，不是我们代码的 bug**：

**根因：`maven.neoforged.net` 在本机网络不可达**
- 安装器日志：`Downloading library from https://maven.neoforged.net/releases/...` 紧接着
  `java.net.SocketTimeoutException` / `SocketException: Connection reset`，共 41 次 → 退出码 1
- `curl` 直连该主机：`000`（失败）；同一构件的 bmclapi 镜像：`302`（有货）
- **Mojang 的文件（piston-meta / piston-data）在同一台机器上是下成功的** —— 所以不是断网，
  是这一个仓库被卡

**验证过的错误做法（别再试）**
- `--mirror https://bmclapi2.bangbang93.com/maven/` **无效**：加了尾斜杠、不加尾斜杠都试过，
  安装器仍然直连 `maven.neoforged.net`。原因：该 flag 只影响安装器自身的构件解析，
  **不改写 version json 里那些绝对 URL 的库**。
- `-Djava.net.useSystemProxies=true` 只在系统代理真的开着时有用；没开代理等于没加。

**本轮改了什么**
1. **新增「安装器代理」输入框**（下载 → 服务器页，加载器下面）+ 设置项 `ServerInstallerProxy`：
   填 `127.0.0.1:7890` 会拼成 `-Dhttp.proxyHost/-Dhttp.proxyPort/-Dhttps.*` 传给安装器 JVM。
   允许写成 `host:port` 或 `http://host:port`；格式不对只记日志、不阻断。
2. **失败提示不再是废话**：新增 `DiagnoseInstallerLog`，从日志里找「第一次失败前最后一条
   `Downloading library from https://host/...`」，直接点名卡在哪个仓库 + 数失败次数；
   `ReportInstallerFailed` 再给出「开加速器 / 填安装器代理 / 改用原版或 Paper」三条路。

**真正的彻底解法**（还没做，成本高）：自己按 version json 从 bmclapi 把 libraries 全下下来、
生成 `run.bat` / `user_jvm_args.txt`，绕开安装器。已验证 bmclapi 镜像有这些构件，
技术上可行；量级 ≈ 复刻一次安装器输出。

**续工锚点（2026-11-02 14:28 · 安装进度改计时 + 加载器版本排序 · 已被上一条取代，留作背景）**

代码基线 **`2026.11.02-custom.9`**（0 错误，`sha256=545e2334…fb36`，已部署 `pcl2ziyong\`）。

用户实测反馈两条，都修了：

1. **安装最后一步进度一直是 0**：`LoaderTask` 的 `Progress` 默认是 `-1`，`LoaderBase.Progress`
   在 Loading 状态把 -1 当成 0 返回 —— 所以第三步永远显示 0，看着像卡死。
   安装器本身不吐进度，改成**按时间估**：`WaitInstallerWithProgress(P, Task, ExpectedSeconds, TimeoutMs)`
   轮询 `HasExited`，边等边推进度（前 30% 时间走 70%，之后按剩余时间逼近 95%，真结束才给 100%）。
   预估：Forge / NeoForge 180 秒、Fabric 40 秒。**它只回答「还在动」，不承诺剩余时间。**
   顺带把 `RunInstaller` 的签名从 `Action` 改成 `Action(Of LoaderTask(Of String, String))` 好传进度。
2. **加载器版本要排好序**：`CompareVersionDesc` 原来只按 `.` 切段，
   `NeoForge legacy` 的 `1.20.1-47.1.106` 会退化成字符串比较 → 排序不靠谱。
   改成按 `.` 和 `-` 都切段，并在段数相同时用整串倒序兜底（排序稳定，不会每次刷新换顺序）。
   排序位置在 `If/ElseIf` 链之后统一执行，Forge / NeoForge / Fabric / Paper / Purpur 全覆盖。

**已实机验证**：Fabric 服务端装成功（不传 `-downloadMinecraft` 这条路是通的）。

**未实机验证**：Forge / NeoForge 走新任务链装一次 —— 重点看任务栏只有一条、第三步进度会动、
装完有 `run.bat` 与 `server.json`；以及各加载器下拉第一条是不是最新版。

**续工锚点（2026-11-02 14:22 · 第 1 步：安装合并成一条任务 + server.json · 已被上一条取代，留作背景）**

代码基线 **`2026.11.02-custom.8`**（0 错误，`sha256=d786524a…e387`，已部署 `pcl2ziyong\`）。

按 `18-server-reference.md` 的**第 1 步**做完了，并顺手落了第 2 步的第一块。

**1. 安装合并成一条任务**（`PageServerInstall`）
- 新增 `InstallLoaderServer(Title, McVer, Dir, InstallerUrl, InstallerPath, RunInstaller, SuccessCheck)`：
  一个 `LoaderCombo` 三步顺序执行 —— 下载原版（权重 60）→ 下载 installer（5）→ 静默安装（**120**）。
  安装步骤用现成的 `LoaderTask(Of String, String)` 包，不新写 Loader 基类。
- 为什么权重给安装最大：安装器联网拉依赖要几分钟，进度条先跑满会让人以为「下载好了直接去开服」。
- 目录里已有 `server.jar`（重装加载器）时自动跳过原版那步，不会白下一份。
- Forge / NeoForge / Fabric 三条路统一走它；原来的 `EnsureServerJar` 拆成
  `GetVanillaServerUrls`（解析地址）+ `PrepareVanillaForInstaller`（复制 `minecraft_server.<版本>.jar` + eula），
  后者在**安装步骤开头**调用，所以复制发生在原版下完之后、安装器启动之前。
- 失败处理：退出码非 0 时抛 `安装器退出码 N`，`Catch` 里按有无退出码分流到
  `ReportInstallerFailure`（带日志摘要）或网络类提示。

**2. `server.json`（第 2 步的地基）**
- 新增 `PageServerInstall.WriteServerMeta / ReadServerMeta`，安装成功时往服务端目录写
  `server.json`（`mc` / `loader` / `loaderVer` / `updated`）。原版、Paper 家族、Forge、NeoForge、Fabric 都写。
- 联机页 `ServerRecordInfo` 改成**优先读 `server.json`**，读不到才退回「已装记录」的 ver 字符串。
  好处：换机、别人拷来的目录也能自报版本，不用靠记录猜。
- **还没做**：Java / 内存 / 端口进 server.json、拆掉 `LinkServerJar`/`LinkServerDir`/`LinkServerProcDir` 三轨。
  等第 1 步实机验过再做，否则出问题分不清是安装层还是实例层。

**未实机验**：Forge / NeoForge 走新任务链装一次（重点看任务栏是否只有一条、进度是否包含安装、
装完是否有 `run.bat` 与 `server.json`）；Fabric 不传 `-downloadMinecraft` 是否正常。

**续工锚点（2026-11-02 14:15 · 修 4 处 + 版本不符检测 + 服务端参考调研 · 已被上一条取代，留作背景）**

代码基线 **`2026.11.02-custom.6`**（自用版，0 错误，`sha256=9cb38f67…d161`，已部署 `pcl2ziyong\`）。

**本轮改的（都是用户 14:11 逐条点的）**：
1. **「清空」按钮位置**：原来 `Grid.RowSpan=3` 挂在第 3 列，视觉上悬在两行之间。改成与「加入服务器」
   同一行（Row 0 / Column 3），「备注名称」输入框 `ColumnSpan=3` 铺满。
2. **命令不用带 `/`**：命令框 HintText 改成「命令（不用带 / ），如 list、say 你好」并加 ToolTip。
   （代码本来就会 `TrimStart("/")`，只是没告诉用户，导致有人以为必须带。）
3. **Forge 版本列表老旧**：根因是 `Ids.Sort(CompareVersionDesc)` 原来只写在 NeoForge 分支里，
   Forge 直接用接口顺序 —— 实测 `bmclapi2/forge/minecraft/1.20.1` 返回是**乱序**的
   （首条 47.0.1、末条 47.4.27，最新 47.4.28），于是下拉第一条永远是很老的版本。
   现在排序提到 `If/ElseIf` 链之后统一做。
4. **启动世界与服务器不符的检测**（用户新要求）：`BtnServerJoin_Click` 里补齐——
   - 服务端版本优先用运行中探到的 `ServerVersion`，没启动过就用 `ServerRecordInfo(ServerDir())`
     从已装记录（`1.20.1-forge-47.0.1` 这种）解析出 MC 版本 + 加载器；认不出就返回 Nothing，不硬猜
   - 版本不一致改成**可继续的警告**（「仍然尝试 / 先去换版本」），原来是单按钮死拦
   - 服务端带 Forge / NeoForge 时额外提示「客户端要装同版本加载器和 mod」

产物 `2026.11.02-custom.6`，0 错误，已部署，`build/` 与 `custom/` 已同步。

**服务端功能暂停开发，先调研**（用户 14:11 / 14:12 两次强调）：已写 `18-server-reference.md`。
核心结论：**只有 HMCL 把服务端当一等公民**（服务端是实例，`server-manifest.json` 走安装 / 补全 /
更新任务链）；Prism、OpenLauncher 都没有服务端功能，没有第二家可抄。
我们这轮 6 个 bug 里有 4 个的根因都是「目录 + jar 双轨、没有实例概念」——建议顺序：
① 安装合并成一条任务（LoaderCombo + LoaderTask，安装进度也算进去）② 服务端实例化
③ 服务端整合包 ④ 世界备份 / 端口自检。**等用户拍板再动。**

**续工锚点（2026-11-02 13:50 · 修服务端安装/开服/白名单等 6 个问题 · 已被上一条取代，留作背景）**

代码基线 **`2026.11.02-custom.5`**（自用版，0 错误，`sha256=41f67109…c75d`，已部署 `pcl2ziyong\`）。

**先说一件记录缺口**：`custom.23` / `custom.24`（11-01 21:48 / 21:49 构建）没有写进本文件，是
11-02 核对源码时才发现的。它们只改了 `PageLinkMain`（名单加边框、已装服务端加「刷新」、
RCON 按钮重排 + 新增「重置」「刷新」两个按钮、`DefaultRconPort` 常量）。

**本轮修的 6 个问题**（用户 13:43 报的）：

1. **装模组加载器只下加载器、没有原版**（最严重）
   根因：三个安装器都「不预下原版，指望 installer 自己去拉」。NeoForge / Forge 认的是
   `minecraft_server.<版本>.jar`，国内网络拉不动 → 目录里只剩 `*-installer.jar`；
   而开服时选文件又能选中 installer，于是「开服」跑的是安装程序。
   改法：`EnsureServerJar` 下完 `server.jar` 后**再复制一份 `minecraft_server.<版本>.jar`**
   （安装器见了就跳过下载）；Forge / NeoForge / Fabric 全部改成**先下原版再静默安装**；
   Fabric 去掉 `-downloadMinecraft`。成功判据从「退出码 0」升级为
   **退出码 0 且 run.bat（Forge/NeoForge）或 fabric-server-launch.jar 真的存在**，
   否则报「安装不完整」，不再谎报完成。
2. **开服可能启动 installer** → 新增 `StartServerByDir`：只认
   `fabric-server-launch.jar` → `server.jar` → `run.bat`；目录里只有 installer 时明确说
   「这里只有安装器，回下载页重装」。手选文件时也拦 installer。
3. **服务器地址填了清不掉** → 「加入服务器」右边加「清空」，一并清 Settings 与「启动时默认进入」。
4. **「用这个开服」与「开本地服务端」重复** → 合并成一个 **「开服」**（`BtnServerStart`），
   优先用「已装服务端」里选中的，其次最近安装/添加的，都没有才让用户挑一次文件。
   `BtnUseInstalled` 已删除。
5. **白名单必须先开本地服才能改** → 根因是 `ServerJarDir()` 只认手选过的 jar，而 Forge 目录
   根本没有 server.jar。新增 `ServerDir()`：已装服务端选择 → `LinkServerDir`（新增设置项，
   `AddServerRecord` 写入）→ `LinkServerProcDir` → jar 目录。配置、白名单、RCON 密码全改用它。
   同时远程可用：连上 RCON 且目标非本机时，加/删玩家、op/deop 走 RCON 命令（`UseRconForPlayers`
   + `RconExec`）；远程读不到完整名单，列表位置改成文字说明，不假装知道。
6. **删玩家没同步撤管理员** → 删完白名单后比对 `ops.json`，有就一并 `ApplyOpChange(..., False)`
   （运行中发 `deop`，没运行写文件）。
7. **「保存配置」多余** → 按钮删掉，改成**改动即存**（输入框 500ms 防抖，勾选/下拉立即写），
   状态行显示「已自动写入 server.properties（HH:mm:ss）」。
   **顺带修一个会被自动保存放大的老问题**：`ServerConfigRefresh` 原来把「正版验证」一律显示成
   未勾（注释写着"默认离线"），配上自动保存就等于「随手改个难度 → 正版验证被悄悄关掉」。
   现在读文件里的真实值。

**新增设置项**：`LinkServerDir`（注册在 `Settings.vb`，硬约束：不注册会 `KeyNotFoundException`）。

产物 `2026.11.02-custom.1` ~ `.5`（过程中逐步编译验证），最终 `.5`，0 错误，已部署，两棵树已同步。

**未实机验**（下次务必逐条点）：
- Forge / NeoForge **真装一次**：确认先下原版、installer 不再联网拉原版、生成 run.bat、点「开服」能进世界
- Fabric 去掉 `-downloadMinecraft` 后是否仍正常（依赖目录里的 server.jar）
- 配置自动保存：改难度/端口后看 `server.properties` 是否即时变化；切到另一个服是否会串改
- 远程 RCON 下加/删白名单、op/deop
- 删除玩家是否连带撤掉管理员

**续工锚点（2026-11-01 15:20 · 备份 + 发布 custom.22，tag 改用版本号 · 已被上一条取代，留作背景）**

**已完成备份**：`<备份盘>\2026-11-01-custom.22\`（**1.7 GB**，五份：
`pcl-src`（排除 `obj`/`bin` 中间产物，754 个文件）/ `PCL-custom`（含全部 `versions` 归档）/
`launcher-dev` / `jiyiku` / **`_tools`（编译工具链，5997 个文件）**）。

**备份口径（2026-11-01 15:12 修正）**：
- `obj` / `bin` **不备份**：编译中间产物与输出，共约 23 MB，重编即再生
- **`_tools` 必须备份**：它在 `gongtong\_tools`（**在 `pcl-src` 外面**），是 `build.cmd` 的硬依赖 ——
  `dotnet`（.NET SDK 8.0.404 便携版 717 MB）、`nuget`（net48 引用程序集缓存 183 MB）、
  `dotnet-home`、`terracotta`（17 MB）、截图脚本。
  **缺了它，恢复出来的源码根本编不了**（`build.cmd` 里 `set DOTNET=%WB%\_tools\dotnet\dotnet.exe`）。
  本轮第一次备份就漏了它，因为排除规则写成「排除 `_tools`」——而 `_tools` 根本不在 `pcl-src` 里，
  那条规则对目录内是空操作，等于是「什么都没排除，但也谁都没备份上」。
- 顺带：因为 `obj` 没备份，恢复后第一次编译必须**带 restore**（不能加 `-NoRestore`），
  好在 `NUGET_PACKAGES` 指向备份内的 `_tools\nuget`，断网也能还原。

**已发布 GitHub**：`4huang0yan4/pcl-shiguangxiugaiban`，main = `a41bbb3`，tag **`2026.11.01-custom.22`**。

**tag 命名规则改了（用户 2026-11-01 15:05 提的）**：原来用纯序号 `1`/`2`/`3`，
「并不能一眼看到更新了什么」。现在 **tag 名 = 完整版本号**（`<日期>-custom.<序号>`），
与 exe 版本行、CHANGELOG 小节标题一致；日期在前所以 GitHub 按名称排序即按时间排序。
打 tag 必须同时做两件事，否则等于没做：
1. `git tag -a <版本号> -m "一行摘要"`（annotated，别用轻量 tag）
2. `CHANGELOG.md` 顶部 **「版本索引」表**加一行（tag / 日期 / 一句话内容 / 包含版本区间）

历史 `1`/`2`/`3` **保留不动**（已公开不重写），含义补进了索引表。本轮先误打了序号 `4`
并推送，随即删除远端 tag 4、改打版本号 tag —— 远端现在只剩 `1`/`2`/`3`/`2026.11.01-custom.22`。

**发布前的把关（照旧，一条没少）**：
- 同步时 `/XF` 点名排除 4 个必须回退的文件（`PageSetupUI.xaml`、`ModSecret.vb`、`ModCustom.vb`、
  `FormMain.xaml.vb`）；本轮这 4 个确实没动，`git status` 复核为「未改动」
- `launcher-dev` 排除 `08c`/`12`/`15`/`16`/`17` 五份 + `_backup/` + `evidence/`
- 两棵树合规自检：`ThemeCheckAllImpl`/`EnsureCustomTheme`/`去掉了全部预设配色` 全 0，
  `IsEnabled="False"` 各 10（上游 12，差 2 是已删的联机页）
- 暂存区再查一遍（`git grep --cached`）+ 推完从 `raw.githubusercontent.com` 拉文件复核：
  `custom/PageLinkMain.xaml.vb` 解锁词 0；两棵树的 `PageSetupUI.xaml` 都是 0 解锁词 / 10 个 `IsEnabled=False`

**本轮推送内容**（commit `4beba2a` 功能 + `a41bbb3` 规则/索引）：custom.17~custom.22
（关闭启动器时的服务端行为、RCON 自动接管、密码默认用完即清）。`git diff --name-only`
只有 7 个真改动 —— **别被 `git status` 里几百个 M 吓到，那是 `core.autocrlf` 的换行符假象**。

**续工锚点（2026-11-01 15:12 · RCON 密码改为「默认用完即清」· 已被上一条取代，留作背景）**

代码基线 **`2026.11.01-custom.22`**（自用版，0 错误，`sha256=612a23dd…0a6d`，已部署 `pcl2ziyong\`）。

**用户定的口径**：密码**一次性输入、用完即清**，下次连接重新输入 —— 这作为**默认**，
既安全又不用额外设置。之前「记住密码 + 圆点占位」那套**保留为开关，默认关闭**，
开启时给一次**强提醒**（说明 RCON 密码等同管理员凭据、会落盘）。

**实现要点**：
- 新增设置 `LinkRconRememberPass`（**默认 False**）。只有它为 True 时才把密码写进
  `LinkRconPass`（`Encrypted:=True`）；关掉时立刻把已存的那份擦掉
- 连接成功后，若未勾「记住密码」→ **清空密码框**，下次要重输
- **密码的三级回退**（`GetRconPassword(Host)`）：手输 → 勾了记住就读设置 →
  **仅当目标是本机**（`IsLocalHost`：空 / 127.0.0.1 / localhost / ::1 / 0.0.0.0）
  才从 `server.properties` 现读。
  **②③ 必须带本机判断**：连远程服却拿本机配置里的密码去凑，只会多一次莫名的「密码错误」
- 本机服务端靠 ③ 保证「重启启动器也能自动接管」，不需要存密码
- 「显示密码」只在勾了「记住密码」时可用（`CheckRconShowPass.IsEnabled = Remember`），
  否则框里本来就是用户自己输的明文，这个开关没有意义
- 勾「记住密码」弹**确认框**（`IsWarn`）：说明会加密落盘、等于本机留一份管理员凭据、
  公用机或远程服建议不勾；取消则回滚勾选状态
- 「重新生成」仍写进 `server.properties`（**那是权威源**），并自动把新密码显示出来

**一条不能忘的认知**：RCON 密码**以 `server.properties` 的 `rcon.password` 为准**，
启动器只是生成器和抄写员 —— 任何「只存在我们设置里」的设计都会把用户绑死在本启动器上。

产物 `2026.11.01-custom.22`，0 错误，已部署。**未实机验**：连上后密码框是否清空、
勾「记住密码」的确认框与取消回滚、「显示密码」在未记住时是否置灰。

**续工锚点（2026-11-01 14:58 · RCON 密码可见可控 · 已被上一条取代，留作背景）**

代码基线 **`2026.11.01-custom.21`**（自用版，0 错误，`sha256=9c05ea81…480d`，已部署 `pcl2ziyong\`）。

**用户纠正的是「重置」的语义**：`custom.20` 把重置做成「清空密码」，用户指出
**这会让人只能靠我们的启动器** —— 密码只剩在我们设置里，换个启动器或直接命令行开服就不知道密码了。

**根本认知**（写在这里免得再犯）：RCON 密码**以 `server.properties` 里的 `rcon.password` 为准**，
启动器只是「生成器 + 抄写员」。所以设计目标是**让用户随时能看到、抄走**，而不是藏起来。

**改法**：
- 密码框默认仍是圆点占位（`RconPassMask = ••••••••`，长度也不暴露），
  但新增 **「显示密码」勾选** —— 勾上立刻显示明文，方便抄到别的工具
- **「重新生成」取代「重置」**：生成新随机密码 → 写设置 → **写进 `server.properties`** →
  断开旧连接 → **自动勾上「显示密码」把新密码摆出来** → Hint 里再念一遍
- 没选过 `server.jar` 时只存设置，Hint 里说明「开一次服后会写进去」

产物 `2026.11.01-custom.21`，0 错误，已部署。**未实机验**：显示密码勾选、重新生成后重开服能否连上。

**续工锚点（2026-11-01 14:52 · RCON 密码不回显 + 重置按钮 · 已被上一条取代，留作背景）**

代码基线 **`2026.11.01-custom.20`**（自用版，0 错误，`sha256=de669e97…d197`，已部署 `pcl2ziyong\`）。

**用户报**：远程 RCON 的密码框是明文，要像密钥一样保护；再加个重置按钮。

**做法（没用 WPF 的 PasswordBox）**：PCL 全项目没有密码控件，原生 `PasswordBox` 样式和
`MyTextBox` 不统一，硬塞会很突兀。改用**占位符回显**：
- `RconRefresh` 里密码存在就显示固定 8 个圆点 `••••••••`（**长度都不暴露**）
- `GetRconPassword()`：框里还是占位符 → 退回 `LinkRconPass`；否则用用户新输入的
- 连接 / 自动接管都改走 `GetRconPassword()`

**重置按钮**（红色「重置密码」）：
清 `LinkRconPass` + 把 `server.properties` 里 `rcon.password=` 置空（**必须清文件**，
否则 `EnsureRconConfigured` 会沿用文件里的旧密码）+ 断开现有 RCON 连接 + 刷新界面。
下次开服重新生成随机密码。

产物 `2026.11.01-custom.20`，0 错误，已部署。**未实机验**：占位符显示、重置后重开服能否用新密码连上。

**续工锚点（2026-11-01 14:50 · 用 RCON 解决「重启后接管」· 已被上一条取代，留作背景）**

代码基线 **`2026.11.01-custom.19`**（自用版，0 错误，`sha256=720d34a8…b3b9e`，
部署时 exe 被占用，暂存 `pcl2ziyong\_pending\`）。测试项：`17-tests` 的 **T-A7**。

**问题**：`custom.18` 解决了「重开后知道服务端还在跑」，但**接管不了** ——
`ServerProc.StandardInput` 是管道，PCL 一退就断，重启后没有第二条控制通道。

**用户提的思路是「单独起一个小程序到托盘」。没走这条，改用 RCON**，理由：
- 托盘宿主 = 新可执行文件 + IPC 协议 + 构建脚本改动 + 常驻内存；而且 PCL 有单实例
  `PCL_SingletonMutex`，复用自身做 headless 宿主还得绕开它
- **RCON 就是现成的控制通道**（`custom.16` 已经实现并验证过），TCP 不受启动器生命周期影响
- 重启后连上去，发命令、发 stop 全都行 —— 目标达成，代价接近零

**做法**：
1. `EnsureRconConfigured(Dir)`：`BtnServerStart` 里**自动**往 `server.properties` 写
   `enable-rcon=true` / `rcon.port=25575` / 随机 16 位 `rcon.password`（存 `LinkRconPass`）；
   文件里已有非空密码就沿用户的
2. `AutoConnectRcon()`：`ServerLeftoverRefresh` 查到遗留进程后自动连 RCON，
   控制台打 `[RCON] 已自动连上 …`，命令框立刻可用
3. `SendServerCommand` 优先级调整为 **本机进程活着走 stdin、否则走 RCON**
   （本机走 stdin 才能拿到实时 stdout；同时避免 RCON 回显与 stdout 重复）
4. `BtnServerStop` 的遗留分支：**RCON 连上就走优雅 stop**（等 20 秒），连不上才强杀

**注意**：老服务端的 `server.properties` 没有那三行 → **必须用新版 PCL 重开一次服**才有 RCON。

**续工锚点（2026-11-01 14:42 · 关闭启动器时的服务端行为 · 已被上一条取代，留作背景）**

代码基线 **`2026.11.01-custom.18`**（自用版，0 错误，已部署 `pcl2ziyong\`）。
测试项见 `17-tests-2026-11-01.md` 的 **T-A6**。

**先查清的事实**：`ServerProc` 是普通子进程，`FormMain.EndProgram` 里没有任何杀它的代码，
`PageLinkMain.TryExit` 原来是 `Return False` 的空实现。所以**关掉启动器服务端本来就不会死**，
会继续后台跑。真正的毛病是：PCL 重开后 `ServerProc` 变量丢了 → 界面显示「未启动」，
但服务端还在跑 —— 状态不一致，且再也接管不了控制台。

**改法（四点）**：
1. **记录 PID**：`SaveServerProcRecord` 在启动服务端时写 `LinkServerProcPid` / `Dir` / `Time`；
   Java 直启和 `run.bat` 两条路都写
2. **重开后识别**：`ServerLeftoverRefresh`（`ServerRefresh` 里调）按 PID +
   **进程名校验**（必须 java / cmd，PID 会被复用，只按数字判会误杀别的程序）找回它，
   `LabServerProc` 显示「后台仍在运行（PID …）」
3. **关闭时问清楚**：`TryExit` 实现 —— 服务端在跑时弹「让它继续跑 / 一起关掉」，
   各自后果写进弹窗；选一起关则 `StopServerGracefully`
4. **设置项** `LinkServerKeepOnExit`（默认 True = 保留），联机页勾选框
   「关闭启动器时一并关掉本机服务端」控制它；**静默退出**（托盘/快捷键）时按设置走、不弹窗

**关键细节**：
- `StopServerGracefully` 必须**同步**等待 —— 异步的话启动器先退了、服务端还在，等于没关。
  发 `stop` 后等 10 秒，超时 `taskkill /F /T /PID`（`/T` 杀进程树，因为 run.bat 那条路
  记录的是 cmd，java 是它的子进程）
- 启动器重启后 stdin 已断，接手不了，只能强杀 —— 所以「关闭服务器」按钮在查到遗留进程时
  改走「强制结束」分支，弹窗明说会丢未保存进度

产物 `2026.11.01-custom.18`，0 错误，已部署。**未实机验**：见 T-A6。

**续工锚点（2026-11-01 14:32 · 备份完成 + 发布版已推 GitHub tag 3 · 已被上一条取代，留作背景）**

代码基线 **`2026.11.01-custom.16`**（自用版，0 错误）。**发布版已推送**：
`4huang0yan4/pcl-shiguangxiugaiban` → main `15a5cb0`，tag `3`。

**备份**：`<备份盘>\2026-11-01-custom.16\`（734 MB），四份：
`pcl-src`（排除 obj/bin/_tools）、`PCL-custom`（含全部 versions 归档）、`launcher-dev`、`jiyiku`。
robocopy 退出码 1（= 有文件被复制，正常）。

**推送口径（用户反复强调：本地=自用版，GitHub=发布版）**：
- 用 robocopy 从自用版同步，**`/XF` 精确排除那 4 个待回退文件**
  （`PageSetupUI.xaml` / `ModSecret.vb` / `ModCustom.vb` / `FormMain.xaml.vb`），
  它们保持仓库版现状不动 —— 本轮这 4 个文件确实没改，所以排除即正确
- `launcher-dev` 同步再排除 `08c` / `12` / `15` / `16` / `17`（前四个是既有口径；
  `17` 是因为它写了「主题解锁实现必须剔掉」，等于自证另有解锁版）
- 另外 `git reset` 掉三类不该进仓库的东西：`launcher-dev/_backup/`、`launcher-dev/evidence/`、
  `pcl-src/build/*.zip` 与 `*.orig`（合并残留）

**合规自检（两棵树一起，别只查一棵）**：`ThemeCheckAllImpl` / `EnsureCustomTheme` /
`去掉了全部预设配色` 全 0；`IsEnabled="False"` 各 **10**（上游 12，差 2 是已删的联机页）。
推完又用 raw.githubusercontent 拉远端文件复核：`custom/ModCustom.vb` 0 / `PageSetupUI.xaml` 10 / 0。

**踩到的坑**：robocopy 全量覆盖后 `git status` 报 334 个 M，其中 **284 个是换行符假象**
（`core.autocrlf=true` + 工作区 LF）。`git diff --name-only` 一查只剩 **50 个真改动**，
`git add -A` 后假象自动消失。**下次别被 334 这个数字吓到，先看 `git diff --name-only | wc -l`。**

**提交**：`0153e9b`（功能）→ `15a5cb0`（CHANGELOG 汇总）→ tag `3`。

**续工锚点（2026-11-01 14:20 · 远程 RCON + 第三方图标 + 添加入口文案 · 已被上一条取代，留作背景）**

代码基线 **`2026.11.01-custom.16`**（0 错误，`sha256=5826be6a…9107`，部署时 exe 被占用，暂存 `pcl2ziyong\_pending\`）。
测试项见 `17-tests-2026-11-01.md` 的 **T-A5**。

**1. 远程服务器 RCON**（用户要的「接入其他家的服务器」的远端部分）
联机页控制台下面新增一块「远程服务器（RCON）」：地址 / 端口（默认 25575）/ 密码 + 连接 / 断开。
**命令输入框复用**：`SendServerCommand` 先看 `Rcon.IsOpen`，连上就走 RCON，否则走本地进程 stdin。
设置项 `LinkRconHost` / `LinkRconPort` / `LinkRconPass`（密码 `Encrypted:=True`，等同管理员凭据）。

协议实现放在 `PageLinkMain` 里的**嵌套类 `RconClient`**（项目 vbproj 是显式列文件的，
新建 .vb 要改工程，嵌套类省事）：
- 包 = `长度(4) + 请求号(4) + 类型(4) + 正文 + \0\0`，全部小端
- 类型：`3`=AUTH、`2`=EXECCOMMAND、`0`=RESPONSE_VALUE、`2`=AUTH_RESPONSE
- 认证应答可能是两个包（先空 RESPONSE_VALUE 再 AUTH_RESPONSE），失败时请求号为 **-1**
- 正文长度 = 包长 − 10；用 UTF-8 编解码（中文命令）

**RCON 的固有限制**（要跟用户说清）：看不到服务端实时日志，只能拿命令回显；
必须服务端开 `enable-rcon=true` 且知道 `rcon.password`。

**2. 第三方登录图标**：`RadioLoginType3` 加 `Logo`（自己画的 2U 机架 path，EvenOdd 挖两个灯位），
`LogoScale=0.9`。PCL 的 `Logo` 常量里没有服务器图标，只能手写。

**3. 「添加已有」悬停文案**：明确写「只用于本机磁盘上已经装好的目录」，并指路
「云服务器 / 别人机器上的改用下面的远程服务器（RCON）」。

**续工锚点（2026-11-01 14:15 · 外置登录入口 / 安装器提示 / 美西螈差异 / 添加已有服务端 · 已被上一条取代，留作背景）**

代码基线 **`2026.11.01-custom.15`**（0 错误，`sha256=a9b95fd0…bb4a`，已部署 `pcl2ziyong\`）。
测试步骤见 **`17-tests-2026-11-01.md`**（本轮全部待验项都在那）。

**1. 外置登录没有入口（用户 14:05 报「界面不显示」）** —— 真 bug。
登录页只有 `RadioLoginType5`（正版）/ `RadioLoginType0`（离线）两个按钮，
`RadioLoginType2`（统一通行证）在上游就被注释掉了，**Authlib 从来没做过切换按钮**，
只能靠 `Settings.LoginPageType = 4` 手工改设置才能进。
改法：`PageLaunchLeft.xaml` 的 `PanType` 加 `RadioLoginType3`（Text「第三方」，
`McLoginType.Auth = 3` 所以编号必须是 3，`FindName("RadioLoginType" & LoginType)` 靠这个）；
`Case 0` 分支加 `RadioLoginType3.Checked` 判断；`RadioLoginType_Change` 的 `Handles` 补上它。
网格从 5 列扩到 6 列（末尾补一个 `1*` 保持居中）。

**2. 安装器网络改「提示开加速器」**（用户选择不做重方案）：选到 Forge/NeoForge/Fabric 时，
`LabLoaderHint` 追加「安装时要连官方仓库，国内建议先开加速器 / 代理。」
`ReportInstallerFailure` 文案也改成三段式（截图码 + 摘要 + 开加速器 + 备选原版/Paper）。

**3. 美西螈差异检测**：`CountOutdatedProfiles` 比 `profiles\<名>\mods` 与
`versions\<名>\mods` 的 **jar 数量**（不比目录时间——PCL 侧一启动游戏就刷新目录时间，会漏判）。
扫描结果里多一句「N 个的 mod 数量对不上，建议重新转换」。

**4. 添加已有服务端目录**（用户要的「接入其他家的服务器开服」的本地部分）：
「已装服务端」下拉右边加 **「添加已有」**，选目录后校验有 `server.jar` /
`fabric-server-launch.jar` / `run.bat` 之一，登记进 `LinkServerList`（标签用目录名）。
为此把 `PageServerInstall.AddServerRecord` 改成 `Public Shared`。

**关于远程服务器**：PCL 没法托管远端进程。真要做只能走 **RCON**
（`enable-rcon=true` + `rcon.port` + `rcon.password`），能发命令、看不到实时日志，
且要求用户有 RCON 密码。**本轮没做**，等用户确认是否需要。

**续工锚点（2026-11-01 13:58 · 修控制台中文乱码 · 已被上一条取代，留作背景）**

代码基线 **`2026.11.01-custom.11`**（0 错误，`sha256=3af60d47…6dae`，已部署 `pcl2ziyong\`）。

**用户报的现象**：控制台发 `say 测试`，游戏里显示成未知符号（乱码）。

**根因**（两个方向都要治）：
1. **写 stdin**：.NET Framework 的 `Process.StandardInput` 是 `StreamWriter`，按系统 ANSI（GBK）编码；
   而 Java 18+ 起 `System.in` 默认 UTF-8（JEP 400）→ 发出去的中文就是乱码。
   **`ProcessStartInfo.StandardInputEncoding` 是 .NET Core 2.1+ 才有的属性，.NET Framework 没有**，
   所以不能靠设属性解决，只能改走字节流。
2. **读 stdout**：Java 在 Windows 上原生输出编码是系统代码页，
   即使 `file.encoding=UTF-8` 也不一定改到 stdout。

**修法（三处一起）**：
- `WriteServerLine`：`Encoding.UTF8.GetBytes(Line & vbLf)` 写 `StandardInput.BaseStream`，
  绕开 StreamWriter 的编码。全部 `StandardInput.WriteLine` 都换成它（stop / op / deop / 自定义命令）
- 启动参数加 `-Dfile.encoding -Dstdout.encoding -Dstderr.encoding -Dsun.stdout.encoding
  -Dsun.stderr.encoding -Dsun.stdin.encoding` 全设 UTF-8
- `Psi.StandardOutputEncoding / StandardErrorEncoding = UTF8`（这个 .NET Framework **有**）
- `run.bat` 那条路（Forge/NeoForge）：JVM 参数全在 `user_jvm_args.txt`，
  所以 `BtnUseInstalled` 走 bat 前先调 `PageServerInstall.EnsureServerJvmArgs(D)` 把编码参数补进去
  （为此把 `EnsureServerJvmArgs` / `SuggestServerMemoryMb` 改成 `Public Shared`）

**顺带**：`BtnServerStart` 的 `-Xmx2G -Xms1G` 是写死的，改成按物理内存算（与安装页同一套规则）。

**注意**：**已经在跑的服务端必须重启**才生效，JVM 参数只在启动时读。
**已实机验证**：用户 13:58 重启服务端后发中文命令正常。

**续工锚点（2026-11-01 13:52 · 服务端控制台面板 · 已被上一条取代，留作背景）**

代码基线 **`2026.11.01-custom.8`**（0 错误，`sha256=da8c85ec…0112`，已部署 `pcl2ziyong\`）。

**做的**：联机页「服务器」卡片底部加控制台 —— 只读多行输出框（Consolas，NoWrap，双向滚动）
+ 命令输入行（回车发送）+ 发送 / 清空按钮。

**关键发现**：`ServerOutput` **本来就已经挂着** `OutputDataReceived` / `ErrorDataReceived`，
只是全部写进 `server-run.log` 文件、没给人看。所以不用重新接流，只要在同一个回调里
`RunInUi` 追加到界面即可 —— 改动比预想小得多。

**几个细节**：
- 命令自动去掉前导 `/`：服务端从 stdin 读的是不带斜杠的形式，玩家习惯带
- 长度用自维护的 `ServerConsoleChars` 计数，**不要每行读 `TextBox.Text.Length`**（那是 O(n) 重建字符串，刷屏会卡）
- 超过 200000 字符砍掉前一半，避免长时间开服把内存吃满
- 文件日志照旧保留，两条路并行

**验证要点**：启动后能否实时看到输出；发 `list` / `say` 是否生效；刷屏（如登录多名玩家）时界面是否卡。

**续工锚点（2026-11-01 13:45 · 服务端配置补游戏规则 + 内存参数 · 已被上一条取代，留作背景）**

代码基线 **`2026.11.01-custom.6`**（0 错误，`sha256=43b05003…ea98`，部署时 exe 被占用，暂存 `pcl2ziyong\_pending\`）。

**做了什么**
1. 联机页「服务器」卡片补了第二组表单 **「游戏规则」**：难度（和平/简单/普通/困难）、
   游戏模式（生存/创造/冒险/观察者）、视距（3–32）、允许 PvP。
   读写都走 `server.properties`，与原有表单共用同一套「保留其余键」的写入逻辑。
   视距**留空 = 不写这一项**，避免把服务端原有值冲掉。
2. `EnsureServerJvmArgs`（`PageServerInstall.xaml.vb`）：Forge / NeoForge 安装成功后，
   把 `user_jvm_args.txt` 里那行 `# -Xmx4G` 换成实际值 —— 物理内存 1/4、夹 2G–8G、512M 取整。
   **已有非注释的 `-Xmx` 直接返回，不动用户设置**。

**纠正了两条记错的待办**（写 10-31 那份清单时没细看代码）：
- 「server.properties 编辑器」：联机页**本来就有**端口/人数/白名单/正版验证/MOTD 表单
- 「服务端启动/停止入口」：`BtnServerStart` / `BtnServerStop` **本来就有**，是 PCL 托管进程。
  真正缺的是**控制台输出面板**（现在只能「打开日志」看），已改成新待办

**剩下的都得上量级**：服务端控制台面板（要接 `StandardOutput` + 环形缓冲）、
Velocity/Waterfall 代理端、安装器网络重方案。

**续工锚点（2026-11-01 13:40 · 服务端管理员改为勾选列表 · 已被上一条取代，留作背景）**

代码基线 **`2026.11.01-custom.4`**（0 错误，`sha256=58449fad…cd38`，已部署 `pcl2ziyong\`）。

**最终形态**：联机页「服务器」卡片的玩家名单是**勾选列表**（`PanPlayerList` + `MyListItem(Type=CheckBox)`），
一行一个玩家，**勾上 = 管理员，取消勾选 = 普通玩家**。`.1` 那版先做的「输入名字 + 两个按钮」
在 `.2`~`.4` 换掉了 —— 用户要的是直观的勾选，不是填名字再点按钮。

**两条路径分开走**（这块最容易写错的地方）：
- **服务端在跑** → `ServerProc.StandardInput.WriteLine("op 名字")` / `deop`，立即生效。
  **不能直接改 `ops.json`**：服务端内存里有名单，改文件会被盖回去
- **没跑** → 写 `ops.json`，`uuid` 用现成的 `OfflineUuid`（该文件里早有，白名单写 `whitelist.json` 就用它），
  `level` 固定 4，`bypassesPlayerLimit=false`，下次开服生效

**勾选列表的三个坑**：
- `MyListItem.Check` **只在勾上时触发，取消勾选不触发**（`SetChecked` 里 `If value Then RaiseEvent Check`）。
  必须改用 **`Changed`** 事件，两种状态变化都有
- 程序设置 `Checked` 也会触发 `Changed`，重建列表时会自我递归 → 用 `IsRefreshingPlayers` 标志位挡住；
  同时用 `e.RaiseByMouse` 区分「用户点的」和「程序设的」
- 重建列表 = `Children.Clear()` + 重新 new，事件处理器随之丢弃，不用手动解绑

**其他决定**：
- 管理员判定按 **name** 对照，不按 uuid —— 离线服和正版服的 uuid 来源不同，混用时按 uuid 必然对不上
- 名单取白名单 ∪ 管理员：只开管理员、白名单为空时，否则界面一个管理员都看不到
- `ServerRefresh()` 现在会调 `WhitelistRefresh()`，进页面就有名单；`BtnServerStart_Click` 换 jar 后也刷一次
- 没选过 `server.jar` 时列表给出提示，而不是显示「还没有玩家」误导
- 走控制台后等 1.5 秒再刷新，服务端回写 `ops.json` 有延迟

产物 `2026.11.01-custom.4`，0 错误，已部署。**未实机验**：勾选 / 取消勾选是否即时生效、重启后勾选状态是否正确回显。

**续工锚点（2026-11-01 13:35 · 服务端管理员（OP）管理初版 · 已被上一条取代，留作背景）**

**续工锚点（2026-10-31 15:38 · 服务端七项 + 美西螈导入 + 自定义 UUID + 打开日志 · 已被上一条取代，留作背景）**

代码基线 **`2026.10.31-custom.65`**（0 错误，`sha256=f47b6af8…350c`，已部署 `pcl2ziyong\`）。

**本轮新增/修复（按时间）**

| 版本 | 内容 |
|---|---|
| `.54`/`.55` | 迁移弹窗补「取消」（`MyMsgBoxSelect` 的 Button2 默认为空 → 取消按钮被折叠、确定又禁用 → 弹窗关不掉）；加载器下拉把显示与值拆开（`Content` 给人看、`Tag` 存真值），Paper「构建 133」、NeoForge 补 `1.21.1-` 前缀，并按数字段倒序排（maven-metadata 是乱序的） |
| `.56` | 离线登录支持**自定义 UUID**（`LoginLegacyUuid`），并加「按名字生成」用标准算法 `MD5("OfflinePlayer:"+名字)` 算 |
| `.57`/`.58` | **扫描识别美西螈（Axolotl）**：它的结构是 `meta\versions`（版本）+ `profiles`（实例），目录名还不固定。按特征识别后转换：复制 json（改 `id`）+ jar + 游戏数据到根下 `versions\`，`libraries`/`assets` 建目录联接回 `meta\` |
| `.61` | 美西螈**重新转换（覆盖）**：扫描过滤原本会把已添加的目录剔掉，导致没有入口，改成美西螈目录永远保留 |
| `.62`~`.64` | 服务端六项：目录冲突检测、Java 版本校验、装完提示去联机页、server.properties 初写、Purpur/Folia、installer 走系统代理 |
| `.65` | 联机页「已装服务端」加**「打开日志」**：优先 `logs\latest.log`，没有就取 `logs` 下最新的 `.log*`；没在列表里选则退回到 `LinkServerJar` 所在目录 |

**这轮踩到的坑（都写进代码注释了）**

- `MyMsgBoxSelect` **不传 Button2 就没有取消按钮**，而这个弹窗未选中时「确定」是禁用的 → 死路
- 往 `New Setting() {...}` 数组初始化器中间插**整行注释会打断隐式续行**（BC30201），注释只能写行尾
- `MyHint` 是 `Border`，**没有 `TextWrapping` 属性**（内部 TextBlock 已经 Wrap）
- `DirectoryUtils.Delete` 删 PCL 侧转换副本前，**必须先确认源 json 存在**，否则删完转换失败连旧副本也没了
- MC 读 `server.properties` 用 ISO-8859-1，写中文注释会乱码 → 初写的那份不加注释

**已实机验证**（用户 15:38 确认）：扫描到美西螈 → 添加 → 能启动；Paper / NeoForge 下载正常；
迁移弹窗可取消；**目录冲突弹窗出现**；**Purpur 安装完成**；**装完联机页列表里有它**。

**未实机验证**：Folia 真下一次；Java 偏低弹窗；server.properties 被服务端接管；
带代理时 NeoForge 能否装上（用户网络未开代理，`-Djava.net.useSystemProxies=true` 实际效果待测）；
自定义 UUID 是否进 `--uuid`；重新转换覆盖是否正常。

**下一轮候选**：见第一节「服务端功能」下的四条未做项（Velocity/Waterfall、安装器网络重方案、
server.properties 编辑器、服务端控制台）。

**续工锚点（2026-10-31 15:00 · 插件可下载 + 存储分析 + 扫描导入 custom.50 · 已被上一条取代，留作背景）**

代码基线 **`2026.10.31-custom.50`**（0 错误，6,011,392 字节，`sha256=786ee91a…3f05`，已部署 `pcl2ziyong\`）。

**本轮修的两个 bug**

1. **插件页点开没版本**（连改 4 版才找到真因）：`MyResourceItem` 打开详情页时的
   `Select Case PageSubType` **漏了 `DownloadPlugin`**，`TargetType` 保持默认值 **0**；
   而 `ResourceTypes` 是 Flags 枚举、0 不匹配任何类别 → 详情页 `GetResults` 按 0 过滤 → 列表全空。
   前三轮改的 `project_type` / `loaders` / `categories` 都是**必要的**（Modrinth 把插件标成 `mod`，
   标记只在 `categories` 里），但只要这里漏一支就永远空白。已把默认值改成 `Any` 并补插件分支。
2. **服务端 Forge 装不上**：`Dialogs.SelectFolder` 返回的路径**带尾随反斜杠**，引发三连——
   双斜杠让 `FileUtils.Exists` 判为不存在（每次都重下原版）、`\\?\` 前缀下双斜杠非法（删文件报
   「文件名、目录名或卷标语法不正确」）、命令行 `"...\"` 尾斜杠转义引号（installer 收到 `D://xxx"` 报
   `Illegal char <">`）。现拿到目录立刻 `TrimEnd("\"c)` 并 `DirectoryUtils.Create`。

**本轮新增的三处功能**

- 版本设置 → 概览 → **占用空间**：后台统计实例各一级子目录（不写死清单，mod 自建目录也能看到）
- 选择版本页 → **扫描其他启动器**：扫 APPDATA / 用户目录 / 各固定盘三层内的 `.minecraft`，
  确认后**一次性写入** `LaunchFolders`（不能循环调 `AddFolder`，它内部异步，会并发覆盖设置项）
- 百宝箱 → **空间分析**：所有 MC 文件夹 + 启动器 `PCL\` 目录的占用，只读不改文件

**本轮踩的语言/API 坑（都在编译前后暴露）**

- VB 里 `Dir`、`CurDir` 都是**内置函数**，不能当变量名 → BC30068「表达式是一个值，不能作为赋值目标」
- `ModCustom` 是 **Module**，成员不能声明 `Shared` → BC30433
- `PathUtils.GetFileName` **不存在**（只有 GetFileNameWithoutExtension）→ 用 `IO.Path.GetFileName`

**明确不做**：红石联机（Axolotl 的第二个后端，是对方的私有中转服务，没有协议可接，且已有陶瓦）。

**续工锚点（2026-10-31 12:05 · 服务端与联机迭代收工 custom.40 · 已推 `95db7b2` · 已被下一条取代，留作背景）**

代码基线 **`2026.10.31-custom.40`**（`0 错误`；已部署 `pcl2ziyong\`，`sha256=a9bb7313…`）。

**本轮完成**（服务端 + 联机页）：
- 下载页新增「服务器」分区：版本分组（最新/正式/预览）、点版本进安装页（版本号 + 加载器 + 开始下载 + 返回）；
- 原版服务端下载走镜像 + 任务管理（Loader 在 UI 线程创建/登记）；安装器失败自动普通下载重试；
- Forge/NeoForge/Fabric 版本列表；Forge/Fabric installer 下载进任务栏，安装前先下原版 `server.jar` 并写 eula；
- 联机页：加入可选离线/正版（默认离线）、服务端配置（端口/人数/白名单/正版验证/MOTD）、白名单批量编辑与勾选删除、mods 目录管理、已装服务端下拉 + 用这个开服/打开目录/删除（支持 jar 与 run.bat）；
- 设置页自动更新模块（默认关，不发布 Release 资产）；LICENCE 统一 3168 字节 LF。

**GitHub**：`95db7b2`（含 CHANGELOG 汇总 custom.11~40）。**备份**：`<备份盘>\2026-10-31-custom.40\`（记忆库/源码/launcher-dev/pcl-source-github/产物，diff 零差异；700 MB）。

**待实机**：Forge/Fabric 安装器退出码、安装后联机页直接开服、白名单勾选删除。

**续工锚点（2026-10-31 11:30 · 加入可选登录方式 + 默认离线 custom.10 · 已推 `29cb014` · 已被上一条取代）**

代码基线 **`2026.10.31-custom.10`**（`0 错误`；已部署 `pcl2ziyong\`，`sha256=10e0a7f8…`）。

**问题**：实测「加入服务器」进不去——正版服务器对非正版限制多，**离线模式更稳**。

**改动**：
- 「加入时用」下拉（**离线登录默认** / 微软正版登录）→ 加入时 `Settings.Set("LoginType", …)`；
- 首次开服若没有 `server.properties`，按**离线优先**创建（`online-mode=false`）。

**GitHub**：`e79a73d`（代码）+ `29cb014`（CHANGELOG）。**待实机**：离线能进、正版能进（若已登录）、默认离线配置生效。

**续工锚点（2026-10-31 11:15 · 白名单与 mods 管理 custom.9 · 已推 `af3cad5` · 已被上一条取代）**

代码基线 **`2026.10.31-custom.9`**（`0 错误`；已部署 `pcl2ziyong\`，`sha256=29807fb0…`）。

**服务器卡片增强**：
- 白名单：多行文本（每行一个玩家名）→ 保存写 `whitelist.json`（离线 UUID = MD5("OfflinePlayer:" + 名字)，版本 3），并把 `white-list` 置 true；在线模式 UUID 需开服后 `whitelist add`。
- mod 管理：「打开 mods 目录」「添加 mod」（选 jar 复制到 `mods\`，重名覆盖）；服务端须是 Fabric/Forge 才加载。

**GitHub**：`605880b`（代码）+ `af3cad5`（CHANGELOG）。**待实机**：白名单保存/载入、mod 复制、开服后白名单是否生效。

**续工锚点（2026-10-31 11:00 · 服务端配置/停止/版本检测 custom.8 · 已推 `74de68c` · 已被上一条取代）**

代码基线 **`2026.10.31-custom.8`**（10:34 编出，`0 错误`；已部署 `pcl2ziyong\`，`sha256=92a52190…`）。

**服务器卡片增强**：
- 配置编辑：端口 / 最大人数 / 白名单 / 正版验证 / MOTD → 写 `server.properties`（保留其余键）；
- 关闭服务器：stdin 发 `stop`，15 秒未退出强杀；
- 启动改为重定向 stdin/stdout，日志写 `server-run.log`，解析 `Starting minecraft server version` 与 `Done`；
- 加入前版本检测：服务端版本 ≠ 客户端 `VanillaName` → 提示先装对应版本（解决「第一版直接启动、版本不符没进去」）。

**发布口径**：仓库只推源码 + `CHANGELOG.md` + `手动更新.md`（新增，给不用智能体的人），不发二进制；`AGENTS.md` 第八节是智能体操作单。

**GitHub**：`74de68c`。**待实机**：配置保存、优雅停止、版本不一致提示。

**续工锚点（2026-10-31 10:55 · 更新改走源码+智能体，不发布二进制 · 已推 `448dce0` · 已被上一条取代）**

**方向变更**：不做 Release 资产（不发 exe / zip）。仓库只放源码 + `CHANGELOG.md` + `AGENTS.md` 第八节「跟进更新操作单」；更新由智能体读 `CHANGELOG.md`、按操作单同步源码并编译。

- `Modules/UpdateSelf.vb` 保留但**默认关闭**（设置 → 其他 →「更新」）；仓库没有 Release 资产时它只会提示「已是最新」，不阻塞。
- 第八节硬规则：同步时**不要整拷 4 个回退文件**（`PageSetupUI.xaml` / `ModSecret.vb` / `ModCustom.vb` / `FormMain.xaml.vb`）。
- GitHub：`448dce0`（CHANGELOG + AGENTS 第八节）。

**续工锚点（2026-10-31 10:40 · 服务器卡片增加「一键开本地服务端」custom.6 · 已推 `73ac127` · 已被上一条取代）**

代码基线 **`2026.10.31-custom.6`**（10:19 编出，`0 错误`；已部署到 `pcl2ziyong\`，`sha256=b5050475…`）。

**方案 A 落地**：联机页「服务器」卡片底部加「开本地服务端」按钮 + 状态行：
- 首次点选 `server.jar`（路径存 `LinkServerJar`），之后直接用；
- 自动写 `eula.txt`（`eula=true`），省掉首次启动必停那一步；
- Java 取「最近一次启动游戏用的」→ 否则 `Configs.JavaList` 第一个；都找不到则提示去「设置 → 启动 → Java」；
- 从 `server.properties` 读 `server-port`（缺省 25565），`Process.Start` 最小化启动 `java -Xmx2G -Xms1G -jar … nogui`，保留控制台窗口供看日志；
- 后台每秒 TCP 探测端口，就绪后回填 `127.0.0.1:端口` 并提示「点加入服务器」。

**服务端下载入口的位置（用户找不到）**：不是下载页左侧分类，而是**版本列表每一项右侧的图标按钮**（另存为 / 更新日志 / **下载服务端**），点它才选保存目录并下载原版 server jar + `Launch Server.bat`。

**GitHub**：`73ac127` 已推送（6 文件 +184，build 与 custom 各 3 份）。**待实机确认**：按钮能选 jar、eula 自动写、服务端能起、就绪后地址回填。

**续工锚点（2026-10-31 10:30 · 联机页新增「服务器」卡片 custom.5 · 已推 `5a93466` · 已被上一条取代）**

代码基线 **`2026.10.31-custom.5`**（10:12 编出，`0 错误`；已部署到 `pcl2ziyong\`）。

**需求**：联机页加第三个入口「服务器」，填地址直接进服，勾选后启动游戏默认进入。

**实现**（4 个文件，build + custom 各一份）：
- `Pages\PageLink\PageLinkMain.xaml`：`PanSelect` 的 Row5 加 `CardServer`（服务器地址、备注名称、「加入服务器」、勾选「启动游戏时默认进入」、状态行）；
- `PageLinkMain.xaml.vb`：`ServerRefresh` / `ServerStatusRefresh` / `ServerAddressLooksValid`（host、host:port、`[IPv6]:port`，挡掉空格与 `://`）/ `BtnServerJoin_Click` / `CheckServerDefault_Change`；`PageEnter` 时刷新；
- `Pages\PageSetup\Settings.vb`：登记 `LinkServerAddress` / `LinkServerName` / `LinkServerDefault`（Registry 源）；
- `Modules\Minecraft\ModLaunch.vb`：`McLaunchArgumentsGame` 里取值顺序改为「本次指定 → 勾选的默认直连 → 版本自带 `VersionServerEnter`」。

**行为**：「加入服务器」= 保存地址并 `McLaunchStart(New McLaunchOptions With {.ServerIp = Addr})`，没选中版本时只保存并提示。勾选默认后，从主页「启动游戏」也会带上 `--quickPlayMultiplayer`（1.20+）或 `--server/--port`（旧版）；取消勾选即恢复原样。

**GitHub**：`5a93466` 已推送（8 文件 +288/−2，build 与 custom 各 4 份）。
**待实机确认**：联机页能显示第三张卡、地址格式校验、点「加入服务器」能启动进服、勾选后主页启动默认进服。

**续工锚点（2026-10-31 10:20 · 设置页新增「卸载与清理」custom.4 · 已推 `d087065` · 已被上一条取代）**

代码基线 **`2026.10.31-custom.4`**（10:06 编出，`0 错误`；已同步部署到 `pcl2ziyong\`，sha256 见 `build-info.txt`）。

**新增**：设置 → 其他 →「卸载与清理」卡片（`PageSetupSystem.xaml` + `.xaml.vb`）：
- 强提醒列出「会删：PCL 文件夹、主程序与运行库、注册表 `HKCU\Software\PCLDebug`」与「不会删：`.minecraft`、你自己放进该文件夹的文件」；
- **两次确认**（第一次强提醒，第二次「删除并退出」）；
- 删除方式：写 `%TEMP%\Uninstall\PCL-Uninstall.ps1`，延迟 3 秒后 `Remove-Item` 指定项 + 删注册表，**不走回收站**，脚本自删，启动器立即退出；
- 路径校验：必须存在 `Plain Craft Launcher 2.exe`，且 `Paths.Base` 去尾斜杠后长度 > 4，否则中止。

**注意**：这是真删除。测试前先确认已有备份；`pcl2ziyong\` 里的 `.minecraft` 不在删除清单（只删 `PCL\`、exe、exe.config、`libwebp.dll`）。

**GitHub**：`d087065` 已推送（build / custom 各两份，4 文件 +130/−2）。**待实机确认**：设置页能显示卡片、两次确认正常、取消后不删除。

**续工锚点（2026-10-31 10:15 · 凭据框第三版实机通过；补记部署目录 · 已被上一条取代）**

**用户实机确认 `custom.3` 正常**：选中密文拖不出去、点击即全选、`Ctrl+V` 替换。

**这次为什么「改了不生效」**：编译产物在 `gongtong\PCL-custom\`，而日常运行的是**移出来的** `<本地>\pcl2ziyong\`。两边 exe 不同步时，测到的还是旧版（当时是 `custom.16`）。

- 已把 `custom.3` 复制到 `pcl2ziyong\`，旧版备份为 `_backup\Plain Craft Launcher 2.exe.custom.16.2026-10-31`。
- **以后每次出包（至少让用户实机验证前）必须同步**：`cp PCL-custom\Plain Craft Launcher 2.exe <本地>\pcl2ziyong\`，旧版先备份到该目录 `_backup\`，回读 `sha256` 核对。
- 判据：`pcl2ziyong\Plain Craft Launcher 2.exe` 的 sha256 必须等于 `versions\<label>\build-info.txt`。

**GitHub 文档补充（2026-10-31）**：仓库新增 **`使用说明.md`**（129 行：安装/登录/联机/导出/百宝箱/常见问题/更新卸载），README 顶部加入口，提交 `6472875`。

**注意**：不能直接把 `PCL-custom\使用说明.md` 上传——那是自用版口径（写了主题解锁、`secrets.env` 老路、本机路径如 `<本地>\PCL\.minecraft`），与仓库合规版矛盾；公开版是重写的，只保留两边一致的功能。

**续工锚点（2026-10-31 10:05 · 凭据框第三版：从 MouseDown 源头禁拖拽 · 已推 `7ab37f5` · 已被上一条取代）**

代码基线 **`2026.10.31-custom.3`**（09:54 编出，`0 错误`；`sha256` 见 `versions\2026.10.31-custom.3\build-info.txt`）。

**前两版为什么还漏**：只拦 `PreviewMouseMove` 不够——WPF 拖拽在 `MouseDown` 之后、`MouseMove` 阈值处由 OLE 接管，`PreviewMouseMove` 的 Handled 压不住 TextBox 的类处理。Alt+Tab / 最小化那条第二版已修好（缩略图不再显明文），但「选密文拖到搜索框」仍能拖出。

**第三版**：`PreviewMouseLeftButtonDown` 里 `Box.Focus()` + `Box.SelectAll()` + `e.Handled = True`——**没有「按住拖动」这一步，拖拽链路从源头断掉**。点击即全选，`Ctrl+V` 粘贴替换整段；键盘输入与 `Ctrl+A` 不受影响。

**代价**：不能用鼠标点选 / 拖选（点击 = 全选），改单个字符只能清空重填或用键盘。

**冒烟**：启动 8 秒，版本 `测试版 2026.10.31-custom.3`，无未知错误。**GitHub**：`7ab37f5` 已推送。

**待实机确认**：选密文拖到搜索框应拖不动；点击框是否全选；`Ctrl+V` 是否替换整段。

**续工锚点（2026-10-31 09:55 · 凭据框第二版：彻底禁拖拽 + 最小化缩略图 · 已推 `4548ce0` · 已被上一条取代）**

代码基线 **`2026.10.31-custom.2`**（09:49 编出，`0 错误`；`sha256` 见 `versions\2026.10.31-custom.2\build-info.txt`）。

**第一版为什么不够**（用户实机反馈）：拖拽仍能拖出、最小化/切窗口后任务栏缩略图还能看到明文、「偶尔才拦住」。

- **拖拽**：WPF 的拖拽启动阈值只有 4px，等 `PreviewMouseMove` 能判断「已拖出输入框」时 OLE 已接管，之后不再有 WPF 鼠标事件。**只能整段拦**——左键按下即 `e.Handled = True`。代价：**不能用鼠标拖动选择**（点击定位、双击选词、Ctrl+A、Shift+方向键、Shift+点击都正常）。
- **缩略图**：DWM 会把最小化那一刻的画面冻成缩略图，`Deactivated` 后再复位已经晚了。新增 `HwndSource` 钩子拦 `WM_SYSCOMMAND / SC_MINIMIZE`，**先复位再放行**；同时挂 `StateChanged` 与 `IsVisibleChanged`，复位时 `InvalidateVisual + UpdateLayout` 强制重绘。

**冒烟**：启动 9 秒，版本行 `测试版 2026.10.31-custom.2`，无「未知错误」/`KeyNotFoundException`。

**GitHub**：提交 `4548ce0` 已推送（同一文件 build + custom 两份）。

**待实机确认**：① 揭盖后拖不动；② 最小化 / Alt+Tab 后缩略图无明文；③ 双击 / Ctrl+A 选词仍可用。

**续工锚点（2026-10-31 09:45 · 凭据框加固 custom.1，已推 GitHub `3b63f8d` · 已被上一条取代）**

代码基线 **`2026.10.31-custom.1`**（09:33 编出，`0 错误`，5,939,712 字节，`sha256=bdb9bda8…e676`）。

**改了什么**（只动 `Pages\PageSetup\PageSetupSystem.xaml.vb`，已同步 `custom\`）：
1. **切窗口复位**：`AttachCredentialGuards()` 挂主窗口 `Deactivated` 与页面 `IsVisibleChanged`；按住「显示」后 Alt+Tab / 切页立即恢复 `●` 遮罩（此前只靠 MouseUp/MouseLeave）。
2. **禁拖拽复制**：TextBox 把选中文字拖到外部是独立于剪贴板的通道。`PreviewMouseMove` 在拖出输入框时清空选区，`DataObject.AddCopyingHandler` 兜底取消数据打包。

**冒烟**：完全权限启动 10 秒，`Log1` 版本行 `测试版 2026.10.31-custom.1`、无「未知错误」。日志里的 `McInstance` NullReference 是**测试目录结构**导致（`PCL-custom\PCL\Setup.ini` 的 `LaunchFolderSelect:$` 把归档目录 `versions\` 当游戏版本扫），与本次改动无关。

**GitHub**：重新 clone 到 `gongtong\pcl-source-github\`（保留），只同步这个文件（build + custom 两份），提交 `3b63f8d` 已推送。推送前按 `16` 的 4.0 节自查：两侧 `EnsureCustomTheme` / `ThemeCheckAllImpl` 均 0、`IsEnabled="False"` 各 10、`ThemeCheckAll` 空实现。

**未实机验证**：按住显示 + Alt+Tab、拖拽选中文字出框，需在界面里点一次确认。

**续工锚点（2026-10-31 08:45 · 覆盖层第三次被污染，历史已重写并推 `4dd745a` · 已被上一条取代）**

**用户报「历史还是有解锁版」，属实。** 前一天的「解锁代码 0」结论是假的——检查命令把路径写死成
`pcl-src/build/...`，`custom\` 整棵树没被扫到：

- 当时 HEAD（`81d5a20`）的 `custom/Modules/ModCustom.vb`（2 处）、`custom/FormMain.xaml.vb`（1 处）带解锁实现
- 初始提交 `c7de439` 的 `custom/Pages/PageSetup/PageSetupUI.xaml` 还含旧版「去掉了全部预设配色」文案

**处理**（逐提交把 `custom` 对齐该提交自己的 `build`）：

1. 工作树：用 `build/` 覆盖 `custom/` 的 `ModCustom.vb`、`FormMain.xaml.vb`
2. `git filter-branch --force --prune-empty --tree-filter`：每个提交把它的 `build/` 四个文件复制到
   `custom/` 对应路径，历史每一代都自洽（`--prune-empty` 顺手删掉变空的「对齐」提交）
3. 清 `refs/original` + `reflog expire --expire=now --all` + `gc --prune=now`
4. `push --force` → 本地与远端均为 **`4dd745a`**（8 提交 / 710 文件 / 13.00 MiB）

**验证**：8 提交 × 2 位置（build / custom）共 16 项，解锁关键字全 **0**，`IsEnabled="False"` 全 **10**；
旧 SHA `c7de439` / `81d5a20` / `78a7f1f` / `eed7f19` 全部 `cat-file -e` 失败。

**这是同一个坑的第三次**（10-30 两次，10-31 一次）。根因不是「忘记同步」，而是**自检只查一棵树**。
`16-dual-track.md` 4.0 节已改成两棵树一起查，并写明判据与「清完必须验旧 SHA 不可达」。
**以后任何「解锁 0」的结论，必须同时给出 build 与 custom 两侧的数字，否则无效。**

**目录口径补充**：仓库侧 `launcher-dev` 比本地少 4 份（`08c` / `12` / `15` / `16`）是**有意的**，
不是漏同步——`15` 写明「自用版保持原样」、`16` 是双轨对照表，公开等于自证另有解锁版。

**仓库**：10-31 几经反复，最终建了**新名字**的仓库 <https://github.com/4huang0yan4/pcl-shiguangxiugaiban>（Public）。
历史压成**单个孤儿提交** `1e7da05`（`parents: []`，710 文件 / 13.00 MiB）。
原因：此前那 8 个提交虽然代码已全干净，但**提交说明本身**写着「把主题解锁实现带进了仓库」「此前仍是解锁版」
这类描述，公开等于自己留把柄，所以重建为一条中性说明。原 8 提交历史保留在本地分支 `history-backup`（未推送）。
旧仓库 `pcl-shiguang` 已删除，其旧 SHA 在新库全部 404（含 `c7de439`、`4dd745a`）。
**本地工作副本已于 08:45 删除**（只保留自编版 `gongtong\`）；8 提交历史导成同目录`pcl-shiguang-历史-8提交.bundle`（12 MB，未推送），两个更早的中间 bundle 已清掉（含解锁历史）。

**续工锚点（2026-10-31 08:10 · 审核通过，进入维护期 · 已被下一条取代，留作背景）**

代码基线 **`2026.10.30-custom.16`**（5,938,688 字节，`sha256` `0fcfb49e…8970`）。**功能开发到此收口。**

**三件事同日落地**：
1. **Mojang AppID 审核通过** —— 微软正版登录的最后一关过了。此前 `login_with_xbox` 报
   `403 Invalid app registration`（PCL 误译成「当前 IP 异常」），根因是新应用需人工进白名单，
   `aka.ms/mce-reviewappid` 申请、每周一轮。**现在不需要改任何配置，启动器里那个 Client ID 直接可登。**
2. **GitHub 仓库公开**：<https://github.com/4huang0yan4/pcl-shiguangxiugaiban>（Public；该仓库后已删除重建，见上一条锚点）。
   为彻底清除历史里的旧解锁快照，走的是**删库重建**（force push 之后旧对象仍可按 SHA 取到，
   重建才真正归零；已验证 `eed7f19` 返回 `No commit found`）。
3. **计划表转维护期**：`12` 顶部改为「初步开发完成，进入维护期」，只保留四类事
   （上游跟进 / 故障修复 / 外部变更 / 仓库维护），不再排新功能。

**维护期最容易犯的错（已经犯过一次）**：往仓库同步代码时**不能按文件整拷**——
`PageSetupUI.xaml`、`ModSecret.vb`、`ModCustom.vb`、`FormMain.xaml.vb` 这 4 个必须保持"仓库版"形态，
整拷会把主题解锁带回去，且会进 git 历史。规则与自检命令见 `16-dual-track.md` 第 4.0 节。

**明确不做**：新功能 · 自研协议 · P2P 增强 · 跨平台 · 下载暂停（`NetState` 无 `Paused`，
要动多线程分段状态机，出错表现是文件损坏或卡死不报错，风险高于收益）· T-06 联机长测 · 今日人品 ·
Cleanroom/LegacyFabric/Quilt/LabyMod · 皮肤游戏内同步（改用 CustomSkinLoader 类模组）· R6 抽象 · XPCL。

**明确不做**：T-06 联机长测 · 今日人品 · **下载暂停**（`NetState` 无 `Paused`，要动多线程分段状态机，
出错表现是文件损坏或卡死不报错，风险高于收益）· Cleanroom/LegacyFabric/Quilt/LabyMod ·
皮肤游戏内同步（改用 CustomSkinLoader 类模组）· R6 抽象 · 自研 Scaffolding · P2P 增强 · XPCL。

**明确不做**：T-06 联机长测 · 今日人品 · **下载暂停**（`NetState` 无 `Paused`，要动多线程分段状态机，
出错表现是文件损坏或卡死不报错，风险高于收益）· Cleanroom/LegacyFabric/Quilt/LabyMod ·
皮肤游戏内同步（改用 CustomSkinLoader 类模组）· R6 抽象 · 自研 Scaffolding · P2P 增强 · XPCL。

**历史续工锚点**（2026-10-30 15:10 及更早）已移到 `08c-anchors.md`，只读归档。
