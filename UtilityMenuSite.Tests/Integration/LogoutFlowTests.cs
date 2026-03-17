using System.Net;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;
using UtilityMenuSite.Tests.Infrastructure;

namespace UtilityMenuSite.Tests.Integration;

/// <summary>
/// Verifies the GET-based logout route works correctly.
///
/// Background: logout navbars run inside InteractiveServer components, where
/// &lt;AntiforgeryToken /&gt; generates tokens that don't match the SSR antiforgery
/// cookie — causing a 400 when using a POST form. The fix was to change logout
/// to a GET via NavigationManager, removing antiforgery from the equation.
/// These tests confirm that the logout route:
///   1. Redirects to the home page (not a blank screen or error)
///   2. Is reachable without authentication (no 401/403 blocking it)
///   3. Does not redirect to the login page (i.e. is not itself auth-protected)
/// </summary>
public class LogoutFlowTests(CustomWebApplicationFactory factory)
    : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client = factory.CreateClient(
        new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    [Fact]
    public async Task Logout_Get_RedirectsToHomePage()
    {
        var response = await _client.GetAsync("/account/logout");

        response.StatusCode.Should().Be(HttpStatusCode.Redirect,
            "GET /account/logout must redirect rather than render a blank page or return an error");

        response.Headers.Location?.AbsolutePath.Should().Be("/",
            "after signing out the user should land on the home page");
    }

    [Fact]
    public async Task Logout_DoesNotRequireAuthentication()
    {
        // An unauthenticated GET must not be blocked — it should still redirect to home,
        // not to the login page. Hitting logout while already signed out must be harmless.
        var response = await _client.GetAsync("/account/logout");

        var location = response.Headers.Location?.ToString();
        location.Should().NotContainEquivalentOf("login",
            "the logout route must not itself be auth-protected");
    }

    [Fact]
    public async Task Logout_DoesNotReturn400()
    {
        // Regression guard: the old POST-form logout returned 400 due to antiforgery
        // mismatch in InteractiveServer. GET must never produce that error.
        var response = await _client.GetAsync("/account/logout");

        response.StatusCode.Should().NotBe(HttpStatusCode.BadRequest,
            "a 400 here would indicate the antiforgery regression has returned");
    }
}
