using LiteMCLauncher.Models;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace LiteMCLauncher.Pages;

/// <summary>
/// Launch preflight prompt. Java problems can be overridden (Continue) or sent
/// to the Java management page; login problems must be resolved first.
/// </summary>
public static class LaunchPreflightDialog
{
    public static async Task<bool> ShowAsync(
        XamlRoot xamlRoot,
        Frame frame,
        LaunchPreflightResult result)
    {
        if (!result.LoginReady)
        {
            var loginDialog = new ContentDialog
            {
                Title = "登录状态不可用",
                Content = new TextBlock
                {
                    Text = result.ErrorMessage ?? "请先重新登录账号。",
                    TextWrapping = TextWrapping.Wrap,
                },
                CloseButtonText = "知道了",
                XamlRoot = xamlRoot,
            };

            await loginDialog.ShowAsync();
            return false;
        }

        var title = !result.JavaReady ? "缺少 Java 运行时" : "Java 版本不匹配";
        var message = result.ErrorMessage ?? "当前 Java 版本与游戏不匹配。";

        if (!string.IsNullOrWhiteSpace(result.JavaPath))
        {
            message += $"\n\n当前 Java：{result.JavaVersion ?? "未知版本"}\n{result.JavaPath}";
        }

        if (result.RequiredJavaMajor > 0)
        {
            message += $"\n建议安装 Java {result.RequiredJavaMajor}。";
        }

        var dialog = new ContentDialog
        {
            Title = title,
            Content = new TextBlock
            {
                Text = message,
                TextWrapping = TextWrapping.Wrap,
            },
            PrimaryButtonText = "继续启动",
            SecondaryButtonText = "去安装 Java",
            CloseButtonText = "取消",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = xamlRoot,
        };

        var choice = await dialog.ShowAsync();
        if (choice == ContentDialogResult.Secondary)
        {
            frame.Navigate(typeof(SettingsPage));
            return false;
        }

        return choice == ContentDialogResult.Primary;
    }
}
