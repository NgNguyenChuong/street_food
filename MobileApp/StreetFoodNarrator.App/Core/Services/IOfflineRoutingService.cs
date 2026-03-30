namespace StreetFoodNarrator.App.Core.Services;

public readonly record struct GeoCoordinate(double Latitude, double Longitude);

public sealed record OfflineRouteResult(
    IReadOnlyList<GeoCoordinate> Path,
    string Source,
    double? DistanceMeters = null);

public interface IOfflineRoutingService
{
    bool IsRouterDbReady { get; }
    string RouterDbStatus { get; }
    Task EnsureInitializedAsync(CancellationToken cancellationToken = default);
    Task<OfflineRouteResult?> TryBuildWalkingRouteAsync(
        GeoCoordinate start,
        GeoCoordinate end,
        CancellationToken cancellationToken = default);
}
