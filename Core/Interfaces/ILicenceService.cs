using UtilityMenuSite.Core.Models;
using UtilityMenuSite.Data.Models;

namespace UtilityMenuSite.Core.Interfaces;

public interface ILicenceService
{
    Task<LicenceValidationResult> ValidateLicenceAsync(string licenceKey, CancellationToken ct = default);
    Task<LicenceEntitlementsResult?> GetEntitlementsAsync(string licenceKey, CancellationToken ct = default);
    Task<ActivateMachineResult> ActivateMachineAsync(ActivateMachineRequest request, CancellationToken ct = default);
    Task<bool> DeactivateMachineAsync(Guid machineId, CancellationToken ct = default);
    Task<bool> DeactivateMachineByFingerprintAsync(string licenceKey, string fingerprint, CancellationToken ct = default);
    Task<Licence?> GetActiveLicenceAsync(Guid userId, CancellationToken ct = default);
    Task<List<Machine>> GetActiveMachinesAsync(Guid licenceId, CancellationToken ct = default);
    Task<Subscription?> GetSubscriptionAsync(Guid userId, CancellationToken ct = default);
    Task<string?> GetLicenceKeyForStripeCustomerAsync(string stripeCustomerId, CancellationToken ct = default);
    Task EnsureStripeCustomerAsync(Guid userId, string stripeCustomerId, string email, CancellationToken ct = default);
    Task<Subscription> SyncSubscriptionAsync(string stripeCustomerId, string stripeSubId, string status, Guid userId, string planType, CancellationToken ct = default);
    Task<Licence> ProvisionLicenceAsync(Guid userId, Guid subscriptionId, string licenceKey, string licenceType, CancellationToken ct = default);
}
