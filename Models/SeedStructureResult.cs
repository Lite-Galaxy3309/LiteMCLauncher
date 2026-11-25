using LiteMCLauncher.Services;

namespace LiteMCLauncher.Models;

public sealed class SeedStructureResult
{
    public SeedStructureResult(string name, int x, int z, double distance)
    {
        Name = name;
        X = x;
        Z = z;
        Distance = distance;
    }

    public string Name { get; set; }

    public int X { get; set; }

    public int Z { get; set; }

    public double Distance { get; set; }

    public string DistanceDisplay => LauncherLanguageService.IsEnglish
        ? $"{Distance:0} blocks"
        : $"{Distance:0} 格";
}
