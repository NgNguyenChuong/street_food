using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace StreetFoodNarrator.API.Controllers;

[Route("api/[controller]")]
[ApiController]
public class AutoTranslateController : ControllerBase
{
    private readonly HttpClient _httpClient;

    public AutoTranslateController()
    {
        _httpClient = new HttpClient();
    }

    /// <summary>
    /// Tự động dịch text sang nhiều ngôn ngữ cùng lúc dùng Google Translate API publish endpoint
    /// </summary>
    [HttpPost]
    [Authorize] // Bắt buộc đăng nhập (Admin/Vendor ok)
    public async Task<ActionResult<Dictionary<string, string>>> Translate([FromBody] AutoTranslateRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Text))
        {
            return BadRequest("Text to translate cannot be empty.");
        }

        var sourceLang = request.SourceLang ?? "vi";
        var targets = request.TargetLangs ?? new List<string> { "en", "zh-CN" };
        var results = new Dictionary<string, string>();

        foreach (var target in targets)
        {
            // Google Translate often uses "zh-CN" instead of just "zh" for simplified Chinese
            var tl = target.ToLower() == "zh" ? "zh-CN" : target;
            var sl = sourceLang;

            try
            {
                // Unofficial but reliable single-endpoint used by extensions/scripts
                var url = $"https://translate.googleapis.com/translate_a/single?client=gtx&sl={sl}&tl={tl}&dt=t&q={Uri.EscapeDataString(request.Text)}";
                
                var response = await _httpClient.GetStringAsync(url);
                
                // Format trả về của Google là array of arrays: [[["TranslatedText","OriginalText",null,null,1]],null,"vi",null,null,null,1,[]]
                using var doc = JsonDocument.Parse(response);
                var root = doc.RootElement;
                
                if (root.ValueKind == JsonValueKind.Array && root.GetArrayLength() > 0)
                {
                    var linesArray = root[0];
                    if (linesArray.ValueKind == JsonValueKind.Array)
                    {
                        var translatedText = string.Empty;
                        // Nối các câu lại (nếu text có nhiều câu bị tách dòng)
                        foreach (var line in linesArray.EnumerateArray())
                        {
                            if (line.ValueKind == JsonValueKind.Array && line.GetArrayLength() > 0)
                            {
                                translatedText += line[0].GetString();
                            }
                        }
                        results[target] = translatedText.Trim();
                    }
                }
            }
            catch (Exception ex)
            {
                // Lỗi dịch một ngôn ngữ thì ghi log, vẫn cho qua các ngôn ngữ khác
                Console.WriteLine($"[AutoTranslate Error] {target}: {ex.Message}");
                // Có thể throw hoặc trả về string trống, ở đây ta trả string trống
                results[target] = ""; 
            }
        }

        return Ok(results);
    }
}

public class AutoTranslateRequest
{
    public string Text { get; set; } = string.Empty;
    public string SourceLang { get; set; } = "vi";
    public List<string> TargetLangs { get; set; } = new List<string> { "en", "zh-CN" };
}
