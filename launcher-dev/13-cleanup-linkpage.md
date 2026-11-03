# 13 联机页遗留代码：处置方案与删除清单（待复核）

状态：`[已执行 2026-10-29]`（用户选 B 方案 + 彻底清除；产物 `custom.21` / `custom.22`，编译 0 错误）
来源：记忆库 `xiangmu/pcl-shiguang-qidongqi` →「联机页遗留代码（2026-10-29 盘点）」
基线：`2026.10.29-custom.20`
结论先说：**分两件事做——先修一个会误判的活接口，再决定要不要拆死代码。**

---

## 一、必须先修的一件事（不改界面，0.5h）

**问题**：`ModEvent.vb:336` 的 `Case EventType.加入房间` 还在调用 `PageLinkMain.Join()`，而 `Join()` 走的是 PCL 旧邀请码校验（`ValidateCodeFormat`）。帮助文档里只要出现该事件，就会弹出旧邀请码框；用户贴陶瓦房间码（`U/XXXX-…`）会被判成

> 邀请码有误，请让房主使用 PCL 创建房间！

这是**误导性提示**，不是死代码。

### 方案对比

| | A. 事件改走陶瓦（推荐） | B. 屏蔽事件 |
|---|---|---|
| 做法 | `Join(Code)` 开头先判 `ModCustom.TcCheckCode(Code)`：命中则切到联机页、填入房间码、走 `TcJoin`；不命中再回落到旧逻辑或给"请使用 U/ 开头的陶瓦房间码" | 在 `ModEvent` 的 `Case` 里直接 `Return`，并 `Logger.Info` 记一条 |
| 触点 | `PageLinkMain.xaml.vb` 的 `Join()`（约 126–158）+ `CompTerracotta` 的加入入口 | `ModEvent.vb:336–344`（约 9 行） |
| 收益 | 事件语义正确，帮助文档里的"加入房间"真的能加入 | 最省事 |
| 代价 | 多接一处 UI 切换（复用现有 `TextTcRoom` + `BtnTcJoin`） | 该自定义事件实际作废 |
| 风险 | 低（不改陶瓦协议，只加前置判断） | 低，但等于承认功能残缺 |

**建议选 A**；若你只想先止血，选 B 也合规，但要显式写进文档「该事件已禁用」。

---

## 二、删除清单（分三批，每批可独立回退）

> 依据：`PageLinkMain.xaml.vb` 1055 行、`PageLinkMain.xaml` 185 行、`PageSetupLink.xaml` 72 行 + `.xaml.vb` 102 行。
> 三批都做完约省 **900 行**（含 XAML）。

### 批次 1：设置页的「联机」整页（最安全，先删）

| 要删 | 位置 | 说明 |
|---|---|---|
| 整页 XAML + 代码 | `Pages\PageSetup\PageSetupLink.xaml`、`PageSetupLink.xaml.vb`（174 行） | 设置里硬隐藏（`PageSetupUI.xaml.vb:496` 把 `FrmSetupLeft.ItemLink.Visibility = Collapsed`），用户永远看不到；内容是顶部 `CardLinkTool` 的**过时副本** |
| 隐藏那行 | `PageSetupUI.xaml.vb:496` | 页都不在了，这行一并删 |
| 导航项 | `PageSetupUI.xaml` 里 `ItemLink`（若存在） | 只删不可达项，不动其它设置项 |

**验证**：编译 0 错误；设置页可正常打开、左侧没有「联机」；顶部联机页正常。

### 批次 2：两张死卡 + 完成态

| 要删 | 位置 | 说明 |
|---|---|---|
| `PanSelectCreate`、`PanSelectJoin` 两张卡 | `PageLinkMain.xaml:75`、`:100` 起 | 初始 `Collapsed`，**全代码库没有任何地方把它设为 Visible** |
| 对应两个事件处理 | `PageLinkMain.xaml.vb` 里 `Handles …MouseLeftButtonUp` | 随卡片一起删 |
| `LinkStates.Finished` 与 `ChangeState` 的 Case | 枚举 `:16–21`、`ChangeState` 的 `Select Case` | 完成页已删（`custom.13`），该分支只剩空调用 |

**验证**：编译 0 错误；联机页滚动正常、陶瓦卡片可用。

### 批次 3：EasyTier 空壳（最大，最后做）

| 要删 | 位置 | 行数 |
|---|---|---|
| Region「加载」 | `PageLinkMain.xaml.vb:359–718` | 360 |
| Region「进程与日志」 | `:720–832` | 113 |
| Region「CLI 节点信息」 | `:834–1010` | 177 |
| 死方法 | `CheckCrash()`、`Update()`、`ProcessCore` / `ProcessStart` / `Panic` / `LogHistory` / `PathEasyTier` / `RefreshPeerLoader` | 视引用而定 |

**验证**：编译 0 错误；联机页建房/加入冒烟（陶瓦 7 项里挑 3 项：安装已存在、建房出码、复位）；`FormMain` 关窗仍会调 `TryExit`，需确认没有编译/运行错误。

---

## 三、不可删（删了会炸）

| 项 | 位置 | 为什么留 |
|---|---|---|
| `TryExit()` | `PageLinkMain.xaml.vb:299–318` | `FormMain.xaml.vb:585` 关窗时调用做二次确认；本分支状态恒 `Waiting`，实际空转，但调用点在 |
| `Join()` 无参版 + `ValidateCodeFormat()` | `:126–170` | `ModEvent.EventType.加入房间` 仍会调它——修完第一节后可保留（走陶瓦前置判断），不要连着删 |

---

## 四、执行顺序与回退

1. **备份先行**：把 `PageLinkMain.xaml(.vb)`、`PageSetupLink.xaml(.vb)`、`PageSetupUI.xaml.vb` 复制到
   `pcl-src\_deprecated\2026-10-29-linkpage\`（不放进 `custom\`，避免被覆盖层带回去）。
2. 走第一节的方案（A 或 B）→ `apply-custom` → `build.cmd` → 冒烟。
3. 批次 1 → 编译 + 设置页冒烟；批次 2 → 编译 + 联机页冒烟；批次 3 → 编译 + 建房冒烟。
4. **每批单独出包**（`custom.21` / `.22` / `.23`），出问题只回退那一批：从备份目录拷回对应文件、重跑 `apply-custom` + `build`。
5. 收工更新：本文件状态列、`08` 锚点、记忆库笔记的「联机页遗留代码」一节删掉已处理项。

## 五、工时

| 项 | 预估 |
|---|---|
| 第一节 修事件 | 0.5h |
| 批次 1 | 0.5h |
| 批次 2 | 0.5h |
| 批次 3 | 2–3h（含冒烟） |
| 合计 | 约半天到一天 |
