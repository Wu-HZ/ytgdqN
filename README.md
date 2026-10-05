<!-- markdownlint-disable no-inline-html no-alt-text -->

# <img src="Image/rain.ico" width="45" aligh="left"> 雨天跟打器 (.NET 8)

<p align="left">
  <a href="https://github.com/Wu-HZ/ytgdqN/releases/latest">
    <img src="https://img.shields.io/github/v/release/Wu-HZ/ytgdqN.svg?color=orange" alt="Release" />
  </a>
  <a href="./LICENSE.txt">
    <img src="https://img.shields.io/github/license/Wu-HZ/ytgdqN" alt="Apache License 2.0" />
  </a>
</p>

> **Note**: 本分支所有改动由Claude完成（仅这句话除外）

> **Note**: This is a .NET 8 fork of [LightAPIs/ytgdq](https://github.com/LightAPIs/ytgdq)

## 简介

雨天跟打器是一款免费开源的便携打字练习软件。本分支将原项目从 .NET Framework 4.0 迁移到 .NET 8，以获得更好的性能和现代化支持。

## 运行要求

- .NET 8.0 Desktop Runtime ([下载](https://dotnet.microsoft.com/download/dotnet/8.0))

## 使用说明

[Wiki](https://github.com/LightAPIs/ytgdq/wiki)

## Legado WebDAV 电子书跟打（实验性）

在 Legado 中开启 WebDAV 阅读进度同步，并确保同一份 TXT 已上传至 WebDAV 的 `books/` 目录；目前可尝试 UTF-8、带 BOM 的 UTF-16 与 GB18030/GBK 编码；自动匹配 Legado 默认启用的 12 条目录规则，其他内置正则规则可在书目页手动选择。无目录时可选择「默认分章规则」，手动输入 Legado 当前的无目录分章字数以预览字节分块与章节（每次预览都需明确输入）；该参数不在云端进度中，且 Legado 存在跨块边界差异，因此此模式**不会读取或上传进度，也不能用于跟打发文**。打开跟打器的「发文... → Legado 电子书」页，点击「WebDAV 设置…」在次级窗口输入与 Legado 相同的地址、同步目录、账号和密码；保存后及以后每次打开发文窗口首次进入「Legado 电子书」页时，都会自动扫描 `books/` 下的 TXT 和 EPUB（同一窗口内不反复自动扫描）；列表行显示缓存状态。双击 TXT 可读取进度；选择 EPUB 可预览正文和目录、只读查看云端进度，并可选择章节跟打。首次读取 TXT 时下载并缓存；之后命中本地缓存时只拉取云端进度。需要获取云端新版时点击“更新缓存”。TXT 确认云端章节后，使用发文窗口现有的每段字数设置开始跟打；Ctrl+N 跳过当前段也会把 TXT 进度推进到该段末尾并尝试上传。密码用 Windows 当前用户的 DPAPI 加密后保存在配置文件；换电脑或 Windows 用户时需重新输入。使用 HTTP 时会先提示确认：Basic 账号密码只经过 Base64 编码，传输链路不加密。

打开 TXT 时从 `bookProgress/` 拉取云端书名、作者、章节索引及章内位置，只有目录和章节标题与 TXT 一致才开始跟打。打完一段后才上传进度；若阅读端进度更靠前、进度文件变化或服务器不支持 ETag 条件写入，则不覆盖云端。若退出时仍在电子书发文，重启后会用保存的书籍信息及加密凭据自动重连，并按云端最新章节位置重新发文（不会用本地未同步输入覆盖云端）；主动停止或切换发文则不自动恢复。HTTP 自动重连前仍需确认明文传输风险。不支持与 Legado 不同的 TXT 版本、没有现存云端进度的书籍，以及尚无法确认 Legado 无目录按字数分章长度的情况；章节标题、索引或章内位置不匹配时拒绝写回。

EPUB 可预览 EPUB2/EPUB3 导航、卷首项、无资源分卷和同一文件的锚点区间及跨 spine 文件的章节正文（单章最多预览 64 个文件，缺失资源会拒绝预览）；打开时只读校验云端书名、章节索引及标题并选中对应目录项。云端进度仅供查看；EPUB 可以跟打，但**永不写回云端阅读进度**。选择与云端进度对应的章节时，`durChapterPos` 仅用于估算预览文本中的起点，可能与 Legado 实际位置有偏差；跟打进度只在本次运行期间本地推进，不会上传。真实样本中 Legado 章内位置与预览字序号不同，因此不承诺精确定位。无目录 TXT 按字数分章即使提供了字数设置也保持禁用同步，直到能以真实 Legado 章节及位置样本对照。目前本地合成样本与模拟 WebDAV 协议检查通过；使用当前 WebDAV 配置已扫描到 TXT 9 本、EPUB 3 本：一份 TXT 的真实云端章节标题和位置范围与本地缓存匹配，并通过先加密备份、再以 ETag 条件原样写回进度 JSON、最后读回逐字节一致的测试（未推进阅读位置）；一份 EPUB 的真实章节索引与标题吻合，但云端章内位置与 Legado 正文处理后的坐标不一致。长期跟打和真实界面交互尚未验证。运行协议检查：`dotnet run --project tests/LegadoSyncContractTests/LegadoSyncContractTests.csproj`。

## .NET 8 迁移说明

本分支对原项目进行了以下现代化改造：

| 改动项 | 说明 |
|--------|------|
| 目标框架 | .NET Framework 4.0 → .NET 8.0 |
| 项目格式 | 传统 .csproj → SDK 风格 .csproj |
| 构建工具 | MSBuild (Visual Studio) → dotnet CLI |
| 快捷方式创建 | COM 组件 (IWshRuntimeLibrary) → P/Invoke (IShellLink) |
| TLS 支持 | 手动配置 → 原生支持 |
| DPI 支持 | 无 → PerMonitorV2 高清显示 |

### 构建方法

```bash
# 构建
dotnet build

# 发布 x64 版本
dotnet publish -c Release -r win-x64 --self-contained false

# 发布 x86 版本
dotnet publish -c Release -r win-x86 --self-contained false
```

## 下载地址

- [Releases](https://github.com/Wu-HZ/ytgdqN/releases)

## 致谢

- [LightAPIs/ytgdq](https://github.com/LightAPIs/ytgdq) - Fork自：雨天跟打器 ([Apache-2.0 License](https://github.com/LightAPIs/ytgdq/blob/main/LICENSE.txt))
- [taliove/tygdq](https://github.com/taliove/tygdq) - 源项目：添雨跟打器 ([Apache-2.0 License](https://github.com/taliove/tygdq/blob/master/LICENSE))

## 相关项目

- [LightAPIs/article-storage](https://github.com/LightAPIs/article-storage)

## 许可证

[Apache License 2.0](/LICENSE.txt)
