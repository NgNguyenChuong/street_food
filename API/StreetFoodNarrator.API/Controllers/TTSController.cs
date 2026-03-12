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

            // ═══ DEBUG LOG: xem text gốc và normalized ═══
            _logger.LogWarning("🔍 TTS DEBUG - Original text: {Text}", request.Text);
            _logger.LogWarning("🔍 TTS DEBUG - Normalized text: {NormalizedText}", normalizedText);

            // Clamp script so playback stays within ~30s (average 2.6 words/s)
            var clamped = ClampToMaxDuration(normalizedText, 30);
            if (string.IsNullOrWhiteSpace(clamped.Text))
            {
                return BadRequest(new { message = "Text is empty after trimming" });
            }

            // Get voice for language (allow explicit voice override)
            var voice = TtsVoiceCatalog.GetAllowedVoice(request.Voice, request.Language ?? "vi-VN");
            
            // Create temp file name
            var fileName = $"tts_{Guid.NewGuid()}.mp3";
            var outputPath = GetUploadAudioPath(fileName);
            
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
            
            // ═══ DEBUG LOG: xem SSML output ═══
            _logger.LogWarning("🔍 TTS DEBUG - Final SSML/text sent to Edge-TTS: {TtsText}", ttsText);
            
            var tempTextFile = Path.Combine(Path.GetTempPath(), $"tts_text_{Guid.NewGuid()}.txt");
            await System.IO.File.WriteAllTextAsync(tempTextFile, ttsText, System.Text.Encoding.UTF8);
            
            try
            {
                var startInfo = new ProcessStartInfo
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
                    filePath = $"/uploads/audio/{fileName}",
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

    private string GetUploadAudioPath(string fileName)
    {
        return Path.Combine(_env.ContentRootPath, "Uploads", "audio", fileName);
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
}

public class TTSRequest
{
    public string Text { get; set; } = string.Empty;
    public string? Language { get; set; } = "vi-VN";
    public string? Voice { get; set; }
}

public class VoiceInfo
{
    public string Language { get; set; } = string.Empty;
    public string Voice { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Gender { get; set; } = string.Empty;
}
