using Microsoft.Extensions.Configuration;

namespace Endjin.Ip.Maturity.Model.Host.Tests.TestData;

/// <summary>
/// The Host's appsettings.json, which the build copies next to the tests.
/// </summary>
internal static class HostSettings
{
    /// <summary>
    /// Reads the Host's appsettings.json, optionally with settings layered over it as app settings
    /// would be.
    /// </summary>
    /// <param name="overrides">Settings that take precedence over the file.</param>
    public static IConfiguration Load(params (string Key, string? Value)[] overrides) =>
        new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false)
            .AddInMemoryCollection(overrides.Select(o => KeyValuePair.Create(o.Key, o.Value)))
            .Build();

    /// <summary>
    /// Gets the GitHub organizations that the Host allows as shipped.
    /// </summary>
    public static AllowedGitHubOrganizations AllowedOrganizations() => AllowedGitHubOrganizations.FromConfiguration(Load());
}
