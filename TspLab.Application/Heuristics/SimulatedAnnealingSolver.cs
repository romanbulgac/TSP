using TspLab.Domain.Entities;
using TspLab.Domain.Interfaces;
using TspLab.Domain.Models;

namespace TspLab.Application.Heuristics;

/// <summary>
/// Implements the Simulated Annealing metaheuristic for solving TSP.
/// Uses probabilistic acceptance of worse solutions to escape local optima,
/// with temperature cooling over time following the Metropolis criterion.
/// </summary>
public sealed class SimulatedAnnealingSolver : ITspSolver
{
    // Adaptive parameters based on problem size
    private static double GetInitialTemperature(int cityCount) => Math.Max(1000.0, cityCount * 50.0);
    private static double GetFinalTemperature(int cityCount) => Math.Max(0.1, cityCount * 0.01);
    private static double GetCoolingRate(int cityCount) => cityCount > 50 ? 0.9995 : 0.995;
    private static int GetMaxIterations(int cityCount) => Math.Max(10000, cityCount * cityCount * 10);

    /// <inheritdoc />
    public string Name => "Simulated Annealing";

    /// <inheritdoc />
    public string Description => "Metaheuristic that uses probabilistic acceptance of worse solutions to escape local optima, cooling temperature over time";

    /// <inheritdoc />
    public Task<Tour> SolveAsync(City[] cities, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(cities);

        if (cities.Length == 0)
            throw new ArgumentException("Cannot solve TSP with no cities", nameof(cities));

        if (cities.Length == 1)
            return Task.FromResult(new Tour([0]));

        if (cities.Length == 2)
        {
            var trivialTour = new Tour([0, 1]);
            trivialTour.Distance = 2 * cities[0].DistanceTo(cities[1]);
            return Task.FromResult(trivialTour);
        }

        // Use adaptive configuration based on problem size
        var config = SimulatedAnnealingConfig.ForProblemSize(cities.Length);
        return Task.FromResult(SolveInternal(cities, config, cancellationToken));
    }

    /// <summary>
    /// Solves TSP using Simulated Annealing with custom configuration
    /// </summary>
    /// <param name="cities">Array of cities to visit</param>
    /// <param name="config">SA algorithm configuration</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Best tour found</returns>
    public async Task<Tour> SolveAsync(City[] cities, SimulatedAnnealingConfig config, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(cities);
        ArgumentNullException.ThrowIfNull(config);

        if (!config.IsValid())
            throw new ArgumentException("Invalid SA configuration", nameof(config));

        return await Task.Run(() => SolveInternal(cities, config, cancellationToken), cancellationToken);
    }

    /// <summary>
    /// Internal implementation of the Simulated Annealing algorithm.
    /// </summary>
    /// <param name="cities">The array of cities to visit.</param>
    /// <param name="config">SA configuration parameters.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The best tour found by simulated annealing.</returns>
    private static Tour SolveInternal(City[] cities, SimulatedAnnealingConfig config, CancellationToken cancellationToken)
    {
        var cityCount = cities.Length;
        
        // Use configuration parameters
        var initialTemperature = config.InitialTemperature;
        var finalTemperature = config.FinalTemperature;
        var coolingRate = config.CoolingRate;
        var maxIterations = config.MaxIterations;

        // Start with nearest neighbor heuristic or random tour based on configuration
        var currentTour = config.UseNearestNeighborInitialization 
            ? GenerateNearestNeighborTour(cities)
            : GenerateRandomTour(cityCount);
        var currentDistance = CalculateTourDistance(cities, currentTour);

        // Keep track of the best solution found
        var bestTour = (int[])currentTour.Clone();
        var bestDistance = currentDistance;

        var temperature = initialTemperature;
        var iteration = 0;
        var improvementCount = 0;

        while (temperature > finalTemperature && iteration < maxIterations && !cancellationToken.IsCancellationRequested)
        {
            // Use multiple neighborhood operators for better exploration
            var neighborTour = Random.Shared.NextDouble() < config.TwoOptProbability 
                ? Generate2OptNeighbor(currentTour) 
                : GenerateSwapNeighbor(currentTour);
            
            var neighborDistance = CalculateTourDistance(cities, neighborTour);

            // Calculate energy difference (negative because we minimize distance)
            var deltaE = neighborDistance - currentDistance;

            // Accept the neighbor based on Metropolis criterion
            if (ShouldAcceptSolution(deltaE, temperature))
            {
                currentTour = neighborTour;
                currentDistance = neighborDistance;

                // Update best solution if this is better
                if (currentDistance < bestDistance)
                {
                    bestTour = (int[])currentTour.Clone();
                    bestDistance = currentDistance;
                    improvementCount++;
                }
            }

            // Adaptive cooling with occasional reheating if enabled
            if (config.EnableAdaptiveReheating && 
                iteration % config.ReheatCheckInterval == 0 && 
                improvementCount == 0)
            {
                temperature *= 1.1; // Reheat slightly if no improvements
            }
            else
            {
                temperature *= coolingRate;
            }
            
            iteration++;
            
            // Reset improvement counter periodically
            if (iteration % config.ReheatCheckInterval == 0)
                improvementCount = 0;
        }

        var resultTour = new Tour(bestTour);
        resultTour.Distance = bestDistance;
        return resultTour;
    }

    /// <summary>
    /// Generates an initial tour using nearest neighbor heuristic instead of random.
    /// </summary>
    /// <param name="cities">The array of cities.</param>
    /// <returns>A nearest neighbor tour as an array of city indices.</returns>
    private static int[] GenerateNearestNeighborTour(City[] cities)
    {
        var cityCount = cities.Length;
        var visited = new bool[cityCount];
        var tour = new int[cityCount];

        // Start from a random city
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

    /// <summary>
    /// Generates an initial random tour by shuffling city indices.
    /// </summary>
    /// <param name="cityCount">The number of cities in the problem.</param>
    /// <returns>A random tour as an array of city indices.</returns>
    private static int[] GenerateRandomTour(int cityCount)
    {
        var tour = Enumerable.Range(0, cityCount).ToArray();

        // Fisher-Yates shuffle for uniform randomness
        for (var i = tour.Length - 1; i > 0; i--)
        {
            var j = Random.Shared.Next(i + 1);
            (tour[i], tour[j]) = (tour[j], tour[i]);
        }

        return tour;
    }

    /// <summary>
    /// Generates a neighbor solution using 2-opt move (more effective than simple swap).
    /// </summary>
    /// <param name="currentTour">The current tour to perturb.</param>
    /// <returns>A new tour with a 2-opt improvement applied.</returns>
    private static int[] Generate2OptNeighbor(int[] currentTour)
    {
        var neighbor = (int[])currentTour.Clone();
        var length = neighbor.Length;

        // Select two random positions for 2-opt move
        var i = Random.Shared.Next(length);
        var j = Random.Shared.Next(length);

        // Ensure i < j and they're not adjacent
        if (i > j) (i, j) = (j, i);
        if (j - i < 2) return neighbor; // Skip if too close

        // Reverse the segment between i+1 and j
        Array.Reverse(neighbor, i + 1, j - i);

        return neighbor;
    }

    /// <summary>
    /// Generates a neighbor solution by swapping two random cities.
    /// </summary>
    /// <param name="currentTour">The current tour to perturb.</param>
    /// <returns>A new tour with two cities swapped.</returns>
    private static int[] GenerateSwapNeighbor(int[] currentTour)
    {
        var neighbor = (int[])currentTour.Clone();

        // Select two random distinct positions
        var pos1 = Random.Shared.Next(neighbor.Length);
        var pos2 = Random.Shared.Next(neighbor.Length);

        // Ensure positions are different
        while (pos2 == pos1)
        {
            pos2 = Random.Shared.Next(neighbor.Length);
        }

        // Swap the cities at these positions
        (neighbor[pos1], neighbor[pos2]) = (neighbor[pos2], neighbor[pos1]);

        return neighbor;
    }

    /// <summary>
    /// Determines whether to accept a solution based on the Metropolis criterion.
    /// Always accepts improving solutions, probabilistically accepts worse solutions.
    /// </summary>
    /// <param name="deltaE">The energy difference (positive means worse solution).</param>
    /// <param name="temperature">The current temperature.</param>
    /// <returns>True if the solution should be accepted, false otherwise.</returns>
    private static bool ShouldAcceptSolution(double deltaE, double temperature)
    {
        // Always accept improving solutions
        if (deltaE <= 0)
            return true;

        // Accept worse solutions with probability exp(-deltaE/T)
        var acceptanceProbability = Math.Exp(-deltaE / temperature);
        var randomValue = Random.Shared.NextDouble();

        return randomValue < acceptanceProbability;
    }

    /// <summary>
    /// Calculates the total distance of a tour.
    /// </summary>
    /// <param name="cities">The array of all cities.</param>
    /// <param name="tour">The tour sequence as city indices.</param>
    /// <returns>The total distance of the tour.</returns>
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
