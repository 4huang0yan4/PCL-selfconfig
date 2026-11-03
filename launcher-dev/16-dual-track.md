# 16 双轨维护：自用版与仓库版

状态：`[现行]`（2026-10-30 建；同日 21:40 改为私人仓库口径）

## 一、为什么有两份

上游《PCL 存储库合理使用指南》对「重度使用」有一条硬约束：

> 软件在 Windows 系统上运行时，不应有与 PCL 的赞助解锁功能类似的功能或表现。
> 例如：界面的主要色相必须为蓝色。

本分支自用版为方便，拆掉了主题的赞助解锁（预设配色全开、色相 0–360 自由调）。这在自用时无碍，
**但公开发布就构成违规**。用户决定：**本地自用保持原样，另出一份合规的发布副本**。

## 二、两份在哪

| 用途 | 路径 | 主题行为 | 去向 |
|---|---|---|---|
| **自用** | `gongtong\pcl-src\` | 预设配色全开 + 自定义色相滑块（0–360） | ❌ 不上传 |
| **仓库版** | `gongtong\pcl-source-github\pcl-src\` | **与上游一致**：预设锁回赞助解锁、4 个滑块 `Visibility=Collapsed` | ✅ 私人仓库（2026-10-30 起，非公开） |

仓库版是在自用版基础上**回退 4 个文件**得到的，其余完全相同。已 `git init` 并提交，
根目录有 `AGENTS.md`（给智能体看的入口）。

**仓库地址**：<https://github.com/4huang0yan4/pcl-shiguangxiugaiban>（Private）
远端用 **SSH**：`git@github.com:4huang0yan4/pcl-shiguangxiugaiban.git`——本机走代理时 `git` 的 CONNECT 隧道被拒（502），
HTTPS 推不上去，SSH 22 端口反而通。密钥 `~/.ssh/id_ed25519_pcl`，已写进 `~/.ssh/config`。
2026-10-30 21:49 首次推送成功（`e7b41b7`，721 文件）。

## 三、两份的差异清单（共 4 个文件）

| 文件 | 自用版 | 发布版 |
|---|---|---|
| `Pages/PageSetup/PageSetupUI.xaml` | 预设区 `IsEnabled="False"` 全去掉；4 个配色滑块可见；顶部有一句 MyHint 说明 | **原样用上游**：`IsEnabled="False"` 保留、滑块 `Visibility="Collapsed"`、无说明 |
| `Modules/ModSecret.vb` | `ThemeCheckAll` → 调 `ModCustom.ThemeCheckAllImpl` | **空实现**（上游行为） |
| `Modules/ModCustom.vb` | 有 `EnsureCustomTheme`（强制切自定义）与 `ThemeCheckAllImpl`（解锁 UI） | **两个函数已删除** |
| `FormMain.xaml.vb` | 启动时调 `ModCustom.EnsureCustomTheme()` | **该调用已删除** |

`custom\` 覆盖层里的 `ModSecret.vb` / `ModCustom.vb` **也要同步改**，否则 `sync.cmd` 会用覆盖层把 `build\` 覆盖回解锁版。

**另有一处 2026-10-30 22:20 才出现的差异**：仓库版把上游的 `PanLauncherHide` 赞助遮罩块
（"赞助一下就能解锁主题"那段，7 行 XAML）**原样补回**了，自用版仍是删除状态。理由是 `LICENCE`
第 4 条要求保留赞助劝导。该块默认 `Visibility="Collapsed"`，两版都不会显示，**行为无差异**；
但若再次从自用版同步 `PageSetupUI.xaml`，记得把这 7 行补回去，否则仓库版的合规性会退化。

## 四、同步口径（重要）

**口径（2026-10-30 用户定）：以自用版为源，逐项剔除不合规的部分，其余照传。**
不再维护"先回退好再当起点"的另一棵树——仓库副本就是自用版同步过来、再把第 5 条相关的东西压回去。

**每次改完自用版代码，往仓库副本同步时：**

```bat
rem 1. 把改动同步过去（排除产物/工具链/密钥）
MSYS_NO_PATHCONV=1 robocopy "D:\...\gongtong\pcl-src\build" ^
  "D:\...\pcl-source-github\pcl-src\build" /E /XD obj bin /XF secrets.env

MSYS_NO_PATHCONV=1 robocopy "D:\...\gongtong\launcher-dev" ^
  "D:\...\pcl-source-github\launcher-dev" /E

rem 2. 然后重新应用「发布版回退」——上面 4 个文件会被覆盖回去，必须再改一遍
```

**这 4 个文件在同步后必须重新回退。** 建议做成脚本，否则一定会忘。
（脚本还没写，属于待办；在写出来之前，按本文第三节表格手工核对。）

### 4.0 同步的硬规则：不能按文件整拷（2026-10-30 / 10-31 共踩两次）

**两次踩在同一处**：

- **10-30**：为同步 Entra 跳转和改名，从 `gongtong\pcl-src` 整拷了 `ModCustom.vb` 与 `FormMain.xaml.vb`，
  解锁实现当场回去（`EnsureCustomTheme`、`ThemeCheckAllImpl`），并进了 git 历史。
- **10-31**：同样的整拷又发生一次。**而且上一次的「自检通过」是假的**——检查命令把路径写死成
  `pcl-src/build/...`，`custom\` 整棵树没被扫到。于是 `build\` 干净、`custom\` 带解锁，
  提交时两棵树一起进去，历史里每一代都能 `git show` 出解锁代码。

**规则**：

1. 同步前先看文件名在不在「需回退的 4 个文件」里。在 → **不要整拷**，手工只搬目标改动。
2. 4 个文件是：`PageSetupUI.xaml`、`ModSecret.vb`、`ModCustom.vb`、`FormMain.xaml.vb`。
3. 同步完**必须两棵树一起查**，少查一棵等于没查：

```bash
cd <仓库副本>/pcl-src
for side in "build/Plain Craft Launcher 2" "custom"; do
  echo "-- $side"
  grep -rc "ThemeCheckAllImpl\|EnsureCustomTheme\|去掉了全部预设配色" \
       "$side/Modules/ModCustom.vb" "$side/Modules/ModSecret.vb" \
       "$side/FormMain.xaml.vb" "$side/Pages/PageSetup/PageSetupUI.xaml"
  grep -c 'IsEnabled="False"' "$side/Pages/PageSetup/PageSetupUI.xaml"
done
```

**判据：两棵树的解锁关键字全为 0；`IsEnabled="False"` 全为 10**（上游 12，差 2 是已删的联机页）。

4. 已经提交进历史了，**别只再加一个修复提交**——历史里那版还在。
   - 只涉及最后一两个提交：`git reset --soft <故障前>` 再 `git commit`
   - 涉及多代：`git filter-branch --force --prune-empty --tree-filter '<脚本>' -- --all`，
     脚本里逐提交把 4 个文件换成该提交**自己的** `build/` 版本（这样每一代都自洽）
5. 重写后必须清三样，否则本地仍能取出旧内容：

```bash
git for-each-ref --format='%(refname)' refs/original | xargs -n1 git update-ref -d
git reflog expire --expire=now --all
git gc --prune=now
```

6. 收尾验一遍**旧 SHA 必须不可达**：`git cat-file -e <旧SHA>` 要失败。
   只 `push --force` 不清本地对象，`git show <旧SHA>:...` 照样取得出。

### 4.0.1 2026-11-03 补记：发布副本曾因「4 文件失同步」而无法编译

**现象**：发布副本用 `WB_ROOT` + `PCL_TREE` 编译，四工程中第 4 个报 3 处 `BC30456`：

```
PageDownload\Resource\MyResourceItem.xaml.vb(109,22): error BC30456: "DownloadPlugin" 不是 "FormMain.PageSubType" 的成员
PageDownload\PageDownloadLeft.xaml.vb(44,18):        error BC30456: 同上
PageOther\PageOtherTest.xaml.vb(23,9):                error BC30456: "SpaceAnalyzeImpl" 不是 "ModCustom" 的成员
```

**根因（对本文第三节理解的一次修正）**：4 个隔离区文件**不是「回退到上游原版」**，而是
**「自用版减去解锁部分」**。10-31 之后自用版给 `FormMain.xaml.vb` 加了 `DownloadPlugin = 8`
枚举、给 `ModCustom.vb` 加了 `SpaceAnalyzeImpl` 与 `SystemSelfUpdate` 分支，这些**与主题无关**，
但 4 个文件当时按「上游原版」维护，于是把功能一起丢了；`custom\` 覆盖层同样滞后 105 行。

**修法**：以自用版文件为基，只剥三处 ——

| 文件 | 剥离内容 |
|---|---|
| `ModCustom.vb` | `EnsureCustomTheme`（含注释块）、`ThemeCheckAllImpl` 整段 |
| `ModSecret.vb` | `ThemeCheckAll` 体内的 `ModCustom.ThemeCheckAllImpl(EffectSetup)` 调用改成空注释 |
| `FormMain.xaml.vb` | `ModCustom.EnsureCustomTheme()` 调用及其上一行注释 |
| `PageSetupUI.xaml` | 保持发布副本原样（已合规，无需从自用版取） |

`custom\` 覆盖层的同名文件直接取剥离后的结果，保持与 `build\` 逐字节一致。

**验证（2026-11-03）**：4 工程 `0 个错误`，产物 6,124,544 字节；两棵树解锁关键字 0 命中、
`IsEnabled="False"` 10 处、`PanLauncherHide` 保留；exe 内四个标记字符串（`EnsureCustomTheme`、
`ThemeCheckAllImpl`、`去掉了全部预设配色`、`解锁主题选项失败`）均 0 次。

**下次同步的做法**：先 `diff --strip-trailing-cr` 自用版与发布副本的同名文件，若差异里出现
「非主题相关的新增」（新枚举、新函数、新分支），说明又漂了，按上表重做一遍，别整文件拷贝。

### 4.1 同步后还要做的两件清理（2026-10-30 补）

**① 脱敏。** 发布副本的 `launcher-dev` 已去掉本机绝对路径，但同步会重新带出来。每次同步后扫一遍：

```bash
grep -rnE "[A-Z]:\\\\" <发布副本>/launcher-dev
```

命中就替换成占位（不是删掉整句，否则文档断意）：

| 真实前缀 | 替换为 |
|---|---|
| `<工作区>` | `<工作区>` |
| `<记忆库>` | `<记忆库>` |
| `<本地>` · `<下载目录>` | `<本地>` · `<下载目录>` |
| `<备份盘>` | `<备份盘>` |

`C:\ProgramData`、`C:\...\powershell.exe` 这类系统通用路径可保留。
**`pcl-src` 里的盘符命中不用管**——那是 Windows 路径常量与 dll 的 `publicKeyToken`，正常。

**② 清掉不该进仓库的东西。** `robocopy /E` 只增不删，源端删了的目录在副本里会留尸：

| 必须清 | 原因 |
|---|---|
| `launcher-dev/evidence/` | 截图（窗口里可能带本机路径）+ 第三方整合包样本 |
| `launcher-dev/_backup/` | 文档旧版备份，纯噪音 |
| `pcl-src/build/diag*.log` | 诊断日志，曾占 5.7 MB |
| `**/*.orig` `**/*.verorig` | 补丁/版本号工具的残留 |

### 4.2 仓库根的三份"门面"文件（同步不会覆盖，改动要单独搬）

`AGENTS.md`（给智能体看的入口）、`README.md`、`声明.md`、`THIRD-PARTY-NOTICES.md` 只存在于发布副本，
**本地 `gongtong` 没有对应源**。改它们直接在副本里改，并留意它们与 `15` 的结论是否还一致。

## 五、验证发布版是否可编译

发布副本不含 `_tools`，但 `build.cmd` 支持两个环境变量指向外部：

```bat
set WB_ROOT=D:\...\gongtong                              rem 工具链所在（含 _tools）
set PCL_TREE=D:\...\pcl-source-github\pcl-src\build      rem 要编译的树
cd /d D:\...\pcl-source-github\pcl-src
build.cmd -NoArchive
```

`-NoArchive` 只编译、不写 `PCL-custom\versions\` 归档，适合验证。
2026-10-30 用这条命令验证过：**4 个工程全部生成成功**，产物 5,926,400 字节。

编译完记得清理 `obj` / `bin`（`.gitignore` 会挡住，但物理上不该留在上传目录）。

## 六、发布版合规自检（每次发布前跑一遍）

```bat
rem 解锁痕迹应为 0
findstr /C:"EnsureCustomTheme" <发布副本源码>
findstr /C:"ThemeCheckAllImpl" <发布副本源码>
rem 产物里也不该有（编完的 exe）
python -c "d=open(r'...bin\...exe','rb').read(); print(d.count('EnsureCustomTheme'.encode()))"
```

2026-10-30 已验证：
- 源码里两个函数名 0 命中
- 产物 exe 里 `本版本去掉了全部预设配色` / `解锁主题选项失败` / `EnsureCustomTheme` / `ThemeCheckAllImpl` **全部 0 次**
- XAML 的 `Visibility="Collapsed"` 22 处、`IsEnabled="False"` 10 处，与上游一致（差的 2 处是已删除的「联机页」隐藏项，与解锁无关）

## 七、风险提示

1. **最大的风险是漂移**：改自用版后忘了回退发布副本，或反过来。
   缓解：本文件第三节那 4 个文件当作"隔离区"，任何一次同步后都用自检脚本核一遍。
2. **发布版不能因为"自用方便"再被改回去**。若将来又想放开主题，只能改自用版。
3. `custom\` 覆盖层里也有 `ModSecret.vb` / `ModCustom.vb`，**两处都要改**，否则 `sync.cmd` 会把 `build\` 覆盖回解锁版。
   本次已同时处理。

## 八、待办

- [ ] 把"同步 + 回退 4 个文件 + 自检"写成一个脚本（`tools\prepare-release.ps1`），避免手工漏改
- [ ] 发布版编译一次并实机启动，确认主题页确实锁住了（当前只验证了编译通过 + 字符串清除）
