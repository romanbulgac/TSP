using BenchmarkDotNet.Running;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Engines;
using BenchmarkDotNet.Exporters;
using BenchmarkDotNet.Exporters.Csv;
using BenchmarkDotNet.Loggers;

namespace TspLab.Tests.Benchmarks;

/// <summary>
/// Helper class for running TSP benchmarks and analyzing results
/// </summary>
public static class BenchmarkRunner
{
    /// <summary>
    /// Runs performance benchmarks for all genetic algorithm combinations
    /// </summary>
    public static void RunPerformanceBenchmarks()
    {
        Console.WriteLine("Starting TSP Genetic Algorithm Performance Benchmarks...");
        Console.WriteLine("Testing all combinations of crossover and mutation operators");
        Console.WriteLine("This will test performance characteristics of each combination");
        Console.WriteLine();

        var config = ManualConfig.Create(DefaultConfig.Instance)
            .AddJob(Job.Default.WithStrategy(RunStrategy.Throughput))
            .AddLogger(ConsoleLogger.Default)
            .AddExporter(MarkdownExporter.GitHub)
            .AddExporter(CsvExporter.Default);

        BenchmarkDotNet.Running.BenchmarkRunner.Run<GeneticAlgorithmBenchmarks>(config);
    }

    /// <summary>
    /// Runs solution quality benchmarks for all genetic algorithm combinations
    /// </summary>
    public static void RunQualityBenchmarks()
    {
        Console.WriteLine("Starting TSP Genetic Algorithm Quality Benchmarks...");
        Console.WriteLine("Testing solution quality for all combinations of crossover and mutation operators");
        Console.WriteLine("This will help identify the best algorithm combinations for different problem sizes");
        Console.WriteLine();

        var config = ManualConfig.Create(DefaultConfig.Instance)
            .AddJob(Job.Default.WithStrategy(RunStrategy.ColdStart).WithIterationCount(3))
            .AddLogger(ConsoleLogger.Default)
            .AddExporter(MarkdownExporter.GitHub)
            .AddExporter(CsvExporter.Default);

        BenchmarkDotNet.Running.BenchmarkRunner.Run<GeneticAlgorithmCombinationsBenchmarks>(config);
    }

    /// <summary>
    /// Runs all benchmarks
    /// </summary>
    public static void RunAllBenchmarks()
    {
        Console.WriteLine("=== TSP Genetic Algorithm Comprehensive Benchmarks ===");
        Console.WriteLine();
        
        Console.WriteLine("Phase 1: Performance Benchmarks");
        Console.WriteLine("Measuring execution speed of genetic operators");
        RunPerformanceBenchmarks();
        
        Console.WriteLine();
        Console.WriteLine("Phase 2: Solution Quality Benchmarks");  
        Console.WriteLine("Measuring solution quality and convergence");
        RunQualityBenchmarks();
        
        Console.WriteLine();
        Console.WriteLine("=== Benchmarks Complete ===");
        Console.WriteLine("Check the generated reports for detailed results.");
    }

    /// <summary>
    /// Prints information about available genetic algorithm combinations
    /// </summary>
    public static void PrintAvailableCombinations()
    {
        var crossovers = new[] { "OrderCrossover", "CycleCrossover", "PartiallyMappedCrossover", "EdgeRecombinationCrossover" };
        var mutations = new[] { "SwapMutation", "InversionMutation", "TwoOptMutation" };
        
        Console.WriteLine("Available Genetic Algorithm Combinations:");
        Console.WriteLine("=========================================");
        Console.WriteLine();
        
        Console.WriteLine("Crossover Operators:");
        foreach (var crossover in crossovers)
        {
            Console.WriteLine($"  - {crossover}");
        }
        
        Console.WriteLine();
        Console.WriteLine("Mutation Operators:");
        foreach (var mutation in mutations)
        {
            Console.WriteLine($"  - {mutation}");
        }
        
        Console.WriteLine();
        Console.WriteLine($"Total Combinations: {crossovers.Length * mutations.Length}");
        Console.WriteLine();
        
        Console.WriteLine("Combinations to be tested:");
        int combinationNumber = 1;
        foreach (var crossover in crossovers)
        {
            foreach (var mutation in mutations)
            {
                Console.WriteLine($"  {combinationNumber:D2}. {crossover} + {mutation}");
                combinationNumber++;
            }
        }
    }
}
