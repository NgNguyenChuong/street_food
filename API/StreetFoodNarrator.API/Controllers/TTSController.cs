using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;
using System.Text;
using StreetFoodNarrator.API.Services;

namespace StreetFoodNarrator.API.Controllers;

[Route("api/[controller]")]
[ApiController]
[Authorize]
public class TTSController : ControllerBase
{
    private readonly ILogger<TTSController> _logger;
    private readonly IWebHostEnvironment _env;

    public TTSController(ILogger<TTSController> logger, IWebHostEnvironment env)
    {
        _logger = logger;
        _env = env;
    }

    /// <summary>
    /// Test edge-tts installation
    /// </summary>
    [HttpGet("test")]
    [AllowAnonymous]
    public IActionResult TestTTS()
    {
        try
        {
            // Check multiple possible paths
            var paths = new List<string>
            {
                Path.Combine(Directory.GetParent(_env.ContentRootPath)!.Parent!.FullName, ".venv", "Scripts", "edge-tts.exe"),
                "D:\\project\\street_food\\.venv\\Scripts\\edge-tts.exe",
                "edge-tts"
            };

            var existingPaths = paths.Where(p => {
                try { return System.IO.File.Exists(p); } catch { return false; }
            }).ToList();

            return Ok(new
            {
                message = "TTS Test",
                contentRoot = _env.ContentRootPath,
                parentPath = Directory.GetParent(_env.ContentRootPath)?.FullName,
                grandParentPath = Directory.GetParent(_env.ContentRootPath)?.Parent?.FullName,
                testedPaths = paths,
                existingPaths = existingPaths,
                hasEdgeTts = existingPaths.Any()
            });
        }
        catch (Exception ex)
        {
            return Ok(new { error = ex.Message, stack = ex.StackTrace });
        }
    }

    /// <summary>
    /// Generate audio using Edge TTS
    /// </summary>
    [HttpPost("generate")]
    [AllowAnonymous]  // Allow demo without auth
    public async Task<IActionResult> GenerateTTS([FromBody] TTSRequest request)
    {
        if (string.IsNullOrEmpty(request.Text))
        {
            return BadRequest(new { message = "Text is required" });
        }

        try
        {
            var normalizedText = TtsTextPreprocessor.NormalizePlainText(request.Text, request.Language);

            // Clamp script so playback stays within ~30s (average 2.6 words/s)
            var clamped = ClampToMaxDuration(normalizedText, 30);
            if (string.IsNullOrWhiteSpace(clamped.Text))
            {
                return BadRequest(new { message = "Text is empty after trimming" });
            }

            // Get voice for language (allow explicit voice override)
            var voice = TtsVoiceCatalog.GetAllowedVoice(request.Voice, request.Language ?? "vi-VN");
            
            // Create file name (poi + language + unique suffix to avoid file-lock conflicts)
            var uniqueSuffix = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
            var fileName = BuildTtsFileName(request.PoiName, request.Language, request.IsTemp == true ? uniqueSuffix : null);
            var subDir = request.IsTemp == true ? "audio-temp" : "audio";
            var outputPath = GetUploadAudioPath(fileName, subDir);
            
            // Ensure directory exists
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);

            // Use Python script wrapper for edge-tts
            var pythonPath = "D:\\project\\street_food\\.venv\\Scripts\\python.exe";
            var scriptPath = "D:\\project\\street_food\\tts_wrapper.py";
            
            if (!System.IO.File.Exists(pythonPath))
            {
                return StatusCode(500, new { 
                    message = "Python executable not found", 
                    path = pythonPath,
                    suggestion = "Make sure Python virtual environment is set up at D:\\project\\street_food\\.venv"
                });
            }
            
            if (!System.IO.File.Exists(scriptPath))
            {
                return StatusCode(500, new { 
                    message = "TTS wrapper script not found", 
                    path = scriptPath 
                });
            }
            
            // Write text to temp file to avoid command line encoding issues with Vietnamese characters
            var ttsText = TtsTextPreprocessor.BuildSsmlIfNeeded(normalizedText, request.Language);
            var tempTextFile = Path.Combine(Path.GetTempPath(), $"tts_text_{Guid.NewGuid()}.txt");
            await System.IO.File.WriteAllTextAsync(tempTextFile, ttsText, System.Text.Encoding.UTF8);
            
            try
            {
                var rate = ToEdgeRate(request.Speed);
                var volume = ToEdgeVolume(request.Volume);

                var startInfo = new ProcessStartInfo
                {
                    FileName = pythonPath,
                    Arguments = $"\"{scriptPath}\" \"{voice}\" \"@{tempTextFile}\" \"{outputPath}\" \"{rate}\" \"{volume}\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    StandardOutputEncoding = System.Text.Encoding.UTF8,
                    StandardErrorEncoding = System.Text.Encoding.UTF8
                };

                using var process = Process.Start(startInfo);
                if (process == null)
                {
                    return StatusCode(500, new { message = "Failed to start edge-tts process" });
                }

                var output = await process.StandardOutput.ReadToEndAsync();
                var error = await process.StandardError.ReadToEndAsync();
                await process.WaitForExitAsync();

                if (process.ExitCode != 0)
                {
                    _logger.LogError($"Edge-TTS error: {error}");
                    return StatusCode(500, new { message = "TTS generation failed", error });
                }

                // Check if file exists
                if (!System.IO.File.Exists(outputPath))
                {
                    return StatusCode(500, new { message = "Audio file was not created" });
                }

                // Return file info
                var fileInfo = new FileInfo(outputPath);
                return Ok(new
                {
                    fileName,
                    filePath = $"/uploads/{subDir}/{fileName}",
                    fileSize = fileInfo.Length,
                    durationSeconds = clamped.EstimatedSeconds,
                    truncated = clamped.Truncated,
                    language = request.Language,
                    voice
                });
            }
            finally
            {
                // Clean up temp file
                try
                {
                    if (System.IO.File.Exists(tempTextFile))
                    {
                        System.IO.File.Delete(tempTextFile);
                    }
                }
                catch { /* Ignore cleanup errors */ }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating TTS");
            return StatusCode(500, new { message = "Internal server error: " + ex.Message });
        }
    }

    /// <summary>
    /// Delete a temporary generated audio file
    /// </summary>
    [HttpPost("delete-temp")]
    [Authorize(Roles = "Admin,Vendor")]
    public IActionResult DeleteTemp([FromBody] DeleteTtsFileRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.FilePath))
        {
            return BadRequest(new { message = "FilePath is required" });
        }

        var raw = request.FilePath.Trim();
        if (!IsTempOrAudioPath(raw))
        {
            return BadRequest(new { message = "Invalid file path" });
        }

        var fileName = Path.GetFileName(raw);
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return BadRequest(new { message = "Invalid file path" });
        }

        var subDir = GetSubDirFromPath(raw);
        if (string.IsNullOrWhiteSpace(subDir))
        {
            return BadRequest(new { message = "Invalid file path" });
        }
        var path = GetUploadAudioPath(fileName, subDir);
        if (System.IO.File.Exists(path))
        {
            System.IO.File.Delete(path);
            return Ok(new { message = "Deleted" });
        }

        return Ok(new { message = "Not found" });
    }

    /// <summary>
    /// Get available voices for a language
    /// </summary>
    [HttpGet("voices")]
    [AllowAnonymous]  // Allow demo without auth
    public async Task<IActionResult> GetVoices([FromQuery] string? language = null)
    {
        try
        {
            var voices = GetAllVoices();
            
            if (!string.IsNullOrEmpty(language))
            {
                voices = voices.Where(v => v.Language == language).ToList();
            }

            return Ok(voices);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting voices");
            return StatusCode(500, new { message = ex.Message });
        }
    }

    /// <summary>
    /// Test TTS with RAW text (no processing) - for debugging
    /// </summary>
    [HttpPost("test-raw")]
    [AllowAnonymous]
    public async Task<IActionResult> TestRawTTS([FromBody] TTSRequest request)
    {
        if (string.IsNullOrEmpty(request.Text))
        {
            return BadRequest(new { message = "Text is required" });
        }

        try
        {
            var voice = TtsVoiceCatalog.GetAllowedVoice(request.Voice, request.Language ?? "vi-VN");
            var fileName = $"tts_raw_{Guid.NewGuid()}.mp3";
            var outputPath = GetUploadAudioPath(fileName);
            
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);

            var pythonPath = "D:\\project\\street_food\\.venv\\Scripts\\python.exe";
            var scriptPath = "D:\\project\\street_food\\tts_wrapper.py";
            
            if (!System.IO.File.Exists(pythonPath) || !System.IO.File.Exists(scriptPath))
            {
                return StatusCode(500, new { message = "Python or TTS wrapper not found" });
            }
            
            // â•â•â• WRITE RAW TEXT - NO PROCESSING AT ALL â•â•â•
            var tempTextFile = Path.Combine(Path.GetTempPath(), $"tts_raw_{Guid.NewGuid()}.txt");
            await System.IO.File.WriteAllTextAsync(tempTextFile, request.Text, System.Text.Encoding.UTF8);
            
            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = pythonPath,
                    Arguments = $"\"{scriptPath}\" \"{voice}\" \"@{tempTextFile}\" \"{outputPath}\" \"{ToEdgeRate(request.Speed)}\" \"{ToEdgeVolume(request.Volume)}\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    StandardOutputEncoding = System.Text.Encoding.UTF8,
                    StandardErrorEncoding = System.Text.Encoding.UTF8
                };

                using var process = Process.Start(startInfo);
                if (process == null)
                {
                    return StatusCode(500, new { message = "Failed to start edge-tts process" });
                }

                var output = await process.StandardOutput.ReadToEndAsync();
                var error = await process.StandardError.ReadToEndAsync();
                await process.WaitForExitAsync();

                if (process.ExitCode != 0)
                {
                    _logger.LogError($"Edge-TTS error: {error}");
                    return StatusCode(500, new { message = "TTS generation failed", error });
                }

                if (!System.IO.File.Exists(outputPath))
                {
                    return StatusCode(500, new { message = "Audio file was not created" });
                }

                var fileInfo = new FileInfo(outputPath);
                return Ok(new
                {
                    fileName,
                    filePath = $"/uploads/audio/{fileName}",
                    fileSize = fileInfo.Length,
                    message = "RAW text sent to Edge-TTS (no normalize, no SSML)",
                    originalText = request.Text,
                    voice
                });
            }
            finally
            {
                try
                {
                    if (System.IO.File.Exists(tempTextFile))
                    {
                        System.IO.File.Delete(tempTextFile);
                    }
                }
                catch { /* Ignore cleanup errors */ }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in raw TTS test");
            return StatusCode(500, new { message = "Internal server error: " + ex.Message });
        }
    }

    private string GetVoiceForLanguage(string language)
    {
        return TtsVoiceCatalog.GetDefaultVoice(language);
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

    private List<VoiceInfo> GetAllVoices()
    {
        var voices = new List<VoiceInfo>();
        foreach (var (language, voice, name, gender) in TtsVoiceCatalog.GetFemaleVoices())
        {
            voices.Add(new VoiceInfo
            {
                Language = language,
                Voice = voice,
                Name = name,
                Gender = gender
            });
        }

        return voices;
    }

    private string EscapeText(string text)
    {
        return text.Replace("\"", "\\\"").Replace("\n", " ").Replace("\r", " ");
    }

    private string GetUploadAudioPath(string fileName, string? subDir = null)
    {
        var dir = string.IsNullOrWhiteSpace(subDir) ? "audio" : subDir;
        return Path.Combine(_env.ContentRootPath, "Uploads", dir, fileName);
    }

    private static (string Text, double EstimatedSeconds, bool Truncated) ClampToMaxDuration(string input, int maxSeconds)
    {
        const double wordsPerSecond = 2.6; // empirical average speech rate
        int maxWords = (int)Math.Floor(maxSeconds * wordsPerSecond);

        var words = (input ?? string.Empty)
            .Split(new[] { ' ', '\n', '\r', '\t' }, StringSplitOptions.RemoveEmptyEntries)
            .ToList();

        bool truncated = words.Count > maxWords;
        if (truncated)
        {
            words = words.Take(maxWords).ToList();
        }

        var finalText = string.Join(" ", words);
        double estSeconds = words.Count / wordsPerSecond;
        return (finalText, Math.Round(estSeconds, 2), truncated);
    }

    private static string BuildTtsFileName(string? poiName, string? language, string? suffix = null)
    {
        var namePart = SanitizeFileSegment(poiName);
        var langPart = NormalizeLangForFile(language);
        return string.IsNullOrWhiteSpace(suffix)
            ? $"{namePart}_{langPart}.mp3"
            : $"{namePart}_{langPart}_{suffix}.mp3";
    }

    private static string NormalizeLangForFile(string? language)
    {
        var lang = (language ?? "vi-VN").Trim().ToLowerInvariant();
        // Keep full language tag, normalize to safe slug (e.g., "vi-VN" -> "vi-vn")
        var sb = new System.Text.StringBuilder();
        foreach (var ch in lang)
        {
            if (char.IsLetterOrDigit(ch)) sb.Append(ch);
            else sb.Append('-');
        }
        var slug = System.Text.RegularExpressions.Regex.Replace(sb.ToString(), "-{2,}", "-").Trim('-');
        return string.IsNullOrWhiteSpace(slug) ? "vi-vn" : slug;
    }

    private static string SanitizeFileSegment(string? input)
    {
        if (string.IsNullOrWhiteSpace(input)) return "audio";
        var normalized = input.Trim().Normalize(System.Text.NormalizationForm.FormD);
        var sb = new System.Text.StringBuilder();
        foreach (var ch in normalized)
        {
            var cat = System.Globalization.CharUnicodeInfo.GetUnicodeCategory(ch);
            if (cat == System.Globalization.UnicodeCategory.NonSpacingMark) continue;
            var lower = char.ToLowerInvariant(ch);
            if (char.IsLetterOrDigit(lower)) sb.Append(lower);
            else if (lower == ' ' || lower == '-') sb.Append('-');
        }
        var slug = System.Text.RegularExpressions.Regex.Replace(sb.ToString(), "-{2,}", "-").Trim('-');
        if (slug.Length == 0) slug = "audio";
        if (slug.Length > 50) slug = slug[..50].Trim('-');
        return slug;
    }

    private static string ToEdgeRate(double? speed)
    {
        if (speed is null) return "+0%";
        var clamped = Math.Max(0.5, Math.Min(1.5, speed.Value));
        var pct = (clamped - 1.0) * 100;
        var sign = pct >= 0 ? "+" : "-";
        return $"{sign}{Math.Abs(pct):0}%";
    }

    private static string ToEdgeVolume(double? volume)
    {
        if (volume is null) return "+0%";
        var clamped = Math.Max(0.5, Math.Min(1.5, volume.Value));
        var pct = (clamped - 1.0) * 100;
        var sign = pct >= 0 ? "+" : "-";
        return $"{sign}{Math.Abs(pct):0}%";
    }

    private static bool IsTempOrAudioPath(string raw)
    {
        return raw.StartsWith("/uploads/audio-temp/", StringComparison.OrdinalIgnoreCase)
            || raw.StartsWith("/uploads/temp/", StringComparison.OrdinalIgnoreCase)
            || raw.StartsWith("/uploads/audio/", StringComparison.OrdinalIgnoreCase);
    }

    private static string? GetSubDirFromPath(string raw)
    {
        if (raw.StartsWith("/uploads/audio-temp/", StringComparison.OrdinalIgnoreCase)) return "audio-temp";
        if (raw.StartsWith("/uploads/temp/", StringComparison.OrdinalIgnoreCase)) return "temp";
        if (raw.StartsWith("/uploads/audio/", StringComparison.OrdinalIgnoreCase)) return "audio";
        return null;
    }
}

public class TTSRequest
{
    public string Text { get; set; } = string.Empty;
    public string? Language { get; set; } = "vi-VN";
    public string? Voice { get; set; }
    public double? Speed { get; set; }
    public double? Volume { get; set; }
    public string? PoiName { get; set; }
    public bool? IsTemp { get; set; }
}

public class DeleteTtsFileRequest
{
    public string FilePath { get; set; } = string.Empty;
}

public class VoiceInfo
{
    public string Language { get; set; } = string.Empty;
    public string Voice { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Gender { get; set; } = string.Empty;
}

