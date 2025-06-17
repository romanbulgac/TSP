using System.Diagnostics;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
using TspLab.Domain.Entities;
using TspLab.Domain.Interfaces;
using TspLab.Domain.Models;
using TspLab.Domain;


namespace TspLab.Application.Services;

/// <summary>
/// Core genetic algorithm engine for solving TSP
/// </summary>
public sealed class GeneticEngine
{
    private readonly ILogger<GeneticEngine> _logger;
    private readonly Random _random;

    public GeneticEngine(ILogger<GeneticEngine> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _random = new Random();
    }

    /// <summary>
    /// Runs the genetic algorithm with streaming results
    /// </summary>
    /// <param name="cities">Array of cities to visit</param>
    /// <param name="config">GA configuration</param>
    /// <param name="crossover">Crossover operator</param>
    /// <param name="mutation">Mutation operator</param>
    /// <param name="fitnessFunction">Fitness function</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Stream of generation results</returns>
    public async IAsyncEnumerable<GeneticAlgorithmResult> RunAsync(
        City[] cities,
        GeneticAlgorithmConfig config,
        ICrossover crossover,
        IMutation mutation,
        IFitnessFunction fitnessFunction,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(cities);
        ArgumentNullException.ThrowIfNull(crossover);
        ArgumentNullException.ThrowIfNull(mutation);
        ArgumentNullException.ThrowIfNull(fitnessFunction);

        if (!config.IsValid())
            throw new ArgumentException("Invalid configuration", nameof(config));

        _logger.LogInformation("Starting GA with {PopulationSize} individuals, {MaxGenerations} generations",
            config.PopulationSize, config.MaxGenerations);

        var stopwatch = Stopwatch.StartNew();
        mutation.MutationRate = config.MutationRate;

        // Initialize population
        var population = InitializePopulation(cities.Length, config.PopulationSize);
        
        // Evaluate initial fitness
        await EvaluatePopulation(population, cities, fitnessFunction);
        
        var bestTour = GetBestTour(population);
        var convergenceHistory = new List<double>();
        
        // Tracking for stagnation detection
        var lastImprovementGeneration = 0;
        var lastBestFitness = bestTour.Fitness;
        const double fitnessImprovementThreshold = 1e-6; // Minimum improvement considered significant
        
        int currentGeneration = 0;
        bool completedEarly = false;

        for (currentGeneration = 0; currentGeneration < config.MaxGenerations; currentGeneration++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Create new generation
            var newPopulation = await CreateNewGeneration(
                population, config, crossover, mutation, fitnessFunction, cities);

            population = newPopulation;
            
            // Track best solution
            var currentBest = GetBestTour(population);
            
            if (currentBest.Fitness - bestTour.Fitness > fitnessImprovementThreshold)
            {
                bestTour = currentBest.Clone();
                lastImprovementGeneration = currentGeneration;
                lastBestFitness = bestTour.Fitness;
                
                _logger.LogDebug("Improvement found at generation {Generation}: fitness = {Fitness}", 
                    currentGeneration, bestTour.Fitness);
            }

            var avgFitness = population.Average(t => t.Fitness);
            convergenceHistory.Add(bestTour.Fitness);

            // Check for early termination conditions
            var generationsWithoutImprovement = currentGeneration - lastImprovementGeneration;
            var shouldStopDueToStagnation = config.StagnationLimit > 0 && 
                                          generationsWithoutImprovement >= config.StagnationLimit;

            // Check if this is the last generation (either due to max generations or early termination)
            var isLastGeneration = currentGeneration >= config.MaxGenerations - 1 || shouldStopDueToStagnation;

            // Yield current result
            var result = new GeneticAlgorithmResult(
                Generation: currentGeneration,
                BestFitness: bestTour.Fitness,
                AverageFitness: avgFitness,
                BestTour: bestTour.Cities.ToArray(),
                BestDistance: bestTour.Distance,
                ElapsedMilliseconds: stopwatch.ElapsedMilliseconds,
                IsComplete: isLastGeneration
            );
            
            yield return result;

            // Early termination checks
            if (shouldStopDueToStagnation)
            {
                _logger.LogInformation("Stopping due to stagnation: {Generations} generations without improvement", 
                    generationsWithoutImprovement);
                completedEarly = true;
                break;
            }
        }

        // Only send final result if we didn't complete early (to avoid duplication)
        if (!completedEarly && currentGeneration >= config.MaxGenerations)
        {
            yield return new GeneticAlgorithmResult(
                Generation: config.MaxGenerations - 1, // Last valid generation index
                BestFitness: bestTour.Fitness,
                AverageFitness: population.Average(t => t.Fitness),
                BestTour: bestTour.Cities.ToArray(),
                BestDistance: bestTour.Distance,
                ElapsedMilliseconds: stopwatch.ElapsedMilliseconds,
                IsComplete: true
            );
        }

        _logger.LogInformation("GA completed. Best fitness: {Fitness}, Generations: {Generations}, Total time: {ElapsedMs}ms",
            bestTour.Fitness, completedEarly ? currentGeneration + 1 : config.MaxGenerations, stopwatch.ElapsedMilliseconds);
    }

    /// <summary>
    /// Initializes a random population
    /// </summary>
    private Tour[] InitializePopulation(int cityCount, int populationSize)
    {
        var population = new Tour[populationSize];
        var baseTour = Enumerable.Range(0, cityCount).ToArray();

        for (int i = 0; i < populationSize; i++)
        {
            var cities = (int[])baseTour.Clone();
            
            // Shuffle using Fisher-Yates algorithm
            for (int j = cities.Length - 1; j > 0; j--)
            {
                int k = _random.Next(j + 1);
                (cities[j], cities[k]) = (cities[k], cities[j]);
            }

            population[i] = new Tour(cities);
        }

        return population;
    }

    /// <summary>
    /// Evaluates fitness for all tours in the population
    /// </summary>
    private async Task EvaluatePopulation(Tour[] population, City[] cities, IFitnessFunction fitnessFunction)
    {
        // Parallel fitness evaluation for better performance
        await Task.Run(() =>
        {
            Parallel.ForEach(population, tour =>
            {
                tour.Fitness = fitnessFunction.CalculateFitness(tour, cities.AsSpan());
                tour.Distance = fitnessFunction.CalculateDistance(tour, cities.AsSpan());
            });
        });
    }

    /// <summary>
    /// Creates a new generation using genetic operators
    /// </summary>
    private async Task<Tour[]> CreateNewGeneration(
        Tour[] population,
        GeneticAlgorithmConfig config,
        ICrossover crossover,
        IMutation mutation,
        IFitnessFunction fitnessFunction,
        City[] cities)
    {
        var newPopulation = new Tour[population.Length];
        int eliteCount = (int)(population.Length * config.ElitismRate);

        // Sort by fitness (descending)
        var sortedPopulation = population.OrderByDescending(t => t.Fitness).ToArray();

        // Elitism - carry over best individuals
        for (int i = 0; i < eliteCount; i++)
        {
            newPopulation[i] = sortedPopulation[i].Clone();
        }

        // Generate offspring for remaining slots
        for (int i = eliteCount; i < newPopulation.Length; i += 2)
        {
            // Tournament selection
            var parent1 = TournamentSelection(population, config.TournamentSize);
            var parent2 = TournamentSelection(population, config.TournamentSize);

            // Crossover
            var (offspring1, offspring2) = crossover.Crossover(parent1, parent2, _random);

            // Mutation
            mutation.Mutate(offspring1, _random);
            if (i + 1 < newPopulation.Length)
                mutation.Mutate(offspring2, _random);

            newPopulation[i] = offspring1;
            if (i + 1 < newPopulation.Length)
                newPopulation[i + 1] = offspring2;
        }

        // Evaluate new population
        await EvaluatePopulation(newPopulation, cities, fitnessFunction);

        return newPopulation;
    }

    /// <summary>
    /// Tournament selection for parent selection
    /// </summary>
    private Tour TournamentSelection(Tour[] population, int tournamentSize)
    {
        var tournament = new Tour[tournamentSize];
        
        for (int i = 0; i < tournamentSize; i++)
        {
            int randomIndex = _random.Next(population.Length);
            tournament[i] = population[randomIndex];
        }

        return tournament.OrderByDescending(t => t.Fitness).First();
    }

    /// <summary>
    /// Gets the best tour from the population
    /// </summary>
    private static Tour GetBestTour(Tour[] population)
    {
        return population.OrderByDescending(t => t.Fitness).First();
    }
}
