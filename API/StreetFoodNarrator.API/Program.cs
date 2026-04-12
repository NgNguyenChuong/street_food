using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Rewrite;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Extensions.FileProviders;
using MongoDB.Driver;
using StreetFoodNarrator.API.Data;
using StreetFoodNarrator.API.Models;
using StreetFoodNarrator.API.Services;
using System.Globalization;
using System.Net;
using System.Text;
using System.IdentityModel.Tokens.Jwt;

var builder = WebApplication.CreateBuilder(args);
const int QrCodeExpiryWindowDays = 5;
const int QrCodeExpiryWindowMinutesForTest = 3;
var configuredAndroidApkDownloadUrl = builder.Configuration["AppDownload:AndroidApkUrl"]?.Trim();
var androidApkDownloadUrl = string.IsNullOrWhiteSpace(configuredAndroidApkDownloadUrl)
    ? "/uploads/streetfood-narrator.apk"
    : configuredAndroidApkDownloadUrl;
var configuredAndroidApkLandingPageUrl = builder.Configuration["AppDownload:AndroidLandingPageUrl"]?.Trim();
var androidApkLandingPageUrl = string.IsNullOrWhiteSpace(configuredAndroidApkLandingPageUrl)
    ? "/apk-download.html"
    : configuredAndroidApkLandingPageUrl;

// Add services to the container
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        // Fix circular reference issue when serializing navigation properties
        options.JsonSerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
        options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
        options.JsonSerializerOptions.DictionaryKeyPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
        // Accept both camelCase and PascalCase property names from clients
        options.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
    });
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Database Configuration - MongoDB
var mongoDbSettings = builder.Configuration.GetSection("MongoDbSettings").Get<MongoDbSettings>()
    ?? throw new InvalidOperationException("MongoDbSettings not configured");

builder.Services.AddSingleton(mongoDbSettings);
builder.Services.AddSingleton<IMongoClient>(_ => new MongoClient(mongoDbSettings.ConnectionString));
builder.Services.AddSingleton<MongoDbContext>();
builder.Services.AddSingleton<MongoSequenceService>();
builder.Services.AddHostedService<PremiumExpiryMonitorService>();

// Identity Configuration
builder.Services.AddIdentity<ApplicationUser, ApplicationRole>(options =>
{
    options.Password.RequireDigit = false;
    options.Password.RequireLowercase = false;
    options.Password.RequireUppercase = true;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequiredLength = 8;
    options.User.RequireUniqueEmail = true;
})
.AddMongoDbStores<ApplicationUser, ApplicationRole, Guid>(
    mongoDbSettings.ConnectionString,
    mongoDbSettings.DatabaseName)
.AddDefaultTokenProviders();

// JWT Authentication
var jwtSettings = builder.Configuration.GetSection("JwtSettings");
var secretKey = jwtSettings["SecretKey"] ?? throw new InvalidOperationException("JWT SecretKey not configured");

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtSettings["Issuer"],
        ValidAudience = jwtSettings["Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey))
    };
});

// CORS Configuration - Allow frontend to access API
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        if (builder.Environment.IsDevelopment())
        {
            // Development: cho phép tất cả origin (Web admin + Mobile app trên LAN)
            policy.SetIsOriginAllowed(_ => true)
                  .AllowAnyHeader()
                  .AllowAnyMethod()
                  .AllowCredentials();
        }
        else
        {
            policy.WithOrigins(
                    "http://localhost:5004",
                    "http://localhost:5500",
                    "http://127.0.0.1:5500",
                    "http://localhost:3000")
                  .AllowAnyHeader()
                  .AllowAnyMethod()
                  .AllowCredentials();
        }
    });
});

var app = builder.Build();

// Optional one-off POI reset/seed command
if (args.Contains("--reset-pois", StringComparer.OrdinalIgnoreCase))
{
    using var scope = app.Services.CreateScope();
    var services = scope.ServiceProvider;
    try
    {
        await PoiSeedReset.RunAsync(services);
        Console.WriteLine("✅ POI reset completed.");
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"❌ POI reset failed: {ex.Message}");
        Console.Error.WriteLine(ex);
    }
    return;
}

// Initialize database with roles and admin user
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        await DbInitializer.Initialize(services);
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "An error occurred while seeding the database.");
    }
}

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Street Food Narrator API V1");
        c.RoutePrefix = "swagger";
    });
}

// HTTPS redirect chỉ bật ở Production
// Development: tắt để mobile app trên LAN có thể dùng HTTP
if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

// URL Rewriting - Remove .html extension from URLs
// MUST be BEFORE authentication check and UseStaticFiles
var rewriteOptions = new RewriteOptions()
    .AddRewrite(@"^(?!api|swagger|uploads|qr)([a-zA-Z0-9\-_/]+)(?<!\.(js|css|json|png|jpg|jpeg|gif|svg|ico|woff|woff2|ttf|eot|html))(\?.*)?$", "$1.html$2", skipRemainingRules: true);

app.UseRewriter(rewriteOptions);

// Protect admin HTML pages with JWT stored in auth_token cookie
var publicHtmlPages = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
{
    "/",
    "/index.html",
    "/login.html",
    "/register.html",
    "/apk-download.html"
};

app.Use(async (context, next) =>
{
    var path = context.Request.Path.Value ?? string.Empty;

    // Skip API routes
    if (path.StartsWith("/api", StringComparison.OrdinalIgnoreCase))
    {
        await next();
        return;
    }

    // Only check HTML pages (after rewrite, path will have .html)
    if (!path.EndsWith(".html", StringComparison.OrdinalIgnoreCase))
    {
        await next();
        return;
    }

    // Check if it's a public page
    if (publicHtmlPages.Contains(path))
    {
        await next();
        return;
    }

    // Require authentication for protected pages
    var token = context.Request.Cookies["auth_token"];
    if (string.IsNullOrWhiteSpace(token))
    {
        context.Response.Redirect("/index.html");
        return;
    }

    try
    {
        var handler = new JwtSecurityTokenHandler();
        var principal = handler.ValidateToken(token, new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtSettings["Issuer"],
            ValidAudience = jwtSettings["Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey))
        }, out _);

        context.User = principal;
        await next();
    }
    catch
    {
        context.Response.Redirect("/index.html");
    }
});

var staticContentTypeProvider = new FileExtensionContentTypeProvider();
staticContentTypeProvider.Mappings[".apk"] = "application/vnd.android.package-archive";

app.UseDefaultFiles(); // Enable index.html as default landing
app.UseStaticFiles(new StaticFileOptions
{
    ContentTypeProvider = staticContentTypeProvider
}); // Serve static files from wwwroot

// Serve runtime uploads from outside wwwroot to avoid StaticWebAssets build-time locks
var uploadsRoot = Path.Combine(app.Environment.ContentRootPath, "Uploads");
Directory.CreateDirectory(uploadsRoot);
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(uploadsRoot),
    RequestPath = "/uploads",
    ContentTypeProvider = staticContentTypeProvider
});
app.UseCors("AllowFrontend");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.MapGet("/qr/{**deepPath}", (HttpContext context, string? deepPath) =>
{
    var normalizedPath = string.IsNullOrWhiteSpace(deepPath)
        ? "main"
        : deepPath.Trim('/');

    var queryPart = context.Request.QueryString.HasValue
        ? context.Request.QueryString.Value
        : string.Empty;

    var appDeepLink = $"streetfood://qr/{normalizedPath}{queryPart}";
    var expiresAtUtc = TryParseQrExpiry(context.Request.Query);
    var isExpired = expiresAtUtc.HasValue && DateTimeOffset.UtcNow > expiresAtUtc.Value;
    var isTestMode = IsQrTestMode(context.Request.Query);
    var qrResetText = isTestMode
        ? $"{QrCodeExpiryWindowMinutesForTest} phut"
        : $"{QrCodeExpiryWindowDays} ngay";

    var expiryText = expiresAtUtc.HasValue
        ? $"Mã QR có hiệu lực đến: {expiresAtUtc.Value.ToLocalTime().ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture)}"
        : $"Mã QR được làm mới định kỳ mỗi {qrResetText}.";

    var safeAppDeepLink = WebUtility.HtmlEncode(appDeepLink);
    var safeApkDownloadUrl = WebUtility.HtmlEncode(androidApkDownloadUrl);
    var safeApkLandingPageUrl = WebUtility.HtmlEncode(androidApkLandingPageUrl);
    var safeExpiryText = WebUtility.HtmlEncode(expiryText);
    var safeQrResetText = WebUtility.HtmlEncode(qrResetText);

    var htmlTemplate = """
<!doctype html>
<html lang="vi">
<head>
    <meta charset="utf-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1" />
    <title>Street Food Narrator QR</title>
    <style>
        :root {
            color-scheme: light;
            --bg-a: #fff8ed;
            --bg-b: #f2fff3;
            --ink: #153124;
            --muted: #486357;
            --primary: #0f8f52;
            --primary-dark: #0b6d3f;
            --secondary: #fff;
            --secondary-border: #bfd8c9;
            --warn-bg: #fff2e2;
            --warn-border: #ffc98a;
            --warn-ink: #8a4800;
            --danger-bg: #ffe8e8;
            --danger-border: #ffb5b5;
            --danger-ink: #9f1d1d;
        }

        * { box-sizing: border-box; }

        body {
            margin: 0;
            min-height: 100vh;
            font-family: "Segoe UI", "Noto Sans", sans-serif;
            color: var(--ink);
            background:
                radial-gradient(circle at 20% 10%, #ffe7c2 0%, transparent 40%),
                radial-gradient(circle at 80% 90%, #cbf4d4 0%, transparent 35%),
                linear-gradient(140deg, var(--bg-a), var(--bg-b));
            display: grid;
            place-items: center;
            padding: 20px;
        }

        .card {
            width: min(560px, 100%);
            background: rgba(255, 255, 255, 0.95);
            border: 1px solid #d6eadb;
            border-radius: 18px;
            box-shadow: 0 18px 50px rgba(19, 60, 42, 0.14);
            padding: 24px;
            backdrop-filter: blur(4px);
        }

        h1 {
            margin: 0 0 8px;
            font-size: clamp(1.3rem, 1.1rem + 1vw, 2rem);
            line-height: 1.2;
        }

        .sub {
            margin: 0;
            color: var(--muted);
            line-height: 1.5;
        }

        .meta {
            margin-top: 14px;
            font-size: 0.95rem;
            color: var(--muted);
        }

        .actions {
            margin-top: 20px;
            display: flex;
            gap: 12px;
            flex-wrap: wrap;
        }

        .btn {
            appearance: none;
            border: 0;
            border-radius: 12px;
            cursor: pointer;
            font-weight: 700;
            padding: 12px 18px;
            text-decoration: none;
            transition: transform 0.15s ease, opacity 0.15s ease, background-color 0.15s ease;
            display: inline-flex;
            align-items: center;
            justify-content: center;
            min-width: 170px;
        }

        .btn:active { transform: translateY(1px); }

        .btn-primary {
            background: var(--primary);
            color: #fff;
        }

        .btn-primary:hover { background: var(--primary-dark); }

        .btn-secondary {
            background: var(--secondary);
            color: var(--ink);
            border: 1px solid var(--secondary-border);
        }

        .btn.disabled {
            opacity: 0.45;
            pointer-events: none;
        }

        .hint {
            margin-top: 14px;
            padding: 10px 12px;
            border-radius: 10px;
            border: 1px solid var(--warn-border);
            background: var(--warn-bg);
            color: var(--warn-ink);
            font-size: 0.92rem;
            line-height: 1.45;
            display: none;
        }

        .hint.visible { display: block; }

        .expired {
            margin-top: 14px;
            padding: 10px 12px;
            border-radius: 10px;
            border: 1px solid var(--danger-border);
            background: var(--danger-bg);
            color: var(--danger-ink);
            font-size: 0.95rem;
            line-height: 1.45;
            display: none;
        }

        .expired.visible { display: block; }

        @media (max-width: 560px) {
            .card { padding: 18px; border-radius: 14px; }
            .btn { width: 100%; min-width: 0; }
        }
    </style>
</head>
<body data-app-link="__APP_LINK__" data-apk-link="__APK_LINK__" data-fallback-link="__FALLBACK_LINK__" data-is-expired="__IS_EXPIRED__">
    <main class="card">
        <h1 id="title">Đang mở Street Food Narrator...</h1>
        <p class="sub" id="subtitle">Nếu điện thoại đã cài app, ứng dụng sẽ tự mở tại quán bạn vừa quét QR.</p>
        <p class="meta" id="expiryInfo">__EXPIRY_TEXT__</p>

        <div class="actions" id="actionGroup">
            <button class="btn btn-primary" id="openAppBtn" type="button">Mở ứng dụng</button>
            <a class="btn btn-secondary" id="downloadApkBtn" href="__APK_LINK__" target="_blank" rel="noopener noreferrer">Tải APK Android</a>
            <button class="btn btn-secondary" id="downloadQrBtn" type="button">Tải mã QR</button>
        </div>

        <p class="hint" id="fallbackHint">Nếu app chưa mở sau vài giây, hãy bấm <b>Tải APK Android</b> để cài đặt rồi quét lại QR này.</p>
        <p class="expired" id="expiredHint">QR này đã hết hạn. Vui lòng lấy mã mới tại điểm đến trước khi mở app.</p>
    </main>

    <script>
        (() => {
            const appLink = document.body.dataset.appLink || "";
            const apkLink = document.body.dataset.apkLink || "";
            const fallbackLink = document.body.dataset.fallbackLink || "";
            const isExpired = (document.body.dataset.isExpired || "false") === "true";
            let appOpened = false;

            const openBtn = document.getElementById("openAppBtn");
            const downloadBtn = document.getElementById("downloadApkBtn");
            const downloadQrBtn = document.getElementById("downloadQrBtn");
            const actions = document.getElementById("actionGroup");
            const title = document.getElementById("title");
            const subtitle = document.getElementById("subtitle");
            const expiryInfo = document.getElementById("expiryInfo");
            const hint = document.getElementById("fallbackHint");
            const expiredHint = document.getElementById("expiredHint");

            const openApp = () => {
                if (!appLink || isExpired) return;
                window.location.href = appLink;
            };

            const scheduleFallbackToDownloadPage = () => {
                if (isExpired || !fallbackLink) return;
                window.setTimeout(() => {
                    if (appOpened) return;
                    window.location.href = fallbackLink;
                }, 2200);
            };

            document.addEventListener("visibilitychange", () => {
                if (document.visibilityState === "hidden")
                    appOpened = true;
            });

            window.addEventListener("pagehide", () => {
                appOpened = true;
            });

            openBtn.addEventListener("click", () => {
                openApp();
                scheduleFallbackToDownloadPage();
            });

            if (!apkLink || apkLink === "#") {
                downloadBtn.classList.add("disabled");
                downloadBtn.removeAttribute("href");
            }

            if (downloadQrBtn) {
                downloadQrBtn.addEventListener("click", async () => {
                    const qrPayload = window.location.href;
                    const qrUrl = "https://api.qrserver.com/v1/create-qr-code/?size=1024x1024&margin=16&ecc=M&data="
                        + encodeURIComponent(qrPayload);

                    try {
                        const response = await fetch(qrUrl, { cache: "no-store" });
                        if (!response.ok) throw new Error("QR download failed");

                        const blob = await response.blob();
                        const objectUrl = URL.createObjectURL(blob);
                        const anchor = document.createElement("a");
                        anchor.href = objectUrl;
                        anchor.download = "streetfood-qr.png";
                        document.body.appendChild(anchor);
                        anchor.click();
                        anchor.remove();
                        URL.revokeObjectURL(objectUrl);
                    } catch {
                        window.open(qrUrl, "_blank", "noopener,noreferrer");
                    }
                });
            }

            if (isExpired) {
                title.textContent = "QR đã hết hạn";
                if (subtitle) {
                    subtitle.textContent = "";
                    subtitle.style.display = "none";
                }
                if (expiryInfo) {
                    expiryInfo.style.display = "none";
                }
                if (actions) {
                    actions.style.display = "none";
                }
                hint.classList.remove("visible");
                expiredHint.classList.add("visible");

                window.setTimeout(() => {
                    if (window.history.length > 1) {
                        window.history.back();
                    }

                    window.setTimeout(() => {
                        if (document.visibilityState === "hidden") return;
                        try {
                            window.open("", "_self");
                            window.close();
                        } catch {
                            // ignored
                        }

                        window.setTimeout(() => {
                            if (document.visibilityState === "hidden") return;
                            window.location.replace("about:blank");
                        }, 450);
                    }, 450);
                }, 1800);
                return;
            }

            window.setTimeout(openApp, 120);
            scheduleFallbackToDownloadPage();
            window.setTimeout(() => hint.classList.add("visible"), 900);
        })();
    </script>
</body>
</html>
""";

    var html = htmlTemplate
        .Replace("__APP_LINK__", safeAppDeepLink, StringComparison.Ordinal)
        .Replace("__APK_LINK__", safeApkDownloadUrl, StringComparison.Ordinal)
        .Replace("__FALLBACK_LINK__", safeApkLandingPageUrl, StringComparison.Ordinal)
        .Replace("__IS_EXPIRED__", isExpired ? "true" : "false", StringComparison.Ordinal)
        .Replace("__EXPIRY_TEXT__", safeExpiryText, StringComparison.Ordinal)
        .Replace("__QR_RESET_TEXT__", safeQrResetText, StringComparison.Ordinal);

    return Results.Content(html, "text/html; charset=utf-8");
});

app.Run();

static DateTimeOffset? TryParseQrExpiry(IQueryCollection query)
{
        static string? GetFirstValue(IQueryCollection source, params string[] keys)
        {
                foreach (var key in keys)
                {
                        if (source.TryGetValue(key, out var values))
                        {
                                var value = values.ToString();
                                if (!string.IsNullOrWhiteSpace(value))
                                        return value.Trim();
                        }
                }

                return null;
        }

        var rawValue = GetFirstValue(query, "exp", "expires", "expiry", "expiresAt", "expires_at");
        if (string.IsNullOrWhiteSpace(rawValue))
                return null;

        if (long.TryParse(rawValue, out var epochSeconds) && epochSeconds > 0)
        {
                try
                {
                        return DateTimeOffset.FromUnixTimeSeconds(epochSeconds);
                }
                catch
                {
                        return null;
                }
        }

        if (DateTimeOffset.TryParse(rawValue, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsed))
                return parsed.ToUniversalTime();

        return null;
}

static bool IsQrTestMode(IQueryCollection query)
{
    if (query.TryGetValue("mode", out var modeValues))
    {
        var mode = modeValues.ToString();
        if (string.Equals(mode, "test", StringComparison.OrdinalIgnoreCase))
            return true;
    }

    if (query.TryGetValue("cycle", out var cycleValues))
    {
        var cycle = cycleValues.ToString();
        if (!string.IsNullOrWhiteSpace(cycle) &&
            cycle.StartsWith("test-", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }
    }

    return false;
}
