using System.Net;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;
using UtilityMenuSite.Tests.Infrastructure;

namespace UtilityMenuSite.Tests.Integration;

/// <summary>
/// Verifies that all dashboard routes are protected by authentication.
/// An unauthenticated GET to any dashboard page must redirect to the login page
/// rather than rendering (which would expose user data) or returning 404/500.
/// </summary>
public class DashboardRouteAuthTests(CustomWebApplicationFactory factory)
    : IClassFixture<CustomWebApplicationFactory>
{
    // AllowAutoRedirect = false so we can assert on the 302 itself,
    // not on the eventual destination.
    private readonly HttpClient _client = factory.CreateClient(
        new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    public static TheoryData<string> DashboardRoutes =>
        new()
        {
            "/dashboard",
            "/dashboard/licence",
            "/dashboard/billing",
            "/dashboard/devices",
            "/dashboard/settings"
        };

    [Theory]
    [MemberData(nameof(DashboardRoutes))]
    public async Task DashboardRoute_WhenUnauthenticated_RedirectsToLogin(string route)
    {
        var response = await _client.GetAsync(route);

        response.StatusCode.Should().Be(HttpStatusCode.Redirect,
            $"unauthenticated GET to {route} must redirect to login rather than rendering");

        var location = response.Headers.Location?.ToString();
        location.Should().NotBeNullOrEmpty();
        location.Should().Contain("login",
            $"the redirect from {route} should point to the login page");
    }
}
