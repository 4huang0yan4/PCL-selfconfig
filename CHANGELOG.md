# 变更记录

> 源码与变更记录在本仓库；发布版（Release）附**发布版 exe**（不含任何官方服务凭据）与 `LICENCE`、声明、使用说明，GitHub 同时自动附源码 zip。更新由智能体按 `AGENTS.md` 第八节的步骤执行。
> **本地构建号**形如 `<日期>-custom.<当日序号>`，与 `Plain Craft Launcher 2.exe` 启动日志里的版本行一致。
> **对外发布号**为 `PCL-selfconfig-custom.<N>`，从 1 起逐次 +1，与本地构建号解耦——本地一天可以出很多版，对外只按发布次数计。

## 版本索引

一张表看清每次发布改了什么。**tag 名 = 版本号**（自 `2026.11.01-custom.22` 起；更早的三个是纯序号，保留不改）。

| tag | 日期 | 一句话内容 | 包含版本 |
|---|---|---|---|
| `PCL-selfconfig-custom.3` | 2026-11-03 | 联机页拆三卡（加入/本机/远程）、状态徽标与关服自检、局域网/跨网二选一、接管条；新增《服务器使用方法》与重写《联机使用方法》两篇指南，帮助页常驻入口；修复换帮助包不生效、检查更新报未找到设置项、更新下载无进度与替换失败 | 2026.11.03-custom.9 |
| `PCL-selfconfig-custom.2` | 2026-11-03 | 自动更新可用：查本仓库 Release、比对构建号、下载 zip 替换 exe 重启；发布版默认开启 | 2026.11.03-custom.4 |
| `PCL-selfconfig-custom.1` | 2026-11-03 | 首个公开版：改名 PCL 自配版、固定龙猫蓝、删主题预设、README 重写、服务端/联机/导出等功能 | 2026.11.03-custom.1 ~ custom.3 |
| `2026.11.02-custom.31` | 2026-11-02 | 服务端安装合成一条任务、镜像自建装 Forge/NeoForge、Java 自动下载、T-01~T-11 实机验收 | custom.1 ~ custom.31 |
| `2026.11.01-custom.22` | 2026-11-01 | 服务端进程托管、RCON 自动接管、RCON 密码默认用完即清 | custom.17 ~ custom.22 |
| `3` | 2026-11-01 | 服务端管理员勾选、实时控制台、远程 RCON、美西螈导入、外置登录入口 | custom.41 ~ custom.16 |
| `2` | 2026-10-31 | 服务端下载与联机页服务端管理（分组列表、加载器安装、白名单、已装服务端） | custom.11 ~ custom.40 |
| `1` | 2026-10-31 | 新增本分支自动更新模块（默认关闭） | custom.1 ~ custom.10 |

## 2026.11.03-custom.9（联机页重做 + 开服/联机指南）

**联机页拆成三张卡**

- `CardServerJoin` / `CardServerLocal` / `CardServerRemote`：加入服务器、本机开服、远程服务器各自独立；`ComboCardOrder` 提供六种上下排列，默认 加入 → 本机 → 远程。
- 每张卡标题旁加状态徽标：灰=未运行、绿=运行中、黄=启动中/关闭中、红=出错。细节行（进程名、RCON 状态）与徽标文本重复时自动收起，避免「未运行」念两遍。
- 移除旧版联机页两处折叠控件（远程、跨网），地址与选项一律展开。

**本机开服**

- `CheckNetLan` / `CheckNetCross` 二选一：局域网模式列出本机内网地址并支持复制；跨网模式展开本地地址、对外地址（穿透隧道给的公网地址）。
- 跨网可选「开服前自动拉起联机工具」与「与联机方案共用同一支工具」（`LinkServerNetToolSync`，关掉后 `LinkServerNetToolPath` 独立生效）。`ModCustom` 的 `LinkTool*` 全部改为 `…At(Path)` 重载，原属性/方法委托过去，判定与启动流程只留一份实现。
- 「关闭服务器」：RCON `stop` → 等 15 秒 → 强杀；状态走「关闭中」再变「未运行」。进程退出监听前置 + `Try/Finally` + 2 秒 `DispatcherTimer` 自检三层兜底，避免事件丢失后卡在「关闭中」。
- 启动器重启后的接管条（`PanTakeover`）：重新接管 / 关闭后台服务端；`StopLeftoverServer()` 与正常关服复用同一条路径。
- 远程 RCON 与本机接管拆成两条通道（`Rcon` / `RconLocal`）：远程地址密码只归远程卡，本机接管直读本机 `server.properties` 连 `127.0.0.1`；老版本误写进远程地址的 `127.0.0.1` 由 `LinkRconHostMigrated` 一次性清掉。

**帮助**

- 新增《服务器使用方法》（14 张卡）：服务端三种来源、本机卡每个按钮、`server.properties` 四项、游戏规则、白名单与 OP、控制台、内存与 Java、端口与防火墙、备份回档、关服接管、报错对照、模组服与插件服。
- 重写《联机使用方法》：按界面真实顺序（陶瓦联机 → 联机方案 → 加入服务器 → 本机 → 远程）逐块讲，含樱花穿透从零开始、官方密钥申请、不用工具的几种连法、连不上排查十条。
- 「更多 → 帮助」页面顶部新增常驻卡片「常用指南（本版补写）」，两个按钮直达上述两篇；指南分类里这两篇排在最前；「帮助」行右侧另有一个图标入口（悬停显示）。

**帮助更新机制（两个隐藏 bug）**

- `HelpTryExtract` 原来只按 `VersionCode` 判断是否重解压，而它是常量；改为比对缓存 `Help.zip` 的大小 + 最后写入时间，并把 `ExtractResources` 提到判断之外——内容一变必定重解压。
- `Resources.resx` 以 `ResXFileRef` 引用 `Help.zip`，MSBuild 不跟踪被引用文件，换包后 `PCL.Resources.resources` 不重建，新帮助根本进不了 exe。`tools/help-pack.py` 打包后自动刷新 resx 时间戳。
- `PageOtherHelp.EnterHelpPage` 支持相对路径（`指南/xxx.json`），并可从已加载条目按路径结尾反查；找不到时左下角提示，不再抛 Invoke 异常。

**产物**

- 本地构建号 `2026.11.03-custom.9`，exe sha256 `fda1206ac4f2bc671e8a430edc79df3a0bc3080806e808ec475f1c594cecede8`。
- `PCL-selfconfig-custom.3.zip`，2,774,249 B，sha256 `e31bd21017e7e56e12f02fc7226830abaa0ba475aa81f035deb758e14295e1d5`。
- 发布版不含 `ThemeApplyPreset` / `EnsureCustomTheme`，配色仍固定龙猫蓝；`SystemSelfUpdate` 保持默认开启。

## 2026.11.03-custom.4（自动更新可用）

`PCL-selfconfig-custom.1` 的自动更新模块是照旧仓库写的（`pcl-shiguangxiugaiban`、只认 `.exe` 资产、版本号算 `2026.10.31.7` 这种四段式），换成 `PCL-selfconfig` 的 zip 发布后完全不通。本版重写。

**换仓库与 Version 判断**

- 仓库改为 `4huang0yan4/PCL-selfconfig`，仍走 `gh-proxy` / `ghfast` 代理兜底。
- 版本比较不再猜 tag：读 Release 正文里的「本地构建号：`2026.11.03-custom.N`」，与 exe 自身的构建号比较；正文没有该行时退回只按 `Settings("SelfUpdateTag")` 判重，避免发布号（`custom.1`）与构建号（`2026.11.03-custom.4`）两套编号互相误判。

**资产与环境**

- 资产接受 `.zip` 与 `.exe`：zip 用 `ZipFile.OpenRead` 只抽 `Plain Craft Launcher 2.exe` 与 `Plain Craft Launcher 2.exe.config`，忽略说明文档，避免解压出无关文件。
- 更新脚本 `apply.cmd` 改为「拷 exe（有 config 才拷）→ start → del」，与 Release 附 zip 的形态一致。
- 弹窗写明发布号、构建号与正文摘要；同一 Release 只提示一次（`Settings("SelfUpdateTag")`）。

**开关**

- 发布版「设置 → 更新 → 检查本分支（GitHub）更新并提示下载」**默认开启**（`Settings.vb:220`）。
- 自用版保持关闭：自用版的配色解锁与发布版不同，被自动更新覆盖会丢失主题配色。

**产物**

- `2026.11.03-custom.4`，exe sha256 `07f916849f26b208c6e04e97516d0c25469dc16d450253ab37dd06dae1458be4`。
- `PCL-selfconfig-custom.2.zip`，2,737,070 B，sha256 `2e3163d08caa1d9b5c3cc133fa77f2fe22ebc04daec0c4943e79d0049a60d702`。
## 2026.11.03-custom.1 ~ custom.3（改名、合规收口与发布准备）

**改名**

- 界面内外统一为 **PCL 自配版**（24 处）：窗口标题、主界面 Logo、关于页、实例导出作者、README / 声明 / 帮助源文本。
- 仓库名改为 `PCL-selfconfig`；旧仓库与旧历史整体弃用，`git init` 重建为单提交。

**合规收口**

- 删除上游主题预设实现 `ThemeApplyPreset`（15 主题 HSL 表）及其在 `ModSecret` 中的调用，两棵树（`build` 与 `custom`）同步删除。
- `声明.md` 身份表与第 5 条改为与代码一致：不提供与赞助解锁相当的外观能力，相关入口保持禁用态。
- 产物验证：`ThemeApplyPreset` / `EnsureCustomTheme` 命中 0；「PCL 自配版」2 处。

**主题固定龙猫蓝（custom.3）**

- 上游主题预设实现 `ThemeApplyPreset` 已移除、自定义配色滑块全部收起、隐藏主题与「自定义」禁用；
  本版再把四个**点了也不变色**的基础主题按钮（甜柠青 / 小草绿 / 菠萝黄 / 橡木棕）一并禁用，只留龙猫蓝。
- 原因：`ColorHue` 在发布版写死 210、全树无赋值，`ThemeRefresh` 只按固定色相重算，切换不会变色。

**隐私**

- `launcher-dev/09b-vm-testlog.md` 的内网与公网 IP 脱敏为 `192.168.x.*` / `203.0.113.10`；旧 `.git` 不再使用（备份留在本机，未推送）。

**文档**

- `README.md` 重写：是什么 → 功能总览（启动 / 下载 / 服务端 / 联机 / 设置）→ 怎么用（安装、登录、下载、开服、联机、导出、卸载）→ 需要自备的凭据 → 已知限制 → 许可。
- `使用说明.md` 新增「四、开服（本机服务器）」：下载安装、开服关服、配置、玩家名单与管理员、白名单、控制台与 RCON、已装服务端；后续章节顺延。
- 发布版首次提供 exe（Release 附件），随包附 `LICENCE`、`声明.md`、`使用说明.md`、`THIRD-PARTY-NOTICES.md`。

## 2026.11.02-custom.1 ~ custom.31（服务端安装重构与实机验收）

**服务端安装：一条任务跑完**

- 原版 `server.jar`、加载器安装器下载、静默安装三步合成**一个任务栏条目**「安装 Forge 服务端 x.y」，权重按预计耗时分配（原版 60 / 安装器 5 / 安装 120）；安装也算进度，不再出现「下载完但没装完」的错觉。
- Forge / NeoForge / Fabric 统一走 `InstallLoaderServer`；目录里已有 `server.jar` 时自动跳过下载原版那一步。

**镜像自建安装（不用官方 maven 也能装 Forge / NeoForge）**

- `maven.neoforged.net` / `maven.minecraftforge.net` 在本机不可达（curl 返回 000），而安装器内部写死了这两个域的绝对 URL，`--mirror` 只影响安装器自身构件、不改写 version json 里的库地址。
- 改为自己按安装器内容安装：解析 `install_profile.json` + `version.json`，从 `bmclapi` 镜像下 30 个库、解压 `data/`、按 processor 的 `--from/--to` 成对搬文件、`java -jar` 跑 `EXTRACT_FILES` 与 `PROCESS_MINECRAFT_JAR` 两个 processor。
- 2026-11-02 15:35 NeoForge 26.3.0.101 走 **启动器 UI 全流程**装通（`libraries` 79 MB，开服输出 `Done (3.031s)!`），全程没有访问官方 maven；镜像路径抛异常自动回退官方安装器。
- 修两个 bug：processor 误用全量 classpath 导致 `NoSuchMethodError: AtParser.<init>`；参数含空格未加引号。

**服务端元数据与配置**

- 安装成功写 `<目录>\server.json`（`mc` / `loader` / `loaderVer` / `updated`），联机页判断优先读它，换机或别人拷来的目录也能自报版本。
- 配置项（端口 / 人数 / 白名单 / 正版验证 / MOTD / 难度 / 模式 / 视距 / PvP）**改动即存**，不用再点保存。
- 删除服务端走回收站；删除玩家连带撤销管理员（运行中走 `deop`，未运行写 `ops.json`）。

**Java 与账号**

- 缺 Java 自动下载（`custom.30`）；Java 列表「清理」只删 `runtime` 下的；「加入时用」跟随启动页。
- 外置登录入口（第三方）、自定义 UUID 实测进 `--uuid`、美西螈「重新转换（覆盖）」（先确认源 json 存在，不会删了旧副本却装不上新的）。

**实机验收**

- T-01 ~ T-09、T-11 全部通过，T-10（Java 版本偏低弹窗）跳过；B 组（第二台机器）待跑。明细见 `launcher-dev/19-tests-2026-11-02.md`。

## 2026.11.01-custom.17 ~ custom.22（服务端进程与 RCON 控制）

**关闭启动器时服务端怎么办**

- 关启动器时弹窗问「让它继续跑」（玩家还能进，但启动器接管不了它的控制台）或「一起关掉」（发 `stop` 等它存盘退出）两个选择。
- 启动服务端时记住 PID / 目录 / 启动时间；重开启动器按 PID + **进程名校验**（java / cmd）认出遗留进程，避免 PID 被复用时误判。
- 新增设置「关闭启动器时一并关掉本机服务端」，**默认不勾**（= 保留）；静默退出（托盘 / 快捷键）按这个设置走，不再弹窗。

**用 RCON 接管遗留服务端**

- 「开本地服务端」时自动补齐 `enable-rcon=true` / `rcon.port` / 随机 `rcon.password`，已有非空密码则沿用。
- 重开启动器认出遗留进程后**自动连接 RCON**，命令框立刻可用；「关闭服务器」此时走 RCON 优雅 `stop`，连不上才退化为强制结束。
- 命令通道：本机进程活着优先走 stdin（有实时输出），否则走 RCON。

**RCON 密码策略**

- **默认一次性输入、用完即清**：连接成功后清空密码框，不写入磁盘；密码的权威来源始终是服务端自己的 `server.properties`。
- 「记住密码」为开关且**默认关闭**，勾选时弹风险确认（RCON 密码等同管理员凭据，勾了等于在本机留一份），取消则回滚勾选；「显示密码」仅在已记住时可用。
- 密码回退顺序：手输 → 已记住 → **仅当目标是本机时**从 `server.properties` 现读（远程服不再误用本机配置里的密码）。
- 「重新生成」生成新随机密码并写进 `server.properties`，同时把新密码显示出来 —— 换别的启动器或命令行开服用的也是它。

## 2026.10.31-custom.41 ~ 2026.11.01-custom.16（服务端管理与版本导入）

**服务端（联机页「服务器」）**

- **玩家名单改为勾选列表**：勾上即管理员，取消勾选变回普通玩家。服务端在运行时走控制台 `op` / `deop` 立即生效；未运行时写 `ops.json`（权限等级 4），下次开服生效。
- **配置补游戏规则**：难度 / 游戏模式 / 视距 / 允许 PvP。视距留空表示不改动该项。
- **实时控制台**：显示服务端输出、可发任意命令（回车发送，自动去掉前导 `/`）。JVM 与 stdin 统一 UTF-8，中文命令不再乱码。
- **远程服务器（RCON）**：可控制云服务器 / 别人机器上的服务端。需要对方在 `server.properties` 开 `enable-rcon=true` 并设 `rcon.password`。连上后命令框自动走远程；看不到实时日志，只有命令回显。
- **添加已有**：把本机别处装好的服务端目录（自己搭的、拷来的、别的启动器装的）登记进「已装服务端」列表。
- **Forge / NeoForge 安装后按物理内存写 `user_jvm_args.txt`**（物理内存 1/4，夹 2G–8G，512M 取整），并补上 UTF-8 编码参数；已手动设过 `-Xmx` 则不动。
- **安装器失败不再谎报「安装完成」**：给出退出码、日志摘要，并提示开加速器重试（安装器要直连官方仓库）或改用走镜像的原版 / Paper。
- **目录冲突检测**：保存目录里已有服务端文件时先确认，避免不同加载器混在一起互相覆盖。
- **打开日志**：直接打开该服务端的 `logs\latest.log`（没有就取最新日志并选中）。

**下载**

- 新增**插件**下载页（Bukkit / Spigot / Paper 生态）。
- 服务端新增 **Purpur**（Paper 增强版）与 **Folia**（多线程分支）。

**其他启动器**

- **扫描其他启动器**可识别**美西螈（Axolotl）**的数据目录（版本在 `meta\versions`、实例在 `profiles`，目录名不固定），自动转换为 PCL 能读的结构：复制版本 json / jar / 游戏数据，`libraries` 与 `assets` 只建目录联接不占额外空间。
- 已转换的实例可**重新转换**（覆盖，旧副本进回收站）；`mods` 数量对不上时扫描结果会提示。

**登录**

- 登录页新增**「第三方」**入口（外置登录 / Authlib-Injector），默认填好 LittleSkin，可改服务器地址。
- 离线登录支持**自定义 UUID**，并提供「按名字生成」标准离线 UUID（`MD5("OfflinePlayer:" + 名字)` 的 v3 变体）。

**实例**

- 版本概览新增**占用空间**统计与**迁移数据**到其他版本（存档 / 加设置 / 全部三档，只复制不删除）。

## 2026.10.31-custom.11 ~ custom.40（服务端与联机迭代）

- **下载页新增「服务器」分区**：版本分组列表（最新 / 正式 / 预览）、点版本进安装页、原版服务端下载（进任务管理）。
- **加载器服务端**：Forge / NeoForge / Fabric 版本列表；Forge/Fabric 安装器下载（进任务栏）+ 安装前自动下原版 `server.jar`；安装完成记录到联机页。
- **联机页「服务器」**：加入时可选离线 / 正版登录（默认离线）；服务端配置（端口 / 人数 / 白名单 / 正版验证 / MOTD）；白名单编辑（批量、勾选删除）；mods 目录管理；「已装服务端」下拉 + 用这个开服 / 打开目录 / 删除。
- **设置页**：本分支自动更新模块（默认关，不发布 Release 资产）。
- **许可**：仓库 `LICENCE` 统一为 3,168 字节 LF。

## 2026.10.31-custom.10

- 联机页「服务器」：
  - 新增「加入时用」下拉：**离线登录（默认）** / 微软正版登录；加入时切换 `LoginType`。
  - 首次开服若没有 `server.properties`，按**离线优先**创建（`online-mode=false`）。

## 2026.10.31-custom.9

- 联机页「服务器」：
  - **白名单编辑**：多行文本，每行一个玩家名；保存写 `whitelist.json`（离线 UUID = MD5("OfflinePlayer:" + 名字)，版本 3），并自动把 `white-list` 置为 true；在线模式需开服后用 `whitelist add`。
  - **mod 管理**：「打开 mods 目录」「添加 mod」（选 jar 复制到 `mods\`，重名覆盖）；服务端是 Fabric/Forge 时才加载。

## 2026.10.31-custom.8

- 联机页「服务器」：
  - **服务端配置编辑**：端口 / 最大人数 / 白名单 / 正版验证 / MOTD，保存写入 `server.properties`（其余键保持不变）；
  - **关闭服务器**：向服务端 stdin 发送 `stop`，15 秒未退出强制结束；
  - 启动改为重定向 stdin/stdout，日志写 `server-run.log`，解析 `Starting minecraft server version` 与 `Done`；
  - **加入前版本检测**：服务端版本与客户端 `VanillaName` 不一致时提示先安装对应版本。
- 新增 [`手动更新.md`](手动更新.md)（不用智能体的更新步骤），README 加入口。

## 2026.10.31-custom.7

- 新增 `Modules/UpdateSelf.vb`：检查本仓库 Release 的更新模块，**默认关闭**（设置 → 其他 →「更新」）。
- 说明：本仓库不建 GitHub Release（只发源码），该模块目前只会提示「已是最新」；更新请走 `AGENTS.md` 第八节。

## 2026.10.31-custom.6

- 联机页「服务器」卡片增加**一键开本地服务端**：选 `server.jar` → 自动写 `eula.txt` → 用 PCL 的 Java 启动 → 探测端口，就绪后回填 `127.0.0.1:端口`。

## 2026.10.31-custom.5

- 联机页新增第三块**「服务器」卡片**：填 `host` 或 `host:port` 直接进服（`McLaunchOptions.ServerIp`）；勾选「启动游戏时默认进入」后，从主页启动也会自动连该服务器。
- `ModLaunch` 取值顺序：本次指定 → 勾选的默认直连 → 版本自带 `VersionServerEnter`。

## 2026.10.31-custom.4

- 设置 → 其他新增**「卸载与清理」**：删除 `PCL\`、主程序、运行库与注册表 `HKCU\Software\PCLDebug`，两次确认；不删 `.minecraft` 与用户文件。

## 2026.10.31-custom.3

- 凭据输入框：`PreviewMouseLeftButtonDown` 直接 `Focus + SelectAll + Handled`，从源头断掉拖拽（点击=全选，`Ctrl+V` 替换；不能点选/拖选）。

## 2026.10.31-custom.2

- 凭据输入框：切窗口/切页复位遮罩；拦 `WM_SYSCOMMAND / SC_MINIMIZE`，最小化前先收起遮挡（任务栏缩略图不再显示明文）；`PreviewMouseMove` 拦拖拽。

## 2026.10.31-custom.1

- 凭据输入框：主窗口 `Deactivated` 与页面 `IsVisibleChanged` 时收起遮挡（此前只靠 MouseUp/MouseLeave）。

## 2026.10.30-custom.16（上一个发布基线）

- 功能面收口：R2 四端导出、R4 `.mrpack` 导入、R7 与 PCL CE 互通（局域网）、R8 下载链路、百宝箱、5 篇自定义帮助、微软正版登录。