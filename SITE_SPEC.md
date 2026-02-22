# UtilityMenuSite — Blueprint Document

**Version:** 1.0
**Date:** 2026-02-22
**Author:** Martin Hanna
**Status:** Authoritative — Supersedes all prior design notes

---

## Table of Contents

1. [Document Purpose & Scope](#1-document-purpose--scope)
2. [High-Level Architecture](#2-high-level-architecture)
3. [Solution Structure](#3-solution-structure)
4. [Hosting Model](#4-hosting-model)
5. [Authentication & Authorization Model](#5-authentication--authorization-model)
6. [Database Schema](#6-database-schema)
7. [Stripe Integration Blueprint (Refactored)](#7-stripe-integration-blueprint-refactored)
8. [UtilityMenu Add-in Refactor — Stripe Removal](#8-utilitymenu-add-in-refactor--stripe-removal)
9. [Licensing API (Add-in → Website)](#9-licensing-api-add-in--website)
10. [Page-by-Page Specification](#10-page-by-page-specification)
11. [Component & Layout Structure](#11-component--layout-structure)
12. [API Endpoint Reference](#12-api-endpoint-reference)
13. [Configuration & Secrets Management](#13-configuration--secrets-management)
14. [CI/CD & Deployment](#14-cicd--deployment)
15. [Future-Proofing & Extensibility](#15-future-proofing--extensibility)
16. [Naming Conventions & Coding Standards](#16-naming-conventions--coding-standards)

---

## 1. Document Purpose & Scope

This blueprint defines the complete architecture, database schema, page specifications, API design, Stripe integration, and CI/CD pipeline for **UtilityMenuSite** — the official web application for the UtilityMenu Excel add-in.

### What UtilityMenuSite Is

UtilityMenuSite is a **Blazor Web App (.NET 8)** hosted on Azure App Service. It is the authoritative back-end system for:

- Marketing the UtilityMenu product (landing page, pricing, blog, documentation)
- Creating and managing user accounts
- Processing payments and subscriptions via Stripe
- Issuing and managing Pro and Custom module licences
- Providing a secure REST API that the UtilityMenu add-in calls to validate licences, retrieve entitlements, and manage device activations

### Critical Architectural Mandate — Stripe Refactor

The UtilityMenu add-in currently contains Stripe SDK references, checkout session creation, webhook handling, and polling. **All of this must be removed from the add-in and moved into UtilityMenuSite.** The add-in must never hold a Stripe secret key or call the Stripe API directly.

After the refactor:

| Responsibility | Before | After |
|---|---|---|
| Stripe secret key | Stored in add-in config | Stored in UtilityMenuSite server config only |
| Checkout session creation | `StripeCheckoutService` in add-in | `CheckoutController` in UtilityMenuSite |
| Webhook handling | `StripeWebhookHandler` in add-in | `WebhookController` in UtilityMenuSite |
| Licence provisioning | `DatabaseService` called from add-in | `DatabaseService` called from UtilityMenuSite only |
| Licence validation | Add-in calls website API | Add-in calls website API (unchanged flow, refined contract) |
| User database | Partially in add-in scope | Exclusively in UtilityMenuSite SQL Server |

---

## 2. High-Level Architecture

```
+--------------------------------------------------+
|              USER'S BROWSER                      |
|  Blazor Web App (SSR + Interactive Server)       |
|  Pages: Landing, Pricing, Dashboard, Blog, Docs  |
+------------------+-------------------------------+
                   |
+------------------v-------------------------------+
|             UtilityMenuSite                      |
|  ASP.NET Core 8 Application                      |
|  +--------------------------------------------+  |
|  | Blazor Components (Pages + Layouts)         |  |
|  | Razor Pages (Stripe return URLs)            |  |
|  | API Controllers (/api/*)                    |  |
|  | Stripe Webhook Endpoint (/api/stripe/*)     |  |
|  +--------------------------------------------+  |
|  +--------------------------------------------+  |
|  | Service Layer                               |  |
|  | StripeService, LicenceService,              |  |
|  | UserService, BlogService, ContactService    |  |
|  +--------------------------------------------+  |
|  +--------------------------------------------+  |
|  | Data Layer (EF Core + SQL Server)           |  |
|  | Repositories, DbContext, Migrations         |  |
|  +--------------------------------------------+  |
+------------------+-------------------------------+
                   |                        |
     +-------------+                +-------+-------+
     |                              |               |
+----v-------+              +-------v----+   +------v-----+
| SQL Server |              |   Stripe   |   | Azure Blob |
| (Azure SQL)|              |   API      |   | Storage    |
+------------+              +------------+   | (Assets)   |
                                             +------------+
                   |
+------------------v-------------------------------+
|             UtilityMenu Add-in (Excel)           |
|  Calls:                                          |
|  POST /api/checkout/create                       |
|  GET  /api/checkout/status?sessionId=...         |
|  GET  /api/licence/validate?key=...              |
|  GET  /api/licence/entitlements?key=...          |
|  POST /api/licence/activate                      |
|  POST /api/licence/deactivate                    |
+--------------------------------------------------+
```

---

## 3. Solution Structure

```
UtilityMenuSite/                           # Solution root (GitHub repo: martinh67/UtilityMenuSite)
|
+-- src/
|   +-- UtilityMenuSite/                   # Main Blazor Web App + API
|   |   +-- Bootstrap/                     # Application entry point, DI registration
|   |   |   +-- Program.cs
|   |   |   +-- DependencyRegistration.cs
|   |   +-- Components/                    # Blazor components
|   |   |   +-- Layout/                    # MainLayout, NavBar, Footer
|   |   |   +-- Shared/                    # Reusable UI components
|   |   |   +-- Pages/                     # Route components (see Section 10)
|   |   +-- Controllers/                   # ASP.NET Core API controllers
|   |   |   +-- CheckoutController.cs
|   |   |   +-- LicenceController.cs
|   |   |   +-- WebhookController.cs
|   |   +-- Services/                      # Application service layer
|   |   |   +-- Payment/
|   |   |   +-- Licensing/
|   |   |   +-- User/
|   |   |   +-- Blog/
|   |   |   +-- Contact/
|   |   +-- Data/                          # EF Core data layer
|   |   |   +-- Context/                   # AppDbContext
|   |   |   +-- Models/                    # Entity models
|   |   |   +-- Repositories/             # Repository implementations
|   |   |   +-- Migrations/               # EF Core migrations
|   |   +-- Core/                          # Domain interfaces, DTOs, constants
|   |   |   +-- Interfaces/
|   |   |   +-- Models/
|   |   |   +-- Constants/
|   |   +-- Infrastructure/               # Cross-cutting concerns
|   |   |   +-- Logging/
|   |   |   +-- Configuration/
|   |   |   +-- Security/
|   |   +-- wwwroot/                       # Static assets
|   |   |   +-- css/
|   |   |   +-- js/
|   |   |   +-- downloads/                 # Synced by CI: Setup.exe, version.json
|   |   |   +-- img/
|   |   +-- appsettings.json
|   |   +-- appsettings.Development.json
|   |   +-- appsettings.UAT.json
|   |   +-- appsettings.Production.json
|   |   +-- UtilityMenuSite.csproj
|   |
|   +-- UtilityMenuSite.Tests/             # xUnit test project
|       +-- Services/
|       +-- Controllers/
|       +-- Data/
|       +-- UtilityMenuSite.Tests.csproj
|
+-- .github/
|   +-- workflows/                         # CI/CD workflows (flat, reusable-* prefix)
|   +-- actions/                           # Composite actions
|   +-- CICD.md                            # Pipeline documentation
|
+-- docs/                                  # Documentation
+-- scripts/                               # Build/deployment Python scripts
+-- version.json                           # Version manifest
+-- CLAUDE.md                              # Claude Code instructions
+-- README.md
```

---

## 4. Hosting Model

### Selection: Blazor Web App (.NET 8) — Interactive Server Mode

**Decision:** Blazor Web App with server-side rendering (SSR) for all marketing and static pages, and Interactive Server components for authenticated dashboard, checkout flows, and admin pages.

**Rationale:**

| Criterion | Blazor Server (Interactive) | Blazor WASM | Decision |
|---|---|---|---|
| SEO for marketing pages | Excellent (SSR) | Poor (client-only) | Server wins |
| Real-time dashboard updates | Native SignalR | Requires polling or WASM | Server wins |
| Stripe webhook handling | Native ASP.NET Core controllers | Requires separate API | Server wins |
| Deployment simplicity | Single Azure App Service | Two deployments (WASM + API) | Server wins |
| Offline capability | None | Limited | Not required |
| .NET 8 support | Full | Full | Equal |

**Rendering modes per page type:**

| Page Type | Render Mode |
|---|---|
| Landing, Pricing, Blog, Docs | Static SSR (no JS required) |
| User Dashboard, Account | `InteractiveServer` |
| Checkout flow | `InteractiveServer` |
| Admin pages | `InteractiveServer` |
| API Controllers | ASP.NET Core pipeline (no Blazor) |

### `Program.cs` Registration Outline

```csharp
var builder = WebApplication.CreateBuilder(args);

// Blazor
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// API Controllers (for licensing, checkout, webhooks)
builder.Services.AddControllers();

// EF Core + SQL Server
builder.Services.AddDbContext<AppDbContext>(opts =>
    opts.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// Application Services
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<ILicenceService, LicenceService>();
builder.Services.AddScoped<IStripeService, StripeService>();
builder.Services.AddScoped<IBlogService, BlogService>();
builder.Services.AddScoped<IContactService, ContactService>();

// Repositories
builder.Services.AddScoped<IUserRepository, UserRepository>();
// ... all repositories

// Authentication (ASP.NET Core Identity)
builder.Services.AddAuthentication(/* ... */);
builder.Services.AddAuthorization();

var app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();
app.UseStaticFiles();
app.UseAntiforgery();

app.MapControllers();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
```

---

## 5. Authentication & Authorization Model

### Identity Provider: ASP.NET Core Identity + SQL Server

The site uses **ASP.NET Core Identity** backed by the SQL Server database. This provides email/password authentication out of the box with a proven, extensible foundation.

### Authentication Flows

#### 5.1 Registration Flow

1. User completes registration form (email, password, display name)
2. Identity creates `AspNetUsers` record
3. Email verification sent (optional for MVP; required for Pro tier)
4. User record created in `Users` table (linked by `ExternalId = IdentityUser.Id`)

#### 5.2 Login Flow

1. User submits email + password
2. ASP.NET Core Identity validates credentials
3. Cookie issued (persistent, 30-day sliding expiry)
4. User redirected to dashboard

#### 5.3 External OAuth (Future)

Google OAuth can be added via `AddGoogle()` in `AddAuthentication()`. The `ExternalId` column in `Users` will hold the Google `sub` claim.

### Authorization Policies

```csharp
builder.Services.AddAuthorization(opts =>
{
    opts.AddPolicy("RequireAuthenticated", p => p.RequireAuthenticatedUser());
    opts.AddPolicy("RequireProLicence",    p => p.RequireClaim("licence_type", "pro", "custom"));
    opts.AddPolicy("RequireAdmin",         p => p.RequireRole("Admin"));
});
```

### API Authentication (Add-in Calls)

The licensing API endpoints called by the add-in are authenticated via **API key** (the licence key itself acts as the credential for entitlement queries). Checkout creation endpoints require the user's `ApiToken` issued at registration.

| Endpoint Group | Auth Method |
|---|---|
| `/api/licence/*` | Licence key in query string or header |
| `/api/checkout/*` | Bearer API token |
| `/api/stripe/webhook` | Stripe webhook signature (HMAC) |
| `/api/admin/*` | Admin role cookie |

---

## 6. Database Schema

### 6.1 Entity Relationship Overview

```
AspNetUsers (Identity)
    |  1:1
    v
Users
    |  1:many
    +-> StripeCustomers
    |       |  1:many
    |       v
    |   Subscriptions
    |       |  1:many
    |       v
    |   Licences
    |       |  many:many (via LicenceModules)
    |       v
    |   Modules
    |
    +-> Machines (via Licences)
    +-> UsageEvents
    +-> ApiTokens

StripeWebhookEvents (audit/idempotency)
BlogPosts -> BlogCategories
ContactSubmissions
AuditLogs
```

---

### 6.2 Full Table Definitions

#### Table: `Users`

```sql
CREATE TABLE Users (
    UserId          UNIQUEIDENTIFIER    NOT NULL DEFAULT NEWSEQUENTIALID(),
    IdentityId      NVARCHAR(450)       NOT NULL,    -- FK -> AspNetUsers.Id
    Email           NVARCHAR(256)       NOT NULL,
    DisplayName     NVARCHAR(100)       NULL,
    ExternalId      NVARCHAR(200)       NULL,        -- OAuth sub claim
    ApiToken        NVARCHAR(64)        NOT NULL,    -- Add-in API auth token
    IsActive        BIT                 NOT NULL DEFAULT 1,
    CreatedAt       DATETIME2           NOT NULL DEFAULT SYSUTCDATETIME(),
    UpdatedAt       DATETIME2           NOT NULL DEFAULT SYSUTCDATETIME(),

    CONSTRAINT PK_Users PRIMARY KEY (UserId),
    CONSTRAINT UQ_Users_Email UNIQUE (Email),
    CONSTRAINT UQ_Users_IdentityId UNIQUE (IdentityId),
    CONSTRAINT UQ_Users_ApiToken UNIQUE (ApiToken)
);
CREATE INDEX IX_Users_Email      ON Users (Email);
CREATE INDEX IX_Users_IdentityId ON Users (IdentityId);
```

**Notes:**
- `IdentityId` links to ASP.NET Core Identity's `AspNetUsers.Id`
- `ApiToken` is a 64-character cryptographically random token issued at registration; the add-in stores this and sends it in the `Authorization: Bearer` header for checkout requests
- `ExternalId` is populated when the user signs in via OAuth

---

#### Table: `StripeCustomers`

```sql
CREATE TABLE StripeCustomers (
    StripeCustomerId    NVARCHAR(100)       NOT NULL,    -- Stripe cus_xxx
    UserId              UNIQUEIDENTIFIER    NOT NULL,
    Email               NVARCHAR(256)       NOT NULL,
    CreatedAt           DATETIME2           NOT NULL DEFAULT SYSUTCDATETIME(),

    CONSTRAINT PK_StripeCustomers PRIMARY KEY (StripeCustomerId),
    CONSTRAINT FK_StripeCustomers_Users FOREIGN KEY (UserId)
        REFERENCES Users(UserId) ON DELETE CASCADE,
    CONSTRAINT UQ_StripeCustomers_UserId UNIQUE (UserId)
);
CREATE INDEX IX_StripeCustomers_UserId ON StripeCustomers (UserId);
```

**Notes:**
- One Stripe customer per user (enforced by `UQ_StripeCustomers_UserId`)
- Created on first checkout session, before payment completes
- `StripeCustomerId` is the primary key because Stripe events reference it

---

#### Table: `Subscriptions`

```sql
CREATE TABLE Subscriptions (
    SubscriptionId          UNIQUEIDENTIFIER    NOT NULL DEFAULT NEWSEQUENTIALID(),
    UserId                  UNIQUEIDENTIFIER    NOT NULL,
    StripeCustomerId        NVARCHAR(100)       NOT NULL,
    StripeSubscriptionId    NVARCHAR(100)       NOT NULL,
    StripePriceId           NVARCHAR(100)       NULL,
    Status                  NVARCHAR(50)        NOT NULL,  -- active|trialing|past_due|canceled|paused|grace_period
    PlanType                NVARCHAR(50)        NOT NULL,  -- monthly|annual|lifetime
    CurrentPeriodStart      DATETIME2           NULL,
    CurrentPeriodEnd        DATETIME2           NULL,
    GracePeriodEnd          DATETIME2           NULL,
    CancelAtPeriodEnd       BIT                 NOT NULL DEFAULT 0,
    CanceledAt              DATETIME2           NULL,
    TrialStart              DATETIME2           NULL,
    TrialEnd                DATETIME2           NULL,
    CreatedAt               DATETIME2           NOT NULL DEFAULT SYSUTCDATETIME(),
    UpdatedAt               DATETIME2           NOT NULL DEFAULT SYSUTCDATETIME(),

    CONSTRAINT PK_Subscriptions PRIMARY KEY (SubscriptionId),
    CONSTRAINT FK_Subscriptions_Users FOREIGN KEY (UserId)
        REFERENCES Users(UserId),
    CONSTRAINT UQ_Subscriptions_StripeId UNIQUE (StripeSubscriptionId),
    CONSTRAINT CK_Subscriptions_Status CHECK (Status IN (
        'active','trialing','past_due','canceled','paused','grace_period','lifetime'
    ))
);
CREATE INDEX IX_Subscriptions_UserId               ON Subscriptions (UserId);
CREATE INDEX IX_Subscriptions_StripeCustomerId     ON Subscriptions (StripeCustomerId);
CREATE INDEX IX_Subscriptions_StripeSubscriptionId ON Subscriptions (StripeSubscriptionId);
```

---

#### Table: `Licences`

```sql
CREATE TABLE Licences (
    LicenceId       UNIQUEIDENTIFIER    NOT NULL DEFAULT NEWSEQUENTIALID(),
    UserId          UNIQUEIDENTIFIER    NOT NULL,
    SubscriptionId  UNIQUEIDENTIFIER    NOT NULL,
    LicenceKey      NVARCHAR(30)        NOT NULL,    -- UMENU-XXXX-XXXX-XXXX
    LicenceType     NVARCHAR(30)        NOT NULL,    -- individual|team|lifetime|custom
    MaxActivations  INT                 NOT NULL DEFAULT 2,
    IsActive        BIT                 NOT NULL DEFAULT 1,
    ExpiresAt       DATETIME2           NULL,
    LastValidatedAt DATETIME2           NULL,
    Signature       NVARCHAR(512)       NULL,        -- HMAC of licence payload
    CreatedAt       DATETIME2           NOT NULL DEFAULT SYSUTCDATETIME(),
    UpdatedAt       DATETIME2           NOT NULL DEFAULT SYSUTCDATETIME(),

    CONSTRAINT PK_Licences PRIMARY KEY (LicenceId),
    CONSTRAINT FK_Licences_Users FOREIGN KEY (UserId)
        REFERENCES Users(UserId),
    CONSTRAINT FK_Licences_Subscriptions FOREIGN KEY (SubscriptionId)
        REFERENCES Subscriptions(SubscriptionId),
    CONSTRAINT UQ_Licences_LicenceKey UNIQUE (LicenceKey),
    CONSTRAINT CK_Licences_LicenceType CHECK (LicenceType IN (
        'individual','team','lifetime','custom'
    ))
);
CREATE INDEX IX_Licences_UserId         ON Licences (UserId);
CREATE INDEX IX_Licences_SubscriptionId ON Licences (SubscriptionId);
CREATE INDEX IX_Licences_LicenceKey     ON Licences (LicenceKey);
CREATE INDEX IX_Licences_IsActive       ON Licences (IsActive) WHERE IsActive = 1;
```

---

#### Table: `Modules`

```sql
CREATE TABLE Modules (
    ModuleId        UNIQUEIDENTIFIER    NOT NULL DEFAULT NEWSEQUENTIALID(),
    ModuleName      NVARCHAR(100)       NOT NULL,    -- Must match control.Tag in ribbon XML
    DisplayName     NVARCHAR(200)       NOT NULL,
    Description     NVARCHAR(500)       NULL,
    Tier            NVARCHAR(30)        NOT NULL,    -- core|pro|custom
    IsActive        BIT                 NOT NULL DEFAULT 1,
    SortOrder       INT                 NOT NULL DEFAULT 0,
    CreatedAt       DATETIME2           NOT NULL DEFAULT SYSUTCDATETIME(),

    CONSTRAINT PK_Modules PRIMARY KEY (ModuleId),
    CONSTRAINT UQ_Modules_ModuleName UNIQUE (ModuleName),
    CONSTRAINT CK_Modules_Tier CHECK (Tier IN ('core','pro','custom'))
);
```

**Seed data (matches ribbon XML `control.Tag` values):**

| ModuleName | DisplayName | Tier |
|---|---|---|
| `GetLastRow` | Get Last Row | core |
| `GetLastColumn` | Get Last Column | core |
| `UnhideRows` | Unhide Rows | core |
| `AdvancedData` | Advanced Data Tools | pro |
| `BulkOperations` | Bulk Operations | pro |
| `DataExport` | Data Export | pro |
| `SqlBuilder` | SQL Builder | pro |

---

#### Table: `LicenceModules`

```sql
CREATE TABLE LicenceModules (
    LicenceModuleId UNIQUEIDENTIFIER    NOT NULL DEFAULT NEWSEQUENTIALID(),
    LicenceId       UNIQUEIDENTIFIER    NOT NULL,
    ModuleId        UNIQUEIDENTIFIER    NOT NULL,
    ExpiresAt       DATETIME2           NULL,        -- NULL = no per-module expiry
    GrantedAt       DATETIME2           NOT NULL DEFAULT SYSUTCDATETIME(),

    CONSTRAINT PK_LicenceModules PRIMARY KEY (LicenceModuleId),
    CONSTRAINT FK_LicenceModules_Licences FOREIGN KEY (LicenceId)
        REFERENCES Licences(LicenceId) ON DELETE CASCADE,
    CONSTRAINT FK_LicenceModules_Modules FOREIGN KEY (ModuleId)
        REFERENCES Modules(ModuleId),
    CONSTRAINT UQ_LicenceModules UNIQUE (LicenceId, ModuleId)
);
CREATE INDEX IX_LicenceModules_LicenceId ON LicenceModules (LicenceId);
```

---

#### Table: `Machines`

```sql
CREATE TABLE Machines (
    MachineId           UNIQUEIDENTIFIER    NOT NULL DEFAULT NEWSEQUENTIALID(),
    LicenceId           UNIQUEIDENTIFIER    NOT NULL,
    MachineFingerprint  NVARCHAR(200)       NOT NULL,
    MachineName         NVARCHAR(200)       NULL,
    IsActive            BIT                 NOT NULL DEFAULT 1,
    FirstSeenAt         DATETIME2           NOT NULL DEFAULT SYSUTCDATETIME(),
    LastSeenAt          DATETIME2           NOT NULL DEFAULT SYSUTCDATETIME(),
    DeactivatedAt       DATETIME2           NULL,

    CONSTRAINT PK_Machines PRIMARY KEY (MachineId),
    CONSTRAINT FK_Machines_Licences FOREIGN KEY (LicenceId)
        REFERENCES Licences(LicenceId) ON DELETE CASCADE
);
CREATE INDEX IX_Machines_LicenceId             ON Machines (LicenceId);
CREATE INDEX IX_Machines_LicenceId_Fingerprint ON Machines (LicenceId, MachineFingerprint);
CREATE INDEX IX_Machines_IsActive              ON Machines (IsActive) WHERE IsActive = 1;
```

---

#### Table: `StripeWebhookEvents`

```sql
CREATE TABLE StripeWebhookEvents (
    WebhookEventId  UNIQUEIDENTIFIER    NOT NULL DEFAULT NEWSEQUENTIALID(),
    StripeEventId   NVARCHAR(100)       NOT NULL,
    EventType       NVARCHAR(100)       NOT NULL,
    RawPayload      NVARCHAR(MAX)       NOT NULL,
    ProcessedAt     DATETIME2           NULL,
    FailedAt        DATETIME2           NULL,
    ErrorMessage    NVARCHAR(MAX)       NULL,
    ReceivedAt      DATETIME2           NOT NULL DEFAULT SYSUTCDATETIME(),

    CONSTRAINT PK_StripeWebhookEvents PRIMARY KEY (WebhookEventId),
    CONSTRAINT UQ_StripeWebhookEvents_StripeEventId UNIQUE (StripeEventId)
);
CREATE INDEX IX_StripeWebhookEvents_ProcessedAt ON StripeWebhookEvents (ProcessedAt)
    WHERE ProcessedAt IS NULL;
```

---

#### Table: `ApiTokens`

```sql
CREATE TABLE ApiTokens (
    TokenId     UNIQUEIDENTIFIER    NOT NULL DEFAULT NEWSEQUENTIALID(),
    UserId      UNIQUEIDENTIFIER    NOT NULL,
    Token       NVARCHAR(64)        NOT NULL,
    Name        NVARCHAR(100)       NOT NULL DEFAULT 'Add-in Token',
    IsActive    BIT                 NOT NULL DEFAULT 1,
    CreatedAt   DATETIME2           NOT NULL DEFAULT SYSUTCDATETIME(),
    LastUsedAt  DATETIME2           NULL,
    ExpiresAt   DATETIME2           NULL,

    CONSTRAINT PK_ApiTokens PRIMARY KEY (TokenId),
    CONSTRAINT FK_ApiTokens_Users FOREIGN KEY (UserId)
        REFERENCES Users(UserId) ON DELETE CASCADE,
    CONSTRAINT UQ_ApiTokens_Token UNIQUE (Token)
);
CREATE INDEX IX_ApiTokens_Token ON ApiTokens (Token);
```

---

#### Table: `BlogCategories`

```sql
CREATE TABLE BlogCategories (
    CategoryId  UNIQUEIDENTIFIER    NOT NULL DEFAULT NEWSEQUENTIALID(),
    Name        NVARCHAR(100)       NOT NULL,
    Slug        NVARCHAR(100)       NOT NULL,
    SortOrder   INT                 NOT NULL DEFAULT 0,

    CONSTRAINT PK_BlogCategories PRIMARY KEY (CategoryId),
    CONSTRAINT UQ_BlogCategories_Slug UNIQUE (Slug)
);
```

---

#### Table: `BlogPosts`

```sql
CREATE TABLE BlogPosts (
    PostId          UNIQUEIDENTIFIER    NOT NULL DEFAULT NEWSEQUENTIALID(),
    CategoryId      UNIQUEIDENTIFIER    NOT NULL,
    AuthorId        UNIQUEIDENTIFIER    NULL,        -- FK -> Users.UserId
    Title           NVARCHAR(300)       NOT NULL,
    Slug            NVARCHAR(300)       NOT NULL,
    Summary         NVARCHAR(500)       NULL,
    Body            NVARCHAR(MAX)       NOT NULL,    -- Markdown or HTML
    CoverImageUrl   NVARCHAR(500)       NULL,
    IsPublished     BIT                 NOT NULL DEFAULT 0,
    IsFeatured      BIT                 NOT NULL DEFAULT 0,
    PublishedAt     DATETIME2           NULL,
    CreatedAt       DATETIME2           NOT NULL DEFAULT SYSUTCDATETIME(),
    UpdatedAt       DATETIME2           NOT NULL DEFAULT SYSUTCDATETIME(),
    MetaTitle       NVARCHAR(200)       NULL,
    MetaDescription NVARCHAR(500)       NULL,

    CONSTRAINT PK_BlogPosts PRIMARY KEY (PostId),
    CONSTRAINT FK_BlogPosts_Categories FOREIGN KEY (CategoryId)
        REFERENCES BlogCategories(CategoryId),
    CONSTRAINT FK_BlogPosts_Users FOREIGN KEY (AuthorId)
        REFERENCES Users(UserId),
    CONSTRAINT UQ_BlogPosts_Slug UNIQUE (Slug)
);
CREATE INDEX IX_BlogPosts_IsPublished ON BlogPosts (IsPublished, PublishedAt DESC)
    WHERE IsPublished = 1;
CREATE INDEX IX_BlogPosts_CategoryId  ON BlogPosts (CategoryId);
```

---

#### Table: `ContactSubmissions`

```sql
CREATE TABLE ContactSubmissions (
    SubmissionId    UNIQUEIDENTIFIER    NOT NULL DEFAULT NEWSEQUENTIALID(),
    Name            NVARCHAR(200)       NOT NULL,
    Email           NVARCHAR(256)       NOT NULL,
    Subject         NVARCHAR(300)       NOT NULL,
    Message         NVARCHAR(MAX)       NOT NULL,
    IsResolved      BIT                 NOT NULL DEFAULT 0,
    ResolvedAt      DATETIME2           NULL,
    Notes           NVARCHAR(MAX)       NULL,
    SubmittedAt     DATETIME2           NOT NULL DEFAULT SYSUTCDATETIME(),
    IpAddress       NVARCHAR(45)        NULL,

    CONSTRAINT PK_ContactSubmissions PRIMARY KEY (SubmissionId)
);
CREATE INDEX IX_ContactSubmissions_IsResolved ON ContactSubmissions (IsResolved, SubmittedAt DESC);
```

---

#### Table: `UsageEvents`

```sql
CREATE TABLE UsageEvents (
    EventId     UNIQUEIDENTIFIER    NOT NULL DEFAULT NEWSEQUENTIALID(),
    UserId      UNIQUEIDENTIFIER    NULL,
    LicenceId   UNIQUEIDENTIFIER    NULL,
    MachineId   UNIQUEIDENTIFIER    NULL,
    ModuleId    UNIQUEIDENTIFIER    NULL,
    EventType   NVARCHAR(100)       NOT NULL,
    EventData   NVARCHAR(MAX)       NULL,
    OccurredAt  DATETIME2           NOT NULL DEFAULT SYSUTCDATETIME(),

    CONSTRAINT PK_UsageEvents PRIMARY KEY (EventId),
    CONSTRAINT FK_UsageEvents_Users     FOREIGN KEY (UserId)     REFERENCES Users(UserId),
    CONSTRAINT FK_UsageEvents_Licences  FOREIGN KEY (LicenceId)  REFERENCES Licences(LicenceId),
    CONSTRAINT FK_UsageEvents_Machines  FOREIGN KEY (MachineId)  REFERENCES Machines(MachineId),
    CONSTRAINT FK_UsageEvents_Modules   FOREIGN KEY (ModuleId)   REFERENCES Modules(ModuleId)
);
CREATE INDEX IX_UsageEvents_UserId    ON UsageEvents (UserId, OccurredAt DESC);
CREATE INDEX IX_UsageEvents_EventType ON UsageEvents (EventType, OccurredAt DESC);
```

---

#### Table: `AuditLogs`

```sql
CREATE TABLE AuditLogs (
    AuditLogId      UNIQUEIDENTIFIER    NOT NULL DEFAULT NEWSEQUENTIALID(),
    UserId          UNIQUEIDENTIFIER    NULL,
    Action          NVARCHAR(100)       NOT NULL,
    EntityName      NVARCHAR(100)       NULL,
    EntityId        NVARCHAR(100)       NULL,
    OldValues       NVARCHAR(MAX)       NULL,
    NewValues       NVARCHAR(MAX)       NULL,
    IpAddress       NVARCHAR(45)        NULL,
    OccurredAt      DATETIME2           NOT NULL DEFAULT SYSUTCDATETIME(),

    CONSTRAINT PK_AuditLogs PRIMARY KEY (AuditLogId)
);
CREATE INDEX IX_AuditLogs_UserId ON AuditLogs (UserId, OccurredAt DESC);
CREATE INDEX IX_AuditLogs_Action ON AuditLogs (Action, OccurredAt DESC);
```

---

### 6.3 EF Core DbContext

```csharp
namespace UtilityMenuSite.Data.Context;

public class AppDbContext : IdentityDbContext<ApplicationUser>
{
    public DbSet<User>                  Users                 { get; set; } = null!;
    public DbSet<StripeCustomer>        StripeCustomers       { get; set; } = null!;
    public DbSet<Subscription>          Subscriptions         { get; set; } = null!;
    public DbSet<Licence>               Licences              { get; set; } = null!;
    public DbSet<Module>                Modules               { get; set; } = null!;
    public DbSet<LicenceModule>         LicenceModules        { get; set; } = null!;
    public DbSet<Machine>               Machines              { get; set; } = null!;
    public DbSet<StripeWebhookEvent>    StripeWebhookEvents   { get; set; } = null!;
    public DbSet<ApiToken>              ApiTokens             { get; set; } = null!;
    public DbSet<BlogCategory>          BlogCategories        { get; set; } = null!;
    public DbSet<BlogPost>              BlogPosts             { get; set; } = null!;
    public DbSet<ContactSubmission>     ContactSubmissions    { get; set; } = null!;
    public DbSet<UsageEvent>            UsageEvents           { get; set; } = null!;
    public DbSet<AuditLog>              AuditLogs             { get; set; } = null!;

    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        // Apply all IEntityTypeConfiguration<T> classes in Data/Configuration/
        builder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
```

---

## 7. Stripe Integration Blueprint (Refactored)

### 7.1 Architecture Overview

After the refactor, the Stripe integration is **exclusively within UtilityMenuSite**. The add-in never calls Stripe. The add-in never holds a Stripe secret key.

```
Add-in (Excel)
    |
    | POST /api/checkout/create {priceId, customerEmail}
    v
UtilityMenuSite (CheckoutController)
    |
    | StripeService.CreateCheckoutSessionAsync(...)
    v
Stripe API
    |
    | Returns {url, sessionId}
    v
UtilityMenuSite (CheckoutController)
    |
    | Returns {url, sessionId} to add-in
    v
Add-in opens browser to url
    |
    | User completes payment on Stripe Hosted Checkout
    v
Stripe
    |
    | Redirect to SuccessUrl: /checkout/success?session_id=...
    v
UtilityMenuSite (Razor Page: /checkout/success)
    |
    | Add-in polls GET /api/checkout/status?sessionId=...
    v
UtilityMenuSite (CheckoutController)
    |
    | StripeService.GetSessionStatusAsync(sessionId)
    v
Stripe API or DB cache -> Returns {status, licenceKey}
    |
    v
Add-in stores licenceKey locally, updates UI

SEPARATELY (reliable, server-side):
Stripe -> POST /api/stripe/webhook
    |
    v
UtilityMenuSite (WebhookController)
    |
    | Verify signature
    | Store in StripeWebhookEvents (idempotent)
    | Route to handler (checkout.session.completed, invoice.paid, etc.)
    | DatabaseService.SyncSubscriptionAsync(...)
    | LicenceService.ProvisionLicenceAsync(...)
```

### 7.2 Stripe Products & Prices Configuration

Define these in the Stripe Dashboard and reference by Price ID in `appsettings.Production.json`:

| Product | Price ID Key | Billing |
|---|---|---|
| UtilityMenu Pro - Monthly | `Stripe:Prices:ProMonthly` | GBP 5.00/month |
| UtilityMenu Pro - Annual | `Stripe:Prices:ProAnnual` | GBP 45.00/year |
| UtilityMenu Custom Module | `Stripe:Prices:CustomModule` | One-time or recurring |

### 7.3 StripeService Implementation

```csharp
namespace UtilityMenuSite.Services.Payment;

public class StripeService : IStripeService
{
    private readonly StripeSettings _settings;
    private readonly ILicenceService _licenceService;
    private readonly IUserService _userService;
    private readonly ILogger<StripeService> _logger;

    public StripeService(
        IOptions<StripeSettings> settings,
        ILicenceService licenceService,
        IUserService userService,
        ILogger<StripeService> logger)
    {
        _settings = settings.Value;
        _licenceService = licenceService;
        _userService = userService;
        _logger = logger;

        StripeConfiguration.ApiKey = _settings.SecretKey;
    }

    public async Task<CheckoutSessionResult> CreateCheckoutSessionAsync(
        string priceId,
        string customerEmail,
        string mode,          // "subscription" or "payment"
        CancellationToken ct = default)
    {
        // Look up or create Stripe customer for this email
        var stripeCustomerId = await GetOrCreateStripeCustomerAsync(customerEmail, ct);

        var options = new SessionCreateOptions
        {
            Customer = stripeCustomerId,
            PaymentMethodTypes = new List<string> { "card" },
            LineItems = new List<SessionLineItemOptions>
            {
                new() { Price = priceId, Quantity = 1 }
            },
            Mode = mode,
            SuccessUrl = $"{_settings.BaseUrl}/checkout/success?session_id={{CHECKOUT_SESSION_ID}}",
            CancelUrl  = $"{_settings.BaseUrl}/pricing"
        };

        var service = new SessionService();
        var session = await service.CreateAsync(options, cancellationToken: ct);

        return new CheckoutSessionResult
        {
            SessionId    = session.Id,
            CheckoutUrl  = session.Url,
            CustomerId   = stripeCustomerId
        };
    }

    public async Task<SessionStatusResult> GetSessionStatusAsync(
        string sessionId,
        CancellationToken ct = default)
    {
        var service = new SessionService();
        var session = await service.GetAsync(sessionId, cancellationToken: ct);

        // Look up any provisioned licence for this session's customer
        var licenceKey = await _licenceService
            .GetLicenceKeyForStripeCustomerAsync(session.CustomerId, ct);

        return new SessionStatusResult
        {
            SessionId     = sessionId,
            Status        = MapStatus(session.PaymentStatus, session.Status),
            LicenceKey    = licenceKey,
            CustomerEmail = session.CustomerDetails?.Email
        };
    }

    public async Task<BillingPortalResult> CreateBillingPortalSessionAsync(
        string stripeCustomerId,
        CancellationToken ct = default)
    {
        var options = new Stripe.BillingPortal.SessionCreateOptions
        {
            Customer  = stripeCustomerId,
            ReturnUrl = $"{_settings.BaseUrl}/dashboard"
        };
        var service = new Stripe.BillingPortal.SessionService();
        var session = await service.CreateAsync(options, cancellationToken: ct);
        return new BillingPortalResult { Url = session.Url };
    }

    private static string MapStatus(string? paymentStatus, string? sessionStatus)
    {
        if (paymentStatus == "paid") return "complete";
        if (sessionStatus == "expired") return "expired";
        if (paymentStatus == "unpaid" && sessionStatus == "complete") return "failed";
        return "pending";
    }
}
```

### 7.4 Webhook Handler

```csharp
// Controllers/WebhookController.cs
[ApiController]
[Route("api/stripe")]
public class WebhookController : ControllerBase
{
    private readonly IStripeWebhookService _webhookService;

    [HttpPost("webhook")]
    public async Task<IActionResult> Handle(CancellationToken ct)
    {
        var body      = await new StreamReader(Request.Body).ReadToEndAsync(ct);
        var signature = Request.Headers["Stripe-Signature"].ToString();

        var success = await _webhookService.ProcessAsync(body, signature, ct);

        return success ? Ok() : BadRequest();
    }
}
```

```csharp
// Services/Payment/StripeWebhookService.cs
public class StripeWebhookService : IStripeWebhookService
{
    private static readonly TimeSpan DefaultGracePeriod = TimeSpan.FromDays(7);

    public async Task<bool> ProcessAsync(
        string body, string signature, CancellationToken ct)
    {
        // 1. Verify signature
        Event stripeEvent;
        try
        {
            stripeEvent = EventUtility.ConstructEvent(
                body, signature, _settings.WebhookSecret);
        }
        catch (StripeException ex)
        {
            _logger.LogWarning("Webhook signature failed: {msg}", ex.Message);
            return false;
        }

        // 2. Idempotency check
        var alreadyProcessed = await _db.StripeWebhookEvents
            .AnyAsync(e => e.StripeEventId == stripeEvent.Id, ct);
        if (alreadyProcessed)
            return true;

        // 3. Store raw event
        var record = new StripeWebhookEvent
        {
            StripeEventId = stripeEvent.Id,
            EventType     = stripeEvent.Type,
            RawPayload    = body
        };
        _db.StripeWebhookEvents.Add(record);
        await _db.SaveChangesAsync(ct);

        // 4. Route and process
        try
        {
            await RouteEventAsync(stripeEvent, ct);
            record.ProcessedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            record.FailedAt      = DateTime.UtcNow;
            record.ErrorMessage  = ex.ToString();
            await _db.SaveChangesAsync(ct);
            throw; // Return 500 so Stripe retries
        }

        return true;
    }

    private async Task RouteEventAsync(Event e, CancellationToken ct)
    {
        switch (e.Type)
        {
            case EventTypes.CheckoutSessionCompleted:
                await HandleCheckoutCompletedAsync(
                    (Stripe.Checkout.Session)e.Data.Object, ct);
                break;
            case EventTypes.InvoicePaid:
                await HandleInvoicePaidAsync((Invoice)e.Data.Object, ct);
                break;
            case EventTypes.InvoicePaymentFailed:
                await HandlePaymentFailedAsync((Invoice)e.Data.Object, ct);
                break;
            case EventTypes.CustomerSubscriptionUpdated:
                await HandleSubscriptionUpdatedAsync((Subscription)e.Data.Object, ct);
                break;
            case EventTypes.CustomerSubscriptionDeleted:
                await HandleSubscriptionDeletedAsync((Subscription)e.Data.Object, ct);
                break;
            default:
                _logger.LogDebug("Unhandled event type: {type}", e.Type);
                break;
        }
    }

    private async Task HandleCheckoutCompletedAsync(
        Stripe.Checkout.Session session, CancellationToken ct)
    {
        var email = session.CustomerDetails?.Email ?? session.CustomerEmail;
        if (string.IsNullOrWhiteSpace(email))
        {
            _logger.LogError("checkout.session.completed: no email");
            return;
        }

        var user = await _userService.RegisterOrGetAsync(email, ct);

        await _licenceService.EnsureStripeCustomerAsync(
            user.UserId, session.CustomerId, email, ct);

        var isSubscription = session.Mode == "subscription";

        var subscription = await _licenceService.SyncSubscriptionAsync(
            stripeCustomerId: session.CustomerId,
            stripeSubId:      session.SubscriptionId ?? session.Id,
            status:           "active",
            userId:           user.UserId,
            planType:         isSubscription ? "monthly" : "lifetime",
            ct:               ct);

        var licenceKey = LicenceKeyGenerator.Generate();
        await _licenceService.ProvisionLicenceAsync(
            userId:         user.UserId,
            subscriptionId: subscription.SubscriptionId,
            licenceKey:     licenceKey,
            licenceType:    isSubscription ? "individual" : "lifetime",
            ct:             ct);
    }
}
```

### 7.5 Webhook Event Map

| Stripe Event | Handler Action |
|---|---|
| `checkout.session.completed` | Register user, create StripeCustomer, Subscription, Licence, grant module entitlements |
| `invoice.paid` | Update `Subscription.Status = active`, extend `CurrentPeriodEnd`, clear `GracePeriodEnd`, update linked `Licence.ExpiresAt` |
| `invoice.payment_failed` | Set `Status = past_due`, set `GracePeriodEnd = now + 7 days`, update `Licence.ExpiresAt = GracePeriodEnd` |
| `customer.subscription.updated` | Sync all billing fields, propagate `IsActive` to linked Licence |
| `customer.subscription.deleted` | Set `Status = canceled`, set `Licence.IsActive = false`, set `Licence.ExpiresAt = now` |

### 7.6 Billing Portal Integration

The user dashboard includes a "Manage Billing" button that:

1. Server calls `StripeService.CreateBillingPortalSessionAsync(stripeCustomerId)`
2. Returns the Stripe Billing Portal URL
3. Redirects user to Stripe's hosted portal
4. Stripe handles plan changes, payment method updates, and cancellations
5. Webhook events propagate all changes back to UtilityMenuSite

---

## 8. UtilityMenu Add-in Refactor — Stripe Removal

### 8.1 Files to Remove or Gut from UtilityMenu

| File | Action |
|---|---|
| `Services/Payment/StripeCheckoutService.cs` | Remove entirely |
| `Services/Payment/StripeWebhookHandler.cs` | Remove entirely |
| `Infrastructure/Payment/StripeSettings.cs` | Remove `SecretKey`, `WebhookSecret`, `PublishableKey`; retain only `ApiBaseUrl`, `PollingIntervalSeconds`, `PollingTimeoutMinutes` |
| `appsettings.json` Stripe section | Remove `SecretKey`, `WebhookSecret`, `PublishableKey` |
| NuGet: `Stripe.net` (UtilityMenu.csproj) | Remove |
| NuGet: `Stripe.net` (UtilityMenu.Api.csproj) | Remove |

### 8.2 Files to Retain and Modify

| File | Action |
|---|---|
| `Services/Payment/ApiCheckoutService.cs` | Retain — proxies through website API correctly |
| `Services/Payment/PaymentService.cs` | Retain — orchestrator; remove direct `StripeSettings.SecretKey` references |
| `Services/Payment/LicenceValidationService.cs` | Retain — calls website `/api/licence/entitlements` |
| `Services/Payment/PremiumAccessService.cs` | Retain — local licence cache |
| `Services/Payment/PaymentActionsService.cs` | Retain — UI coordination |

### 8.3 Add-in Configuration After Refactor

`appsettings.json` Stripe section becomes:

```json
{
  "Stripe": {
    "ApiBaseUrl": "https://utilitymenusite.azurewebsites.net",
    "PollingIntervalSeconds": 5,
    "PollingTimeoutMinutes": 15
  }
}
```

No Stripe keys exist in the add-in configuration at any level.

### 8.4 Add-in Payment Flow After Refactor

```
User clicks "Upgrade to Pro" in ribbon
    |
PremiumUpgradeDialogController opens dialog
    |
User clicks "Subscribe"
    |
ApiCheckoutService.CreateCheckoutSessionAsync(plan, email)
    |
    | POST https://site.../api/checkout/create
    | Body: { priceId, mode, customerEmail }
    v
Website returns { url, sessionId }
    |
Process.Start(url) -> opens browser to Stripe Hosted Checkout
    |
Background polling: ApiCheckoutService.WaitForCompletionAsync(sessionId, timeout)
    |
    | Polls GET https://site.../api/checkout/status?sessionId=...
    | until status = "complete" or timeout
    v
On complete: response includes { status, licenceKey }
    |
PaymentService.ActivateLicenseFromSession(licenceKey, email)
    |
PremiumAccessService.ActivateLicense(license) -> saved locally
    |
PaymentCompleted event -> UI updates ribbon
```

---

## 9. Licensing API (Add-in -> Website)

### 9.1 API Token Flow

At first add-in launch after registration:

1. User logs into website, navigates to Dashboard
2. Dashboard displays their `ApiToken` (from `ApiTokens` table)
3. User enters this token once in the add-in settings dialog
4. Add-in stores `ApiToken` in `UserSettings` (encrypted local settings)
5. Subsequent API calls include `Authorization: Bearer {ApiToken}`

### 9.2 Licence Key Flow

1. After payment, add-in receives `licenceKey` from `/api/checkout/status`
2. Add-in stores `licenceKey` in `UserSettings`
3. On every Excel session start, add-in calls `/api/licence/validate?key={licenceKey}`
4. On success, add-in calls `/api/licence/entitlements?key={licenceKey}` for module list
5. Module list cached locally for 7 days (staleness window)

---

## 10. Page-by-Page Specification

### Page 1: Home / Landing Page

**Route:** `/`
**Render Mode:** Static SSR

**Purpose:** Convert visitors to trial/purchase. Communicate the value proposition clearly.

**Required Components:**
- `HeroSection` — headline, sub-headline, CTA button ("Get UtilityMenu Free"), screenshot
- `FeaturesGrid` — 6 feature tiles (Core, Pro features overview)
- `ProBanner` — Pro upgrade call-to-action with pricing preview
- `BlogTeaser` — latest 3 blog posts
- `DownloadSection` — Setup.exe download button (reads `downloads/version.json`)
- `Footer`

**Required Backend:**
- `GET /` — renders page
- `IBlogService.GetLatestPostsAsync(count: 3)` — for blog teaser
- `VersionManifestService.GetLatestAsync()` — reads `downloads/version.json` for download button version

**Required UI Elements:**
- Hero: `<h1>` headline, `<p>` sub-headline, download CTA, product screenshot
- Features: icon + title + description cards in a responsive grid
- Pro banner: price, feature list, "Subscribe Now" -> `/pricing`
- Blog teaser: post cards with title, summary, date, category badge

**Navigation Flows:**
- "Get UtilityMenu Free" -> triggers `downloads/UtilityMenuInstaller.exe` download
- "Subscribe Now" -> `/pricing`
- Blog cards -> `/blog/{slug}`

**SEO Requirements:**
- `<title>UtilityMenu — Excel Add-in for Power Users</title>`
- Open Graph tags
- Canonical URL

---

### Page 2: Pricing & Plans

**Route:** `/pricing`
**Render Mode:** Static SSR (with Interactive for "Subscribe" button)

**Purpose:** Clearly present Free vs. Pro plans. Drive subscriptions.

**Required Components:**
- `PricingHeader` — page title, billing toggle (monthly/annual)
- `PricingCard` x2 — Free tier, Pro tier
- `FaqSection` — common questions about licensing, seats, refunds
- `ComparisonTable` — feature-by-feature Free vs. Pro

**Pricing Card Data:**

```
Free Card:
  - Price: GBP 0/month
  - Features: Core modules (Get Last Row, Get Last Column, Unhide Rows)
  - CTA: "Download Free" -> download Setup.exe

Pro Card:
  - Price: GBP 5/month or GBP 45/year (billing toggle)
  - Features: All Free + Advanced Data, Bulk Operations, Data Export, SQL Builder
  - Seats: Up to 2 machines
  - CTA: "Subscribe Now" -> opens checkout flow
```

**Required Stripe Interactions:**

"Subscribe Now" button:
1. If user logged in: call `POST /api/checkout/create` with `priceId`, user email
2. If user not logged in: redirect to `/account/register?returnUrl=/pricing`
3. Receive `{url, sessionId}`, redirect browser to `url`
4. Poll `/api/checkout/status?sessionId=...` until complete
5. On complete: show success message and licence key

**Required Validation:**
- User must be authenticated before initiating checkout (server enforces this)

**Navigation Flows:**
- "Download Free" -> Setup.exe download
- "Subscribe Now" -> Stripe Hosted Checkout -> `/checkout/success?session_id=...`
- FAQ links -> `/docs/licensing`

---

### Page 3: Checkout Success

**Route:** `/checkout/success`
**Render Mode:** Interactive Server
**Query:** `?session_id={stripeSessionId}`

**Purpose:** Confirm payment, display licence key, provide next steps.

**Required Components:**
- `CheckoutSuccessBanner` — animated success message
- `LicenceKeyDisplay` — shows licence key in a copyable code block
- `NextStepsGuide` — 3-step instructions: download -> install -> enter licence key

**Required Backend:**
- `GET /api/checkout/status?sessionId={id}` — polls until `status=complete`, returns `licenceKey`
- Timeout after 60 seconds; shows "still processing" message with contact link

**Required UI:**
- Loading spinner while polling
- Once complete: large green success message, licence key in `<code>` block with "Copy" button
- "Download UtilityMenu" button, "View your Dashboard" link -> `/dashboard`

---

### Page 4: Account Registration

**Route:** `/account/register`
**Render Mode:** Interactive Server

**Purpose:** Create new user account.

**Form Fields:**
- Display Name (required, 2-100 chars)
- Email Address (required, valid email format, unique check)
- Password (required, min 8 chars, at least 1 uppercase, 1 digit)
- Confirm Password (must match)
- Terms & Conditions checkbox (required)

**Required Backend:**
- `POST /account/register` (ASP.NET Core Identity handler)
  - Creates `AspNetUsers` record
  - Creates `Users` record with new `ApiToken`
  - Records `UsageEvents` row (`user_registered`)
  - Redirects to `returnUrl` or `/dashboard`

**Required Validation:**
- Client-side: Blazor `EditForm` with `DataAnnotationsValidator`
- Server-side: `UserManager.CreateAsync()` returns `IdentityResult`
- Duplicate email shows friendly error message

**Navigation Flows:**
- On success -> `/dashboard`
- Already have account -> `/account/login`
- Return URL preserved through `?returnUrl=` query parameter

---

### Page 5: Account Login

**Route:** `/account/login`
**Render Mode:** Interactive Server

**Purpose:** Authenticate returning users.

**Form Fields:**
- Email Address (required)
- Password (required)
- Remember Me checkbox
- Forgot Password link

**Required Backend:**
- `POST /account/login` (ASP.NET Core Identity `SignInManager`)
- Lockout after 5 failed attempts (Identity default)
- Issues authentication cookie on success

**Navigation Flows:**
- On success -> `returnUrl` or `/dashboard`
- Forgot Password -> `/account/forgot-password`
- Register -> `/account/register`

---

### Page 6: Forgot Password / Reset Password

**Route:** `/account/forgot-password`, `/account/reset-password`
**Render Mode:** Interactive Server

**Purpose:** Self-service password reset via email.

**Required Backend:**
- `POST /account/forgot-password` -> `UserManager.GeneratePasswordResetTokenAsync()` -> send email with link
- `POST /account/reset-password` -> `UserManager.ResetPasswordAsync(email, token, newPassword)`

---

### Page 7: User Dashboard

**Route:** `/dashboard`
**Render Mode:** Interactive Server
**Auth:** Requires `RequireAuthenticated` policy

**Purpose:** Central hub for the authenticated user — licence status, subscription management, API token, device list, download.

**Required Components:**
- `DashboardHeader` — welcome message, account email
- `LicenceStatusCard` — licence key, type, status (Active/Expired/None), expiry date
- `SubscriptionCard` — plan name, billing period, next renewal date, "Manage Billing" button
- `ApiTokenCard` — masked token, "Copy" button, "Regenerate" button
- `ActiveMachinesCard` — list of activated machines with "Deactivate" button per machine
- `DownloadCard` — current version, Setup.exe download link
- `ModuleEntitlementsCard` — list of granted modules (Pro tier)

**Required Backend:**
- `IUserService.GetCurrentUserAsync(userId)` — user details
- `ILicenceService.GetActiveLicenceAsync(userId)` — licence + modules
- `ILicenceService.GetActiveMachinesAsync(licenceId)` — machine list
- `ILicenceService.GetSubscriptionAsync(userId)` — subscription + billing dates
- `StripeService.CreateBillingPortalSessionAsync(stripeCustomerId)` — "Manage Billing"
- `IUserService.RegenerateApiTokenAsync(userId)` — regenerate token

**Required Stripe Interactions:**
- "Manage Billing" -> calls `StripeService.CreateBillingPortalSessionAsync()` -> redirect to Stripe portal

**Navigation Flows:**
- "Manage Billing" -> Stripe Billing Portal (external)
- "Download" -> `downloads/UtilityMenuInstaller.exe`
- "Deactivate Machine" -> `POST /api/licence/deactivate` -> refreshes machine list

---

### Page 8: Blog Index

**Route:** `/blog`
**Render Mode:** Static SSR

**Purpose:** List all published blog posts, filterable by category.

**Required Components:**
- `BlogHeader` — title, search box (future)
- `CategoryFilter` — pills for each `BlogCategory`
- `BlogPostGrid` — cards for each post (title, summary, date, category, cover image)
- `Pagination` — 12 posts per page

**Required Backend:**
- `IBlogService.GetPublishedPostsAsync(categorySlug, page, pageSize)`
- `IBlogService.GetCategoriesAsync()` — for filter pills

---

### Page 9: Blog Post

**Route:** `/blog/{slug}`
**Render Mode:** Static SSR

**Purpose:** Display individual blog post content.

**Required Components:**
- `BlogPostHeader` — title, author, date, category badge, cover image
- `BlogPostBody` — rendered Markdown/HTML content
- `BlogPostFooter` — related posts (3), social sharing links (future)
- `BackToBlog` — navigation link

**Required Backend:**
- `IBlogService.GetPostBySlugAsync(slug)` — returns `BlogPost` or 404
- `IBlogService.GetRelatedPostsAsync(categoryId, excludePostId, count: 3)`

**SEO:**
- `<title>{post.MetaTitle ?? post.Title} | UtilityMenu Blog</title>`
- `<meta name="description">` from `post.MetaDescription ?? post.Summary`
- Open Graph image from `post.CoverImageUrl`

---

### Page 10: Blog Admin Editor (Admin Only)

**Route:** `/admin/blog`, `/admin/blog/new`, `/admin/blog/{postId}/edit`
**Render Mode:** Interactive Server
**Auth:** Requires `RequireAdmin` policy

**Purpose:** Create, edit, publish, and delete blog posts.

**Form Fields:**
- Title (required)
- Slug (auto-generated from title, editable)
- Category (dropdown)
- Summary (required, max 500 chars)
- Body (Markdown editor with side-by-side preview)
- Cover Image URL
- Is Published (checkbox)
- Is Featured (checkbox)
- Meta Title (optional)
- Meta Description (optional)

**Required Backend:**
- `IBlogService.CreatePostAsync(dto)`
- `IBlogService.UpdatePostAsync(postId, dto)`
- `IBlogService.DeletePostAsync(postId)`
- `IBlogService.PublishPostAsync(postId)`

---

### Page 11: Documentation

**Route:** `/docs`, `/docs/{slug}`
**Render Mode:** Static SSR

**Purpose:** Host product documentation (getting started, feature guides, API reference, FAQ).

**Implementation:** Markdown files stored in `wwwroot/docs/` directory, rendered at request time. A `DocsService` reads and parses Markdown files from disk using Markdig.

**Document Structure:**

```
wwwroot/docs/
+-- getting-started/
|   +-- installation.md
|   +-- first-use.md
|   +-- trust-settings.md
+-- features/
|   +-- core-modules.md
|   +-- pro-modules.md
+-- licensing/
|   +-- overview.md
|   +-- activation.md
|   +-- faq.md
+-- api/
|   +-- reference.md
+-- changelog.md
```

---

### Page 12: Contact Page

**Route:** `/contact`
**Render Mode:** Interactive Server

**Purpose:** Allow users to submit support requests or enquiries.

**Form Fields:**
- Name (required, 2-200 chars)
- Email (required, valid format)
- Subject (required, dropdown: General Enquiry, Technical Support, Billing, Feature Request, Bug Report)
- Message (required, 20-5000 chars)
- Honeypot field (hidden, spam prevention)

**Required Backend:**
- `IContactService.SubmitAsync(dto)` — saves to `ContactSubmissions` table
- Optional: send notification email to admin via SMTP/SendGrid

**Required Validation:**
- `EditForm` with `DataAnnotationsValidator`
- Rate limiting: max 3 submissions per IP per hour
- Honeypot check (server-side)

**Navigation Flows:**
- On success: inline success message, form cleared
- Error: inline error message, form retained

---

### Page 13: Admin Dashboard

**Route:** `/admin`
**Render Mode:** Interactive Server
**Auth:** Requires `RequireAdmin` policy

**Purpose:** Central admin hub — user management, licence management, contact submissions, webhook retry.

**Required Components:**
- `AdminStatsPanel` — total users, active licences, recent signups
- `AdminUserSearch` — search users by email, view licence status
- `AdminContactQueue` — unresolved contact submissions with "Mark Resolved" action
- `AdminWebhookRetry` — list of failed webhook events with "Retry" button
- `AdminBlogLink` — quick link to `/admin/blog`

**Required Backend:**
- `IUserService.SearchUsersAsync(query)`
- `IContactService.GetPendingSubmissionsAsync()`
- `IStripeWebhookService.GetFailedEventsAsync()`
- `IStripeWebhookService.RetryEventAsync(webhookEventId)`

---

### Page 14: Admin — User Detail

**Route:** `/admin/users/{userId}`
**Render Mode:** Interactive Server
**Auth:** Requires `RequireAdmin` policy

**Purpose:** Inspect and manage an individual user account.

**Required Components:**
- `UserDetailPanel` — account info, API token (masked), registration date
- `SubscriptionPanel` — current subscription, billing history
- `LicencePanel` — licence key, type, status, expiry, module entitlements
- `MachineList` — activated machines with deactivation controls
- `AuditLogPanel` — last 50 audit events for this user

---

### Page 15: Privacy Policy & Terms

**Route:** `/privacy`, `/terms`
**Render Mode:** Static SSR

**Purpose:** Legal compliance. Static content pages with no database interaction.

---

## 11. Component & Layout Structure

### 11.1 Layout Hierarchy

```
App.razor
+-- MainLayout.razor                    # Default for marketing pages
|   +-- NavBar.razor                    # Top navigation
|   +-- <Body>                          # Page content slot
|   +-- Footer.razor
|
+-- AuthLayout.razor                    # Login/Register pages (no navbar)
|   +-- <Body>
|
+-- DashboardLayout.razor               # Authenticated dashboard
|   +-- DashboardNavBar.razor           # With user menu, logout
|   +-- DashboardSidebar.razor          # Links: Overview, Billing, Devices, Settings
|   +-- <Body>
|
+-- AdminLayout.razor                   # Admin pages
    +-- AdminNavBar.razor
    +-- AdminSidebar.razor
    +-- <Body>
```

### 11.2 Shared Components

| Component | Location | Purpose |
|---|---|---|
| `NavBar` | `Components/Layout/NavBar.razor` | Top navigation, responsive hamburger menu |
| `Footer` | `Components/Layout/Footer.razor` | Links: Privacy, Terms, GitHub, Contact |
| `LoadingSpinner` | `Components/Shared/LoadingSpinner.razor` | Loading state indicator |
| `AlertBanner` | `Components/Shared/AlertBanner.razor` | Success/warning/error banners |
| `CopyButton` | `Components/Shared/CopyButton.razor` | JS clipboard copy button |
| `MarkdownRenderer` | `Components/Shared/MarkdownRenderer.razor` | Renders Markdown to HTML (Markdig) |
| `PricingCard` | `Components/Shared/PricingCard.razor` | Reusable pricing tier card |
| `LicenceKeyDisplay` | `Components/Shared/LicenceKeyDisplay.razor` | Monospace code block with copy button |
| `PageTitle` | `Components/Shared/PageTitle.razor` | Consistent `<title>` + meta tags |
| `Pagination` | `Components/Shared/Pagination.razor` | Generic pagination control |

### 11.3 Styling Approach

**Framework:** Bootstrap 5.3

**Rationale:**
- Production-ready responsive grid
- Minimal JavaScript dependency for MVP
- Dark mode support via `data-bs-theme="dark"` (future)

**Custom CSS:** `wwwroot/css/site.css` for brand overrides:
- Brand colour: `#2563EB` (Excel-inspired blue)
- Accent: `#16A34A` (Pro badge green)
- Font: Inter (Google Fonts)

### 11.4 NavBar Structure

```
[UtilityMenu Logo]  [Home]  [Pricing]  [Docs]  [Blog]  ...  [Login]  [Get Started / Dashboard]
```

- Authenticated state: show "Dashboard" and avatar menu (Dashboard, Settings, Logout)
- Unauthenticated: show "Login" and "Get Started" (-> /account/register)

---

## 12. API Endpoint Reference

All API endpoints are under `/api/`. All responses are JSON. All errors follow:

```json
{
  "error": "Description of the error",
  "code": "ERROR_CODE"
}
```

---

### 12.1 Checkout Endpoints

#### `POST /api/checkout/create`

Creates a Stripe Checkout session. Called by the add-in to initiate payment.

**Auth:** `Authorization: Bearer {ApiToken}`

**Request Body:**

```json
{
  "priceId": "price_1T0LsaRde3KmVcOKcbd8qQY5",
  "mode": "subscription",
  "customerEmail": "user@example.com"
}
```

**Response 200 OK:**

```json
{
  "url": "https://checkout.stripe.com/c/pay/...",
  "sessionId": "cs_live_..."
}
```

**Response 401 Unauthorized:** API token invalid or missing
**Response 400 Bad Request:** Invalid `priceId` or `mode`

**Stripe:** `SessionService.CreateAsync()`

---

#### `GET /api/checkout/status`

Polls the status of a Checkout session. Called by the add-in after opening the browser.

**Auth:** `Authorization: Bearer {ApiToken}`

**Query:** `?sessionId=cs_live_...`

**Response 200 OK:**

```json
{
  "status": "complete",
  "licenceKey": "UMENU-ABCD-EFGH-IJKL",
  "customerEmail": "user@example.com"
}
```

Status values: `pending`, `complete`, `expired`, `failed`

`licenceKey` is only present when `status = "complete"`. Retrieved from `Licences` table by matching the session's `CustomerId` to `StripeCustomers`.

---

### 12.2 Licence Endpoints

#### `GET /api/licence/validate`

Fast validity check — is this licence key active and non-expired?

**Auth:** None (licence key is the credential)
**Query:** `?key=UMENU-ABCD-EFGH-IJKL`

**Response 200 OK:**

```json
{
  "isValid": true,
  "licenceType": "individual",
  "expiresAt": "2027-03-01T00:00:00Z"
}
```

**Response 200 OK (invalid):**

```json
{
  "isValid": false,
  "reason": "expired"
}
```

Reason values: `not_found`, `inactive`, `expired`

**Rate Limiting:** 60 requests/minute per source IP

---

#### `GET /api/licence/entitlements`

Returns the list of module names the licence is entitled to use. The add-in uses this to enable/disable ribbon buttons.

**Auth:** None (licence key is the credential)
**Query:** `?key=UMENU-ABCD-EFGH-IJKL`

**Response 200 OK:**

```json
{
  "isValid": true,
  "licenceKey": "UMENU-ABCD-EFGH-IJKL",
  "licenceType": "individual",
  "expiresAt": "2027-03-01T00:00:00Z",
  "modules": [
    "AdvancedData",
    "BulkOperations",
    "DataExport",
    "SqlBuilder"
  ],
  "signature": "base64-hmac-sha256..."
}
```

**Response 404 Not Found:** Licence key not found or inactive

**DB Query:**

```sql
SELECT m.ModuleName
FROM LicenceModules lm
JOIN Modules m ON lm.ModuleId = m.ModuleId
WHERE lm.LicenceId = @licenceId
  AND m.IsActive = 1
  AND (lm.ExpiresAt IS NULL OR lm.ExpiresAt > SYSUTCDATETIME())
```

**Signature:** HMAC-SHA256 of the JSON payload using the server signing key. The add-in validates this before trusting the payload.

**Side Effect:** Updates `Licence.LastValidatedAt` and inserts `UsageEvents` row (`licence_validated`)

---

#### `POST /api/licence/activate`

Activates a machine against a licence key. Called on first add-in launch with a new key.

**Auth:** `Authorization: Bearer {ApiToken}`

**Request Body:**

```json
{
  "licenceKey": "UMENU-ABCD-EFGH-IJKL",
  "machineFingerprint": "sha256-hash-of-machine-identifiers",
  "machineName": "MARTIN-LAPTOP"
}
```

**Response 200 OK:**

```json
{
  "machineId": "guid",
  "activatedAt": "2026-02-22T10:00:00Z",
  "activeCount": 1,
  "maxActivations": 2
}
```

**Response 400 Bad Request:**

```json
{
  "error": "Seat limit reached. This licence allows 2 active machines.",
  "code": "SEAT_LIMIT_EXCEEDED"
}
```

**Response 404 Not Found:** Licence key not found, inactive, or expired

**DB Operations:**
- Validate licence key
- Check for existing machine activation (re-activation path)
- Check active seat count against `MaxActivations`
- Insert `Machines` row
- Insert `UsageEvents` row (telemetry)

---

#### `POST /api/licence/deactivate`

Frees a machine seat.

**Auth:** `Authorization: Bearer {ApiToken}`

**Request Body:**

```json
{
  "machineId": "guid"
}
```

**Response 200 OK:**

```json
{
  "success": true
}
```

**DB Operations:**
- `UPDATE Machines SET IsActive = 0, DeactivatedAt = NOW() WHERE MachineId = @id`
- Insert `UsageEvents` row (telemetry)

---

### 12.3 Webhook Endpoint

#### `POST /api/stripe/webhook`

Receives and processes Stripe webhook events.

**Auth:** Stripe webhook signature (`Stripe-Signature` header, verified with `STRIPE_WEBHOOK_SECRET`)

**Request:** Raw JSON body from Stripe

**Response 200 OK:** Event accepted (processed or duplicate)
**Response 400 Bad Request:** Signature verification failed
**Response 500 Internal Server Error:** Processing error — Stripe will retry

---

### 12.4 Admin Endpoints (Internal)

| Method | Route | Purpose |
|---|---|---|
| `GET` | `/api/admin/users` | Search users |
| `GET` | `/api/admin/users/{id}` | User detail |
| `POST` | `/api/admin/webhooks/{id}/retry` | Retry failed webhook event |
| `GET` | `/api/admin/contacts` | List contact submissions |
| `PATCH` | `/api/admin/contacts/{id}/resolve` | Mark contact resolved |

All admin endpoints require `[Authorize(Roles = "Admin")]` attribute.

---

## 13. Configuration & Secrets Management

### 13.1 `appsettings.json` Structure

```json
{
  "ConnectionStrings": {
    "DefaultConnection": ""
  },
  "Stripe": {
    "PublishableKey": "",
    "SecretKey": "",
    "WebhookSecret": "",
    "BaseUrl": "",
    "Prices": {
      "ProMonthly": "",
      "ProAnnual": "",
      "CustomModule": ""
    }
  },
  "Licensing": {
    "HmacSigningKey": "",
    "StalenessWindowDays": 7,
    "GracePeriodDays": 7
  },
  "Email": {
    "Provider": "SendGrid",
    "ApiKey": "",
    "FromAddress": "noreply@utilitymenu.com",
    "FromName": "UtilityMenu"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  }
}
```

### 13.2 Environment-Specific Files

| File | Environment | Contains |
|---|---|---|
| `appsettings.json` | All | Structure, defaults, non-sensitive |
| `appsettings.Development.json` | Local dev | Localhost DB string, Stripe test keys |
| `appsettings.UAT.json` | UAT | UAT DB string, Stripe test keys, UAT base URL |
| `appsettings.Production.json` | Production | Placeholder values only — real secrets injected via Azure App Service Configuration |

### 13.3 GitHub Actions Secrets

| Secret Name | Purpose |
|---|---|
| `AZURE_WEBAPP_PUBLISH_PROFILE` | Azure App Service publish credentials |
| `AZURE_SQL_CONNECTIONSTRING_UAT` | UAT database connection string |
| `AZURE_SQL_CONNECTIONSTRING_PROD` | Production database connection string |
| `STRIPE_SECRET_KEY_UAT` | Stripe test secret key (UAT) |
| `STRIPE_SECRET_KEY_PROD` | Stripe live secret key (Production) |
| `STRIPE_WEBHOOK_SECRET_UAT` | Stripe webhook signing secret (UAT) |
| `STRIPE_WEBHOOK_SECRET_PROD` | Stripe webhook signing secret (Production) |
| `LICENCE_HMAC_KEY` | HMAC signing key for licence payloads |
| `GH_PAGES_PAT` | Cross-repo token (existing — for Setup.exe sync) |

### 13.4 Azure Key Vault (Future)

For production hardening, secrets should migrate from App Service Configuration to Azure Key Vault with Managed Identity access.

---

## 14. CI/CD & Deployment

### 14.1 Repository

UtilityMenuSite lives in its own GitHub repository: `martinh67/UtilityMenuSite`

The CI/CD pipeline follows the **same three-layer architecture** established in the UtilityMenu repo:

1. Orchestration layer (`ci.yml`, `deploy-*.yml`)
2. Reusable workflows (`reusable-*.yml`)
3. Composite actions (`actions/*/action.yml`)

### 14.2 Workflow Map

```
.github/
+-- workflows/
|   +-- ci.yml                          # Main orchestrator
|   +-- deploy-dev.yml                  # Dev: feature branches
|   +-- deploy-uat.yml                  # UAT: develop branch
|   +-- deploy-prod.yml                 # Production: main branch
|   +-- reusable-build.yml              # Build Blazor app
|   +-- reusable-test.yml               # Run xUnit tests
|   +-- reusable-migrate.yml            # Run EF Core migrations
|   +-- reusable-deploy-azure.yml       # Deploy to Azure App Service
|
+-- actions/
    +-- setup-dotnet/
    |   +-- action.yml                  # Setup .NET 8 + NuGet cache
    +-- setup-ef/
        +-- action.yml                  # Install dotnet-ef tool
```

### 14.3 `ci.yml` Jobs

1. `build` — calls `reusable-build.yml`
2. `test` — calls `reusable-test.yml` (depends on `build`)
3. `migrate-uat` — calls `reusable-migrate.yml` (`develop` branch only, depends on `test`)
4. `deploy-uat` — calls `reusable-deploy-azure.yml` (depends on `migrate-uat`, `develop` only)
5. `summary` — pipeline summary

### 14.4 `reusable-build.yml` — Build Blazor App

```yaml
name: Build Blazor App

on:
  workflow_call:
    inputs:
      configuration:
        type: string
        default: Release
    outputs:
      artifact-name:
        value: ${{ jobs.build.outputs.artifact-name }}

jobs:
  build:
    runs-on: ubuntu-latest
    outputs:
      artifact-name: site-build-${{ inputs.configuration }}

    steps:
      - name: Checkout code
        uses: actions/checkout@v4

      - name: Setup .NET
        uses: ./.github/actions/setup-dotnet

      - name: Restore dependencies
        run: dotnet restore src/UtilityMenuSite/UtilityMenuSite.csproj

      - name: Build
        run: |
          dotnet publish src/UtilityMenuSite/UtilityMenuSite.csproj \
            --configuration ${{ inputs.configuration }} \
            --output ./publish

      - name: Upload artifact
        uses: actions/upload-artifact@v4
        with:
          name: site-build-${{ inputs.configuration }}
          path: ./publish
          retention-days: 30
```

### 14.5 `reusable-migrate.yml` — Database Migrations

```yaml
name: Run EF Core Migrations

on:
  workflow_call:
    inputs:
      environment:
        type: string
        required: true    # Development|UAT|Production
    secrets:
      connection-string:
        required: true

jobs:
  migrate:
    runs-on: ubuntu-latest

    steps:
      - name: Checkout code
        uses: actions/checkout@v4

      - name: Setup .NET and EF tools
        uses: ./.github/actions/setup-ef

      - name: Run migrations
        run: |
          dotnet ef database update \
            --project src/UtilityMenuSite \
            --connection "${{ secrets.connection-string }}"
        env:
          ASPNETCORE_ENVIRONMENT: ${{ inputs.environment }}
```

**Important:** Migrations run automatically in UAT. Production migrations require manual approval via GitHub Environment protection rules. Never run migrations without a database backup.

### 14.6 Environment Configuration

| Environment | Trigger | Azure App | DB | Stripe Keys | Approval Required |
|---|---|---|---|---|---|
| Dev | `feature/**` push | No deploy | N/A | N/A | No |
| UAT | `develop` push | `utilitymenu-uat` | Azure SQL UAT | Test keys | No |
| Production | `main` push | `utilitymenu-prod` | Azure SQL Prod | Live keys | Yes |

### 14.7 Azure Infrastructure

| Resource | Name | SKU |
|---|---|---|
| App Service Plan | `utilitymenu-plan` | B2 (MVP), P1v3 (production) |
| App Service (UAT) | `utilitymenu-uat` | — |
| App Service (Prod) | `utilitymenu-prod` | — |
| Azure SQL Server | `utilitymenu-sql` | General Purpose S1 |
| Azure SQL DB (UAT) | `utilitymenu-uat-db` | — |
| Azure SQL DB (Prod) | `utilitymenu-prod-db` | — |
| Azure Blob Storage | `utilitymenuassets` | LRS, Standard |

### 14.8 Setup.exe Sync (Receiving Side)

The `martinh67/UtilityMenu` CI pipeline pushes `UtilityMenuInstaller.exe` and `version.json` into `martinh67/UtilityMenuSite/wwwroot/downloads/` via `reusable-deploy-website.yml`.

UtilityMenuSite serves these as static files. No additional workflow action is required on the UtilityMenuSite side. The `DownloadCard` on the dashboard and download buttons throughout the site read `downloads/version.json` to display the current version dynamically.

---

## 15. Future-Proofing & Extensibility

### 15.1 Adding a New Module

1. Add row to `Modules` table via EF Core seed or migration:
   ```csharp
   new Module { ModuleName = "NewFeature", DisplayName = "New Feature", Tier = "pro" }
   ```
2. Add the ribbon button to `RibbonUI.xml` in the add-in with matching `control.Tag`
3. Implement the `IModule` in the add-in under `Modules/Pro/`
4. The webhook handler already grants all pro modules via `GetPremiumModulesAsync()` — no change needed
5. Existing Pro subscribers receive the new module automatically on next licence validation
6. Update the Pricing page comparison table and documentation

### 15.2 Adding a New Subscription Tier

1. Create a new Stripe product and price in the Stripe Dashboard
2. Add `StripePriceId` to `appsettings.json` under `Stripe:Prices`
3. Add new `PlanType` constant (e.g., `"enterprise"`)
4. Add pricing card to the `/pricing` page
5. Update `LicenceService.ProvisionLicenceAsync()` to handle the new tier's module grants
6. Add new `Tier` value to `Modules.Tier` CHECK constraint if needed

### 15.3 Adding Team / Enterprise Licensing

The schema supports this now via `LicenceType = "team"` and `MaxActivations > 2`:

1. Add `OrganisationId` column to `Licences` and create `Organisations` table
2. Add team invitation flow: owner invites members -> members activate under the shared licence
3. Add `TeamMembers` table: `(OrganisationId, UserId, Role, JoinedAt)`
4. Update `ActivateMachineAsync()` to check team seat pool instead of individual `MaxActivations`
5. Add organisation admin portal

### 15.4 Adding New Blog Categories

1. Insert row into `BlogCategories` via Admin dashboard or seed migration
2. The `CategoryFilter` component renders dynamically from `IBlogService.GetCategoriesAsync()`
3. No code changes required

### 15.5 Adding New Pages

1. Create a new `.razor` file under `Components/Pages/`
2. Annotate with `@page "/your-route"`
3. Choose `@rendermode InteractiveServer` or leave blank for static SSR
4. Add to `NavBar` or `Footer` if needed

### 15.6 Adding OAuth Providers

ASP.NET Core Identity is already in place. Adding Google:

```csharp
builder.Services.AddAuthentication()
    .AddGoogle(opts =>
    {
        opts.ClientId     = builder.Configuration["Auth:Google:ClientId"];
        opts.ClientSecret = builder.Configuration["Auth:Google:ClientSecret"];
    });
```

The `Users.ExternalId` column stores the OAuth `sub` claim.

### 15.7 Adding a Support Portal

The `ContactSubmissions` table is the foundation. Future enhancements:

1. Add `TicketNumber` (auto-increment), `Status` (open/in-progress/closed), `Priority`
2. Add email threading via `ParentSubmissionId`
3. Add admin assignment via `AssignedToUserId`
4. Expose ticket status at `/support/tickets/{ticketNumber}` for authenticated users

### 15.8 Adding Custom Module Requests

1. Add `CustomModuleRequests` table (email, description, budget, status)
2. Add `POST /api/custom-module/request` endpoint
3. Add admin view at `/admin/custom-requests`
4. On approval: provision a `custom` tier module, grant to the user's licence

---

## 16. Naming Conventions & Coding Standards

### 16.1 C# Conventions

| Element | Convention | Example |
|---|---|---|
| Classes | PascalCase | `LicenceService`, `CheckoutController` |
| Interfaces | `I` prefix + PascalCase | `ILicenceService` |
| Methods | PascalCase | `GetActiveLicenceAsync` |
| Private fields | `_camelCase` | `_licenceService` |
| Properties | PascalCase | `IsActive`, `ExpiresAt` |
| Constants | PascalCase | `DefaultGracePeriod` |
| Async methods | `Async` suffix | `ProvisionLicenceAsync` |
| Namespaces | `UtilityMenuSite.{Layer}.{Feature}` | `UtilityMenuSite.Services.Payment` |

### 16.2 Blazor Conventions

| Element | Convention | Example |
|---|---|---|
| Page components | PascalCase | `PricingPage.razor` |
| Shared components | PascalCase | `LicenceKeyDisplay.razor` |
| CSS isolation | Matching `.razor.css` file | `PricingPage.razor.css` |
| Parameters | PascalCase | `[Parameter] public string Title { get; set; }` |
| Event callbacks | `On` prefix | `[Parameter] public EventCallback OnSubmit` |

### 16.3 API Response Conventions

| Status | Meaning |
|---|---|
| 200 | Successful read |
| 201 | Successful create (with `Location` header) |
| 400 | Validation failure |
| 401 | Authentication failure |
| 403 | Authorization failure |
| 404 | Not found |
| 500 | Unexpected server error |

All error responses: `{ "error": "message", "code": "ERROR_CODE" }`

### 16.4 Database Naming

| Element | Convention | Example |
|---|---|---|
| Tables | PascalCase plural | `Licences`, `BlogPosts` |
| Columns | PascalCase | `LicenceId`, `CreatedAt` |
| Primary keys | `{TableName}Id` | `LicenceId`, `UserId` |
| Foreign keys | `FK_{Table}_{Referenced}` | `FK_Licences_Users` |
| Indexes | `IX_{Table}_{Columns}` | `IX_Licences_LicenceKey` |
| Unique constraints | `UQ_{Table}_{Column}` | `UQ_Licences_LicenceKey` |
| Check constraints | `CK_{Table}_{Column}` | `CK_Licences_LicenceType` |

### 16.5 ASCII-Only Output Rule

All workflow YAML files and Python scripts in this repository must use ASCII-only output markers:

- `[OK]` — success
- `[FAIL]` — failure
- `[WARNING]` — warning
- `[INFO]` — informational

No Unicode, no emojis, no checkmarks in CI/CD files. Windows runners use cp1252 encoding. Exception: Markdown documentation (`.md` files) may use emojis.

---

## Appendix A — Licence Key Format

Format: `UMENU-XXXX-XXXX-XXXX`

- Prefix: `UMENU-`
- 12 random Base32 characters (no ambiguous chars: no `I`, `O`, `0`, `1`)
- Grouped in 3 blocks of 4, separated by `-`
- Total length: 18 characters
- Character set: `ABCDEFGHJKLMNPQRSTUVWXYZ23456789`
- Generated by `LicenceKeyGenerator.Generate()` on the server only

```csharp
public static class LicenceKeyGenerator
{
    private const string Chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

    public static string Generate()
    {
        var bytes = new byte[12];
        using (var rng = RandomNumberGenerator.Create())
            rng.GetBytes(bytes);

        var chars = new char[12];
        for (int i = 0; i < 12; i++)
            chars[i] = Chars[bytes[i] % Chars.Length];

        return $"UMENU-{new string(chars, 0, 4)}-{new string(chars, 4, 4)}-{new string(chars, 8, 4)}";
    }
}
```

---

## Appendix B — Machine Fingerprint

The add-in generates a machine fingerprint for device activation composed from:

- `Environment.MachineName`
- `Environment.UserName`
- Volume serial number (Win32)

SHA-256 hashed, hex-encoded. 64 characters. The server stores this opaquely and uses it only for equality checking.

---

## Appendix C — HMAC Licence Signature

The `/api/licence/entitlements` response includes a `signature` field. The add-in validates this before trusting the payload.

- **Algorithm:** HMAC-SHA256
- **Key:** `Licensing:HmacSigningKey` from server configuration (never transmitted to the client)
- **Input:** Canonical JSON of `{ expiresAt, licenceKey, licenceType, modules }` (keys sorted alphabetically)
- **Output:** Base64-encoded HMAC

```csharp
var payload = JsonSerializer.Serialize(new
{
    expiresAt  = licence.ExpiresAt?.ToString("O"),
    licenceKey = licence.LicenceKey,
    licenceType = licence.LicenceType,
    modules    = entitlements.OrderBy(m => m).ToArray()
}, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

var key  = Convert.FromBase64String(_settings.HmacSigningKey);
using var hmac = new HMACSHA256(key);
var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
var signature = Convert.ToBase64String(hash);
```

---

## Appendix D — API Token Generation

Generated at user registration. 64 cryptographically random URL-safe Base64 characters.

```csharp
public static string GenerateApiToken()
{
    var bytes = new byte[48];
    using (var rng = RandomNumberGenerator.Create())
        rng.GetBytes(bytes);
    return Convert.ToBase64String(bytes)
        .Replace('+', '-')
        .Replace('/', '_')
        .TrimEnd('=')
        .Substring(0, 64);
}
```

---

## Document Revision History

| Version | Date | Author | Changes |
|---|---|---|---|
| 1.0 | 2026-02-22 | Martin Hanna | Initial blueprint — complete specification |

---

**This document is the authoritative design specification for UtilityMenuSite. All implementation decisions must be consistent with this blueprint. Update this document when the architecture changes.**
