using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace LiteMCLauncher.Pages;

public sealed partial class ToolsPage : Page
{
    public ToolsPage()
    {
        InitializeComponent();
    }

    private void CoordinateTool_Click(object sender, RoutedEventArgs e)
    {
        Frame.Navigate(typeof(CoordinatePage));
    }

    private void SeedFinderTool_Click(object sender, RoutedEventArgs e)
    {
        Frame.Navigate(typeof(SeedFinderPage));
    }

    private void BiomeMapTool_Click(object sender, RoutedEventArgs e)
    {
        Frame.Navigate(typeof(BiomeMapPage));
    }
}
