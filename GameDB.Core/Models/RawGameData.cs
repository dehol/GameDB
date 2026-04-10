using System.ComponentModel.DataAnnotations;

namespace GameDB.Core.Models;

/// <summary>
/// Stores raw JSON data fetched from external APIs before processing.
/// This is the first stage of the import pipeline.
/// </summary>
public class RawGameData
{
    [Key]
    public int RawGameDataId { get; set; }
    
    /// <summary>
    /// Source of the data: "IGDB", "Steam", "GOG", "EGS"
    /// </summary>
    [Required]
    public string Source { get; set; } = null!;
    
    /// <summary>
    /// External ID from the source (e.g., IGDB ID, Steam AppId)
    /// </summary>
    [Required]
    public string ExternalId { get; set; } = null!;
    
    /// <summary>
    /// Raw JSON data from the API response
    /// </summary>
    [Required]
    public string RawJson { get; set; } = null!;
    
    /// <summary>
    /// When this data was fetched from the API
    /// </summary>
    public DateTime FetchedAt { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// Whether this data has been processed into StagingGame
    /// </summary>
    public bool Processed { get; set; }
    
    /// <summary>
    /// When this data was processed
    /// </summary>
    public DateTime? ProcessedAt { get; set; }
    
    /// <summary>
    /// Number of times processing has been attempted
    /// </summary>
    public int ProcessingAttempts { get; set; }
    
    /// <summary>
    /// Error message if processing failed
    /// </summary>
    public string? ProcessingError { get; set; }
}
