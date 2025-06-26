using System.Globalization;
using System.IO.Compression;
using System.Text;
using TspLab.Domain.Models;

namespace TspLab.Application.Services;

/// <summary>
/// Service for exporting benchmark data to CSV format
/// </summary>
public sealed class CsvExportService
{
    /// <summary>
    /// Exports algorithm step data to CSV format
    /// </summary>
    /// <param name="stepData">List of algorithm step data</param>
    /// <param name="includeHeaders">Whether to include headers in CSV</param>
    /// <returns>CSV content as string</returns>
    public string ExportStepDataToCsv(List<AlgorithmStepData> stepData, bool includeHeaders = true)
    {
        if (!stepData.Any())
            return "";

        var csv = new StringBuilder();

        // Add headers if requested
        if (includeHeaders)
        {
            csv.AppendLine("RunId,Algorithm,Problem,Configuration,Step,BestFitness,AverageFitness,BestDistance,ElapsedTimeMs,IsComplete,Timestamp,AdditionalMetrics");
        }

        // Add data rows
        foreach (var step in stepData)
        {
            var additionalMetrics = string.Join(";", step.AdditionalMetrics.Select(kv => $"{kv.Key}:{kv.Value}"));
            
            csv.AppendLine($"{EscapeCsvField(step.RunId)}," +
                          $"{EscapeCsvField(step.AlgorithmName)}," +
                          $"{EscapeCsvField(step.ProblemName)}," +
                          $"{EscapeCsvField(step.Configuration)}," +
                          $"{step.Step}," +
                          $"{step.BestFitness.ToString("F6", CultureInfo.InvariantCulture)}," +
                          $"{step.AverageFitness.ToString("F6", CultureInfo.InvariantCulture)}," +
                          $"{step.BestDistance.ToString("F6", CultureInfo.InvariantCulture)}," +
                          $"{step.ElapsedTimeMs}," +
                          $"{step.IsComplete}," +
                          $"{step.Timestamp:yyyy-MM-dd HH:mm:ss.fff}," +
                          $"{EscapeCsvField(additionalMetrics)}");
        }

        return csv.ToString();
    }

    /// <summary>
    /// Exports benchmark run summary to CSV format
    /// </summary>
    /// <param name="runs">List of benchmark runs</param>
    /// <param name="includeHeaders">Whether to include headers in CSV</param>
    /// <returns>CSV content as string</returns>
    public string ExportRunSummaryToCsv(List<BenchmarkRunInfo> runs, bool includeHeaders = true)
    {
        if (!runs.Any())
            return "";

        var csv = new StringBuilder();

        // Add headers if requested
        if (includeHeaders)
        {
            csv.AppendLine("RunId,Algorithm,Problem,Configuration,RunNumber,StartTime,EndTime,DurationMs,FinalDistance,TotalSteps,ConvergenceRate");
        }

        // Add data rows
        foreach (var run in runs)
        {
            var durationMs = (run.EndTime - run.StartTime).TotalMilliseconds;
            var convergenceRate = CalculateConvergenceRate(run.StepData);
            
            csv.AppendLine($"{EscapeCsvField(run.RunId)}," +
                          $"{EscapeCsvField(run.AlgorithmName)}," +
                          $"{EscapeCsvField(run.ProblemName)}," +
                          $"{EscapeCsvField(run.Configuration)}," +
                          $"{run.RunNumber}," +
                          $"{run.StartTime:yyyy-MM-dd HH:mm:ss.fff}," +
                          $"{run.EndTime:yyyy-MM-dd HH:mm:ss.fff}," +
                          $"{durationMs.ToString("F0", CultureInfo.InvariantCulture)}," +
                          $"{run.FinalDistance.ToString("F6", CultureInfo.InvariantCulture)}," +
                          $"{run.TotalSteps}," +
                          $"{convergenceRate.ToString("F6", CultureInfo.InvariantCulture)}");
        }

        return csv.ToString();
    }

    /// <summary>
    /// Exports convergence data specifically optimized for plotting
    /// </summary>
    /// <param name="runs">List of benchmark runs</param>
    /// <param name="includeHeaders">Whether to include headers in CSV</param>
    /// <returns>CSV content as string</returns>
    public string ExportConvergenceDataToCsv(List<BenchmarkRunInfo> runs, bool includeHeaders = true)
    {
        if (!runs.Any())
            return "";

        var csv = new StringBuilder();

        // Add headers if requested
        if (includeHeaders)
        {
            csv.AppendLine("RunId,Algorithm,Problem,Configuration,Step,BestDistance,RelativeImprovement,ElapsedTimeMs");
        }

        // Add convergence data for each run
        foreach (var run in runs)
        {
            if (!run.StepData.Any()) continue;

            var initialDistance = run.StepData.First().BestDistance;
            
            foreach (var step in run.StepData)
            {
                var relativeImprovement = initialDistance > 0 ? 
                    (initialDistance - step.BestDistance) / initialDistance : 0;
                
                csv.AppendLine($"{EscapeCsvField(run.RunId)}," +
                              $"{EscapeCsvField(run.AlgorithmName)}," +
                              $"{EscapeCsvField(run.ProblemName)}," +
                              $"{EscapeCsvField(run.Configuration)}," +
                              $"{step.Step}," +
                              $"{step.BestDistance.ToString("F6", CultureInfo.InvariantCulture)}," +
                              $"{relativeImprovement.ToString("F6", CultureInfo.InvariantCulture)}," +
                              $"{step.ElapsedTimeMs}");
            }
        }

        return csv.ToString();
    }

    /// <summary>
    /// Creates individual CSV files for each benchmark run
    /// </summary>
    /// <param name="exportRequest">Export configuration and data</param>
    /// <returns>ZIP file content with individual CSV files</returns>
    public byte[] CreateIndividualRunExport(CsvExportRequest exportRequest)
    {
        using var memoryStream = new MemoryStream();
        using var archive = new ZipArchive(memoryStream, ZipArchiveMode.Create);

        // Create individual CSV file for each run
        foreach (var run in exportRequest.Runs)
        {
            // Generate individual CSV content for this run
            var individualCsv = ExportIndividualRunToCsv(run);
            
            // Create descriptive filename
            var fileName = GenerateIndividualRunFilename(run);
            AddFileToZip(archive, fileName, individualCsv);
        }

        // Create summary index file
        var indexCsv = CreateRunIndexCsv(exportRequest.Runs);
        AddFileToZip(archive, "00_RUN_INDEX.csv", indexCsv);

        // Create README with explanation
        var readme = CreateIndividualRunsReadme(exportRequest.Runs.Count);
        AddFileToZip(archive, "README.txt", readme);

        archive.Dispose();
        return memoryStream.ToArray();
    }

    /// <summary>
    /// Exports data for a single benchmark run
    /// </summary>
    /// <param name="run">Single benchmark run data</param>
    /// <returns>CSV content for this run</returns>
    public string ExportIndividualRunToCsv(BenchmarkRunInfo run)
    {
        var csv = new StringBuilder();

        // Add metadata header
        csv.AppendLine("# TSP Benchmark Individual Run Data");
        csv.AppendLine($"# Run ID: {run.RunId}");
        csv.AppendLine($"# Algorithm: {run.AlgorithmName}");
        csv.AppendLine($"# Problem: {run.ProblemName}");
        csv.AppendLine($"# Configuration: {run.Configuration}");
        csv.AppendLine($"# Run Number: {run.RunNumber}");
        csv.AppendLine($"# Start Time: {run.StartTime:yyyy-MM-dd HH:mm:ss}");
        csv.AppendLine($"# End Time: {run.EndTime:yyyy-MM-dd HH:mm:ss}");
        csv.AppendLine($"# Duration: {(run.EndTime - run.StartTime).TotalSeconds:F2} seconds");
        csv.AppendLine($"# Final Distance: {run.FinalDistance:F6}");
        csv.AppendLine($"# Total Steps: {run.TotalSteps}");
        csv.AppendLine("#");

        // Add column headers
        csv.AppendLine("Step,BestFitness,AverageFitness,BestDistance,ElapsedTimeMs,IsComplete,Timestamp,AdditionalMetrics");

        // Add step data
        foreach (var step in run.StepData)
        {
            var additionalMetrics = string.Join(";", step.AdditionalMetrics.Select(kv => $"{kv.Key}:{kv.Value}"));
            
            csv.AppendLine($"{step.Step}," +
                          $"{step.BestFitness.ToString("F6", CultureInfo.InvariantCulture)}," +
                          $"{step.AverageFitness.ToString("F6", CultureInfo.InvariantCulture)}," +
                          $"{step.BestDistance.ToString("F6", CultureInfo.InvariantCulture)}," +
                          $"{step.ElapsedTimeMs}," +
                          $"{step.IsComplete}," +
                          $"{step.Timestamp:yyyy-MM-dd HH:mm:ss.fff}," +
                          $"{EscapeCsvField(additionalMetrics)}");
        }

        return csv.ToString();
    }

    /// <summary>
    /// Generates a descriptive filename for individual run
    /// </summary>
    /// <param name="run">Benchmark run info</param>
    /// <returns>Generated filename</returns>
    public string GenerateIndividualRunFilename(BenchmarkRunInfo run)
    {
        var algorithm = SanitizeFileName(run.AlgorithmName);
        var problem = SanitizeFileName(run.ProblemName);
        var timestamp = run.StartTime.ToString("yyyyMMdd_HHmmss");
        
        // Extract key configuration parameters for filename
        var configSummary = ExtractConfigSummary(run.Configuration);
        
        return $"{algorithm}_{problem}_{configSummary}_Run{run.RunNumber:D2}_{timestamp}.csv";
    }

    /// <summary>
    /// Creates an index CSV file listing all individual runs
    /// </summary>
    /// <param name="runs">List of all runs</param>
    /// <returns>Index CSV content</returns>
    private string CreateRunIndexCsv(List<BenchmarkRunInfo> runs)
    {
        var csv = new StringBuilder();
        csv.AppendLine("FileName,RunId,Algorithm,Problem,Configuration,RunNumber,StartTime,FinalDistance,Duration,TotalSteps");

        foreach (var run in runs)
        {
            var fileName = GenerateIndividualRunFilename(run);
            var duration = (run.EndTime - run.StartTime).TotalSeconds;
            
            csv.AppendLine($"{EscapeCsvField(fileName)}," +
                          $"{EscapeCsvField(run.RunId)}," +
                          $"{EscapeCsvField(run.AlgorithmName)}," +
                          $"{EscapeCsvField(run.ProblemName)}," +
                          $"{EscapeCsvField(run.Configuration)}," +
                          $"{run.RunNumber}," +
                          $"{run.StartTime:yyyy-MM-dd HH:mm:ss}," +
                          $"{run.FinalDistance:F6}," +
                          $"{duration:F2}," +
                          $"{run.TotalSteps}");
        }

        return csv.ToString();
    }

    /// <summary>
    /// Extracts key parameters from configuration string for filename
    /// </summary>
    /// <param name="configuration">Configuration string</param>
    /// <returns>Shortened config summary</returns>
    private string ExtractConfigSummary(string configuration)
    {
        if (string.IsNullOrEmpty(configuration))
            return "Default";

        var parts = configuration.Split(',').Take(2); // Take first 2 key parameters
        var summary = string.Join("_", parts.Select(p => p.Replace(":", "").Replace(".", "")));
        
        return SanitizeFileName(summary).Substring(0, Math.Min(20, summary.Length));
    }

    /// <summary>
    /// Creates README for individual runs export
    /// </summary>
    /// <param name="totalRuns">Total number of runs</param>
    /// <returns>README content</returns>
    private string CreateIndividualRunsReadme(int totalRuns)
    {
        return $@"TSP Benchmark Individual Run Export
====================================

This ZIP file contains {totalRuns} individual CSV files, one for each benchmark run.

File Naming Convention:
[Algorithm]_[Problem]_[ConfigSummary]_Run[XX]_[Timestamp].csv

Examples:
- GeneticAlgorithm_SmallRandom_Pop50Gen200_Run01_20250619_120000.csv
- AntColonyOptimization_MediumCircular_Ants50Iter300_Run01_20250619_120100.csv
- SimulatedAnnealing_LargeRandom_InitTemp10000Cool0995_Run01_20250619_120200.csv

File Structure:
Each CSV file contains:
1. Header comments with run metadata
2. Step-by-step algorithm data
3. Columns: Step, BestFitness, AverageFitness, BestDistance, ElapsedTimeMs, IsComplete, Timestamp, AdditionalMetrics

Special Files:
- 00_RUN_INDEX.csv: Summary of all runs with filenames
- README.txt: This file

Usage for Analysis:
1. Load individual files for detailed step-by-step analysis
2. Use RUN_INDEX.csv to identify files of interest
3. Compare runs with same algorithm/problem but different configurations
4. Create convergence plots from individual run data

Generated on: {DateTime.Now:yyyy-MM-dd HH:mm:ss}";
    }

    /// <summary>
    /// Creates a complete ZIP export with all data types
    /// </summary>
    /// <param name="exportRequest">Export configuration and data</param>
    /// <returns>ZIP file content</returns>
    public byte[] CreateFullExport(CsvExportRequest exportRequest)
    {
        using var memoryStream = new MemoryStream();
        using var archive = new ZipArchive(memoryStream, ZipArchiveMode.Create);

        // Export step data
        if (exportRequest.IncludeStepData)
        {
            var allStepData = exportRequest.Runs.SelectMany(r => r.StepData).ToList();
            var stepDataCsv = ExportStepDataToCsv(allStepData);
            AddFileToZip(archive, "detailed_step_data.csv", stepDataCsv);

            // Group by algorithm for separate analysis
            var algorithmGroups = exportRequest.Runs.GroupBy(r => r.AlgorithmName);
            foreach (var group in algorithmGroups)
            {
                var algorithmStepData = group.SelectMany(r => r.StepData).ToList();
                var algorithmCsv = ExportStepDataToCsv(algorithmStepData);
                var fileName = $"step_data_{SanitizeFileName(group.Key)}.csv";
                AddFileToZip(archive, fileName, algorithmCsv);
            }
        }

        // Export summary data
        if (exportRequest.IncludeSummary)
        {
            var summaryCsv = ExportRunSummaryToCsv(exportRequest.Runs);
            AddFileToZip(archive, "run_summary.csv", summaryCsv);
        }

        // Export convergence data
        var convergenceCsv = ExportConvergenceDataToCsv(exportRequest.Runs);
        AddFileToZip(archive, "convergence_data.csv", convergenceCsv);

        // Export configuration details
        if (exportRequest.IncludeConfiguration)
        {
            var configCsv = ExportConfigurationDataToCsv(exportRequest.Runs);
            AddFileToZip(archive, "configuration_details.csv", configCsv);
        }

        // Create README with explanation
        var readme = CreateReadmeContent();
        AddFileToZip(archive, "README.txt", readme);

        archive.Dispose();
        return memoryStream.ToArray();
    }

    /// <summary>
    /// Generates a unique filename for the export based on timestamp and content
    /// </summary>
    /// <param name="prefix">Filename prefix</param>
    /// <param name="extension">File extension (without dot)</param>
    /// <returns>Generated filename</returns>
    public string GenerateExportFilename(string prefix = "tsp_benchmark", string extension = "csv")
    {
        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        return $"{prefix}_{timestamp}.{extension}";
    }

    /// <summary>
    /// Sanitizes a string to be used as a filename by removing invalid characters
    /// </summary>
    /// <param name="fileName">Original filename</param>
    /// <returns>Sanitized filename safe for file system</returns>
    private static string SanitizeFileName(string fileName)
    {
        if (string.IsNullOrEmpty(fileName))
            return "Unknown";

        var invalidChars = Path.GetInvalidFileNameChars();
        var sanitized = string.Join("_", fileName.Split(invalidChars, StringSplitOptions.RemoveEmptyEntries));
        
        // Ensure it's not too long and remove spaces
        sanitized = sanitized.Replace(" ", "_").Trim();
        if (sanitized.Length > 50)
            sanitized = sanitized.Substring(0, 50);
            
        return string.IsNullOrEmpty(sanitized) ? "Unknown" : sanitized;
    }

    /// <summary>
    /// Adds a text file to a ZIP archive
    /// </summary>
    /// <param name="archive">ZIP archive to add file to</param>
    /// <param name="fileName">Name of the file in the archive</param>
    /// <param name="content">Text content of the file</param>
    private static void AddFileToZip(ZipArchive archive, string fileName, string content)
    {
        var entry = archive.CreateEntry(fileName);
        using var stream = entry.Open();
        using var writer = new StreamWriter(stream, Encoding.UTF8);
        writer.Write(content);
    }

    /// <summary>
    /// Escapes a CSV field to handle commas, quotes, and newlines
    /// </summary>
    /// <param name="field">Field value to escape</param>
    /// <returns>Escaped field value</returns>
    private static string EscapeCsvField(string field)
    {
        if (string.IsNullOrEmpty(field))
            return "";

        // Escape commas, quotes, and newlines
        if (field.Contains(',') || field.Contains('"') || field.Contains('\n') || field.Contains('\r'))
        {
            return $"\"{field.Replace("\"", "\"\"")}\"";
        }

        return field;
    }

    /// <summary>
    /// Calculates convergence rate for a set of step data
    /// </summary>
    /// <param name="stepData">Algorithm step data</param>
    /// <returns>Convergence rate</returns>
    private double CalculateConvergenceRate(List<AlgorithmStepData> stepData)
    {
        if (stepData.Count < 2) return 0;

        var firstDistance = stepData.First().BestDistance;
        var lastDistance = stepData.Last().BestDistance;
        var totalSteps = stepData.Count;

        return firstDistance > 0 ? (firstDistance - lastDistance) / (firstDistance * totalSteps) : 0;
    }

    /// <summary>
    /// Creates README content for the export
    /// </summary>
    /// <returns>README text content</returns>
    private static string CreateReadmeContent()
    {
        return @"TSP Benchmark Export Files
==========================

This ZIP file contains detailed data from TSP algorithm benchmarks.

Files included:
- detailed_step_data.csv: Complete step-by-step data for all algorithms
- step_data_[algorithm].csv: Step data grouped by algorithm type
- run_summary.csv: Summary statistics for each benchmark run
- convergence_data.csv: Convergence analysis data optimized for plotting
- configuration_details.csv: Parameter configurations for each run

Data Fields:
- RunId: Unique identifier for each benchmark run
- Algorithm: Name of the TSP algorithm used
- Problem: Test problem name
- Configuration: Algorithm configuration parameters
- Step: Generation/iteration number
- BestFitness: Best fitness value at this step
- AverageFitness: Average population fitness at this step
- BestDistance: Best tour distance found so far
- ElapsedTimeMs: Time elapsed since algorithm start (milliseconds)
- IsComplete: Whether the algorithm has finished

Usage Tips:
1. Use convergence_data.csv for creating convergence plots
2. Use run_summary.csv for statistical analysis and comparison
3. Use detailed_step_data.csv for in-depth algorithm behavior analysis
4. Filter data by Algorithm or Problem columns for focused analysis

Generated on: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
    }

    /// <summary>
    /// Exports configuration data to CSV format
    /// </summary>
    /// <param name="runs">List of benchmark runs</param>
    /// <returns>CSV content with configuration details</returns>
    private string ExportConfigurationDataToCsv(List<BenchmarkRunInfo> runs)
    {
        if (!runs.Any())
            return "";

        var csv = new StringBuilder();
        csv.AppendLine("RunId,Algorithm,Problem,Configuration,RunNumber,Parameter,Value");

        foreach (var run in runs)
        {
            // Parse configuration string (assuming format like "Pop:50,Gen:200,Mut:0.02")
            var configPairs = run.Configuration.Split(',');
            foreach (var pair in configPairs)
            {
                var parts = pair.Split(':');
                if (parts.Length == 2)
                {
                    csv.AppendLine($"{EscapeCsvField(run.RunId)}," +
                                  $"{EscapeCsvField(run.AlgorithmName)}," +
                                  $"{EscapeCsvField(run.ProblemName)}," +
                                  $"{EscapeCsvField(run.Configuration)}," +
                                  $"{run.RunNumber}," +
                                  $"{EscapeCsvField(parts[0].Trim())}," +
                                  $"{EscapeCsvField(parts[1].Trim())}");
                }
            }
        }

        return csv.ToString();
    }
}
