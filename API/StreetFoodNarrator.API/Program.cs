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
var preferLatestPublishedApk = bool.TryParse(
    builder.Configuration["AppDownload:PreferLatestPublishedApk"],
    out var preferLatestFromConfig) && preferLatestFromConfig;
var androidApkDownloadUrl = ResolveAndroidApkDownloadUrl(
    configuredAndroidApkDownloadUrl,
    builder.Environment.ContentRootPath,
    preferLatestPublishedApk);
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
builder.Services.AddHostedService<DeviceActivityMonitorService>();

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
        Console.WriteLine("POI reset completed.");
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"POI reset failed: {ex.Message}");
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
    "/apk-download.html",
    "/web-app",
    "/web-app.html"
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

static void ApplyApkNoCacheHeaders(StaticFileResponseContext context)
{
    if (!string.Equals(Path.GetExtension(context.File.Name), ".apk", StringComparison.OrdinalIgnoreCase))
        return;

    context.Context.Response.Headers["Cache-Control"] = "no-store, no-cache, must-revalidate, max-age=0";
    context.Context.Response.Headers["Pragma"] = "no-cache";
    context.Context.Response.Headers["Expires"] = "0";
}

// Serve runtime uploads from outside wwwroot to avoid StaticWebAssets build-time locks
var uploadsRoot = Path.Combine(app.Environment.ContentRootPath, "Uploads");
Directory.CreateDirectory(uploadsRoot);
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(uploadsRoot),
    RequestPath = "/uploads",
    ContentTypeProvider = staticContentTypeProvider,
    OnPrepareResponse = ApplyApkNoCacheHeaders
});

// Do not fall through to wwwroot/uploads: keep /uploads served from one canonical source only.
app.Use(async (context, next) =>
{
    if (context.Request.Path.StartsWithSegments("/uploads", StringComparison.OrdinalIgnoreCase))
    {
        context.Response.StatusCode = 404;
        return;
    }

    await next();
});

app.UseDefaultFiles(); // Enable index.html as default landing
app.UseStaticFiles(new StaticFileOptions
{
    ContentTypeProvider = staticContentTypeProvider,
    OnPrepareResponse = ApplyApkNoCacheHeaders
}); // Serve static files from wwwroot

app.UseCors("AllowFrontend");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

// iOS web app route — serves web-app.html with same query params preserved in the URL.
// Tourists on iOS land here after scanning a QR code (the QR page detects iOS and redirects here).
app.MapGet("/web-app", (HttpContext context, IWebHostEnvironment env) =>
{
    var file = Path.Combine(env.WebRootPath, "web-app.html");
    return Results.File(file, "text/html; charset=utf-8");
});

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
    var safeExpiresAtUtc = expiresAtUtc.HasValue
        ? expiresAtUtc.Value.UtcDateTime.ToString("O", CultureInfo.InvariantCulture)
        : string.Empty;

    var apkLandingUrlForQr = AppendQueryParameter(
        androidApkLandingPageUrl,
        "apk",
        androidApkDownloadUrl);
    apkLandingUrlForQr = AppendQueryParameter(
        apkLandingUrlForQr,
        "dl",
        appDeepLink);

    var safeAppDeepLink = WebUtility.HtmlEncode(appDeepLink);
    var safeApkDownloadUrl = WebUtility.HtmlEncode(androidApkDownloadUrl);
    var safeApkLandingPageUrl = WebUtility.HtmlEncode(apkLandingUrlForQr);
    var safeExpiryText = WebUtility.HtmlEncode(expiryText);
    var safeQrResetText = WebUtility.HtmlEncode(qrResetText);

    // iOS web app fallback: build absolute URL to /web-app with same QR query params.
    // When user is on iOS/iPadOS, the QR page JS will show this button instead of the APK button,
    // directing them to the browser-based web app experience.
    var req = context.Request;
    var webAppUrl = $"{req.Scheme}://{req.Host}/web-app{queryPart}";
    var safeWebAppUrl = WebUtility.HtmlEncode(webAppUrl);
    // For use inside a JS string literal — must NOT be HTML-encoded (& not &amp;).
    // Only escape backslash and double-quote for JS string safety.
    var jsWebAppUrl = webAppUrl.Replace("\\", "\\\\").Replace("\"", "\\\"");

    var htmlTemplate = """
<!doctype html>
<html lang="vi">
<head>
    <meta charset="utf-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1" />
    <title>Street Food Narrator</title>
    <script>
        // Runs immediately before body renders.
        // iOS/iPadOS: redirect straight to web app — no intermediate page shown.
        (function () {
            var isIos = /iPad|iPhone|iPod/.test(navigator.userAgent) && !window.MSStream;
            var isIpadOs = navigator.maxTouchPoints > 1 && /Macintosh/.test(navigator.userAgent);
            if ((isIos || isIpadOs) && "__IS_EXPIRED__" !== "true") {
                var dest = "__WEB_APP_URL_JS__";
                if (dest) { window.location.replace(dest); }
            }
        })();
    </script>
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
            width: min(520px, 100%);
            background: rgba(255,255,255,0.97);
            border: 1px solid #d6eadb;
            border-radius: 20px;
            box-shadow: 0 18px 50px rgba(19,60,42,0.14);
            padding: 28px 24px 24px;
            backdrop-filter: blur(4px);
        }

        .logo { font-size: 2.4rem; margin-bottom: 10px; text-align: center; }

        h1 {
            margin: 0 0 6px;
            font-size: clamp(1.25rem, 1.1rem + 1vw, 1.75rem);
            line-height: 1.25;
            text-align: center;
        }

        .sub {
            margin: 0 0 6px;
            color: var(--muted);
            line-height: 1.5;
            font-size: 0.93rem;
            text-align: center;
        }

        .meta {
            margin-top: 10px;
            font-size: 0.88rem;
            color: var(--muted);
            text-align: center;
        }

        .divider {
            border: none;
            border-top: 1px solid #d6eadb;
            margin: 18px 0 16px;
        }

        .choice-label {
            font-size: 0.78rem;
            font-weight: 700;
            text-transform: uppercase;
            letter-spacing: 0.06em;
            color: var(--muted);
            margin-bottom: 10px;
            text-align: center;
        }

        .actions {
            display: flex;
            flex-direction: column;
            gap: 10px;
        }

        .btn {
            appearance: none;
            border: 0;
            border-radius: 13px;
            cursor: pointer;
            font-weight: 700;
            font-size: 0.97rem;
            padding: 13px 18px;
            text-decoration: none;
            transition: transform 0.15s ease, opacity 0.15s ease, background-color 0.15s ease;
            display: flex;
            align-items: center;
            justify-content: center;
            gap: 8px;
            width: 100%;
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
            border: 1.5px solid var(--secondary-border);
        }

        .btn-secondary:hover { border-color: var(--primary); color: var(--primary); }

        .btn.disabled {
            opacity: 0.45;
            pointer-events: none;
        }

        .btn-icon { font-size: 1.15em; }

        .hint {
            margin-top: 12px;
            padding: 10px 12px;
            border-radius: 10px;
            border: 1px solid var(--warn-border);
            background: var(--warn-bg);
            color: var(--warn-ink);
            font-size: 0.88rem;
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
            font-size: 0.93rem;
            line-height: 1.45;
            display: none;
        }

        .expired.visible { display: block; }
    </style>
</head>
<body data-app-link="__APP_LINK__" data-apk-link="__APK_LINK__" data-fallback-link="__FALLBACK_LINK__" data-is-expired="__IS_EXPIRED__" data-expires-at="__EXPIRES_AT_UTC__" data-web-app-url="__WEB_APP_URL__">
    <main class="card">
        <div class="logo">🍜</div>
        <h1 id="title">Street Food Narrator</h1>
        <p class="sub" id="subtitle">Phố ẩm thực Vĩnh Khánh</p>
        <p class="meta" id="expiryInfo">__EXPIRY_TEXT__</p>

        <hr class="divider">
        <div class="choice-label">Chọn cách trải nghiệm</div>

        <div class="actions" id="actionGroup">
            <!-- Dùng trên Web — hoạt động ngay, không cần cài -->
            <a class="btn btn-primary" id="webAppBtn" href="__WEB_APP_URL__">
                <span class="btn-icon">🌐</span> Dùng trên Web ngay
            </a>
            <!-- Tải APK — cài app native Android -->
            <a class="btn btn-secondary" id="downloadApkBtn" href="__APK_LINK__" target="_blank" rel="noopener noreferrer">
                <span class="btn-icon">📥</span> Tải ứng dụng Android
            </a>
        </div>

        <p class="hint" id="fallbackHint">Ứng dụng Android mang lại trải nghiệm thuyết minh tốt nhất với định vị GPS tự động.</p>
        <p class="expired" id="expiredHint">QR này đã hết hạn. Vui lòng lấy mã mới tại điểm đến.</p>
    </main>

    <script>
        (() => {
            const apkLink    = document.body.dataset.apkLink    || "";
            const webAppUrl  = document.body.dataset.webAppUrl  || "";
            const isExpired  = (document.body.dataset.isExpired || "false") === "true";
            const expiresAtText = document.body.dataset.expiresAt || "";
            const expiryAtMs = expiresAtText ? Date.parse(expiresAtText) : Number.NaN;
            const expiryLabel = document.getElementById("expiryInfo")?.textContent || "";
            let expiryTimer = null;

            const downloadBtn  = document.getElementById("downloadApkBtn");
            const webAppBtn    = document.getElementById("webAppBtn");
            const actions      = document.getElementById("actionGroup");
            const title        = document.getElementById("title");
            const subtitle     = document.getElementById("subtitle");
            const expiryInfo   = document.getElementById("expiryInfo");
            const hint         = document.getElementById("fallbackHint");
            const expiredHint  = document.getElementById("expiredHint");

            // Disable APK button if no valid APK URL
            if (!apkLink || apkLink === "#") {
                downloadBtn.classList.add("disabled");
                downloadBtn.removeAttribute("href");
            }

            // Disable web app button if no URL
            if (!webAppUrl) {
                webAppBtn.classList.add("disabled");
                webAppBtn.removeAttribute("href");
            }

            // Show APK hint after short delay so user sees both choices first
            window.setTimeout(() => hint.classList.add("visible"), 1500);

            const formatRemaining = (ms) => {
                const totalSeconds = Math.max(0, Math.floor(ms / 1000));
                const minutes = Math.floor(totalSeconds / 60);
                const seconds = totalSeconds % 60;
                if (minutes >= 60) {
                    const hours = Math.floor(minutes / 60);
                    return `${hours} giờ ${minutes % 60} phút`;
                }
                if (minutes > 0) return `${minutes} phút ${String(seconds).padStart(2, "0")} giây`;
                return `${seconds} giây`;
            };

            const clearExpiryTimer = () => {
                if (expiryTimer) { window.clearInterval(expiryTimer); expiryTimer = null; }
            };

            const showExpiredState = (message) => {
                clearExpiryTimer();
                title.textContent = "QR đã hết hạn";
                if (subtitle)   { subtitle.textContent = ""; subtitle.style.display = "none"; }
                if (expiryInfo) { expiryInfo.style.display = "none"; }
                if (actions)    { actions.style.display = "none"; }
                if (hint)       { hint.style.display = "none"; }
                expiredHint.textContent = message || "QR này đã hết hạn. Vui lòng lấy mã mới.";
                expiredHint.classList.add("visible");
            };

            const refreshExpiryState = () => {
                if (!expiryInfo || Number.isNaN(expiryAtMs)) return;
                const remainingMs = expiryAtMs - Date.now();
                if (remainingMs <= 0) {
                    showExpiredState("QR này đã hết hạn. Vui lòng lấy mã mới tại điểm đến.");
                    return;
                }
                expiryInfo.style.display = "block";
                expiryInfo.textContent = `${expiryLabel} | Còn ${formatRemaining(remainingMs)}`;
            };

            if (isExpired) {
                showExpiredState("QR này đã hết hạn. Vui lòng lấy mã mới tại điểm đến.");
                return;
            }

            refreshExpiryState();
            if (!Number.isNaN(expiryAtMs)) {
                clearExpiryTimer();
                expiryTimer = window.setInterval(refreshExpiryState, 1000);
            }
        })();
    </script>
</body>
</html>
""";

    var html = htmlTemplate
        .Replace("__APP_LINK__", safeAppDeepLink, StringComparison.Ordinal)
        .Replace("__APK_LINK__", safeApkDownloadUrl, StringComparison.Ordinal)
        .Replace("__FALLBACK_LINK__", safeApkLandingPageUrl, StringComparison.Ordinal)
        .Replace("__EXPIRES_AT_UTC__", safeExpiresAtUtc, StringComparison.Ordinal)
        .Replace("__IS_EXPIRED__", isExpired ? "true" : "false", StringComparison.Ordinal)
        .Replace("__EXPIRY_TEXT__", safeExpiryText, StringComparison.Ordinal)
        .Replace("__QR_RESET_TEXT__", safeQrResetText, StringComparison.Ordinal)
        .Replace("__WEB_APP_URL__", safeWebAppUrl, StringComparison.Ordinal)
        .Replace("__WEB_APP_URL_JS__", jsWebAppUrl, StringComparison.Ordinal);

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

static string ResolveAndroidApkDownloadUrl(
    string? configuredUrl,
    string contentRootPath,
    bool preferLatestPublishedApk = false)
{
    const string defaultRelativeApkUrl = "/uploads/streetfood-narrator.apk";

    var resolved = string.IsNullOrWhiteSpace(configuredUrl)
        ? defaultRelativeApkUrl
        : configuredUrl.Trim();

    // If config points to canonical APK:
    // - default behavior: keep canonical stable file if it exists
    // - optional behavior: allow auto-pick latest published file via AppDownload:PreferLatestPublishedApk=true
    // - safety fallback: if canonical is missing, try latest published file
    if (!Uri.TryCreate(resolved, UriKind.Absolute, out _) &&
        string.Equals(resolved, defaultRelativeApkUrl, StringComparison.OrdinalIgnoreCase))
    {
        var canonicalApkPath = Path.Combine(contentRootPath, "Uploads", "streetfood-narrator.apk");
        var canonicalExists = File.Exists(canonicalApkPath);
        var shouldTryLatestPublished = preferLatestPublishedApk || !canonicalExists;

        if (shouldTryLatestPublished)
        {
            var latestPublished = TryResolveLatestPublishedApkUrl(contentRootPath);
            if (!string.IsNullOrWhiteSpace(latestPublished))
                resolved = latestPublished;
        }
    }

    return AppendCacheBustVersionIfLocalApk(resolved, contentRootPath);
}

static string? TryResolveLatestPublishedApkUrl(string contentRootPath)
{
    var uploadsRoot = Path.Combine(contentRootPath, "Uploads");
    if (!Directory.Exists(uploadsRoot))
        return null;

    static bool IsLikelyDebugOrRejectedApk(string fileName)
        => fileName.Contains("crashy", StringComparison.OrdinalIgnoreCase) ||
           fileName.Contains("manual", StringComparison.OrdinalIgnoreCase) ||
           fileName.Contains("debug", StringComparison.OrdinalIgnoreCase) ||
           fileName.Contains("unsigned", StringComparison.OrdinalIgnoreCase);

    var newest = new DirectoryInfo(uploadsRoot)
        .GetFiles("*.apk", SearchOption.TopDirectoryOnly)
        .Where(file => !IsLikelyDebugOrRejectedApk(file.Name))
        .OrderByDescending(file => file.LastWriteTimeUtc)
        .FirstOrDefault();

    if (newest == null)
        return null;

    return $"/uploads/{Uri.EscapeDataString(newest.Name)}";
}

static string AppendCacheBustVersionIfLocalApk(string url, string contentRootPath)
{
    if (string.IsNullOrWhiteSpace(url))
        return url;

    var trimmed = url.Trim();
    if (!trimmed.Contains(".apk", StringComparison.OrdinalIgnoreCase))
        return trimmed;

    if (Uri.TryCreate(trimmed, UriKind.Absolute, out _))
        return trimmed;

    var queryIndex = trimmed.IndexOf('?');
    var pathPart = queryIndex >= 0 ? trimmed[..queryIndex] : trimmed;
    if (!pathPart.EndsWith(".apk", StringComparison.OrdinalIgnoreCase))
        return trimmed;

    var relativePath = pathPart.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
    var physicalPath = Path.Combine(contentRootPath, relativePath);
    if (!File.Exists(physicalPath))
        return trimmed;

    if (trimmed.Contains("v=", StringComparison.OrdinalIgnoreCase))
        return trimmed;

    var versionToken = File.GetLastWriteTimeUtc(physicalPath).ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture);
    var separator = queryIndex >= 0 ? "&" : "?";
    return $"{trimmed}{separator}v={versionToken}";
}

static string AppendQueryParameter(string url, string key, string value)
{
    if (string.IsNullOrWhiteSpace(url) || string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(value))
        return url;

    var separator = url.Contains('?') ? "&" : "?";
    return $"{url}{separator}{Uri.EscapeDataString(key)}={Uri.EscapeDataString(value)}";
}
