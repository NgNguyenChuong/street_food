using MongoDB.Driver;
using StreetFoodNarrator.API.Data;
using StreetFoodNarrator.API.Models;

namespace StreetFoodNarrator.API.Services;

/// <summary>
/// Periodically marks devices as offline when their heartbeat exceeds the threshold.
/// </summary>
public class DeviceActivityMonitorService : BackgroundService
{
    private static readonly TimeSpan CheckInterval = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan OnlineThreshold = TimeSpan.FromMinutes(5);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<DeviceActivityMonitorService> _logger;

    public DeviceActivityMonitorService(IServiceScopeFactory scopeFactory, ILogger<DeviceActivityMonitorService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Device activity monitor started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await MarkStaleDevicesOfflineAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Device activity monitor failed during processing cycle.");
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

        _logger.LogInformation("Device activity monitor stopped.");
    }

    private async Task MarkStaleDevicesOfflineAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MongoDbContext>();

        var cutoff = DateTime.UtcNow.Subtract(OnlineThreshold);

        // Find devices that are marked online but have a stale heartbeat
        var filter = Builders<DeviceInfo>.Filter.Eq(d => d.IsOnline, true)
            & (Builders<DeviceInfo>.Filter.Lt(d => d.LastHeartbeatAt, cutoff)
               | Builders<DeviceInfo>.Filter.Eq(d => d.LastHeartbeatAt, null));

        var update = Builders<DeviceInfo>.Update.Set(d => d.IsOnline, false);

        var result = await db.Devices.UpdateManyAsync(filter, update, cancellationToken: cancellationToken);

        if (result.ModifiedCount > 0)
        {
            _logger.LogInformation("Marked {Count} stale device(s) as offline.", result.ModifiedCount);
        }
    }
}
