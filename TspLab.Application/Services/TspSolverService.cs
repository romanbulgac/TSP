using Microsoft.Extensions.Logging;
using System.Runtime.CompilerServices;
using TspLab.Domain.Entities;
using TspLab.Domain.Models;
using TspLab.Domain.Interfaces;

namespace TspLab.Application.Services;

/// <summary>
/// High-level service for solving TSP using genetic algorithms
/// </summary>
public sealed class TspSolverService
{
    private readonly GeneticEngine _geneticEngine;
    private readonly StrategyResolver _strategyResolver;
    private readonly ILogger<TspSolverService> _logger;

    public TspSolverService(
        GeneticEngine geneticEngine,
        StrategyResolver strategyResolver,
        ILogger<TspSolverService> logger)
    {
        _geneticEngine = geneticEngine ?? throw new ArgumentNullException(nameof(geneticEngine));
        _strategyResolver = strategyResolver ?? throw new ArgumentNullException(nameof(strategyResolver));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Solves TSP using genetic algorithm with streaming results
    /// </summary>
    /// <param name="cities">Array of cities to visit</param>
    /// <param name="config">GA configuration</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Async enumerable of generation results</returns>
    public async IAsyncEnumerable<GeneticAlgorithmResult> SolveAsync(
        City[] cities,
        GeneticAlgorithmConfig config,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(cities);
        if (cities.Length < 3)
            throw new ArgumentException("At least 3 cities are required", nameof(cities));

        _logger.LogInformation("Starting TSP solve for {CityCount} cities", cities.Length);

        // Resolve strategies upfront to validate them
        var crossover = _strategyResolver.ResolveCrossover(config.CrossoverName);
        var mutation = _strategyResolver.ResolveMutation(config.MutationName);
        var fitnessFunction = _strategyResolver.ResolveFitnessFunction(config.FitnessFunctionName);

        _logger.LogInformation("Using strategies: Crossover={Crossover}, Mutation={Mutation}, Fitness={Fitness}",
            crossover.Name, mutation.Name, fitnessFunction.Name);

        // Run genetic algorithm and stream results
        await foreach (var result in _geneticEngine.RunAsync(cities, config, crossover, mutation, fitnessFunction, cancellationToken))
        {
            yield return result;
        }
    }

    /// <summary>
    /// Solves TSP using genetic algorithm with pause/resume support
    /// </summary>
    /// <param name="resumeState">Optional state to resume from</param>
    /// <param name="cities">Array of cities to visit</param>
    /// <param name="config">GA configuration</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Async enumerable of generation results</returns>
    public async IAsyncEnumerable<GeneticAlgorithmResult> SolveWithStateAsync(
        GeneticAlgorithmState? resumeState,
        City[] cities,
        GeneticAlgorithmConfig config,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(cities);
        if (cities.Length < 3)
            throw new ArgumentException("At least 3 cities are required", nameof(cities));

        _logger.LogInformation("Starting TSP solve for {CityCount} cities with state support", cities.Length);

        // Resolve strategies upfront to validate them
        var crossover = _strategyResolver.ResolveCrossover(config.CrossoverName);
        var mutation = _strategyResolver.ResolveMutation(config.MutationName);
        var fitnessFunction = _strategyResolver.ResolveFitnessFunction(config.FitnessFunctionName);

        _logger.LogInformation("Using strategies: Crossover={Crossover}, Mutation={Mutation}, Fitness={Fitness}",
            crossover.Name, mutation.Name, fitnessFunction.Name);

        // Run genetic algorithm with state support and stream results
        await foreach (var result in _geneticEngine.RunWithStateAsync(resumeState, cities, config, crossover, mutation, fitnessFunction, cancellationToken))
        {
            yield return result;
        }
    }

    /// <summary>
    /// Gets available strategy options
    /// </summary>
    /// <returns>Available strategies for crossover, mutation, and fitness functions</returns>
    public AvailableStrategies GetAvailableStrategies()
    {
        return new AvailableStrategies(
            Crossovers: _strategyResolver.GetAvailableCrossovers().ToList(),
            Mutations: _strategyResolver.GetAvailableMutations().ToList(),
            FitnessFunctions: _strategyResolver.GetAvailableFitnessFunctions().ToList()
        );
    }

    /// <summary>
    /// Generates a set of random cities for testing
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
}

/// <summary>
/// Available strategies for genetic algorithm components
/// </summary>
/// <param name="Crossovers">Available crossover operators</param>
/// <param name="Mutations">Available mutation operators</param>
/// <param name="FitnessFunctions">Available fitness functions</param>
public readonly record struct AvailableStrategies(
    IReadOnlyList<string> Crossovers,
    IReadOnlyList<string> Mutations,
    IReadOnlyList<string> FitnessFunctions);
