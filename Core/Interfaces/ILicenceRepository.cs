using UtilityMenuSite.Data.Models;

namespace UtilityMenuSite.Core.Interfaces;

public interface ILicenceRepository
{
    Task<Licence?> GetByKeyAsync(string licenceKey, CancellationToken ct = default);
    Task<Licence?> GetActiveLicenceForUserAsync(Guid userId, CancellationToken ct = default);
    Task<string?> GetLicenceKeyForStripeCustomerAsync(string stripeCustomerId, CancellationToken ct = default);
    Task<Licence> CreateAsync(Licence licence, CancellationToken ct = default);
    Task UpdateAsync(Licence licence, CancellationToken ct = default);
    Task<List<Machine>> GetActiveMachinesAsync(Guid licenceId, CancellationToken ct = default);
    Task<Machine?> GetMachineAsync(Guid licenceId, string fingerprint, CancellationToken ct = default);
    Task<Machine?> GetMachineByIdAsync(Guid machineId, CancellationToken ct = default);
    Task<Machine> CreateMachineAsync(Machine machine, CancellationToken ct = default);
    Task UpdateMachineAsync(Machine machine, CancellationToken ct = default);
    Task<bool> DeactivateMachineByIdAsync(Guid machineId, CancellationToken ct = default);
    Task<int> GetActiveMachineCountAsync(Guid licenceId, CancellationToken ct = default);
    Task<StripeCustomer?> GetStripeCustomerAsync(Guid userId, CancellationToken ct = default);
    Task<StripeCustomer> CreateStripeCustomerAsync(StripeCustomer customer, CancellationToken ct = default);
    Task<Subscription?> GetSubscriptionByStripeIdAsync(string stripeSubId, CancellationToken ct = default);
    Task<Subscription?> GetActiveSubscriptionForUserAsync(Guid userId, CancellationToken ct = default);
    Task<Subscription> CreateSubscriptionAsync(Subscription subscription, CancellationToken ct = default);
    Task UpdateSubscriptionAsync(Subscription subscription, CancellationToken ct = default);
    Task AddLicenceModulesAsync(IEnumerable<LicenceModule> modules, CancellationToken ct = default);
    /// <summary>Returns all active modules for the given tiers (e.g. ["core","pro"] for a Pro provisioning).</summary>
    Task<List<Module>> GetModulesByTiersAsync(IEnumerable<string> tiers, CancellationToken ct = default);
    Task<int> GetTotalActiveLicencesAsync(CancellationToken ct = default);
    Task<bool> StripeWebhookEventExistsAsync(string stripeEventId, CancellationToken ct = default);
    Task<StripeWebhookEvent> CreateWebhookEventAsync(StripeWebhookEvent evt, CancellationToken ct = default);
    Task UpdateWebhookEventAsync(StripeWebhookEvent evt, CancellationToken ct = default);
    Task<List<StripeWebhookEvent>> GetFailedWebhookEventsAsync(CancellationToken ct = default);
    Task<StripeWebhookEvent?> GetWebhookEventAsync(Guid id, CancellationToken ct = default);
    Task RecordUsageEventAsync(UsageEvent evt, CancellationToken ct = default);
}
