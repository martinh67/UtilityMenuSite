// Global usings for UtilityMenuSite (Blazor BFF, net10.0, Microsoft.NET.Sdk.Web).
//
// Most ambient namespaces in a Blazor app belong in _Imports.razor (Razor scope),
// NOT in GlobalUsings.cs (.cs scope). This file only covers C# code-behind / services.
//
// Already covered by ImplicitUsings (Web SDK):
//   System, System.Collections.Generic, System.IO, System.Linq, System.Net.Http,
//   System.Net.Http.Json, System.Threading, System.Threading.Tasks,
//   Microsoft.AspNetCore.Builder, Microsoft.AspNetCore.Hosting,
//   Microsoft.AspNetCore.Http, Microsoft.AspNetCore.Routing,
//   Microsoft.Extensions.Configuration, Microsoft.Extensions.DependencyInjection,
//   Microsoft.Extensions.Hosting, Microsoft.Extensions.Logging.
//
// Site is intentionally lean (~8 .cs files); keep this list minimal.

// --- Shared API contract namespace (DTOs called from services and components) ---
global using UtilityMenuSite.Core.Models.Api;
