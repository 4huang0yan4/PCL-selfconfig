# help-src —— 自定义帮助的源文件

这里是「帮助 → 指南」里那 5 篇自己写的条目的**可编辑源**。它们不直接生效，只是打包素材。

## 为什么要有这个目录

PCL 的帮助有两条来源：

| 来源 | 位置 | 跟着 exe 走吗 |
|---|---|---|
| 内置 | `Resources\Help.zip`，由 `My Project\Resources.resx` 的 `ResXFileRef` 嵌进 exe，启动时解压到临时目录 | ✅ 会 |
| 外部 | `<exe 同级>\PCL\Help\**.json` | ❌ **不会** |

原先这 5 篇放在外部目录，所以**单独把 exe 拷到别处，指南就空了**（2026-10-30 的实际问题）。
现在改为内置：内容放这里，用 `tools\help-pack.py` 推进 `Help.zip`。

> **别再把文件放回 `PCL\Help\`**。外部与内置同名时不会覆盖，会**并列出现两条**。

## 加一篇新指南

1. 按现有格式写 `指南\新标题.json` + `指南\新标题.xaml` 两个文件：

```json
{
	"__Author__": "PCL-拾光启动器",
	"Title": "新标题",
	"Description": "一句话说明",
	"Types": ["指南"],
	"Keywords": "空格分隔的关键词",
	"ShowInPublic": true,
	"ShowInSnapshot": true
}
```

2. 打包（在 `pcl-src\` 下）：

```
py -3 tools\help-pack.py
```

3. 重新编译：`build.cmd -NoRestore`

## 改已有文案

直接改这里的 `.xaml`，然后重复上面第 2、3 步。

只想先看效果、不想编译：把 json+xaml 临时放进 `<exe 同级>\PCL\Help\指南\`，
**但先确认 zip 里没有同名条目**，否则列表会出现两条。看完记得删掉外部那份。

## 命令

```
py -3 tools\help-pack.py --check          # 只报告差异，不写文件
py -3 tools\help-pack.py                  # 打包（自动备份到 _deprecated\help-zip\）
py -3 tools\help-pack.py --zip <路径> --src <目录>
```

只处理 `.json` 与 `.xaml`；同名条目会被覆盖。脚本会整体重建 zip，不会产生重名条目。

## 当前包含

| 标题 | 作用 |
|---|---|
| CurseForge 密钥 | 为什么要自带 CF API Key、不填会走镜像、怎么申请 |
| 原版游戏下载 | 本分支解除了下载限制，怎么下原版 |
| 微软正版登录 | 登录步骤与常见失败 |
| 模组加载器与下载源 | Forge/Fabric/NeoForge 与下载源选择 |
| 联机使用方法 | 樱花穿透 + 陶瓦的完整流程 |
