# 服务端功能的参考实现调研与优化建议

> 2026-11-02 起，服务器相关改动**先看参考实现再动手**（用户 14:11 决定）。
> 本文只做调研与方案，不改代码；方案确认后再排期。

## 一、结论先说

1. **只有 HMCL 真正把「服务端」当成一类一等公民**：它不是「下载一个 jar 然后自己管」，而是把服务端当作
   **独立实例**（有自己的版本、加载器、Java、内存），安装/补全/更新走与客户端同一套任务链。
2. **Prism Launcher 没有服务端功能**（只管客户端实例）。OpenLauncher 同样没有。所以「服务端管理」这块
   没有第二家可以抄，HMCL 是唯一有参考价值的对象。
3. 我们现在的实现是「**目录 + jar**」双轨：先记 `LinkServerJar`，又补 `LinkServerDir`，再靠
   `ServerDir()` 三级回退去猜。本轮 6 个 bug 里有 4 个（白名单要先开服、开服跑到 installer、
   配置写错目录、删玩家不同步）根因都是这个**没有实例概念的目录模型**。这才是该优先重构的地方，
   而不是继续加按钮。

## 二、参考对象实际怎么做

### HMCL（GPLv3，Java，唯一有服务端设计的开源启动器）

- 新建版本时可选择**服务端**类型，得到一个「服务端实例」；配置项（端口 / 人数 / 白名单 / 正版验证 /
  难度 / 模式）在实例设置里，而不是散落在联机页。
- 服务端整合包有独立格式 **`server-manifest.json`**（`ServerModpackProvider` 读它），
  安装用 `ServerModpackLocalInstallTask`，之后还有 `ServerModpackCompletionTask`（补全）
  与 `ModpackUpdateTask`（增量更新）。
  → 也就是说：**mods / config / 脚本随整合包分发，且能一键更新**，玩家不用手动拷 mod。
- 官方帮助里明确：服务端管理员可以用静态 HTTP 服务提供 `server-manifest.json` + `overrides/`，
  客户端/服务端据此检查更新。这是「服主-玩家」分发链，不是本机管理。
- 服务端与客户端共用同一套「版本 + 加载器 + 依赖补全」实现，所以**不存在「服务端目录缺库」这种状态**
  —— 缺什么就补什么，和客户端一样。

### Prism Launcher / OpenLauncher

- 两者都**没有**服务端功能。Prism 的定位是「隔离的客户端实例 + 整合包」，服务端要用户自己开。
- 结论：这块没有更成熟的 UI 范式可抄，HMCL 就是天花板。

### 美西螈（Axolotl）

- 有服务器页与一键开服，卖点是「开服 + 联机」打包（联机走它自己的中转服务，协议不公开）。
- 服务端侧实现不公开可查，之前已确认**不能作为实现参考**，只能作为功能清单参考。

## 三、我们现在的差距（按根因排，不按功能排）

| # | 差距 | 现状 | 参考实现的做法 |
|---|---|---|---|
| A | **没有「服务端实例」概念** | `LinkServerJar` + `LinkServerDir` + `LinkServerProcDir` 三个设置项互相猜 | HMCL：服务端就是一个实例，目录即实例，属性在实例里 |
| B | **安装不是一件事** | 原版一个任务、installer 一个任务、安装步骤完全在任务栏之外（看不到进度） | HMCL：安装是一条任务链，进度条走完才叫装完 |
| C | **服务端 mod 只能手工拷** | 「添加 mod」复制 jar 到 mods | HMCL：`server-manifest.json` + overrides，可校验、可更新 |
| D | **版本匹配靠事后发现** | 连不上才看到 `Internal Exception` | 本分支已加前置检测（版本 + 加载器提示），HMCL 无对应能力 |
| E | **世界没有备份** | 无 | 无开源参考（HMCL 也没有）；Axolotl 有自动备份 |
| F | **端口可达性不自检** | 用户自己撞 | 无参考，需自己做 |

## 四、建议的优化顺序

### 第 1 步：安装合并成「一条任务」（用户已提，成本中）—— ✅ 2026-11-02 已完成

现在：`EnsureServerJar` 一个任务栏条目、installer 一个条目、安装过程只有一行状态文字，
用户中途会以为「下载好了」。

目标：一个任务栏条目「安装 Forge 服务端 1.20.1」，内部三步，权重按预计耗时：

```
LoaderCombo("安装 Forge 服务端 1.20.1", {
    LoaderDownload(原版 server.jar)        ProgressWeight ≈ 60
    LoaderDownload(forge-installer.jar)    ProgressWeight ≈ 5
    LoaderTask(静默安装 + 校验 run.bat)    ProgressWeight ≈ 120   ← 安装也算进度
})
```

- 实现：`InstallLoaderServer(Title, McVer, Dir, InstallerUrl, InstallerPath, RunInstaller, SuccessCheck)`，
  Forge / NeoForge / Fabric 三条路统一走它；原版 URL 先解析（网络请求），下载放进任务里。
- 安装那步用现成的 `LoaderTask(Of String, String)` 包，不新写 Loader 基类。
- 目录里已有 `server.jar` 时（重装加载器）自动跳过下载原版那一步，权重自然只剩安装。
- 好处：只有「跑完」才算下载完成，installer 联网拉依赖那段也在进度里。

### 第 2 步：把服务端升成「实例」（成本高，但能一次性铲掉 A 类 bug）

- 服务端目录 + 一个 `server.json`（版本 / 加载器 / 加载器版本 / Java / 内存 / 端口），
  联机页只认「当前服务端实例」，不再有 jar/dir 双轨与三级回退。
- **已落地第一块（2026-11-02，`custom.8`）**：`WriteServerMeta` / `ReadServerMeta`，
  安装成功时写 `<目录>\server.json`（`mc` / `loader` / `loaderVer` / `updated`），
  联机页的版本与加载器判断**优先读它**，读不到才退回「已装记录」字符串。
  好处立刻可见：换机、别人拷来的目录也能自报版本，不用靠记录猜。
- **尚未做**：Java 路径、内存、端口写进 server.json 并由它驱动启动；撤掉
  `LinkServerJar`/`LinkServerDir`/`LinkServerProcDir` 三轨。等第 1 步实机验过再做，
  否则出问题分不清是安装层还是实例层。

### 第 3 步（可选）：`server-manifest.json` 服务端整合包

- 只对「要分发给别人」的服有用。自己是单人开服的话价值低。
- 若要做，格式与 HMCL 兼容（`server-manifest.json` + `overrides/`），可互导。

### 追加（2026-11-02）：镜像自建安装 —— 不用官方 maven 也能装 Forge / NeoForge

原定「安装器网络重方案」成本最高，但实测发现**比想象中简单，已实现并验证**。

**为什么必须做**：`maven.neoforged.net` / `maven.minecraftforge.net` 在本机都不可达
（curl 返回 000），而安装器内部写死了这两个域的**绝对 URL**；`--mirror` 参数只影响安装器
自身构件解析，**不改写 version json 里的库地址**（带/不带尾斜杠都试过，无效）。

**安装器内部结构**（以 NeoForge 26.3.0.101 为例，`unzip -l` + 解 json 得到）：
- `install_profile.json`：`libraries`（30 个，每个带 `downloads.artifact.path`）+ `processors`（2 个）
  + `data`（`BINPATCH=/data/client.lzma`、`PATCHED=[net.neoforged:minecraft-server-patched:<ver>]`）
- `version.json`：`libraries`（27 个）+ `arguments.jvm`
- `data/`：`run.bat` `run.sh` `win_args.txt` `unix_args.txt` `user_jvm_args.txt` **`client.lzma`（补丁包本身）**
- 两个 processor：① `EXTRACT_FILES`（把 data/* 搬到目标位置，纯解压）② `PROCESS_MINECRAFT_JAR`
  （用安装器自带的 `installertools` 做 binpatch + 从 server.jar 抽 libraries，**纯本地**）

**实现**（`PageServerInstall`）：
1. `MirrorMavenUrl`：`maven.neoforged.net/releases/`、`maven.minecraftforge.net/`、
   `libraries.minecraft.net/` → `https://bmclapi2.bangbang93.com/maven/`
2. `ExtractZipFolder` 解 `data/`；`ReadZipJsonText` 读两个 json
3. 下库：`downloads.artifact.path` 拼本地路径、URL 走镜像；已存在就跳过
4. `ExtractByArgs` 按 processor 的 `--from/--to` 成对搬文件（`user_jvm_args.txt` 已存在则不覆盖）
5. `RunProcessor`：`java -jar libraries/<坐标路径> <替换过占位符的参数>`；
   `ResolvePlaceholder` 处理 `{ROOT}` `{INSTALLER}` `{MINECRAFT_JAR}` `{LIBRARY_DIR}` `{BINPATCH}`
   以及 `[group:artifact:version]` 形式的坐标
6. 入口校验：没有 `run.bat` 就判失败

**验证结果（手工跑通，未走启动器 UI）**：NeoForge 26.3.0.101 + MC 26.3，
`libraries` 共 79 MB，启动输出 `Done (3.031s)! For help, type "help"` —— 全程没有访问官方 maven。

**两个坑**：
- `bmclapi` 对 `neoforge-<ver>-universal.jar` 会 302 跳 CDN，**不带 User-Agent 会 403**；
  PCL 的下载器自带 UA，所以启动器内没问题。
- 老版本 Forge 的 processors 可能不止这两个（含 MERGE_MAPPINGS / MCP 数据），
  所以代码里保留**回退官方安装器**：镜像路径抛异常就自动改用安装器（网络通/开代理时仍可用）。

### 第 4 步（可选）：世界备份 / 端口自检

- 备份：启动前把 `world/` 打包到 `backups/<时间>.zip`，保留最近 N 份。无开源参考，需自研。
- 端口自检：开服后本地探测端口 + 提示「外网要放行 25565/TCP 或做端口映射」。

## 五、需要用户拍板的点

1. ~~第 1 步是否现在做~~ → 已做（`custom.7`~`.8`）。
2. 第 2 步：`server.json` 已落盘并用于版本判断；剩下的「实例化」（Java / 内存 / 端口进实例、
   拆掉三轨）建议第 1 步实机验过后再做。
3. 第 3、4 步是否列入待办（不排期）。

## 相关

`08-todo-risks.md`（现状与坑）、`06-multiplayer.md`（陶瓦联机）、`03-references.md`（参考实现索引）
