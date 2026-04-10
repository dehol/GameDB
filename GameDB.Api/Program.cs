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

// Services
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IGameService, GameService>();
builder.Services.AddScoped<IWishlistService, WishlistService>();
builder.Services.AddScoped<IPriceSyncService, PriceSyncService>();
builder.Services.AddScoped<IProfileService, ProfileService>();
builder.Services.AddScoped<IAlertService, AlertService>();
builder.Services.AddScoped<ILibraryService, LibraryService>();

// Import pipeline services
builder.Services.AddScoped<IRawDataCollector, RawDataCollector>();
builder.Services.AddScoped<IDataStagingService, DataStagingService>();
builder.Services.AddScoped<IDataImportService, DataImportService>();

// Reference Data Cache (scoped for each import operation)
builder.Services.AddScoped<ReferenceDataCache>();

// Pipeline Service (singleton for channel)
builder.Services.AddSingleton<Channel<int>>(sp => 
    Channel.CreateBounded<int>(new BoundedChannelOptions(1)
    {
        FullMode = BoundedChannelFullMode.Wait
    }));
builder.Services.AddSingleton<IPipelineService, ImportPipelineService>();

// Background Service for Import Pipeline
builder.Services.AddHostedService<GameImportWorker>();

builder.Services.AddHttpClient<RawgApiService>(client =>
{
    client.DefaultRequestHeaders.Add("User-Agent", "GameDB/1.0");
    client.Timeout = TimeSpan.FromSeconds(60);
});

// Register IRawgApiService
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
