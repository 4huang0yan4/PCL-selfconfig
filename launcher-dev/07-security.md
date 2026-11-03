# 07 安全与完整性

状态：`[已实施 2026-10-27]`（第五节于 2026-10-28 随归档哈希落地更新）
最后更新：2026-10-28

> 目标不是"做成安全产品"，而是**不被低级问题坑到**：令牌进了日志、依赖有已知 CVE、下到被篡改的包、密钥进了版本库。
> 三个检查都在 `pcl-src\tools\scan.ps1` 里，一条命令跑完。

---

## 一、怎么跑

```powershell
.\tools\scan.ps1              # 全部检查（联网查漏洞库）
.\tools\scan.ps1 -Only deps   # 只查依赖
.\tools\scan.ps1 -Only code   # 只查敏感信息落盘
.\tools\scan.ps1 -Only hash   # 只算产物 SHA256
.\tools\scan.ps1 -Offline     # 不联网，用内置漏洞表兜底
.\tools\scan.ps1 -Json        # 机器可读输出
```

退出码：`0` 无高危 / `1` 有高危 / `2` 脚本自身出错（找不到工程）。

**建议节奏**：每次归档前跑一次；拉了上游代码后必跑 `code` 段。

---

## 二、依赖漏洞（deps）

### 为什么不用 `dotnet list package --vulnerable`

本工程是**老式非 SDK 工程**（`ToolsVersion="15.0"` + MSBuild 命名空间），依赖是**手工 dll 引用**（`<Reference Include="..."><HintPath>Resources\xxx.dll</HintPath>`），既没有 `PackageReference` 也没有 `packages.config`。那条命令对它**完全不输出内容**。

### 实际做法

1. 解析 vbproj 的 `<Reference Include="Name, Version=x.y.z.0, ...">`，提取包名与版本（程序集版本去掉多余尾零 → NuGet 版本）。
2. 过滤系统程序集（`System.*`、`Microsoft.*`、`mscorlib`、`PresentationFramework` 等）与本解决方案项目（`MeloongCore`、`PCLCS`）。
3. 逐个查 **OSV**（`POST https://api.osv.dev/v1/query`，`ecosystem: NuGet`，免密钥、免注册）。
4. 断网或查询失败时，按**包**降级到内置表（不是全局降级，否则会重复计数——踩过一次）。

当前识别出的依赖（8 个）：

| 组件 | 版本 | 状态 |
|---|---|---|
| CacheCow.Client / .FileStore / .Common | 2.13.1 | 未发现漏洞 |
| ThrottleDebounce | 2.0.2 | 未发现漏洞 |
| Imazen.WebP | 10.0.1 | 未发现漏洞 |
| NAudio | 1.7.3 | 未发现漏洞（**版本很老，建议关注**） |
| **Newtonsoft.Json** | **13.0.3**（程序集版本 13.0.0.0） | ~~GHSA-5crp-9r3c-p9vr（High）~~ **误报已排除**：程序集版本被上游永久钉在 13.0.0.0，实际 dll 是 13.0.3（文件版本 13.0.3.27908，与 NuGet 13.0.3 包逐字节相同） |
| Ookii.Dialogs.Wpf | 1.0.0 | 未发现漏洞 |

### 待处理

- [x] ~~把 `Resources\Newtonsoft.Json.dll` 换成 13.0.3~~：2026-10-29 核实**本来就是 13.0.3**，无需升级；`Reference` 的 13.0.0.0 是程序集版本，不能改（改了运行时会绑定失败）。已改 `scan.ps1`：Resources 下有同名 dll 时以文件版本作为 NuGet 版本查询。
- [ ] `[待验证]` NAudio 1.7.3 已十多年未更新，OSV 无记录不代表安全，仅在用到音频播放时才相关。

---

## 三、敏感信息落盘（code）

参考 **PCL CE 的 CVE-2025-54120**（CVSS 9.3）：第三方登录凭据被写进本地日志，用户把日志发给别人排错时等于公开令牌。本项目做了同类检查。

| 规则 | 严重度 | 查什么 |
|---|---|---|
| SEC-001 | High | 日志里**拼接**了 `AccessToken` / `ClientToken` / `RefreshToken` / `CredentialCfKey` / `CredentialMsId` 等变量（纯文字提及不算） |
| SEC-002 | High | `Headers.Add("x-api-key", "字面量")` —— 密钥硬编码 |
| SEC-003 | Critical | `ServerCertificateValidationCallback` 无条件返回 True |
| SEC-004 | ~~Medium~~ 已处理 | 默认不再启用 `Ssl3` / `Tls`(1.0)；旧协议改为默认关闭的开关（2026-10-29） |
| SEC-005 | High | `OAuthClientId` / `CurseForgeAPIKey` 被赋了非空字面量（`ModSecret` 应保持空壳） |

### 2026-10-27 实测发现并修复

**SEC-001 命中真问题**：`ModLaunch.vb` 原本会打

```vb
McLaunchLog("AccessToken：" & McLoginLoader.Output.AccessToken)
McLaunchLog("ClientToken：" & McLoginLoader.Output.ClientToken)
```

上游有个 `FilterAccessToken` 兜底，但它：① 只给 AccessToken 保留**前 5 后 5 位**；② **完全不处理 ClientToken**。所以 ClientToken 是完整明文。

已改为 `MaskTokenForLog()`，日志里只出现 `已隐藏（N 字符）`——足够判断"有没有取到令牌"，但无法被利用。

**SEC-004 已处理（2026-10-29）**：不再默认启用弱协议。`Application.xaml.vb` 改为调 `ModCustom.ApplySecurityProtocol()`：默认只用系统安全协议（Win7 回退也只给 Tls11/Tls12），**Ssl3 任何情况都不启用**；旧协议改成 设置 → 其他 → 下载源 →「允许旧版 TLS」开关（默认关闭，开启时弹窗说明只对老镜像站有用、会降低安全性）。日志会打印 `传输协议：仅默认安全协议 / 已允许旧版 TLS（1.0/1.1）` 便于排查。

### 规则维护注意

- 只报"实际把变量拼进日志"的行。第一版规则写成"日志里出现 AccessToken 字样"，把 `"无需登录，AccessToken 尚未过期"` 这种说明性文字误报成高危，噪音盖过信号。
- `custom\` 下的文件会被 `apply-custom` 覆盖到 `build\` 树，同一处问题会命中两次；脚本按「去掉 build/custom 前缀的相对路径 + 行号 + 规则」去重。

---

## 四、静态分析器（Roslyn）——不适用，已放弃

`<AnalysisLevel>latest-recommended</AnalysisLevel>` 是 **SDK 风格工程**的属性，本项目是 `ToolsVersion=15.0` 的老式工程 + net48，该属性不生效；要启用只能手工挂 `Analyzer` 引用并在 vbproj 里逐个 `<Analyzer Include=...>`，对 VB 的规则覆盖本来就弱（CA 系列不少规则只支持 C#）。

结论：**不值得**。改为第三节的正则规则 + 第二节的依赖扫描，成本低、直接命中本项目实际问题。

---

## 五、二进制完整性

`scan.ps1 -Only hash` 输出 exe 的 SHA-256 与字节数，用于区分"我编的"和"别人给的"。

- 当前基线 `2026.10.28-custom.1`：5,958,144 字节，SHA-256 `affe62a3…4252`（2026-10-28 15:02 编出）。
  上一个基线 `2026.10.27-custom.10` 同为 5,958,144 字节，SHA-256 `0dc31a3b…e3cc`——**源码相同、仅版本章不同**，
  所以字节数一致而哈希不同，属预期。
- `[已确认 2026-10-28]` 归档 `versions\<label>\build-info.txt` 现为**六字段**，含 `sha256`（归档目录内 exe 的哈希），
  `built` 为 ASCII ISO 8601。历史 14 个归档（`2026.10.26-custom.30` ~ `2026.10.27-custom.10`）已由
  `tools\backfill-build-info.ps1` 回填，校验只需 `sha256sum <exe>` 与文件内 `sha256=` 比对。
- `[待测试]` 归档自校验尚未纳入 `scan.ps1`：现在仍要手工比对，可考虑加 `-Only archive` 段遍历 `versions\` 全部重算。
- 拿到任何第三方 PCL 二进制（含 CE 分支）前先比对哈希，不一致即拒绝。

---

## 六、供应链与密钥

- 依赖只从 NuGet 官方源取；缓存 `gongtong\_tools\nuget` 视为可信基线，不手工塞包。
- 密钥两条路：界面填（`HKCU\Software\PCLDebug`，加密存储，**推荐**）或 `secrets.env` 编译期内嵌（备选）。
- 若目录将来 `git init`：`.gitignore` 必须包含 `secrets.env`、`PCL\`、`PCL-custom\.minecraft\`、`out\`、`_tools\`。

---

## 变更记录

| 日期 | 变更 |
|---|---|
| 2026-10-27 | 从"方案"变为"已实施"：`tools\scan.ps1`（依赖 OSV 查询 + 5 条敏感信息规则 + 产物哈希）；实测修掉 ClientToken 日志泄漏；Newtonsoft.Json 13.0.0 漏洞待决策；确认分析器方案不适用于本工程结构 |
| 2026-10-28 | 第五节更新：`build-info.txt` 升为六字段（`sha256` + ISO `built`），14 个历史归档已由 `tools\backfill-build-info.ps1` 回填；基线改为 `custom.10`（5,958,144 字节） |
