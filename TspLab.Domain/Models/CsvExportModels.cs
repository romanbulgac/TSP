using System.ComponentModel;

namespace TspLab.Domain.Models;

/// <summary>
/// Model for CSV export data - records algorithmic step data for analysis
/// </summary>
public class AlgorithmStepData
{
    [DisplayName("RunId")]
    public string RunId { get; set; } = "";
    
    [DisplayName("Algorithm")]
    public string AlgorithmName { get; set; } = "";
    
    [DisplayName("Problem")]
    public string ProblemName { get; set; } = "";
    
    [DisplayName("Configuration")]
    public string Configuration { get; set; } = "";
    
    [DisplayName("Step")]
    public int Step { get; set; }
    
    [DisplayName("BestFitness")]
    public double BestFitness { get; set; }
    
    [DisplayName("AverageFitness")]
    public double AverageFitness { get; set; }
    
    [DisplayName("BestDistance")]
    public double BestDistance { get; set; }
    
    [DisplayName("ElapsedTimeMs")]
    public long ElapsedTimeMs { get; set; }
    
    [DisplayName("IsComplete")]
    public bool IsComplete { get; set; }
    
    [DisplayName("Timestamp")]
    public DateTime Timestamp { get; set; }
    
    // Additional metrics specific to algorithm type
    [DisplayName("AdditionalMetrics")]
    public Dictionary<string, object> AdditionalMetrics { get; set; } = new();
}

/// <summary>
/// Benchmark run metadata for CSV export
/// </summary>
public class BenchmarkRunInfo
{
    public string RunId { get; set; } = "";
    public string AlgorithmName { get; set; } = "";
    public string ProblemName { get; set; } = "";
    public string Configuration { get; set; } = "";
    public int RunNumber { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public double FinalDistance { get; set; }
    public int TotalSteps { get; set; }
    public List<AlgorithmStepData> StepData { get; set; } = new();
}

/// <summary>
/// CSV export request configuration
/// </summary>
public class CsvExportRequest
{
    public List<BenchmarkRunInfo> Runs { get; set; } = new();
    public bool IncludeStepData { get; set; } = true;
    public bool IncludeSummary { get; set; } = true;
    public bool IncludeConfiguration { get; set; } = true;
    public string ExportType { get; set; } = "detailed"; // "detailed", "summary", "convergence"
}

/// <summary>
/// Model for data collection summary - used in Blazor UI
/// </summary>
public class DataCollectionSummary
{
    public int ActiveRuns { get; set; }
    public int CompletedRuns { get; set; }
    public int TotalStepsCollected { get; set; }
    public List<string> Algorithms { get; set; } = new();
    public List<string> Problems { get; set; } = new();
}
