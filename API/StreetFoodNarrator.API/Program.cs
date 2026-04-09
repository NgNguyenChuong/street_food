using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Rewrite;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Extensions.FileProviders;
using MongoDB.Driver;
using StreetFoodNarrator.API.Data;
using StreetFoodNarrator.API.Models;
using System.Text;
using System.IdentityModel.Tokens.Jwt;

var builder = WebApplication.CreateBuilder(args);

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

// Identity Configuration
builder.Services.AddIdentity<ApplicationUser, ApplicationRole>(options =>
{
    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireUppercase = true;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequiredLength = 6;
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
    .AddRewrite(@"^(?!api|swagger|uploads)([a-zA-Z0-9\-_/]+)(?<!\.(js|css|json|png|jpg|jpeg|gif|svg|ico|woff|woff2|ttf|eot|html))(\?.*)?$", "$1.html$2", skipRemainingRules: true);

app.UseRewriter(rewriteOptions);

// Protect admin HTML pages with JWT stored in auth_token cookie
var publicHtmlPages = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
{
    "/",
    "/index.html",
    "/login.html",
    "/register.html"
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

app.UseDefaultFiles(); // Enable index.html as default landing
app.UseStaticFiles(); // Serve static files from wwwroot

// Serve runtime uploads from outside wwwroot to avoid StaticWebAssets build-time locks
var uploadsRoot = Path.Combine(app.Environment.ContentRootPath, "Uploads");
Directory.CreateDirectory(uploadsRoot);
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(uploadsRoot),
    RequestPath = "/uploads"
});
app.UseCors("AllowFrontend");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();
