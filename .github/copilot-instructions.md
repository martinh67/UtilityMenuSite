# GitHub Copilot Instructions — UtilityMenuSite

## Project overview

**UtilityMenuSite** is a Blazor Web App (.NET 10) — marketing site, customer portal, licensing backend, and Stripe payment integration for the **UtilityMenu** Excel add-in.

## Tech stack

- Blazor Web App .NET 10, SSR + InteractiveServer
- SQL Server + EF Core 8 (`IEntityTypeConfiguration` pattern, seed data in configuration files)
- ASP.NET Core Identity (cookie auth, 30-day sliding)
- Stripe.net 47.x (all Stripe logic is site-only — the add-in never calls Stripe directly)
- xUnit + Moq + FluentAssertions
- Bootstrap 5.3 + Bootstrap Icons 1.11 (CDN)

## Directory structure

```
Components/
  Layout/      # MainLayout, AuthLayout, DashboardLayout, AdminLayout
  Pages/       # Blazor pages — marketing, dashboard, admin, docs
  Shared/      # Reusable components (PricingCard, LoadingSpinner, LicenceKeyDisplay, AlertBanner)
Controllers/   # API controllers (Checkout, Licence, Webhook, Admin)
Core/
  Constants/   # LicenceConstants, ModuleConstants
  Interfaces/  # ILicenceService, IUserService, ILicenceRepository, etc.
  Models/      # DTOs and result objects
Data/
  Configuration/ # EF IEntityTypeConfiguration + HasData seed
  Context/       # AppDbContext
  Models/        # EF entities
  Repositories/  # Repository implementations
Infrastructure/
  Configuration/ # Strongly-typed settings (StripeSettings, LicensingSettings)
  Security/      # LicenceKeyGenerator, ApiTokenGenerator
Services/        # Business logic (Payment/, Licensing/, User/, Blog/, Contact/)
UtilityMenuSite.Tests/ # xUnit test project (InternalsVisibleTo is configured)
```

## Naming conventions

- British spelling: `Licence` (noun), `License` (verb)
- Interfaces: `I` prefix — `ILicenceService`, `ILicenceRepository`
- Async methods: `Async` suffix, always accept `CancellationToken ct = default`
- Controllers: `[Route("api/...")]`, return `IActionResult`
- Blazor pages: `@rendermode InteractiveServer` for interactive pages; layouts via `@layout`

## Layouts

| Context | Layout |
|---------|--------|
| Marketing / public | `MainLayout` |
| Login / register | `AuthLayout` |
| Customer dashboard | `DashboardLayout` |
| Admin panel | `AdminLayout` (requires `Admin` role) |

## Licensing system

### Module tiers

| Tier | Auto-granted on provisioning | Managed via |
|------|------------------------------|-------------|
| `core` | Always | Automatic |
| `pro` | Non-custom licence types | Automatic |
| `custom` | Never | Admin grants per user |

### Seeded modules (fixed GUIDs — never change after deployment)

| GUID suffix | ModuleName | Tier |
|-------------|-----------|------|
| `...000000000001` | GetLastRow | core |
| `...000000000002` | GetLastColumn | core |
| `...000000000003` | UnhideRows | core |
| `...000000000004` | AdvancedData | pro |
| `...000000000005` | BulkOperations | pro |
| `...000000000006` | DataExport | pro |
| `...000000000007` | SqlBuilder | pro |
| `...000000000008+` | *(custom modules)* | custom |

### Critical constraint

`ModuleName` in `ModuleConfiguration.cs` and `control.Tag` in the add-in ribbon XML **must be identical strings**. The entitlements API returns module names as strings; the add-in never sees GUIDs.

## Payment and provisioning flow

1. User completes Stripe checkout → redirected to success page.
2. Success page polls `GET /api/checkout/status?sessionId=...` via `StripeService.GetSessionStatusAsync`.
3. If `payment_status == "paid"` and no licence exists, `EnsureProvisionedAsync` provisions the licence inline (eager provisioning — avoids webhook timing dependency).
4. `StripeWebhookService.HandleCheckoutCompletedAsync` (internal method) also provisions on `checkout.session.completed`, but checks for an existing licence first (`GetLicenceKeyForStripeCustomerAsync`) and skips if already done — idempotent.
5. User resolution: first looks up via `StripeCustomer` table by Stripe customer ID; falls back to email lookup only for first-time checkouts.

## User identity linking

`User.IdentityId` is nullable — checkout-created users have no Identity account until they register. `Dashboard.razor` calls `UserService.RegisterFromIdentityAsync` (not `RegisterOrGetAsync`) on first load to persist the IdentityId link. Email lookup always uses `.ToLower()` comparison because Stripe lowercases emails.

## Admin module management

Admins can grant or revoke individual modules at `/admin/users/{userId}`:

- `ILicenceService.GrantModuleAsync(licenceId, moduleId, expiresAt?)` — idempotent, supports time-limited access
- `ILicenceService.RevokeModuleAsync(licenceId, moduleId)`
- API: `POST /api/admin/licences/{licenceId}/modules` and `DELETE /api/admin/licences/{licenceId}/modules/{moduleId}`

## Adding a standard module (core / pro)

1. Add name constant to `Core/Constants/ModuleConstants.cs`.
2. Add `HasData` entry in `Data/Configuration/ModuleConfiguration.cs` with a fixed GUID and correct tier.
3. Add matching `tag` attribute to the ribbon XML in the UtilityMenu add-in repo.
4. Run `dotnet ef migrations add Add<Name>Module` and deploy both repos.

## Adding a custom module (bespoke per-customer)

**One-time (development):**

1. Build the feature in the add-in; register the ribbon button with a `tag` matching the intended `ModuleName`:
   ```xml
   <button id="MyCustomTool" tag="MyCustomTool" getVisible="IsModuleVisible" ... />
   ```
2. Add constant to `ModuleConstants.cs`.
3. Add `HasData` entry in `ModuleConfiguration.cs` with a new fixed GUID (suffix `...000000000008` or higher) and `Tier = "custom"`.
4. Run migration and deploy site + add-in.

**Per-customer (operational):**

5. Customer requests access (contact form or direct).
6. Admin → `/admin/users/{userId}` → **Manage Modules** → click **+** next to the module.
7. Add-in calls `GET /api/licence/entitlements?key=...` → receives module name in signed `modules` array → `IsModuleVisible` returns true → ribbon button appears.

## Download flow and release publishing

### How the download works

1. `VersionManifestService.GetLatestAsync()` calls the GitHub Releases API: `GET https://api.github.com/repos/{owner}/{repo}/releases/latest` (15-minute cache). Falls back to `wwwroot/downloads/version.json` if the API call fails.
2. The first `.exe` asset found in the release becomes `DownloadUrl` and `FileName`.
3. `GET /api/download/installer` (requires auth) proxies the binary from the upstream URL through the site — the user never sees a GitHub URL. Download is recorded as a `UsageEvent`.
4. The JS `triggerFileDownload(url)` uses `fetch()` + blob URL so the page is never navigated away on error. Returns `true`/`false` so `Download.razor` can show an inline error alert.

### GitHub config

`appsettings.json` controls which repo is queried:

```json
"GitHub": { "RepoOwner": "martinh67", "RepoName": "UtilityMenu" }
```

`wwwroot/downloads/version.json` `downloadUrl` **must use the same owner/repo**. These must stay in sync.

### Publishing a new add-in release (checklist)

1. **Build installer in UtilityMenu repo** — output file must be named `UtilityMenu-Setup-{version}.exe` (e.g. `UtilityMenu-Setup-1.2.3.exe`). This is the filename the browser saves.
2. **Create GitHub Release** at `github.com/martinh67/UtilityMenu/releases` tagged `v{version}` and attach the `.exe` as a release asset.
3. **Update `wwwroot/downloads/version.json`** in this repo as fallback:
   ```json
   {
     "version": "1.2.3",
     "releaseDate": "2026-03-27",
     "downloadUrl": "https://github.com/martinh67/UtilityMenu/releases/download/v1.2.3/UtilityMenu-Setup-1.2.3.exe",
     "releaseNotesUrl": "/blog/release-1-2-3",
     "minExcelVersion": "2016",
     "changelog": ["What changed"]
   }
   ```
4. Deploy the site. The GitHub API cache clears within 15 minutes; the fallback is immediately live.

No code changes are needed for routine version bumps — only the GitHub release and `version.json` update.

## Webhook retry

`RetryEventAsync` re-parses stored raw JSON with `throwOnApiVersionMismatch: false` so events captured at an older Stripe SDK version can still be replayed. `HandleCheckoutCompletedAsync` is `internal` and tested directly via `InternalsVisibleTo`.

## .NET 10 Blazor gotchas

- `EditForm` with `method="post"` auto-injects antiforgery — do **not** add `<AntiforgeryToken />` inside `EditForm`.
- Plain `<form>` elements (logout forms in navbars) must keep `<AntiforgeryToken />` manually.
- Do **not** call `AddAntiforgery()` explicitly — `AddRazorComponents()` registers it internally. A second registration creates a conflicting cookie.
- `UseAntiforgery()` must appear after `UseAuthorization()` and before `MapRazorComponents()`.

## EF Core migrations

```bash
dotnet ef migrations add <MigrationName> --project UtilityMenuSite.csproj
dotnet ef database update
dotnet ef migrations script --idempotent -o migrations.sql   # prod review
```

## Testing

```bash
dotnet test
dotnet test /p:CollectCoverage=true /p:CoverletOutputFormat=opencover
```

Unit tests mock all dependencies (Moq). Service-layer integration tests use EF Core InMemory (`UpgradeFlowTests`). Stripe webhook handler tests call `HandleCheckoutCompletedAsync` directly via `InternalsVisibleTo` and build `Stripe.Checkout.Session` objects via reflection to avoid JSON-parsing complexity.

## Deployment

| Target | Trigger |
|--------|---------|
| UAT | Push to `develop` → `deploy-uat.yml` |
| Production | Push tag `v*.*.*` → `deploy-prod.yml` |

Required secrets: `UAT_CONNECTION_STRING`, `UAT_AZURE_CREDENTIALS`, `PROD_CONNECTION_STRING`, `PROD_AZURE_CREDENTIALS`.
