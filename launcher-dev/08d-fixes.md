# 08d 已修复的问题（回归时先扫这里）

状态：2026-10-30 从 `08b-testlog.md` 拆出，内容原样保留。**主文件**：`08-todo-risks.md`；实测记录：`08b-testlog.md`；历史锚点：`08c-anchors.md`。

用法：动到某块代码前先在这里搜一下，避免把已经修过的问题再修一遍、或误以为它没修。

---

## 已修复的问题（F 编号，回归时先扫这里）

### F-01 环境变量大小写重复导致「Minecraft 启动失败：启动进程失败」`[已修复 2026-10-27]`

**症状**：下载完成、点启动立即失败，弹「Minecraft 启动失败：启动进程失败」，堆栈指向
`System.Collections.Hashtable.Insert` / `ProcessStartInfo.get_EnvironmentVariables`，异常文本为
「已添加项。字典中的关键字:"HTTPS_PROXY"所添加的关键字:"https_proxy"」。

**根因**：Windows 环境块允许同名但大小写不同的变量共存（代理软件、容器、IDE 注入常见）。
`System.Diagnostics.ProcessStartInfo.EnvironmentVariables` 是**大小写不敏感**的 `StringDictionary`，
它从 `Environment.GetEnvironmentVariables()`（大小写敏感 Hashtable）复制时会因重复键抛 `ArgumentException`。
这是 .NET Framework 的既有行为，PCL 上游未处理。

顺带证据：PowerShell 的 `Get-ChildItem Env:` 在本机同样报「已添加了具有相同键的项」，可以拿来自查。

**修复**：`ModCustom.FixDuplicateEnvironmentVariables()`，在 `McLaunchStart` 开头调用。用
`StringComparer.OrdinalIgnoreCase` 扫一遍 `Environment.GetEnvironmentVariables().Keys`，保留首个、删除重复项
（只改当前进程环境块，不写注册表）。日志会打印 `[自用修复] 已移除 N 个大小写重复的环境变量：…`。

**回归检查**：若再次出现同类失败，先看日志有没有这行；没有则说明调用点被上游覆盖掉了
（`ModLaunch.vb` 是 custom 覆盖文件，sync 后要确认这一行还在）。

### F-02 凭据框的「隐藏 / 禁复制」返工了三轮 `[已修复 2026-10-27]`

需求：API 密钥类输入框默认隐藏（圆点）、按住按钮才显示、可粘贴不可复制。三轮返工，每轮都踩了一个 WPF/PCL 的隐性行为：

| 轮次 | 症状 | 根因 | 解法 |
|---|---|---|---|
| 一 | 遮罩里能看清明文 | 遮罩背景用了 `{DynamicResource ColorBrushBg1}`，而它是 `#bee0eafd`——**alpha=0xbe，74% 半透明**（PCL 几乎所有主题画刷都带 alpha） | 遮罩改用不透明纯色 `#FFF2F7FF` |
| 二 | Ctrl+C 照样能复制 | PCL 的 Style 里 `ClipboardInterceptor.EnableSafeClipboard=True` **已经给每个输入框挂了 `ApplicationCommands.Copy/Cut` 的 CommandBinding**；命令路由命中第一个匹配项，后加的实例级绑定永远轮不到 | 改在 `CommandManager.PreviewExecutedEvent` 拦（早于所有 CommandBinding） |
| 三 | 右键菜单仍弹出「复制/剪切」 | WPF `TextBox` 的系统编辑菜单**绕过 `ContextMenuOpening` 事件**（该事件拦不住） | 拦 `PreviewMouseRightButtonDown/Up`，右键直接吞掉 |

最终四层拦截，缺一层就漏：

1. `CommandManager.PreviewExecutedEvent` —— 命令级，压过 ClipboardInterceptor
2. `PreviewKeyDown` —— Ctrl+C / Ctrl+X / Ctrl+Insert
3. `PreviewMouseRightButtonDown/Up` —— 右键菜单（Ctrl+V、Shift+Insert 粘贴保留）
4. `HwndSource.AddHook` —— WM_COPY(0x0301) / WM_CUT(0x0300)，兜住外部程序发来的复制请求

**验证方法（可复现）**：先把剪贴板设为与密钥**不同**的哨兵值（如 `SENTINEL-0000`），再对输入框做 Ctrl+A→Ctrl+C，读剪贴板；若仍是哨兵即为拦截成功。注意哨兵不能等于被测内容，否则"没变化"无法区分。

**相关文件**：`custom\Pages\PageSetup\PageSetupSystem.xaml`（遮罩 + 按钮 + 三列布局）、`.xaml.vb`（`AttachSecretInputs` / `CredentialMaskRefresh` / `MaskText`）。

### F-03 导出时「生成压缩包失败：调用线程无法访问此对象」`[已修复 2026-10-27]`

**症状**：勾选「额外写入 MCBBS v2 元数据」后点导出，任务管理里显示

```
生成压缩包失败
→ 调用线程无法访问此对象，因为另一个线程拥有该对象。
   在 System.Windows.Threading.Dispatcher.VerifyAccess
   在 System.Windows.DependencyObject.GetValue
   在 PCL.PageInstanceExport._Closure$_26-0._Lambda$_10
```

**根因**：导出逻辑跑在 `LoaderTask` 的**后台线程**里，我直接读了 WPF 控件属性 `CheckAdvancedMcbbs.Checked`。WPF 的依赖对象有线程亲和性，后台线程读它必抛 `InvalidOperationException`。

上游的做法是**在 UI 线程把选项缓存成局部 Boolean**（`CheckHostedAssets` / `ModrinthUploadMode` / `IncludePCL` 都在 470 行附近一次性取好），我在同一段代码里新增功能时漏了这一步。

**修复**：在缓存区加 `Dim McbbsMode As Boolean = CheckAdvancedMcbbs.Checked`，后台任务里只用 `McbbsMode`。

**同类风险**：任何新加的"读 UI 状态"的代码，只要位置在 `Loaders.Add(New LoaderTask(...))` 的 Lambda 内，都必须走缓存变量。搜 `.Checked`、`.Text`、`.Visibility` 时尤其注意。

---

### F-04 中文注释让 `build.cmd` 一启动就是一堆解析错误 `[已修复 2026-10-28]`

**症状**：`build.cmd` 开头冒出多条 `'xxx' 不是内部或外部命令`，夹杂中文碎片，例如
`'向上一级。若'`、`'，?findstr'`、`'缀，否?cmd'`；随后脚本仍然继续往下跑，很容易被当成"无关警告"忽略。

**根因**：`cmd.exe` 按 **OEM 代码页**（中文 Windows = GBK）逐字节解码 `.cmd`。
UTF-8 的中文句号「。」是 `E3 80 82`；按 GBK 解码时 `E3 80` 先配成一个字，剩下的 `82`
再与**行尾的回车 `0D`** 配成一个双字节字符——回车被吃掉，下一行并入本行注释，
于是注释的后半句被当成命令执行。CRLF 也不救，因为被吃掉的就是那个 CR。

**处理**：`build.cmd` 整体改成**纯 ASCII 注释**（逻辑一行未改，含 `PS_EXE` 绝对路径调用与 ISO 时间戳）。
`sync.cmd` 本就是纯 ASCII。校验办法：`LC_ALL=C grep -c '[^ -~	]' build.cmd` 必须为 0。

**规矩**：`.cmd` 只写 ASCII；中文只放 `.ps1`（UTF-8 with BOM）。这与"脚本编写规范"里的编码铁律一致——
不是风格偏好，是 cmd 的解析行为决定的。

**连带教训**：改完 `.cmd` 必须真的跑一次。这次改动里还有一处 `for /f` 把 PowerShell 写成
`('"C:\...\powershell.exe" -NoProfile ...')`，因 `cmd /c` 会剥掉首个引号而必然失败；两者叠加，
文件在 14:15 改完后从未被执行过，第二天才发现。**`for /f` 内调用带路径的 exe 必须加 `call` 前缀。**

### F-05 受限 shell 里 NuGet 报 `Value cannot be null. (Parameter 'path1')` `[环境限制，非代码缺陷]`

**症状**：`dotnet build/restore` 在 `NuGet.targets(745,5)` 处失败，堆栈为
`NuGetEnvironment.GetFolderPath` → `XPlatMachineWideSetting..ctor` → `ConfigurationDefaults` 静态构造。

**根因**：这类会话的环境块被剥离，`ProgramData` / `ALLUSERSPROFILE` / `PUBLIC` / `LOCALAPPDATA`
全部**未定义**（`cmd /c echo %ProgramData%` 会原样输出 `%ProgramData%`），且
`Environment.GetFolderPath(CommonApplicationData)` 返回 null，NuGet 拿它去 `Path.Combine` 就抛这个异常。
补设环境变量、加 `-p:RestoreRootConfigDirectory`、加 `-p:RestoreConfigFile` 都无效——是平台 API 返回值的问题。

**判据**：同一个 `gongtong` 目录在用户正常终端里能编。所以遇到此错先把环境查一遍，不要去改工程。
`--no-restore` 可绕过（`obj\project.assets.json` 需已存在）；2026-10-28 已把该路径固化为 `build.cmd -NoRestore`，
并实测编出 `2026.10.28-custom.1`（`0 错误`）。另外查证：本机 `Environment.GetFolderPath(CommonApplicationData)`
**返回正常**（`C:\ProgramData`），但在 cmd 里显式 `set ProgramData=…` 也不解决——说明 NuGet 读的是机器级作用域的值，
沙箱下取不到，纯环境变量层面无解。

---

### F-06 切到设置页弹「程序出现未知错误：未找到设置项：」`[已修复 2026-10-29]`

**症状**：`custom.14` 启动后点「设置」，弹框「程序出现未知错误：未找到设置项：」（冒号后为空），
堆栈 `PCL.Settings.Get` ← `PCL.MyHint.MyHint_Loaded`，异常类型 `KeyNotFoundException`；日志见 `PCL\Log1.txt` 11:19:20。

**根因**：`MyHint_Loaded` 在 `CanClose=True` 时无条件 `Settings.Get(Of Boolean)(RelativeSetup)`。
`PageSetupLaunch.xaml` 里「启动卡住时自动重试」卡片那条提示条只写了 `CanClose="True"`、没给 `RelativeSetup`，
键为 `""` → 设置字典里没有空键 → 抛异常，整个设置页加载中断。

**修复（`custom.15`）**：提示条补 `RelativeSetup="HintLaunchAutoRetry"`；`Settings.vb` 同步注册
`New Setting("HintLaunchAutoRetry", False, Source:=Sources.Registry)`。改完 `apply-custom.ps1` 覆盖回 `build\`，
`build.cmd` 重编，`0 错误`，归档 `2026.10.29-custom.15`。

**回归验证**：启动 `custom.15` → 点「设置」→ 页面正常渲染（内存分配 / 高级选项 / 启动卡住时自动重试），
`Log1.txt` 全篇 0 条 ` E ` 行，无 `KeyNotFoundException`。

**同类风险**：任何新增 `MyHint` 只要写 `CanClose="True"`，必须同时给 `RelativeSetup` 并在 `Settings.vb` 登记；
排查时直接搜 `CanClose="True"` 逐个核对（本次只发现这一处漏配）。

---

### F-07 自动重试卡片的提示条数字与下拉框不一致`[已修复 2026-10-29]`

**症状**：设置 → 启动 →「启动卡住时自动重试」里，提示条写死「最多重试 1 次、超时 30 秒」，下拉框却可以是 45 秒 / 3 次，两处数字互相矛盾。

**根因**：提示条是静态文案，把默认值当成了现状描述。下拉框本身的读取与保存都正常——把注册表改成 3 / 60 重启后，界面正确显示 60 秒 / 3 次（已实测），所以问题只在文案。

**修复（`custom.17`）**：文案改为「默认开启；次数与超时以上方选择为准。每次重开都会写进启动日志，搜「自动重新启动」可查。」，不再重复具体数值，单行显示。

**回归验证**：`custom.17` 在 45 秒 / 3 次的设置下打开该卡片，文案与下拉框不再冲突；截图 `evidence\2026-10-29-f07-retry-hint.png`。

**教训**：任何可调参数的说明文字都不要写死当前值；要么中性表述（"以上方为准"），要么按设置动态生成。

---

---

