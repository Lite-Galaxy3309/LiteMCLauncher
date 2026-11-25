namespace LiteMCLauncher.Models;

public sealed class SeedVersionGroup
{
    public SeedVersionGroup(string display, string representativeKey, bool isApproximate = false)
    {
        Display = display;
        RepresentativeKey = representativeKey;
        IsApproximate = isApproximate;
    }

    public string Display { get; set; }

    public string RepresentativeKey { get; set; }

    public bool IsApproximate { get; set; }
}

public sealed class SeedStructureOption
{
    public SeedStructureOption(string key, string display)
    {
        Key = key;
        Display = display;
    }

    public string Key { get; set; }

    public string Display { get; set; }
}
