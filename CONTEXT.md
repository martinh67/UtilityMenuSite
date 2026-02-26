# CONTEXT.md — UtilityMenu Ecosystem

> **Purpose**: Single source of truth for the UtilityMenu ecosystem. Use this document when generating code, refactoring, designing APIs, or reasoning about where functionality belongs. If it contradicts a source file, investigate and update whichever is wrong.

---

## Table of Contents

1. [High-Level Overview](#1-high-level-overview)
2. [UtilityMenuSite — Responsibilities and Architecture](#2-utilitymenusiteresponsibilities-and-architecture)
3. [UtilityMenu — Responsibilities and Architecture](#3-utilitymenu--responsibilities-and-architecture)
4. [How the Two Applications Work Together](#4-how-the-two-applications-work-together)
5. [Architectural Boundaries](#5-architectural-boundaries)
6. [Development and Refactoring Guidance](#6-development-and-refactoring-guidance)
7. [Future Extensions](#7-future-extensions)

---

## 1. High-Level Overview

### What is UtilityMenu?

**UtilityMenu** is a modular productivity add-in for Microsoft Excel, built with [Excel-DNA](https://excel-dna.net/) and .NET 8. It extends Excel's ribbon with purpose-built tools that automate common spreadsheet tasks. Functionality is divided into modules — some included for all users (Core), some gated behind a paid subscription (Pro), and some available as optional extras (Custom).

The add-in is distributed as a single-file packed XLL loaded by Excel, installed via an MSI/Bundle created with WiX.

### What is UtilityMenuSite?

**UtilityMenuSite** is a Blazor Web App (.NET 8) that acts as the SaaS backbone of the UtilityMenu product. It is responsible for:

- Serving the public marketing website, documentation, and blog
- Handling user registration, authentication, and profile management
- Processing payments and subscriptions through Stripe
- Issuing and managing licence keys and module entitlements
- Exposing the Licensing API consumed by the add-in
- Running the admin dashboard for operators

### Why Two Repositories?

The separation reflects a clean product boundary:

| Concern | Owner |
|---------|-------|
| Excel integration, ribbon, module logic | UtilityMenu (add-in) |
| User accounts, payments, licences, content | UtilityMenuSite (web app) |

Keeping them separate ensures the add-in never handles payment secrets, never accesses the database directly, and remains a lightweight installable binary. UtilityMenuSite can be updated, scaled, and deployed independently without requiring users to reinstall the add-in.

### Product Vision

UtilityMenu is a subscription-funded Excel productivity toolkit. The website and backend handle all the complexity of commerce, identity, and licensing so the add-in can focus entirely on delivering value inside Excel. The two repos collaborate through a stable, versioned HTTP API — and nothing more.

---

## 2. UtilityMenuSite — Responsibilities and Architecture

### Role

UtilityMenuSite is the **single source of truth** for the entire product. It owns all persistent state, all financial transactions, and all authoritative decisions about what a given licence is permitted to do.

### Technology Stack

| Layer | Technology |
|-------|-----------|
| Framework | Blazor Web App, .NET 8 (SSR + InteractiveServer) |
| Database | SQL Server (production/UAT) / SQLite (development) |
| ORM | EF Core 8 with `IEntityTypeConfiguration` |
| Auth | ASP.NET Core Identity, cookie-based, 30-day sliding window |
| Payments | Stripe.net 46.x |
| Markdown | Markdig |
| UI | Bootstrap 5.3 (CDN) + Bootstrap Icons 1.11 + site.css |
| Tests | xUnit + Moq + FluentAssertions |
| CI/CD | GitHub Actions → Azure App Service |

### Application Areas

#### Marketing Site
Public-facing pages served with SSR for SEO. Includes the home page, pricing, contact form, terms, and privacy pages. Uses `MainLayout`.

#### Documentation and Blog
Static Markdown files in `wwwroot/docs/` are rendered by `DocsPage.razor` using Markdig. Blog posts are managed through the admin UI, stored in the database, and rendered from Markdown body content.

#### User Accounts and Authentication
Registration, login, logout, password reset, and profile completion flows use ASP.NET Core Identity extended with a custom `User` entity. Authentication is cookie-based with a 30-day sliding expiry. Failed login attempts trigger a 15-minute lockout after five failures.

#### Dashboard (Customer Portal)
Authenticated pages under `DashboardLayout` let users view their licence key, subscription status, active machines, and download the installer. The installer download is gated behind authentication and records a `UsageEvent`.

#### Licensing API
A set of controllers under `/api/licence` and `/api/checkout` that the Excel add-in calls. This is the primary integration surface between the two applications. See [Section 4](#4-how-the-two-applications-work-together) for details.

#### Stripe Integration
All Stripe logic is contained within UtilityMenuSite. The `StripeService` creates Checkout sessions and Billing Portal sessions. The `StripeWebhookService` receives and processes webhook events from Stripe (subscription created, updated, deleted, payment succeeded/failed) and keeps the `Subscriptions`, `Licences`, and `Machines` tables in sync. Webhook events are stored in `StripeWebhookEvents` for idempotent processing and retry support.

#### Admin Dashboard
Pages under `AdminLayout` (role-gated to `Admin`) provide: user search and detail views, contact submission management, webhook retry, and headline statistics.

### Project Structure

```
UtilityMenuSite/
├── Components/
│   ├── Layout/          # MainLayout, AuthLayout, DashboardLayout, AdminLayout
│   │                    # + matching NavBars, Sidebars, Footer
│   ├── Pages/           # 23 Blazor pages (auth, marketing, dashboard, admin, blog, docs)
│   └── Shared/          # PricingCard, LoadingSpinner, LicenceKeyDisplay,
│                        # CopyButton, DownloadButton, MarkdownRenderer,
│                        # Pagination, AlertBanner
├── Controllers/         # LicenceController, CheckoutController,
│                        # WebhookController, AdminController, DownloadController
├── Core/
│   ├── Constants/       # ModuleConstants, LicenceConstants, EventTypeConstants
│   ├── Interfaces/      # All service and repository interfaces
│   └── Models/          # DTOs and result objects (no EF entities)
├── Data/
│   ├── Configuration/   # IEntityTypeConfiguration implementations + seed data
│   ├── Context/         # AppDbContext (inherits IdentityDbContext<ApplicationUser>)
│   ├── Models/          # 15 EF Core entity classes
│   └── Repositories/    # LicenceRepository, UserRepository, BlogRepository,
│                        # ContactRepository
├── Infrastructure/
│   ├── Configuration/   # StripeSettings, LicensingSettings, EmailSettings
│   └── Security/        # LicenceKeyGenerator, ApiTokenGenerator
├── Services/            # LicenceService, StripeService, StripeWebhookService,
│                        # UserService, BlogService, ContactService,
│                        # VersionManifestService
└── wwwroot/
    ├── css/site.css     # Brand stylesheet
    ├── js/site.js       # Clipboard + Bootstrap init utilities
    ├── docs/            # Markdown documentation (served by DocsPage.razor)
    └── downloads/       # version.json (polled by VersionManifestService)
```

### Database Schema

The authoritative SQL Server database contains the following tables. All primary keys are `Guid`. EF Core `IEntityTypeConfiguration` classes live in `Data/Configuration/`.

#### Identity and Users

| Table | Key Columns | Notes |
|-------|-------------|-------|
| `AspNetUsers` | `Id` (string) | ASP.NET Identity. `ApplicationUser` extends `IdentityUser`. |
| `AppUsers` | `UserId`, `IdentityId`, `Email`, `DisplayName`, `Organisation`, `JobRole`, `ApiToken` | Custom user profile; unique on Email, IdentityId, ApiToken. |
| `ApiTokens` | `TokenId`, `UserId`, `Token`, `IsActive`, `LastUsedAt`, `ExpiresAt` | Supports token rotation; `User.ApiToken` is the primary active token. |

#### Licensing

| Table | Key Columns | Notes |
|-------|-------------|-------|
| `Licences` | `LicenceId`, `UserId`, `SubscriptionId`, `LicenceKey`, `LicenceType`, `MaxActivations`, `IsActive`, `ExpiresAt`, `Signature` | `LicenceKey` in `UMENU-XXXX-XXXX-XXXX` format. `Signature` is HMAC-SHA256 for offline verification. |
| `Machines` | `MachineId`, `LicenceId`, `MachineFingerprint`, `IsActive`, `FirstSeenAt`, `LastSeenAt` | Fingerprint is SHA-256 of CPU ID, disk serial, and MAC address. Enforces `MaxActivations`. |
| `Modules` | `ModuleId`, `ModuleName`, `DisplayName`, `Tier`, `IsActive`, `SortOrder` | Seeded with 7 fixed-GUID modules. `ModuleId` values match ribbon XML `control.Tag` values in the add-in. |
| `LicenceModules` | `LicenceModuleId`, `LicenceId`, `ModuleId`, `GrantedAt`, `ExpiresAt` | Junction table; tracks which modules a licence grants, with optional per-module expiry. |

#### Payments and Billing

| Table | Key Columns | Notes |
|-------|-------------|-------|
| `StripeCustomers` | `StripeCustomerId`, `UserId`, `Email` | One-to-one with `AppUsers`. |
| `Subscriptions` | `SubscriptionId`, `UserId`, `StripeSubscriptionId`, `StripePriceId`, `Status`, `PlanType`, `CurrentPeriodEnd`, `GracePeriodEnd`, `CancelAtPeriodEnd` | Synced from Stripe webhooks. `Status` mirrors Stripe values. |
| `StripeWebhookEvents` | `WebhookEventId`, `StripeEventId`, `EventType`, `RawPayload`, `ProcessedAt`, `FailedAt` | Idempotency guard. Raw payload preserved for retry. |

#### Content

| Table | Key Columns | Notes |
|-------|-------------|-------|
| `BlogPosts` | `PostId`, `CategoryId`, `AuthorId`, `Title`, `Slug`, `Body`, `IsPublished`, `MetaTitle`, `MetaDescription` | Body is Markdown. Rendered by Markdig. |
| `BlogCategories` | `CategoryId`, `Name`, `Slug`, `SortOrder` | |
| `ContactSubmissions` | `SubmissionId`, `Name`, `Email`, `Subject`, `Message`, `IsResolved`, `IpAddress` | Rate-limited to 3/hour per IP. |

#### Auditing

| Table | Key Columns | Notes |
|-------|-------------|-------|
| `UsageEvents` | `EventId`, `UserId?`, `LicenceId?`, `MachineId?`, `ModuleId?`, `EventType`, `EventData` (JSON) | Append-only. All FKs use `DeleteBehavior.SetNull` to avoid SQL Server multiple-cascade-path errors. |
| `AuditLogs` | `AuditLogId`, `UserId?`, `Action`, `EntityName`, `EntityId`, `OldValues`, `NewValues`, `IpAddress` | Admin-facing change log. |

#### Seeded Modules

Seven modules are seeded with fixed GUIDs (matching the add-in's ribbon XML):

| ModuleName | Tier |
|------------|------|
| `GetLastRow` | core |
| `GetLastColumn` | core |
| `UnhideRows` | core |
| `AdvancedData` | pro |
| `BulkOperations` | pro |
| `DataExport` | pro |
| `SqlBuilder` | pro |

### Key Design Decisions

- **HMAC Signatures**: Entitlement payloads returned by `/api/licence/entitlements` are signed with `Licensing:HmacSigningKey` (set via user secrets / environment variable). This allows the add-in to verify the response integrity offline.
- **Idempotent Webhooks**: Every Stripe webhook event is written to `StripeWebhookEvents` before processing. The `StripeEventId` is checked for duplicates to prevent double-processing. Raw payload is preserved so failed events can be replayed via `RetryEventAsync`.
- **Grace Periods**: `Subscription.GracePeriodEnd` allows access to continue briefly after a payment failure before a licence is deactivated.
- **Licence Types**: `individual`, `team`, `lifetime`, `custom` — defined in `LicenceConstants`. Plan types are `monthly`, `annual`, `lifetime`.

---

## 3. UtilityMenu — Responsibilities and Architecture

### Role

UtilityMenu is the **lightweight desktop client**. It installs into Excel, renders the ribbon, and executes module logic. It delegates all account, billing, and licensing decisions to UtilityMenuSite via HTTP.

### Technology Stack

| Layer | Technology |
|-------|-----------|
| Add-in Framework | Excel-DNA (.NET 8) |
| Language | C# / .NET 8.0 |
| Local Storage | DPAPI-encrypted JSON files |
| Installer | WiX v3.x (MSI + Bundle) |
| Build automation | Python 3.9+, GitHub Actions |
| Tests | xUnit + Moq + FluentAssertions |

### What the Add-in Does

- Loads as an XLL into Excel via Excel-DNA
- Renders a custom ribbon with module-specific controls
- Activates or disables ribbon controls based on cached entitlements
- Executes module actions when the user interacts with the ribbon
- Manages licence validation silently in the background
- Opens a browser to UtilityMenuSite's Stripe Checkout when the user upgrades
- Polls UtilityMenuSite for checkout completion and refreshes the cached entitlements
- Checks for updates using the version manifest

### What the Add-in Does Not Do

- It does not contain Stripe API calls using a secret key
- It does not contain subscription management logic
- It does not maintain user accounts or perform authentication
- It does not access the UtilityMenuSite database
- It does not store any secrets in recoverable form

### Project Structure

```
UtilityMenu/
├── src/
│   └── UtilityMenu/
│       ├── Bootstrap/        # DI composition root
│       ├── Core/
│       │   ├── Constants/    # Module names, plan types, event types
│       │   ├── Interfaces/   # Service contracts
│       │   └── Models/       # PremiumLicense, PaymentPlan, CheckoutResult, etc.
│       ├── Infrastructure/
│       │   ├── Excel/        # Excel adapter (wraps ExcelDnaUtil.Application)
│       │   ├── Payment/      # LicenseStorage (DPAPI), StripeSettings
│       │   └── Settings/     # App configuration helpers
│       ├── Modules/          # Feature modules (Core, Pro, Tools, About)
│       ├── Services/
│       │   ├── Payment/      # LicenceValidationService, ApiCheckoutService
│       │   └── Update/       # UpdateManifestProviderService
│       └── UI/               # Ribbon XML, dialogs, ribbon actions
└── installer/                # WiX MSI / Bundle source
```

### Local Data Storage

The add-in does not use a relational database. Persistent state is stored in DPAPI-encrypted JSON files in `%LOCALAPPDATA%\UtilityMenu\`:

#### `license.dat` (DPAPI-encrypted)

Model: `PremiumLicense`

| Field | Type | Description |
|-------|------|-------------|
| `LicenceKey` | string | Activation key (e.g., `UMENU-XXXX-XXXX-XXXX`) |
| `Email` | string | Registered email address |
| `LicenceType` | enum | `Individual`, `Team`, `Enterprise`, `Trial`, `Lifetime` |
| `ActivatedAt` | DateTime | Timestamp of first activation |
| `ExpiresAt` | DateTime? | Licence expiry (`null` for lifetime) |
| `GrantedModules` | List\<string\> | Module names the licence unlocks |
| `Signature` | string | HMAC-SHA256 signature from UtilityMenuSite |
| `LastValidatedAt` | DateTime | Timestamp of most recent successful API validation |

This file is regenerated on every successful call to `/api/licence/entitlements`. The HMAC signature is validated locally to detect tampering before use.

#### `stripe.settings.json` (DPAPI-encrypted)

Model: `StripeSettings`

| Field | Type | Description |
|-------|------|-------------|
| `PublishableKey` | string | Stripe publishable key (`pk_test_` / `pk_live_`) |
| `ApiBaseUrl` | string | Base URL of UtilityMenuSite (e.g., `https://utilitymenu.com`) |
| `PollingIntervalSeconds` | int | How often to poll `/api/checkout/status` |
| `PollingTimeoutMinutes` | int | Maximum wait time for checkout completion |

### Licence Validation Logic

On each Excel session start, the add-in checks the freshness of the cached licence:

1. If `LastValidatedAt` is within the staleness window (7 days) **and** the HMAC signature is valid: use the cached licence, no network call needed.
2. If stale, expired, or signature is missing/invalid: call `/api/licence/entitlements?key={licenceKey}` to refresh.
3. If the API is unreachable: fall back to the cached licence if it has not expired. Apply a grace period if the subscription is past due.
4. Update `GrantedModules` and re-enable/disable ribbon controls accordingly.

### Machine Fingerprinting

When activating a machine, the add-in computes a SHA-256 fingerprint from:
- CPU ID
- Primary disk serial number
- Primary network adapter MAC address

This fingerprint is sent to `/api/licence/activate` and stored in the `Machines` table on UtilityMenuSite. The number of active machines is enforced against `Licence.MaxActivations`.

---

## 4. How the Two Applications Work Together

### Overview

```
┌─────────────────────────────────────────────────────────────────┐
│                        UtilityMenuSite                          │
│                                                                 │
│  ┌──────────┐  ┌──────────┐  ┌──────────┐  ┌───────────────┐  │
│  │  Blazor  │  │  Stripe  │  │  Admin   │  │  Licensing    │  │
│  │  Pages   │  │  Webhook │  │  API     │  │  API          │  │
│  └──────────┘  └──────────┘  └──────────┘  └───────┬───────┘  │
│                                                     │          │
│              ┌──────────────────────────────────────┘          │
│              │  SQL Server (authoritative state)               │
└──────────────┼─────────────────────────────────────────────────┘
               │ HTTPS
               │ /api/licence/*
               │ /api/checkout/*
               ▼
┌─────────────────────────────────────────────────────────────────┐
│                         UtilityMenu (Excel Add-in)              │
│                                                                 │
│  ┌──────────────────┐    ┌──────────────────────────────────┐   │
│  │  Excel Ribbon    │    │  Local Cache                     │   │
│  │  (modules/UX)    │    │  license.dat (DPAPI)             │   │
│  └──────────────────┘    │  stripe.settings.json (DPAPI)    │   │
│                          └──────────────────────────────────┘   │
└─────────────────────────────────────────────────────────────────┘
```

### Licensing API Contract

The add-in communicates with UtilityMenuSite through three HTTP endpoints:

#### `GET /api/licence/validate?key={licenceKey}`
Fast validity check. Returns whether the key is active and its expiry date. Rate-limited to 60 requests/minute per IP.

**Response**:
```json
{ "isValid": true, "licenceType": "individual", "expiresAt": "2027-01-01T00:00:00Z" }
```

#### `GET /api/licence/entitlements?key={licenceKey}`
Full entitlements with HMAC-signed payload. The add-in calls this to refresh its local cache. Rate-limited to 60 requests/minute per IP.

**Response**:
```json
{
  "isValid": true,
  "licenceKey": "UMENU-XXXX-XXXX-XXXX",
  "licenceType": "individual",
  "expiresAt": "2027-01-01T00:00:00Z",
  "modules": ["GetLastRow", "GetLastColumn", "UnhideRows", "AdvancedData"],
  "signature": "<hmac-sha256-base64>"
}
```

The `modules` array is the definitive list of module names the add-in should unlock. The `signature` covers the full payload and is verified offline using the embedded signing key.

#### `POST /api/licence/activate`
Activates a machine against a licence. Enforces the `MaxActivations` seat limit. Requires `Authorization: Bearer <apiToken>`.

**Request body**:
```json
{ "licenceKey": "UMENU-...", "machineFingerprint": "<sha256>", "machineName": "DESKTOP-XYZ" }
```

#### `POST /api/licence/deactivate`
Deactivates a specific machine. Requires `Authorization: Bearer <apiToken>`.

#### `POST /api/checkout/create`
Instructs UtilityMenuSite to create a Stripe Checkout session. The add-in never calls Stripe directly.

**Request body**:
```json
{ "priceId": "price_...", "customerEmail": "user@example.com", "mode": "subscription" }
```

**Response**: `{ "url": "https://checkout.stripe.com/...", "sessionId": "cs_..." }`

#### `GET /api/checkout/status?sessionId={id}`
Polled by the add-in after the user completes checkout. Returns the session status and (on success) the newly issued licence key.

**Response**: `{ "status": "complete", "licenceKey": "UMENU-...", "customerEmail": "user@example.com" }`

### End-to-End Activation Flow

```
1. User visits utilitymenu.com and registers an account
       ↓
2. User selects a plan on the Pricing page
       ↓
3. UtilityMenuSite creates a Stripe Checkout session
       ↓
4. User completes payment on Stripe-hosted checkout page
       ↓
5. Stripe sends checkout.session.completed webhook to UtilityMenuSite
       ↓
6. StripeWebhookService provisions:
   - Subscription record (status: active)
   - Licence record (LicenceKey generated, modules granted)
       ↓
7. User downloads and installs UtilityMenu
       ↓
8. On first run, user enters their licence key in the add-in
       ↓
9. Add-in calls POST /api/licence/activate with key + machine fingerprint
       ↓
10. UtilityMenuSite validates seat count and records the Machine
        ↓
11. Add-in calls GET /api/licence/entitlements
        ↓
12. UtilityMenuSite returns signed entitlement payload
        ↓
13. Add-in validates HMAC signature, caches PremiumLicense to license.dat
        ↓
14. Ribbon controls for granted modules are enabled
        ↓
15. On subsequent sessions, cached licence is used (up to 7 days staleness)
```

### Subscription Renewal and Expiry

- Stripe sends `invoice.payment_succeeded` / `invoice.payment_failed` webhooks.
- `StripeWebhookService` updates `Subscription.Status`, `CurrentPeriodEnd`, and `GracePeriodEnd`.
- On the next time the add-in refreshes entitlements, the updated state is reflected.
- During a grace period, the add-in continues to allow access while UtilityMenuSite waits for payment retry.

### Version Updates

The add-in fetches `version.json` from the UtilityMenuSite `/downloads/` directory (or directly from the GitHub raw content URL) to check for new versions. The manifest includes the version number, download URL, file hash, and release notes. Updates are surfaced in the add-in UI; the user is redirected to download the new installer.

---

## 5. Architectural Boundaries

### What UtilityMenuSite Owns

- The SQL Server database and all schema migrations
- User accounts, identity, sessions, and authentication
- All Stripe API keys (publishable and secret)
- Subscription and billing state
- Licence issuance, provisioning, and revocation
- The authoritative list of modules and their GUIDs
- HMAC signing key for entitlement payloads
- Admin dashboard and operator tooling
- Marketing site, blog, and documentation content

### What UtilityMenu Owns

- Excel ribbon definition and interaction logic
- Module implementations (the actual Excel functionality)
- Local licence cache (`license.dat`) and settings (`stripe.settings.json`)
- Machine fingerprint computation
- The XLL binary, installer, and auto-update mechanism
- UI dialogs within Excel

### What Must Never Be Duplicated

| Concern | Canonical Location |
|---------|-------------------|
| Module names and IDs | `ModuleConstants.cs` in UtilityMenuSite; referenced by name in the add-in |
| Module GUIDs | `ModuleConfiguration.cs` seed data; these must match ribbon XML `control.Tag` values |
| Licence types and statuses | `LicenceConstants.cs` in UtilityMenuSite |
| Subscription/billing logic | UtilityMenuSite only |

### What Must Never Be Stored in the Add-in

- Stripe secret keys
- Database connection strings
- User passwords or identity tokens
- Any server-side secret that could be extracted from the installed binary

The `stripe.settings.json` file may contain the Stripe **publishable** key (which is safe to be client-side) and the API base URL. It must never contain the Stripe secret key.

### How the API Contract Should Evolve

- The `/api/licence/*` endpoints are the primary integration surface. Changes must be backwards-compatible or versioned.
- Add new fields to responses rather than removing or renaming existing ones.
- Deprecate and document before removing any field the add-in relies on.
- The `modules` array in the entitlements response uses **string names** (e.g., `"GetLastRow"`), not GUIDs. Module names defined in `ModuleConstants.cs` must remain stable.
- The HMAC signature algorithm (HMAC-SHA256) must remain stable because the add-in verifies it offline.

### Database Schema Separation

- UtilityMenuSite has full ownership of SQL Server schema and EF Core migrations.
- The add-in never connects to SQL Server. It interacts only through the HTTP API.
- The DPAPI-encrypted local files on the user's machine are ephemeral client-side state; they can be deleted and rebuilt from the API at any time.

---

## 6. Development and Refactoring Guidance

### Core Principles

1. **UtilityMenuSite is always authoritative.** If there is a conflict between local add-in state and what the API returns, the API wins.

2. **UtilityMenu is always a lightweight client.** It should contain as little business logic as possible. When in doubt, move logic server-side.

3. **Never reintroduce Stripe or subscription logic into the add-in.** The refactor that moved all Stripe logic to the web app was deliberate. Reversing it would reintroduce secret key exposure risk and deployment complexity.

4. **Keep the Licensing API stable.** The add-in is a distributed binary that cannot be force-updated instantly. Breaking changes to `/api/licence/*` endpoints will break existing installations.

5. **Module names and GUIDs are immutable once released.** Changing `ModuleName` or `ModuleId` in the seed data breaks the link between the server's entitlement records and the add-in's ribbon controls. If a module must be retired, mark `IsActive = false` — never delete or rename.

### Adding a New Module

1. Add the module name constant to `Core/Constants/ModuleConstants.cs` in UtilityMenuSite.
2. Add a seed entry in `Data/Configuration/ModuleConfiguration.cs` with a new stable `Guid`.
3. Update `LicenceService.ProvisionLicenceAsync` if the module should be auto-granted to Pro subscribers.
4. Create a migration: `dotnet ef migrations add AddModuleXxx`.
5. Add the corresponding control to the ribbon XML in UtilityMenu, using the same `Guid` string as the `control.Tag` value.
6. Add documentation to `wwwroot/docs/` and update the relevant docs index page.

### Adding a New API Endpoint

1. Define the request/response DTOs in `Core/Models/`.
2. Add the interface method to the relevant `Core/Interfaces/I*Service.cs`.
3. Implement in `Services/`.
4. Expose via a method in the appropriate controller in `Controllers/`.
5. Apply rate limiting if the endpoint is callable by the unauthenticated add-in.
6. Document the endpoint in this file (Section 4).

### Naming Conventions

- **British spelling**: Use `Licence` (noun) for entity/variable names. `License` (verb) is acceptable in method names like `LicenseUser`.
- **Async methods**: Always suffix with `Async`, always accept `CancellationToken ct = default`.
- **Interfaces**: `I` prefix — `ILicenceService`, `IUserRepository`.
- **Controllers**: `[Route("api/...")]`, return `IActionResult`.
- **Blazor pages**: `@page` directive; interactive pages use `@rendermode InteractiveServer`.

### Running Tests

```bash
# UtilityMenuSite
cd UtilityMenuSite
dotnet test

# UtilityMenu
cd UtilityMenu
dotnet test
```

### Deploying UtilityMenuSite

- **UAT**: push to `develop` branch → triggers `deploy-uat.yml`
- **Production**: push a version tag (`v1.2.3`) → triggers `deploy-prod.yml`

---

## 7. Future Extensions

The current architecture is designed to accommodate the following without requiring structural changes.

### Team Licences

The `LicenceConstants.TypeTeam` type already exists. A team licence can have a higher `MaxActivations` value, allowing multiple machines per organisation. A future `Organisations` table (with `OrganisationMembers`) would link multiple users to a shared subscription. The Licensing API would need to resolve entitlements by organisation membership rather than individual user only.

### Enterprise Plans

Enterprise licences can be provisioned manually (bypassing Stripe checkout) by an admin, setting `LicenceType = "custom"` and populating `LicenceModules` directly. The `LicenceConstants.TypeCustom` type already supports this path.

### Custom Modules

`ModuleConstants.TierCustom` and the `custom` licence type already provide the data model for per-customer module grants. A workflow to sell or provision a custom module add-on would create a new `Module` record, a Stripe price ID in `StripeSettings.Prices.CustomModule`, and a `LicenceModule` row granting access to the purchasing user. The add-in requires no changes provided the module name is included in the `modules` array from the entitlements endpoint.

### Offline Mode

The HMAC-signed licence cache already provides a partial offline capability (7-day staleness window). This can be extended by increasing `LicensingSettings.StalenessWindowDays`. For fully air-gapped environments, a separate licence issuance flow (one-time offline activation code) could be built on top of the existing `Signature` field without changing the add-in's validation logic.

### Additional SaaS Features

UtilityMenuSite's architecture supports:
- **Email notifications** via the `EmailSettings` infrastructure (subscription renewal reminders, payment failure alerts)
- **Usage analytics** via the `UsageEvents` table (module popularity, churn indicators)
- **Affiliate or referral codes** added to the `User` or checkout flow without structural changes
- **Multi-currency or regional pricing** by extending `StripeSettings.Prices`

### New Modules in the Add-in

The ribbon architecture in UtilityMenu is modular by design. New modules are added as self-contained classes under `Modules/`, registered in the DI container, and surfaced via new ribbon controls. The corresponding `Module` record and `ModuleId` (fixed GUID) must be registered in UtilityMenuSite first, before the add-in ships, to ensure the entitlements endpoint returns the new module name.

---

*Last updated: 2026-02-26. Update this file whenever the API contract, database schema, or architectural boundaries change.*
