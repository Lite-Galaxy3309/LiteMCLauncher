using CmlLib.Core;
using CmlLib.Core.Installers;
using LiteMCLauncher.Models;

namespace LiteMCLauncher.Services;

/// <summary>
/// Combines CmlLib's file-count and byte-count progress streams into one
/// progress model suitable for the UI.
/// </summary>
public sealed class InstallProgressTracker
{
    private readonly IProgress<MinecraftInstallProgress>? _progress;
    private readonly object _sync = new();

    private int _totalFiles;
    private int _completedFiles;
    private string? _currentFile;
    private long _totalBytes;
    private long _progressedBytes;
    private double _lastReportedPercentage = -1;
    private string? _lastReportedFile;

    public InstallProgressTracker(IProgress<MinecraftInstallProgress>? progress)
    {
        _progress = progress;
        FileProgress = new CallbackProgress<InstallerProgressChangedEventArgs>(OnFileProgress);
        ByteProgress = new CallbackProgress<ByteProgress>(OnByteProgress);
    }

    public IProgress<InstallerProgressChangedEventArgs> FileProgress { get; }

    public IProgress<ByteProgress> ByteProgress { get; }

    public void ReportStage(string status)
    {
        _progress?.Report(new MinecraftInstallProgress(0, status, null));
    }

    public void ReportComplete(string status)
    {
        _progress?.Report(new MinecraftInstallProgress(100, status, null));
    }

    private void OnFileProgress(InstallerProgressChangedEventArgs args)
    {
        string status;
        string? currentFile;

        lock (_sync)
        {
            _totalFiles = args.TotalTasks;
            _completedFiles = args.ProgressedTasks;
            _currentFile = args.Name;
            currentFile = _currentFile;
            status = BuildStatus();
        }

        Report(status, currentFile);
    }

    private void OnByteProgress(ByteProgress progress)
    {
        string status;
        string? currentFile;

        lock (_sync)
        {
            _totalBytes = progress.TotalBytes;
            _progressedBytes = progress.ProgressedBytes;
            currentFile = _currentFile;
            status = BuildStatus();
        }

        Report(status, currentFile);
    }

    private string BuildStatus()
    {
        var parts = new List<string>();

        if (_totalFiles > 0)
        {
            parts.Add($"文件 {_completedFiles}/{_totalFiles}");
        }

        if (_totalBytes > 0)
        {
            parts.Add($"下载 {FormatBytes(_progressedBytes)} / {FormatBytes(_totalBytes)}");
        }

        return parts.Count == 0 ? "正在准备文件..." : string.Join(" · ", parts);
    }

    private void Report(string status, string? currentFile)
    {
        double percentage;

        lock (_sync)
        {
            percentage = _totalBytes > 0
                ? Math.Clamp(_progressedBytes * 100d / _totalBytes, 0, 100)
                : _totalFiles > 0
                    ? Math.Clamp(_completedFiles * 100d / _totalFiles, 0, 100)
                    : 0;
        }

        if (percentage < 100 &&
            Math.Abs(percentage - _lastReportedPercentage) < 0.5 &&
            string.Equals(currentFile, _lastReportedFile, StringComparison.Ordinal))
        {
            return;
        }

        _lastReportedPercentage = percentage;
        _lastReportedFile = currentFile;
        _progress?.Report(new MinecraftInstallProgress(percentage, status, currentFile));
    }

    private static string FormatBytes(long bytes)
    {
        return bytes switch
        {
            >= 1024L * 1024 * 1024 => $"{bytes / 1024d / 1024d / 1024d:F2} GB",
            >= 1024L * 1024 => $"{bytes / 1024d / 1024d:F1} MB",
            >= 1024 => $"{bytes / 1024d:F1} KB",
            _ => $"{bytes} B",
        };
    }

    private sealed class CallbackProgress<T>(Action<T> callback) : IProgress<T>
    {
        public void Report(T value) => callback(value);
    }
}
