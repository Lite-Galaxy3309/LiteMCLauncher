using LiteMCLauncher.Services;
using Microsoft.UI.Xaml.Media;

namespace LiteMCLauncher.Models;

public sealed class BiomeLegendItem
{
    public BiomeLegendItem(string name, Windows.UI.Color color)
    {
        Name = name;
        Fill = new SolidColorBrush(color);
    }

    public string Name { get; set; }

    public SolidColorBrush Fill { get; set; }
}
