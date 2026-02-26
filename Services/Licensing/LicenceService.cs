using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using UtilityMenuSite.Core.Constants;
using UtilityMenuSite.Core.Interfaces;
using UtilityMenuSite.Core.Models;
using UtilityMenuSite.Data.Models;
using UtilityMenuSite.Infrastructure.Configuration;

namespace UtilityMenuSite.Services.Licensing;

public class LicenceService : ILicenceService
{
    private readonly ILicenceRepository _licenceRepo;
    private readonly LicensingSettings _settings;
    private readonly ILogger<LicenceService> _logger;

    public LicenceService(
        ILicenceRepository licenceRepo,
        IOptions<LicensingSettings> settings,
        ILogger<LicenceService> logger)
    {
        _licenceRepo = licenceRepo;
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task<LicenceValidationResult> ValidateLicenceAsync(string licenceKey, CancellationToken ct = default)
    {
        var licence = await _licenceRepo.GetByKeyAsync(licenceKey, ct);

        if (licence is null)
            return new LicenceValidationResult { IsValid = false, Reason = "not_found" };

        if (!licence.IsActive)
            return new LicenceValidationResult { IsValid = false, Reason = "inactive" };

        if (licence.ExpiresAt.HasValue && licence.ExpiresAt.Value < DateTime.UtcNow)
            return new LicenceValidationResult { IsValid = false, Reason = "expired" };

        // Update last validated timestamp (fire-and-forget)
        licence.LastValidatedAt = DateTime.UtcNow;
        await _licenceRepo.UpdateAsync(licence, ct);

        await _licenceRepo.RecordUsageEventAsync(new UsageEvent
        {
            LicenceId = licence.LicenceId,
            UserId = licence.UserId,
            EventType = "licence_validated",
            OccurredAt = DateTime.UtcNow
        }, ct);

        return new LicenceValidationResult
        {
            IsValid = true,
            LicenceType = licence.LicenceType,
            ExpiresAt = licence.ExpiresAt
        };
    }

    public async Task<LicenceEntitlementsResult?> GetEntitlementsAsync(string licenceKey, CancellationToken ct = default)
    {
        var licence = await _licenceRepo.GetByKeyAsync(licenceKey, ct);
        if (licence is null || !licence.IsActive) return null;

        var now = DateTime.UtcNow;
        if (licence.ExpiresAt.HasValue && licence.ExpiresAt.Value < now) return null;

        var modules = licence.LicenceModules
            .Where(lm => lm.Module.IsActive && (lm.ExpiresAt == null || lm.ExpiresAt > now))
            .Select(lm => lm.Module.ModuleName)
            .OrderBy(m => m)
            .ToList();

        var signature = ComputeSignature(licence, modules);

        await _licenceRepo.RecordUsageEventAsync(new UsageEvent
        {
            LicenceId = licence.LicenceId,
            UserId = licence.UserId,
            EventType = "entitlements_fetched",
            OccurredAt = DateTime.UtcNow
        }, ct);

        licence.LastValidatedAt = DateTime.UtcNow;
        await _licenceRepo.UpdateAsync(licence, ct);

        return new LicenceEntitlementsResult
        {
            IsValid = true,
            LicenceKey = licence.LicenceKey,
            LicenceType = licence.LicenceType,
            ExpiresAt = licence.ExpiresAt,
            Modules = modules,
            Signature = signature
        };
    }

    public async Task<ActivateMachineResult> ActivateMachineAsync(ActivateMachineRequest request, CancellationToken ct = default)
    {
        var licence = await _licenceRepo.GetByKeyAsync(request.LicenceKey, ct);
        if (licence is null || !licence.IsActive || (licence.ExpiresAt.HasValue && licence.ExpiresAt.Value < DateTime.UtcNow))
            throw new InvalidOperationException("Licence not found, inactive, or expired.");

        // Check for existing activation of this machine
        var existingMachine = await _licenceRepo.GetMachineAsync(licence.LicenceId, request.MachineFingerprint, ct);
        if (existingMachine is not null)
        {
            // Re-activate
            if (!existingMachine.IsActive)
            {
                existingMachine.IsActive = true;
                existingMachine.DeactivatedAt = null;
            }
            existingMachine.LastSeenAt = DateTime.UtcNow;
            if (request.MachineName is not null)
                existingMachine.MachineName = request.MachineName;
            await _licenceRepo.UpdateMachineAsync(existingMachine, ct);

            var activeCount = await _licenceRepo.GetActiveMachineCountAsync(licence.LicenceId, ct);
            return new ActivateMachineResult
            {
                MachineId = existingMachine.MachineId,
                ActivatedAt = existingMachine.FirstSeenAt,
                ActiveCount = activeCount,
                MaxActivations = licence.MaxActivations
            };
        }

        // Check seat limit
        var currentActiveCount = await _licenceRepo.GetActiveMachineCountAsync(licence.LicenceId, ct);
        if (currentActiveCount >= licence.MaxActivations)
            throw new SeatLimitExceededException($"Seat limit reached. This licence allows {licence.MaxActivations} active machines.");

        var machine = new Machine
        {
            LicenceId = licence.LicenceId,
            MachineFingerprint = request.MachineFingerprint,
            MachineName = request.MachineName,
            IsActive = true,
            FirstSeenAt = DateTime.UtcNow,
            LastSeenAt = DateTime.UtcNow
        };
        await _licenceRepo.CreateMachineAsync(machine, ct);

        await _licenceRepo.RecordUsageEventAsync(new UsageEvent
        {
            LicenceId = licence.LicenceId,
            UserId = licence.UserId,
            MachineId = machine.MachineId,
            EventType = "machine_activated",
            OccurredAt = DateTime.UtcNow
        }, ct);

        return new ActivateMachineResult
        {
            MachineId = machine.MachineId,
            ActivatedAt = machine.FirstSeenAt,
            ActiveCount = currentActiveCount + 1,
            MaxActivations = licence.MaxActivations
        };
    }

    public async Task<bool> DeactivateMachineAsync(Guid machineId, CancellationToken ct = default)
    {
        var success = await _licenceRepo.DeactivateMachineByIdAsync(machineId, ct);

        if (success)
        {
            var machine = await _licenceRepo.GetMachineByIdAsync(machineId, ct);
            if (machine is not null)
            {
                await _licenceRepo.RecordUsageEventAsync(new UsageEvent
                {
                    LicenceId = machine.LicenceId,
                    MachineId = machineId,
                    EventType = EventTypeConstants.MachineDeactivation,
                    OccurredAt = DateTime.UtcNow
                }, ct);
            }

            _logger.LogInformation("Deactivated machine {MachineId}", machineId);
        }
        else
        {
            _logger.LogWarning("Machine {MachineId} not found or already inactive", machineId);
        }

        return success;
    }

    public async Task<Licence?> GetActiveLicenceAsync(Guid userId, CancellationToken ct = default)
        => await _licenceRepo.GetActiveLicenceForUserAsync(userId, ct);

    public async Task<List<Machine>> GetActiveMachinesAsync(Guid licenceId, CancellationToken ct = default)
        => await _licenceRepo.GetActiveMachinesAsync(licenceId, ct);

    public async Task<Subscription?> GetSubscriptionAsync(Guid userId, CancellationToken ct = default)
        => await _licenceRepo.GetActiveSubscriptionForUserAsync(userId, ct);

    public async Task<string?> GetLicenceKeyForStripeCustomerAsync(string stripeCustomerId, CancellationToken ct = default)
        => await _licenceRepo.GetLicenceKeyForStripeCustomerAsync(stripeCustomerId, ct);

    public async Task EnsureStripeCustomerAsync(Guid userId, string stripeCustomerId, string email, CancellationToken ct = default)
    {
        var existing = await _licenceRepo.GetStripeCustomerAsync(userId, ct);
        if (existing is not null) return;

        await _licenceRepo.CreateStripeCustomerAsync(new StripeCustomer
        {
            StripeCustomerId = stripeCustomerId,
            UserId = userId,
            Email = email,
            CreatedAt = DateTime.UtcNow
        }, ct);
    }

    public async Task<Subscription> SyncSubscriptionAsync(
        string stripeCustomerId, string stripeSubId, string status,
        Guid userId, string planType, CancellationToken ct = default)
    {
        var existing = await _licenceRepo.GetSubscriptionByStripeIdAsync(stripeSubId, ct);
        if (existing is not null)
        {
            existing.Status = status;
            existing.UpdatedAt = DateTime.UtcNow;
            await _licenceRepo.UpdateSubscriptionAsync(existing, ct);
            return existing;
        }

        var subscription = new Subscription
        {
            UserId = userId,
            StripeCustomerId = stripeCustomerId,
            StripeSubscriptionId = stripeSubId,
            Status = status,
            PlanType = planType,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        return await _licenceRepo.CreateSubscriptionAsync(subscription, ct);
    }

    public async Task<Licence> ProvisionLicenceAsync(
        Guid userId, Guid subscriptionId, string licenceKey,
        string licenceType, CancellationToken ct = default)
    {
        var licence = new Licence
        {
            UserId = userId,
            SubscriptionId = subscriptionId,
            LicenceKey = licenceKey,
            LicenceType = licenceType,
            MaxActivations = LicenceConstants.DefaultMaxActivations,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        await _licenceRepo.CreateAsync(licence, ct);

        // Grant modules: always core; also pro for non-custom licence types.
        // Custom licences have modules granted explicitly via admin provisioning.
        var tiersToGrant = licenceType == LicenceConstants.TypeCustom
            ? new[] { ModuleConstants.TierCore }
            : new[] { ModuleConstants.TierCore, ModuleConstants.TierPro };

        var modules = await _licenceRepo.GetModulesByTiersAsync(tiersToGrant, ct);
        var licenceModules = modules.Select(m => new LicenceModule
        {
            LicenceId = licence.LicenceId,
            ModuleId = m.ModuleId,
            GrantedAt = DateTime.UtcNow
        }).ToList();

        await _licenceRepo.AddLicenceModulesAsync(licenceModules, ct);

        _logger.LogInformation("Provisioned licence {LicenceKey} ({LicenceType}) with {ModuleCount} modules",
            licenceKey, licenceType, modules.Count);

        return licence;
    }

    private string ComputeSignature(Licence licence, List<string> modules)
    {
        if (string.IsNullOrWhiteSpace(_settings.HmacSigningKey))
            return string.Empty;

        var payload = JsonSerializer.Serialize(new
        {
            expiresAt = licence.ExpiresAt?.ToString("O"),
            licenceKey = licence.LicenceKey,
            licenceType = licence.LicenceType,
            modules = modules.ToArray()
        }, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

        var key = Convert.FromBase64String(_settings.HmacSigningKey);
        using var hmac = new HMACSHA256(key);
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
        return Convert.ToBase64String(hash);
    }
}

public class SeatLimitExceededException : Exception
{
    public SeatLimitExceededException(string message) : base(message) { }
}
