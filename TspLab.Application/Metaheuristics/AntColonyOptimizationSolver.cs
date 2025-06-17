using System.Diagnostics;
using TspLab.Domain.Entities;
using TspLab.Domain.Interfaces;
using TspLab.Domain.Models;

namespace TspLab.Application.Metaheuristics;

/// <summary>
/// Implements the Ant Colony Optimization (ACO) metaheuristic for solving TSP.
/// Uses artificial ants that deposit pheromone trails to find good solutions
/// through collective intelligence and positive feedback.
/// </summary>
public sealed class AntColonyOptimizationSolver : ITspSolver
{
    /// <inheritdoc />
    public string Name => "Ant Colony Optimization";

    /// <inheritdoc />
    public string Description => "Metaheuristic inspired by ant behavior that uses pheromone trails to find optimal paths through collective intelligence";

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

        return await Task.Run(() => SolveInternal(cities, AntColonyConfig.Default, cancellationToken), cancellationToken);
    }

    /// <summary>
    /// Solves TSP using Ant Colony Optimization with custom configuration
    /// </summary>
    /// <param name="cities">Array of cities to visit</param>
    /// <param name="config">ACO algorithm configuration</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Best tour found</returns>
    public async Task<Tour> SolveAsync(City[] cities, AntColonyConfig config, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(cities);
        ArgumentNullException.ThrowIfNull(config);

        if (!config.IsValid())
            throw new ArgumentException("Invalid ACO configuration", nameof(config));

        return await Task.Run(() => SolveInternal(cities, config, cancellationToken), cancellationToken);
    }

    /// <summary>
    /// Solves TSP using ACO with streaming results for real-time progress monitoring
    /// </summary>
    /// <param name="cities">Array of cities to visit</param>
    /// <param name="config">ACO algorithm configuration</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Async enumerable of iteration results</returns>
    public async IAsyncEnumerable<AntColonyResult> SolveWithProgressAsync(
        City[] cities, 
        AntColonyConfig config, 
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(cities);
        ArgumentNullException.ThrowIfNull(config);

        if (cities.Length < 3)
            throw new ArgumentException("At least 3 cities are required", nameof(cities));

        var cityCount = cities.Length;
        var stopwatch = Stopwatch.StartNew();

        // Initialize distance matrix for performance
        var distances = PrecomputeDistances(cities);

        // Initialize pheromone matrix
        var pheromones = InitializePheromones(cityCount, config.InitialPheromone);

        // Initialize best solution
        var bestTour = InitializeGreedyTour(cities, distances);
        var bestDistance = CalculateTourDistance(bestTour, distances);

        var stagnationCount = 0;
        var lastImprovementIteration = 0;

        for (int iteration = 0; iteration < config.MaxIterations && !cancellationToken.IsCancellationRequested; iteration++)
        {
            // Construct ant solutions
            var antTours = new List<int[]>();
            var antDistances = new List<double>();

            for (int antIndex = 0; antIndex < config.AntCount; antIndex++)
            {
                var tour = ConstructAntTour(distances, pheromones, config.Alpha, config.Beta);
                var distance = CalculateTourDistance(tour, distances);

                // Apply local search if enabled
                if (config.UseLocalSearch)
                {
                    tour = ApplyTwoOptImprovement(tour, distances);
                    distance = CalculateTourDistance(tour, distances);
                }

                antTours.Add(tour);
                antDistances.Add(distance);
            }

            // Find iteration best
            var iterationBestIndex = antDistances.IndexOf(antDistances.Min());
            var iterationBestTour = antTours[iterationBestIndex];
            var iterationBestDistance = antDistances[iterationBestIndex];

            // Update global best
            var improved = false;
            if (iterationBestDistance < bestDistance)
            {
                bestTour = (int[])iterationBestTour.Clone();
                bestDistance = iterationBestDistance;
                lastImprovementIteration = iteration;
                stagnationCount = 0;
                improved = true;
            }
            else
            {
                stagnationCount = iteration - lastImprovementIteration;
            }

            // Update pheromones
            UpdatePheromones(pheromones, antTours, antDistances, config.EvaporationRate, config.EliteAntCount, bestTour, bestDistance);

            // Report progress
            var isLastIteration = iteration == config.MaxIterations - 1;
            if (iteration % config.ProgressReportInterval == 0 || improved || isLastIteration)
            {
                // Yield control to allow UI updates and cancellation checks
                await Task.Yield();
                
                yield return new AntColonyResult
                {
                    Iteration = iteration,
                    BestTour = (int[])bestTour.Clone(),
                    BestDistance = bestDistance,
                    IterationBestTour = (int[])iterationBestTour.Clone(),
                    IterationBestDistance = iterationBestDistance,
                    AverageDistance = antDistances.Average(),
                    ElapsedMilliseconds = stopwatch.ElapsedMilliseconds,
                    IsComplete = isLastIteration,
                    StagnationCount = stagnationCount,
                    Config = config,
                    Statistics = new Dictionary<string, object>
                    {
                        ["MinPheromone"] = GetMinPheromone(pheromones),
                        ["MaxPheromone"] = GetMaxPheromone(pheromones),
                        ["AverageIterationDistance"] = antDistances.Average(),
                        ["BestIterationDistance"] = iterationBestDistance,
                        ["WorstIterationDistance"] = antDistances.Max()
                    }
                };
            }
        }

        // Ensure we always send a final completion result
        if (!cancellationToken.IsCancellationRequested)
        {
            await Task.Yield();
            
            yield return new AntColonyResult
            {
                Iteration = config.MaxIterations - 1,
                BestTour = (int[])bestTour.Clone(),
                BestDistance = bestDistance,
                IterationBestTour = (int[])bestTour.Clone(),
                IterationBestDistance = bestDistance,
                AverageDistance = bestDistance, // Final best is the average for completion
                ElapsedMilliseconds = stopwatch.ElapsedMilliseconds,
                IsComplete = true,
                StagnationCount = stagnationCount,
                Config = config,
                Statistics = new Dictionary<string, object>
                {
                    ["MinPheromone"] = GetMinPheromone(pheromones),
                    ["MaxPheromone"] = GetMaxPheromone(pheromones),
                    ["FinalBestDistance"] = bestDistance,
                    ["TotalIterations"] = config.MaxIterations,
                    ["FinalStagnationCount"] = stagnationCount
                }
            };
        }
    }

    /// <summary>
    /// Internal implementation of ACO algorithm
    /// </summary>
    private static Tour SolveInternal(City[] cities, AntColonyConfig config, CancellationToken cancellationToken)
    {
        var cityCount = cities.Length;

        // Initialize distance matrix
        var distances = PrecomputeDistances(cities);

        // Initialize pheromone matrix
        var pheromones = InitializePheromones(cityCount, config.InitialPheromone);

        // Initialize best solution with greedy approach
        var bestTour = InitializeGreedyTour(cities, distances);
        var bestDistance = CalculateTourDistance(bestTour, distances);

        for (int iteration = 0; iteration < config.MaxIterations && !cancellationToken.IsCancellationRequested; iteration++)
        {
            // Construct solutions for all ants
            var antTours = new List<int[]>();
            var antDistances = new List<double>();

            for (int antIndex = 0; antIndex < config.AntCount; antIndex++)
            {
                var tour = ConstructAntTour(distances, pheromones, config.Alpha, config.Beta);
                var distance = CalculateTourDistance(tour, distances);

                // Apply local search if enabled
                if (config.UseLocalSearch)
                {
                    tour = ApplyTwoOptImprovement(tour, distances);
                    distance = CalculateTourDistance(tour, distances);
                }

                antTours.Add(tour);
                antDistances.Add(distance);

                // Update best solution if improved
                if (distance < bestDistance)
                {
                    bestTour = (int[])tour.Clone();
                    bestDistance = distance;
                }
            }

            // Update pheromone trails
            UpdatePheromones(pheromones, antTours, antDistances, config.EvaporationRate, config.EliteAntCount, bestTour, bestDistance);
        }

        var resultTour = new Tour(bestTour);
        resultTour.Distance = bestDistance;
        return resultTour;
    }

    /// <summary>
    /// Precomputes distance matrix for performance optimization
    /// </summary>
    private static double[,] PrecomputeDistances(City[] cities)
    {
        var cityCount = cities.Length;
        var distances = new double[cityCount, cityCount];

        for (int i = 0; i < cityCount; i++)
        {
            for (int j = i + 1; j < cityCount; j++)
            {
                var distance = cities[i].DistanceTo(cities[j]);
                distances[i, j] = distance;
                distances[j, i] = distance; // Symmetric matrix
            }
        }

        return distances;
    }

    /// <summary>
    /// Initializes pheromone matrix with uniform values
    /// </summary>
    private static double[,] InitializePheromones(int cityCount, double initialValue)
    {
        var pheromones = new double[cityCount, cityCount];

        for (int i = 0; i < cityCount; i++)
        {
            for (int j = 0; j < cityCount; j++)
            {
                if (i != j)
                {
                    pheromones[i, j] = initialValue;
                }
            }
        }

        return pheromones;
    }

    /// <summary>
    /// Creates initial tour using nearest neighbor heuristic
    /// </summary>
    private static int[] InitializeGreedyTour(City[] cities, double[,] distances)
    {
        var cityCount = cities.Length;
        var visited = new bool[cityCount];
        var tour = new int[cityCount];

        // Start from random city
        var current = Random.Shared.Next(cityCount);
        tour[0] = current;
        visited[current] = true;

        // Build tour using nearest neighbor
        for (int step = 1; step < cityCount; step++)
        {
            var nearest = -1;
            var minDistance = double.MaxValue;

            for (int next = 0; next < cityCount; next++)
            {
                if (!visited[next] && distances[current, next] < minDistance)
                {
                    minDistance = distances[current, next];
                    nearest = next;
                }
            }

            tour[step] = nearest;
            visited[nearest] = true;
            current = nearest;
        }

        return tour;
    }

    /// <summary>
    /// Constructs a tour for a single ant using probabilistic selection
    /// </summary>
    private static int[] ConstructAntTour(double[,] distances, double[,] pheromones, double alpha, double beta)
    {
        var cityCount = distances.GetLength(0);
        var visited = new bool[cityCount];
        var tour = new int[cityCount];

        // Start from random city
        var current = Random.Shared.Next(cityCount);
        tour[0] = current;
        visited[current] = true;

        // Build tour step by step
        for (int step = 1; step < cityCount; step++)
        {
            var next = SelectNextCity(current, visited, distances, pheromones, alpha, beta);
            tour[step] = next;
            visited[next] = true;
            current = next;
        }

        return tour;
    }

    /// <summary>
    /// Selects next city for ant using pheromone and heuristic information
    /// </summary>
    private static int SelectNextCity(int current, bool[] visited, double[,] distances, double[,] pheromones, double alpha, double beta)
    {
        var cityCount = distances.GetLength(0);
        var probabilities = new double[cityCount];
        var totalProbability = 0.0;

        // Calculate selection probabilities
        for (int next = 0; next < cityCount; next++)
        {
            if (!visited[next])
            {
                var pheromoneLevel = Math.Pow(pheromones[current, next], alpha);
                var heuristicInfo = Math.Pow(1.0 / (distances[current, next] + 1e-10), beta); // Add small value to avoid division by zero
                probabilities[next] = pheromoneLevel * heuristicInfo;
                totalProbability += probabilities[next];
            }
        }

        // Roulette wheel selection
        if (totalProbability == 0.0)
        {
            // Fallback to random selection from unvisited cities
            var unvisited = new List<int>();
            for (int i = 0; i < cityCount; i++)
            {
                if (!visited[i])
                    unvisited.Add(i);
            }
            return unvisited[Random.Shared.Next(unvisited.Count)];
        }

        var randomValue = Random.Shared.NextDouble() * totalProbability;
        var cumulativeProbability = 0.0;

        for (int next = 0; next < cityCount; next++)
        {
            if (!visited[next])
            {
                cumulativeProbability += probabilities[next];
                if (cumulativeProbability >= randomValue)
                {
                    return next;
                }
            }
        }

        // Fallback (should not reach here)
        for (int i = 0; i < cityCount; i++)
        {
            if (!visited[i])
                return i;
        }

        throw new InvalidOperationException("No unvisited city found");
    }

    /// <summary>
    /// Updates pheromone trails based on ant solutions
    /// </summary>
    private static void UpdatePheromones(double[,] pheromones, List<int[]> antTours, List<double> antDistances,
        double evaporationRate, int eliteAntCount, int[] bestTour, double bestDistance)
    {
        var cityCount = pheromones.GetLength(0);

        // Evaporation phase
        for (int i = 0; i < cityCount; i++)
        {
            for (int j = 0; j < cityCount; j++)
            {
                pheromones[i, j] *= (1.0 - evaporationRate);
            }
        }

        // Pheromone deposition phase
        for (int antIndex = 0; antIndex < antTours.Count; antIndex++)
        {
            var tour = antTours[antIndex];
            var distance = antDistances[antIndex];
            var pheromoneAmount = 1.0 / distance;

            // Deposit pheromone on edges of this tour
            for (int i = 0; i < tour.Length; i++)
            {
                var from = tour[i];
                var to = tour[(i + 1) % tour.Length];
                pheromones[from, to] += pheromoneAmount;
                pheromones[to, from] += pheromoneAmount; // Symmetric
            }
        }

        // Elite ant strategy: additional pheromone from best tours
        if (eliteAntCount > 0)
        {
            var sortedIndices = antDistances
                .Select((distance, index) => new { distance, index })
                .OrderBy(x => x.distance)
                .Take(eliteAntCount)
                .ToList();

            foreach (var elite in sortedIndices)
            {
                var tour = antTours[elite.index];
                var distance = elite.distance;
                var elitePheromoneAmount = eliteAntCount / distance;

                for (int i = 0; i < tour.Length; i++)
                {
                    var from = tour[i];
                    var to = tour[(i + 1) % tour.Length];
                    pheromones[from, to] += elitePheromoneAmount;
                    pheromones[to, from] += elitePheromoneAmount;
                }
            }

            // Additional boost for global best
            var bestPheromoneAmount = eliteAntCount / bestDistance;
            for (int i = 0; i < bestTour.Length; i++)
            {
                var from = bestTour[i];
                var to = bestTour[(i + 1) % bestTour.Length];
                pheromones[from, to] += bestPheromoneAmount;
                pheromones[to, from] += bestPheromoneAmount;
            }
        }
    }

    /// <summary>
    /// Applies 2-opt local search improvement to a tour
    /// </summary>
    private static int[] ApplyTwoOptImprovement(int[] tour, double[,] distances)
    {
        var improved = true;
        var currentTour = (int[])tour.Clone();
        var currentDistance = CalculateTourDistance(currentTour, distances);

        while (improved)
        {
            improved = false;

            for (int i = 1; i < currentTour.Length - 2; i++)
            {
                for (int j = i + 1; j < currentTour.Length; j++)
                {
                    if (j == currentTour.Length - 1 && i == 1)
                        continue; // Skip reverse of entire tour

                    var newDistance = CalculateNewDistanceAfter2Opt(currentTour, distances, i, j, currentDistance);

                    if (newDistance < currentDistance)
                    {
                        // Apply 2-opt swap
                        Array.Reverse(currentTour, i, j - i + 1);
                        currentDistance = newDistance;
                        improved = true;
                    }
                }
            }
        }

        return currentTour;
    }

    /// <summary>
    /// Calculates new distance after potential 2-opt move
    /// </summary>
    private static double CalculateNewDistanceAfter2Opt(int[] tour, double[,] distances, int i, int j, double currentDistance)
    {
        var n = tour.Length;

        // Get the four cities involved in the 2-opt swap
        var city1 = tour[(i - 1 + n) % n];
        var city2 = tour[i];
        var city3 = tour[j];
        var city4 = tour[(j + 1) % n];

        // Calculate the change in distance
        var oldDistance = distances[city1, city2] + distances[city3, city4];
        var newDistance = distances[city1, city3] + distances[city2, city4];

        return currentDistance - oldDistance + newDistance;
    }

    /// <summary>
    /// Calculates total distance of a tour
    /// </summary>
    private static double CalculateTourDistance(int[] tour, double[,] distances)
    {
        var totalDistance = 0.0;

        for (int i = 0; i < tour.Length; i++)
        {
            var from = tour[i];
            var to = tour[(i + 1) % tour.Length];
            totalDistance += distances[from, to];
        }

        return totalDistance;
    }

    /// <summary>
    /// Gets minimum pheromone value in the matrix
    /// </summary>
    private static double GetMinPheromone(double[,] pheromones)
    {
        var min = double.MaxValue;
        var size = pheromones.GetLength(0);

        for (int i = 0; i < size; i++)
        {
            for (int j = 0; j < size; j++)
            {
                if (i != j && pheromones[i, j] < min)
                {
                    min = pheromones[i, j];
                }
            }
        }

        return min;
    }

    /// <summary>
    /// Gets maximum pheromone value in the matrix
    /// </summary>
    private static double GetMaxPheromone(double[,] pheromones)
    {
        var max = 0.0;
        var size = pheromones.GetLength(0);

        for (int i = 0; i < size; i++)
        {
            for (int j = 0; j < size; j++)
            {
                if (i != j && pheromones[i, j] > max)
                {
                    max = pheromones[i, j];
                }
            }
        }

        return max;
    }
}
