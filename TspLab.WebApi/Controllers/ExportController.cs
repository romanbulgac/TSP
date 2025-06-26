using Microsoft.AspNetCore.Mvc;
using TspLab.Application.Services;
using TspLab.Domain.Models;

namespace TspLab.WebApi.Controllers;

/// <summary>
/// Controller for exporting benchmark data to various formats
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class ExportController : ControllerBase
{
    private readonly BenchmarkDataCollector _dataCollector;
    private readonly CsvExportService _csvExportService;
    private readonly ILogger<ExportController> _logger;

    public ExportController(
        BenchmarkDataCollector dataCollector,
        CsvExportService csvExportService,
        ILogger<ExportController> logger)
    {
        _dataCollector = dataCollector ?? throw new ArgumentNullException(nameof(dataCollector));
        _csvExportService = csvExportService ?? throw new ArgumentNullException(nameof(csvExportService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Downloads detailed step data as CSV
    /// </summary>
    /// <param name="includeActive">Include currently running benchmarks</param>
    /// <returns>CSV file download</returns>
    [HttpGet("csv/detailed")]
    public IActionResult DownloadDetailedCsv([FromQuery] bool includeActive = false)
    {
        try
        {
            var csvContent = _dataCollector.ExportData("detailed", includeActive) as string;
            
            if (string.IsNullOrEmpty(csvContent))
            {
                return NotFound("No benchmark data available for export");
            }

            var fileName = _csvExportService.GenerateExportFilename("detailed_steps", "csv");
            var bytes = System.Text.Encoding.UTF8.GetBytes(csvContent);
            
            return File(bytes, "text/csv", fileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error exporting detailed CSV");
            return StatusCode(500, "Error generating CSV export");
        }
    }

    /// <summary>
    /// Downloads run summary as CSV
    /// </summary>
    /// <param name="includeActive">Include currently running benchmarks</param>
    /// <returns>CSV file download</returns>
    [HttpGet("csv/summary")]
    public IActionResult DownloadSummaryCsv([FromQuery] bool includeActive = false)
    {
        try
        {
            var csvContent = _dataCollector.ExportData("summary", includeActive) as string;
            
            if (string.IsNullOrEmpty(csvContent))
            {
                return NotFound("No benchmark data available for export");
            }

            var fileName = _csvExportService.GenerateExportFilename("run_summary", "csv");
            var bytes = System.Text.Encoding.UTF8.GetBytes(csvContent);
            
            return File(bytes, "text/csv", fileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error exporting summary CSV");
            return StatusCode(500, "Error generating CSV export");
        }
    }

    /// <summary>
    /// Downloads convergence data optimized for plotting
    /// </summary>
    /// <param name="includeActive">Include currently running benchmarks</param>
    /// <returns>CSV file download</returns>
    [HttpGet("csv/convergence")]
    public IActionResult DownloadConvergenceCsv([FromQuery] bool includeActive = false)
    {
        try
        {
            var csvContent = _dataCollector.ExportData("convergence", includeActive) as string;
            
            if (string.IsNullOrEmpty(csvContent))
            {
                return NotFound("No benchmark data available for export");
            }

            var fileName = _csvExportService.GenerateExportFilename("convergence_data", "csv");
            var bytes = System.Text.Encoding.UTF8.GetBytes(csvContent);
            
            return File(bytes, "text/csv", fileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error exporting convergence CSV");
            return StatusCode(500, "Error generating CSV export");
        }
    }

    /// <summary>
    /// Downloads comprehensive data package as ZIP file
    /// </summary>
    /// <param name="includeActive">Include currently running benchmarks</param>
    /// <returns>ZIP file download with multiple CSV files</returns>
    [HttpGet("zip/comprehensive")]
    public IActionResult DownloadComprehensiveZip([FromQuery] bool includeActive = false)
    {
        try
        {
            var zipBytes = _dataCollector.ExportData("comprehensive", includeActive) as byte[];
            
            if (zipBytes == null || zipBytes.Length == 0)
            {
                return NotFound("No benchmark data available for export");
            }

            var fileName = _csvExportService.GenerateExportFilename("tsp_benchmark_complete", "zip");
            
            return File(zipBytes, "application/zip", fileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error exporting comprehensive ZIP");
            return StatusCode(500, "Error generating ZIP export");
        }
    }

    /// <summary>
    /// Gets summary of available data for export
    /// </summary>
    /// <returns>Summary information about collected data</returns>
    [HttpGet("summary")]
    public IActionResult GetExportSummary()
    {
        try
        {
            var summary = _dataCollector.GetCollectionSummary();
            return Ok(summary);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting export summary");
            return StatusCode(500, "Error getting export summary");
        }
    }

    /// <summary>
    /// Clears all collected benchmark data
    /// </summary>
    /// <returns>Success confirmation</returns>
    [HttpDelete("clear")]
    public IActionResult ClearCollectedData()
    {
        try
        {
            _dataCollector.ClearAllData();
            _logger.LogInformation("Benchmark data cleared by user request");
            return Ok(new { message = "All benchmark data cleared successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error clearing benchmark data");
            return StatusCode(500, "Error clearing benchmark data");
        }
    }

    /// <summary>
    /// Downloads individual CSV files for each run in a ZIP archive
    /// </summary>
    /// <param name="includeActive">Include currently running benchmarks</param>
    /// <returns>ZIP file download with individual CSV files</returns>
    [HttpGet("zip/individual-runs")]
    public IActionResult DownloadIndividualRunsZip([FromQuery] bool includeActive = false)
    {
        try
        {
            var runs = _dataCollector.GetBenchmarkRuns(includeActive);
            
            if (!runs.Any())
            {
                return NotFound("No benchmark data available for export");
            }

            var exportRequest = new Domain.Models.CsvExportRequest
            {
                Runs = runs,
                IncludeStepData = true,
                IncludeSummary = true,
                IncludeConfiguration = true
            };

            var zipBytes = _csvExportService.CreateIndividualRunExport(exportRequest);
            var fileName = _csvExportService.GenerateExportFilename("individual_runs", "zip");
            
            return File(zipBytes, "application/zip", fileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating individual runs ZIP export");
            return StatusCode(500, "Error generating individual runs export");
        }
    }

    /// <summary>
    /// Test endpoint to verify API connectivity
    /// </summary>
    /// <returns>Simple test response</returns>
    [HttpGet("test")]
    public IActionResult Test()
    {
        return Ok(new { message = "Export API is working", timestamp = DateTime.UtcNow });
    }

    /// <summary>
    /// Get data collection statistics for debugging
    /// </summary>
    /// <returns>Statistics about collected data</returns>
    [HttpGet("stats")]
    public IActionResult GetStats()
    {
        try
        {
            var summary = _dataCollector.GetCollectionSummary();
            var runs = _dataCollector.GetBenchmarkRuns(includeActive: true);
            var activeRuns = _dataCollector.GetActiveRuns();
            var completedRuns = _dataCollector.GetCompletedRuns();

            var stats = new
            {
                Summary = summary,
                TotalRuns = runs.Count,
                ActiveRunsCount = activeRuns.Count,
                CompletedRunsCount = completedRuns.Count,
                TotalSteps = runs.Sum(r => r.StepData?.Count ?? 0),
                MemoryEstimate = $"{runs.Sum(r => r.StepData?.Count ?? 0) * 100} bytes (approx)",
                RunDetails = runs.Take(3).Select(r => new {
                    r.RunId,
                    r.AlgorithmName,
                    r.ProblemName,
                    StepsCount = r.StepData?.Count ?? 0,
                    r.StartTime,
                    r.EndTime
                })
            };

            return Ok(stats);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting data collection statistics");
            return StatusCode(500, "Error getting statistics");
        }
    }
}
