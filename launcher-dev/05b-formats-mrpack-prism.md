# 05b Modrinth .mrpack 与 Prism 实例格式

状态：2026-10-30 从 `05-formats.md` 拆出，内容原样保留。**主文件**：`05-formats.md`（MCBBS v2）。

---

## 二、Modrinth `.mrpack`（国际补充，R4）

### 结构

```
<pack>.mrpack              本质是 zip
├─ modrinth.index.json
└─ overrides/               可选，客户端/服务端各自的覆盖文件
```

### `modrinth.index.json`

```json
{
  "formatVersion": 1,
  "game": "minecraft",
  "versionId": "1.0.0",
  "name": "整合包名称",
  "summary": "一句话描述",
  "files": [
    {
      "path": "mods/example.jar",
      "hashes": { "sha1": "…", "sha512": "…" },
      "env": { "client": "required", "server": "required" },
      "downloads": ["https://cdn.modrinth.com/data/…"],
      "fileSize": 123456
    }
  ],
  "dependencies": {
    "minecraft": "1.20.1",
    "forge": "47.4.28"
  }
}
```

要点：
- `hashes` 必须与 `downloads` 内容一致，导入方会校验；算错即装不上。
- `env` 决定该文件在客户端/服务端是否安装，可选值 `required` / `optional` / `unsupported`。
- `dependencies` 的键是加载器名（`forge` / `fabric-loader` / `quilt-loader` / `neoforge`），不是完整的 `forge-47.4.28`。
- 与 MCBBS v2 的根本差异：`.mrpack` **只放清单与下载链接**，不打包 jar；`overrides/` 只放清单外的文件。所以导出时"哪些进 files、哪些进 overrides"的划分是关键决策。

---

## 三、Prism / MultiMC 实例（导入方向）

### 目录

```
<instance>\
├─ instance.cfg            INI 风格：name、InstanceType、内存等
├─ mmc-pack.json           组件清单
└─ .minecraft\             游戏目录（mods/saves/config…）
```

### `mmc-pack.json`

```json
{
  "formatVersion": 1,
  "components": [
    { "uid": "net.minecraft", "version": "1.20.1", "important": true },
    { "uid": "net.minecraftforge", "version": "47.4.28" }
  ]
}
```

`instance.cfg` 关键键：`name`、`InstanceType=OneSix`、`OverrideMemory`、`OverrideJavaArgs`、`OverrideJavaLocation`。

### 对项目的意义

- **只做导入，不做导出**（P1=C 阶段）。导入能覆盖"从 Prism 迁到自用启动器"这一真实需求。
- 映射关系：`components` → 版本与加载器；`.minecraft\` → 实例目录；`instance.cfg` 的内存/Java 覆盖 → 实例级设置。这三项正好是 P3 里 `IInstanceView` 要抽象的东西。

---

