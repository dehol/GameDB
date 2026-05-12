using GameDB.Core.Configuration;
using GameDB.Core.Interfaces;
using GameDB.Infrastructure;
using GameDB.Infrastructure.BackgroundServices;
using GameDB.Infrastructure.Caching;
using GameDB.Infrastructure.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using System.Threading.Channels;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<AuditSaveChangesInterceptor>();

// DB
builder.Services.AddDbContext<AppDbContext>((sp, opt) =>
    opt.UseNpgsql(builder.Configuration.GetConnectionString("Default"))
       .AddInterceptors(sp.GetRequiredService<AuditSaveChangesInterceptor>()));

// Configuration
builder.Services.Configure<IgdbSettings>(
    builder.Configuration.GetSection("Igdb"));
builder.Services.Configure<ImportSettings>(
    builder.Configuration.GetSection("Import"));


builder.Services.AddSingleton(sp =>
    sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<IgdbSettings>>().Value);
builder.Services.AddSingleton(sp =>
    sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<ImportSettings>>().Value);


// Services
builder.Services.AddSingleton<IItadClient, ItadClient>();
builder.Services.AddScoped<PriceSyncService>();
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<GameService>();
builder.Services.AddScoped<WishlistService>();
builder.Services.AddScoped<ProfileService>();
builder.Services.AddScoped<AlertService>();
builder.Services.AddScoped<NotificationService>();
builder.Services.AddScoped<LibraryService>();
builder.Services.AddScoped<GameImportService>();
builder.Services.AddScoped<ShopLinkService>();
builder.Services.AddScoped<ReferenceDataCache>();
builder.Services.AddSingleton<ItadUuidCache>();
builder.Services.Configure<ItadSettings>(builder.Configuration.GetSection("ItadSettings"));
// Read the ItadSettings section from appsettings.json
var itadSettings = builder.Configuration.GetSection("ItadSettings").Get<ItadSettings>() 
                   ?? new ItadSettings();
builder.Services.AddScoped<IAuditService, AuditService>();
builder.Services.AddScoped<IDataProvider, SteamDataProvider>();
builder.Services.AddScoped<IDataProvider, GogDataProvider>();
builder.Services.AddScoped<IDataProvider, EgsDataProvider>();

// Register it so the DI container can inject it into PriceSyncService
builder.Services.AddSingleton(itadSettings);

// Steam Store API client (browser-like headers to avoid 403)
builder.Services.AddHttpClient("Steam", client =>
{
    client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0.0.0 Safari/537.36");
    client.DefaultRequestHeaders.Add("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,*/*;q=0.8");
    client.DefaultRequestHeaders.Add("Accept-Language", "en-US,en;q=0.9");
    client.Timeout = TimeSpan.FromSeconds(30);
});

// IGDB API Client
builder.Services.AddHttpClient<IgdbApiService>(client =>
{
    client.DefaultRequestHeaders.Add("User-Agent", "GameDB/1.0");
    client.Timeout = TimeSpan.FromSeconds(30);
});
builder.Services.AddScoped<IIgdbApiService>(sp => sp.GetRequiredService<IgdbApiService>());

// Pipeline
builder.Services.AddSingleton<Channel<ImportPipelineWorkItem>>(sp =>
    Channel.CreateBounded<ImportPipelineWorkItem>(new BoundedChannelOptions(1)
    {
        FullMode = BoundedChannelFullMode.Wait
    }));
builder.Services.AddSingleton<IPipelineService, ImportPipelineService>();
builder.Services.AddHostedService<GameImportWorker>();
builder.Services.AddHostedService<CoverRefreshWorker>();
builder.Services.AddHostedService<PriceSyncWorker>();

// JWT Auth
var jwtKey = builder.Configuration["Jwt:Key"]!;
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(opt =>
    {
        opt.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer           = true,
            ValidateAudience         = true,
            ValidateLifetime         = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer              = builder.Configuration["Jwt:Issuer"],
            ValidAudience            = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey         = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
        };
    });

builder.Services.AddAuthorization();
builder.Services.AddControllers();

// Health Checks
builder.Services.AddHealthChecks()
    .AddCheck<GameDB.Infrastructure.HealthChecks.ImportWorkerHealthCheck>("import-worker");

// CORS for React
builder.Services.AddCors(opt =>
    opt.AddDefaultPolicy(p => p
        .SetIsOriginAllowed(origin =>
        {
            if (string.IsNullOrEmpty(origin)) return true;
            return origin.Contains("localhost");
        })
        .AllowAnyHeader()
        .AllowAnyMethod()
        .AllowCredentials()));

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();
}

app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHealthChecks("/health");

app.Run();
