using TspLab.Application.Services;
using TspLab.Domain.Entities;
using TspLab.Domain.Interfaces;
using TspLab.Domain.Models;

namespace TspLab.Application.Solvers;

/// <summary>
/// Wrapper that adapts the existing TspSolverService (Genetic Algorithm) 
/// to the ITspSolver interface for unified algorithm comparison.
/// Extracts the best tour from the generational stream.
/// </summary>
public sealed class GeneticAlgorithmSolver : ITspSolver
{
    private readonly TspSolverService _tspSolverService;

    /// <summary>
    /// Initializes a new instance of the GeneticAlgorithmSolver class.
    /// </summary>
    /// <param name="tspSolverService">The underlying genetic algorithm service.</param>
    public GeneticAlgorithmSolver(TspSolverService tspSolverService)
    {
        _tspSolverService = tspSolverService ?? throw new ArgumentNullException(nameof(tspSolverService));
    }

    /// <inheritdoc />
    public string Name => "Genetic Algorithm";

    /// <inheritdoc />
    public string Description => "Evolutionary algorithm that uses selection, crossover, and mutation to evolve solutions over generations";

    /// <inheritdoc />
    public async Task<Tour> SolveAsync(City[] cities, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(cities);

        if (cities.Length == 0)
            throw new ArgumentException("Cannot solve TSP with no cities", nameof(cities));

        if (cities.Length == 1)
            return new Tour([0]);

        if (cities.Length == 2)
        {
            var trivialTour = new Tour([0, 1]);
            trivialTour.Distance = 2 * cities[0].DistanceTo(cities[1]);
            return trivialTour;
        }

        // Use default GA configuration optimized for quick results
        var config = CreateDefaultConfig(cities.Length);

        Tour? bestTour = null;
        var bestDistance = double.MaxValue;

        // Stream through generations and track the best solution
        await foreach (var result in _tspSolverService.SolveAsync(cities, config, cancellationToken))
        {
            if (result.BestDistance < bestDistance)
            {
                bestTour = new Tour(result.BestTour);
                bestTour.Distance = result.BestDistance;
                bestDistance = result.BestDistance;
            }

            // Early termination if we find a very good solution
            if (result.BestFitness > 0.95) // High fitness indicates good solution
                break;
        }

        return bestTour ?? throw new InvalidOperationException("No solution was found");
    }

    /// <summary>
    /// Creates a default genetic algorithm configuration optimized for the ITspSolver interface.
    /// Uses smaller population and generation limits for faster execution.
    /// </summary>
    /// <param name="cityCount">The number of cities in the problem.</param>
    /// <returns>A configured GeneticAlgorithmConfig instance.</returns>
    private static GeneticAlgorithmConfig CreateDefaultConfig(int cityCount)
    {
        // Scale parameters based on problem size
        var populationSize = Math.Min(100, Math.Max(20, cityCount * 2));
        var maxGenerations = Math.Min(500, Math.Max(50, cityCount * 5));

        return new GeneticAlgorithmConfig
        {
            PopulationSize = populationSize,
            MaxGenerations = maxGenerations,
            MutationRate = 0.02,
            CrossoverRate = 0.8,
            ElitismRate = 0.1,
            TournamentSize = Math.Min(5, populationSize / 10),
            StagnationLimit = 50,
            ProgressReportInterval = 10,
            UseParallelProcessing = true,
            MaxDegreeOfParallelism = -1,
            TrackStatistics = false, // Disable for performance
            CrossoverName = "OrderCrossover",
            MutationName = "SwapMutation",
            FitnessFunctionName = "DistanceFitness"
        };
    }
}
