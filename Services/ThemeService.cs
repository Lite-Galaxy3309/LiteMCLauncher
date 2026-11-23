using Microsoft.UI.Xaml;

namespace LiteMCLauncher.Services;

/// <summary>
/// Applies the saved light/dark theme to the main window content.
/// </summary>
public static class ThemeService
{
    public static void Apply(string? theme)
    {
        if (App.MainWindow?.Content is not FrameworkElement root)
        {
            return;
        }

        root.RequestedTheme = theme switch
        {
            "Light" => ElementTheme.Light,
            "Dark" => ElementTheme.Dark,
            _ => ElementTheme.Default,
        };
    }
}
