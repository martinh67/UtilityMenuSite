using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace UtilityMenuSite.Tests.Infrastructure;

/// <summary>
/// Validates production configuration files contain the expected security settings.
/// These tests read the raw JSON files to catch configuration drift before deployment.
/// </summary>
public class ProductionConfigTests
{
    private static string GetConfigPath(string filename)
    {
        // Walk up from test bin directory to find the site project root
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "appsettings.json")))
            dir = dir.Parent;

        dir.Should().NotBeNull("could not find project root with appsettings.json");
        return Path.Combine(dir!.FullName, filename);
    }

    [Fact]
    public void ProductionConfig_AllowedHosts_IsNotWildcard()
    {
        var path = GetConfigPath("appsettings.Production.json");
        var json = File.ReadAllText(path);
        var doc = JsonDocument.Parse(json);

        doc.RootElement.TryGetProperty("AllowedHosts", out var allowedHosts)
            .Should().BeTrue("appsettings.Production.json must define AllowedHosts");

        var value = allowedHosts.GetString();
        value.Should().NotBe("*", "production must not use wildcard AllowedHosts");
        value.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void ProductionConfig_AllowedHosts_IncludesAzureDomain()
    {
        var path = GetConfigPath("appsettings.Production.json");
        var json = File.ReadAllText(path);
        var doc = JsonDocument.Parse(json);

        var value = doc.RootElement.GetProperty("AllowedHosts").GetString()!;
        value.Should().Contain("utilitymenu-prod.azurewebsites.net",
            "production AllowedHosts must include the Azure App Service domain");
    }

    [Fact]
    public void ProductionConfig_StripeKeys_AreEmpty()
    {
        var path = GetConfigPath("appsettings.Production.json");
        var json = File.ReadAllText(path);
        var doc = JsonDocument.Parse(json);

        var stripe = doc.RootElement.GetProperty("Stripe");
        stripe.GetProperty("SecretKey").GetString().Should().BeEmpty(
            "Stripe SecretKey must not be hardcoded in production config — inject via environment");
        stripe.GetProperty("WebhookSecret").GetString().Should().BeEmpty(
            "Stripe WebhookSecret must not be hardcoded in production config — inject via environment");
    }

    [Fact]
    public void ProductionConfig_ConnectionString_IsEmpty()
    {
        var path = GetConfigPath("appsettings.Production.json");
        var json = File.ReadAllText(path);
        var doc = JsonDocument.Parse(json);

        var connStr = doc.RootElement
            .GetProperty("ConnectionStrings")
            .GetProperty("DefaultConnection")
            .GetString();

        connStr.Should().BeEmpty(
            "connection string must not be hardcoded in production config — inject via environment");
    }

    [Fact]
    public void ProductionConfig_LogLevel_IsWarningOrHigher()
    {
        var path = GetConfigPath("appsettings.Production.json");
        var json = File.ReadAllText(path);
        var doc = JsonDocument.Parse(json);

        var defaultLevel = doc.RootElement
            .GetProperty("Logging")
            .GetProperty("LogLevel")
            .GetProperty("Default")
            .GetString();

        defaultLevel.Should().Be("Warning",
            "production default log level should be Warning to reduce noise");
    }
}
