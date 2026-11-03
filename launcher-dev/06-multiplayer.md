# 06 联机方案

状态：**调研、单机验证与跨机联调均已完成**（2026-10-27）；T-01/T-02 本体 + 虚拟机实测通过
最后更新：2026-10-27

---

## 一、结论先行

| 判断 | 依据 |
|---|---|
| **方案 A（打包二进制 + HTTP API 调用）成立** | 本机实测 18 项集成契约全过，且"建房"单机走到 `host-ok` 并拿到真实房间码 |
| 不需要自研组网 | 陶瓦基于 EasyTier，已解决 NAT 穿透与中继 |
| 合规成本为零（自用） | AGPL 例外条款第 1、2 条都可用；不发布即无义务 |
| **同机无法自测游客端** | 全局文件锁使一台机器只能有一个陶瓦守护进程、一份状态；2026-10-27 用虚拟机绕过并实测联机成功 |
| 唯一必须守住的 UX 前提 | **建房前必须先在游戏里"对局域网开放"** |

**实测证据**：`_tools\terracotta\smoke\` 下的 `probe.py`（契约）、`host_probe.py`（建房）。

```
probe.py（契约，18 项全过）         host_probe.py（建房）
  PORT 端口文件 0.10s                host-scanning → host-starting → host-ok
  PORT /meta /state /state/ide       耗时 4.1 秒
  PORT 第二实例复用同一端口           房间码 U/D8P6-1PM5-ZQEQ-EYY8
  PORT 房间码校验位错→400 对→200
  PORT 无局域网 → 停在 host-scanning
```

---

## 二、二进制来源与校验（已实测）

| 项 | 值 |
|---|---|
| 仓库 | `burningtnt/Terracotta`（主仓，232 星，2026-10-24 仍在提交） |
| 许可 | AGPL-3.0-or-later + 例外条款 |
| 当前版本 | **v0.4.2**（2026-02-09） |
| Windows 包 | `terracotta-0.4.2-windows-x86_64-pkg.tar.gz`，8,135,914 字节 |
| SHA-512 | `6a98f524d4f00373696517306af8aa50d01d55ce4eadb27e9e4bc2f882707a0b5f20d5d4c33371d1459dcf5bf144ffed9beb414202d9ccf32b11dbbfcf19d650` |
| 包内容 | **`terracotta-*.exe` + `VCRUNTIME140.DLL`**（两个文件，必须同目录） |
| 本机位置 | `gongtong\_tools\terracotta\app\` |

下载模板（HMCL 内置 `assets/terracotta.json` 的写法，`${version}` / `${classifier}` 替换）：
```
https://github.com/burningtnt/Terracotta/releases/download/v${version}/terracotta-${version}-${classifier}-pkg.tar.gz
https://gitee.com/burningtnt/Terracotta/releases/download/v${version}/...            （CN）
https://cnb.cool/HMCL-Terracotta/Terracotta/-/releases/download/v${version}/...      （CN，本次实测最快）
https://alist.8mi.tech/d/mirror/HMCL-Terracotta/Auto/v${version}/...                 （CN 镜像）
```
`classifier`：`windows-x86_64` / `windows-arm64` / `macos-{x86_64,arm64}` / `linux-{x86_64,arm64,loongarch64,riscv64}` / `freebsd-x86_64`。

> 下载后**必须**核对 SHA-512。HMCL 的同款清单可作交叉验证来源。

---

## 三、进程模型（集成方式的核心）

```
terracotta.exe --hmcl <临时文件路径>
        │
        ├─ Windows 上：父进程立刻以 --hmcl2 分离出子进程，然后自己退出
        └─ 子进程：监听 127.0.0.1:<随机端口>，并原子写入 <临时文件> = {"port":53318}
```

- **端口是随机的**（Rocket 配置 `port: 0`），**不能写死**，必须等端口文件。
- 写文件用 `tmp` + `rename`，所以"文件存在"即内容完整；实测 **0.10 秒**出现。
- **全局单实例**：`%TEMP%\terracotta\terracotta.lock` 是文件锁，前 2 字节存端口（大端）。
  第二个实例检测到锁 → 进入 `Secondary` 模式 → **把自己的端口文件写成同一个端口**，不重复起服务。
  实测：两个实例的端口文件内容完全相同。
- 版本接管：新实例发现已运行实例的 `compile_timestamp` 更旧时，会 `GET /panic?peaceful=true` 干掉它再接管。

集成含义：

1. PCL 只需 `Process.Start(exe, "--hmcl " & 临时文件)`，轮询读该文件即可，**无需抓 stdout**（陶瓦是窗口子系统程序，控制台无输出）。
2. 用户同时开 HMCL 和 PCL 时**共用同一个陶瓦进程**，不会起两份。副作用是状态共享（见第九节坑 6）。
3. 锁文件残留（崩溃后）不能当"正在运行"的证据，必须 `GET /meta` 探活。

---

## 四、HTTP API 全表

> 已移到 `06b-terracotta-api.md` 第一节：全部 GET 接口、参数、返回、状态码。

## 五、状态机（UI 文案直接照这个映射）

| `state` | 附带字段 | 建议 UI 文案 |
|---|---|---|
| `waiting` | `index` | 空闲，可建房或加入 |
| `host-scanning` | `index` | **正在等待游戏开启局域网…**（见第六节，这一步可能无限期） |
| `host-starting` | `index`, `room` | 正在创建房间，房间码 `room` |
| `host-ok` | `index`, `room`, `profiles`, `profile_index` | 房间已就绪，把这串码给对方：`room` |
| `guest-connecting` | `index`, `room` | 正在连接房间… |
| `guest-starting` | `index`, `room`, `difficulty` | 连接建立中（`difficulty` 表示穿透难度） |
| `guest-ok` | `index`, `url`, `profiles`, `profile_index` | 已就绪，进游戏多人列表双击大厅条目 |
| `exception` | `index`, `type`(0–5) | 失败，按 type 给建议 |

`exception.type` 对照（源码 `ExceptionType` 枚举顺序）：

| type | 含义 | 建议文案 |
|---|---|---|
| 0 | `PingHostFail` | 连不上房主：对方可能已关房、离线，或网络被限制 |
| 1 | `PingHostRst` | 房主主动断开 |
| 2 | `GuestEasytierCrash` | 本机组网组件崩溃，重试或看日志 |
| 3 | `HostEasytierCrash` | 房主侧组网崩溃 |
| 4 | `PingServerRst` | 房主侧的 Minecraft 已关闭（退出世界/游戏） |
| 5 | `ScaffoldingInvalidResponse` | 房间码或协议不匹配，确认双方版本一致 |

`difficulty` 取值：`UNKNOWN` / `EASIEST` / `SIMPLE` / `MEDIUM` / `TOUGH` —— 可直接映射成"网络状况：极佳/良好/一般/较差"。

---

## 六、建房为什么要求"先对局域网开放"（关键 UX）

陶瓦**不负责开世界**，它靠监听 Minecraft 自己的局域网广播（UDP 4445，组播 `224.0.2.60`）发现世界：

```
Minecraft「对局域网开放」 → 周期性广播 [MOTD]...[/MOTD][AD]端口[/AD]
                              ↓
陶瓦绑定 4445 监听 → 取 AD 得到端口 → 才可能进入 host-starting
```

实测：**不发广播时，状态会永久停在 `host-scanning`**，不报错、不超时。
所以界面上必须先有一步提示，否则用户会以为卡死。建议流程：

1. 点「创建房间」→ 若当前状态不是 `host-scanning`，先提示"请在游戏里按 Esc → 对局域网开放 → 开始"。
2. 点「我已开放」→ `GET /state/scanning` → 显示"正在等待游戏开启局域网…"。
3. 超过 20 秒仍 `host-scanning` → 追加提示与「取消」按钮（取消 = `GET /state/ide`）。

**游客侧不用手输地址**：`guest-ok` 后陶瓦会以 1.5 秒间隔广播一个假世界，MOTD 为
`§6§l双击进入陶瓦联机大厅（请保持陶瓦运行）`，`AD` 指向本机代理端口。
所以游客只要进「多人游戏 → 局域网世界」双击那一条即可；`url` 字段（`127.0.0.1[:端口]`）留给"直接连接"兜底。

---

## 七、房间码格式

> 已移到 `06b-terracotta-api.md`：`U/XXXX-XXXX-XXXX-XXXX`、34 字符表、`I`/`O` 归一、校验位能被 7 整除。

## 八、集成设计（落到本项目的形状）

R6 的接口草案按实测调整后：

```vb
Public Interface IMultiplayerProvider
    ReadOnly Property Id As String
    ReadOnly Property DisplayName As String
    Function IsAvailable() As Boolean            ' 二进制存在 + 版本可读
    Function StartHost(RoomCode As String) As Boolean  ' 空串=随机
    Function JoinRoom(RoomCode As String) As Boolean
    Function GetStatus() As ProviderStatus       ' Stage + Detail + RoomCode
    Sub [Reset]()                                ' 取消/回空闲
    Sub [Stop]()                                 ' 关掉后端进程
End Interface
```

`ProviderStatus` 建议字段：`Stage`（Idle/WaitingLan/Hosting/Hosted/Joining/Joined/Failed）、
`RoomCode`、`Detail`（人类可读）、`Difficulty`、`ExceptionType`。

实现要点（`TerracottaProvider`）：

1. 启动前读锁文件探活：`%TEMP%\terracotta\terracotta.lock` 前 2 字节 = 端口 → `GET /meta` 确认版本一致；不一致则用 `--hmcl` 重新拉起（会版本接管）。
2. 拉起：`Process.Start(exe, "--hmcl """ & 临时文件 & """")`，200ms 轮询读端口文件，最多 20 秒。
3. **所有本地 HTTP 必须绕开系统代理**（见第九节坑 1）。
4. 状态轮询 500–1000ms 一次；`waiting` 时降频。
5. UI 只认 `ProviderStatus`，不出现 `If Provider = "terracotta"`。

> **已实现清单**（2026-10-27 那批落地项与实测结论）已移到 `08b-testlog.md` 末尾。
> 这里只保留设计形状，历史落地明细不再重复。

## 九、坑 / 十、许可 / 十一、待办 / 十二、变更记录

> 已移到 `06b-terracotta-api.md`：10 条实测坑（全局文件锁、状态机复位、端口漂移、`/panic` 无响应等）、AGPL 例外条款、待办与变更记录。
