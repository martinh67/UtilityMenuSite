# CLAUDE.md — UtilityMenuSite

Instructions for Claude Code when working in this repository.

## Project Overview

**UtilityMenuSite** is a Blazor Web App (.NET 10) that serves as the marketing site, customer portal, licensing backend and Stripe payment integration for the **UtilityMenu** Excel add-in.

## Architecture

```
UtilityMenuSite/
├── Components/
│   ├── Layout/          # MainLayout, AuthLayout, DashboardLayout, AdminLayout + Navbars/Sidebars
│   ├── Pages/           # All Blazor pages (SSR + InteractiveServer)
│   └── Shared/          # Reusable components (PricingCard, LoadingSpinner, etc.)
├── Controllers/         # API controllers (Checkout, Licence, Webhook, Admin)
├── Core/
│   ├── Constants/       # LicenceConstants, ModuleConstants
│   ├── Interfaces/      # All service and repository interfaces
│   └── Models/          # DTOs and result objects
├── Data/
│   ├── Configuration/   # IEntityTypeConfiguration implementations
│   ├── Context/         # AppDbContext + SeedData
│   ├── Models/          # EF Core entity classes
│   └── Repositories/   # Repository implementations
├── Infrastructure/
│   ├── Configuration/   # Strongly-typed settings (StripeSettings, etc.)
│   └── Security/        # LicenceKeyGenerator, ApiTokenGenerator
├── Services/            # Business logic (Payment, Licensing, User, Blog, Contact)
├── wwwroot/
│   ├── css/site.css     # Brand stylesheet
│   ├── docs/            # Markdown documentation (served by DocsPage.razor)
│   ├── downloads/       # version.json (polled by the add-in)
│   └── js/site.js       # Clipboard + Bootstrap init utilities
└── UtilityMenuSite.Tests/  # xUnit tests (Moq + FluentAssertions)
```

## Tech Stack

| Layer | Technology |
|-------|-----------|
| Framework | Blazor Web App (.NET 10) |
| Database | SQL Server + EF Core 8 |
| Auth | ASP.NET Core Identity (cookie, 30-day sliding) |
| Payments | Stripe.net 47.x |
| Markdown | Markdig |
| CSS | Bootstrap 5.3 (CDN) + site.css |
| Icons | Bootstrap Icons 1.11 (CDN) |
| Tests | xUnit + Moq + FluentAssertions |
| CI/CD | GitHub Actions → Azure App Service |

## Naming Conventions

- **Files/Folders**: PascalCase for C#, kebab-case for CSS/JS/Markdown
- **Interfaces**: `I` prefix — `ILicenceService`, `IUserRepository`
- **Async methods**: `Async` suffix, always accept `CancellationToken ct`
- **Controllers**: `[Route("api/...")]`, return `IActionResult`
- **Blazor pages**: `@page` directive, `@rendermode InteractiveServer` for interactive pages
- **Layouts**: `@layout LayoutName` — marketing uses `MainLayout`, dashboard uses `DashboardLayout`, admin uses `AdminLayout`
- **British spelling**: Licence (noun), License (verb) — use `Licence` for entity/variable names
- **appsettings**: Environment-specific files (Development, UAT, Production)

## Development Setup

1. Set the connection string:
   ```bash
   dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Server=...;Database=UtilityMenu;..."
   dotnet user-secrets set "Stripe:SecretKey" "sk_test_..."
   dotnet user-secrets set "Stripe:WebhookSecret" "whsec_..."
   dotnet user-secrets set "Licensing:HmacSigningKey" "a-long-random-secret-key"
   ```

2. Run migrations:
   ```bash
   dotnet ef database update
   ```

3. Start the app:
   ```bash
   dotnet run
   ```

4. For Stripe webhooks in development, use Stripe CLI:
   ```bash
   stripe listen --forward-to https://localhost:5001/api/stripe/webhook
   ```

## Running Tests

```bash
dotnet test
# With coverage:
dotnet test /p:CollectCoverage=true /p:CoverletOutputFormat=opencover
```

## Common Tasks

### Adding a New Doc Page

1. Add a `.md` file to `wwwroot/docs/`.
2. Add a link in `Components/Pages/Docs/DocsIndex.razor` and `DocsPage.razor` sidebar.

### Adding a New Blog Post (via Admin UI)

1. Log in as admin → **Admin → Blog**.
2. Click **New Post**, fill in title/content (Markdown), publish.

### Adding a Standard Module (Core or Pro)

1. Add a name constant to `Core/Constants/ModuleConstants.cs`.
2. Add a `HasData` seed entry in `Data/Configuration/ModuleConfiguration.cs` with a **fixed GUID** (never change it after deployment) and `Tier = "core"` or `"pro"`.
3. If it should be auto-granted: update `LicenceService.ProvisionLicenceAsync` — the `tiersToGrant` array already includes `core` + `pro` for non-custom licences, so no change is needed unless you add a new tier.
4. Add the matching `tag` attribute to the ribbon XML in the **UtilityMenu** add-in repo. The `ModuleName` and ribbon `control.Tag` **must be identical strings**.
5. Run a migration: `dotnet ef migrations add Add<ModuleName>Module`.
6. Add to the relevant docs page.

### Adding a Custom Module (bespoke per-customer features)

Custom modules use `Tier = "custom"` and are **not** auto-granted to any licence type. They are granted individually by an admin per user.

**One-time development steps (done once per module):**

1. Build the feature in the **UtilityMenu** add-in. Register the ribbon button with a `tag` attribute — this is the module name the entitlements API returns:
   ```xml
   <button id="MyCustomTool" tag="MyCustomTool" getVisible="IsModuleVisible" ... />
   ```
2. Add a name constant to `Core/Constants/ModuleConstants.cs`.
3. Add a `HasData` entry in `Data/Configuration/ModuleConfiguration.cs` with a fixed GUID and `Tier = "custom"`:
   ```csharp
   new Module {
       ModuleId    = Guid.Parse("11111111-0000-0000-0000-000000000008"), // pick and fix
       ModuleName  = "MyCustomTool",   // must match ribbon control.Tag exactly
       DisplayName = "My Custom Tool",
       Tier        = "custom",
       IsActive    = true,
       SortOrder   = 8,
       CreatedAt   = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
   }
   ```
4. Run `dotnet ef migrations add Add<ModuleName>Module` and deploy the site. Deploy the updated add-in.

**Per-customer activation (done each time a customer is granted access):**

5. Customer requests access (via `/contact` or directly).
6. Admin visits `/admin/users/{userId}` → **Manage Modules** card → clicks **+** next to the module.
7. This calls `LicenceService.GrantModuleAsync(licenceId, moduleId, expiresAt)` which inserts a `LicenceModule` row. An optional `expiresAt` date can be set via the API (`POST /api/admin/licences/{licenceId}/modules`) for time-limited access.
8. Next time the add-in calls `GET /api/licence/entitlements?key=...`, the module name appears in the signed `modules` array.
9. The add-in's `IsModuleVisible` callback finds the name in the locally cached `license.dat` and shows the ribbon button.

**Admin API endpoints for module management:**
- `POST /api/admin/licences/{licenceId}/modules` — grant a module (`{ moduleId, expiresAt? }`)
- `DELETE /api/admin/licences/{licenceId}/modules/{moduleId}` — revoke a module

**The critical constraint**: `ModuleName` in `ModuleConfiguration.cs` and `control.Tag` in the add-in ribbon XML must be **identical strings**. The fixed GUIDs are only used internally for DB foreign keys — the add-in works entirely with module name strings.

### Releasing a New Add-in Version

The download page reads the version from the **GitHub Releases API** first, with `wwwroot/downloads/version.json` as a fallback. Both must point to the same release.

**Step 1 — Build and publish the GitHub release (UtilityMenu repo)**

1. Tag the commit: `git tag v1.2.3 && git push origin v1.2.3`
2. Create a GitHub Release for that tag at `github.com/martinh67/UtilityMenu/releases`.
3. Attach the installer binary. The file **must** be named exactly:
   ```
   UtilityMenu-Setup-{version}.exe
   ```
   e.g. `UtilityMenu-Setup-1.2.3.exe`. `VersionManifestService` finds the first `.exe` asset in the release — the name becomes the download filename the browser saves.

**Step 2 — Update the fallback manifest (this repo)**

Update `wwwroot/downloads/version.json` to match the new release:

```json
{
  "version": "1.2.3",
  "releaseDate": "2026-03-27",
  "downloadUrl": "https://github.com/martinh67/UtilityMenu/releases/download/v1.2.3/UtilityMenu-Setup-1.2.3.exe",
  "releaseNotesUrl": "/blog/release-1-2-3",
  "minExcelVersion": "2016",
  "changelog": [
    "What changed in this release"
  ]
}
```

The `downloadUrl` in `version.json` must use the same owner/repo as `appsettings.json` (`GitHub:RepoOwner = "martinh67"`, `GitHub:RepoName = "UtilityMenu"`). These must stay in sync.

**How the download flow works**

1. `VersionManifestService.GetLatestAsync()` calls `GET https://api.github.com/repos/martinh67/UtilityMenu/releases/latest`.
2. If that succeeds it extracts the first `.exe` asset URL (`browser_download_url`) as `DownloadUrl`.
3. If the GitHub API call fails or has no `.exe` asset, it falls back to `wwwroot/downloads/version.json`.
4. `GET /api/download/installer` (authenticated) proxies the binary through the site — the user downloads from this domain, never redirected to GitHub directly. The download is recorded as a `UsageEvent`.
5. The JavaScript uses `fetch()` + a blob URL so the download never navigates away from the download page. A server error shows an inline alert rather than replacing the page with the error text.

**GitHub config** lives in `appsettings.json`:

```json
"GitHub": {
  "RepoOwner": "martinh67",
  "RepoName": "UtilityMenu"
}
```

Change these if the repo is moved. `version.json` `downloadUrl` must use the same owner/name.

**Result**: The site auto-detects the latest release from GitHub with no code changes needed for routine version bumps. Only update `version.json` as a fallback safety net or if the GitHub API is unavailable.

### Deploying

- **UAT**: push to `develop` branch — triggers `deploy-uat.yml`
- **Production**: push a version tag (`v1.2.3`) — triggers `deploy-prod.yml`

### Required GitHub Secrets

| Secret | Environment |
|--------|-------------|
| `UAT_CONNECTION_STRING` | UAT |
| `UAT_AZURE_CREDENTIALS` | UAT |
| `PROD_CONNECTION_STRING` | Production |
| `PROD_AZURE_CREDENTIALS` | Production |

## EF Core Migrations

```bash
# Add a migration
dotnet ef migrations add <MigrationName> --project UtilityMenuSite.csproj

# Apply to local DB
dotnet ef database update

# Generate SQL script (for prod review)
dotnet ef migrations script --idempotent -o migrations.sql
```

## TODO

- **Dev environment**: `deploy-dev.yml` triggers on `feature/**` pushes and requires `DEV_CONNECTION_STRING` and `DEV_AZURE_CREDENTIALS` GitHub secrets. Either create a Dev Azure App Service and add these secrets, or delete `deploy-dev.yml` if a dev environment is not needed.

## .NET 10 Blazor — Known Behaviour

- **`EditForm` auto-injects antiforgery**: In .NET 10, `EditForm` with `method="post"` automatically renders the `__RequestVerificationToken` hidden field. Do **not** add `<AntiforgeryToken />` inside an `EditForm` — it will produce a duplicate token and antiforgery validation will fail with a misleading 400 error.
- **Plain `<form>` tags still need it manually**: The logout forms in NavBar, DashboardNavBar, and AdminNavBar are plain HTML `<form>` elements and must keep `<AntiforgeryToken />`.
- **No explicit `AddAntiforgery()` call**: `AddRazorComponents()` registers the antiforgery service internally. Adding a second explicit `AddAntiforgery()` call creates a conflicting second antiforgery system with a different cookie name, causing token/cookie mismatches. Let Blazor own this entirely.
- **`UseAntiforgery()` position**: Must appear after `UseAuthorization()` and before `MapRazorComponents()` in the middleware pipeline.

## Key Design Decisions

- **Stripe logic is site-only**: All Stripe payment logic lives in this web app. The Excel add-in calls only `/api/licence/*` and `/api/checkout/*` — it never touches Stripe directly.
- **Eager provisioning + idempotent webhook**: `StripeService.GetSessionStatusAsync` provisions the licence inline when `payment_status == "paid"` and no licence exists yet (avoids the success-page polling timeout). `StripeWebhookService.HandleCheckoutCompletedAsync` checks for an existing licence first and skips provisioning if already done, so duplicate delivery is safe.
- **HMAC signatures**: Entitlement payloads are signed with `Licensing:HmacSigningKey` so the add-in can verify responses offline for up to `StalenessWindowDays` (default 7).
- **User resolution in checkout**: `EnsureProvisionedAsync` first looks up the user via the `StripeCustomer` table (by Stripe customer ID) to avoid email case-sensitivity issues. Email lookup is only used as a fallback for first-time checkouts.
- **IdentityId linking**: `User.IdentityId` is nullable. Checkout-created users have no Identity account until they register. `Dashboard.razor` calls `RegisterFromIdentityAsync` (not `RegisterOrGetAsync`) on first load to persist the `IdentityId` link.
- **Email case-insensitivity**: `UserRepository.GetByEmailAsync` normalises both sides with `.ToLower()` because Stripe lowercases emails while ASP.NET Core Identity stores them mixed-case.
- **Idempotent webhook storage**: `StripeWebhookEvents` table is checked before processing each event. Events are stored with raw payload to support `RetryEventAsync`. Retry uses `throwOnApiVersionMismatch: false` so stored events from older SDK versions can still be replayed.
- **Custom modules**: `LicenceService.ProvisionLicenceAsync` auto-grants `core` + `pro` tiers for all non-custom licence types. Custom licences get only `core` at provisioning; additional modules are granted individually by an admin via `GrantModuleAsync` / the Manage Modules UI in `AdminUserDetail.razor`.
- **Module–ribbon contract**: `ModuleName` in `ModuleConfiguration.cs` and `control.Tag` in the add-in ribbon XML must be identical. The entitlements API returns module names as strings; the add-in never sees GUIDs.
- **Cascade deletes**: `UsageEvents` uses `DeleteBehavior.SetNull` (not Cascade) to avoid SQL Server multiple-cascade-path errors.
- **Seed data is in Configuration files**: `ModuleConfiguration` seeds modules with fixed GUIDs. The 7 standard modules use GUIDs `11111111-0000-0000-0000-00000000000{1-7}`. Custom modules must use distinct fixed GUIDs starting from `...000000000008`.
- **Download proxy**: `DownloadController` fetches the installer from the upstream GitHub release URL and streams it to the browser. Users never leave the site domain. Uses the `"installer-proxy"` named `HttpClient` with a 5-minute timeout. Download events are recorded in `UsageEvents`.
- **Version manifest priority**: GitHub Releases API is tried first (15-minute cache); `wwwroot/downloads/version.json` is the fallback. `version.json` must always reflect the latest release so the fallback is current.
- **`InternalsVisibleTo`**: `StripeWebhookService.HandleCheckoutCompletedAsync` is `internal` and directly tested via `InternalsVisibleTo` in the `.csproj`, avoiding JSON-parsing complexity in unit tests.
