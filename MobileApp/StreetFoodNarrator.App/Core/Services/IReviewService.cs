using StreetFoodNarrator.App.Core.Models;

namespace StreetFoodNarrator.App.Core.Services;

public interface IReviewService
{
    Task<List<Review>> GetReviewsAsync(int poiId);
    Task<Review?> SubmitReviewAsync(int poiId, string userName, int rating, string? comment);
}
