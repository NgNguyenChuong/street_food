using Itinero;
using Itinero.Osm.Vehicles;

namespace StreetFoodNarrator.App.Core.Services.Implementations;

public sealed class OfflineRoutingService : IOfflineRoutingService
{
    private const string ExpectedRouterDbFileName = "vinhkhanh_q4.routerdb";
    private static int _vehicleRegistryInitialized;
    private readonly SemaphoreSlim _initGate = new(1, 1);
    private bool _isInitialized;
    private string? _routerDbPath;
    private RouterDb? _routerDb;
    private Router? _router;
    private Itinero.Profiles.Profile? _pedestrianProfile;

    public bool IsRouterDbReady => _isInitialized && _router != null && _pedestrianProfile != null;
    public string RouterDbStatus { get; private set; } = "NotInitialized";

    public async Task EnsureInitializedAsync(CancellationToken cancellationToken = default)
    {
        if (_isInitialized)
            return;

        await _initGate.WaitAsync(cancellationToken);
        try
        {
            if (_isInitialized)
                return;

            var targetDirectory = Path.Combine(FileSystem.AppDataDirectory, "routing");
            Directory.CreateDirectory(targetDirectory);
            if (!string.Equals(AppConfig.OfflineRouterDbFileName, ExpectedRouterDbFileName, StringComparison.OrdinalIgnoreCase))
            {
                RouterDbStatus = "RejectedNonQ4RouterDb";
                _isInitialized = true;
                return;
            }

            _routerDbPath = Path.Combine(targetDirectory, AppConfig.OfflineRouterDbFileName);

            if (!File.Exists(_routerDbPath))
            {
                try
                {
                    await using var input = await FileSystem.OpenAppPackageFileAsync(AppConfig.OfflineRouterDbAssetName);
                    await using var output = File.Create(_routerDbPath);
                    await input.CopyToAsync(output, cancellationToken);
                    RouterDbStatus = "ReadyFromAsset";
                }
                catch (FileNotFoundException)
                {
                    RouterDbStatus = "MissingAsset";
                }
            }
            else
            {
                RouterDbStatus = "ReadyFromCache";
            }

            if (File.Exists(_routerDbPath))
            {
                try
                {
                    EnsureVehicleRegistry();
                    await using var stream = File.OpenRead(_routerDbPath);
                    _routerDb = RouterDb.Deserialize(stream);
                    _router = new Router(_routerDb);

                    var pedestrian = Vehicle.Pedestrian.Shortest();
                    if (_routerDb.SupportProfile(pedestrian.Name))
                    {
                        _pedestrianProfile = pedestrian;
                        RouterDbStatus = $"{RouterDbStatus}|RouterLoaded";
                    }
                    else
                    {
                        var matched = _routerDb.GetSupportedProfiles()
                            .FirstOrDefault(p => p.Name.Contains("pedestrian", StringComparison.OrdinalIgnoreCase));
                        if (matched != null)
                        {
                            _pedestrianProfile = matched;
                            RouterDbStatus = $"{RouterDbStatus}|RouterLoadedFallbackProfile";
                        }
                        else
                        {
                            RouterDbStatus = $"{RouterDbStatus}|PedestrianProfileMissing";
                        }
                    }
                }
                catch (Exception ex)
                {
                    RouterDbStatus = $"RouterLoadFailed:{ex.GetType().Name}";
                    _routerDb = null;
                    _router = null;
                    _pedestrianProfile = null;
                }
            }

            _isInitialized = true;
        }
        finally
        {
            _initGate.Release();
        }
    }

    public async Task<OfflineRouteResult?> TryBuildWalkingRouteAsync(
        GeoCoordinate start,
        GeoCoordinate end,
        CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);

        if (!IsRouterDbReady || _router == null || _pedestrianProfile == null)
            return null;

        if (!IsValidCoordinate(start) || !IsValidCoordinate(end))
            return null;

        try
        {
            var routeResult = _router.TryCalculate(
                _pedestrianProfile,
                (float)start.Latitude,
                (float)start.Longitude,
                (float)end.Latitude,
                (float)end.Longitude,
                cancellationToken);

            if (routeResult.IsError || routeResult.Value?.Shape == null || routeResult.Value.Shape.Length < 2)
            {
                RouterDbStatus = $"RouteFailed:{routeResult.ErrorMessage ?? "Unknown"}";
                return null;
            }

            var route = routeResult.Value;
            var path = route.Shape
                .Select(point => new GeoCoordinate(point.Latitude, point.Longitude))
                .ToList();

            return new OfflineRouteResult(
                Path: path,
                Source: "ItineroOffline",
                DistanceMeters: route.TotalDistance);
        }
        catch (OperationCanceledException)
        {
            return null;
        }
        catch (Exception ex)
        {
            RouterDbStatus = $"RouteException:{ex.GetType().Name}";
            return null;
        }
    }

    private static bool IsValidCoordinate(GeoCoordinate coordinate)
        => coordinate.Latitude is >= -90 and <= 90 &&
           coordinate.Longitude is >= -180 and <= 180;

    private static void EnsureVehicleRegistry()
    {
        if (Interlocked.Exchange(ref _vehicleRegistryInitialized, 1) == 0)
            Vehicle.RegisterVehicles();
    }
}
