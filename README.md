# LiteMCLauncher

LiteMCLauncher 是一个面向 Windows 的轻量级 Minecraft 启动器，使用 WinUI 3 与 CmlLib.Core 构建。项目目标是把账号、实例、Java、下载、Mod、音乐和新闻等常用能力集中在一个简洁的桌面界面中。

> **AI 声明**
>
> 本项目是 AI 辅助开发项目。项目的大量 C# / XAML 代码、界面文案、调试过程和文档由 **DeepSeek** 参与完成。所有需求、产品取舍、最终代码审查和本地测试由项目发起人负责。使用或二次开发本项目时，请同时遵守相关依赖与 Minecraft 官方的许可和使用条款。

## 功能

- Microsoft 账号设备码登录、离线账号、多账号切换
- RefreshToken 持久化与启动时静默刷新
- 多实例管理，实例独立游戏目录
- 原版、Fabric、Forge、NeoForge、Quilt 安装
- 实例图标：原版草方块、加载器图标；下载页按版本类型显示草方块 / 命令方块 / 石头
- 版本下载、资源完整性与 Java 版本启动前检查
- Java 8 / 17 / 21 / 25 一键安装，支持清华 TUNA 镜像与 Adoptium 官方源
- 全局 Java 自动检测；按游戏版本自动选择 Java 8 / 17 / 21+
- 实例特定游戏设置：Java、内存、JVM 参数、游戏语言
- 资源管理：世界、资源包、光影包、Mods、日志
- 最近一次日志查看，WARN / ERROR / `at ...` 堆栈着色
- 本地音乐播放：播放列表、专辑封面、循环模式、启动器/游戏启动时自动播放
- Mojang 官方新闻与 Java 更新，支持在启动器内阅读完整正文和图片
- 启动器界面语言：简体中文 / English

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

## 许可证

本项目使用 [MIT License](LICENSE) 开源，Copyright (c) 2026 Lite-Galaxy3309。

## 参与者

- **Lite-Galaxy3309 / Galaxy3309** — 项目发起人、需求定义、产品设计与测试
- **DeepSeek** — AI 编程助手，参与架构设计、C# / WinUI 3 实现、调试与文档编写

## 免责声明

本项目与 Mojang Studios、Microsoft 没有隶属关系。Minecraft 是 Mojang Studios 的商标。本项目不包含 Minecraft 游戏本体、资源文件或账号凭据，使用前请自行购买并遵守 Minecraft 最终用户许可协议。
