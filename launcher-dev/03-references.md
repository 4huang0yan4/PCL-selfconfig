# 03 参考项目清单

状态：混合（逐条标注）
最后更新：2026-10-30

> 用法：不要通读参考项目源码。先按本表定位"要看哪个文件的哪一段"，再定向阅读。
> 仓库地址与许可均由对话信息整理，**未逐条联网核实**，动手前先验。

---

## 一、联机

### Terracotta（陶瓦联机）

| 项 | 内容 |
|---|---|
| 定位 | 面向 MC 的联机后端，基于 EasyTier，已内置进 HMCL，社区事实标准 |
| 仓库 | **`burningtnt/Terracotta`（主仓，232 星，活跃）**；`zkitefly/Terracotta` 是其 fork（1 星） |
| 许可 | **AGPL-3.0-or-later + 例外条款**（原文已核对：① 未经修改的二进制打包且不静态/动态链接；② 经 HTTP API 交互且在界面显著处标注版权——满足任一即不传染） |
| 版本 | v0.4.2（2026-02-09）。v0.4.2 起移除对 TerracottaLegacy 与 PCL2CE 旧房间格式的支持 → **双方版本必须一致** |
| 要看什么 | 已全部查清并落进 `06-multiplayer.md`：启动参数、API 全表、状态机、房间码规则、10 条坑 |
| 对项目的价值 | R3 的首选路径：打包官方发行二进制 + 通过 API 调用，工作量最小、合规成本最低 |
| 集成方式 | 见 `06-multiplayer.md`（已实测：18 项契约 + 建房路径） |

### EasyTier

| 项 | 内容 |
|---|---|
| 定位 | 通用组网层（非 MC 专用），Terracotta 与多个启动器的联机都建在它之上 |
| 仓库 | `EasyTier/EasyTier` `[待验证]` |
| 许可 | 需核实 `[待验证]` |
| 要看什么 | 节点发现、中继、P2P 打洞的配置项；CLI 与配置文件格式 |
| 对项目的价值 | 方案 B 的基础：Terracotta 不满足需求时，直接下沉到组网层自定义 |

### Scaffolding-MC 协议

**2026-10-30 已核实（V-08 关闭）**：规范与两套实现都存在，且本分支在用的陶瓦就是它的联机中心。

| 项 | 内容 |
|---|---|
| 定位 | 跨启动器联机客户端的**数据交换协议**，建立在 EasyTier 之上；由陶瓦作者 Burning_TNT 主导 |
| 规范仓库 | `Scaffolding-MC/Scaffolding-MC`（MIT，许可写在 README 内嵌块；无独立 LICENSE 文件，故 GitHub 侧 license 字段为 null） |
| 规范内容 | 房间码 `U/NNNN-NNNN-SSSS-SSSS`（按 0-9/A-H/J-N/P-Z 映射后小端整除 7 校验）；EasyTier 网络名 `scaffolding-mc-NNNN-NNNN` + 密钥 `SSSS-SSSS`；联机中心发现 = EasyTier Hostname `scaffolding-mc-server-{port}`；TCP 二进制请求/响应 + 标准协议 `c:ping` / `c:protocols` / `c:server_port` / `c:player_ping` / `c:player_profiles_list`；扩展协议用自有 namespace |
| 规范成熟度 | **无版本号、无 release**；仓库 2025-10-23 建、最后提交 2025-11-08（约 11 个月未动），6 个提交、4 个 open issue、23 星；有 SEP 提案机制（SEP 0 / SEP 1）但未编号发布 |
| 实现 1：陶瓦 | `burningtnt/Terracotta` 源码含 `src/scaffolding/{client,server,profile}.rs` 与 `src/controller/rooms/scaffolding/`；本机 `terracotta-0.4.2-windows-x86_64.exe` 内可直接 grep 到 `scaffolding-mc-server` → **陶瓦 0.4.2 即 Scaffolding 联机中心** |
| 实现 2：PCL CE | `PCL-Community/PCL-CE` 的 `PCL.Core/Link/Scaffolding/{Client,Server,EasyTier,LobbyCodeGenerator.cs}`，同级还有 `PCL.Core/Link/EasyTier/` |
| 结论 | R7 不是"将来实现协议"，而是"陶瓦房间能否被第三方 Scaffolding 客户端加入"——互通条件已齐，只差实机实测（T-13） |
| 对项目的价值 | 互通靠陶瓦，本分支不写协议；保持"房间码即对外标识"，别把陶瓦 HTTP API 泄漏进 UI |

### xPCL

| 项 | 内容 |
|---|---|
| 定位 | 他人开发的 PCL 二创启动器，用户指定为联机模式参考对象 |
| 可查资料 | 公开技术文档少 `[待验证]`；其联机思路在 PCL 生态中的开源实现，建议改看 PCL CE 的 `Link/` 模块 |
| 对项目的价值 | 参考其**交互流程**（几步完成建房/加入、失败怎么提示），不必复刻其实现 |

---

## 二、架构参考

### Prism Launcher

| 项 | 内容 |
|---|---|
| 定位 | MultiMC 血脉，模块化与实例抽象的最佳范本 |
| 仓库 | `PrismLauncher/PrismLauncher` |
| 看什么 | ① `BaseInstance` 抽象接口（启动、更新、模组根路径、导出）；② `MinecraftInstance` 如何在其上实现；③ `Application` 作为中央协调器管理实例/账户/网络/元数据；④ `LaunchTask`、`InstanceCreationTask` 的 Task 化异步模式 |
| 对项目的价值 | R6 与实例模型的直接参考。**以实例为中心的架构**比 PCL 的"全局配置 + 版本目录"更利于扩展，但要评估迁移成本 |

### HMCL

| 项 | 内容 |
|---|---|
| 定位 | 三层架构（平台抽象 → 业务逻辑 → UI），实例完全隔离 |
| 仓库 | `HMCL-dev/HMCL` |
| 看什么 | ① 平台抽象层如何封装 OS/CPU 差异；② `mcbbs.packmeta` 的生成代码（R2 的直接参照）；③ Terracotta 集成代码（R3 的直接参照） |
| 对项目的价值 | R2 与 R3 的实现证据链，比文档可靠 |

### PCL CE（社区分支）

| 项 | 内容 |
|---|---|
| 定位 | 基于 PCL 开源代码的社区分支，含主线未采纳的功能 |
| 仓库 | `Howard302/PCL-CE`、`PCL-Community/PCL-CE` `[待验证]` |
| 许可 | Apache 2.0（大部分目录）+ 自定义许可（PCL 主体）混合，**逐文件确认** `[待验证]` |
| 看什么 | `Link/` 目录（联机）、整合包支持实现；与官方 2.99.0.0 的差异点 |
| 对项目的价值 | 与自家基座同源，移植成本最低；但许可需逐文件核对 |

### PCL 官方（Meloong-Git/PCL）

| 项 | 内容 |
|---|---|
| 定位 | 本项目当前基座（2.99.0.0 开源版） |
| 本地可读 | `gongtong\pcl-src\build\`（已打补丁的工作树）、`gongtong\pcl-src\src\PCL-full\tree\`（未改动原始快照） |
| 看什么 | 下载/安装/启动/整合包四条链路的现有实现，**这比任何外部文档都准** |
| 注意 | `ModSecret.vb` 为空壳；主题与百宝箱在开源版是桩代码，只能自行实现 |

### fixpcl

| 项 | 内容 |
|---|---|
| 定位 | 记录 PCL 2.99.0.0 如何用 `RestrictMcDownload` 编译期常量折叠裁掉下载入口，及作者如何用 IL 重写恢复 |
| 仓库 | `ImUpXuu/fixpcl` `[待验证]` |
| 对项目的价值 | **只作生态理解材料**：告诉我们被裁掉的是什么、下游还剩什么、方法名与调用链的实际命名。**不作为修改官方二进制的依据**（理由见 `01-scope.md`） |

---

## 三、格式兼容

| 项目 | 参考什么 | 详见 |
|---|---|---|
| HMCL | MCBBS v2 的"双清单"结构（`mcbbs.packmeta` + 内含 `manifest.json`），使 MultiMC/Prism 也能导入 | `05-formats.md` |
| Prism | 实例格式 `instance.cfg` + `mmc-pack.json`，用于"直接导入 Prism 实例目录" | `05-formats.md` |
| Modrinth | `.mrpack` 的 `modrinth.index.json` 字段规范 | `05-formats.md` |

---

## 四、本地已有的参考材料

| 材料 | 位置 | 用途 |
|---|---|---|
| 上游未改动快照 | `pcl-src\src\PCL-full\tree\` | 判断某段代码是上游原样还是我们改的 |
| 上游原包 | `pcl-src\pcl-2.99.0.0.tar.gz` | 离线重新解包 |
| 上游同步脚本 | `pcl-src\sync.cmd` | 跟进新 tag 时用 |
| 补丁层 | `pcl-src\custom\`（23 文件）、`pcl-src\tools\apply-*.ps1` | 自用改动的全量记录 |

---

## 核实待办

- [x] Terracotta 仓库归属、许可例外条款原文 —— 2026-10-27 已核对（`burningtnt/Terracotta`，例外条款两条原文已录进 `06`）
- [ ] EasyTier 许可与配置格式 `[待验证]`
- [x] Scaffolding-MC 规范文档与 PCL CE 实现位置 —— 2026-10-30 已核实（规范仓库 + 两套实现），见「联机」节
- [ ] PCL CE 两个仓库地址是否都存在、许可分布 `[待验证]`
- [ ] fixpcl 仓库地址 `[待验证]`
