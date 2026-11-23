namespace LiteMCLauncher.Services;

/// <summary>
/// Microsoft OAuth settings used by <see cref="MicrosoftAuthService"/>.
/// </summary>
public static class MicrosoftAuthOptions
{
    /// <summary>
    /// Public client ID used by the CmlLib authentication samples.
    /// For a production build, register your own Azure application with
    /// "Allow public client flows" enabled and ask Microsoft to allow it for
    /// Minecraft authentication, then replace this value (or set the
    /// <c>LITMC_MS_CLIENT_ID</c> environment variable).
    /// </summary>
    public const string DefaultClientId = "499c8d36-be2a-4231-9ebd-ef291b7bb64c";

    public static string ClientId
    {
        get
        {
            var custom = Environment.GetEnvironmentVariable("LITMC_MS_CLIENT_ID");
            return string.IsNullOrWhiteSpace(custom) ? DefaultClientId : custom;
        }
    }
}
