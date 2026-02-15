using Microsoft.AspNetCore.Mvc;
using System.Net.Http.Headers;
using System.Text.Json;

namespace StreetFoodNarrator.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class GeocodeController : ControllerBase
{
    private static readonly HttpClient Client = CreateClient();

    [HttpGet]
    public async Task<IActionResult> Search([FromQuery] string q, [FromQuery] int limit = 3)
    {
        if (string.IsNullOrWhiteSpace(q))
        {
            return BadRequest(new { message = "Missing query" });
        }

        limit = Math.Clamp(limit, 1, 5);
        var queries = BuildQueries(q);

        try
        {
            foreach (var url in queries)
            {
                using var resp = await Client.GetAsync(url);
                if (!resp.IsSuccessStatusCode)
                {
                    continue;
                }

                var json = await resp.Content.ReadAsStringAsync();
                var data = JsonSerializer.Deserialize<JsonElement>(json);
                if (data.ValueKind == JsonValueKind.Array && data.GetArrayLength() > 0)
                {
                    return Ok(data);
                }
            }

            return Ok(Array.Empty<object>());
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = ex.Message });
        }
    }

    private static HttpClient CreateClient()
    {
        var client = new HttpClient();
        client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("StreetFoodNarrator", "1.0"));
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        return client;
    }

    private static List<string> BuildQueries(string input)
    {
        var limit = 3;
        var country = "vn";
        var baseQuery = input.Trim();

        var cleaned = baseQuery
            .Replace("TP.HCM", "Ho Chi Minh City", StringComparison.OrdinalIgnoreCase)
            .Replace("TP HCM", "Ho Chi Minh City", StringComparison.OrdinalIgnoreCase)
            .Replace("HCM", "Ho Chi Minh City", StringComparison.OrdinalIgnoreCase)
            .Replace("P.", "Phường", StringComparison.OrdinalIgnoreCase)
            .Replace("Q.", "Quận", StringComparison.OrdinalIgnoreCase)
            .Replace("Q ", "Quận ", StringComparison.OrdinalIgnoreCase)
            .Replace("Phuong", "Phường", StringComparison.OrdinalIgnoreCase)
            .Replace("Quan", "Quận", StringComparison.OrdinalIgnoreCase);

        var queries = new List<string>
        {
            $"https://nominatim.openstreetmap.org/search?q={Uri.EscapeDataString(cleaned)}&format=json&limit={limit}&addressdetails=1&countrycodes={country}",
            $"https://nominatim.openstreetmap.org/search?q={Uri.EscapeDataString(baseQuery)}&format=json&limit={limit}&addressdetails=1&countrycodes={country}"
        };

        // Structured fallback: try street + city + country
        var street = ExtractStreet(cleaned);
        if (!string.IsNullOrWhiteSpace(street))
        {
            queries.Add($"https://nominatim.openstreetmap.org/search?street={Uri.EscapeDataString(street)}&city=Ho%20Chi%20Minh%20City&country=Vietnam&format=json&limit={limit}&addressdetails=1");
        }

        return queries;
    }

    private static string ExtractStreet(string input)
    {
        var parts = input.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0) return input.Trim();
        return parts[0];
    }
}
