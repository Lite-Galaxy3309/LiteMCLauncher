using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace LiteMCLauncher.Pages;

public sealed partial class CoordinatePage : Page
{
    public CoordinatePage()
    {
        InitializeComponent();
    }

    private void Back_Click(object sender, RoutedEventArgs e)
    {
        if (Frame.CanGoBack)
        {
            Frame.GoBack();
        }
        else
        {
            Frame.Navigate(typeof(ToolsPage));
        }
    }

    private void OverworldToNether_Click(object sender, RoutedEventArgs e)
    {
        if (!TryRead(OverworldX, OverworldZ, out var x, out var z))
        {
            OverworldToNetherResult.Text = "请输入有效的 X 和 Z 坐标。";
            return;
        }

        OverworldToNetherResult.Text = $"下界坐标：X {Format(x / 8)} / Z {Format(z / 8)}";
    }

    private void NetherToOverworld_Click(object sender, RoutedEventArgs e)
    {
        if (!TryRead(NetherX, NetherZ, out var x, out var z))
        {
            NetherToOverworldResult.Text = "请输入有效的 X 和 Z 坐标。";
            return;
        }

        NetherToOverworldResult.Text = $"主世界坐标：X {Format(x * 8)} / Z {Format(z * 8)}";
    }

    private void Distance_Click(object sender, RoutedEventArgs e)
    {
        if (!TryRead(FromX, FromZ, out var fromX, out var fromZ) ||
            !TryRead(ToX, ToZ, out var toX, out var toZ))
        {
            DistanceResult.Text = "请输入有效的起点和终点坐标。";
            return;
        }

        var dx = toX - fromX;
        var dz = toZ - fromZ;
        var distance = Math.Sqrt((dx * dx) + (dz * dz));
        DistanceResult.Text = $"直线距离：{Format(distance)} 格";
    }

    private static bool TryRead(NumberBox xBox, NumberBox zBox, out double x, out double z)
    {
        x = xBox.Value;
        z = zBox.Value;
        return !double.IsNaN(x) && !double.IsInfinity(x) &&
               !double.IsNaN(z) && !double.IsInfinity(z);
    }

    private static string Format(double value)
    {
        return Math.Round(value, 2).ToString("0.##");
    }
}
