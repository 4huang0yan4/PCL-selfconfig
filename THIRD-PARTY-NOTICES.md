# 第三方组件与许可

本仓库包含或依赖以下第三方组件。各组件的著作权归其作者所有，使用时遵循其各自的许可条款。

## 一、主要来源

| 组件 | 许可 | 说明 |
|---|---|---|
| **Plain Craft Launcher (PCL)** | 《PCL 分发有限许可》+《PCL 存储库合理使用指南》 | 本分支的上游，完整原文见仓库根目录 `LICENCE` |

## 二、随程序分发的库（`Resources\`）

| 组件 | 许可 | 用途 |
|---|---|---|
| Newtonsoft.Json | MIT | JSON 序列化 |
| NAudio | MIT | 音频播放 |
| CacheCow.Client / Common / Client.FileStore | MIT | HTTP 缓存 |
| Imazen.WebP | MIT | WebP 编解码 |
| `libwebp64.dll` | BSD（Google WebP） | WebP 原生库 |
| Ookii.Dialogs.Wpf | BSD | 系统对话框 |
| ThrottleDebounce | MIT | 调用节流 |
| System.Net.Http.Formatting | MS-PL | HTTP 格式化 |

以上组件均按原样分发，未作修改。MIT / BSD / MS-PL 均要求保留原始版权声明，
若需完整许可证文本，请参阅各组件在其官方仓库中的 `LICENSE` 文件。

## 三、运行时不随包分发的组件

| 组件 | 许可 | 说明 |
|---|---|---|
| **Terracotta（陶瓦联机）** | **AGPL-3.0** | 联机能力提供方，<https://github.com/burningtnt/Terracotta> |
| EasyTier | LGPL-3.0 | Terracotta 内部使用的组网组件 |

**这两个组件不由本仓库分发**：启动器在用户需要联机时按提示获取，用户自行下载、自行接受其许可。
若将来要随包分发 Terracotta 二进制，**必须**同时提供其完整源码与 AGPL-3.0 许可证文本，否则构成许可违规。

## 四、不包含的内容

- Minecraft 游戏本体、素材、音效或任何 Mojang / Microsoft 的产权内容
- 任何商业授权或付费内容
- 任何账号凭证、API 密钥

---

最后更新：2026-10-30
