using GameDB.Core.Interfaces;
using GameDB.Core.DTOs.Import;
using GameDB.Core.Models;
using GameDB.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GameDB.Api.Controllers;

/// <summary>
/// Controller for managing game import pipelines.
/// Provides endpoints for starting, monitoring, and cancelling imports.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "admin")]
public class ImportController : ControllerBase
{
    private readonly IPipelineService _pipelineService;
    private readonly AppDbContext _db;

    public ImportController(IPipelineService pipelineService, AppDbContext db)
    {
        _pipelineService = pipelineService;
        _db = db;
    }

    /// <summary>
    /// Starts a new import pipeline. Only one pipeline can run at a time.
    /// </summary>
    /// <returns>Pipeline ID and status endpoint URL</returns>
    /// <response code="200">Pipeline started successfully</response>
    /// <response code="409">A pipeline is already running</response>
    [HttpPost("start")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> StartImport([FromBody] ImportStartRequest? request = null)
    {
        try
        {
            var options = request == null
                ? null
                : new ImportPipelineOptions(
                    Limit: request.Limit,
                    IgdbGameIds: request.IgdbGameIds,
                    OverwriteExisting: request.OverwriteExisting);

            var pipelineId = await _pipelineService.StartImportPipelineAsync(options);
            
            return Ok(new
            {
                pipelineId,
                message = "Import pipeline started successfully",
                statusUrl = $"/api/import/status/{pipelineId}"
            });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new
            {
                error = ex.Message
            });
        }
    }

    /// <summary>
    /// Gets the status of a specific pipeline.
    /// </summary>
    /// <param name="pipelineId">Pipeline ID</param>
    /// <returns>Pipeline status including progress and statistics</returns>
    /// <response code="200">Pipeline status</response>
    /// <response code="404">Pipeline not found</response>
    [HttpGet("status/{pipelineId}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetStatus(int pipelineId)
    {
        var status = await _pipelineService.GetPipelineStatusAsync(pipelineId);
        
        if (status == null)
            return NotFound(new { error = $"Pipeline {pipelineId} not found" });

        return Ok(status);
    }

    /// <summary>
    /// Requests cancellation of a running pipeline.
    /// </summary>
    /// <param name="pipelineId">Pipeline ID to cancel</param>
    /// <returns>Cancellation confirmation</returns>
    [HttpPost("cancel/{pipelineId}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> Cancel(int pipelineId)
    {
        await _pipelineService.CancelPipelineAsync(pipelineId);
        
        return Ok(new
        {
            message = "Cancellation requested",
            pipelineId
        });
    }

    /// <summary>
    /// Gets the currently running pipeline, if any.
    /// </summary>
    /// <returns>Running pipeline status or null</returns>
    [HttpGet("current")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCurrent()
    {
        var runningId = await _pipelineService.GetRunningPipelineIdAsync();
        
        if (runningId == null)
        {
            return Ok(new
            {
                status = "no_running_pipeline",
                message = "No import pipeline is currently running"
            });
        }

        // Reuse GetStatus logic
        return await GetStatus(runningId.Value);
    }

    /// <summary>
    /// Gets history of import jobs with pagination and filtering.
    /// </summary>
    /// <param name="page">Page number (default: 1)</param>
    /// <param name="pageSize">Items per page (default: 20, max: 100)</param>
    /// <param name="status">Filter by status (optional)</param>
    /// <returns>Paginated list of import jobs</returns>
    [HttpGet("jobs")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetJobHistory(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? status = null)
    {
        // Validate parameters
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = _db.ImportJobs.AsQueryable();

        // Apply status filter
        if (!string.IsNullOrEmpty(status) && Enum.TryParse<ImportJobStatus>(status, ignoreCase: true, out var statusEnum))
        {
            query = query.Where(j => j.Status == statusEnum);
        }

        // Get total count
        var total = await query.CountAsync();

        // Get paginated results
        var jobs = await query
            .OrderByDescending(j => j.StartedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(j => new
            {
                j.ImportJobId,
                Status = j.Status.ToString().ToLowerInvariant(),
                j.CurrentPhase,
                j.IsSteamCatalogImport,
                totalGames = j.SteamTotal,
                processedGames = j.SteamProcessed,
                j.TotalGamesCreated,
                j.TotalOffersCreated,
                j.ErrorCount,
                j.StartedAt,
                j.CompletedAt,
                j.ErrorMessage,
                duration = j.CompletedAt.HasValue
                    ? j.CompletedAt.Value.Subtract(j.StartedAt).TotalSeconds
                    : DateTime.UtcNow.Subtract(j.StartedAt).TotalSeconds
            })
            .ToListAsync();

        return Ok(new
        {
            total,
            page,
            pageSize,
            totalPages = (int)Math.Ceiling((double)total / pageSize),
            jobs
        });
    }

    /// <summary>
    /// Gets the latest completed import job.
    /// </summary>
    /// <returns>Latest completed job or null</returns>
    [HttpGet("latest")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetLatest()
    {
        var latestJob = await _db.ImportJobs
            .Where(j => j.Status == ImportJobStatus.Completed)
            .OrderByDescending(j => j.CompletedAt)
            .FirstOrDefaultAsync();

        if (latestJob == null)
        {
            return Ok(new
            {
                status = "never_completed",
                message = "No import jobs have been completed yet"
            });
        }

        // Reuse GetStatus logic
        return await GetStatus(latestJob.ImportJobId);
    }

    /// <summary>
    /// Gets statistics about import operations.
    /// </summary>
    /// <returns>Import statistics</returns>
    [HttpGet("stats")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetStats()
    {
        var totalImports = await _db.ImportJobs.CountAsync();
        var successfulImports = await _db.ImportJobs.CountAsync(j => j.Status == ImportJobStatus.Completed);
        var failedImports = await _db.ImportJobs.CountAsync(j => j.Status == ImportJobStatus.Failed);
        var totalGamesImported = await _db.ImportJobs
            .Where(j => j.Status == ImportJobStatus.Completed)
            .SumAsync(j => j.TotalGamesCreated);
        var totalOffersCreated = await _db.ImportJobs
            .Where(j => j.Status == ImportJobStatus.Completed)
            .SumAsync(j => j.TotalOffersCreated);

        var lastImport = await _db.ImportJobs
            .Where(j => j.Status == ImportJobStatus.Completed)
            .OrderByDescending(j => j.CompletedAt)
            .FirstOrDefaultAsync();

        return Ok(new
        {
            totalImports,
            successfulImports,
            failedImports,
            totalGamesImported,
            totalOffersCreated,
            successRate = totalImports > 0 
                ? Math.Round(100.0 * successfulImports / totalImports, 2) 
                : 0,
            lastImport = lastImport != null
                ? new
                {
                    lastImport.ImportJobId,
                    lastImport.CompletedAt,
                    lastImport.TotalGamesCreated,
                    lastImport.TotalOffersCreated
                }
                : null
        });
    }
}
