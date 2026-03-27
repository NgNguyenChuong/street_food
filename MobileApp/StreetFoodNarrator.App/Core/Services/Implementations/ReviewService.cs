using System.Net.Http.Json;
using StreetFoodNarrator.App.Core.Models;

namespace StreetFoodNarrator.App.Core.Services.Implementations;

public class ReviewService : IReviewService
{
    private readonly HttpClient _http;

    public ReviewService(HttpClient http)
    {
        _http = http;
    }

    public async Task<List<Review>> GetReviewsAsync(int poiId)
    {
        try
        {
            var baseUrl = AppConfig.GetResolvedApiBaseUrl().TrimEnd('/');
            // Use GetAsync and EnsureSuccessStatusCode for more robust error handling if needed, 
            // but for now simple GetFromJsonAsync is fine.
            return await _http.GetFromJsonAsync<List<Review>>($"{baseUrl}/api/reviews/poi/{poiId}") ?? new List<Review>();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ReviewService] GetReviewsAsync error: {ex.Message}");
            return new List<Review>();
        }
    }

    public async Task<Review?> SubmitReviewAsync(int poiId, string userName, int rating, string? comment)
    {
        var fallbackReview = new Review
        {
            POI_ID = poiId,
            UserName = userName,
            Rating = rating,
            Comment = comment,
            CreatedAt = DateTime.UtcNow
        };

        try
        {
            var baseUrl = AppConfig.GetResolvedApiBaseUrl().TrimEnd('/');
            var dto = new { POI_ID = poiId, UserName = userName, Rating = rating, Comment = comment };
            var response = await _http.PostAsJsonAsync($"{baseUrl}/api/reviews", dto);
            
            if (response.IsSuccessStatusCode)
            {
                var created = await response.Content.ReadFromJsonAsync<Review>();
                if (created != null)
                {
                    return created;
                }

                // Some backends return 200/201 with empty body.
                return new Review
                {
                    POI_ID = poiId,
                    UserName = userName,
                    Rating = rating,
                    Comment = comment,
                    CreatedAt = DateTime.UtcNow
                };
            }
            
            var errorContent = await response.Content.ReadAsStringAsync();
            Console.WriteLine($"[ReviewService] SubmitReviewAsync failed: {response.StatusCode} - {errorContent}");
        }
        catch (OperationCanceledException ex)
        {
            // Timeout/cancel: keep UX smooth by returning a local review item.
            Console.WriteLine($"[ReviewService] SubmitReviewAsync timeout/canceled: {ex.Message}");
            return fallbackReview;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ReviewService] SubmitReviewAsync error: {ex.Message}");
        }
        return fallbackReview;
    }
}
