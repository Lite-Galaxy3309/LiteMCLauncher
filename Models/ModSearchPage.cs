namespace LiteMCLauncher.Models;

/// <summary>
/// One page of Modrinth search results.
/// </summary>
public sealed record ModSearchPage(
    IReadOnlyList<ModSearchResult> Results,
    int Offset,
    int Limit,
    int TotalHits)
{
    public int TotalPages => Limit <= 0 ? 0 : Math.Max(1, (int)Math.Ceiling(TotalHits / (double)Limit));
}
