using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System.Runtime.CompilerServices;

namespace LiteMCLauncher.Services;

/// <summary>
/// Small runtime UI translator for the launcher shell. The launcher ships with
/// Simplified Chinese text as the source language and applies English strings
/// by walking the visual tree after navigation.
/// </summary>
public static class LauncherLanguageService
{
    public const string Chinese = "zh-CN";
    public const string English = "en-US";

    private static readonly Dictionary<string, string> ChineseToEnglish = new(StringComparer.Ordinal)
    {
        // Shell
        ["主页"] = "Home",
        ["实例管理"] = "Instances",
        ["版本下载"] = "Downloads",
        ["音乐"] = "Music",
        ["本地音乐"] = "Local music",
        ["在线音乐"] = "Online music",
        ["新闻"] = "News",
        ["设置"] = "Settings",
        ["关于"] = "About",
        ["返回"] = "Back",
        ["刷新"] = "Refresh",
        ["刷新列表"] = "Refresh",
        ["保存设置"] = "Save settings",
        ["打开"] = "Open",
        ["关闭"] = "Close",
        ["取消"] = "Cancel",
        ["确定"] = "OK",
        ["知道了"] = "OK",
        ["移除"] = "Remove",
        ["安装"] = "Install",
        ["重新安装"] = "Reinstall",
        ["已安装"] = "Installed",
        ["启动"] = "Launch",
        ["重命名"] = "Rename",
        ["管理"] = "Manage",
        ["编辑"] = "Edit",
        ["删除"] = "Delete",

        // Home
        ["欢迎回来，选择一个实例，继续你的 Minecraft 冒险。"] = "Welcome back. Choose an instance to continue your Minecraft adventure.",
        ["未选择账号"] = "No account selected",
        ["添加 Microsoft 账号或离线账号后即可启动游戏。"] = "Add a Microsoft or offline account to launch the game.",
        ["离线模式"] = "Offline mode",
        ["登录 Microsoft 账号"] = "Sign in with Microsoft",
        ["当前实例"] = "Current instance",
        ["启动器将使用下面选择的实例和配置。"] = "The launcher will use the instance and settings selected below.",
        ["实例"] = "Instance",
        ["管理实例"] = "Manage instances",
        ["快捷操作"] = "Quick actions",
        ["打开游戏目录"] = "Open game folder",
        ["查看运行日志"] = "View logs",
        ["安装 Mod"] = "Install mods",
        ["环境状态"] = "Environment",
        ["未添加账号"] = "No account added",
        ["上次检查：刚刚"] = "Last checked: just now",
        ["启动游戏"] = "Launch game",
        ["未登录"] = "Not signed in",
        ["未选择实例"] = "No instance selected",
        ["请选择实例"] = "Select an instance",
        ["选择一个实例"] = "Select an instance",
        ["当前没有可启动的实例。"] = "There are no instances to launch.",
        ["正在准备启动…"] = "Preparing to launch…",
        ["正在启动…"] = "Launching…",
        ["取消启动"] = "Cancel launch",
        ["已启动"] = "Launched",

        // Instances
        ["从 .minecraft/versions 读取本地已有的 Minecraft 实例。"] = "Reads local Minecraft instances from .minecraft/versions.",
        ["搜索实例名称、版本或加载器..."] = "Search by instance name, version or loader...",
        ["全部加载器"] = "All loaders",
        ["最近修改"] = "Recently modified",
        ["名称"] = "Name",
        ["更新时间"] = "Updated",
        ["打开实例目录"] = "Open instance folder",
        ["新建实例"] = "New instance",
        ["实例名称"] = "Instance name",
        ["实例设置"] = "Instance settings",
        ["资源管理"] = "Resources",
        ["启用实例特定游戏设置"] = "Enable instance-specific game settings",
        ["已启用"] = "Enabled",
        ["已关闭"] = "Disabled",
        ["当前实例跟随全局游戏设置。启用后，本页参数将覆盖全局设置。"] = "This instance uses the global game settings. Enable this to override them on this page.",
        ["此实例将使用本页的 Java、内存、JVM 参数和语言，不再跟随全局设置。"] = "This instance will use the Java, memory, JVM arguments and language on this page instead of the global settings.",
        ["游戏参数"] = "Game settings",
        ["最大内存（MB）"] = "Maximum memory (MB)",
        ["额外 JVM 参数"] = "Extra JVM arguments",
        ["游戏语言"] = "Game language",
        ["保存实例设置"] = "Save instance settings",
        ["重新检测"] = "Re-detect",
        ["选择 java.exe"] = "Select java.exe",
        ["自动检测（跟随游戏版本）"] = "Auto-detect (match game version)",
        ["实例游戏目录"] = "Instance game directory",
        ["世界"] = "Worlds",
        ["资源包"] = "Resource packs",
        ["光影包"] = "Shader packs",
        ["日志"] = "Logs",
        ["最近一次日志"] = "Latest log",
        ["刷新日志"] = "Refresh logs",
        ["尚未找到日志"] = "No log found yet",
        ["尚未找到 latest.log 或其他日志文件。启动一次游戏后即可查看。"] = "No latest.log or other log file was found. Launch the game once to view logs.",
        ["启用后，Java、内存、JVM 参数和游戏语言都以此页为准，不再读取全局游戏设置。"] = "When enabled, Java, memory, JVM arguments and game language on this page override the global game settings.",
        ["从 Mojang 官方源安装原版（Vanilla）游戏、资源与依赖库。"] = "Install Vanilla Minecraft, assets and libraries from Mojang's official source.",
        ["搜索版本号..."] = "Search versions...",
        ["全部版本"] = "All versions",
        ["正式版"] = "Release",
        ["快照"] = "Snapshot",
        ["旧版"] = "Legacy",
        ["原版"] = "Vanilla",
        ["模组加载器"] = "Mod loader",

        // Settings
        ["调整游戏运行参数、下载源与启动器外观。"] = "Adjust game options, download sources and launcher appearance.",
        ["启动器"] = "Launcher",
        ["切换启动器界面使用的语言，不会影响游戏内语言。"] = "Change the launcher interface language. This does not affect the in-game language.",
        ["界面语言"] = "Interface language",
        ["保存后立即切换，重启启动器后保持。"] = "Changes apply immediately after saving and persist after restart.",
        ["简体中文"] = "Simplified Chinese",
        ["游戏"] = "Game",
        ["这些设置将作为新实例的默认值。"] = "These settings are the defaults for new instances.",
        ["Java 路径"] = "Java path",
        ["留空时自动检测已安装的 Java。"] = "Leave empty to auto-detect installed Java.",
        ["浏览"] = "Browse",
        ["最大内存"] = "Maximum memory",
        ["建议设置为系统内存的 50% 左右。"] = "Around 50% of system memory is recommended.",
        ["仅影响游戏内显示的语言。"] = "Only affects the language shown inside the game.",
        ["多个参数之间使用空格分隔。"] = "Separate multiple arguments with spaces.",
        ["自动检测"] = "Auto-detect",
        ["Java 管理"] = "Java management",
        ["可切换清华 TUNA 镜像和 Adoptium 官方源；国内网络推荐镜像，挂代理时可选官方源。也可以直接使用本机已安装的 Java。"] = "Switch between the Tsinghua TUNA mirror and the official Adoptium source. The mirror is recommended in mainland China; use the official source when a proxy is available. Installed system Java can also be used.",
        ["Java 8"] = "Java 8",
        ["Java 17"] = "Java 17",
        ["Java 21"] = "Java 21",
        ["Java 25"] = "Java 25",
        ["Java 下载源"] = "Java download source",
        ["清华 TUNA 镜像"] = "Tsinghua TUNA mirror",
        ["Adoptium 官方源"] = "Official Adoptium source",
        ["重新检测 Java"] = "Re-detect Java",
        ["默认游戏 Java"] = "Default game Java",
        ["自动检测（推荐）"] = "Auto-detect (recommended)",
        ["打开 Java 目录"] = "Open Java folder",
        ["外观"] = "Appearance",
        ["主题"] = "Theme",
        ["跟随系统"] = "Follow system",
        ["浅色"] = "Light",
        ["深色"] = "Dark",
        ["启用透明效果"] = "Enable transparency",
        ["最小化启动器"] = "Minimize launcher",
        ["启动游戏时最小化启动器"] = "Minimize the launcher when the game starts",
        ["下载源"] = "Download source",
        ["Mojang 官方源"] = "Official Mojang source",
        ["BMCLAPI 镜像"] = "BMCLAPI mirror",

        // Music
        ["导入本地歌曲，播放列表与自动播放设置。"] = "Import local songs, manage playlists and autoplay.",
        ["导入歌曲"] = "Import songs",
        ["启动器启动时播放"] = "Play when the launcher starts",
        ["启动游戏时播放"] = "Play when the game starts",
        ["播放模式"] = "Playback mode",
        ["列表循环"] = "Repeat all",
        ["单曲循环"] = "Repeat one",
        ["随机播放"] = "Shuffle",
        ["音量"] = "Volume",
        ["上一首"] = "Previous",
        ["下一首"] = "Next",
        ["播放或暂停"] = "Play or pause",
        ["切换播放模式"] = "Change playback mode",
        ["还没有歌曲，点击“导入歌曲”添加本地音乐。"] = "No songs yet. Click “Import songs” to add local music.",

        // News
        ["来自 Mojang 官方内容源的近期更新与活动。"] = "Recent updates and events from Mojang's official content feeds.",
        ["刷新新闻"] = "Refresh news",
        ["查看全文"] = "Read full article",
        ["在浏览器打开"] = "Open in browser",
        ["返回新闻列表"] = "Back to news",
        ["正在读取 Mojang 新闻…"] = "Loading Mojang news…",
        ["Mojang 新闻"] = "Mojang news",
        ["Mojang 更新"] = "Mojang update",
        ["快照更新"] = "Snapshot update",
        ["Java 版更新"] = "Java Edition update",
        ["暂时没有读取到新闻，请检查网络后点击“刷新新闻”。"] = "No news could be loaded. Check your network and click “Refresh news”.",
        ["阅读原文"] = "Read original",
        ["文章加载失败"] = "Failed to load article",

        // News / dialogs
        ["正在检查游戏资源完整性并准备启动"] = "Checking game files and preparing to launch",
        ["没有可用的 Java 运行时。请先在“设置 > Java 管理”中安装 Java 8、17、21 或 25，或指定 Java 路径。"] = "No Java runtime is available. Install Java 8, 17, 21 or 25 in Settings > Java management, or specify a Java path.",
        ["自动检测：已为"] = "Auto-detect: selected",

        // Additional static UI strings
        ["BMCLAPI 镜像服务；仅在设置中切换为 BMCLAPI 源时使用。"] = "BMCLAPI mirror service; used only when the download source is set to BMCLAPI.",
        ["Cookie 只保存在本机启动器数据目录，用于网易云请求头；不会上传到 GitHub 或第三方。"] = "The Cookie is stored only in local launcher data and sent as a NetEase request header; it is never uploaded to GitHub or third parties.",
        ["Fabric 游戏版本与 Loader 版本元数据。"] = "Fabric game and loader version metadata.",
        ["Forge 构建元数据与安装器。"] = "Forge build metadata and installer.",
        ["LiteMCLauncher 是独立的第三方启动器，与 Mojang Studios、Microsoft 及 Modrinth 均无隶属或赞助关系。Minecraft 是 Mojang Studios 的商标。"] = "LiteMCLauncher is an independent third-party launcher and is not affiliated with or sponsored by Mojang Studios, Microsoft or Modrinth. Minecraft is a trademark of Mojang Studios.",
        ["Microsoft / Xbox / Minecraft 登录与账号会话链路。"] = "Microsoft / Xbox / Minecraft sign-in and account session chain.",
        ["Minecraft 官方新闻、Java 更新说明与启动器内容图片。"] = "Official Minecraft news, Java update notes and launcher content images.",
        ["Minecraft 版本安装、启动参数和进程构建的核心库。"] = "Core library for Minecraft installation, launch arguments and process building.",
        ["Mod 搜索、项目详情和下载信息。"] = "Mod search, project details and download information.",
        ["Mods 目录"] = "Mods folder",
        ["NeoForge 构建元数据与安装器。"] = "NeoForge build metadata and installer.",
        ["Netease_url（网易云音乐解析参考）"] = "Netease_url (NetEase Cloud Music reference)",
        ["Xbox Live 与 Minecraft 认证链路的辅助库。"] = "Helper library for the Xbox Live and Minecraft authentication chain.",
        ["一个轻量、现代的 Minecraft 启动器，专注于快速的实例管理与流畅的启动体验。"] = "A lightweight, modern Minecraft launcher focused on fast instance management and a smooth launch experience.",
        ["上一页"] = "Previous page",
        ["下一页"] = "Next page",
        ["下载并安装"] = "Download and install",
        ["个性化"] = "Personalization",
        ["主题色"] = "Accent color",
        ["了解 LiteMCLauncher 的版本与作者信息。"] = "Version and author information for LiteMCLauncher.",
        ["从 music.163.com 的 Network 请求头复制 Cookie；可留空"] = "Copy the Cookie from a Network request header on music.163.com; optional.",
        ["作者"] = "Author",
        ["例如：-XX:+UseG1GC"] = "For example: -XX:+UseG1GC",
        ["保存登录"] = "Save login",
        ["修改后点击右上角“保存设置”；Java、内存与 JVM 参数将在下次启动游戏时生效。"] = "After changes, click “Save settings” at the top right; Java, memory and JVM arguments apply on the next game launch.",
        ["像 Windows 个性化一样调整透明效果、主题色与背景。"] = "Adjust transparency, accent color and background like Windows personalization.",
        ["免责声明"] = "Disclaimer",
        ["发布通道"] = "Release channel",
        ["启动器的现代 Windows 桌面 UI 与窗口能力。"] = "Modern Windows desktop UI and window capabilities for the launcher.",
        ["启动游戏后最小化"] = "Minimize after game launch",
        ["国内网络推荐清华镜像；挂了代理可以选择 Adoptium 官方源。"] = "The Tsinghua mirror is recommended in mainland China; use the official Adoptium source when a proxy is available.",
        ["图片"] = "Image",
        ["在线音乐搜索、播放地址和 MUSIC_U Cookie 请求头的实现参考；MIT 许可证。"] = "Reference for online music search, playback URLs and the MUSIC_U Cookie request header; MIT licensed.",
        ["存储"] = "Storage",
        ["安装最新版本"] = "Install latest version",
        ["已下载的库文件与资源缓存，占用约 1.8 GB。"] = "Downloaded libraries and asset cache, using about 1.8 GB.",
        ["开启后使用 Mica 半透明背景；关闭后使用不透明背景。"] = "Enable a translucent Mica background; disable it for an opaque background.",
        ["当前游戏目录"] = "Current game folder",
        ["当前版本"] = "Current version",
        ["恢复默认"] = "Restore defaults",
        ["感谢以下开源项目、平台和文档为 LiteMCLauncher 提供支持。"] = "Thanks to these open-source projects, platforms and documentation for supporting LiteMCLauncher.",
        ["打开目录"] = "Open folder",
        ["打开项目页面"] = "Open project page",
        ["控制下载、更新与界面行为。"] = "Control downloads, updates and interface behavior.",
        ["搜索"] = "Search",
        ["搜索 Mod 或整合包，并下载到当前实例的对应目录。"] = "Search mods or modpacks and download them to the matching folder of the current instance.",
        ["搜索 Mod、整合包或作者..."] = "Search mods, modpacks or authors...",
        ["搜索历史"] = "Search history",
        ["搜索并播放网易云音乐；登录 Cookie 可访问账号相关音质和内容。"] = "Search and play NetEase Cloud Music; a login Cookie can access account-related quality and content.",
        ["搜索歌曲、歌手或专辑"] = "Search songs, artists or albums",
        ["播放"] = "Play",
        ["整合包"] = "Modpacks",
        ["日志文件"] = "Log files",
        ["日本語"] = "Japanese",
        ["更新于"] = "Updated",
        ["最近播放"] = "Recently played",
        ["未找到本地实例"] = "No local instances found",
        ["未选择图片"] = "No image selected",
        ["本项目仅用于学习与个人使用，请在遵守 Minecraft 最终用户许可协议的前提下使用。"] = "This project is for learning and personal use only; use it in compliance with the Minecraft End User License Agreement.",
        ["构建时间"] = "Build date",
        ["正在检测已安装的 Java 运行时…"] = "Detecting installed Java runtimes…",
        ["清理缓存"] = "Clear cache",
        ["版本"] = "Version",
        ["版本、实例、Mod 与存档都保存在此目录下。"] = "Versions, instances, mods and saves are stored in this folder.",
        ["版本信息"] = "Version information",
        ["界面主题"] = "Interface theme",
        ["繁體中文"] = "Traditional Chinese",
        ["纯色"] = "Solid color",
        ["缓存"] = "Cache",
        ["网易云 Cookie / MUSIC_U"] = "NetEase Cookie / MUSIC_U",
        ["背景"] = "Background",
        ["背景色"] = "Background color",
        ["自动检测，或从已安装的 Java 中指定启动器的默认运行时。"] = "Auto-detect, or choose the launcher's default runtime from installed Java.",
        ["覆盖系统强调色，应用于按钮、选中项与强调图标。"] = "Override the system accent color for buttons, selection and accent icons.",
        ["让启动器在游戏启动后自动最小化。"] = "Automatically minimize the launcher after the game starts.",
        ["设备码登录、Token 缓存和静默刷新。"] = "Device-code login, token cache and silent refresh.",
        ["该项目使用了 AI，不喜勿喷。"] = "This project uses AI; please be kind.",
        ["详情"] = "Details",
        ["跟随系统时会自动适配 Windows 主题。"] = "Follow Windows theme automatically.",
        ["输入关键词开始搜索"] = "Enter a keyword to search",
        ["返回 Mods 列表"] = "Back to Mods",
        ["选择图片"] = "Choose image",
        ["选择账号"] = "Select account",
        ["选择颜色"] = "Choose color",
        ["透明效果"] = "Transparency",
        ["重新扫描"] = "Rescan",
        ["重新读取版本清单"] = "Reload version manifest",
        ["镜像源可以提升国内网络下的下载速度。"] = "A mirror can improve download speed on mainland China networks.",
        ["项目介绍（展开查看详情）"] = "Project description (expand for details)",
        ["项目截图"] = "Project screenshots",
        ["鸣谢"] = "Credits",
        ["默认 Java"] = "Default Java",
        ["默认使用 Mica，也可以选择纯色或本地图片。"] = "Mica by default; a solid color or local image can also be used.",
        ["默认（Mica）"] = "Default (Mica)",

        ["尚未添加账号"] = "No account added yet",
        ["已登录"] = "Signed in",
        ["正在读取实例…"] = "Loading instances…",
        ["正在扫描 .minecraft/versions"] = "Scanning .minecraft/versions",
        ["扫描失败"] = "Scan failed",
        ["没有匹配的实例"] = "No matching instances",
        ["刷新版本列表"] = "Refresh versions",
        ["安装时附加的模组加载器"] = "Mod loader for installation",
        ["正在读取 Mojang 版本清单…"] = "Loading Mojang version manifest…",
        ["尚未搜索"] = "No search yet",
        ["输入关键词后开始搜索"] = "Enter a keyword to start searching",
        ["搜索 Mod"] = "Search Mods",
        ["搜索 Mod 或整合包"] = "Search mods or modpacks",
        ["已暂停。"] = "Paused.",
        ["尚未安装由启动器管理的 Java。"] = "No launcher-managed Java is installed.",
        ["输入关键词搜索网易云音乐"] = "Enter a keyword to search NetEase Cloud Music",

        ["移除账号"] = "Remove account",
        ["添加 Microsoft 账号"] = "Add Microsoft account",

        ["尝试调整搜索关键词或筛选条件。"] = "Try adjusting the search keyword or filters.",

        ["删除实例"] = "Delete instance",
        ["重命名实例"] = "Rename instance",
        ["保存"] = "Save",
        ["输入实例名称"] = "Enter instance name",
        ["名称仅用于启动器显示，不会修改版本目录。留空可恢复为默认名称。"] = "The name is only shown in the launcher and does not change the version folder. Leave it empty to restore the default name.",
        ["正在准备启动..."] = "Preparing to launch...",
        ["启动失败"] = "Launch failed",

        ["尚未找到日志文件。启动一次游戏后即可查看。"] = "No log files found yet. Launch the game once to view logs.",

        ["播放器"] = "Player",
        ["历史播放"] = "Play history",
        ["清空历史"] = "Clear history",
        ["未在播放"] = "Nothing playing",

        ["在线音乐搜索"] = "Online music search",
        ["播放器与历史记录；搜索和登录已移到单独的搜索页面。"] = "Player and history; search and login are now on a separate page.",
        ["搜索历史："] = "Search history:",

        ["搜索并播放网易云音乐"] = "Search and play NetEase Cloud Music",

        ["工具"] = "Tools",
        ["实用工具"] = "Utilities",
        ["一些方便 Minecraft 游玩的小工具。"] = "Small utilities for Minecraft.",
        ["坐标计算器"] = "Coordinate calculator",
        ["主世界/下界坐标互转与距离计算"] = "Overworld/Nether coordinate conversion and distance calculation",
        ["种子查看器"] = "Seed viewer",
        ["内嵌 ChunkBase 种子地图查询（暂未开放）"] = "Embedded ChunkBase seed map (not available yet)",
        ["主世界 → 下界"] = "Overworld → Nether",
        ["下界 → 主世界"] = "Nether → Overworld",
        ["主世界 X"] = "Overworld X",
        ["主世界 Z"] = "Overworld Z",
        ["下界 X"] = "Nether X",
        ["下界 Z"] = "Nether Z",
        ["主世界与下界坐标互转，以及两点直线距离计算。"] = "Convert between Overworld and Nether coordinates, and calculate straight-line distance.",
        ["主世界 X/Z 除以 8，得到下界坐标。"] = "Divide Overworld X/Z by 8 to get Nether coordinates.",
        ["下界 X/Z 乘以 8，得到主世界坐标。"] = "Multiply Nether X/Z by 8 to get Overworld coordinates.",
        ["直线距离"] = "Straight-line distance",
        ["计算两个坐标之间的水平直线距离。"] = "Calculate the horizontal straight-line distance between two coordinates.",
        ["起点 X"] = "Start X",
        ["起点 Z"] = "Start Z",
        ["终点 X"] = "End X",
        ["终点 Z"] = "End Z",
        ["计算"] = "Calculate",
        ["结果会显示在这里。"] = "The result will appear here.",
        ["请输入有效的 X 和 Z 坐标。"] = "Enter valid X and Z coordinates.",
        ["请输入有效的起点和终点坐标。"] = "Enter valid start and end coordinates.",

        ["登录已过期"] = "Sign-in expired",
        ["Microsoft 登录已失效，请重新登录该账号。"] = "Microsoft sign-in has expired. Please sign in to this account again.",
        ["正在登录…"] = "Signing in…",
        ["正在登录..."] = "Signing in...",

        ["无法启动 Minecraft 进程。"] = "Unable to start the Minecraft process.",

        ["内嵌 ChunkBase 种子地图查询"] = "Embedded ChunkBase seed map search",
        ["内嵌 ChunkBase 种子地图查询。"] = "Embedded ChunkBase seed map search.",

        ["在线种子地图与区块查看服务；种子查看器使用其公开网页。"] = "Online seed map and chunk viewer; the seed viewer uses its public web page.",

        ["Java 运行时下载与安装包。"] = "Java runtime downloads and installers.",
        ["Java 下载镜像源。"] = "Java download mirror.",
        ["Mod 与整合包搜索、下载 API。"] = "Mod and modpack search and download API.",
        ["玩家头像渲染与备用头像服务。"] = "Player avatar rendering and fallback avatar services.",
        ["种子查看器内嵌网页所使用的浏览器运行时。"] = "Browser runtime used by the seed viewer web page.",

        ["Forge 与 NeoForge 安装器支持。"] = "Forge and NeoForge installer support.",

        ["在线音乐搜索与播放服务。"] = "Online music search and playback service.",
    };

    private static readonly Dictionary<string, string> EnglishToChinese = BuildReverseMap();
    private static readonly ConditionalWeakTable<TextBlock, object> HookedTextBlocks = new();
    private static readonly ConditionalWeakTable<ComboBox, object> HookedComboBoxes = new();

    private static Dictionary<string, string> BuildReverseMap()
    {
        var reverse = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var pair in ChineseToEnglish)
        {
            reverse.TryAdd(pair.Value, pair.Key);
        }

        return reverse;
    }

    public static string CurrentLanguage { get; private set; } = Chinese;

    public static bool IsEnglish => string.Equals(CurrentLanguage, English, StringComparison.OrdinalIgnoreCase);

    public static void SetLanguage(string? language)
    {
        CurrentLanguage = string.Equals(language, English, StringComparison.OrdinalIgnoreCase)
            ? English
            : Chinese;
    }

    public static string Translate(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return text;
        }

        if (IsEnglish)
        {
            if (ChineseToEnglish.TryGetValue(text, out var english))
            {
                return english;
            }

            return TranslateDynamic(text);
        }

        return EnglishToChinese.TryGetValue(text, out var chinese) ? chinese : text;
    }

    private static string TranslateDynamic(string text)
    {
        if (text.StartsWith("正在检查游戏资源完整性并准备启动 ", StringComparison.Ordinal))
        {
            return "Checking game files and preparing to launch " +
                   text["正在检查游戏资源完整性并准备启动 ".Length..];
        }

        if (text.StartsWith("自动检测：已为 ", StringComparison.Ordinal))
        {
            return ("Auto-detect: " + text["自动检测：已为 ".Length..])
                .Replace(" 选择 Java ", " selected Java ", StringComparison.Ordinal)
                .Replace("。", ".", StringComparison.Ordinal);
        }

        if (text.StartsWith("Minecraft ", StringComparison.Ordinal) &&
            text.EndsWith(" 已启动。", StringComparison.Ordinal))
        {
            return text[..^"已启动。".Length] + "launched.";
        }

        if (text.StartsWith("下界坐标：X ", StringComparison.Ordinal))
        {
            return "Nether coordinates: " + text["下界坐标：X ".Length..];
        }

        if (text.StartsWith("主世界坐标：X ", StringComparison.Ordinal))
        {
            return "Overworld coordinates: " + text["主世界坐标：X ".Length..];
        }

        if (text.StartsWith("直线距离：", StringComparison.Ordinal) &&
            text.EndsWith(" 格", StringComparison.Ordinal))
        {
            return "Distance: " + text["直线距离：".Length..^" 格".Length] + " blocks";
        }

        if (text.Contains(" · 原版", StringComparison.Ordinal))
        {
            text = text.Replace(" · 原版", " · Vanilla", StringComparison.Ordinal);
        }

        if (text.StartsWith("昨天 ", StringComparison.Ordinal))
        {
            return "Yesterday " + text["昨天 ".Length..];
        }

        if (text.StartsWith("今天 ", StringComparison.Ordinal))
        {
            return "Today " + text["今天 ".Length..];
        }

        if (text.StartsWith("前天 ", StringComparison.Ordinal))
        {
            return "2 days ago " + text["前天 ".Length..];
        }

        if (text.StartsWith("已安装 ", StringComparison.Ordinal) &&
            text.Contains("Java 运行时", StringComparison.Ordinal))
        {
            return ("Installed " + text["已安装 ".Length..])
                .Replace(" 个 Java 运行时：", " Java runtimes: ", StringComparison.Ordinal)
                .Replace("。安装目录：", ". Install folder: ", StringComparison.Ordinal);
        }

        if (text.StartsWith("尚未安装由启动器管理的 Java。安装目录：", StringComparison.Ordinal))
        {
            return text.Replace(
                "尚未安装由启动器管理的 Java。安装目录：",
                "No launcher-managed Java is installed. Install folder: ",
                StringComparison.Ordinal);
        }

        if (text.StartsWith("已分配 ", StringComparison.Ordinal) &&
            text.EndsWith(" MB 内存", StringComparison.Ordinal))
        {
            return text
                .Replace("已分配 ", "Allocated ", StringComparison.Ordinal)
                .Replace(" MB 内存", " MB RAM", StringComparison.Ordinal);
        }

        if (text.StartsWith("Java ", StringComparison.Ordinal) &&
            text.EndsWith(" 已就绪", StringComparison.Ordinal))
        {
            return text[..^" 已就绪".Length] + " ready";
        }

        if (text.StartsWith("离线 UUID · ", StringComparison.Ordinal))
        {
            return "Offline UUID · " + text["离线 UUID · ".Length..];
        }

        if (text.StartsWith("离线模式 · ", StringComparison.Ordinal))
        {
            return "Offline mode · " + Translate(text["离线模式 · ".Length..]);
        }

        if (text.StartsWith("已登录 · ", StringComparison.Ordinal))
        {
            return "Signed in · " + Translate(text["已登录 · ".Length..]);
        }

        if (text.StartsWith("微软账号已登录 · ", StringComparison.Ordinal))
        {
            return "Microsoft account signed in · " + Translate(text["微软账号已登录 · ".Length..]);
        }

        if (text.StartsWith("微软账号需要重新登录 · ", StringComparison.Ordinal))
        {
            return "Microsoft account needs sign-in · " + Translate(text["微软账号需要重新登录 · ".Length..]);
        }

        if (text.StartsWith("请选择实例 · ", StringComparison.Ordinal))
        {
            return "Select an instance · " + Translate(text["请选择实例 · ".Length..]);
        }

        if (text.StartsWith("正在检测 ", StringComparison.Ordinal) &&
            text.Contains(" 支持的游戏版本", StringComparison.Ordinal))
        {
            var rest = text["正在检测 ".Length..];
            var suffixIndex = rest.IndexOf(" 支持的游戏版本", StringComparison.Ordinal);
            if (suffixIndex > 0)
            {
                return "Detecting supported game versions for " + Translate(rest[..suffixIndex]);
            }
        }

        if (text.StartsWith("正在读取 ", StringComparison.Ordinal))
        {
            return "Loading " + text["正在读取 ".Length..];
        }

        if (text.StartsWith("正在扫描 ", StringComparison.Ordinal))
        {
            return "Scanning " + text["正在扫描 ".Length..];
        }

        if (text.StartsWith("第 ", StringComparison.Ordinal) &&
            text.EndsWith(" 页", StringComparison.Ordinal))
        {
            return "Page " + text["第 ".Length..^" 页".Length];
        }

        if (text.StartsWith("启动 ", StringComparison.Ordinal))
        {
            return "Launch " + text["启动 ".Length..];
        }

        if (text.StartsWith("正在检测 ", StringComparison.Ordinal))
        {
            return "Detecting " + Translate(text["正在检测 ".Length..]);
        }

        if (text.StartsWith("正在搜索 ", StringComparison.Ordinal))
        {
            return "Searching " + text["正在搜索 ".Length..];
        }

        if (text.StartsWith("共 ", StringComparison.Ordinal) &&
            text.EndsWith(" 个版本", StringComparison.Ordinal))
        {
            return text["共 ".Length..^" 个版本".Length] + " versions";
        }

        if (text.StartsWith("共 ", StringComparison.Ordinal) &&
            text.EndsWith(" 个实例", StringComparison.Ordinal))
        {
            return text["共 ".Length..^" 个实例".Length] + " instances";
        }

        if (text.EndsWith(" 个结果", StringComparison.Ordinal))
        {
            return text[..^" 个结果".Length] + " results";
        }

        if (text.EndsWith(" 已暂停。", StringComparison.Ordinal))
        {
            return "Paused.";
        }

        if (text.StartsWith("正在播放：", StringComparison.Ordinal))
        {
            return "Playing: " + text["正在播放：".Length..];
        }

        if (text.StartsWith("找到 ", StringComparison.Ordinal) &&
            text.EndsWith(" 首歌曲。", StringComparison.Ordinal))
        {
            return "Found " + text["找到 ".Length..^" 首歌曲。".Length] + " songs.";
        }

        return text;
    }

    public static void Apply(DependencyObject? root)
    {
        if (root is null)
        {
            return;
        }

        var elements = new List<DependencyObject>();
        Collect(root, elements);

        foreach (var element in elements)
        {
            try
            {
                TranslateElement(element);
            }
            catch
            {
                // One unsupported template child must not block the rest.
            }
        }
    }

    private static void Collect(DependencyObject element, List<DependencyObject> elements)
    {
        elements.Add(element);

        try
        {
            var count = VisualTreeHelper.GetChildrenCount(element);
            for (var index = 0; index < count; index++)
            {
                Collect(VisualTreeHelper.GetChild(element, index), elements);
            }
        }
        catch
        {
            // Some template children are not part of the visual tree yet.
        }
    }

    private static void TranslateSelectedComboBoxItem(ComboBox comboBox)
    {
        if (comboBox.SelectedItem is ComboBoxItem { Content: string text } selected)
        {
            selected.Content = Translate(text);
        }
    }

    private static void TranslateComboBoxItems(ComboBox comboBox)
    {
        var selectedIndex = comboBox.SelectedIndex;
        foreach (var item in comboBox.Items)
        {
            if (item is ComboBoxItem comboItem && comboItem.Content is string text)
            {
                comboItem.Content = Translate(text);
            }
            else if (item is FrameworkElement element)
            {
                Apply(element);
            }
        }

        if (selectedIndex >= 0 && comboBox.SelectedIndex != selectedIndex)
        {
            comboBox.SelectedIndex = selectedIndex;
        }
    }

    private static void OnTextBlockTextChanged(DependencyObject sender, DependencyProperty dp)
    {
        if (!IsEnglish || sender is not TextBlock textBlock || string.IsNullOrEmpty(textBlock.Text))
        {
            return;
        }

        var translated = Translate(textBlock.Text);
        if (!string.Equals(translated, textBlock.Text, StringComparison.Ordinal))
        {
            textBlock.Text = translated;
        }
    }

    private static void TranslateElement(DependencyObject element)
    {
        switch (element)
        {
            case TextBlock textBlock:
                if (!HookedTextBlocks.TryGetValue(textBlock, out _))
                {
                    HookedTextBlocks.Add(textBlock, new object());
                    textBlock.RegisterPropertyChangedCallback(
                        TextBlock.TextProperty,
                        OnTextBlockTextChanged);
                }

                if (!string.IsNullOrEmpty(textBlock.Text))
                {
                    textBlock.Text = Translate(textBlock.Text);
                }

                break;

            case Button button when button.Content is string buttonText:
                button.Content = Translate(buttonText);
                break;

            case HyperlinkButton hyperlink when hyperlink.Content is string hyperlinkText:
                hyperlink.Content = Translate(hyperlinkText);
                break;

            case NavigationViewItem navigationItem when navigationItem.Content is string navigationText:
                navigationItem.Content = Translate(navigationText);
                break;

            case TabViewItem tabItem when tabItem.Header is string tabHeader:
                tabItem.Header = Translate(tabHeader);
                break;

            case ToggleSwitch toggle:
                if (toggle.Header is string header)
                {
                    toggle.Header = Translate(header);
                }

                if (toggle.OnContent is string onContent)
                {
                    toggle.OnContent = Translate(onContent);
                }

                if (toggle.OffContent is string offContent)
                {
                    toggle.OffContent = Translate(offContent);
                }

                break;

            case TextBox textBox:
                if (textBox.Header is string textBoxHeader)
                {
                    textBox.Header = Translate(textBoxHeader);
                }

                textBox.PlaceholderText = Translate(textBox.PlaceholderText);
                break;

            case PasswordBox passwordBox:
                if (passwordBox.Header is string passwordHeader)
                {
                    passwordBox.Header = Translate(passwordHeader);
                }

                passwordBox.PlaceholderText = Translate(passwordBox.PlaceholderText);
                break;

            case CheckBox checkBox when checkBox.Content is string checkText:
                checkBox.Content = Translate(checkText);
                break;

            case RadioButton radioButton when radioButton.Content is string radioText:
                radioButton.Content = Translate(radioText);
                break;

            case MenuFlyoutItem menuItem when menuItem.Text is { Length: > 0 } menuText:
                menuItem.Text = Translate(menuText);
                break;

            case NumberBox numberBox when numberBox.Header is string numberHeader:
                numberBox.Header = Translate(numberHeader);
                break;

            case ComboBox comboBox:
                if (comboBox.Header is string comboHeader)
                {
                    comboBox.Header = Translate(comboHeader);
                }

                TranslateSelectedComboBoxItem(comboBox);
                if (!HookedComboBoxes.TryGetValue(comboBox, out _))
                {
                    HookedComboBoxes.Add(comboBox, new object());
                    comboBox.DropDownOpened += (_, _) => TranslateComboBoxItems(comboBox);
                }

                break;

            case ComboBoxItem comboItem when comboItem.Content is string comboText:
                comboItem.Content = Translate(comboText);
                break;
        }
    }
}
