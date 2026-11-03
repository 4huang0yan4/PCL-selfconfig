# 14b 跨网失败根因与 P2P 可行性（T-13 续）

状态：2026-10-30 从 `14-t13-scaffolding.md` 拆出，内容原样保留。**主文件**：`14-t13-scaffolding.md`（互通实测本身）。

回答两个问题：**跨网为什么不通**（CE 侧 `--p2p-only` 硬限制）、**我们自己能不能改善 P2P**（结论：本分支这侧做不了）。

---

## 七、跨网连不上：根因分析（2026-10-30 追加）

用户实测：**局域网（VM）通，跨网不通。** 查 PCL CE 源码后，根因指向 CE 侧的 EasyTier 启动参数，**不是本分支的缺陷**。

### 证据：CE 的 EasyTier 参数里有 `--p2p-only`

`PCL.Core/Link/Scaffolding/EasyTier/EasyTierEntity.cs` 的 `_BuildProcessAsync`，宿主与客户端**都**带这几个标志：

```csharp
args.AddFlag("no-tun")
    .AddFlag("multi-thread")
    .AddFlag("enable-kcp-proxy")
    .AddFlag("enable-quic-proxy")
    .AddFlagIf(!Config.Link.TryPunchSym, "disable-sys-hole-punching")
    .Add("encryption-algorithm", "aes-gcm")
    .Add("default-protocol", Config.Link.ProtocolPreference.ToString().ToLowerInvariant())
    .Add("private-mode", "true")
    .AddFlag("p2p-only");          // ← 只做 P2P，不接受中继转发
```

`--p2p-only` 的语义是"**只允许点对点直连，不用中继兜底**"。于是：

| 场景 | 结果 | 原因 |
|---|---|---|
| 局域网（VM 桥接） | ✅ 通 | 同网段，P2P 直接可达或经局域网广播发现 |
| 跨网 | ❌ 不通 | P2P 打洞失败时**没有中继兜底** |

而用户的 CE 界面显示 **`UDP: 严格（宽松对称）, TCP: 中等（受限圆锥）`** —— UDP 严格 NAT 下打洞成功率低，正好落进"必须靠中继"的区间。

### 节点不是问题（这点先排除）

CE 的 `_fallbackNodeLinks` 与本分支陶瓦的内置节点**有交集**：

| 节点 | 陶瓦 0.4.2 | PCL CE |
|---|---|---|
| `tcp://public.easytier.top:11010` | ✅ | ✅ |
| `https://etnode.zkitefly.eu.org/node1` | ✅ | ✅ |
| `https://etnode.zkitefly.eu.org/node2` | ✅ | ✅ |
| `tcp://public2.easytier.cn:54321` | — | ✅ |
| `https://etnode.zkitefly.eu.org/-node1` `-node2` | — | ✅ |

所以**公共节点足够重合，不是"找不到彼此"**。问题在"找到了但连不通，且不允许绕行"。

### 用户实测的四组数据（2026-10-30）

| 房主 | 游客 | 内核 | 跨网 |
|---|---|---|---|
| 自编版 | 自编版 | 陶瓦 0.4.2 ↔ 陶瓦 0.4.2 | **✅ 通** |
| 自编版 | HMCL | 陶瓦 ↔ HMCL 内置陶瓦 | **✅ 通** |
| 自编版 | PCL CE | 陶瓦 ↔ CE 自研 EasyTier | **❌ 不通** |
| （局域网内）自编版 | PCL CE | 同上 | ✅ 通 |

**这几组数据把范围锁死了**：前两组本质是同一个陶瓦内核，跨网能通说明本分支的跨网链路本身没问题；第三组唯一变量是 CE。

### CE 侧已无任何可调项（用户逐条确认，2026-10-30）

我把两个"可能有效"的开关都问了，用户确认**两个都是默认值、本来就开着**：

| 开关 | 我的建议 | 实际 |
|---|---|---|
| 协议偏好 | 改成 TCP | **默认就是 TCP**，改与不改都不通 |
| 对称 NAT 打洞（`Config.Link.TryPunchSym`） | 打开它（关着会加 `disable-sys-hole-punching`） | **默认开着**，本来就在对对称 NAT 做端口猜测 |

**所以 CE 的默认配置已经是最优，改无可改。** 而 `p2p-only` 是代码里的 `AddFlag`，UI 上没有对应开关。

**结论闭合**：CE ↔ 陶瓦 跨网在双方默认配置下不可行，且**没有任何用户侧或本分支侧的调整余地**。要跨网，只有两条路——对端换成 HMCL / 同启动器，或等 CE 上游放开 `p2p-only`。

## 八、能不能我们自己实现/改善 P2P？——`[待决策，优先级弱]`

**问题**：能否通过本分支的努力，让 CE 的跨网连上（即让 P2P 打洞成功）。

**结论：在自己这一侧几乎做不了。** 逐条验证如下。

### 陶瓦确实支持 P2P/中继策略，但不给我们接口

`src/easytier/argument.rs` 的 `Argument` 枚举里有 `P2POnly` —— 说明陶瓦内部能切 P2P-only。但：

- **CLI 只暴露 `--help` / `--hmcl` / `--hmcl2`**（实测），没有传 EasyTier 参数的入口；
- HTTP API 只有 `room` / `player` / `public_nodes`，`public_nodes` 是**追加节点**（与内置合并），改不了中继策略。

### 端口预映射（UPnP）这条路也堵着

`src/ports.rs` 的 `request()` 绑定的是 **`127.0.0.1:0`（回环 + 随机端口）**，回退值 `35780 + enum` —— 这些是**本机内部服务**（EasyTier RPC / Scaffolding / MC 端口），不是 P2P 监听口。

P2P 的监听地址由 `Argument::Listener { address: SocketAddr }` 在运行时决定，**端口随机**。于是出现死锁：

1. 想在陶瓦启动前映射端口 → 不知道端口；
2. 启动后再映射 → EasyTier 已完成 NAT 类型检测，不会重新探测，除非重启 EasyTier（陶瓦不提供单独重启）。

### 唯一在理论上有效、但不该做的事

自建一个公网 EasyTier 节点传给 `public_nodes` —— 对**陶瓦对端**有用（双方都能走中继），但对 **CE 无效**，因为 CE 的 `p2p-only` 拒绝中继转发。等于花钱改善了不需要改善的场景。

### 建议

**不实现**。理由三条：

1. **控制权不在我们手里**：核心变量（CE 的 `p2p-only`、NAT 类型、路由器能力）没有一个是本分支能改的；
2. **收益不确定、成本确定**：UPnP 需要引第三方库 + 依赖路由器开启 + 只对"房主一侧可映射"生效，而对端仍需能打洞；
3. **已有可用替代**：第三方穿透（樱花等）本来就解决了"NAT 严格连不上"，延迟代价已写进提示。

**若要重新评估，先验证一个前提**（成本 5 分钟，不做代码）：确认陶瓦运行时的 EasyTier 监听端口是否**固定**。命令是启动陶瓦并建房后 `netstat -ano | findstr <terracotta PID>`，看 UDP 监听端口是否每次一致。**固定 → UPnP 方案才值得讨论；随机 → 直接否决。**

### 结论（已落 UI）

**这个限制无法从本分支绕开**：陶瓦 CLI 全部选项只有 `--help` / `--hmcl` / `--hmcl2`，没有任何 relay/p2p/node 开关；HTTP API 只能传 `room`、`player`、`public_nodes`，都碰不到 EasyTier 的中继策略。CE 侧 `p2p-only` 是硬编码 `AddFlag`。

`custom.4`（2026-10-30 15:04）在联机页加了一条常驻提示，按用户要求改成**正面表述**（先说"大部分都能用"，再点出 PCL CE 的特殊性）：

> 连不上怎么办：跨网络、且有一方网络受限（NAT 严格）时容易连不通。大部分以陶瓦联机为主的启动器可以直接互加——HMCL、FCL、ZL2、MoLaunch 等（陶瓦官方接入名单，其中 HMCL 已实测可用）；PCL CE 走的是协议互通，同一局域网能连、跨网受限。也可以改用下方的「联机方案（第三方工具）」走穿透，通常能连上但延迟不保证。

名单不是凭印象列的，来自陶瓦官方 `sites/docs/integrations.md`，共 7 个启动器 + 1 个模组：

| 接入方式 | 名单 |
|---|---|
| **直接内置陶瓦**（可直接互加） | HMCL、FCL（FoldCraftLauncher）、ZL2（ZalithLauncher2）、FMCL、MoLaunch、Axolotl |
| **通过 Scaffolding 协议互通**（本分支实测：局域网通、跨网受限） | PCL-CE |
| 游戏内模组 | VoxLink |

官方文档原文就写明了这个二分：「部分启动器直接内置陶瓦联机功能；另有部分启动器通过 Scaffolding 协议与陶瓦联机实现互通」。**这正好从官方口径解释了为什么 CE 跨网表现不同** —— 它不是内置陶瓦，是协议对接。

第二条出路是用户提的：本分支预留的第三方工具卡片（樱花穿透等）就是兜底。穿透走中转，NAT 严格也能连，代价是延迟——提示里已写明"延迟不保证"。

**不做的事**：不加"对方启动器"下拉框、不加自定义公共节点输入框。前者只是把同一句话包成控件，后者对 `p2p-only` 无效，都会让界面比事实更复杂。

### 结论口径（对外别说过头）

- **跨网能不能通，取决于两端是否允许中继，以及 NAT 类型。**
- 本分支的陶瓦侧**没有加 `p2p-only`**（我们只传 `room`，不传任何 EasyTier 参数），所以限制不在我们这。
- **但也不能说"我们的跨网一定行"**：若对端是 CE，它就是 p2p-only；若双方 NAT 都严格，没有中继无论如何都不通。