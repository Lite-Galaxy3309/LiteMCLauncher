using System.Diagnostics;
using System.IO.Compression;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;

namespace LiteMCLauncher.Bootstrapper;

internal static class Program
{
    private const string AppVersion = "0.6.4";
    private const string CompleteMarker = ".payload-complete";

    [STAThread]
    private static void Main()
    {
        var launcherRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "LiteMCLauncher");
        var logPath = Path.Combine(launcherRoot, "bootstrapper.log");
        var appDirectory = Path.Combine(launcherRoot, $"app-{AppVersion}");
        var appExecutable = Path.Combine(appDirectory, "LiteMCLauncher.exe");
        var requiredNative = Path.Combine(appDirectory, "libSkiaSharp.dll");

        try
        {
            Directory.CreateDirectory(launcherRoot);
            Log(logPath, $"start version={AppVersion}");

            using var mutex = new Mutex(false, $@"Local\LiteMCLauncher-Bootstrapper-{AppVersion}");
            if (!mutex.WaitOne(TimeSpan.FromMinutes(10)))
            {
                throw new TimeoutException("另一个启动器实例正在解压，请稍后重试。");
            }

            try
            {
                if (!IsComplete(appExecutable, requiredNative, appDirectory))
                {
                    Log(logPath, "payload incomplete; extracting");
                    ExtractPayload(appDirectory);
                    Log(logPath, "payload extracted");
                }

                if (!IsComplete(appExecutable, requiredNative, appDirectory))
                {
                    throw new InvalidOperationException("应用文件解压不完整。");
                }

                Log(logPath, "starting app");
                var process = Process.Start(new ProcessStartInfo(appExecutable)
                {
                    WorkingDirectory = appDirectory,
                    UseShellExecute = true,
                });

                if (process is null)
                {
                    throw new InvalidOperationException("无法创建应用进程。");
                }

                if (!process.WaitForExit(5000))
                {
                    Log(logPath, "app started");
                    return;
                }

                if (process.ExitCode != 0)
                {
                    throw new InvalidOperationException(
                        $"应用启动后立即退出，退出码 {process.ExitCode}。\n日志：{logPath}");
                }

                Log(logPath, "app exited normally");
            }
            finally
            {
                mutex.ReleaseMutex();
            }
        }
        catch (Exception exception)
        {
            Log(logPath, $"FAILED {exception}");
            MessageBox(
                IntPtr.Zero,
                $"LiteMCLauncher 启动失败：\n{exception.Message}\n\n诊断日志：\n{logPath}",
                "LiteMCLauncher",
                0x00000010);
        }
    }

    private static bool IsComplete(string appExecutable, string requiredNative, string appDirectory)
    {
        return File.Exists(appExecutable) &&
               new FileInfo(appExecutable).Length > 0 &&
               File.Exists(requiredNative) &&
               new FileInfo(requiredNative).Length > 0 &&
               File.Exists(Path.Combine(appDirectory, CompleteMarker));
    }

    private static void ExtractPayload(string appDirectory)
    {
        using var payload = Assembly.GetExecutingAssembly().GetManifestResourceStream("payload.zip")
                            ?? throw new InvalidOperationException("内置应用包不存在。");
        using var archive = new ZipArchive(payload, ZipArchiveMode.Read);

        var staging = $"{appDirectory}.tmp-{Guid.NewGuid():N}";
        try
        {
            Directory.CreateDirectory(staging);
            archive.ExtractToDirectory(staging, overwriteFiles: true);
            File.WriteAllText(Path.Combine(staging, CompleteMarker), AppVersion, Encoding.UTF8);

            if (Directory.Exists(appDirectory))
            {
                Directory.Delete(appDirectory, recursive: true);
            }

            Directory.Move(staging, appDirectory);
        }
        finally
        {
            if (Directory.Exists(staging))
            {
                try
                {
                    Directory.Delete(staging, recursive: true);
                }
                catch
                {
                }
            }
        }
    }

    private static void Log(string path, string message)
    {
        try
        {
            File.AppendAllText(
                path,
                $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}{Environment.NewLine}",
                Encoding.UTF8);
        }
        catch
        {
        }
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int MessageBox(IntPtr hWnd, string text, string caption, uint type);
}
