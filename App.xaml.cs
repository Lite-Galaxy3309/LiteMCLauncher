using LiteMCLauncher.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace LiteMCLauncher;

/// <summary>
/// Provides application-specific behavior to supplement the default Application class.
/// </summary>
public partial class App : Application
{
    private Window? _window;

    public static Window? MainWindow { get; private set; }

    /// <summary>
    /// Initializes the singleton application object.  This is the first line of authored code
    /// executed, and as such is the logical equivalent of main() or WinMain().
    /// </summary>
    public App()
    {
        InitializeComponent();
        UnhandledException += (_, args) =>
        {
            try
            {
                File.WriteAllText(
                    Path.Combine(Path.GetTempPath(), "lmcl-crash.txt"),
                    args.Exception.ToString());
            }
            catch
            {
            }
        };
    }

    /// <summary>
    /// Invoked when the application is launched.
    /// </summary>
    /// <param name="args">Details about the launch request and process.</param>
    protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
    {
        try
        {
            PersonalizationService.ApplyAccentOnly();
            _window = new MainWindow();
            MainWindow = _window;
            PersonalizationService.Apply();
            _window.Activate();

            // Refresh the saved account as soon as possible. HomePage awaits the
            // same initialization task before showing the account list.
            _ = AppServices.Auth.InitializeAsync();
            _ = AppServices.Music.InitializeAsync();
            _ = AppServices.OnlineMusic.InitializeAsync();
        }
        catch (Exception exception)
        {
            try
            {
                File.WriteAllText(
                    Path.Combine(Path.GetTempPath(), "lmcl-start-crash.txt"),
                    exception.ToString());
            }
            catch
            {
            }

            throw;
        }
    }
}
