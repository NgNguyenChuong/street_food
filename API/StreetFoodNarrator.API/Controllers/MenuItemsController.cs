using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using StreetFoodNarrator.API.Data;
using StreetFoodNarrator.API.Models;
using System.Security.Claims;

namespace StreetFoodNarrator.API.Controllers;

[Route("api/[controller]")]
[ApiController]
public class MenuItemsController : ControllerBase
{
    private readonly MongoDbContext _db;
    private readonly MongoSequenceService _sequence;

    public MenuItemsController(MongoDbContext db, MongoSequenceService sequence)
    {
        _db = db;
        _sequence = sequence;
    }

    // GET /api/MenuItems?poiId=5&page=1&pageSize=20
    [HttpGet]
    public async Task<ActionResult> GetMenuItems(
        [FromQuery] int? poiId = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        var filter = Builders<MenuItem>.Filter.Eq(m => m.IsDeleted, false);

        if (poiId.HasValue)
            filter &= Builders<MenuItem>.Filter.Eq(m => m.POI_ID, poiId.Value);

        var total = await _db.MenuItems.CountDocumentsAsync(filter);
        var items = await _db.MenuItems
            .Find(filter)
            .SortBy(m => m.SortOrder)
            .ThenBy(m => m.MenuItemId)
            .Skip((page - 1) * pageSize)
            .Limit(pageSize)
            .ToListAsync();

        return Ok(new
        {
            data = items,
            total = (int)total,
            page,
            pageSize,
            totalPages = (int)Math.Ceiling(total / (double)pageSize)
        });
    }

    // GET /api/MenuItems/{id}
    [HttpGet("{id}")]
    public async Task<ActionResult<MenuItem>> GetMenuItem(int id)
    {
        var item = await _db.MenuItems
            .Find(m => m.MenuItemId == id && !m.IsDeleted)
            .FirstOrDefaultAsync();

        if (item == null) return NotFound(new { message = "Menu item not found" });
        return Ok(item);
    }

    // POST /api/MenuItems
    [Authorize(Roles = "Admin,Vendor")]
    [HttpPost]
    public async Task<ActionResult<MenuItem>> CreateMenuItem([FromBody] CreateMenuItemModel model)
    {
        // Verify POI exists
        var poi = await _db.POIs.Find(p => p.POI_ID == model.POI_ID && p.DeletedAt == null).FirstOrDefaultAsync();
        if (poi == null) return NotFound(new { message = "POI not found" });

        // Vendor scoping
        int vendorId = 0;
        if (User.IsInRole("Vendor"))
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var vendor = await _db.VendorProfiles.Find(v => v.UserId == userId).FirstOrDefaultAsync();
            if (vendor == null || poi.VendorId != vendor.VendorId)
                return Forbid();
            vendorId = vendor.VendorId;
        }
        else if (User.IsInRole("Admin"))
        {
            vendorId = poi.VendorId ?? 0;
        }

        var nextId = await _sequence.GetNextAsync("menu_item_id");
        var item = new MenuItem
        {
            MenuItemId  = nextId,
            POI_ID      = model.POI_ID,
            VendorId    = vendorId,
            Name_Vi     = model.Name_Vi,
            Name_En     = model.Name_En,
            Name_Zh     = model.Name_Zh,
            Description_Vi = model.Description_Vi,
            Description_En = model.Description_En,
            Description_Zh = model.Description_Zh,
            Price       = model.Price,
            PriceUnit   = model.PriceUnit ?? "VND",
            PriceNote   = model.PriceNote,
            Category    = model.Category,
            ImageUrl    = model.ImageUrl,
            Tags        = model.Tags,
            IsSignatureDish = model.IsSignatureDish,
            IsAvailable = model.IsAvailable,
            SortOrder   = model.SortOrder,
            CreatedAt   = DateTime.UtcNow,
            UpdatedAt   = DateTime.UtcNow
        };

        await _db.MenuItems.InsertOneAsync(item);
        return CreatedAtAction(nameof(GetMenuItem), new { id = item.MenuItemId }, item);
    }

    // PUT /api/MenuItems/{id}
    [Authorize(Roles = "Admin,Vendor")]
    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateMenuItem(int id, [FromBody] UpdateMenuItemModel model)
    {
        var item = await _db.MenuItems.Find(m => m.MenuItemId == id && !m.IsDeleted).FirstOrDefaultAsync();
        if (item == null) return NotFound(new { message = "Menu item not found" });

        // Vendor scoping
        if (User.IsInRole("Vendor"))
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var vendor = await _db.VendorProfiles.Find(v => v.UserId == userId).FirstOrDefaultAsync();
            if (vendor == null || item.VendorId != vendor.VendorId)
                return Forbid();
        }

        var update = Builders<MenuItem>.Update
            .Set(m => m.Name_Vi,        model.Name_Vi     ?? item.Name_Vi)
            .Set(m => m.Name_En,        model.Name_En     ?? item.Name_En)
            .Set(m => m.Name_Zh,        model.Name_Zh     ?? item.Name_Zh)
            .Set(m => m.Description_Vi, model.Description_Vi ?? item.Description_Vi)
            .Set(m => m.Description_En, model.Description_En ?? item.Description_En)
            .Set(m => m.Description_Zh, model.Description_Zh ?? item.Description_Zh)
            .Set(m => m.Price,          model.Price       ?? item.Price)
            .Set(m => m.PriceUnit,      model.PriceUnit   ?? item.PriceUnit)
            .Set(m => m.PriceNote,      model.PriceNote   ?? item.PriceNote)
            .Set(m => m.Category,       model.Category    ?? item.Category)
            .Set(m => m.ImageUrl,       model.ImageUrl    ?? item.ImageUrl)
            .Set(m => m.Tags,           model.Tags        ?? item.Tags)
            .Set(m => m.IsSignatureDish,model.IsSignatureDish ?? item.IsSignatureDish)
            .Set(m => m.IsAvailable,    model.IsAvailable ?? item.IsAvailable)
            .Set(m => m.SortOrder,      model.SortOrder   ?? item.SortOrder)
            .Set(m => m.UpdatedAt,      DateTime.UtcNow);

        await _db.MenuItems.UpdateOneAsync(m => m.MenuItemId == id, update);
        return Ok(new { message = "Updated successfully" });
    }

    // DELETE /api/MenuItems/{id}
    [Authorize(Roles = "Admin,Vendor")]
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteMenuItem(int id)
    {
        var item = await _db.MenuItems.Find(m => m.MenuItemId == id && !m.IsDeleted).FirstOrDefaultAsync();
        if (item == null) return NotFound(new { message = "Menu item not found" });

        // Vendor scoping
        if (User.IsInRole("Vendor"))
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var vendor = await _db.VendorProfiles.Find(v => v.UserId == userId).FirstOrDefaultAsync();
            if (vendor == null || item.VendorId != vendor.VendorId)
                return Forbid();
        }

        var update = Builders<MenuItem>.Update
            .Set(m => m.IsDeleted,  true)
            .Set(m => m.DeletedAt,  DateTime.UtcNow)
            .Set(m => m.UpdatedAt,  DateTime.UtcNow);

        await _db.MenuItems.UpdateOneAsync(m => m.MenuItemId == id, update);
        return Ok(new { message = "Deleted successfully" });
    }
}

// ─── Request Models ────────────────────────────────────────────────────────────
public class CreateMenuItemModel
{
    public int POI_ID { get; set; }
    public string Name_Vi { get; set; } = null!;
    public string? Name_En { get; set; }
    public string? Name_Zh { get; set; }
    public string? Description_Vi { get; set; }
    public string? Description_En { get; set; }
    public string? Description_Zh { get; set; }
    public decimal Price { get; set; }
    public string? PriceUnit { get; set; }
    public string? PriceNote { get; set; }
    public string? Category { get; set; }
    public string? ImageUrl { get; set; }
    public List<string>? Tags { get; set; }
    public bool IsSignatureDish { get; set; } = false;
    public bool IsAvailable { get; set; } = true;
    public int SortOrder { get; set; } = 0;
}

public class UpdateMenuItemModel
{
    public string? Name_Vi { get; set; }
    public string? Name_En { get; set; }
    public string? Name_Zh { get; set; }
    public string? Description_Vi { get; set; }
    public string? Description_En { get; set; }
    public string? Description_Zh { get; set; }
    public decimal? Price { get; set; }
    public string? PriceUnit { get; set; }
    public string? PriceNote { get; set; }
    public string? Category { get; set; }
    public string? ImageUrl { get; set; }
    public List<string>? Tags { get; set; }
    public bool? IsSignatureDish { get; set; }
    public bool? IsAvailable { get; set; }
    public int? SortOrder { get; set; }
}
