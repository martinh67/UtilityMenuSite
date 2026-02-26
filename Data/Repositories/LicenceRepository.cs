using Microsoft.EntityFrameworkCore;
using UtilityMenuSite.Core.Interfaces;
using UtilityMenuSite.Data.Context;
using UtilityMenuSite.Data.Models;

namespace UtilityMenuSite.Data.Repositories;

public class LicenceRepository : ILicenceRepository
{
    private readonly AppDbContext _db;

    public LicenceRepository(AppDbContext db) => _db = db;

    public async Task<Licence?> GetByKeyAsync(string licenceKey, CancellationToken ct = default)
        => await _db.Licences
            .Include(l => l.LicenceModules)
                .ThenInclude(lm => lm.Module)
            .FirstOrDefaultAsync(l => l.LicenceKey == licenceKey, ct);

    public async Task<Licence?> GetActiveLicenceForUserAsync(Guid userId, CancellationToken ct = default)
        => await _db.Licences
            .Include(l => l.LicenceModules)
                .ThenInclude(lm => lm.Module)
            .Include(l => l.Machines.Where(m => m.IsActive))
            .Where(l => l.UserId == userId && l.IsActive)
            .OrderByDescending(l => l.CreatedAt)
            .FirstOrDefaultAsync(ct);

    public async Task<string?> GetLicenceKeyForStripeCustomerAsync(string stripeCustomerId, CancellationToken ct = default)
        => await _db.StripeCustomers
            .Where(sc => sc.StripeCustomerId == stripeCustomerId)
            .SelectMany(sc => sc.User.Licences)
            .Where(l => l.IsActive)
            .OrderByDescending(l => l.CreatedAt)
            .Select(l => l.LicenceKey)
            .FirstOrDefaultAsync(ct);

    public async Task<Licence> CreateAsync(Licence licence, CancellationToken ct = default)
    {
        _db.Licences.Add(licence);
        await _db.SaveChangesAsync(ct);
        return licence;
    }

    public async Task UpdateAsync(Licence licence, CancellationToken ct = default)
    {
        licence.UpdatedAt = DateTime.UtcNow;
        _db.Licences.Update(licence);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<List<Machine>> GetActiveMachinesAsync(Guid licenceId, CancellationToken ct = default)
        => await _db.Machines
            .Where(m => m.LicenceId == licenceId && m.IsActive)
            .OrderByDescending(m => m.LastSeenAt)
            .ToListAsync(ct);

    public async Task<Machine?> GetMachineAsync(Guid licenceId, string fingerprint, CancellationToken ct = default)
        => await _db.Machines
            .FirstOrDefaultAsync(m => m.LicenceId == licenceId && m.MachineFingerprint == fingerprint, ct);

    public async Task<Machine?> GetMachineByIdAsync(Guid machineId, CancellationToken ct = default)
        => await _db.Machines.FindAsync([machineId], ct);

    public async Task<Machine> CreateMachineAsync(Machine machine, CancellationToken ct = default)
    {
        _db.Machines.Add(machine);
        await _db.SaveChangesAsync(ct);
        return machine;
    }

    public async Task UpdateMachineAsync(Machine machine, CancellationToken ct = default)
    {
        _db.Machines.Update(machine);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<bool> DeactivateMachineByIdAsync(Guid machineId, CancellationToken ct = default)
    {
        var machine = await _db.Machines.FindAsync([machineId], ct);
        if (machine is null || !machine.IsActive) return false;

        machine.IsActive = false;
        machine.DeactivatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<int> GetActiveMachineCountAsync(Guid licenceId, CancellationToken ct = default)
        => await _db.Machines.CountAsync(m => m.LicenceId == licenceId && m.IsActive, ct);

    public async Task<StripeCustomer?> GetStripeCustomerAsync(Guid userId, CancellationToken ct = default)
        => await _db.StripeCustomers.FirstOrDefaultAsync(sc => sc.UserId == userId, ct);

    public async Task<StripeCustomer> CreateStripeCustomerAsync(StripeCustomer customer, CancellationToken ct = default)
    {
        _db.StripeCustomers.Add(customer);
        await _db.SaveChangesAsync(ct);
        return customer;
    }

    public async Task<Subscription?> GetSubscriptionByStripeIdAsync(string stripeSubId, CancellationToken ct = default)
        => await _db.Subscriptions.FirstOrDefaultAsync(s => s.StripeSubscriptionId == stripeSubId, ct);

    public async Task<Subscription?> GetActiveSubscriptionForUserAsync(Guid userId, CancellationToken ct = default)
        => await _db.Subscriptions
            .Where(s => s.UserId == userId && (s.Status == "active" || s.Status == "trialing" || s.Status == "grace_period" || s.Status == "lifetime"))
            .OrderByDescending(s => s.CreatedAt)
            .FirstOrDefaultAsync(ct);

    public async Task<Subscription> CreateSubscriptionAsync(Subscription subscription, CancellationToken ct = default)
    {
        _db.Subscriptions.Add(subscription);
        await _db.SaveChangesAsync(ct);
        return subscription;
    }

    public async Task UpdateSubscriptionAsync(Subscription subscription, CancellationToken ct = default)
    {
        subscription.UpdatedAt = DateTime.UtcNow;
        _db.Subscriptions.Update(subscription);
        await _db.SaveChangesAsync(ct);
    }

    public async Task AddLicenceModulesAsync(IEnumerable<LicenceModule> modules, CancellationToken ct = default)
    {
        _db.LicenceModules.AddRange(modules);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<List<Module>> GetModulesByTiersAsync(IEnumerable<string> tiers, CancellationToken ct = default)
    {
        var tierList = tiers.ToList();
        return await _db.Modules
            .Where(m => m.IsActive && tierList.Contains(m.Tier))
            .OrderBy(m => m.SortOrder)
            .ToListAsync(ct);
    }

    public async Task<int> GetTotalActiveLicencesAsync(CancellationToken ct = default)
        => await _db.Licences.CountAsync(l => l.IsActive, ct);

    public async Task<bool> StripeWebhookEventExistsAsync(string stripeEventId, CancellationToken ct = default)
        => await _db.StripeWebhookEvents.AnyAsync(e => e.StripeEventId == stripeEventId, ct);

    public async Task<StripeWebhookEvent> CreateWebhookEventAsync(StripeWebhookEvent evt, CancellationToken ct = default)
    {
        _db.StripeWebhookEvents.Add(evt);
        await _db.SaveChangesAsync(ct);
        return evt;
    }

    public async Task UpdateWebhookEventAsync(StripeWebhookEvent evt, CancellationToken ct = default)
    {
        _db.StripeWebhookEvents.Update(evt);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<List<StripeWebhookEvent>> GetFailedWebhookEventsAsync(CancellationToken ct = default)
        => await _db.StripeWebhookEvents
            .Where(e => e.FailedAt != null && e.ProcessedAt == null)
            .OrderByDescending(e => e.ReceivedAt)
            .ToListAsync(ct);

    public async Task<StripeWebhookEvent?> GetWebhookEventAsync(Guid id, CancellationToken ct = default)
        => await _db.StripeWebhookEvents.FindAsync([id], ct);

    public async Task RecordUsageEventAsync(UsageEvent evt, CancellationToken ct = default)
    {
        _db.UsageEvents.Add(evt);
        await _db.SaveChangesAsync(ct);
    }
}
