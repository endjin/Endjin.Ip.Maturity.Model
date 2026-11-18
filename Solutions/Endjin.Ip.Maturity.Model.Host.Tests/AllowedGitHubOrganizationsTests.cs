using Endjin.Ip.Maturity.Model.Host.Tests.TestData;
using Microsoft.Extensions.Configuration;
using Shouldly;

namespace Endjin.Ip.Maturity.Model.Host.Tests;

/// <summary>
/// A deployment evaluates IMMs only for repositories owned by the GitHub organizations that its
/// Imm:AllowedGitHubOrganizations setting lists. The Host's appsettings.json lists endjin's open
/// source estate. An app setting (Imm__AllowedGitHubOrganizations) whose value is names separated
/// by commas, semicolons or white space replaces the whole list. '*' allows every organization.
/// A missing or empty list stops the Host starting. GitHub names are not case sensitive, so
/// neither is the list.
/// </summary>
[TestClass]
public class AllowedGitHubOrganizationsTests
{
    private static readonly string[] OssEstate =
    [
        "endjin",
        "corvus-dotnet",
        "ais-dotnet",
        "marain-dotnet",
        "menes-dotnet",
        "argotic-syndication-framework",
        "zerofailed",
        "reaqtive",
    ];

    [TestMethod]
    public void ShippedAppSettings_AllowEndjinsOpenSourceEstateOnly()
    {
        // Act
        AllowedGitHubOrganizations allowed = HostSettings.AllowedOrganizations();

        // Assert
        allowed.AllowsAny.ShouldBeFalse();
        allowed.Organizations.ShouldBe(OssEstate, ignoreOrder: true);
        allowed.IsAllowed("microsoft").ShouldBeFalse();
    }

    [TestMethod]
    [DataRow("contoso,fabrikam")]
    [DataRow("contoso;fabrikam")]
    [DataRow("contoso fabrikam")]
    [DataRow(" contoso , fabrikam ")]
    [DataRow("contoso,\nfabrikam,")]
    public void AppSettingOverride_ReplacesTheWholeShippedList(string value)
    {
        // Arrange
        IConfiguration configuration = HostSettings.Load((AllowedGitHubOrganizations.ConfigurationKey, value));

        // Act
        AllowedGitHubOrganizations allowed = AllowedGitHubOrganizations.FromConfiguration(configuration);

        // Assert
        // The shipped list has eight entries; an override with two must not merge with them by index.
        allowed.AllowsAny.ShouldBeFalse();
        allowed.Organizations.ShouldBe(["contoso", "fabrikam"], ignoreOrder: true);
        allowed.IsAllowed("endjin").ShouldBeFalse();
        allowed.IsAllowed("reaqtive").ShouldBeFalse();
    }

    // Environment variables use '__' for ':', which is how an app setting reaches the configuration.
    [TestMethod]
    public void AppSettingOverride_FromAnEnvironmentVariableName_ReplacesTheShippedList()
    {
        // Arrange
        IConfiguration configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json")
            .AddInMemoryCollection([KeyValuePair.Create<string, string?>(
                AllowedGitHubOrganizations.AppSettingName.Replace("__", ConfigurationPath.KeyDelimiter, StringComparison.Ordinal), "contoso")])
            .Build();

        // Act
        AllowedGitHubOrganizations allowed = AllowedGitHubOrganizations.FromConfiguration(configuration);

        // Assert
        allowed.Organizations.ShouldBe(["contoso"]);
    }

    [TestMethod]
    [DataRow("*")]
    [DataRow(" * ")]
    [DataRow("endjin,*")]
    public void AppSettingOverride_Star_AllowsEveryOrganization(string value)
    {
        // Arrange
        IConfiguration configuration = HostSettings.Load((AllowedGitHubOrganizations.ConfigurationKey, value));

        // Act
        AllowedGitHubOrganizations allowed = AllowedGitHubOrganizations.FromConfiguration(configuration);

        // Assert
        allowed.AllowsAny.ShouldBeTrue();
        allowed.Organizations.ShouldBeEmpty();
        allowed.IsAllowed("microsoft").ShouldBeTrue();
    }

    [TestMethod]
    public void Array_IsReadEntryByEntry()
    {
        // Arrange
        IConfiguration configuration = Configuration(
            ($"{AllowedGitHubOrganizations.ConfigurationKey}:0", "contoso"),
            ($"{AllowedGitHubOrganizations.ConfigurationKey}:1", "fabrikam"));

        // Act
        AllowedGitHubOrganizations allowed = AllowedGitHubOrganizations.FromConfiguration(configuration);

        // Assert
        allowed.Organizations.ShouldBe(["contoso", "fabrikam"], ignoreOrder: true);
    }

    [TestMethod]
    public void NotConfigured_IsAConfigurationErrorNamingTheSetting()
    {
        // Act
        InvalidOperationException exception = Should.Throw<InvalidOperationException>(
            () => AllowedGitHubOrganizations.FromConfiguration(Configuration()));

        // Assert
        exception.Message.ShouldContain(AllowedGitHubOrganizations.ConfigurationKey);
        exception.Message.ShouldContain(AllowedGitHubOrganizations.AppSettingName);
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("   ")]
    [DataRow(" , ; ")]
    public void EmptyArrayEntries_AreAConfigurationError(string value)
    {
        // Arrange
        IConfiguration configuration = Configuration(($"{AllowedGitHubOrganizations.ConfigurationKey}:0", value));

        // Act & Assert
        Should.Throw<InvalidOperationException>(() => AllowedGitHubOrganizations.FromConfiguration(configuration));
    }

    [TestMethod]
    [DataRow("Endjin")]
    [DataRow("ENDJIN")]
    [DataRow("Corvus-DotNet")]
    public void IsAllowed_IgnoresCase(string org)
    {
        HostSettings.AllowedOrganizations().IsAllowed(org).ShouldBeTrue();
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("endjin-fork")]
    [DataRow("endji")]
    public void IsAllowed_NameNotExactlyInTheList_IsFalse(string org)
    {
        HostSettings.AllowedOrganizations().IsAllowed(org).ShouldBeFalse();
    }

    private static IConfiguration Configuration(params (string Key, string? Value)[] settings) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(settings.Select(s => KeyValuePair.Create(s.Key, s.Value)))
            .Build();
}
