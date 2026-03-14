using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace StreetFoodNarrator.API.Services;

public static class TtsTextPreprocessor
{
    private static readonly Regex DecimalRegex = new(@"(?<=\d)\.(?=\d)", RegexOptions.Compiled);
    private static readonly Regex MultiSpaceRegex = new(@"\s+", RegexOptions.Compiled);
    private static readonly Regex EnglishWordRegex = new(@"^[A-Za-z][A-Za-z'\-]*[A-Za-z]$", RegexOptions.Compiled);
    private static readonly Regex UpperAcronymRegex = new(@"^[A-Z]{2,}$", RegexOptions.Compiled);
    private static readonly Regex EnglishTriggerRegex = new(@"[FJWZfjwz]", RegexOptions.Compiled);
    private static readonly Regex HasLatinRegex = new(@"[A-Za-z]", RegexOptions.Compiled);

    private static readonly Dictionary<char, string> ViSymbolMap = new()
    {
        ['%'] = " ph\u1EA7n tr\u0103m ",
        ['&'] = " v\u00E0 ",
        ['@'] = " a c\u00F2ng ",
        ['+'] = " c\u1ED9ng ",
        ['='] = " b\u1EB1ng ",
        ['*'] = " nh\u00E2n ",
        ['/'] = " tr\u00EAn ",
        ['#'] = " s\u1ED1 "
    };

    private static readonly Dictionary<char, string> EnSymbolMap = new()
    {
        ['%'] = " percent ",
        ['&'] = " and ",
        ['@'] = " at ",
        ['+'] = " plus ",
        ['='] = " equals ",
        ['*'] = " times ",
        ['/'] = " over ",
        ['#'] = " number "
    };

    private static readonly Dictionary<char, string> ZhSymbolMap = new()
    {
        ['%'] = " \u767E\u5206\u4E4B ",
        ['&'] = " \u548C ",
        ['@'] = " \u827E\u7279 ",
        ['+'] = " \u52A0 ",
        ['='] = " \u7B49\u4E8E ",
        ['*'] = " \u4E58 ",
        ['/'] = " \u9664\u4EE5 ",
        ['#'] = " \u53F7 "
    };

    public static string NormalizePlainText(string input, string? language)
    {
        var text = (input ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        text = text.Replace("\r\n", "\n").Replace('\r', '\n');
        text = text.Replace("\u2013", "-").Replace("\u2014", "-");
        text = text.Replace("\u2022", ", ").Replace("\u00B7", ", ");
        text = text.Replace("\u2026", ".");

        var normalizedLanguage = TtsVoiceCatalog.NormalizeLanguage(language);
        text = ReplaceDecimals(text, normalizedLanguage);
        if (normalizedLanguage == "vi")
        {
            text = NormalizeVietnameseTimeAndSlash(text);
        }
        text = ReplaceSymbols(text, normalizedLanguage);
        text = MultiSpaceRegex.Replace(text, " ").Trim();
        return text;
    }

    private static string NormalizeVietnameseTimeAndSlash(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return text;

        // 11h00 -> "11 giờ", 11h30 -> "11 giờ 30 phút"
        text = Regex.Replace(text, @"\b([01]?\d|2[0-3])\s*h\s*([0-5]\d)\b", match =>
        {
            var hour = match.Groups[1].Value;
            var minute = match.Groups[2].Value;
            if (minute == "00") return $"{hour} giờ";
            return $"{hour} giờ {minute} phút";
        });

        // 11:00 -> "11 giờ", 11:30 -> "11 giờ 30 phút"
        text = Regex.Replace(text, @"\b([01]?\d|2[0-3]):([0-5]\d)\b", match =>
        {
            var hour = match.Groups[1].Value;
            var minute = match.Groups[2].Value;
            if (minute == "00") return $"{hour} giờ";
            return $"{hour} giờ {minute} phút";
        });

        // 11 giờ 00 -> "11 giờ"
        text = Regex.Replace(text, @"\b([01]?\d|2[0-3])\s*giờ\s*0{1,2}\b", "$1 giờ");

        // 24/7 -> "24 trên 7" (and similar a/b patterns)
        text = Regex.Replace(text, @"\b(\d+)\s*/\s*(\d+)\b", "$1 trên $2");

        return text;
    }

    public static string BuildSsmlIfNeeded(string text, string? language)
    {
        // ═══ FIX: Edge-TTS CLI đọc SSML tags như text thường ═══
        // Tạm thời disable SSML wrapper vì Edge-TTS không parse đúng
        // TODO: Tìm cách enable SSML cho edge-tts CLI nếu cần
        return text;
        
        /* ORIGINAL CODE - CAUSING ISSUE:
        var normalizedLanguage = TtsVoiceCatalog.NormalizeLanguage(language);
        if (normalizedLanguage == "en")
        {
            return text;
        }

        var ssmlLang = normalizedLanguage == "zh" ? "zh-CN" : "vi-VN";
        var tokens = Regex.Split(text, @"(\s+)");
        var sb = new StringBuilder();
        foreach (var token in tokens)
        {
            if (string.IsNullOrEmpty(token))
            {
                continue;
            }

            if (IsEnglishToken(token, normalizedLanguage))
            {
                sb.Append("<lang xml:lang=\"en-US\">");
                sb.Append(EscapeForSsml(token));
                sb.Append("</lang>");
            }
            else
            {
                sb.Append(EscapeForSsml(token));
            }
        }

        return $"<speak version=\"1.0\" xml:lang=\"{ssmlLang}\">{sb}</speak>";
        */
    }

    private static string ReplaceDecimals(string text, string normalizedLanguage)
    {
        var separator = normalizedLanguage switch
        {
            "vi" => " ph\u1EA9y ",
            "en" => " point ",
            "zh" => " \u70B9 ",
            _ => " point "
        };

        return DecimalRegex.Replace(text, separator);
    }

    private static string ReplaceSymbols(string text, string normalizedLanguage)
    {
        var map = normalizedLanguage switch
        {
            "vi" => ViSymbolMap,
            "en" => EnSymbolMap,
            "zh" => ZhSymbolMap,
            _ => ViSymbolMap
        };

        var sb = new StringBuilder(text.Length + 16);
        foreach (var ch in text)
        {
            if (map.TryGetValue(ch, out var replacement))
            {
                sb.Append(replacement);
            }
            else
            {
                sb.Append(ch);
            }
        }

        return sb.ToString();
    }

    private static bool IsEnglishToken(string token, string normalizedLanguage)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return false;
        }

        if (!HasLatinRegex.IsMatch(token))
        {
            return false;
        }

        if (normalizedLanguage == "zh")
        {
            return true;
        }

        if (!EnglishWordRegex.IsMatch(token))
        {
            return false;
        }

        if (UpperAcronymRegex.IsMatch(token))
        {
            return true;
        }

        if (EnglishTriggerRegex.IsMatch(token))
        {
            return true;
        }

        return token.Contains("'") || token.Contains("-");
    }

    private static string EscapeForSsml(string input)
    {
        if (string.IsNullOrEmpty(input))
        {
            return string.Empty;
        }

        return input
            .Replace("&", "&amp;")
            .Replace("<", "&lt;")
            .Replace(">", "&gt;")
            .Replace("\"", "&quot;")
            .Replace("'", "&apos;");
    }
}
