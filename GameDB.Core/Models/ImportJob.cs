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
    public DateTime LastUpdatedAt { get; set; } = DateTime.UtcNow;
    public int RetryCount { get; set; }
    
    /// <summary>True when job was started from Steam catalog import (not full bulk import).</summary>
    public bool IsSteamCatalogImport { get; set; }
    
    // Steam
    public int SteamTotal { get; set; }
    public int SteamProcessed { get; set; }
    public int SteamImported { get; set; }
    /// <summary>Steam catalog import: rows updated (existing offers/games).</summary>
    public int SteamUpdated { get; set; }
    public int SteamSkipped { get; set; }
    public int SteamFailed { get; set; }
    
    // GOG
    public int GogTotal { get; set; }
    public int GogProcessed { get; set; }
    public int GogImported { get; set; }
    public int GogUpdated { get; set; }
    public int GogSkipped { get; set; }
    public int GogFailed { get; set; }
    
    // EGS
    public int EgsTotal { get; set; }
    public int EgsProcessed { get; set; }
    public int EgsImported { get; set; }
    public int EgsUpdated { get; set; }
    public int EgsSkipped { get; set; }
    public int EgsFailed { get; set; }
    
    // Totals
    public int TotalGamesCreated { get; set; }
    public int TotalGamesUpdated { get; set; }
    public int TotalGamesSkipped { get; set; }
    public int TotalGamesFailed { get; set; }
    public int TotalOffersCreated { get; set; }
    public int TotalOffersUpdated { get; set; }
    public int TotalOffersSkipped { get; set; }
    public int TotalOffersFailed { get; set; }
    public int ErrorCount { get; set; }

    // Collection/import telemetry
    public int IgdbCollected { get; set; }
    public int EligibleForImport { get; set; }
    public int SkippedAlreadyImported { get; set; }
    public int SkippedNoStoreOffers { get; set; }
    public int SkippedInvalidStoreIds { get; set; }
    public int SkippedDuplicateTitles { get; set; }

    // Requested options
    public int? RequestedLimit { get; set; }
    public string? RequestedIgdbGameIds { get; set; }
    public bool RequestedOverwriteExisting { get; set; }
    
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? ErrorMessage { get; set; }
    public string? WarningMessage { get; set; }
}
