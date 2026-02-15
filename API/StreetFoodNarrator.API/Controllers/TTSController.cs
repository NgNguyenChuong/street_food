using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;
using System.Text;

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
    public async Task<IActionResult> GenerateTTS([FromBody] TTSRequest request)
    {
        if (string.IsNullOrEmpty(request.Text))
        {
            return BadRequest(new { message = "Text is required" });
        }

        try
        {
            // Get voice for language
            var voice = GetVoiceForLanguage(request.Language ?? "vi-VN");
            
            // Create temp file name
            var fileName = $"tts_{Guid.NewGuid()}.mp3";
            var outputPath = Path.Combine(_env.WebRootPath, "uploads", "audio", fileName);
            
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
            var tempTextFile = Path.Combine(Path.GetTempPath(), $"tts_text_{Guid.NewGuid()}.txt");
            await System.IO.File.WriteAllTextAsync(tempTextFile, request.Text, System.Text.Encoding.UTF8);
            
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
        var normalized = NormalizeLanguage(language);
        return normalized switch
        {
            "vi" => "vi-VN-HoaiMyNeural",      // Vietnamese Female
            "en" => "en-US-JennyNeural",        // English US Female
            "ja" => "ja-JP-NanamiNeural",       // Japanese Female
            "ko" => "ko-KR-SunHiNeural",        // Korean Female
            "zh" => "zh-CN-XiaoxiaoNeural",     // Chinese Mandarin Female
            "fr" => "fr-FR-DeniseNeural",       // French Female
            _ => "vi-VN-HoaiMyNeural"              // Default Vietnamese
        };
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
        if (lang.StartsWith("ja")) return "ja";
        if (lang.StartsWith("fr")) return "fr";
        if (lang.StartsWith("ko")) return "ko";
        if (lang.StartsWith("zh")) return "zh";
        return "vi";
    }

    private List<VoiceInfo> GetAllVoices()
    {
        return new List<VoiceInfo>
        {
            new VoiceInfo { Language = "vi-VN", Voice = "vi-VN-HoaiMyNeural", Name = "Hoài My (Nữ)", Gender = "Female" },
            new VoiceInfo { Language = "vi-VN", Voice = "vi-VN-NamMinhNeural", Name = "Nam Minh (Nam)", Gender = "Male" },
            new VoiceInfo { Language = "en-US", Voice = "en-US-JennyNeural", Name = "Jenny (Female)", Gender = "Female" },
            new VoiceInfo { Language = "en-US", Voice = "en-US-GuyNeural", Name = "Guy (Male)", Gender = "Male" },
            new VoiceInfo { Language = "ja-JP", Voice = "ja-JP-NanamiNeural", Name = "Nanami (女性)", Gender = "Female" },
            new VoiceInfo { Language = "ja-JP", Voice = "ja-JP-KeitaNeural", Name = "Keita (男性)", Gender = "Male" },
            new VoiceInfo { Language = "ko-KR", Voice = "ko-KR-SunHiNeural", Name = "Sun-Hi (여성)", Gender = "Female" },
            new VoiceInfo { Language = "ko-KR", Voice = "ko-KR-InJoonNeural", Name = "In-Joon (남성)", Gender = "Male" },
            new VoiceInfo { Language = "zh-CN", Voice = "zh-CN-XiaoxiaoNeural", Name = "Xiaoxiao (女)", Gender = "Female" },
            new VoiceInfo { Language = "zh-CN", Voice = "zh-CN-YunxiNeural", Name = "Yunxi (男)", Gender = "Male" },
            new VoiceInfo { Language = "fr-FR", Voice = "fr-FR-DeniseNeural", Name = "Denise (Femme)", Gender = "Female" },
            new VoiceInfo { Language = "fr-FR", Voice = "fr-FR-HenriNeural", Name = "Henri (Homme)", Gender = "Male" }
        };
    }

    private string EscapeText(string text)
    {
        return text.Replace("\"", "\\\"").Replace("\n", " ").Replace("\r", " ");
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
