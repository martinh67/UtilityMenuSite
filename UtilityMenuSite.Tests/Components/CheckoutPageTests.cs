using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.JSInterop;
using System.Security.Claims;
using UtilityMenuSite.Components.Pages;

namespace UtilityMenuSite.Tests.Components;

/// <summary>
/// Component tests for the Pro upgrade page (Checkout.razor). The page is the
/// Site-side entrypoint for P0 Customer Journey D (upgrade) — it gathers the
/// user profile, then calls IUtilityMenuApiClient.CreateCheckoutAsync.
///
/// Audit P1 #10 / #13: Site has no Blazor component tests for any
/// user-facing flow. This re-introduces the test project (Phase 3c demolition
/// deleted it) with a bUnit setup and pins down the upgrade-page contract.
/// </summary>
public class CheckoutPageTests : TestContext
{
    private readonly Mock<IUtilityMenuApiClient> _api = new();
    private readonly FakeAuthenticationStateProvider _auth;

    public CheckoutPageTests()
    {
        _auth = new FakeAuthenticationStateProvider();
        Services.AddSingleton<IUtilityMenuApiClient>(_api.Object);
        Services.AddSingleton<AuthenticationStateProvider>(_auth);
        Services.AddAuthorizationCore();
        // bUnit ships an in-memory IJSRuntime by default.

        // Default: profile fetch succeeds with a confirmed user.
        _api.Setup(a => a.GetProfileAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(ApiResult<ProfileDto>.Success(new ProfileDto(
                IdentityId: Guid.NewGuid().ToString(),
                UserId: Guid.NewGuid(),
                Email: "checkout@example.com",
                DisplayName: "Test User",
                Organisation: null,
                JobRole: null,
                UsageInterests: null,
                ProfileCompletedAt: DateTime.UtcNow,
                ApiToken: "tok",
                CreatedAt: DateTime.UtcNow.AddDays(-7))));
    }

    private void NavigateWithPlan(string plan)
    {
        var nav = Services.GetRequiredService<NavigationManager>();
        nav.NavigateTo(nav.GetUriWithQueryParameter("plan", plan));
    }

    [Fact]
    public void Checkout_RendersOrderSummary_ForMonthlyPlan()
    {
        _auth.SignInAs("checkout@example.com");
        NavigateWithPlan("monthly");

        var cut = RenderComponent<Checkout>();

        cut.Markup.Should().Contain("Complete Your Order");
        cut.Markup.Should().Contain("£5");
        cut.Markup.Should().Contain("Billed monthly");
    }

    [Fact]
    public void Checkout_RendersAnnualPriceAndSavingsCallout_WhenPlanIsAnnual()
    {
        _auth.SignInAs("checkout@example.com");
        NavigateWithPlan("annual");

        var cut = RenderComponent<Checkout>();

        cut.Markup.Should().Contain("£45");
        cut.Markup.Should().Contain("Billed annually");
        cut.Markup.Should().Contain("save");
    }

    [Fact]
    public async Task Checkout_ProceedToStripe_CallsApiWithCorrectPlanAndEmail()
    {
        _auth.SignInAs("checkout@example.com");
        NavigateWithPlan("annual");

        _api.Setup(a => a.CreateCheckoutAsync(It.IsAny<CreateCheckoutRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ApiResult<CreateCheckoutResponse>.Success(
                new CreateCheckoutResponse("cs_test_123", "https://stripe.example/checkout/abc")));

        var cut = RenderComponent<Checkout>();

        await cut.Find("button.btn-primary.btn-lg").ClickAsync(new());

        _api.Verify(a => a.CreateCheckoutAsync(
            It.Is<CreateCheckoutRequest>(r =>
                r.PlanType == "annual" &&
                r.Email == "checkout@example.com" &&
                r.SuccessUrl.Contains("session_id={CHECKOUT_SESSION_ID}") &&
                r.CancelUrl.EndsWith("/pricing")),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Checkout_ApiFailure_ShowsErrorMessage()
    {
        _auth.SignInAs("checkout@example.com");
        NavigateWithPlan("monthly");

        _api.Setup(a => a.CreateCheckoutAsync(It.IsAny<CreateCheckoutRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ApiResult<CreateCheckoutResponse>.Failure("STRIPE_ERROR", "boom"));

        var cut = RenderComponent<Checkout>();

        await cut.Find("button.btn-primary.btn-lg").ClickAsync(new());

        cut.Markup.Should().Contain("Failed to start checkout");
    }

    private sealed class FakeAuthenticationStateProvider : AuthenticationStateProvider
    {
        private AuthenticationState _state =
            new(new ClaimsPrincipal(new ClaimsIdentity()));

        public void SignInAs(string email)
        {
            var identity = new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.Name, email),
                new Claim(ClaimTypes.Email, email)
            }, authenticationType: "Test");
            _state = new AuthenticationState(new ClaimsPrincipal(identity));
            NotifyAuthenticationStateChanged(Task.FromResult(_state));
        }

        public override Task<AuthenticationState> GetAuthenticationStateAsync()
            => Task.FromResult(_state);
    }
}
