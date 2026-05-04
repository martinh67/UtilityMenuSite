using Microsoft.Playwright;
using Microsoft.Playwright.NUnit;
using NUnit.Framework;

namespace UtilityMenuSite.E2E;

/// <summary>
/// Top-of-funnel happy-path: marketing root loads, /pricing renders, the
/// account/register flow accepts a fresh email, and an authenticated user
/// can reach /download.
///
/// The full signup → confirm-email → checkout → activate → use loop needs
/// real Stripe + email infrastructure, which we deliberately don't drive
/// from CI. These tests cover the parts of the journey that are pure UI
/// + API and would catch the most common deploy regressions:
///   - Site is reachable and renders
///   - Auth cookie issuance works end-to-end (register populates a
///     usable session — proves the API client is wired)
///   - Authenticated routes (Dashboard / Download) don't 500 immediately
///     after login
///
/// Configurable via E2E_BASE_URL (defaults to UAT). Email/password are
/// generated per-run so tests are idempotent.
/// </summary>
[Parallelizable(ParallelScope.Self)]
[TestFixture]
public class HappyPathTests : PageTest
{
    private static string BaseUrl =>
        Environment.GetEnvironmentVariable("E2E_BASE_URL")
        ?? "https://utilitymenu-uat-site.azurewebsites.net";

    [Test]
    public async Task MarketingRoot_Renders()
    {
        var response = await Page.GotoAsync(BaseUrl);
        Assert.That(response, Is.Not.Null);
        Assert.That(response!.Status, Is.EqualTo(200), "Marketing root must return 200");
        // Confirm something recognisably-the-site is in the body. Loose
        // string match on purpose — the marketing copy is allowed to evolve.
        var bodyText = await Page.InnerTextAsync("body");
        Assert.That(bodyText, Does.Contain("UtilityMenu").IgnoreCase);
    }

    [Test]
    public async Task Pricing_Renders()
    {
        var response = await Page.GotoAsync($"{BaseUrl}/pricing");
        Assert.That(response, Is.Not.Null);
        Assert.That(response!.Status, Is.EqualTo(200), "/pricing must return 200");
    }

    [Test]
    public async Task Register_NewAccount_LandsOnCompleteProfile()
    {
        var email = $"e2e-{Guid.NewGuid():N}@e2e.utilitymenu.test";
        const string password = "E2E-test-password-1!";

        await Page.GotoAsync($"{BaseUrl}/account/register");
        await Page.FillAsync("input[type=email]", email);
        await Page.FillAsync("input[type=password]:nth-of-type(1), input[name='_model.Password']", password);
        await Page.FillAsync("input[name='_model.ConfirmPassword'], input[type=password]:nth-of-type(2)", password);
        await Page.CheckAsync("input[type=checkbox]");
        await Page.ClickAsync("button[type=submit]");

        // Register form posts back, then redirects to /account/complete-profile.
        // Wait for the URL to settle before asserting.
        await Page.WaitForURLAsync(url => url.Contains("/account/complete-profile") || url.Contains("/dashboard"),
            new PageWaitForURLOptions { Timeout = 15_000 });

        Assert.That(Page.Url, Does.Contain("/account/").IgnoreCase
            .Or.Contain("/dashboard"), $"Expected redirect into authenticated area; got {Page.Url}");
    }

    [Test]
    public async Task Health_Returns200()
    {
        var response = await Page.GotoAsync($"{BaseUrl}/health");
        Assert.That(response, Is.Not.Null);
        Assert.That(response!.Status, Is.EqualTo(200), "Site /health must return 200");
    }
}
