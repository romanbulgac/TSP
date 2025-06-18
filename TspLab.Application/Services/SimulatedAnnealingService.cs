using Microsoft.Extensions.Logging;
using System.Runtime.CompilerServices;
using TspLab.Application.Heuristics;
using TspLab.Domain.Entities;
using TspLab.Domain.Models;

namespace TspLab.Application.Services;

/// <summary>
/// High-level service for solving TSP using Simulated Annealing
/// </summary>
public sealed class SimulatedAnnealingService
{
    private readonly ILogger<SimulatedAnnealingService> _logger;

    public SimulatedAnnealingService(ILogger<SimulatedAnnealingService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Solves TSP using SA with streaming results for real-time monitoring
    /// </summary>
    /// <param name="cities">Array of cities to visit</param>
    /// <param name="config">SA configuration</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Async enumerable of iteration results</returns>
    public async IAsyncEnumerable<SimulatedAnnealingResult> SolveAsync(
        City[] cities,
        SimulatedAnnealingConfig config,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(cities);
        ArgumentNullException.ThrowIfNull(config);

        if (cities.Length < 3)
            throw new ArgumentException("At least 3 cities are required", nameof(cities));

        if (!config.IsValid())
            throw new ArgumentException("Invalid SA configuration", nameof(config));

        _logger.LogInformation("Starting SA solve for {CityCount} cities with temp {InitialTemp}-{FinalTemp}",
            cities.Length, config.InitialTemperature, config.FinalTemperature);

        await foreach (var result in SolveWithProgressAsync(cities, config, cancellationToken))
        {
            if (result.Iteration % 100 == 0 || result.IsComplete)
            {
                _logger.LogDebug("SA Iteration {Iteration}: Best Distance = {BestDistance:F2}, Temp = {Temperature:F2}",
                    result.Iteration, result.BestDistance, result.CurrentTemperature);
            }

            yield return result;

            if (result.IsComplete)
            {
                _logger.LogInformation("SA completed after {Iterations} iterations. Best distance: {BestDistance:F2}",
                    result.Iteration + 1, result.BestDistance);
                break;
            }
        }
    }

    /// <summary>
    /// Creates a default SA configuration optimized for the given problem size
    /// </summary>
    /// <param name="cityCount">Number of cities in the problem</param>
    /// <returns>Optimized SA configuration</returns>
    public static SimulatedAnnealingConfig CreateDefaultConfig(int cityCount)
    {
        return SimulatedAnnealingConfig.ForProblemSize(cityCount);
    }

    /// <summary>
    /// Internal method that provides streaming results during SA execution
    /// </summary>
    private static async IAsyncEnumerable<SimulatedAnnealingResult> SolveWithProgressAsync(
        City[] cities,
        SimulatedAnnealingConfig config,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var cityCount = cities.Length;
        var startTime = DateTime.UtcNow;
        
        // Initialize
        var currentTour = config.UseNearestNeighborInitialization 
            ? GenerateNearestNeighborTour(cities)
            : GenerateRandomTour(cityCount);
        var currentDistance = CalculateTourDistance(cities, currentTour);

        var bestTour = (int[])currentTour.Clone();
        var bestDistance = currentDistance;

        var temperature = config.InitialTemperature;
        var iteration = 0;
        var improvementCount = 0;
        var acceptedMoves = 0;
        var totalMoves = 0;

        while (temperature > config.FinalTemperature && iteration < config.MaxIterations && !cancellationToken.IsCancellationRequested)
        {
            // Generate neighbor
            var neighborTour = Random.Shared.NextDouble() < config.TwoOptProbability 
                ? Generate2OptNeighbor(currentTour) 
                : GenerateSwapNeighbor(currentTour);
            
            var neighborDistance = CalculateTourDistance(cities, neighborTour);
            var deltaE = neighborDistance - currentDistance;
            totalMoves++;

            // Accept or reject based on SA criterion
            if (ShouldAcceptSolution(deltaE, temperature))
            {
                currentTour = neighborTour;
                currentDistance = neighborDistance;
                acceptedMoves++;

                if (currentDistance < bestDistance)
                {
                    bestTour = (int[])currentTour.Clone();
                    bestDistance = currentDistance;
                    improvementCount++;
                }
            }

            // Cool down or reheat
            if (config.EnableAdaptiveReheating && 
                iteration % config.ReheatCheckInterval == 0 && 
                improvementCount == 0)
            {
                temperature *= 1.1; // Reheat
            }
            else
            {
                temperature *= config.CoolingRate;
            }

            // Report progress every 100 iterations or on improvements
            if (iteration % 100 == 0 || improvementCount > 0)
            {
                var elapsed = DateTime.UtcNow - startTime;
                var acceptanceRate = totalMoves > 0 ? (double)acceptedMoves / totalMoves : 0.0;

                yield return new SimulatedAnnealingResult
                {
                    Iteration = iteration,
                    TotalIterations = config.MaxIterations,
                    BestTour = bestTour.ToList(),
                    BestDistance = bestDistance,
                    CurrentTemperature = temperature,
                    InitialTemperature = config.InitialTemperature,
                    AcceptanceRate = acceptanceRate,
                    TotalAccepted = acceptedMoves,
                    TotalRejected = totalMoves - acceptedMoves,
                    Improvements = improvementCount,
                    ElapsedTime = elapsed,
                    IsComplete = false,
                    Phase = "Optimizing"
                };

                // Reset counters for next reporting period
                acceptedMoves = 0;
                totalMoves = 0;

                // Yield control to allow cancellation and prevent blocking
                await Task.Yield();
            }

            iteration++;
            
            // Reset improvement counter periodically
            if (iteration % config.ReheatCheckInterval == 0)
                improvementCount = 0;
        }

        // Final result
        var finalElapsed = DateTime.UtcNow - startTime;
        yield return new SimulatedAnnealingResult
        {
            Iteration = iteration,
            TotalIterations = config.MaxIterations,
            BestTour = bestTour.ToList(),
            BestDistance = bestDistance,
            CurrentTemperature = temperature,
            InitialTemperature = config.InitialTemperature,
            AcceptanceRate = 0.0,
            TotalAccepted = 0,
            TotalRejected = 0,
            Improvements = 0,
            ElapsedTime = finalElapsed,
            IsComplete = true,
            Phase = "Complete"
        };
    }

    // Helper methods
    private static int[] GenerateNearestNeighborTour(City[] cities)
    {
        var cityCount = cities.Length;
        var visited = new bool[cityCount];
        var tour = new int[cityCount];
        var current = Random.Shared.Next(cityCount);
        tour[0] = current;
        visited[current] = true;

        for (int step = 1; step < cityCount; step++)
        {
            var nearest = -1;
            var minDistance = double.MaxValue;

            for (int next = 0; next < cityCount; next++)
            {
                if (!visited[next])
                {
                    var distance = cities[current].DistanceTo(cities[next]);
                    if (distance < minDistance)
                    {
                        minDistance = distance;
                        nearest = next;
                    }
                }
            }

            tour[step] = nearest;
            visited[nearest] = true;
            current = nearest;
        }

        return tour;
    }

    private static int[] GenerateRandomTour(int cityCount)
    {
        var tour = Enumerable.Range(0, cityCount).ToArray();
        for (var i = tour.Length - 1; i > 0; i--)
        {
            var j = Random.Shared.Next(i + 1);
            (tour[i], tour[j]) = (tour[j], tour[i]);
        }
        return tour;
    }

    private static int[] Generate2OptNeighbor(int[] currentTour)
    {
        var neighbor = (int[])currentTour.Clone();
        var length = neighbor.Length;
        var i = Random.Shared.Next(length);
        var j = Random.Shared.Next(length);

        if (i > j) (i, j) = (j, i);
        if (j - i < 2) return neighbor;

        Array.Reverse(neighbor, i + 1, j - i);
        return neighbor;
    }

    private static int[] GenerateSwapNeighbor(int[] currentTour)
    {
        var neighbor = (int[])currentTour.Clone();
        var pos1 = Random.Shared.Next(neighbor.Length);
        var pos2 = Random.Shared.Next(neighbor.Length);
        while (pos2 == pos1) pos2 = Random.Shared.Next(neighbor.Length);
        (neighbor[pos1], neighbor[pos2]) = (neighbor[pos2], neighbor[pos1]);
        return neighbor;
    }

    private static bool ShouldAcceptSolution(double deltaE, double temperature)
    {
        if (deltaE <= 0) return true;
        var acceptanceProbability = Math.Exp(-deltaE / temperature);
        return Random.Shared.NextDouble() < acceptanceProbability;
    }

    private static double CalculateTourDistance(City[] cities, int[] tour)
    {
        var totalDistance = 0.0;
        for (var i = 0; i < tour.Length; i++)
        {
            var nextIndex = (i + 1) % tour.Length;
            totalDistance += cities[tour[i]].DistanceTo(cities[tour[nextIndex]]);
        }
        return totalDistance;
    }
}
