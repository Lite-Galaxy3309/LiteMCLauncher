using Microsoft.UI.Xaml.Media;

namespace LiteMCLauncher.Models;

public sealed class LogLine
{
    public string Text { get; init; } = string.Empty;

    public Brush? Foreground { get; init; }
}
