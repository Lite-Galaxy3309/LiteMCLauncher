using System.Diagnostics;
using System.IO.Compression;
using System.Reflection;
using System.Runtime.InteropServices;

namespace LiteMCLauncher.Bootstrapper;

internal static class Program
{
    private const string AppVersion = "0.5.4";

    [STAThread]
    private static void Main()
    {
        try
        {
            var launcherRoot = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "LiteMCLauncher");
            var appDirectory = Path.Combine(launcherRoot, $"app-{AppVersion}");
            var appExecutable = Path.Combine(appDirectory, "LiteMCLauncher.exe");

            if (!File.Exists(appExecutable))
            {
                Directory.CreateDirectory(launcherRoot);
                ExtractPayload(appDirectory);
            }

            Process.Start(new ProcessStartInfo(appExecutable)
            {
                WorkingDirectory = appDirectory,
                UseShellExecute = true,
            });
        }
        catch (Exception exception)
        {
            MessageBox(
                IntPtr.Zero,
                $"LiteMCLauncher 启动失败：\n{exception.Message}",
                "LiteMCLauncher",
                0x00000010);
        }
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

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int MessageBox(IntPtr hWnd, string text, string caption, uint type);
}
