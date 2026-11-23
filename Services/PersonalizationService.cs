using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace LiteMCLauncher.Services;

/// <summary>
/// Applies theme, accent color, transparency and background personalization.
/// </summary>
public static class PersonalizationService
{
    private static readonly string[] AccentResourceKeys =
    [
        "SystemAccentColor",
        "SystemAccentColorLight1",
        "SystemAccentColorLight2",
        "SystemAccentColorLight3",
        "SystemAccentColorDark1",
        "SystemAccentColorDark2",
        "SystemAccentColorDark3",
        "AccentFillColorDefaultBrush",
        "AccentFillColorSecondaryBrush",
        "AccentFillColorTertiaryBrush",
        "AccentTextFillColorPrimaryBrush",
        "TextOnAccentFillColorPrimaryBrush",
    ];

    public static void Apply(bool refreshExistingControls = false)
    {
        var settings = AppServices.Settings.Current;
        ThemeService.Apply(settings.Theme);
        ApplyAccent(settings.AccentColor, settings.Theme);

        if (App.MainWindow is MainWindow mainWindow)
        {
            mainWindow.ApplyWindowBackground(settings);

            if (refreshExistingControls &&
                mainWindow.Content is FrameworkElement root &&
                root.IsLoaded)
            {
                // Force theme resources (including the accent brushes) to be
                // re-evaluated after the user changes personalization settings.
                var requestedTheme = root.RequestedTheme;
                root.RequestedTheme = requestedTheme == ElementTheme.Dark
                    ? ElementTheme.Light
                    : ElementTheme.Dark;
                root.RequestedTheme = requestedTheme;
            }
        }
    }

    /// <summary>
    /// Applies accent resources before the main window creates its controls.
    /// </summary>
    public static void ApplyAccentOnly()
    {
        var settings = AppServices.Settings.Current;
        ApplyAccent(settings.AccentColor, settings.Theme);
    }

    public static Color? ParseColor(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var text = value.Trim().TrimStart('#');
        if (text.Length == 6)
        {
            text = "FF" + text;
        }

        if (text.Length != 8)
        {
            return null;
        }

        return uint.TryParse(
            text,
            System.Globalization.NumberStyles.HexNumber,
            null,
            out var argb)
            ? Color.FromArgb(
                (byte)((argb >> 24) & 0xFF),
                (byte)((argb >> 16) & 0xFF),
                (byte)((argb >> 8) & 0xFF),
                (byte)(argb & 0xFF))
            : null;
    }

    public static string ToHex(Color color) =>
        $"#{color.A:X2}{color.R:X2}{color.G:X2}{color.B:X2}";

    private static void ApplyAccent(string? accentHex, string theme)
    {
        var color = ParseColor(accentHex);
        if (color is null)
        {
            ResetAccent();
            return;
        }

        var isDark = theme == "Dark" ||
                     (theme == "System" && Application.Current.RequestedTheme == ApplicationTheme.Dark);

        var accent = isDark ? Lighten(color.Value, 0.16) : Darken(color.Value, 0.06);
        var textOnAccent = GetContrastColor(accent);

        Application.Current.Resources["SystemAccentColor"] = color.Value;
        Application.Current.Resources["SystemAccentColorLight1"] = Lighten(color.Value, 0.10);
        Application.Current.Resources["SystemAccentColorLight2"] = Lighten(color.Value, 0.20);
        Application.Current.Resources["SystemAccentColorLight3"] = Lighten(color.Value, 0.30);
        Application.Current.Resources["SystemAccentColorDark1"] = Darken(color.Value, 0.10);
        Application.Current.Resources["SystemAccentColorDark2"] = Darken(color.Value, 0.20);
        Application.Current.Resources["SystemAccentColorDark3"] = Darken(color.Value, 0.30);

        Application.Current.Resources["AccentFillColorDefaultBrush"] = new SolidColorBrush(accent);
        Application.Current.Resources["AccentFillColorSecondaryBrush"] = new SolidColorBrush(
            Color.FromArgb(0xE6, accent.R, accent.G, accent.B));
        Application.Current.Resources["AccentFillColorTertiaryBrush"] = new SolidColorBrush(
            Color.FromArgb(0xCC, accent.R, accent.G, accent.B));
        Application.Current.Resources["AccentTextFillColorPrimaryBrush"] = new SolidColorBrush(accent);
        Application.Current.Resources["TextOnAccentFillColorPrimaryBrush"] = new SolidColorBrush(textOnAccent);
    }

    private static void ResetAccent()
    {
        foreach (var key in AccentResourceKeys)
        {
            Application.Current.Resources.Remove(key);
        }
    }

    private static Color Lighten(Color color, double amount)
    {
        return Color.FromArgb(
            color.A,
            ClampByte(color.R + (255 - color.R) * amount),
            ClampByte(color.G + (255 - color.G) * amount),
            ClampByte(color.B + (255 - color.B) * amount));
    }

    private static Color Darken(Color color, double amount)
    {
        return Color.FromArgb(
            color.A,
            ClampByte(color.R * (1 - amount)),
            ClampByte(color.G * (1 - amount)),
            ClampByte(color.B * (1 - amount)));
    }

    private static Color GetContrastColor(Color color)
    {
        var luminance = (0.2126 * color.R + 0.7152 * color.G + 0.0722 * color.B) / 255d;
        return luminance > 0.55 ? Color.FromArgb(255, 0, 0, 0) : Colors.White;
    }

    private static byte ClampByte(double value) =>
        (byte)Math.Clamp((int)Math.Round(value), 0, 255);
}
