using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

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
    };

    private static readonly Dictionary<string, string> EnglishToChinese = BuildReverseMap();

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
            return ChineseToEnglish.TryGetValue(text, out var english) ? english : text;
        }

        return EnglishToChinese.TryGetValue(text, out var chinese) ? chinese : text;
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
            TranslateElement(element);
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

    private static void TranslateElement(DependencyObject element)
    {
        switch (element)
        {
            case TextBlock textBlock when !string.IsNullOrEmpty(textBlock.Text):
                textBlock.Text = Translate(textBlock.Text);
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

            case NumberBox numberBox when numberBox.Header is string numberHeader:
                numberBox.Header = Translate(numberHeader);
                break;

            case ComboBox comboBox:
                if (comboBox.Header is string comboHeader)
                {
                    comboBox.Header = Translate(comboHeader);
                }

                break;

            case ComboBoxItem comboItem when comboItem.Content is string comboText:
                comboItem.Content = Translate(comboText);
                break;
        }
    }
}
