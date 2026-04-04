namespace StreetFoodNarrator.App.Core.Services;

public interface IRemoteLocalizationService
{
    Task RefreshAsync(string languageCode, CancellationToken cancellationToken = default);
}
