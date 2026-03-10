# BLAZOR_DESIGN.md — Blazor Project Blueprint

A reusable architectural and stylistic blueprint for scaffolding new Blazor websites. This document defines the patterns, coding style, folder structure, and development philosophy to apply to any new Blazor project.

This file is intentionally **project-agnostic**. It applies equally to websites for research labs, gyms, SaaS products, membership portals, dashboards, or any other client or personal project.

---

## 1. Core Philosophy

All projects should follow these principles:

- **Clarity over cleverness** — code should be readable by a future developer (or future-me) without mental gymnastics.
- **Modular and maintainable** — features should be isolated, testable, and replaceable.
- **Predictable structure** — consistent naming, folder layout, and DI patterns across all projects.
- **Security-first mindset** — no secrets in source control, clean config layering, least-privilege access.
- **Fast scaffolding, clean refinement** — generate a complete structure first, then iterate.
- **Minimal friction** — avoid unnecessary abstractions or ceremony; keep things simple and idiomatic.

---

## 2. Standard Project Structure

Every new Blazor project should start with the same solution layout:

```
MySolution/
├── MySolution.sln
│
├── MySolution.Client/                  # Blazor UI (SSR or WASM)
│   ├── Components/
│   │   ├── Layout/                     # MainLayout, AuthLayout, Navbars, Sidebars
│   │   ├── Pages/                      # Routable Blazor pages
│   │   └── Shared/                     # Reusable UI components
│   ├── wwwroot/
│   │   ├── css/site.css
│   │   └── js/site.js
│   └── Program.cs
│
├── MySolution.Server/                  # ASP.NET Core host
│   ├── Controllers/                    # API controllers or minimal API endpoints
│   ├── Middleware/                     # Custom middleware
│   ├── BackgroundServices/             # IHostedService implementations
│   ├── appsettings.json
│   ├── appsettings.Development.json
│   └── Program.cs
│
├── MySolution.Shared/                  # DTOs, models, enums shared across layers
│   ├── DTOs/
│   ├── Requests/
│   ├── Responses/
│   ├── Enums/
│   └── Constants/
│
├── MySolution.Domain/                  # Core business rules; no external dependencies
│   ├── Entities/
│   ├── ValueObjects/
│   ├── Interfaces/
│   └── Services/
│
├── MySolution.Infrastructure/          # EF Core, external APIs, repositories
│   ├── Data/
│   │   ├── Context/                    # DbContext
│   │   ├── Configuration/              # IEntityTypeConfiguration classes
│   │   ├── Migrations/
│   │   └── Repositories/
│   ├── ExternalServices/               # Email, payments, SMS, analytics clients
│   └── Configuration/                 # Strongly-typed settings classes
│
└── MySolution.Tests/                   # xUnit test project
    ├── Domain/
    ├── Services/
    └── Api/
```

This structure should be used even for small projects. It keeps everything consistent and makes it easy to scale later.

---

## 3. Layer Responsibilities

### Client
- UI components, pages, layout, routing.
- Minimal logic; heavy logic lives in Domain or Server.
- Use `.razor` + `.razor.cs` partial classes for clarity.

### Server
- API endpoints (minimal APIs or controllers).
- Authentication and authorization.
- DI configuration.
- Background services, scheduled tasks.
- Webhooks or external callbacks.

### Shared
- DTOs, request/response models.
- Validation attributes or FluentValidation rules.
- Enums, constants, shared utilities.

### Domain
- Core business rules.
- Domain entities and value objects.
- Domain services and interfaces.
- **No external dependencies.**

### Infrastructure
- EF Core DbContext and migrations.
- Repository implementations.
- External API clients (email, payments, SMS, etc.).
- Configuration providers.

---

## 4. Coding Style & Conventions

### Naming

| Item | Convention | Example |
|------|-----------|---------|
| Interfaces | `I` prefix | `IUserService`, `IOrderRepository` |
| Services | Noun + `Service` | `PaymentService`, `UserService` |
| Repositories | Noun + `Repository` | `UserRepository` |
| Components | Descriptive noun | `PricingCard.razor`, `UserProfile.razor` |
| Partial classes | Matching `.razor.cs` | `PricingCard.razor.cs` |
| Folders | PascalCase | `Components/Shared/` |
| Config keys | `Section:Subsection:Key` | `Stripe:SecretKey` |
| Async methods | `Async` suffix, `CancellationToken ct` | `GetUserAsync(int id, CancellationToken ct)` |
| Constants | PascalCase static class | `ModuleConstants.ProPlan` |

### Patterns

- **Dependency Injection everywhere** — no static service locators.
- **Async all the way down** — avoid blocking calls.
- **DTOs as records** — immutable where possible.
- **EF Core**:
  - No lazy loading.
  - Explicit navigation properties.
  - Migrations live in `/Infrastructure/Data/Migrations`.
- **Repository pattern** — DbContext never leaks into Domain or Client.
- **Result objects** — prefer `Result<T>` over exceptions for expected failures.

### UI Style

- Clean, professional, minimal.
- Bootstrap 5 or Tailwind depending on project prompt.
- Components should be small and composable.
- Avoid deeply nested component hierarchies.
- Interactive pages use `@rendermode InteractiveServer` (or WASM where appropriate).
- Marketing/public pages use SSR for SEO.

### Blazor Page Conventions

```razor
@page "/feature"
@layout DashboardLayout
@rendermode InteractiveServer

@inject IFeatureService FeatureService

<PageTitle>Feature — AppName</PageTitle>

<!-- Markup -->

@code {
    // Minimal code-behind; push logic to services
}
```

---

## 5. Configuration & Environment Setup

### AppSettings Structure

```json
{
  "ConnectionStrings": {
    "Default": ""
  },
  "ExternalServices": {
    "Email": { "ApiKey": "", "Provider": "" },
    "Payments": { "ApiKey": "", "WebhookSecret": "" },
    "Analytics": { "Key": "" }
  },
  "FeatureFlags": {
    "EnableX": false,
    "EnableY": true
  }
}
```

### Secret Management

- **Never commit secrets.**
- Use environment variables or `dotnet user-secrets` for local development.
- For local dev: `appsettings.Development.json` for non-secret overrides only.
- CI/CD secrets live in GitHub Actions secrets or Azure Key Vault — never in code.

### Strongly-Typed Settings

```csharp
// Infrastructure/Configuration/StripeSettings.cs
public class StripeSettings
{
    public string SecretKey { get; init; } = "";
    public string WebhookSecret { get; init; } = "";
}

// Program.cs registration
builder.Services.Configure<StripeSettings>(builder.Configuration.GetSection("Stripe"));
```

---

## 6. API Design Guidelines

### Endpoint Grouping

Group endpoints by feature domain:

```
/api/auth        — login, register, refresh
/api/content     — posts, pages, documents
/api/members     — profiles, subscriptions
/api/admin       — management, reporting
/api/webhooks    — external callbacks (Stripe, etc.)
```

### Controller Pattern

```csharp
[ApiController]
[Route("api/[controller]")]
public class MembersController : ControllerBase
{
    private readonly IMemberService _memberService;

    public MembersController(IMemberService memberService)
        => _memberService = memberService;

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var result = await _memberService.GetAsync(id, ct);
        return result.IsSuccess ? Ok(result.Value) : NotFound();
    }
}
```

### API Response Wrapper

```csharp
// Shared/Responses/ApiResponse.cs
public record ApiResponse<T>(bool Success, T? Data, string? Error = null);
```

### Rules

- Use typed responses (`ApiResponse<T>`).
- Validate all inputs (data annotations or FluentValidation).
- Keep endpoints thin; push logic into Domain services.
- Idempotent webhooks — check a processed-events table before acting.

---

## 7. Scaffolding Workflow for Claude

When starting a new project:

1. **Create the full folder structure** exactly as defined in Section 2.
2. **Generate placeholder files** for:
   - Pages
   - Components
   - Services
   - Interfaces
   - DbContext
   - Configuration classes
   - DI registration (`Program.cs`)
3. **Stub out major flows** based on the project-specific prompt (e.g., membership, content, admin, bookings, payments).
4. **Ensure the project builds immediately** — no broken references or missing namespaces.
5. **Document assumptions** using inline comments.
6. **Follow the architectural and naming conventions** in this blueprint.

### DI Registration Pattern

```csharp
// Organised by layer in Program.cs or extension methods
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("Default")));
```

---

## 8. Documentation Expectations

Every project should include:

| File | Purpose |
|------|---------|
| `README.md` | Setup, run instructions, environment variables |
| `ARCHITECTURE.md` | Summary of structure and design decisions |
| `DEVELOPMENT.md` | Migrations, build commands, environment setup |
| `CLAUDE.md` | Instructions specific to Claude Code for this repo |

Code should be self-documenting; add comments only where the intent is not immediately obvious from the code itself.

### Common Commands (include in DEVELOPMENT.md)

```bash
# Secrets
dotnet user-secrets set "ConnectionStrings:Default" "Server=...;Database=...;"

# Migrations
dotnet ef migrations add <MigrationName> --project MySolution.Infrastructure
dotnet ef database update

# Run
dotnet run --project MySolution.Server

# Test
dotnet test
dotnet test /p:CollectCoverage=true /p:CoverletOutputFormat=opencover
```

---

## 9. Output Expectations for Claude

When using this blueprint, Claude should:

- **Produce complete, ready-to-paste folder structures** — every layer represented.
- **Generate initial code files** with correct namespaces and DI patterns.
- **Follow the exact conventions** defined in this document.
- **Keep code clean, minimal, and idiomatic** — no over-engineering.
- **Ensure the project is buildable from the first commit** — no placeholder `TODO: implement` that would cause compile errors.
- **Use this blueprint as the architectural baseline**; the companion project-specific prompt provides the functional requirements.
- **Defer to project-specific overrides** — if the project prompt explicitly contradicts this blueprint (e.g., "use Tailwind instead of Bootstrap"), honour the project prompt.

---

*Last updated: 2026-02-23*
