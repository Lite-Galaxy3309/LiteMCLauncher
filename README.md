# LiteMCLauncher

LiteMCLauncher 是一个面向 Windows 的轻量级 Minecraft 启动器，使用 WinUI 3 与 CmlLib.Core 构建。项目目标是把账号、实例、Java、下载、Mod、音乐和新闻等常用能力集中在一个简洁的桌面界面中。

> **AI 声明**
>
> 本项目是 AI 辅助开发项目。项目的大量 C# / XAML 代码、界面文案、调试过程和文档由 **DeepSeek** 参与完成。所有需求、产品取舍、最终代码审查和本地测试由项目发起人负责。使用或二次开发本项目时，请同时遵守相关依赖与 Minecraft 官方的许可和使用条款。

## 功能

- Microsoft 账号设备码登录、离线账号、多账号切换
- RefreshToken 持久化与启动时静默刷新
- 多实例管理，实例独立游戏目录；支持重命名、打开目录和删除实例（含世界/资源包/Mods 等实例数据）
- 原版、Fabric、Forge、NeoForge 安装
- 实例图标：原版草方块、加载器图标；下载页按版本类型显示草方块 / 命令方块 / 石头
- 版本下载、资源完整性与 Java 版本启动前检查
- Java 下载运行时启动前会执行 `java -version` 校验，并使用真实的 `java.exe` 路径启动游戏
- Java 8 / 17 / 21 / 25 一键安装，统一安装到 `.minecraft/java/OpenJDK-版本号`，支持清华 TUNA 镜像与 Adoptium 官方源
- 全局 Java 自动检测；按游戏版本自动选择 Java 8 / 17 / 21+
- 实例特定游戏设置：Java、内存、JVM 参数、游戏语言
- 资源管理：世界、资源包、光影包、Mods、日志
- 日志文件选择：查看实例全部日志文件；INFO 绿色、WARN 黄色、ERROR 红色、`at ...` 堆栈蓝色
- 本地音乐播放：播放列表、专辑封面、循环模式、启动器/游戏启动时自动播放
- 在线音乐：独立搜索页与播放器页，支持上一首/暂停/下一首、单曲循环/列表循环/随机播放、可删除的历史播放；可填写 MUSIC_U Cookie 登录自己的账号
- Mojang 官方新闻与 Java 更新，支持在启动器内阅读完整正文和图片
- 启动器界面语言：简体中文 / English（覆盖设置、关于、实例、下载、音乐、新闻、Mod 等静态界面）

## 技术栈

- .NET 10
- WinUI 3 / Windows App SDK 2.7
- CmlLib.Core 4.1
- CmlLib.Core.Auth.Microsoft / XboxAuthNet
- Microsoft.Identity.Client (MSAL.NET)
- System.Text.Json

## 环境要求

- Windows 10 1809 或更高版本（推荐 Windows 11）
- [.NET 10 SDK](https://dotnet.microsoft.com/)
- Visual Studio 2022/2026（可选，用于 XAML 设计器）
- Windows App SDK 运行时

## 构建

```powershell
git clone https://github.com/Lite-Galaxy3309/LiteMCLauncher.git
cd LiteMCLauncher
dotnet build .\LiteMCLauncher.csproj
```

## 运行

```powershell
dotnet run --project .\LiteMCLauncher.csproj --launch-profile "LiteMCLauncher (Unpackaged)"
```

单文件发布可参考 `tools/LiteMCLauncher.Bootstrapper`。仓库不提交约 140 MB 的 `dist/LiteMCLauncher.exe`，请从源码构建，或在 Releases 中获取发布包。

## 数据目录

启动器的 Minecraft 数据默认位于：

```text
%APPDATA%\.minecraft
```

账号、实例设置、音乐和日志等启动器数据会写入对应的应用数据目录。请不要把个人账号文件提交到仓库。

## 鸣谢与社区项目

LiteMCLauncher 的实现离不开以下社区项目、平台和文档：

| 项目 | 用途 | 地址 |
|---|---|---|
| CmlLib.Core | Minecraft 版本安装、启动参数与进程构建 | https://github.com/CmlLib/CmlLib.Core |
| CmlLib.Core.Auth.Microsoft | Microsoft / Xbox / Minecraft 登录链路 | https://github.com/CmlLib/CmlLib.Core.Auth.Microsoft |
| XboxAuthNet | Xbox Live 认证辅助库 | https://github.com/CmlLib/XboxAuthNet |
| Windows App SDK / WinUI 3 | Windows 桌面 UI 与窗口能力 | https://github.com/microsoft/WindowsAppSDK |
| Microsoft.Identity.Client (MSAL.NET) | 设备码登录、Token 缓存与静默刷新 | https://github.com/AzureAD/microsoft-authentication-library-for-dotnet |
| Fabric Meta / Fabric Loader | Fabric 游戏版本与加载器版本元数据 | https://meta.fabricmc.net |
| MinecraftForge | Forge 构建元数据与安装器 | https://github.com/MinecraftForge/MinecraftForge |
| NeoForged / NeoForge | NeoForge 构建元数据与安装器 | https://github.com/neoforged/NeoForge |
| Modrinth API | Mod 搜索、项目详情与下载信息 | https://docs.modrinth.com |
| Netease_url | 网易云音乐搜索、播放地址与 Cookie 请求头的实现参考（MIT） | https://github.com/Suxiaoqinx/Netease_url |
| 网易云音乐 | 在线音乐搜索与播放服务 | https://music.163.com |
| BMCLAPI | BMCLAPI 镜像服务；仅在源设置为 BMCLAPI 时使用 | https://bmclapidoc.bangbang93.com |
| Mojang Launcher Content | Minecraft 官方新闻与 Java 更新内容 | https://launchercontent.mojang.com |

## 许可证

本项目使用 [MIT License](LICENSE) 开源，Copyright (c) 2026 Lite-Galaxy3309。

## 参与者

- **Lite-Galaxy3309 / Galaxy3309** — 项目发起人、需求定义、产品设计与测试
- **DeepSeek** — AI 编程助手，参与架构设计、C# / WinUI 3 实现、调试与文档编写

## 免责声明

本项目与 Mojang Studios、Microsoft 没有隶属关系。Minecraft 是 Mojang Studios 的商标。本项目不包含 Minecraft 游戏本体、资源文件或账号凭据，使用前请自行购买并遵守 Minecraft 最终用户许可协议。
