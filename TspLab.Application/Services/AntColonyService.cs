using Microsoft.Extensions.Logging;
using System.Runtime.CompilerServices;
using TspLab.Application.Metaheuristics;
using TspLab.Domain.Entities;
using TspLab.Domain.Models;

namespace TspLab.Application.Services;

/// <summary>
/// High-level service for solving TSP using Ant Colony Optimization
/// </summary>
public sealed class AntColonyService
{
    private readonly ILogger<AntColonyService> _logger;

    public AntColonyService(ILogger<AntColonyService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Solves TSP using ACO with streaming results for real-time monitoring
    /// </summary>
    /// <param name="cities">Array of cities to visit</param>
    /// <param name="config">ACO configuration</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Async enumerable of iteration results</returns>
    public async IAsyncEnumerable<AntColonyResult> SolveAsync(
        City[] cities,
        AntColonyConfig config,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(cities);
        ArgumentNullException.ThrowIfNull(config);

        if (cities.Length < 3)
            throw new ArgumentException("At least 3 cities are required", nameof(cities));

        if (!config.IsValid())
            throw new ArgumentException("Invalid ACO configuration", nameof(config));

        _logger.LogInformation("Starting ACO solve for {CityCount} cities with {AntCount} ants for {MaxIterations} iterations",
            cities.Length, config.AntCount, config.MaxIterations);

        var solver = new AntColonyOptimizationSolver();

        await foreach (var result in solver.SolveWithProgressAsync(cities, config, cancellationToken))
        {
            if (result.Iteration % config.ProgressReportInterval == 0 || result.IsComplete)
            {
                _logger.LogDebug("ACO Iteration {Iteration}: Best Distance = {BestDistance:F2}, Avg Distance = {AvgDistance:F2}",
                    result.Iteration, result.BestDistance, result.AverageDistance);
            }

            yield return result;

            if (result.IsComplete)
            {
                _logger.LogInformation("ACO completed after {Iterations} iterations. Best distance: {BestDistance:F2}",
                    result.Iteration + 1, result.BestDistance);
                break;
            }
        }
    }

    /// <summary>
    /// Creates a default ACO configuration optimized for the given problem size
    /// </summary>
    /// <param name="cityCount">Number of cities in the problem</param>
    /// <returns>Optimized ACO configuration</returns>
    public static AntColonyConfig CreateDefaultConfig(int cityCount)
    {
        // Scale parameters based on problem size
        var antCount = Math.Min(100, Math.Max(20, cityCount));
        var maxIterations = Math.Min(500, Math.Max(100, cityCount * 3));

        return new AntColonyConfig
        {
            AntCount = antCount,
            MaxIterations = maxIterations,
            Alpha = 1.0,
            Beta = 2.0,
            EvaporationRate = 0.5,
            InitialPheromone = 0.1,
            EliteAntCount = Math.Max(1, antCount / 10),
            UseLocalSearch = true,
            ProgressReportInterval = Math.Max(1, maxIterations / 20)
        };
    }

    /// <summary>
    /// Generates a set of random cities for testing ACO
    /// </summary>
    /// <param name="count">Number of cities to generate</param>
    /// <param name="seed">Random seed for reproducible results</param>
    /// <returns>Array of randomly placed cities</returns>
    public static City[] GenerateRandomCities(int count, int? seed = null)
    {
        if (count < 3)
            throw new ArgumentException("At least 3 cities are required", nameof(count));

        var random = seed.HasValue ? new Random(seed.Value) : new Random();
        var cities = new City[count];

        for (int i = 0; i < count; i++)
        {
            cities[i] = new City(
                Id: i,
                Name: $"City_{i:D2}",
                X: random.NextDouble() * 1000,
                Y: random.NextDouble() * 1000
            );
        }

        return cities;
    }

    /// <summary>
    /// Generates clustered cities for testing ACO performance on different problem types
    /// </summary>
    /// <param name="count">Number of cities to generate</param>
    /// <param name="clusterCount">Number of clusters</param>
    /// <param name="seed">Random seed for reproducible results</param>
    /// <returns>Array of clustered cities</returns>
    public static City[] GenerateClusteredCities(int count, int clusterCount = 3, int? seed = null)
    {
        if (count < 3)
            throw new ArgumentException("At least 3 cities are required", nameof(count));

        if (clusterCount < 1)
            throw new ArgumentException("At least 1 cluster is required", nameof(clusterCount));

        var random = seed.HasValue ? new Random(seed.Value) : new Random();
        var cities = new City[count];
        var citiesPerCluster = count / clusterCount;
        var cityIndex = 0;

        for (int cluster = 0; cluster < clusterCount; cluster++)
        {
            // Generate cluster center
            var centerX = random.NextDouble() * 800 + 100; // Keep clusters away from edges
            var centerY = random.NextDouble() * 800 + 100;
            var clusterRadius = random.NextDouble() * 80 + 40; // Cluster radius between 40-120

            var citiesInThisCluster = (cluster == clusterCount - 1) ?
                count - cityIndex : citiesPerCluster;

            for (int i = 0; i < citiesInThisCluster; i++)
            {
                // Generate city within cluster
                var angle = random.NextDouble() * 2 * Math.PI;
                var distance = random.NextDouble() * clusterRadius;
                var x = centerX + distance * Math.Cos(angle);
                var y = centerY + distance * Math.Sin(angle);

                cities[cityIndex] = new City(
                    Id: cityIndex,
                    Name: $"C{cluster}_City_{i:D2}",
                    X: Math.Max(0, Math.Min(1000, x)), // Clamp to bounds
                    Y: Math.Max(0, Math.Min(1000, y))
                );

                cityIndex++;
            }
        }

        return cities;
    }
}
