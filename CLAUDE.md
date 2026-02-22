# CLAUDE.md — UtilityMenuSite

Instructions for Claude Code when working in this repository.

## Project Overview

**UtilityMenuSite** is a Blazor Web App (.NET 8) that serves as the marketing site, customer portal, licensing backend, and Stripe payment integration for the **UtilityMenu** Excel add-in.

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
| Framework | Blazor Web App (.NET 8) |
| Database | SQL Server + EF Core 8 |
| Auth | ASP.NET Core Identity (cookie, 30-day sliding) |
| Payments | Stripe.net 46.x |
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

### Adding a New Module

1. Add a constant to `Core/Constants/ModuleConstants.cs`.
2. Add a seed entry in `Data/Configuration/ModuleConfiguration.cs` with a stable Guid.
3. Update `LicenceService.ProvisionLicenceAsync` if it should be auto-granted to Pro.
4. Add to the relevant docs page.

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

## Key Design Decisions

- **Stripe refactor**: All Stripe payment logic lives in this web app only. The Excel add-in calls only the `/api/licence/*` and `/api/checkout/*` endpoints — it never touches Stripe directly.
- **HMAC signatures**: Entitlement payloads are signed with `Licensing:HmacSigningKey` so the add-in can verify responses offline.
- **Idempotent webhooks**: `StripeWebhookEvents` table is checked before processing each event. Events are stored with raw payload to support `RetryEventAsync`.
- **Cascade deletes**: `UsageEvents` uses `DeleteBehavior.SetNull` (not Cascade) to avoid SQL Server multiple-cascade-path errors.
- **Seed data is in Configuration files**: `ModuleConfiguration` seeds the 7 modules with fixed GUIDs that match ribbon XML `control.Tag` values.
