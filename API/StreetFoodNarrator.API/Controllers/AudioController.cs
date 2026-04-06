using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Bson;
using MongoDB.Driver;
using StreetFoodNarrator.API.Data;
using StreetFoodNarrator.API.Models;
using StreetFoodNarrator.API.Services;
using System.Security.Claims;

namespace StreetFoodNarrator.API.Controllers;

[Route("api/[controller]")]
[ApiController]
public class AudioController : ControllerBase
{
    private readonly MongoDbContext _db;
    private readonly MongoSequenceService _sequence;
    private readonly IWebHostEnvironment _env;
    private static readonly HashSet<string> AllowedAudioExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mp3", ".wav", ".m4a"
    };
    private const long MaxAudioBytes = 25L * 1024 * 1024; // 25 MB

    public AudioController(MongoDbContext db, MongoSequenceService sequence, IWebHostEnvironment env)
    {
        _db = db;
        _sequence = sequence;
        _env = env;
    }

    /// <summary>
    /// Get all audio content with pagination
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<AudioListResponse>> GetAudioList(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? language = null,
        [FromQuery] int? poiId = null,
        [FromQuery] string? status = null,
        [FromQuery] string? poiName = null)
    {
        AudioListResponse EmptyResponse()
        {
            return new AudioListResponse
            {
                Data = new List<AudioDto>(),
                Total = 0,
                Page = page,
                PageSize = pageSize,
                TotalPages = 1
            };
        }

        var filter = Builders<AudioContent>.Filter.Empty;

        // Anonymous users should only see published + active + not deleted
        if (User.Identity?.IsAuthenticated != true)
        {
            filter &= Builders<AudioContent>.Filter.Eq(a => a.Status, AudioStatuses.Published)
                   & Builders<AudioContent>.Filter.Eq(a => a.IsActive, true)
                   & Builders<AudioContent>.Filter.Eq(a => a.IsDeleted, false);
        }
        else if (!User.IsInRole("Admin") && !User.IsInRole("Vendor"))
        {
            return Forbid();
        }

        if (!string.IsNullOrEmpty(language))
        {
            var normalizedLanguage = NormalizeLanguage(language);
            var langRegex = new BsonRegularExpression($"^{normalizedLanguage}", "i");
            filter &= Builders<AudioContent>.Filter.Regex(a => a.Language, langRegex);
        }

        if (poiId.HasValue)
        {
            filter &= Builders<AudioContent>.Filter.Eq(a => a.POI_ID, poiId.Value);
        }

        if (!string.IsNullOrWhiteSpace(poiName))
        {
            var regex = new BsonRegularExpression(poiName.Trim(), "i");
            var poiFilter = Builders<POI>.Filter.Eq(p => p.DeletedAt, null) &
                            Builders<POI>.Filter.Or(
                                Builders<POI>.Filter.Regex(p => p.Name_Vi, regex),
                                Builders<POI>.Filter.Regex(p => p.Name_En, regex),
                                Builders<POI>.Filter.Regex(p => p.Name_Zh, regex)
                            );

            if (IsVendor())
            {
                var vendor = await GetVendorProfileAsync();
                if (vendor == null)
                {
                    return Ok(EmptyResponse());
                }

                poiFilter &= Builders<POI>.Filter.Eq(p => p.VendorId, vendor.VendorId);
            }

            var matchedPoiIds = await _db.POIs
                .Find(poiFilter)
                .Project(p => p.POI_ID)
                .ToListAsync();

            if (matchedPoiIds.Count == 0)
            {
                return Ok(EmptyResponse());
            }

            filter &= Builders<AudioContent>.Filter.In(a => a.POI_ID, matchedPoiIds);
        }

        // Only allow status filtering for authenticated users (Admin/Vendor).
        // Anonymous is already hard-limited to Published above.
        if (!string.IsNullOrWhiteSpace(status) && User.Identity?.IsAuthenticated == true)
        {
            var normalizedStatus = status.Trim().ToLowerInvariant();
            if (normalizedStatus == AudioStatuses.Published)
            {
                filter &= Builders<AudioContent>.Filter.Or(
                    Builders<AudioContent>.Filter.Eq(a => a.Status, AudioStatuses.Published),
                    Builders<AudioContent>.Filter.Eq(a => a.Status, null),
                    Builders<AudioContent>.Filter.Eq(a => a.Status, string.Empty)
                );
            }
            else
            {
                filter &= Builders<AudioContent>.Filter.Eq(a => a.Status, normalizedStatus);
            }
        }

        if (IsVendor())
        {
            var vendor = await GetVendorProfileAsync();
            if (vendor == null)
            {
                return Ok(EmptyResponse());
            }

            var vendorPoiIds = await _db.POIs
                .Find(p => p.VendorId == vendor.VendorId && p.DeletedAt == null)
                .Project(p => p.POI_ID)
                .ToListAsync();

            filter &= Builders<AudioContent>.Filter.In(a => a.POI_ID, vendorPoiIds);
        }

        var total = await _db.AudioContents.CountDocumentsAsync(filter);
        var audios = await _db.AudioContents
            .Find(filter)
            .SortByDescending(a => a.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Limit(pageSize)
            .ToListAsync();

        var poiIds = audios.Select(a => a.POI_ID).Distinct().ToList();
        var poiNameMap = new Dictionary<int, string>();
        var poiVendorMap = new Dictionary<int, int?>();
        if (poiIds.Count > 0)
        {
            var pois = await _db.POIs
                .Find(p => poiIds.Contains(p.POI_ID) && p.DeletedAt == null)
                .Project(p => new { p.POI_ID, p.Name_Vi, p.VendorId })
                .ToListAsync();
            poiNameMap = pois.ToDictionary(p => p.POI_ID, p => p.Name_Vi);
            poiVendorMap = pois.ToDictionary(p => p.POI_ID, p => p.VendorId);
        }

        var audioDtos = audios.Select(a => new AudioDto
        {
            AudioContent_ID = a.AudioContent_ID,
            Title = a.Title,
            Language = a.Language,
            AudioUrl = a.AudioUrl,
            Duration = a.Duration,
            FileSize = a.FileSize,
            IsActive = a.IsActive,
            POI_ID = a.POI_ID,
            POIName = poiNameMap.TryGetValue(a.POI_ID, out var name) ? name : string.Empty,
            VendorId = a.VendorId ?? (poiVendorMap.TryGetValue(a.POI_ID, out var vId) ? vId : null),
            Status = NormalizeStatus(a.Status),
            RejectedReason = a.RejectedReason,
            CreatedByRole = a.CreatedByRole,
            CreatedAt = a.CreatedAt
        }).ToList();

        return Ok(new AudioListResponse
        {
            Data = audioDtos,
            Total = (int)total,
            Page = page,
            PageSize = pageSize,
            TotalPages = (int)Math.Ceiling(total / (double)pageSize)
        });
    }

    /// <summary>
    /// Get audio by ID
    /// </summary>
    [HttpGet("{id}")]
    public async Task<ActionResult<AudioContent>> GetAudio(int id)
    {
        var audio = await _db.AudioContents.Find(a => a.AudioContent_ID == id).FirstOrDefaultAsync();

        if (audio == null)
        {
            return NotFound(new { message = "Audio not found" });
        }

        // Anonymous users can only access published, active, non-deleted audio
        if (User.Identity?.IsAuthenticated != true)
        {
            var normalized = NormalizeStatus(audio.Status);
            if (normalized != AudioStatuses.Published || audio.IsActive != true || audio.IsDeleted == true)
            {
                return NotFound(new { message = "Audio not found" });
            }
        }
        else if (IsVendor())
        {
            var vendor = await GetVendorProfileAsync();
            if (vendor == null || !await VendorOwnsPoiAsync(vendor.VendorId, audio.POI_ID))
            {
                return Forbid();
            }
        }
        else if (!User.IsInRole("Admin"))
        {
            return Forbid();
        }

        var poi = await _db.POIs.Find(p => p.POI_ID == audio.POI_ID && p.DeletedAt == null).FirstOrDefaultAsync();
        if (poi != null)
        {
            audio.POI = poi;
        }

        return Ok(audio);
    }

    /// <summary>
    /// Get audio for specific POI and language (for mobile app)
    /// Returns the published audio file that matches both POI ID and language
    /// </summary>
    [HttpGet("poi/{poiId}/{language}")]
    public async Task<ActionResult<AudioContent>> GetAudioForPoi(int poiId, string language)
    {
        var normalizedLang = NormalizeLanguage(language);

        // Find published audio matching POI + Language
        var filter = Builders<AudioContent>.Filter.And(
            Builders<AudioContent>.Filter.Eq(a => a.POI_ID, poiId),
            Builders<AudioContent>.Filter.Regex(a => a.Language, new BsonRegularExpression($"^{normalizedLang}", "i")),
            Builders<AudioContent>.Filter.Eq(a => a.Status, AudioStatuses.Published),
            Builders<AudioContent>.Filter.Eq(a => a.IsActive, true),
            Builders<AudioContent>.Filter.Eq(a => a.IsDeleted, false)
        );

        // Get all candidate audios, then sort in memory for priority:
        // 1. Admin > Vendor
        // 2. Highest Version
        // 3. Newest CreatedAt
        var audios = await _db.AudioContents.Find(filter).ToListAsync();

        var audio = audios
            .OrderByDescending(a => string.Equals(a.CreatedByRole, "Admin", StringComparison.OrdinalIgnoreCase))
            .ThenByDescending(a => a.Version)
            .ThenByDescending(a => a.CreatedAt)
            .FirstOrDefault();

        if (audio == null)
        {
            return NotFound(new 
            { 
                message = "No published audio found for this POI and language",
                poiId,
                language = normalizedLang
            });
        }

        // Attach POI info
        var poi = await _db.POIs.Find(p => p.POI_ID == poiId && p.DeletedAt == null).FirstOrDefaultAsync();
        if (poi != null)
        {
            audio.POI = poi;
        }

        return Ok(audio);
    }

    /// <summary>
    /// Debug: check audio file path and existence for a POI + language
    /// </summary>
    [HttpGet("poi/{poiId}/{language}/debug")]
    public async Task<ActionResult> GetAudioForPoiDebug(int poiId, string language)
    {
        var normalizedLang = NormalizeLanguage(language);
        var filter = Builders<AudioContent>.Filter.And(
            Builders<AudioContent>.Filter.Eq(a => a.POI_ID, poiId),
            Builders<AudioContent>.Filter.Eq(a => a.Language, normalizedLang),
            Builders<AudioContent>.Filter.Eq(a => a.Status, AudioStatuses.Published),
            Builders<AudioContent>.Filter.Eq(a => a.IsActive, true),
            Builders<AudioContent>.Filter.Eq(a => a.IsDeleted, false)
        );

        var audios = await _db.AudioContents.Find(filter).ToListAsync();

        var audio = audios
            .OrderByDescending(a => string.Equals(a.CreatedByRole, "Admin", StringComparison.OrdinalIgnoreCase))
            .ThenByDescending(a => a.Version)
            .ThenByDescending(a => a.CreatedAt)
            .FirstOrDefault();

        if (audio == null)
        {
            return NotFound(new
            {
                message = "No published audio found for this POI and language",
                poiId,
                language = normalizedLang
            });
        }

        var audioUrl = audio.AudioUrl ?? string.Empty;
        var isRemote = audioUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase);
        var physicalPath = string.Empty;
        var hasPath = !isRemote && TryGetAudioPhysicalPath(audioUrl, out physicalPath);
        var exists = hasPath && System.IO.File.Exists(physicalPath);
        long? fileSize = null;
        if (exists)
        {
            try { fileSize = new FileInfo(physicalPath).Length; } catch { }
        }

        return Ok(new
        {
            poiId,
            language = normalizedLang,
            audioUrl,
            isRemote,
            physicalPath = hasPath ? physicalPath : null,
            exists,
            fileSize
        });
    }

    /// <summary>
    /// Create audio content record (for TTS-generated files)
    /// </summary>
    [Authorize(Roles = "Admin,Vendor")]
    [HttpPost]
    public async Task<ActionResult<AudioContent>> CreateAudioContent([FromBody] CreateAudioModel model)
    {
        try
        {
            // Check POI exists
            var poi = await _db.POIs.Find(p => p.POI_ID == model.PoiId && p.DeletedAt == null).FirstOrDefaultAsync();
            if (poi == null)
            {
                return NotFound(new { message = "POI not found", poiId = model.PoiId });
            }

            if (IsVendor())
            {
                var vendor = await GetVendorProfileAsync();
                if (vendor == null || poi.VendorId != vendor.VendorId)
                {
                    return Forbid();
                }
            }

            var nextId = await _sequence.GetNextAsync("audio_content_id");
            var role = GetPrimaryRole();
            var normalizedLang = NormalizeLanguage(model.Language);
            var status = role == "Admin" ? AudioStatuses.Approved : AudioStatuses.Draft;

            // Deduplication guards
            if (role == "Admin")
            {
                // Deactivate any existing Admin-created audio for same POI + language to avoid duplicates
                var oldAdminFilter = Builders<AudioContent>.Filter.And(
                    Builders<AudioContent>.Filter.Eq(a => a.POI_ID, model.PoiId),
                    Builders<AudioContent>.Filter.Regex(a => a.Language, new BsonRegularExpression($"^{normalizedLang}", "i")),
                    Builders<AudioContent>.Filter.Eq(a => a.CreatedByRole, "Admin"),
                    Builders<AudioContent>.Filter.Eq(a => a.IsActive, true)
                );
                await _db.AudioContents.UpdateManyAsync(oldAdminFilter,
                    Builders<AudioContent>.Update.Set(a => a.IsActive, false).Set(a => a.UpdatedAt, DateTime.UtcNow));
            }
            else
            {
                // Vendor: block if there is already a pending audio for same POI + language (awaiting review)
                var pendingExists = await _db.AudioContents.Find(
                    Builders<AudioContent>.Filter.And(
                        Builders<AudioContent>.Filter.Eq(a => a.POI_ID, model.PoiId),
                        Builders<AudioContent>.Filter.Regex(a => a.Language, new BsonRegularExpression($"^{normalizedLang}", "i")),
                        Builders<AudioContent>.Filter.Eq(a => a.Status, AudioStatuses.Pending),
                        Builders<AudioContent>.Filter.Eq(a => a.IsDeleted, false)
                    )).AnyAsync();
                if (pendingExists)
                    return Conflict(new { message = "B\u1ea1n đ\u00e3 c\u00f3 m\u1ed9t audio đang ch\u1edd duy\u1ec7t cho ng\u00f4n ng\u1eef n\u00e0y. H\u00e3y ch\u1edd admin duy\u1ec7t ho\u1eb7c x\u00f3a b\u1ea3n c\u0169." });
            }

            // If file is in temp uploads, move it to final audio folder before saving
            if (!string.IsNullOrWhiteSpace(model.FilePath) && IsTempAudioPath(model.FilePath))
            {
                var fileName = string.IsNullOrWhiteSpace(model.FileName)
                    ? Path.GetFileName(model.FilePath)
                    : model.FileName;

                if (!string.IsNullOrWhiteSpace(fileName))
                {
                    var tempPath = GetTempAudioPhysicalPath(model.FilePath, fileName);
                    var finalDir = Path.Combine(_env.ContentRootPath, "Uploads", "audio");
                    Directory.CreateDirectory(finalDir);
                    var finalPath = Path.Combine(finalDir, fileName);

                    if (System.IO.File.Exists(tempPath))
                    {
                        if (System.IO.File.Exists(finalPath))
                        {
                            System.IO.File.Delete(finalPath);
                        }
                        System.IO.File.Move(tempPath, finalPath);
                        model.FilePath = $"/uploads/audio/{fileName}";
                        model.FileName = fileName;
                    }
                }
            }

            // Create audio record
            var audio = new AudioContent
            {
                AudioContent_ID = nextId,
                Title = model.Title ?? "Untitled",
                Description = model.Description ?? "",
                Language = NormalizeLanguage(model.Language),
                AudioUrl = model.FilePath ?? "",
                FileSize = model.FileSize,
                Duration = model.Duration > 0 ? (int)Math.Round(model.Duration) : null,
                TTSText = model.TtsText,
                Format = model.Format,
                Bitrate = model.Bitrate,
                TTSVoice = model.TtsVoice,
                TTSProvider = model.TtsProvider,
                TTSSpeed = model.TtsSpeed,
                TTSPitch = model.TtsPitch,
                TTSConfig = model.TtsConfig,
                TemplateId = model.TemplateId,
                TemplateName = model.TemplateName,
                TemplateVariables = model.TemplateVariables,
                POI_ID = model.PoiId,
                VendorId = poi.VendorId,
                Status = status,
                CreatedByUserId = GetUserId(),
                CreatedByRole = role,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            await _db.AudioContents.InsertOneAsync(audio);

            return CreatedAtAction(nameof(GetAudio), new { id = audio.AudioContent_ID }, audio);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { 
                message = "Error creating audio record", 
                error = ex.Message,
                innerError = ex.InnerException?.Message 
            });
        }
    }

    /// <summary>
    /// Upload audio file for a POI
    /// </summary>
    [Authorize(Roles = "Admin,Vendor")]
    [HttpPost("upload")]
    public async Task<ActionResult<AudioContent>> UploadAudio([FromForm] UploadAudioModel model)
    {
        if (model.AudioFile == null || model.AudioFile.Length == 0)
        {
            return BadRequest(new { message = "No file uploaded" });
        }

        if (model.AudioFile.Length > MaxAudioBytes)
        {
            return BadRequest(new { message = $"File size exceeds {(MaxAudioBytes / (1024 * 1024))}MB limit" });
        }

        var extension = Path.GetExtension(model.AudioFile.FileName)?.ToLowerInvariant() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(extension) || !AllowedAudioExtensions.Contains(extension))
        {
            return BadRequest(new { message = "Only MP3, WAV, and M4A audio files are allowed" });
        }

        // Check POI exists
        var poi = await _db.POIs.Find(p => p.POI_ID == model.POI_ID && p.DeletedAt == null).FirstOrDefaultAsync();
        if (poi == null)
        {
            return NotFound(new { message = "POI not found" });
        }

        if (IsVendor())
        {
            var vendor = await GetVendorProfileAsync();
            if (vendor == null || poi.VendorId != vendor.VendorId)
            {
                return Forbid();
            }
        }

        // Save under ContentRoot/Uploads/audio (served via /uploads static mapping in Program.cs)
        var finalDir = Path.Combine(_env.ContentRootPath, "Uploads", "audio");
        Directory.CreateDirectory(finalDir);

        var fileName = $"{Guid.NewGuid()}{extension}";
        var filePath = Path.Combine(finalDir, fileName);

        // Save file
        using (var stream = new FileStream(filePath, FileMode.Create))
        {
            await model.AudioFile.CopyToAsync(stream);
        }

        var nextId = await _sequence.GetNextAsync("audio_content_id");
        var role = GetPrimaryRole();
        var normalizedLangUpload = NormalizeLanguage(model.Language);
        var status = role == "Admin" ? AudioStatuses.Approved : AudioStatuses.Draft;

        // Deduplication guards
        if (role == "Admin")
        {
            // Deactivate existing Admin audio for same POI + language
            var oldAdminFilter = Builders<AudioContent>.Filter.And(
                Builders<AudioContent>.Filter.Eq(a => a.POI_ID, model.POI_ID),
                Builders<AudioContent>.Filter.Regex(a => a.Language, new BsonRegularExpression($"^{normalizedLangUpload}", "i")),
                Builders<AudioContent>.Filter.Eq(a => a.CreatedByRole, "Admin"),
                Builders<AudioContent>.Filter.Eq(a => a.IsActive, true)
            );
            await _db.AudioContents.UpdateManyAsync(oldAdminFilter,
                Builders<AudioContent>.Update.Set(a => a.IsActive, false).Set(a => a.UpdatedAt, DateTime.UtcNow));
        }
        else
        {
            // Vendor: block if already has a pending audio for same POI + language
            var pendingExists = await _db.AudioContents.Find(
                Builders<AudioContent>.Filter.And(
                    Builders<AudioContent>.Filter.Eq(a => a.POI_ID, model.POI_ID),
                    Builders<AudioContent>.Filter.Regex(a => a.Language, new BsonRegularExpression($"^{normalizedLangUpload}", "i")),
                    Builders<AudioContent>.Filter.Eq(a => a.Status, AudioStatuses.Pending),
                    Builders<AudioContent>.Filter.Eq(a => a.IsDeleted, false)
                )).AnyAsync();
            if (pendingExists)
            {
                // Clean up the already-saved file since we abort
                if (System.IO.File.Exists(filePath)) System.IO.File.Delete(filePath);
                return Conflict(new { message = "B\u1ea1n đ\u00e3 c\u00f3 m\u1ed9t audio đang ch\u1edd duy\u1ec7t. H\u00e3y ch\u1edd admin duy\u1ec7t ho\u1eb7c x\u00f3a b\u1ea3n c\u0169." });
            }
}
            var audio = new AudioContent
            {
                AudioContent_ID = nextId,
                Title = model.Title,
                Description = model.Description,
                Language = NormalizeLanguage(model.Language),
                AudioUrl = $"/uploads/audio/{fileName}",
                FileSize = model.AudioFile.Length,
                TTSText = model.TTSText,
                Format = extension.TrimStart('.'),
                POI_ID = model.POI_ID,
                VendorId = poi.VendorId,
                Status = status,
                CreatedByUserId = GetUserId(),
                CreatedByRole = role,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await _db.AudioContents.InsertOneAsync(audio);

        return CreatedAtAction(nameof(GetAudio), new { id = audio.AudioContent_ID }, audio);
    }

    /// <summary>
    /// Replace audio file for an existing audio record
    /// </summary>
    [Authorize(Roles = "Admin,Vendor")]
    [HttpPost("{id}/replace-file")]
    public async Task<ActionResult<AudioContent>> ReplaceAudioFile(int id, [FromForm] ReplaceAudioFileModel model)
    {
        if (model.AudioFile == null || model.AudioFile.Length == 0)
        {
            return BadRequest(new { message = "No file uploaded" });
        }

        if (model.AudioFile.Length > MaxAudioBytes)
        {
            return BadRequest(new { message = $"File size exceeds {(MaxAudioBytes / (1024 * 1024))}MB limit" });
        }

        var extension = Path.GetExtension(model.AudioFile.FileName)?.ToLowerInvariant() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(extension) || !AllowedAudioExtensions.Contains(extension))
        {
            return BadRequest(new { message = "Only MP3, WAV, and M4A audio files are allowed" });
        }

        var audio = await _db.AudioContents.Find(a => a.AudioContent_ID == id).FirstOrDefaultAsync();
        if (audio == null)
        {
            return NotFound(new { message = "Audio not found" });
        }

        if (IsVendor())
        {
            var vendor = await GetVendorProfileAsync();
            if (vendor == null || !await VendorOwnsPoiAsync(vendor.VendorId, audio.POI_ID))
            {
                return Forbid();
            }

            var status = NormalizeStatus(audio.Status);
            if (status == AudioStatuses.Approved || status == AudioStatuses.Published)
            {
                return BadRequest(new { message = "Approved audio cannot be edited by vendor" });
            }
        }

        // Delete old file if exists
        if (!string.IsNullOrEmpty(audio.AudioUrl) && TryGetAudioPhysicalPath(audio.AudioUrl, out var oldPath))
        {
            if (System.IO.File.Exists(oldPath))
            {
                System.IO.File.Delete(oldPath);
            }
        }

        // Save new file
        var finalDir = Path.Combine(_env.ContentRootPath, "Uploads", "audio");
        Directory.CreateDirectory(finalDir);
        var fileName = $"{Guid.NewGuid()}{extension}";
        var filePath = Path.Combine(finalDir, fileName);
        using (var stream = new FileStream(filePath, FileMode.Create))
        {
            await model.AudioFile.CopyToAsync(stream);
        }

        var audioUrl = $"/uploads/audio/{fileName}";
        var format = extension.TrimStart('.');
        var updatedAt = DateTime.UtcNow;

        var update = Builders<AudioContent>.Update
            .Set(a => a.AudioUrl, audioUrl)
            .Set(a => a.FileSize, model.AudioFile.Length)
            .Set(a => a.Format, format)
            .Set(a => a.Duration, null)
            .Set(a => a.UpdatedAt, updatedAt);

        // Vendor replacing file: reset status to draft so they can re-submit for review
        if (IsVendor())
        {
            update = update
                .Set(a => a.Status, AudioStatuses.Draft)
                .Set(a => a.RejectedReason, null);
        }

        await _db.AudioContents.UpdateOneAsync(a => a.AudioContent_ID == id, update);

        audio.AudioUrl = audioUrl;
        audio.FileSize = model.AudioFile.Length;
        audio.Format = format;
        audio.Duration = null;
        audio.UpdatedAt = updatedAt;

        return Ok(audio);
    }

    /// <summary>
    /// Get POIs without audio for a specific language
    /// </summary>
    [Authorize(Roles = "Admin,Vendor")]
    [HttpGet("pois-without-audio")]
    public async Task<ActionResult<List<POIWithoutAudioDto>>> GetPOIsWithoutAudio([FromQuery] string language = "vi")
    {
        var normalizedLanguage = NormalizeLanguage(language);
        var langRegex = new BsonRegularExpression($"^{normalizedLanguage}", "i");
        var statusFilter = Builders<AudioContent>.Filter.In(a => a.Status, new[] { AudioStatuses.Approved, AudioStatuses.Published });
        var filter = Builders<AudioContent>.Filter.Regex(a => a.Language, langRegex) & statusFilter;
        var poiIdsWithAudio = await _db.AudioContents
            .DistinctAsync(a => a.POI_ID, filter);
        var poiIdList = await poiIdsWithAudio.ToListAsync();

        var poiFilter = Builders<POI>.Filter.Eq(p => p.IsActive, true) &
                        Builders<POI>.Filter.Eq(p => p.DeletedAt, null) &
                        Builders<POI>.Filter.Nin(p => p.POI_ID, poiIdList) &
                        Builders<POI>.Filter.Ne(p => p.Description_Vi, null) &
                        Builders<POI>.Filter.Ne(p => p.Description_Vi, string.Empty);

        if (IsVendor())
        {
            var vendor = await GetVendorProfileAsync();
            if (vendor == null)
            {
                return Forbid();
            }

            poiFilter &= Builders<POI>.Filter.Eq(p => p.VendorId, vendor.VendorId);
        }

        var poisRaw = await _db.POIs
            .Find(poiFilter)
            .ToListAsync();

        var poisWithoutAudio = poisRaw.Select(p => new POIWithoutAudioDto
        {
            POI_ID = p.POI_ID,
            Name_Vi = p.Name_Vi,
            Name_En = p.Name_En,
            Name_Zh = p.Name_Zh,
            Description_Vi = p.Description_Vi!,
            Description_En = p.Description_En,
            Description_Zh = p.Description_Zh,
            Address = p.Address,
            Latitude = (decimal)(p.Location?.Latitude ?? 0),
            Longitude = (decimal)(p.Location?.Longitude ?? 0),
            SignatureDish = p.SignatureDishes?.FirstOrDefault(),
            SignatureDishes = p.SignatureDishes,
            Specialties = p.Specialties,
            OpeningHours = p.OpeningHours,
            OpeningHoursText = p.OpeningHoursText,
            PhoneNumber = p.PhoneNumber,
            AveragePrice = p.AveragePrice,
            PriceLevel = p.PriceLevel,
            Rating = p.Rating,
            Tags = p.Tags,
            History = p.History,
            Story = p.Story,
            ImageUrl = p.ImageUrl,
            ImageUrls = p.ImageUrls,
            HasAudio = false
        }).ToList();

        return Ok(poisWithoutAudio);
    }

    /// <summary>
    /// Update audio metadata
    /// </summary>
    [Authorize(Roles = "Admin,Vendor")]
    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateAudio(int id, [FromBody] UpdateAudioModel model)
    {
        var audio = await _db.AudioContents.Find(a => a.AudioContent_ID == id).FirstOrDefaultAsync();

        if (audio == null)
        {
            return NotFound(new { message = "Audio not found" });
        }

        if (IsVendor())
        {
            var vendor = await GetVendorProfileAsync();
            if (vendor == null || !await VendorOwnsPoiAsync(vendor.VendorId, audio.POI_ID))
            {
                return Forbid();
            }

            var status = NormalizeStatus(audio.Status);
            if (status == AudioStatuses.Approved || status == AudioStatuses.Published)
            {
                return BadRequest(new { message = "Approved audio cannot be edited by vendor" });
            }
        }

        var update = Builders<AudioContent>.Update
            .Set(a => a.Title, model.Title ?? audio.Title)
            .Set(a => a.Description, model.Description ?? audio.Description)
            .Set(a => a.IsActive, model.IsActive ?? audio.IsActive)
            .Set(a => a.UpdatedAt, DateTime.UtcNow);

        await _db.AudioContents.UpdateOneAsync(a => a.AudioContent_ID == id, update);

        return Ok(audio);
    }

    /// <summary>
    /// Delete audio
    /// </summary>
    [Authorize(Roles = "Admin,Vendor")]
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteAudio(int id)
    {
        var audio = await _db.AudioContents.Find(a => a.AudioContent_ID == id).FirstOrDefaultAsync();

        if (audio == null)
        {
            return NotFound(new { message = "Audio not found" });
        }

        if (IsVendor())
        {
            var vendor = await GetVendorProfileAsync();
            if (vendor == null || !await VendorOwnsPoiAsync(vendor.VendorId, audio.POI_ID))
            {
                return Forbid();
            }

            var status = NormalizeStatus(audio.Status);
            if (status == AudioStatuses.Approved || status == AudioStatuses.Published)
            {
                return BadRequest(new { message = "Approved audio cannot be deleted by vendor" });
            }
        }

        // Delete physical file
        if (!string.IsNullOrEmpty(audio.AudioUrl) && TryGetAudioPhysicalPath(audio.AudioUrl, out var filePath))
        {
            if (System.IO.File.Exists(filePath))
            {
                System.IO.File.Delete(filePath);
            }
        }

        await _db.AudioContents.DeleteOneAsync(a => a.AudioContent_ID == id);

        return Ok(new { message = "Audio deleted successfully" });
    }

    /// <summary>
    /// Generate audio file for an existing audio record (using TTSText)
    /// </summary>
    [Authorize(Roles = "Admin,Vendor")]
    [HttpPost("{id}/generate-file")]
    public async Task<IActionResult> GenerateAudioFile(int id)
    {
        var audio = await _db.AudioContents.Find(a => a.AudioContent_ID == id).FirstOrDefaultAsync();
        if (audio == null)
        {
            return NotFound(new { message = "Audio not found" });
        }

        if (IsVendor())
        {
            var vendor = await GetVendorProfileAsync();
            if (vendor == null || !await VendorOwnsPoiAsync(vendor.VendorId, audio.POI_ID))
            {
                return Forbid();
            }
        }

        var result = await GenerateAudioFileInternal(audio);
        if (!result.Ok)
        {
            return StatusCode(result.StatusCode ?? 500, new { message = result.Error ?? "TTS generation failed" });
        }

        return Ok(new { audioUrl = result.AudioUrl, fileSize = result.FileSize });
    }

    /// <summary>
    /// Vendor submits audio for admin review
    /// </summary>
    [Authorize(Roles = "Vendor")] // Admin doesn't need to submit since it's auto-approved
    [HttpPost("{id}/submit")]
    public async Task<IActionResult> SubmitAudio(int id)
    {
        var audio = await _db.AudioContents.Find(a => a.AudioContent_ID == id).FirstOrDefaultAsync();
        if (audio == null)
        {
            return NotFound(new { message = "Audio not found" });
        }

        var vendor = await GetVendorProfileAsync();
        if (vendor == null || !await VendorOwnsPoiAsync(vendor.VendorId, audio.POI_ID))
        {
            return Forbid();
        }

        var status = NormalizeStatus(audio.Status);
        if (status == AudioStatuses.Approved)
        {
            return BadRequest(new { message = "Audio đã được duyệt, không thể gửi lại." });
        }
        if (status == AudioStatuses.Pending)
        {
            return BadRequest(new { message = "Audio đang chờ duyệt, không cần gửi lại." });
        }
        // Only allow submit from draft or rejected
        if (status != AudioStatuses.Draft && status != AudioStatuses.Rejected)
        {
            return BadRequest(new { message = $"Không thể gửi duyệt audio ở trạng thái '{status}'." });
        }
        if (string.IsNullOrWhiteSpace(audio.AudioUrl))
        {
            return BadRequest(new { message = "Audio chưa có file âm thanh. Hãy tạo hoặc tải lên file trước khi gửi duyệt." });
        }

        var update = Builders<AudioContent>.Update
            .Set(a => a.Status, AudioStatuses.Pending)
            .Set(a => a.RejectedReason, null)
            .Set(a => a.UpdatedAt, DateTime.UtcNow);

        await _db.AudioContents.UpdateOneAsync(a => a.AudioContent_ID == id, update);
        return Ok(new { message = "Audio đã được gửi đến Admin để duyệt." });
    }

    /// <summary>
    /// Admin approves audio
    /// </summary>
    [Authorize(Roles = "Admin")]
    [HttpPost("{id}/approve")]
    public async Task<IActionResult> ApproveAudio(int id)
    {
        var audio = await _db.AudioContents.Find(a => a.AudioContent_ID == id).FirstOrDefaultAsync();
        if (audio == null)
        {
            return NotFound(new { message = "Audio not found" });
        }

        var update = Builders<AudioContent>.Update
            .Set(a => a.Status, AudioStatuses.Approved)
            .Set(a => a.RejectedReason, null)
            .Set(a => a.ApprovedByUserId, GetUserId())
            .Set(a => a.ApprovedAt, DateTime.UtcNow)
            .Set(a => a.UpdatedAt, DateTime.UtcNow);

        await _db.AudioContents.UpdateOneAsync(a => a.AudioContent_ID == id, update);
        return Ok(new { message = "Audio approved" });
    }

    /// <summary>
    /// Admin rejects audio
    /// </summary>
    [Authorize(Roles = "Admin")]
    [HttpPost("{id}/reject")]
    public async Task<IActionResult> RejectAudio(int id, [FromBody] RejectAudioRequest request)
    {
        var audio = await _db.AudioContents.Find(a => a.AudioContent_ID == id).FirstOrDefaultAsync();
        if (audio == null)
        {
            return NotFound(new { message = "Audio not found" });
        }

        var reason = request?.Reason?.Trim();
        var update = Builders<AudioContent>.Update
            .Set(a => a.Status, AudioStatuses.Rejected)
            .Set(a => a.RejectedReason, reason)
            .Set(a => a.ApprovedByUserId, null)
            .Set(a => a.ApprovedAt, null)
            .Set(a => a.UpdatedAt, DateTime.UtcNow);

        await _db.AudioContents.UpdateOneAsync(a => a.AudioContent_ID == id, update);
        return Ok(new { message = "Audio rejected" });
    }

    /// <summary>
    /// Bulk generate audio files for POIs based on existing descriptions.
    /// </summary>
    [Authorize(Roles = "Admin,Vendor")]
    [HttpPost("bulk-generate")]
    public async Task<IActionResult> BulkGenerate([FromBody] BulkGenerateAudioRequest request)
    {
        var languages = (request.Languages == null || request.Languages.Count == 0)
            ? new List<string> { "vi", "en", "zh" }
            : request.Languages;

        var normalizedLanguageSet = new HashSet<string>(
            languages.Select(NormalizeLanguage),
            StringComparer.OrdinalIgnoreCase);

        var poiFilter = Builders<POI>.Filter.Eq(p => p.DeletedAt, null);
        if (request.PoiIds is { Count: > 0 })
        {
            poiFilter &= Builders<POI>.Filter.In(p => p.POI_ID, request.PoiIds);
        }

        var pois = await _db.POIs.Find(poiFilter).ToListAsync();
        if (pois.Count == 0)
        {
            return Ok(new { message = "No POIs found", created = 0, skipped = 0, failed = 0 });
        }

        var poiIds = pois.Select(p => p.POI_ID).ToList();
        var existingAudios = await _db.AudioContents
            .Find(a => poiIds.Contains(a.POI_ID))
            .ToListAsync();

        // OnlyMissing: only skip when there is an APPROVED audio for this POI+lang.
        // draft/rejected records do NOT count as having audio.
        var approvedStatuses = new[] { AudioStatuses.Approved };
        var existingSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var audio in existingAudios.Where(a => approvedStatuses.Contains(a.Status, StringComparer.OrdinalIgnoreCase) && a.IsActive))
        {
            var key = $"{audio.POI_ID}:{NormalizeLanguage(audio.Language)}";
            existingSet.Add(key);
        }

        var created = 0;
        var skipped = 0;
        var failed = 0;
        var errors = new List<object>();

        foreach (var poi in pois)
        {
            foreach (var language in languages)
            {
                var normalized = NormalizeLanguage(language);
                if (!normalizedLanguageSet.Contains(normalized))
                {
                    continue;
                }

                var key = $"{poi.POI_ID}:{normalized}";
                if (request.OnlyMissing && existingSet.Contains(key))
                {
                    skipped++;
                    continue;
                }

                var role = GetPrimaryRole();
                var nextId = await _sequence.GetNextAsync("audio_content_id");
                var audio = new AudioContent
                {
                    AudioContent_ID = nextId,
                    Title = $"{poi.GetName(normalized)} - {language}",
                    Description = string.Empty,
                    Language = normalized,
                    AudioUrl = string.Empty,
                    FileSize = null,
                    Duration = null,
                    TTSText = null,
                    POI_ID = poi.POI_ID,
                    VendorId = poi.VendorId,
                    // Admin auto-approved; Vendor starts as draft and needs to submit for review
                    Status = role == "Admin" ? AudioStatuses.Approved : AudioStatuses.Draft,
                    CreatedByUserId = GetUserId(),
                    CreatedByRole = role,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                await _db.AudioContents.InsertOneAsync(audio);

                var result = await GenerateAudioFileInternal(audio);
                if (!result.Ok)
                {
                    failed++;
                    errors.Add(new { poiId = poi.POI_ID, language, error = result.Error });
                    continue;
                }

                created++;
                existingSet.Add(key);
            }
        }

        return Ok(new
        {
            message = "Bulk generation completed",
            created,
            skipped,
            failed,
            errors
        });
    }

    private string GetVoiceForLanguage(string language)
    {
        return TtsVoiceCatalog.GetDefaultVoice(language);
    }

    private async Task<(bool Ok, string? Error, int? StatusCode, string? AudioUrl, long? FileSize)> GenerateAudioFileInternal(AudioContent audio)
    {
        var text = audio.TTSText;
        if (string.IsNullOrWhiteSpace(text))
        {
            var poi = await _db.POIs.Find(p => p.POI_ID == audio.POI_ID && p.DeletedAt == null).FirstOrDefaultAsync();
            if (poi != null)
            {
                var langKey = NormalizeLanguage(audio.Language ?? "vi-VN");
                var name = poi.GetName(langKey);
                var desc = poi.GetDescription(langKey) ?? string.Empty;
                text = string.IsNullOrWhiteSpace(desc) ? name : $"{name}. {desc}";
            }
        }

        if (string.IsNullOrWhiteSpace(text))
        {
            return (false, "No TTSText available to generate audio", 400, null, null);
        }

        var voice = TtsVoiceCatalog.GetAllowedVoice(audio.TTSVoice, audio.Language ?? "vi-VN");
        var fileName = $"tts_{Guid.NewGuid()}.mp3";
        var outputPath = GetUploadAudioPath(fileName);
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);

        var pythonPath = ResolvePythonPath();
        var scriptPath = ResolveTtsWrapperPath();

        if (string.IsNullOrWhiteSpace(pythonPath) || string.IsNullOrWhiteSpace(scriptPath))
        {
            return (false, "Python or TTS wrapper not found", 500, null, null);
        }

        var tempTextFile = Path.Combine(Path.GetTempPath(), $"tts_text_{Guid.NewGuid()}.txt");
        var normalizedText = TtsTextPreprocessor.NormalizePlainText(text, audio.Language);
        var ttsText = TtsTextPreprocessor.BuildSsmlIfNeeded(normalizedText, audio.Language);
        await System.IO.File.WriteAllTextAsync(tempTextFile, ttsText, System.Text.Encoding.UTF8);

        try
        {
            var startInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = pythonPath,
                Arguments = $"\"{scriptPath}\" \"{voice}\" \"@{tempTextFile}\" \"{outputPath}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                StandardOutputEncoding = System.Text.Encoding.UTF8,
                StandardErrorEncoding = System.Text.Encoding.UTF8
            };

            using var process = System.Diagnostics.Process.Start(startInfo);
            if (process == null)
            {
                return (false, "Failed to start TTS process", 500, null, null);
            }

            var error = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();
            if (process.ExitCode != 0)
            {
                return (false, $"TTS generation failed: {error}", 500, null, null);
            }

            if (!System.IO.File.Exists(outputPath))
            {
                return (false, "Audio file was not created", 500, null, null);
            }

            var fileInfo = new FileInfo(outputPath);
            var audioUrl = $"/uploads/audio/{fileName}";

            var update = Builders<AudioContent>.Update
                .Set(a => a.AudioUrl, audioUrl)
                .Set(a => a.FileSize, fileInfo.Length)
                .Set(a => a.Format, Path.GetExtension(outputPath).TrimStart('.').ToLowerInvariant())
                .Set(a => a.UpdatedAt, DateTime.UtcNow);

            await _db.AudioContents.UpdateOneAsync(a => a.AudioContent_ID == audio.AudioContent_ID, update);

            return (true, null, null, audioUrl, fileInfo.Length);
        }
        finally
        {
            try { if (System.IO.File.Exists(tempTextFile)) System.IO.File.Delete(tempTextFile); } catch { }
        }
    }

    private string GetUploadAudioPath(string fileName)
    {
        return Path.Combine(_env.ContentRootPath, "Uploads", "audio", fileName);
    }

    private string GetWorkspaceRootPath()
    {
        var parent = Directory.GetParent(_env.ContentRootPath);
        var grandParent = parent != null ? Directory.GetParent(parent.FullName) : null;
        if (grandParent != null) return grandParent.FullName;
        if (parent != null) return parent.FullName;
        return _env.ContentRootPath;
    }

    private static bool IsCommandName(string candidate)
    {
        if (string.IsNullOrWhiteSpace(candidate)) return false;
        if (Path.IsPathRooted(candidate)) return false;
        return !candidate.Contains(Path.DirectorySeparatorChar) && !candidate.Contains(Path.AltDirectorySeparatorChar);
    }

    private List<string> GetPythonCandidates()
    {
        var workspace = GetWorkspaceRootPath();
        return new List<string>
        {
            Environment.GetEnvironmentVariable("TTS_PYTHON_PATH") ?? string.Empty,
            Path.Combine(workspace, ".venv", "Scripts", "python.exe"),
            Path.Combine(_env.ContentRootPath, ".venv", "Scripts", "python.exe"),
            "python",
            "py"
        }.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    private List<string> GetTtsWrapperCandidates()
    {
        var workspace = GetWorkspaceRootPath();
        return new List<string>
        {
            Environment.GetEnvironmentVariable("TTS_WRAPPER_PATH") ?? string.Empty,
            Path.Combine(workspace, "tts_wrapper.py"),
            Path.Combine(_env.ContentRootPath, "tts_wrapper.py")
        }.Where(x => !string.IsNullOrWhiteSpace(x)).Select(Path.GetFullPath).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    private string? ResolvePythonPath()
    {
        foreach (var candidate in GetPythonCandidates())
        {
            if (IsCommandName(candidate)) return candidate;
            if (System.IO.File.Exists(candidate)) return candidate;
        }
        return null;
    }

    private string? ResolveTtsWrapperPath()
    {
        foreach (var candidate in GetTtsWrapperCandidates())
        {
            if (System.IO.File.Exists(candidate)) return candidate;
        }
        return null;
    }

    private bool TryGetAudioPhysicalPath(string audioUrl, out string physicalPath)
    {
        physicalPath = string.Empty;
        if (string.IsNullOrWhiteSpace(audioUrl))
        {
            return false;
        }

        // Only allow deleting files under /uploads/audio/
        if (!audioUrl.StartsWith("/uploads/audio/", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var fileName = Path.GetFileName(audioUrl);
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return false;
        }

        // Primary location (new): ContentRoot/Uploads/audio
        var contentRootPath = Path.Combine(_env.ContentRootPath, "Uploads", "audio", fileName);
        if (contentRootPath.Contains(".."))
        {
            return false;
        }

        physicalPath = contentRootPath;
        if (System.IO.File.Exists(physicalPath))
        {
            return true;
        }

        // Legacy location (older uploads): wwwroot/uploads/audio
        var legacyPath = Path.Combine(_env.WebRootPath, "uploads", "audio", fileName);
        physicalPath = legacyPath;
        return true;
    }

    private static string NormalizeLanguage(string? language)
    {
        if (string.IsNullOrWhiteSpace(language))
        {
            return "vi";
        }

        var lang = language.Trim().ToLowerInvariant();
        if (lang.StartsWith("vi")) return "vi";
        if (lang.StartsWith("en")) return "en";
        if (lang.StartsWith("zh")) return "zh";
        return "vi";
    }

    private static bool IsTempAudioPath(string raw)
    {
        return raw.StartsWith("/uploads/audio-temp/", StringComparison.OrdinalIgnoreCase)
            || raw.StartsWith("/uploads/temp/", StringComparison.OrdinalIgnoreCase);
    }

    private string GetTempAudioPhysicalPath(string raw, string fileName)
    {
        var subDir = raw.StartsWith("/uploads/audio-temp/", StringComparison.OrdinalIgnoreCase) ? "audio-temp" : "temp";
        return Path.Combine(_env.ContentRootPath, "Uploads", subDir, fileName);
    }

    private static string NormalizeStatus(string? status)
    {
        if (string.IsNullOrWhiteSpace(status))
        {
            return AudioStatuses.Published;
        }

        return status.Trim().ToLowerInvariant();
    }

    private bool IsVendor() => User.IsInRole("Vendor") && !User.IsInRole("Admin");

    private string GetUserId()
    {
        return User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
    }

    private string GetPrimaryRole()
    {
        if (User.IsInRole("Admin")) return "Admin";
        if (User.IsInRole("Vendor")) return "Vendor";
        return "Unknown";
    }

    private async Task<VendorProfile?> GetVendorProfileAsync()
    {
        var userId = GetUserId();
        if (string.IsNullOrWhiteSpace(userId))
        {
            return null;
        }

        var vendor = await _db.VendorProfiles.Find(v => v.UserId == userId).FirstOrDefaultAsync();
        if (vendor != null)
        {
            return vendor;
        }

        if (!User.IsInRole("Vendor"))
        {
            return null;
        }

        var vendorId = await _sequence.GetNextAsync("vendor_id");
        var email = User.FindFirstValue(ClaimTypes.Email);
        var name = User.FindFirstValue(ClaimTypes.Name);
        var fallbackName = !string.IsNullOrWhiteSpace(name)
            ? name
            : (!string.IsNullOrWhiteSpace(email) ? email.Split('@')[0] : "Vendor");

        vendor = new VendorProfile
        {
            VendorId = vendorId,
            UserId = userId,
            ContactName = name,
            ContactEmail = email,
            BusinessName = fallbackName
        };

        await _db.VendorProfiles.InsertOneAsync(vendor);
        return vendor;
    }

    private async Task<bool> VendorOwnsPoiAsync(int vendorId, int poiId)
    {
        var poi = await _db.POIs.Find(p => p.POI_ID == poiId && p.DeletedAt == null).FirstOrDefaultAsync();
        return poi != null && poi.VendorId == vendorId;
    }
}

// DTOs
public class AudioDto
{
    public int AudioContent_ID { get; set; }
    public string Title { get; set; } = null!;
    public string Language { get; set; } = null!;
    public string? AudioUrl { get; set; }
    public double? Duration { get; set; }
    public long? FileSize { get; set; }
    public bool IsActive { get; set; }
    public int POI_ID { get; set; }
    public string POIName { get; set; } = null!;
    public int? VendorId { get; set; }
    public string Status { get; set; } = AudioStatuses.Published;
    public string? RejectedReason { get; set; }
    public string? CreatedByRole { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class AudioListResponse
{
    public List<AudioDto> Data { get; set; } = new();
    public int Total { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalPages { get; set; }
}

public class UploadAudioModel
{
    public IFormFile AudioFile { get; set; } = null!;
    public string Title { get; set; } = null!;
    public string? Description { get; set; }
    public string Language { get; set; } = "vi";
    public int POI_ID { get; set; }
    public string? TTSText { get; set; }
}

public class ReplaceAudioFileModel
{
    public IFormFile AudioFile { get; set; } = null!;
}

public class UpdateAudioModel
{
    public string? Title { get; set; }
    public string? Description { get; set; }
    public bool? IsActive { get; set; }
}

public class CreateAudioModel
{
    public int PoiId { get; set; }
    public string Language { get; set; } = "vi";
    public string Title { get; set; } = null!;
    public string? Description { get; set; }
    public string FileName { get; set; } = null!;
    public string FilePath { get; set; } = null!;
    public long FileSize { get; set; }
    public double Duration { get; set; }
    public string Type { get; set; } = "TTS";
    public string? TtsText { get; set; }

    public string? Format { get; set; }
    public int? Bitrate { get; set; }

    public string? TtsVoice { get; set; }
    public string? TtsProvider { get; set; }
    public double? TtsSpeed { get; set; }
    public double? TtsPitch { get; set; }
    public TTSConfig? TtsConfig { get; set; }

    public string? TemplateId { get; set; }
    public string? TemplateName { get; set; }
    public Dictionary<string, string>? TemplateVariables { get; set; }
}

public class POIWithoutAudioDto
{
    public int POI_ID { get; set; }
    public string Name_Vi { get; set; } = null!;
    public string? Name_En { get; set; }
    public string? Name_Zh { get; set; }
    public string Description_Vi { get; set; } = null!;
    public string? Description_En { get; set; }
    public string? Description_Zh { get; set; }
    public string? Address { get; set; }
    public decimal Latitude { get; set; }
    public decimal Longitude { get; set; }
    public string? SignatureDish { get; set; }
    public List<string>? SignatureDishes { get; set; }
    public List<string>? Specialties { get; set; }
    public List<string>? OpeningHours { get; set; }
    public string? OpeningHoursText { get; set; }
    public string? PhoneNumber { get; set; }
    public decimal? AveragePrice { get; set; }
    public int? PriceLevel { get; set; }
    public double? Rating { get; set; }
    public List<string>? Tags { get; set; }
    public string? History { get; set; }
    public string? Story { get; set; }
    public string? ImageUrl { get; set; }
    public List<string>? ImageUrls { get; set; }
    public bool HasAudio { get; set; }
}


public class BulkGenerateAudioRequest
{
    public List<string>? Languages { get; set; }
    public List<int>? PoiIds { get; set; }
    public bool OnlyMissing { get; set; } = true;
}

public class RejectAudioRequest
{
    public string? Reason { get; set; }
}
