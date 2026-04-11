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

// DB
builder.Services.AddDbContext<AppDbContext>(opt =>
    opt.UseNpgsql(builder.Configuration.GetConnectionString("Default")));

// Configuration
builder.Services.Configure<RawgSettings>(
    builder.Configuration.GetSection("Rawg"));
builder.Services.Configure<ImportSettings>(
    builder.Configuration.GetSection("Import"));

// Register configuration as singleton for easy access
builder.Services.AddSingleton(sp => 
    sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<ImportSettings>>().Value);
builder.Services.AddSingleton(sp => 
    sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<RawgSettings>>().Value);

// Services (using concrete classes, not interfaces)
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<GameService>();
builder.Services.AddScoped<WishlistService>();
builder.Services.AddScoped<PriceSyncService>();
builder.Services.AddScoped<ProfileService>();
builder.Services.AddScoped<AlertService>();
builder.Services.AddScoped<LibraryService>();

// Import service (unified)
builder.Services.AddScoped<GameImportService>();

// Reference Data Cache (scoped for each import operation)
builder.Services.AddScoped<ReferenceDataCache>();

// Pipeline Service (singleton for channel) - keep interface for singleton pattern
builder.Services.AddSingleton<Channel<int>>(sp => 
    Channel.CreateBounded<int>(new BoundedChannelOptions(1)
    {
        FullMode = BoundedChannelFullMode.Wait
    }));
builder.Services.AddSingleton<IPipelineService, ImportPipelineService>();

// Background Service for Import Pipeline
builder.Services.AddHostedService<GameImportWorker>();

// RAWG API Client (keep interface for HTTP client factory pattern)
builder.Services.AddHttpClient<RawgApiService>(client =>
{
    client.DefaultRequestHeaders.Add("User-Agent", "GameDB/1.0");
    client.Timeout = TimeSpan.FromSeconds(60);
});
builder.Services.AddScoped<IRawgApiService>(sp => sp.GetRequiredService<RawgApiService>());

// JWT Auth
var jwtKey = builder.Configuration["Jwt:Key"]!;
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(opt =>
    {
        opt.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
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
        .SetIsOriginAllowed(origin => {
            if (string.IsNullOrEmpty(origin)) return true;
            return origin.Contains("localhost");
        })
        .AllowAnyHeader()
        .AllowAnyMethod()
        .AllowCredentials()));

var app = builder.Build();

app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

// Health check endpoint
app.MapHealthChecks("/health");

app.Run();
