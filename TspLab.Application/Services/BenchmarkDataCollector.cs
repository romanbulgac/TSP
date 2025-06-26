using TspLab.Domain.Models;

namespace TspLab.Application.Services;

/// <summary>
/// Service for collecting and managing benchmark data for CSV export
/// </summary>
public sealed class BenchmarkDataCollector
{
    private readonly Dictionary<string, BenchmarkRunInfo> _activeRuns = new();
    private readonly List<BenchmarkRunInfo> _completedRuns = new();
    private readonly CsvExportService _csvExportService;

    public BenchmarkDataCollector(CsvExportService csvExportService)
    {
        _csvExportService = csvExportService ?? throw new ArgumentNullException(nameof(csvExportService));
    }

    /// <summary>
    /// Starts collecting data for a new benchmark run
    /// </summary>
    /// <param name="algorithmName">Name of the algorithm</param>
    /// <param name="problemName">Name of the test problem</param>
    /// <param name="configuration">Algorithm configuration string</param>
    /// <param name="runNumber">Run number within the benchmark</param>
    /// <returns>Unique run ID for tracking</returns>
    public string StartRun(string algorithmName, string problemName, string configuration, int runNumber)
    {
        var runId = GenerateRunId(algorithmName, problemName, runNumber);
        
        var runInfo = new BenchmarkRunInfo
        {
            RunId = runId,
            AlgorithmName = algorithmName,
            ProblemName = problemName,
            Configuration = configuration,
            RunNumber = runNumber,
            StartTime = DateTime.Now,
            StepData = new List<AlgorithmStepData>()
        };

        _activeRuns[runId] = runInfo;
        return runId;
    }

    /// <summary>
    /// Records a step result from genetic algorithm
    /// </summary>
    /// <param name="runId">Run ID to record data for</param>
    /// <param name="result">Genetic algorithm result</param>
    public void RecordGeneticAlgorithmStep(string runId, GeneticAlgorithmResult result)
    {
        if (!_activeRuns.TryGetValue(runId, out var runInfo))
            return;

        var stepData = new AlgorithmStepData
        {
            RunId = runId,
            AlgorithmName = runInfo.AlgorithmName,
            ProblemName = runInfo.ProblemName,
            Configuration = runInfo.Configuration,
            Step = result.Generation,
            BestFitness = result.BestFitness,
            AverageFitness = result.AverageFitness,
            BestDistance = result.BestDistance,
            ElapsedTimeMs = result.ElapsedMilliseconds,
            IsComplete = result.IsComplete,
            Timestamp = DateTime.Now,
            AdditionalMetrics = new Dictionary<string, object>
            {
                ["TourLength"] = result.BestTour?.Length ?? 0
            }
        };

        runInfo.StepData.Add(stepData);

        if (result.IsComplete)
        {
            CompleteRun(runId, result.BestDistance, result.Generation);
        }
    }

    /// <summary>
    /// Records a step result from ant colony optimization
    /// </summary>
    /// <param name="runId">Run ID to record data for</param>
    /// <param name="result">ACO result</param>
    public void RecordAntColonyStep(string runId, AntColonyResult result)
    {
        if (!_activeRuns.TryGetValue(runId, out var runInfo))
            return;

        var stepData = new AlgorithmStepData
        {
            RunId = runId,
            AlgorithmName = runInfo.AlgorithmName,
            ProblemName = runInfo.ProblemName,
            Configuration = runInfo.Configuration,
            Step = result.Iteration,
            BestFitness = CalculateFitnessFromDistance(result.BestDistance),
            AverageFitness = CalculateFitnessFromDistance(result.AverageDistance),
            BestDistance = result.BestDistance,
            ElapsedTimeMs = result.ElapsedMilliseconds,
            IsComplete = result.IsComplete,
            Timestamp = DateTime.Now,
            AdditionalMetrics = new Dictionary<string, object>
            {
                ["IterationBestDistance"] = result.IterationBestDistance,
                ["AverageDistance"] = result.AverageDistance,
                ["StagnationCount"] = result.StagnationCount
            }
        };

        // Add ACO-specific statistics if available
        if (result.Statistics.Any())
        {
            foreach (var stat in result.Statistics)
            {
                stepData.AdditionalMetrics[$"ACO_{stat.Key}"] = stat.Value;
            }
        }

        runInfo.StepData.Add(stepData);

        if (result.IsComplete)
        {
            CompleteRun(runId, result.BestDistance, result.Iteration);
        }
    }

    /// <summary>
    /// Records a step result from simulated annealing
    /// </summary>
    /// <param name="runId">Run ID to record data for</param>
    /// <param name="result">SA result</param>
    public void RecordSimulatedAnnealingStep(string runId, SimulatedAnnealingResult result)
    {
        if (!_activeRuns.TryGetValue(runId, out var runInfo))
            return;

        var stepData = new AlgorithmStepData
        {
            RunId = runId,
            AlgorithmName = runInfo.AlgorithmName,
            ProblemName = runInfo.ProblemName,
            Configuration = runInfo.Configuration,
            Step = result.Iteration,
            BestFitness = CalculateFitnessFromDistance(result.BestDistance),
            AverageFitness = CalculateFitnessFromDistance(result.BestDistance), // SA doesn't have population
            BestDistance = result.BestDistance,
            ElapsedTimeMs = (long)result.ElapsedTime.TotalMilliseconds,
            IsComplete = result.IsComplete,
            Timestamp = DateTime.Now,
            AdditionalMetrics = new Dictionary<string, object>
            {
                ["CurrentTemperature"] = result.CurrentTemperature,
                ["InitialTemperature"] = result.InitialTemperature,
                ["AcceptanceRate"] = result.AcceptanceRate,
                ["TotalAccepted"] = result.TotalAccepted,
                ["TotalRejected"] = result.TotalRejected,
                ["Improvements"] = result.Improvements,
                ["Phase"] = result.Phase
            }
        };

        runInfo.StepData.Add(stepData);

        if (result.IsComplete)
        {
            CompleteRun(runId, result.BestDistance, result.Iteration);
        }
    }

    /// <summary>
    /// Records data for algorithms that don't provide step-by-step updates
    /// </summary>
    /// <param name="runId">Run ID to record data for</param>
    /// <param name="finalDistance">Final tour distance</param>
    /// <param name="executionTimeMs">Total execution time in milliseconds</param>
    public void RecordDirectAlgorithmResult(string runId, double finalDistance, long executionTimeMs)
    {
        if (!_activeRuns.TryGetValue(runId, out var runInfo))
            return;

        var stepData = new AlgorithmStepData
        {
            RunId = runId,
            AlgorithmName = runInfo.AlgorithmName,
            ProblemName = runInfo.ProblemName,
            Configuration = runInfo.Configuration,
            Step = 1, // Single step for direct algorithms
            BestFitness = CalculateFitnessFromDistance(finalDistance),
            AverageFitness = CalculateFitnessFromDistance(finalDistance),
            BestDistance = finalDistance,
            ElapsedTimeMs = executionTimeMs,
            IsComplete = true,
            Timestamp = DateTime.Now,
            AdditionalMetrics = new Dictionary<string, object>
            {
                ["AlgorithmType"] = "Direct"
            }
        };

        runInfo.StepData.Add(stepData);
        CompleteRun(runId, finalDistance, 1);
    }

    /// <summary>
    /// Exports all collected data to CSV format
    /// </summary>
    /// <param name="exportType">Type of export: "detailed", "summary", "convergence", or "comprehensive"</param>
    /// <param name="includeActiveRuns">Whether to include currently running benchmarks</param>
    /// <returns>CSV content or ZIP file bytes depending on export type</returns>
    public object ExportData(string exportType = "comprehensive", bool includeActiveRuns = false)
    {
        var allRuns = new List<BenchmarkRunInfo>(_completedRuns);
        
        if (includeActiveRuns)
        {
            allRuns.AddRange(_activeRuns.Values);
        }

        if (!allRuns.Any())
            return "";

        var exportRequest = new CsvExportRequest
        {
            Runs = allRuns,
            IncludeStepData = true,
            IncludeSummary = true,
            IncludeConfiguration = true,
            ExportType = exportType
        };

        return exportType switch
        {
            "detailed" => _csvExportService.ExportStepDataToCsv(allRuns.SelectMany(r => r.StepData).ToList()),
            "summary" => _csvExportService.ExportRunSummaryToCsv(allRuns),
            "convergence" => _csvExportService.ExportConvergenceDataToCsv(allRuns),
            "comprehensive" => _csvExportService.CreateFullExport(exportRequest),
            "individual-runs" => _csvExportService.CreateIndividualRunExport(exportRequest),
            _ => _csvExportService.ExportStepDataToCsv(allRuns.SelectMany(r => r.StepData).ToList())
        };
    }

    /// <summary>
    /// Gets summary statistics of collected data
    /// </summary>
    /// <returns>Summary information</returns>
    public DataCollectionSummary GetCollectionSummary()
    {
        return new DataCollectionSummary
        {
            ActiveRuns = _activeRuns.Count,
            CompletedRuns = _completedRuns.Count,
            TotalStepsCollected = _completedRuns.Sum(r => r.StepData.Count) + _activeRuns.Values.Sum(r => r.StepData.Count),
            Algorithms = _completedRuns.Select(r => r.AlgorithmName).Distinct().ToList(),
            Problems = _completedRuns.Select(r => r.ProblemName).Distinct().ToList()
        };
    }

    /// <summary>
    /// Clears all collected benchmark data
    /// </summary>
    public void ClearAllData()
    {
        _activeRuns.Clear();
        _completedRuns.Clear();
    }

    /// <summary>
    /// Cancels and removes an active run
    /// </summary>
    /// <param name="runId">Run ID to cancel</param>
    public void CancelRun(string runId)
    {
        _activeRuns.Remove(runId);
    }

    /// <summary>
    /// Gets all benchmark runs for export
    /// </summary>
    /// <param name="includeActive">Whether to include currently active runs</param>
    /// <returns>List of all benchmark runs</returns>
    public List<BenchmarkRunInfo> GetBenchmarkRuns(bool includeActive = false)
    {
        var allRuns = new List<BenchmarkRunInfo>(_completedRuns);
        
        if (includeActive)
        {
            allRuns.AddRange(_activeRuns.Values);
        }
        
        return allRuns.OrderBy(r => r.StartTime).ToList();
    }

    /// <summary>
    /// Gets benchmark run data by run ID
    /// </summary>
    /// <param name="runId">The run ID to retrieve</param>
    /// <returns>Benchmark run info or null if not found</returns>
    public BenchmarkRunInfo? GetRunById(string runId)
    {
        if (_activeRuns.TryGetValue(runId, out var activeRun))
        {
            return activeRun;
        }
        
        return _completedRuns.FirstOrDefault(r => r.RunId == runId);
    }

    /// <summary>
    /// Gets all active runs
    /// </summary>
    /// <returns>Collection of active runs</returns>
    public IReadOnlyCollection<BenchmarkRunInfo> GetActiveRuns()
    {
        return _activeRuns.Values.ToList().AsReadOnly();
    }

    /// <summary>
    /// Gets all completed runs
    /// </summary>
    /// <returns>Collection of completed runs</returns>
    public IReadOnlyCollection<BenchmarkRunInfo> GetCompletedRuns()
    {
        return _completedRuns.AsReadOnly();
    }

    private void CompleteRun(string runId, double finalDistance, int totalSteps)
    {
        if (!_activeRuns.TryGetValue(runId, out var runInfo))
            return;

        runInfo.EndTime = DateTime.Now;
        runInfo.FinalDistance = finalDistance;
        runInfo.TotalSteps = totalSteps;

        _completedRuns.Add(runInfo);
        _activeRuns.Remove(runId);
    }

    private static string GenerateRunId(string algorithmName, string problemName, int runNumber)
    {
        var timestamp = DateTime.Now.ToString("yyyyMMddHHmmss");
        var safeAlgorithm = algorithmName.Replace(" ", "").Replace("/", "");
        var safeProblem = problemName.Replace(" ", "").Replace("/", "");
        return $"{safeAlgorithm}_{safeProblem}_R{runNumber}_{timestamp}";
    }

    private static double CalculateFitnessFromDistance(double distance)
    {
        // Convert distance to fitness (higher is better)
        return distance > 0 ? 1.0 / distance : 0.0;
    }
}
