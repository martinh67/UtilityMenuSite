# Phase 3 Continuation — UtilityMenuSite Refactor

**Status as of this commit**: Phase 3a complete. Foundation + Account flow rewired.

This file is the working punchlist for the rest of the Phase 3 refactor. Once everything below is done, this file should be deleted.

## What Phase 3a delivered

- **API client foundation** at `Services/Api/`:
  - `IUtilityMenuApiClient` + `UtilityMenuApiClient` with all eight `/api/v1/account/*` endpoints fully wired
  - `Core/Models/Api/{AuthRequests,AuthResponses,ApiResult}.cs` (DTOs that mirror UtilityMenuAPI's contract)
- **Auth infrastructure** at `Services/Auth/`:
  - `IJwtTokenStorage` + `HttpContextJwtTokenStorage` — reads access/refresh tokens from claims
  - `IAuthCookieIssuer` + `AuthCookieIssuer` — wraps `HttpContext.SignInAsync`/`SignOutAsync`, packs JWT + refresh into Identity's existing cookie scheme
  - `ApiAuthHandler` — `DelegatingHandler` attaching `Authorization: Bearer` and `x-api-key` to every outbound API call
- **Program.cs wiring** (additive — Identity stays in place during migration):
  - `ApiSettings` bound from `Api:*` config
  - `IHttpContextAccessor`, `IJwtTokenStorage`, `IAuthCookieIssuer`, `ApiAuthHandler`, typed `IUtilityMenuApiClient`
- **Account pages refactored** to call `IUtilityMenuApiClient` instead of `SignInManager`/`UserManager`:
  - `Login.razor`, `Register.razor`, `Logout.razor`, `ForgotPassword.razor`, `ResetPassword.razor`

The Site builds clean with zero warnings, both auth flows (cookie + Identity-EF + API) coexisting.

## What's still left in Phase 3

### 3b — Remaining page refactor

Pages still using `I*Service` / `UserManager` / `SignInManager` / `IBlogService` / etc. directly:

| Page | Current dependencies | API endpoint(s) needed |
|---|---|---|
| `Components/Pages/Account/CompleteProfile.razor` | `IUserService`, `UserManager` | `PUT /api/v1/account/profile` (NEW — needs adding to API) |
| `Components/Pages/Dashboard/Dashboard.razor` | `IUserService`, `ILicenceService`, `IVersionManifestService` | `GET /api/v1/dashboard/me`, `GET /api/v1/version/manifest` |
| `Components/Pages/Dashboard/Billing.razor` | `IUserService`, `IStripeService` | `GET /api/v1/dashboard/subscription`, `POST /api/v1/checkout/billing-portal` |
| `Components/Pages/Dashboard/Licence.razor` | `IUserService`, `ILicenceService` | `GET /api/v1/dashboard/licence` |
| `Components/Pages/Dashboard/Devices.razor` | `IUserService`, `ILicenceService` | `GET /api/v1/dashboard/machines`, `POST /api/v1/licence/deactivate` |
| `Components/Pages/Dashboard/Settings.razor` | `IUserService` | `PUT /api/v1/account/profile` |
| `Components/Pages/Admin/AdminDashboard.razor` | `IUserService`, `IContactService`, `IStripeWebhookService` | `GET /api/v1/admin/{stats,users,contacts,webhooks}` |
| `Components/Pages/Admin/AdminUserDetail.razor` | `IUserService`, `ILicenceService` | `GET /api/v1/admin/users/{id}`, `POST /api/v1/admin/licences/{id}/modules` |
| `Components/Pages/Admin/BlogAdmin.razor` | `IBlogService` | `GET /api/v1/admin/blog/posts` |
| `Components/Pages/Admin/BlogEditor.razor` | `IBlogService` | `POST/PUT /api/v1/admin/blog/posts` |
| `Components/Pages/Blog/BlogIndex.razor` | `IBlogService` | `GET /api/v1/blog/posts`, `GET /api/v1/blog/categories` |
| `Components/Pages/Blog/BlogPost.razor` | `IBlogService` | `GET /api/v1/blog/posts/{slug}` |
| `Components/Pages/Contact.razor` | `IContactService` | `POST /api/v1/contact` |
| `Components/Pages/Checkout.razor` | `IStripeService` | `POST /api/v1/checkout/create` |
| `Components/Pages/CheckoutSuccess.razor` | `IStripeService` | `GET /api/v1/checkout/status` |
| `Components/Pages/Download.razor` | `IVersionManifestService` | `GET /api/v1/version/manifest` |

Pages **NOT** affected (no DB access): `Home`, `Pricing`, `Privacy`, `Terms`, `Error`, `Docs/*`.

**Process for each page:**
1. Add the matching method + DTOs to `IUtilityMenuApiClient` and implement in `UtilityMenuApiClient`. Mirror the API's request/response shape exactly.
2. If the API endpoint doesn't exist yet, add it in UtilityMenuAPI first (lift the corresponding controller action — most are already there, just need verifying / refining).
3. Edit the .razor: remove `@using Microsoft.AspNetCore.Identity` and service/repo `@inject`s, replace with `@inject IUtilityMenuApiClient Api`, swap method calls.
4. Build verify after each page.

### 3c — Demolition (after every page above is migrated)

Once nothing references the old service/repo layer:

1. **Delete folders**:
   - `Controllers/` — all 5 controllers (LicenceController, CheckoutController, WebhookController, AdminController, DownloadController)
   - `Data/` — DbContext, models, configurations, repositories, seed
   - `Migrations/` — entire folder (API now owns these)
   - `Services/Licensing/`, `Services/Payment/`, `Services/User/`, `Services/Blog/`, `Services/Contact/`
   - `Services/AuditLogService.cs`, `Services/EmailService.cs`, `Services/VersionManifestService.cs`
   - `Infrastructure/Security/` — HMAC signer + key generators (server concerns, now on API)
   - Most of `Infrastructure/Configuration/` — keep `ApiSettings.cs`, drop `StripeSettings`, `LicensingSettings`, `EmailSettings`
   - `Core/Constants/{LicenceConstants,EventTypeConstants}.cs` — server-only
   - `Core/Interfaces/I*Repository.cs`, `I*Service.cs` — anything backed by EF

2. **Delete Identity-coupled files**: `Data/Models/ApplicationUser.cs` is gone with `Data/`. Any remaining `@using Microsoft.AspNetCore.Identity` lines disappear with the page rewires.

3. **Remove NuGet packages** from `UtilityMenuSite.csproj`:
   - `Microsoft.AspNetCore.Identity.EntityFrameworkCore`
   - `Microsoft.EntityFrameworkCore.SqlServer`
   - `Microsoft.EntityFrameworkCore.Tools`
   - `Microsoft.EntityFrameworkCore.Design`
   - `Stripe.net` (Site no longer talks to Stripe directly)
   - `Microsoft.Extensions.Diagnostics.HealthChecks.EntityFrameworkCore` (no DbContext to check)

4. **Strip Program.cs**:
   - Drop `AddDbContext<AppDbContext>`
   - Drop `AddIdentity<ApplicationUser, IdentityRole>().AddEntityFrameworkStores<AppDbContext>().AddDefaultTokenProviders()` and `ConfigureApplicationCookie`
   - Replace with a plain `AddAuthentication().AddCookie("Cookies", opts => ...)` — and update `AuthCookieIssuer` to use the new scheme name (`CookieAuthenticationDefaults.AuthenticationScheme`) instead of `IdentityConstants.ApplicationScheme`
   - Drop all `Configure<StripeSettings>`, `Configure<LicensingSettings>`, `Configure<EmailSettings>`
   - Drop all old service/repo registrations (Userservice, LicenceService, StripeService, StripeWebhookService, BlogService, ContactService, VersionManifestService, AuditLogService, EmailService, all 4 repositories)
   - Drop named `HttpClient`s `sendgrid`, `installer-proxy` (the `installer-proxy` move depends on whether DownloadController stays — see point 5)
   - Drop `AddRateLimiter` (now on API)
   - Drop `AddHealthChecks().AddDbContextCheck<AppDbContext>` and the `MigrateAsync()` startup block
   - Drop `AddMemoryCache` if nothing else uses it

5. **DownloadController decision**: per the architectural plan it stays on Site (UI-adjacent streaming). Two options:
   - Keep the controller and the `installer-proxy` HttpClient. Replace its direct `AppDbContext` write of `UsageEvent` with a `POST /api/v1/usage-events` call (needs new API endpoint).
   - Or move it to API entirely if the streaming concern is outweighed by the architectural cleanliness.

6. **App.razor / Routes.razor cookie-scheme references**: search for any remaining `[Authorize]` policies that name `IdentityConstants.ApplicationScheme` or similar after the cookie scheme switch.

### 3d — UAT smoke test + cutover

1. Deploy refactored Site + UtilityMenuAPI to UAT (Phase 2 infra is ready).
2. Verify end-to-end:
   - Register → confirm email → login → JWT issued → cookie set → dashboard renders
   - Forgot password → email link → reset → login again
   - Logout → cookie cleared → API refresh-token revoked
   - Pro-tier features gated correctly via `licence_type` claim + API live re-check
3. Move the Stripe webhook URL from `{site}/api/stripe/webhook` to `{api}/api/v1/stripe/webhook` in the Stripe dashboard. Smoke-test with `stripe trigger`.
4. Once UAT smoke is clean, promote to Prod.

## Rough effort estimate for the remainder

| Section | Effort |
|---|---|
| 3b page refactor (16 pages × ~30 min) | ~1.5d |
| 3c demolition + Program.cs strip | ~0.5d |
| 3d UAT smoke + Stripe webhook switch + Prod promote | ~0.5d |
| **Total** | **~2.5d** |

Phase 3a (this commit) was ~0.5d worth of foundation work. Original plan was 4–5d for all of Phase 3.

## Architectural decisions baked into Phase 3a (don't re-litigate)

1. **Tokens stored as claims on the auth cookie**, not in `ProtectedLocalStorage`. Inky uses localStorage; we chose claims because the Site is SSR-with-EditForm-method=post and that pattern doesn't mesh with JS interop on the initial submit. Refresh-on-401 logic can be added in Phase 3 continuation if UX demands it.
2. **Identity's `IdentityConstants.ApplicationScheme` is reused** as the cookie scheme during the migration. Once Identity is removed (3c step 4), switch to `CookieAuthenticationDefaults.AuthenticationScheme = "Cookies"`.
3. **`x-api-key` is always attached** by `ApiAuthHandler` (along with Bearer when authenticated). API's `MultiScheme` gives precedence to ApiKey, so the Site is always identified as the trusted Site service principal.
4. **Identity wiring stays in Program.cs during migration** so non-yet-refactored pages keep working. This is *intentional duplication* — the alternative would be a half-broken Site mid-migration.
