using System.Diagnostics;
using CmlLib.Core;
using CmlLib.Core.Auth;
using CmlLib.Core.FileExtractors;
using CmlLib.Core.ProcessBuilder;
using LiteMCLauncher.Models;

namespace LiteMCLauncher.Services;

/// <summary>
/// Prepares the selected instance, builds the JVM and game arguments with
/// CmlLib.Core's process builder, and starts it with Process.Start.
/// </summary>
public sealed class GameLaunchService
{
    private readonly JavaService _javaService;
    private readonly LauncherState _launcherState;
    private readonly LauncherSettingsService _settingsService;
    private readonly SemaphoreSlim _launchGate = new(1, 1);

    public GameLaunchService()
        : this(AppServices.Java, AppServices.LauncherState, AppServices.Settings)
    {
    }

    public GameLaunchService(
        JavaService javaService,
        LauncherState launcherState,
        LauncherSettingsService settingsService)
    {
        _javaService = javaService;
        _launcherState = launcherState;
        _settingsService = settingsService;
    }

    public async Task<GameLaunchResult> LaunchAsync(
        MinecraftInstance instance,
        AccountModel account,
        IProgress<MinecraftInstallProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(instance);
        ArgumentNullException.ThrowIfNull(account);

        await _launchGate.WaitAsync(cancellationToken);
        try
        {
            var tracker = new InstallProgressTracker(progress);
            var settings = _settingsService.Current;
            var instanceSettings = InstanceSettingsStore.Get(instance.Id);
            var useInstanceSettings = instanceSettings.Enabled;
            var configuredJavaPath = useInstanceSettings
                ? instanceSettings.JavaPath
                : settings.JavaPath;
            var javaPath = ResolveConfiguredJava(configuredJavaPath);

            if (!string.IsNullOrWhiteSpace(configuredJavaPath) && javaPath is null)
            {
                throw new InvalidOperationException(useInstanceSettings
                    ? "实例设置中的 Java 路径无效。请在实例设置中重新选择 Java。"
                    : "设置中的 Java 路径无效。请在“设置 > 游戏 > Java 路径”中选择 java.exe 或 Java 目录。");
            }

            if (javaPath is null)
            {
                var java = await _javaService.GetPreferredRuntimeForGameAsync(
                    instance.GameVersion,
                    cancellationToken);
                javaPath = java?.PreferredExecutable;

                if (java is not null)
                {
                    tracker.ReportStage(
                        $"自动检测：已为 {instance.GameVersion} 选择 Java {java.Version}（{java.Name}）。");
                }
            }

            if (javaPath is null)
            {
                throw new InvalidOperationException(
                    "没有可用的 Java 运行时。请先在“设置 > Java 管理”中安装 Java 8、17、21 或 25，或指定 Java 路径。");
            }

            if (!JavaService.ValidateRuntime(javaPath, out var javaValidationOutput))
            {
                throw new InvalidOperationException(
                    $"选中的 Java 无法运行，请重新安装或改选其他 Java。{javaValidationOutput}".Trim());
            }

            var requiredMajor = JavaService.GetRequiredJavaMajor(instance.GameVersion);
            var selectedVersion = JavaService.ReadJavaVersionFromPath(javaPath);
            var selectedMajor = JavaService.ParseJavaMajor(selectedVersion ?? string.Empty);
            var moddedInstance = !string.IsNullOrWhiteSpace(instance.Loader) &&
                                 !string.Equals(instance.Loader, "原版", StringComparison.OrdinalIgnoreCase);
            if (selectedMajor != 0 &&
                (selectedMajor < requiredMajor || (moddedInstance && selectedMajor != requiredMajor)))
            {
                throw new InvalidOperationException(
                    $"该实例需要 Java {requiredMajor}，当前选择的是 Java {selectedVersion}。模组加载器请使用对应 Java 大版本，Fabric/Forge/NeoForge 1.20.1 请选择 Java 17。");
            }

            var launchPath = CreateLaunchPath(instance);
            tracker.ReportStage($"正在检查游戏资源完整性并准备启动 {instance.DisplayName}...");

            var parameters = MinecraftLauncherParameters.CreateDefault(launchPath);
            if (parameters.FileExtractors is { } extractors)
            {
                var javaExtractor = extractors.OfType<JavaFileExtractor>().FirstOrDefault();
                if (javaExtractor is not null)
                {
                    extractors.Remove(javaExtractor);
                }
            }

            var launcher = new MinecraftLauncher(parameters);

            var extraJvmArguments = MLaunchOption.DefaultExtraJvmArguments.ToList();
            var configuredJvmArguments = useInstanceSettings
                ? instanceSettings.JvmArguments
                : settings.ExtraJvmArguments;
            if (!string.IsNullOrWhiteSpace(configuredJvmArguments))
            {
                extraJvmArguments.Add(MArgument.FromCommandLine(configuredJvmArguments));
            }

            var extraGameArguments = new List<MArgument>();
            var gameLanguage = useInstanceSettings
                ? instanceSettings.GameLanguage
                : settings.GameLanguage;
            ApplyGameLanguage(launchPath.BasePath, gameLanguage);
            if (!string.IsNullOrWhiteSpace(gameLanguage))
            {
                extraGameArguments.Add(new MArgument(["--lang", gameLanguage]));
            }

            var maximumRamMb = useInstanceSettings && instanceSettings.MaximumRamMb > 0
                ? instanceSettings.MaximumRamMb
                : settings.MaximumRamMb;

            var session = account.IsOffline
                ? MSession.CreateOfflineSession(account.Username)
                : new MSession
                {
                    Username = account.Username,
                    UUID = account.Uuid,
                    AccessToken = account.AccessToken,
                    UserType = "msa",
                    Xuid = account.Xuid,
                };

            var option = new MLaunchOption
            {
                Path = launchPath,
                Session = session,
                JavaPath = javaPath,
                MaximumRamMb = maximumRamMb,
                MinimumRamMb = Math.Min(1024, maximumRamMb),
                ScreenWidth = 1280,
                ScreenHeight = 720,
                GameLauncherName = "LiteMCLauncher",
                GameLauncherVersion = LauncherInfo.Version,
                ClientId = "LiteMCLauncher",
                VersionType = "LiteMCLauncher",
                ExtraJvmArguments = extraJvmArguments,
                ExtraGameArguments = extraGameArguments,
            };

            var process = await launcher.InstallAndBuildProcessAsync(
                instance.Id,
                option,
                tracker.FileProgress,
                tracker.ByteProgress,
                cancellationToken);

            process.StartInfo.UseShellExecute = false;
            process.StartInfo.CreateNoWindow = true;
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.RedirectStandardError = true;

            var outputPath = Path.Combine(AppPaths.DataDirectory, "last-java-output.log");
            Task? outputTask = null;

            try
            {
                Directory.CreateDirectory(AppPaths.DataDirectory);
                File.WriteAllText(
                    Path.Combine(AppPaths.DataDirectory, "last-launch.txt"),
                    $"Java: {javaPath}{Environment.NewLine}" +
                    $"Executable: {process.StartInfo.FileName}{Environment.NewLine}" +
                    $"Arguments: {process.StartInfo.Arguments}");
            }
            catch
            {
            }

            try
            {
                if (!process.Start())
                {
                    throw new InvalidOperationException("无法启动 Minecraft 进程。");
                }
            }
            catch (Exception exception) when (exception is not InvalidOperationException)
            {
                throw new InvalidOperationException(
                    $"无法启动 Minecraft 进程：{exception.Message}\n" +
                    $"Java: {process.StartInfo.FileName}\n" +
                    $"Game: {instance.Id}", exception);
            }

            outputTask = CaptureJavaOutputAsync(process, outputPath);

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(2));
                if (process.HasExited)
                {
                    var output = string.Empty;
                    if (outputTask is not null && await Task.WhenAny(outputTask, Task.Delay(1000)) == outputTask)
                    {
                        output = await File.ReadAllTextAsync(outputPath);
                    }

                    throw new InvalidOperationException(
                        $"Minecraft 进程启动后立即退出（退出码 {process.ExitCode}）。\n{output}".Trim());
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }

            tracker.ReportComplete($"Minecraft {instance.Id} 已启动。");
            return new GameLaunchResult(process.Id, instance.Id);
        }
        finally
        {
            _launchGate.Release();
        }
    }

    private static string? ResolveConfiguredJava(string? configuredPath)
    {
        if (string.IsNullOrWhiteSpace(configuredPath))
        {
            return null;
        }

        var path = configuredPath.Trim().Trim('"');

        if (Directory.Exists(path))
        {
            var java = Path.Combine(path, "bin", "java.exe");
            if (File.Exists(java))
            {
                return java;
            }

            var javaw = Path.Combine(path, "bin", "javaw.exe");
            if (File.Exists(javaw))
            {
                return javaw;
            }
        }

        if (File.Exists(path) &&
            Path.GetFileName(path).StartsWith("java", StringComparison.OrdinalIgnoreCase))
        {
            return path;
        }

        return null;
    }

    private static async Task CaptureJavaOutputAsync(Process process, string outputPath)
    {
        try
        {
            var stdout = process.StandardOutput.ReadToEndAsync();
            var stderr = process.StandardError.ReadToEndAsync();
            var output = $"--- stdout ---{Environment.NewLine}{await stdout}" +
                         $"{Environment.NewLine}--- stderr ---{Environment.NewLine}{await stderr}";
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
            await File.WriteAllTextAsync(outputPath, output);
        }
        catch
        {
        }
    }

    private static void ApplyGameLanguage(string gameDirectory, string? language)
    {
        if (string.IsNullOrWhiteSpace(language))
        {
            return;
        }

        try
        {
            Directory.CreateDirectory(gameDirectory);
            var optionsPath = Path.Combine(gameDirectory, "options.txt");
            var lines = File.Exists(optionsPath)
                ? File.ReadAllLines(optionsPath).ToList()
                : [];

            var languageLine = $"lang:{language}";
            var index = lines.FindIndex(line =>
                line.StartsWith("lang:", StringComparison.OrdinalIgnoreCase));
            if (index >= 0)
            {
                lines[index] = languageLine;
            }
            else
            {
                lines.Insert(0, languageLine);
            }

            File.WriteAllLines(optionsPath, lines);
        }
        catch
        {
            // Launching must continue even if options.txt is read-only.
        }
    }

    private MinecraftPath CreateLaunchPath(MinecraftInstance instance)
    {        var gameDirectory = _launcherState.GetGameDirectory(instance);
        Directory.CreateDirectory(gameDirectory);

        // The game directory is per-instance, while versions, libraries,
        // assets and the Java runtime stay in the shared .minecraft root.
        var shared = new MinecraftPath(AppPaths.MinecraftDirectory);
        var launchPath = new MinecraftPath(AppPaths.MinecraftDirectory)
        {
            BasePath = gameDirectory,
            Library = shared.Library,
            Versions = shared.Versions,
            Assets = shared.Assets,
            Resource = shared.Resource,
            Runtime = shared.Runtime,
        };

        launchPath.CreateDirs();
        _launcherState.EnsureInstanceDirectories(instance);
        return launchPath;
    }
}
