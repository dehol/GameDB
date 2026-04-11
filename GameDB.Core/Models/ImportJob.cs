using System.ComponentModel.DataAnnotations;

namespace GameDB.Core.Models;

public class ImportJob
{
    [Key]
    public int ImportJobId { get; set; }
    
    /// <summary>
    /// Current status of the import job
    /// </summary>
    public ImportJobStatus Status { get; set; } = ImportJobStatus.Pending;
    
    public string? CurrentPhase { get; set; }
    
    /// <summary>True when job was started from Steam catalog import (not full bulk import).</summary>
    public bool IsSteamCatalogImport { get; set; }
    
    // Steam
    public int SteamTotal { get; set; }
    public int SteamProcessed { get; set; }
    public int SteamImported { get; set; }
    /// <summary>Steam catalog import: rows updated (existing offers/games).</summary>
    public int SteamUpdated { get; set; }
    
    // GOG
    public int GogTotal { get; set; }
    public int GogProcessed { get; set; }
    public int GogImported { get; set; }
    
    // EGS
    public int EgsTotal { get; set; }
    public int EgsProcessed { get; set; }
    public int EgsImported { get; set; }
    
    // Totals
    public int TotalGamesCreated { get; set; }
    public int TotalOffersCreated { get; set; }
    public int ErrorCount { get; set; }
    
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? ErrorMessage { get; set; }
}
