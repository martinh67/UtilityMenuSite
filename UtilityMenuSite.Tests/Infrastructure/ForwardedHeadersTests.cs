using System.Net;
using System.Text.RegularExpressions;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace UtilityMenuSite.Tests.Infrastructure;

/// <summary>
/// Verifies that X-Forwarded-Proto / X-Forwarded-For headers from Azure App Service's
/// reverse proxy are correctly honoured by the middleware pipeline.
///
/// Without UseForwardedHeaders(), the app sees every request as HTTP (Azure terminates
/// SSL externally), which breaks antiforgery validation because the cookie is issued
/// with Secure=true (the scheme is "https") but the app can't correlate it on
/// subsequent requests it still sees as HTTP.
/// </summary>
public class ForwardedHeadersTests(CustomWebApplicationFactory factory)
    : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client = factory.CreateClient(
        new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = false
        });

    /// <summary>
    /// Core claim: UseForwardedHeaders must propagate X-Forwarded-Proto to
    /// HttpContext.Request.Scheme. We verify this by running the real middleware
    /// pipeline via TestServer.SendAsync, which returns the HttpContext after all
    /// middleware (including ForwardedHeadersMiddleware) has executed.
    /// </summary>
    [Fact]
    public async Task ForwardedHeaders_WithXForwardedProtoHttps_SetsRequestSchemeToHttps()
    {
        var context = await factory.Server.SendAsync(ctx =>
        {
            ctx.Request.Method = "GET";
            ctx.Request.Path = "/health";
            ctx.Request.Headers["X-Forwarded-Proto"] = "https";
            ctx.Request.Headers["X-Forwarded-For"] = "203.0.113.42";
        });

        context.Request.Scheme.Should().Be("https",
            "UseForwardedHeaders must propagate X-Forwarded-Proto: https to " +
            "HttpContext.Request.Scheme so that antiforgery and auth cookies " +
            "are issued and validated with the correct scheme");
    }

    [Fact]
    public async Task ForwardedHeaders_WithoutXForwardedProto_SchemeRemainsHttp()
    {
        var context = await factory.Server.SendAsync(ctx =>
        {
            ctx.Request.Method = "GET";
            ctx.Request.Path = "/health";
        });

        context.Request.Scheme.Should().Be("http",
            "without a forwarded proto header the scheme should remain http (the test transport)");
    }

    [Fact]
    public async Task RegisterPage_WithAzureForwardedHeaders_Returns200()
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/account/register");
        AddAzureProxyHeaders(request);

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK,
            "the register page must load successfully when Azure proxy headers are present — " +
            "a 3xx here would mean UseHttpsRedirection is incorrectly redirecting because " +
            "UseForwardedHeaders hasn't set the scheme to https yet");
    }

    [Fact]
    public async Task RegisterPage_WithoutForwardedHeaders_Returns200()
    {
        var response = await _client.GetAsync("/account/register");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task RegisterPage_RendersAntiforgeryToken()
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/account/register");
        AddAzureProxyHeaders(request);

        var response = await _client.SendAsync(request);
        var html = await response.Content.ReadAsStringAsync();

        var token = ExtractAntiforgeryToken(html);
        token.Should().NotBeNullOrEmpty(
            "the register form must include a __RequestVerificationToken hidden input " +
            "so browsers can submit it with the antiforgery cookie");
    }

    [Fact]
    public async Task RegisterFormPost_WithoutAntiforgeryToken_Returns400()
    {
        // Proves antiforgery enforcement is active in the pipeline.
        var response = await _client.PostAsync("/account/register",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["_model.Email"] = "no-token@example.com"
            }));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest,
            "form submissions without an antiforgery token must be rejected with 400");
    }

    [Fact]
    public async Task HealthEndpoint_WithAzureForwardedHeaders_Returns200()
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/health");
        AddAzureProxyHeaders(request);

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static void AddAzureProxyHeaders(HttpRequestMessage request)
    {
        request.Headers.TryAddWithoutValidation("X-Forwarded-Proto", "https");
        request.Headers.TryAddWithoutValidation("X-Forwarded-For", "203.0.113.42");
    }

    private static string? ExtractAntiforgeryToken(string html)
    {
        var inputMatch = Regex.Match(
            html,
            @"<input[^>]*name=""__RequestVerificationToken""[^>]*>",
            RegexOptions.IgnoreCase);

        if (!inputMatch.Success) return null;

        var valueMatch = Regex.Match(
            inputMatch.Value,
            @"value=""([^""]+)""",
            RegexOptions.IgnoreCase);

        return valueMatch.Success ? valueMatch.Groups[1].Value : null;
    }
}
