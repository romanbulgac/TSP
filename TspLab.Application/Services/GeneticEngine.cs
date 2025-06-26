using System.Diagnostics;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
using TspLab.Domain.Entities;
using TspLab.Domain.Interfaces;
using TspLab.Domain.Models;
using TspLab.Domain;


namespace TspLab.Application.Services;

/// <summary>
/// Core genetic algorithm engine for solving TSP with pause/resume support
/// </summary>
public sealed class GeneticEngine
{
    private readonly ILogger<GeneticEngine> _logger;
    private readonly IAlgorithmStateManager? _stateManager;
    private Random _random;

    public GeneticEngine(ILogger<GeneticEngine> logger, IAlgorithmStateManager? stateManager = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _stateManager = stateManager;
        _random = new Random();
    }

    /// <summary>
    /// Runs the genetic algorithm with streaming results (original method for backwards compatibility)
    /// </summary>
    public async IAsyncEnumerable<GeneticAlgorithmResult> RunAsync(
        City[] cities,
        GeneticAlgorithmConfig config,
        ICrossover crossover,
        IMutation mutation,
        IFitnessFunction fitnessFunction,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (var result in RunWithStateAsync(null, cities, config, crossover, mutation, fitnessFunction, cancellationToken))
        {
            yield return result;
        }
    }

    /// <summary>
    /// Runs the genetic algorithm with pause/resume support
    /// </summary>
    /// <param name="resumeState">Optional state to resume from</param>
    /// <param name="cities">Array of cities to visit</param>
    /// <param name="config">GA configuration</param>
    /// <param name="crossover">Crossover operator</param>
    /// <param name="mutation">Mutation operator</param>
    /// <param name="fitnessFunction">Fitness function</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Stream of generation results</returns>
    public async IAsyncEnumerable<GeneticAlgorithmResult> RunWithStateAsync(
        GeneticAlgorithmState? resumeState,
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

        // Initialize state
        var sessionId = resumeState?.SessionId ?? Guid.NewGuid().ToString();
        var stopwatch = resumeState != null ? Stopwatch.StartNew() : Stopwatch.StartNew();
        var elapsedOffset = resumeState?.ElapsedMilliseconds ?? 0;

        // Set random seed for reproducibility if provided
        if (config.RandomSeed.HasValue)
        {
            _random = new Random(config.RandomSeed.Value);
        }

        mutation.MutationRate = config.MutationRate;

        Tour[] population;
        Tour bestTour;
        List<double> convergenceHistory;
        int startGeneration;
        int lastImprovementGeneration;
        int generationsWithoutImprovement;

        if (resumeState != null)
        {
            // Resume from saved state
            _logger.LogInformation("Resuming GA from generation {Generation}", resumeState.CurrentGeneration);
            
            population = resumeState.Population.Tours.Select(t => t.ToTour()).ToArray();
            bestTour = resumeState.BestTour?.ToTour() ?? GetBestTour(population);
            convergenceHistory = new List<double>(resumeState.ConvergenceHistory);
            startGeneration = resumeState.CurrentGeneration;
            lastImprovementGeneration = resumeState.LastImprovementGeneration;
            generationsWithoutImprovement = resumeState.GenerationsWithoutImprovement;
            
            _logger.LogInformation("Resumed with {PopulationSize} individuals, best fitness: {Fitness}", 
                population.Length, bestTour.Fitness);
        }
        else
        {
            // Initialize new run
            _logger.LogInformation("Starting new GA with {PopulationSize} individuals, {MaxGenerations} generations",
                config.PopulationSize, config.MaxGenerations);

            population = InitializePopulation(cities.Length, config.PopulationSize);
            await EvaluatePopulation(population, cities, fitnessFunction);
            
            bestTour = GetBestTour(population);
            convergenceHistory = new List<double>();
            startGeneration = 0;
            lastImprovementGeneration = 0;
            generationsWithoutImprovement = 0;
        }

        const double fitnessImprovementThreshold = 1e-6;
        int currentGeneration = startGeneration;
        bool completedEarly = false;

        // Create checkpoint configuration
        var checkpointConfig = new CheckpointConfig
        {
            EnableAutoCheckpoint = config.TrackStatistics,
            CheckpointInterval = Math.Max(50, config.ProgressReportInterval * 2),
            MaxCheckpoints = 5,
            UseBrowserStorage = true
        };

        for (currentGeneration = startGeneration; currentGeneration < config.MaxGenerations; currentGeneration++)
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

                _logger.LogDebug("Improvement found at generation {Generation}: fitness = {Fitness}",
                    currentGeneration, bestTour.Fitness);
            }

            var avgFitness = population.Average(t => t.Fitness);
            convergenceHistory.Add(bestTour.Fitness);

            // Check for early termination conditions
            generationsWithoutImprovement = currentGeneration - lastImprovementGeneration;
            var shouldStopDueToStagnation = config.StagnationLimit > 0 &&
                                          generationsWithoutImprovement >= config.StagnationLimit;

            var isLastGeneration = currentGeneration >= config.MaxGenerations - 1 || shouldStopDueToStagnation;

            // Create checkpoint if enabled
            if (_stateManager != null && checkpointConfig.EnableAutoCheckpoint && 
                currentGeneration % checkpointConfig.CheckpointInterval == 0)
            {
                var checkpointState = CreateAlgorithmState(
                    sessionId, currentGeneration, config, cities, population, bestTour, 
                    convergenceHistory, elapsedOffset + stopwatch.ElapsedMilliseconds,
                    generationsWithoutImprovement, lastImprovementGeneration,
                    isLastGeneration ? AlgorithmStatus.Completed : AlgorithmStatus.Running);

                try
                {
                    await _stateManager.CreateCheckpointAsync(checkpointState, checkpointConfig, cancellationToken);
                    _logger.LogDebug("Created checkpoint at generation {Generation}", currentGeneration);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to create checkpoint at generation {Generation}", currentGeneration);
                }
            }

            // Yield current result
            var result = new GeneticAlgorithmResult(
                Generation: currentGeneration,
                BestFitness: bestTour.Fitness,
                AverageFitness: avgFitness,
                BestTour: bestTour.Cities.ToArray(),
                BestDistance: bestTour.Distance,
                ElapsedMilliseconds: elapsedOffset + stopwatch.ElapsedMilliseconds,
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

        // Save final state if state manager is available
        if (_stateManager != null)
        {
            var finalState = CreateAlgorithmState(
                sessionId, currentGeneration, config, cities, population, bestTour,
                convergenceHistory, elapsedOffset + stopwatch.ElapsedMilliseconds,
                generationsWithoutImprovement, lastImprovementGeneration,
                AlgorithmStatus.Completed);

            try
            {
                await _stateManager.SaveStateAsync(finalState, cancellationToken);
                _logger.LogInformation("Saved final algorithm state for session {SessionId}", sessionId);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to save final algorithm state");
            }
        }

        _logger.LogInformation("GA completed. Best fitness: {Fitness}, Generations: {Generations}, Total time: {ElapsedMs}ms",
            bestTour.Fitness, completedEarly ? currentGeneration + 1 : config.MaxGenerations, 
            elapsedOffset + stopwatch.ElapsedMilliseconds);
    }

    /// <summary>
    /// Pauses the algorithm and saves the current state
    /// </summary>
    public async Task<string?> PauseAndSaveStateAsync(
        string sessionId,
        int currentGeneration,
        GeneticAlgorithmConfig config,
        City[] cities,
        Tour[] population,
        Tour bestTour,
        List<double> convergenceHistory,
        long elapsedMilliseconds,
        int generationsWithoutImprovement,
        int lastImprovementGeneration,
        CancellationToken cancellationToken = default)
    {
        if (_stateManager == null)
            return null;

        var state = CreateAlgorithmState(
            sessionId, currentGeneration, config, cities, population, bestTour,
            convergenceHistory, elapsedMilliseconds, generationsWithoutImprovement,
            lastImprovementGeneration, AlgorithmStatus.Paused);

        try
        {
            await _stateManager.SaveStateAsync(state, cancellationToken);
            _logger.LogInformation("Paused and saved algorithm state for session {SessionId}", sessionId);
            return sessionId;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save paused algorithm state");
            return null;
        }
    }

    private static GeneticAlgorithmState CreateAlgorithmState(
        string sessionId,
        int currentGeneration,
        GeneticAlgorithmConfig config,
        City[] cities,
        Tour[] population,
        Tour? bestTour,
        List<double> convergenceHistory,
        long elapsedMilliseconds,
        int generationsWithoutImprovement,
        int lastImprovementGeneration,
        AlgorithmStatus status)
    {
        return new GeneticAlgorithmState
        {
            SessionId = sessionId,
            CurrentGeneration = currentGeneration,
            Config = config,
            Cities = cities,
            Population = new PopulationState
            {
                Tours = population.Select(TourWithFitness.FromTour).ToList()
            },
            BestTour = bestTour != null ? TourWithFitness.FromTour(bestTour) : null,
            ConvergenceHistory = convergenceHistory,
            PausedAt = DateTime.UtcNow,
            ElapsedMilliseconds = elapsedMilliseconds,
            GenerationsWithoutImprovement = generationsWithoutImprovement,
            LastImprovementGeneration = lastImprovementGeneration,
            Status = status
        };
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
                // Check if tour is valid first
                if (!tour.IsValid())
                {
                    _logger.LogWarning("Invalid tour detected during evaluation: {Tour}", tour);
                    tour.Fitness = 0.0; // Very bad fitness for invalid tours
                    tour.Distance = double.MaxValue;
                    return;
                }

                try
                {
                    tour.Fitness = fitnessFunction.CalculateFitness(tour, cities.AsSpan());
                    tour.Distance = fitnessFunction.CalculateDistance(tour, cities.AsSpan());
                    
                    // Additional validation of calculated values
                    if (double.IsNaN(tour.Fitness) || double.IsInfinity(tour.Fitness) || tour.Fitness < 0)
                    {
                        _logger.LogWarning("Invalid fitness calculated: {Fitness} for tour: {Tour}", tour.Fitness, tour);
                        tour.Fitness = 0.0;
                        tour.Distance = double.MaxValue;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error evaluating tour: {Tour}", tour);
                    tour.Fitness = 0.0;
                    tour.Distance = double.MaxValue;
                }
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

            Tour offspring1, offspring2;

            // Apply crossover based on crossover rate
            if (_random.NextDouble() < config.CrossoverRate)
            {
                // Crossover
                (offspring1, offspring2) = crossover.Crossover(parent1, parent2, _random);
                
                // Validate offspring - if invalid, use parents as fallback
                if (!offspring1.IsValid())
                {
                    _logger.LogWarning("Invalid offspring1 after crossover, using parent1 as fallback");
                    offspring1 = parent1.Clone();
                }
                if (!offspring2.IsValid())
                {
                    _logger.LogWarning("Invalid offspring2 after crossover, using parent2 as fallback");
                    offspring2 = parent2.Clone();
                }
            }
            else
            {
                // No crossover - just copy parents
                offspring1 = parent1.Clone();
                offspring2 = parent2.Clone();
            }

            // Mutation
            var beforeMutation1 = offspring1.IsValid();
            mutation.Mutate(offspring1, _random);
            if (!offspring1.IsValid())
            {
                _logger.LogWarning("Invalid offspring1 after mutation, using pre-mutation version");
                offspring1 = beforeMutation1 ? parent1.Clone() : offspring1;
            }
            
            if (i + 1 < newPopulation.Length)
            {
                var beforeMutation2 = offspring2.IsValid();
                mutation.Mutate(offspring2, _random);
                if (!offspring2.IsValid())
                {
                    _logger.LogWarning("Invalid offspring2 after mutation, using pre-mutation version");
                    offspring2 = beforeMutation2 ? parent2.Clone() : offspring2;
                }
            }

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
