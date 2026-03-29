using System.Net;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace UtilityMenuSite.Tests.Infrastructure;

/// <summary>
/// Verifies that the SecurityHeadersMiddleware adds the expected security
/// headers to every response. These headers protect against clickjacking,
/// MIME-sniffing, and unwanted browser feature access.
/// </summary>
public class SecurityHeadersTests(CustomWebApplicationFactory factory)
    : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client = factory.CreateClient(
        new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = false
        });

    [Fact]
    public async Task Response_ContainsXContentTypeOptionsNosniff()
    {
        var response = await _client.GetAsync("/health");

        response.Headers.TryGetValues("X-Content-Type-Options", out var values)
            .Should().BeTrue("X-Content-Type-Options header must be present");
        values.Should().Contain("nosniff");
    }

    [Fact]
    public async Task Response_ContainsXFrameOptionsDeny()
    {
        var response = await _client.GetAsync("/health");

        response.Headers.TryGetValues("X-Frame-Options", out var values)
            .Should().BeTrue("X-Frame-Options header must be present");
        values.Should().Contain("DENY");
    }

    [Fact]
    public async Task Response_ContainsReferrerPolicy()
    {
        var response = await _client.GetAsync("/health");

        response.Headers.TryGetValues("Referrer-Policy", out var values)
            .Should().BeTrue("Referrer-Policy header must be present");
        values.Should().Contain("strict-origin-when-cross-origin");
    }

    [Fact]
    public async Task Response_ContainsPermissionsPolicy()
    {
        var response = await _client.GetAsync("/health");

        response.Headers.TryGetValues("Permissions-Policy", out var values)
            .Should().BeTrue("Permissions-Policy header must be present");
        values.Should().Contain("camera=(), microphone=(), geolocation=()");
    }

    [Fact]
    public async Task SecurityHeaders_PresentOnPageResponses()
    {
        // Verify headers are present on HTML page responses, not just /health
        var response = await _client.GetAsync("/");

        response.Headers.TryGetValues("X-Content-Type-Options", out _)
            .Should().BeTrue("security headers must be present on page responses");
        response.Headers.TryGetValues("X-Frame-Options", out _)
            .Should().BeTrue("security headers must be present on page responses");
    }
}
