using MongoDB.Driver;
using StreetFoodNarrator.API.Data;
using StreetFoodNarrator.API.Models;

namespace StreetFoodNarrator.API.Services;

public class PremiumExpiryMonitorService : BackgroundService
{
    private static readonly TimeSpan CheckInterval = TimeSpan.FromSeconds(30);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<PremiumExpiryMonitorService> _logger;

    public PremiumExpiryMonitorService(IServiceScopeFactory scopeFactory, ILogger<PremiumExpiryMonitorService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Premium expiry monitor started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessOnceAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Premium expiry monitor failed during processing cycle.");
            }

            try
            {
                await Task.Delay(CheckInterval, stoppingToken);
            }
            catch (TaskCanceledException)
            {
                // shutdown
            }
        }

        _logger.LogInformation("Premium expiry monitor stopped.");
    }

    private async Task ProcessOnceAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MongoDbContext>();

        var now = DateTime.UtcNow;

        var activeFromProfile = await db.VendorProfiles
            .Find(v => v.ServicePlan == "premium" && v.PremiumExpiresAt.HasValue && v.PremiumExpiresAt > now)
            .Project(v => v.VendorId)
            .ToListAsync(cancellationToken);

        var activeFromSubmission = await db.ServiceSubmissions
            .Find(s => s.Status == SubmissionStatuses.Approved && s.ExpiresAt.HasValue && s.ExpiresAt > now)
            .Project(s => s.VendorId)
            .ToListAsync(cancellationToken);

        var activeVendorIds = activeFromProfile.Concat(activeFromSubmission).ToHashSet();

        var expiredFromProfile = await db.VendorProfiles
            .Find(v => v.ServicePlan == "premium" && v.PremiumExpiresAt.HasValue && v.PremiumExpiresAt <= now)
            .Project(v => v.VendorId)
            .ToListAsync(cancellationToken);

        var expiredFromSubmission = await db.ServiceSubmissions
            .Find(s => s.Status == SubmissionStatuses.Approved && s.ExpiresAt.HasValue && s.ExpiresAt <= now)
            .Project(s => s.VendorId)
            .ToListAsync(cancellationToken);

        var candidateVendorIds = expiredFromProfile.Concat(expiredFromSubmission).ToHashSet();
        var targetVendorIds = candidateVendorIds.Where(v => !activeVendorIds.Contains(v)).ToList();

        if (targetVendorIds.Count == 0)
            return;

        var vendorFilter = Builders<VendorProfile>.Filter.In(v => v.VendorId, targetVendorIds);
        var vendorUpdate = Builders<VendorProfile>.Update
            .Set(v => v.ServicePlan, null)
            .Set(v => v.PremiumExpiresAt, null)
            .Set(v => v.UpdatedAt, now);
        var vendorResult = await db.VendorProfiles.UpdateManyAsync(vendorFilter, vendorUpdate, cancellationToken: cancellationToken);

        var targetVendorIdsNullable = targetVendorIds.Select(v => (int?)v).ToList();
        var poiFilter = Builders<POI>.Filter.In(p => p.VendorId, targetVendorIdsNullable)
                      & Builders<POI>.Filter.Eq(p => p.IsActive, true)
                      & Builders<POI>.Filter.Eq(p => p.DeletedAt, null);
        var poiUpdate = Builders<POI>.Update
            .Set(p => p.IsActive, false)
            .Set(p => p.UpdatedAt, now);
        var poiResult = await db.POIs.UpdateManyAsync(poiFilter, poiUpdate, cancellationToken: cancellationToken);

        _logger.LogInformation(
            "Premium expired for {VendorCount} vendors. Updated vendor profiles: {VendorUpdated}. Deactivated POIs: {PoiUpdated}.",
            targetVendorIds.Count,
            vendorResult.ModifiedCount,
            poiResult.ModifiedCount);
    }
}
