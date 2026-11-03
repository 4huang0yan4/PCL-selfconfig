# 05 格式兼容：字段级说明

状态：MCBBS v2 `[已确认]`（源码核实 + PCL 侧导入实测）；`.mrpack` 导出 `[已确认]`、导入未做；Prism 识别 `[待测试]`
最后更新：2026-10-28

> MCBBS v2 的字段表已于 2026-10-27 按 **HMCL 源码 + PCL 的 `InstallPackMCBBS`** 逐字核实，导出物经 PCL 侧安装验证通过，
> 不再是"凭经验推测"。仍属 `[待测试]` 的只有**跨启动器**那一环：HMCL / Prism 是否同样认这份包（T-07 / T-08），
> 以及 Forge / NeoForge 分支的 `addons` 是否正确（T-09）。
> 验证优先级不变：先导入（读别人的包），再导出（写给别人读）。导出错不报错，导入错立刻暴露。

---

## 一、MCBBS v2（国内主格式，R2 优先）

### 结构

```
<pack>.zip
├─ mcbbs.packmeta          整合包元数据（HMCL 定义的格式）
├─ manifest.json           CurseForge 格式清单（关键：让 MultiMC/Prism 也能导入）
├─ overrides/              实际文件（mods、config、resourcepacks…）
└─ （可选）HMCL 的特有文件
```

**"双清单"是核心**：只写 `mcbbs.packmeta` 就只有 HMCL 能读；同时带 `manifest.json`，MultiMC 血脉（Prism、ATLauncher 等）也能导入。这就是"不发明格式"的具体落地方式。

### `mcbbs.packmeta`（`[已核实 2026-10-27]` 权威字段表）

来源：HMCL 主线源码 `McbbsModpackManifest.java`（字段定义）+ **PCL 自己的 `InstallPackMCBBS`**（消费视角）。以下字段名逐字来自代码，不是推测。

```json
{
  "manifestType": "minecraftModpack",
  "manifestVersion": 1,
  "name": "整合包名称",
  "version": "1.0",
  "author": "作者",
  "description": "描述",
  "url": "",
  "forceUpdate": false,
  "fileApi": "",
  "origin": [ { "type": "curseforge", "id": 123456 } ],
  "addons": [
    { "id": "game",  "version": "1.20.1" },
    { "id": "forge", "version": "47.4.28" }
  ],
  "libraries": [],
  "files": [
    { "path": "mods/xxx.jar", "hash": "sha1…", "force": true },
    { "projectID": 238222, "fileID": 4567890, "fileName": "xxx.jar", "url": "https://…", "force": true }
  ],
  "settings": { "install_mods": true, "install_resourcepack": true },
  "launchInfo": { "minMemory": 512, "supportJava": [17], "launchArgument": [], "javaArgument": [] }
}
```

**字段要点（逐条都有代码依据）**

| 字段 | 说明 |
|---|---|
| `addons[].id` | **小写短名**，不是 `net.minecraftforge` 那种 patchId。PCL 的 `InstallPackMCBBS` 只认：`game` / `forge` / `neoforge` / `fabric` / `quilt` / `optifine`。缺 `game` 直接抛"未提供游戏版本信息"；带 `quilt` 直接拒绝安装 |
| `addons[].version` | 加载器版本号（`forge` → `47.4.28`，`fabric` → `0.20.1`），`game` → 标准版本号 |
| `files[]` | **多态**：`AddonFile`（`path` + `hash`(SHA-1) + `force`）或 `CurseFile`（`projectID` + `fileID` + `fileName` + `url` + `force`） |
| `force` | 自动更新时是否强制覆盖用户改过的文件 |
| `settings.install_mods` / `install_resourcepack` | 安装时是否安装模组 / 资源包 |
| `launchInfo.javaArgument` | PCL 写入 `VersionAdvanceJvm`（JVM 参数） |
| `launchInfo.launchArgument` | PCL 写入 `VersionAdvanceGame`（游戏参数） |
| `launchInfo.supportJava` | 支持的 Java 主版本列表 |
| `fileApi` | 远程自动更新源；不用更新就留空 |

**最关键的一条（决定包怎么打包）**：

PCL 的 `InstallPackMCBBS` 只做两件事——解压到临时目录后 **`CopyOverrideDirectory(.../overrides, VersionFolder)`**，然后按 `addons` 重建版本。**它不按 `files[]` 列表去下载模组**。

由此推出：**若目标包含"PCL 自己能导入"，模组文件必须实体放进 `overrides/`**，不能只在 `files[]` 里给 projectID/fileID。这也是 PCL 导出选项里 `DontCheckHostedAssets`（"把 Mod 直接放入整合包中"）的意义所在。代价是包变大。

HMCL 侧则两种都认（有 `CurseFile` 就会去 CF 下载），所以"实体入包"对两边都安全。

> 旧版本本文档里写的 `defaultMemory` / `javaVersion` / `launchArguments` **全是错的**，已按源码纠正。

**实测确认（2026-10-27）**：本分支导出的样本 `evidence\sample-26.3-fabric.mcbbs.zip` 实际写入值为

```json
"addons": [ {"id":"game","version":"26.3"}, {"id":"fabric","version":"0.20.1"} ]
"manifest.json": { "minecraft": { "version":"26.3", "modLoaders":[{"id":"fabric-0.20.1","primary":true}] } }
```

包根同时含 `mcbbs.packmeta`、`manifest.json`、`modrinth.index.json`（三种元数据共存，PCL 判断优先级为 mcbbs > 其它，实测无冲突）。PCL 导入成功。

### 一个必须避开的坑：`manifest.json` 的版本号

HMCL issue #2268 记录过：`manifest.json` 里 `minecraft.version` 写成**显示名**（`"1.20 Release Candidate 1"`）会导致下游启动器找不到版本而安装失败，必须写**标准版本号**（`"1.20-rc1"`）。

对我们的意义：PCL 里 `Version.VanillaName`（标准号）与 `VersionDisplayName`（显示名）是两个字段，**生成清单只能用 `VanillaName`**。现有 `.mrpack` 生成代码用的正是 `VanillaName`，照抄即可。

### `manifest.json`（CurseForge 规范，字段名可信度高）

```json
{
  "manifestType": "minecraftModpack",
  "manifestVersion": 1,
  "name": "整合包名称",
  "version": "1.0",
  "author": "作者",
  "minecraft": {
    "version": "1.20.1",
    "modLoaders": [ { "id": "forge-47.4.28", "primary": true } ]
  },
  "files": [
    { "projectID": 238222, "fileID": 4567890, "required": true }
  ],
  "overrides": "overrides"
}
```

加载器 `id` 前缀：`forge-`、`fabric-`、`neoforge-`、`quilt-`。`primary: true` 标主加载器。

### 实现注意

- `files` 里的 `projectID`/`fileID` 是 CurseForge 的数字 ID，**只有从 CF 下载的模组才有**。本地整合包里的自编译/手改模组只能塞进 `overrides/`，不能进 `files`。
- 中文文件名与编码：`forceEncoding` 只管文本类文件，zip 内路径建议全 ASCII。
- **[已确认 2026-10-27]** PCL 分支现有的"导出整合包"有两种输出，都在 `Pages\PageInstance\PageInstanceExport.xaml.vb`：
  - **PCL 自有格式**：`.zip`，内含 `PCL\Setup.ini` 与若干导出配置项（`IncludeJava` / `IncludeLauncher` / `IncludeLauncherCustom` / `DontCheckHostedAssets` / `ModrinthUploadMode` / `PackPath`），可选把 PCL 本体一起打包。
  - **Modrinth 格式**：`IncludeLauncher=False` 时扩展名即 `.mrpack`，已生成 `modrinth.index.json`（字段与本文第二节完全一致），并用 `FileUtils.CreateZipFromDirectory` 打包。
  - **没有** MCBBS v2 支持：全库搜不到 `packmeta`，也没有 CurseForge 的 `manifest.json` 生成。→ 这正是 R2 要补的。
- 参考实现价值：`modrinth.index.json` 的生成代码（约 690–705 行）可直接照抄结构，改字段即可产出 `manifest.json`。

---

## 二、Modrinth `.mrpack` / 三、Prism 实例

> 已移到 `05b-formats-mrpack-prism.md`（含结构、`modrinth.index.json` 字段、`mmc-pack.json`、对本项目的意义）。

## 核实方法与当前进度

**已完成（2026-10-27）**

1. `[已确认]` MCBBS v2 字段表按 **HMCL 源码**（`McbbsModpackManifest.java`）与 **PCL 的 `InstallPackMCBBS`** 逐字核实，无需导包实测。
2. `[已确认]` 导出的 MCBBS v2 包经 **PCL 侧安装成功**，样本 `evidence\sample-26.3-fabric.mcbbs.zip`。

**仍待实测（集在 `08` 第二节）**

- `[待测试]` **T-07**：用 HMCL 导入同一份样本包，确认它能识别为 MCBBS 包。
- `[待测试]` **T-08**：用 Prism 导入，确认被识别为实例。
- `[待测试]` **T-09**：建一个 Forge / NeoForge 实例导出，检查 `addons` 是否为 `forge` + 版本号（目前只跑过 Fabric）。
- `[待测试]` `.mrpack` 导入方向未做，若要落地需先解包比对 `modrinth.index.json` 再写代码。

**不要在没做上面几项的情况下写新的导出分支。**

---

## 变更记录

| 日期 | 变更 |
|---|---|
| 2026-10-27 | 建立；三种格式的结构与字段；标注逐项待核实；给出四步核实法 |
| 2026-10-28 | 状态对齐：MCBBS v2 字段已按源码核实并 PCL 侧实测通过，取消"未经解包实测"的旧警示；剩余项收敛为跨启动器三条（T-07/08/09）与 `.mrpack` 导入未做 |
