namespace GameDB.Core.Configuration;

public class ImportSettings
{
    /// <summary>
    /// Number of items to process in a single batch
    /// </summary>
    public int BatchSize { get; set; } = 50; // Зменшено зі 100 для кращого балансу пам'яті та продуктивності
    
    /// <summary>
    /// Number of Steam AppIds to fetch in a single API call
    /// </summary>
    public int SteamBatchSize { get; set; } = 50;
    
    /// <summary>
    /// Maximum number of concurrent API calls
    /// </summary>
    public int MaxConcurrentApiCalls { get; set; } = 2; // Зменшено з 10 для стабільності API
    
    /// <summary>
    /// Maximum number of concurrent data processing operations
    /// </summary>
    public int MaxConcurrentProcessing { get; set; } = 10; // Зменшено з 20 для стабільності
    
    /// <summary>
    /// Maximum number of concurrent database operations
    /// </summary>
    public int MaxConcurrentDbOperations { get; set; } = 15; // Зменшено з 30 для балансу
    
    /// <summary>
    /// Maximum number of retry attempts for failed operations
    /// </summary>
    public int MaxRetries { get; set; } = 3;
    
    /// <summary>
    /// Base delay in seconds for exponential backoff retry
    /// </summary>
    public double RetryBaseDelaySeconds { get; set; } = 1.0;
    
    /// <summary>
    /// Job timeout in minutes - jobs running longer than this are considered stuck
    /// </summary>
    public int JobTimeoutMinutes { get; set; } = 120;
    
    /// <summary>
    /// Whether to enable detailed logging for debugging
    /// </summary>
    public bool EnableDetailedLogging { get; set; } = true;
    
    /// <summary>
    /// Number of popular games to fetch from IGDB on each import
    /// </summary>
    public int IgdbPopularGamesLimit { get; set; } = 5000;
    
    /// <summary>
    /// Number of days to keep processed RawGameData before cleanup
    /// </summary>
    public int RawDataRetentionDays { get; set; } = 7;

    /// <summary>
    /// How often (in hours) the PriceSyncWorker runs automatic price sync.
    /// Also used as the staleness threshold: offers synced less than this many hours ago are skipped.
    /// </summary>
    public int PriceSyncIntervalHours { get; set; } = 1;

    /// <summary>
    /// Max number of offers to sync per shop per run. Set to 0 for unlimited.
    /// Use a small number (e.g. 50) for testing.
    /// </summary>
    public int PriceSyncMaxOffers { get; set; } = 0;
}
