using Windows.UI;

namespace LiteMCLauncher.Models;

public sealed record BiomeMapResult(
    byte[] ImageBytes,
    int Width,
    int Height,
    double MinX,
    double MinZ,
    double MaxX,
    double MaxZ,
    IReadOnlyList<(string Name, Color Color)> Legend,
    bool IsApproximate);
