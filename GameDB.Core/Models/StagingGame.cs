using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace GameDB.Core.Models;

/// <summary>
/// Staging table for normalized game data before importing to main tables.
/// This is the second stage of the import pipeline.
/// </summary>
public class StagingGame
{
    [Key]
    public int StagingGameId { get; set; }
    
    /// <summary>
    /// Normalized title for matching across different stores
    /// </summary>
    [Required]
    public string NormalizedTitle { get; set; } = null!;
    
    /// <summary>
    /// IGDB game ID (primary source of metadata)
    /// </summary>
    public string? IgdbId { get; set; }
    
    /// <summary>
    /// Steam App ID
    /// </summary>
    public string? SteamAppId { get; set; }
    
    /// <summary>
    /// GOG game ID
    /// </summary>
    public string? GogId { get; set; }
    
    // Normalized data from IGDB
    public string Title { get; set; } = null!;
    public string? Description { get; set; }
    public DateOnly? ReleaseDate { get; set; }
    public string? Developer { get; set; }
    public string? Publisher { get; set; }
    
    /// <summary>
    /// JSON array of genre names
    /// </summary>
    public string? GenresJson { get; set; }
    
    [JsonIgnore]
    public List<string> Genres
    {
        get => string.IsNullOrEmpty(GenresJson) 
            ? new List<string>() 
            : System.Text.Json.JsonSerializer.Deserialize<List<string>>(GenresJson) ?? new List<string>();
        set => GenresJson = System.Text.Json.JsonSerializer.Serialize(value);
    }
    
    // Price data from Steam
    public decimal? SteamPrice { get; set; }
    public short? SteamDiscount { get; set; }
    
    // Price data from GOG
    public decimal? GogPrice { get; set; }
    public short? GogDiscount { get; set; }
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// Whether this staged game has been imported to main tables
    /// </summary>
    public bool IsProcessed { get; set; }
    
    /// <summary>
    /// ID of the imported Game entity (after processing)
    /// </summary>
    public int? GameId { get; set; }
    
    [JsonIgnore]
    public Game? Game { get; set; }
}
