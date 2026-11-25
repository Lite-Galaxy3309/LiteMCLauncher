using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace LiteMCLauncher.Services;

public static class ContentDialogLocalization
{
    public static async Task<ContentDialogResult> ShowLocalizedAsync(this ContentDialog dialog)
    {
        Localize(dialog);

        if (dialog.Content is FrameworkElement content)
        {
            dialog.Opened += (_, _) =>
            {
                LauncherLanguageService.ApplyNew(content);
                content.LayoutUpdated += (_, _) =>
                    LauncherLanguageService.ApplyNew(content);
            };
        }

        return await dialog.ShowAsync();
    }

    public static void Localize(ContentDialog dialog)
    {
        if (dialog.Title is string title)
        {
            dialog.Title = LauncherLanguageService.Translate(title);
        }

        if (dialog.PrimaryButtonText is { Length: > 0 } primary)
        {
            dialog.PrimaryButtonText = LauncherLanguageService.Translate(primary);
        }

        if (dialog.SecondaryButtonText is { Length: > 0 } secondary)
        {
            dialog.SecondaryButtonText = LauncherLanguageService.Translate(secondary);
        }

        if (dialog.CloseButtonText is { Length: > 0 } close)
        {
            dialog.CloseButtonText = LauncherLanguageService.Translate(close);
        }

        if (dialog.Content is string text)
        {
            dialog.Content = LauncherLanguageService.Translate(text);
        }
        else if (dialog.Content is FrameworkElement content)
        {
            LauncherLanguageService.Apply(content);
        }
    }
}
