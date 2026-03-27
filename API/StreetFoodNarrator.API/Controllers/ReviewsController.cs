using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using StreetFoodNarrator.API.Data;
using StreetFoodNarrator.API.Models;

namespace StreetFoodNarrator.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ReviewsController : ControllerBase
{
    private readonly MongoDbContext _context;

    public ReviewsController(MongoDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Gets all reviews for a specific POI, sorted by newest first
    /// </summary>
    [HttpGet("poi/{poiId}")]
    public async Task<ActionResult<IEnumerable<Review>>> GetReviewsForPoi(int poiId)
    {
        var reviews = await _context.Reviews
            .Find(r => r.POI_ID == poiId)
            .SortByDescending(r => r.CreatedAt)
            .ToListAsync();

        return Ok(reviews);
    }

    /// <summary>
    /// Submits a new review and calculates the new POI rating average
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<Review>> SubmitReview([FromBody] ReviewDto dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        // Find the POI
        var poi = await _context.POIs.Find(p => p.POI_ID == dto.POI_ID).FirstOrDefaultAsync();
        if (poi == null)
            return NotFound($"POI with ID {dto.POI_ID} not found.");

        // Create the new review record
        var review = new Review
        {
            POI_ID = dto.POI_ID,
            UserName = dto.UserName,
            Rating = dto.Rating,
            Comment = dto.Comment,
            CreatedAt = DateTime.UtcNow
        };
        await _context.Reviews.InsertOneAsync(review);

        // Calculate the new moving average rating for the POI
        // newAvg = ((oldAvg * numReviews) + newRating) / (numReviews + 1)
        var oldAvg = poi.Rating ?? 0.0;
        var oldNum = poi.NumReviews;
        var newNum = oldNum + 1;
        var newAvg = ((oldAvg * oldNum) + dto.Rating) / newNum;

        // Update the POI with the new data
        var update = Builders<POI>.Update
            .Set(p => p.Rating, Math.Round(newAvg, 1))
            .Set(p => p.NumReviews, newNum)
            .Set(p => p.UpdatedAt, DateTime.UtcNow);

        await _context.POIs.UpdateOneAsync(p => p.POI_ID == dto.POI_ID, update);

        return CreatedAtAction(nameof(GetReviewsForPoi), new { poiId = review.POI_ID }, review);
    }
}
