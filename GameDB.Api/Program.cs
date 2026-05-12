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
using System.Text.Json.Serialization;

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
builder.Services.Configure<ShopApiSettings>(
    builder.Configuration.GetSection("ShopApis"));

builder.Services.AddSingleton(sp =>
    sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<IgdbSettings>>().Value);
builder.Services.AddSingleton(sp =>
    sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<ImportSettings>>().Value);

// Services
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<GameService>();
builder.Services.AddScoped<WishlistService>();
builder.Services.AddScoped<PriceSyncService>();
builder.Services.AddScoped<ProfileService>();
builder.Services.AddScoped<AlertService>();
builder.Services.AddScoped<NotificationService>();
builder.Services.AddScoped<LibraryService>();
builder.Services.AddScoped<GameImportService>();
builder.Services.AddScoped<ReferenceDataCache>();
builder.Services.AddScoped<ISteamApiService, SteamApiService>();
builder.Services.AddScoped<IGogApiService, GogApiService>();
builder.Services.AddScoped<IEpicGamesApiService, EpicGamesApiService>();
builder.Services.AddScoped<IWishlistImportService, WishlistImportService>();
builder.Services.AddScoped<IAuditService, AuditService>();
builder.Services.AddScoped<IDataProvider, SteamDataProvider>();
builder.Services.AddScoped<IDataProvider, GogDataProvider>();
builder.Services.AddScoped<IDataProvider, EgsDataProvider>();

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
builder.Services.AddSingleton<WishlistImportBackgroundService>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<WishlistImportBackgroundService>());

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
