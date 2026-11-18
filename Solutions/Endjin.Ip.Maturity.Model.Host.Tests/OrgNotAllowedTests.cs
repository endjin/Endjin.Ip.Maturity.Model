using Endjin.Ip.Maturity.Model.Host.Tests.TestData;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Shouldly;

namespace Endjin.Ip.Maturity.Model.Host.Tests;

/// <summary>
/// A deployment evaluates IMMs only for repositories owned by the organizations it allows. For any
/// other organization, the badges are light grey and say "org not allowed", and the showbadges
/// page has status 403 and says how to get the organization allowed. Both set X-Imm-Error to
/// org-not-allowed. Nothing is fetched from GitHub, so the deployment cannot be used to fetch
/// content from any repository.
/// </summary>
[TestClass]
public class OrgNotAllowedTests
{
    private const string OtherOrg = "contoso";

    [TestMethod]
    public async Task GitHubImmTotalScore_OrgNotAllowed_ReturnsAnErrorBadgeWithoutFetchingFromGitHub()
    {
        // Arrange
        ImmFunctionFixture fixture = new(HostYaml.TwoRuleSet, HostYaml.BothRulesScored);
        HttpRequest request = ImmFunctionFixture.CreateRequest();

        // Act
        IActionResult result = await fixture.Functions.GitHubImmTotalScore(request, OtherOrg, ImmFunctionFixture.Project);

        // Assert
        ShouldBeOrgNotAllowedBadge(result, request, "IMM");
        ShouldNotHaveFetched(fixture);
    }

    [TestMethod]
    public async Task GitHubImmByRule_OrgNotAllowed_ReturnsAnErrorBadgeWithoutFetchingFromGitHub()
    {
        // Arrange
        ImmFunctionFixture fixture = new(HostYaml.TwoRuleSet, HostYaml.BothRulesScored);
        HttpRequest request = ImmFunctionFixture.CreateRequest();

        // Act
        IActionResult result = await fixture.Functions.GitHubImmByRule(
            request, OtherOrg, ImmFunctionFixture.Project, HostYaml.SharedEngineeringStandardsRuleId);

        // Assert
        ShouldBeOrgNotAllowedBadge(result, request, "IMM rule");
        ShouldNotHaveFetched(fixture);
    }

    // The organization is checked first, so a request for another organization learns nothing else.
    [TestMethod]
    public async Task GitHubImmByRule_OrgNotAllowedAndRuleIdNotAGuid_SaysOrgNotAllowed()
    {
        // Arrange
        ImmFunctionFixture fixture = new(HostYaml.TwoRuleSet, HostYaml.BothRulesScored);
        HttpRequest request = ImmFunctionFixture.CreateRequest();

        // Act
        IActionResult result = await fixture.Functions.GitHubImmByRule(request, OtherOrg, ImmFunctionFixture.Project, "not-a-guid");

        // Assert
        ShouldBeOrgNotAllowedBadge(result, request, "IMM rule");
    }

    [TestMethod]
    public async Task GitHubImmAllBadges_OrgNotAllowed_Returns403PageNamingTheSettingWithoutFetchingFromGitHub()
    {
        // Arrange
        ImmFunctionFixture fixture = new(HostYaml.TwoRuleSet, HostYaml.BothRulesScored);
        HttpRequest request = ImmFunctionFixture.CreateRequest();

        // Act
        IActionResult result = await fixture.Functions.GitHubImmAllBadges(request, OtherOrg, ImmFunctionFixture.Project);

        // Assert
        ContentResult content = result.ShouldBeOfType<ContentResult>();
        content.StatusCode.ShouldBe(StatusCodes.Status403Forbidden);
        content.ContentType.ShouldBe("text/html");
        content.Content!.ShouldContain(ImmBadges.RenderErrorBadge("IMM", "org not allowed"));
        content.Content!.ShouldContain(OtherOrg);
        content.Content!.ShouldContain("Imm:AllowedGitHubOrganizations");
        IHeaderDictionary headers = request.HttpContext.Response.Headers;
        headers["X-Imm-Error"].ToString().ShouldBe("org-not-allowed");
        headers.CacheControl.ToString().ShouldBe("no-cache, no-store, must-revalidate");
        ShouldNotHaveFetched(fixture);
    }

    [TestMethod]
    public async Task GitHubImmTotalScore_OrgNotAllowed_LogsAtInformationLevel()
    {
        // Arrange
        ImmFunctionFixture fixture = new(HostYaml.TwoRuleSet, HostYaml.BothRulesScored);

        // Act
        await fixture.Functions.GitHubImmTotalScore(ImmFunctionFixture.CreateRequest(), OtherOrg, ImmFunctionFixture.Project);

        // Assert
        (LogLevel level, string message, Exception? exception) = fixture.Logger.Entries.ShouldHaveSingleItem();
        level.ShouldBe(LogLevel.Information);
        message.ShouldContain("org-not-allowed");
        message.ShouldContain(OtherOrg);
        exception.ShouldBeNull();
    }

    [TestMethod]
    public async Task GitHubImmTotalScore_OrgInConfiguredList_ReturnsTheScore()
    {
        // Arrange
        ImmFunctionFixture fixture = new(
            HostYaml.TwoRuleSet, HostYaml.BothRulesScored, AllowedGitHubOrganizations.Only([OtherOrg]));
        HttpRequest request = ImmFunctionFixture.CreateRequest();

        // Act
        IActionResult result = await fixture.Functions.GitHubImmTotalScore(request, OtherOrg, ImmFunctionFixture.Project);

        // Assert
        result.ShouldBeOfType<ContentResult>().StatusCode.ShouldBe(StatusCodes.Status200OK);
        request.HttpContext.Response.Headers.ContainsKey("X-Imm-Error").ShouldBeFalse();
        await fixture.ImmSource.Received(1).GetIpMaturityModelAsync(OtherOrg, ImmFunctionFixture.Project, Arg.Any<string>());
    }

    [TestMethod]
    public async Task GitHubImmTotalScore_ConfiguredListWithoutTheOrg_RefusesEvenAnOrgInTheDefaults()
    {
        // Arrange
        ImmFunctionFixture fixture = new(
            HostYaml.TwoRuleSet, HostYaml.BothRulesScored, AllowedGitHubOrganizations.Only([OtherOrg]));
        HttpRequest request = ImmFunctionFixture.CreateRequest();

        // Act
        IActionResult result = await fixture.Functions.GitHubImmTotalScore(request, "endjin", ImmFunctionFixture.Project);

        // Assert
        ShouldBeOrgNotAllowedBadge(result, request, "IMM");
    }

    [TestMethod]
    public async Task GitHubImmTotalScore_AnyOrgAllowed_ReturnsTheScore()
    {
        // Arrange
        ImmFunctionFixture fixture = new(HostYaml.TwoRuleSet, HostYaml.BothRulesScored, AllowedGitHubOrganizations.Any);
        HttpRequest request = ImmFunctionFixture.CreateRequest();

        // Act
        IActionResult result = await fixture.Functions.GitHubImmTotalScore(request, OtherOrg, ImmFunctionFixture.Project);

        // Assert
        result.ShouldBeOfType<ContentResult>().StatusCode.ShouldBe(StatusCodes.Status200OK);
        request.HttpContext.Response.Headers.ContainsKey("X-Imm-Error").ShouldBeFalse();
    }

    [TestMethod]
    public async Task GitHubImmTotalScore_OrgInDefaultsWithDifferentCase_ReturnsTheScore()
    {
        // Arrange
        ImmFunctionFixture fixture = new(HostYaml.TwoRuleSet, HostYaml.BothRulesScored);
        HttpRequest request = ImmFunctionFixture.CreateRequest();

        // Act
        IActionResult result = await fixture.Functions.GitHubImmTotalScore(request, "Ais-DotNet", ImmFunctionFixture.Project);

        // Assert
        result.ShouldBeOfType<ContentResult>().StatusCode.ShouldBe(StatusCodes.Status200OK);
        request.HttpContext.Response.Headers.ContainsKey("X-Imm-Error").ShouldBeFalse();
    }

    private static void ShouldBeOrgNotAllowedBadge(IActionResult result, HttpRequest request, string subject)
    {
        ContentResult content = result.ShouldBeOfType<ContentResult>();
        content.StatusCode.ShouldBe(StatusCodes.Status200OK);
        content.ContentType.ShouldBe("image/svg+xml");
        content.Content.ShouldBe(ImmBadges.RenderErrorBadge(subject, "org not allowed"));
        request.HttpContext.Response.Headers["X-Imm-Error"].ToString().ShouldBe("org-not-allowed");
    }

    private static void ShouldNotHaveFetched(ImmFunctionFixture fixture)
    {
        fixture.RuleDefinitionSource.ReceivedCalls().ShouldBeEmpty();
        fixture.ImmSource.ReceivedCalls().ShouldBeEmpty();
    }
}
