using TspLab.Domain.Entities;
using TspLab.Domain.Interfaces;

namespace TspLab.Application.Heuristics;

/// <summary>
/// Implements the Simulated Annealing metaheuristic for solving TSP.
/// Uses probabilistic acceptance of worse solutions to escape local optima,
/// with temperature cooling over time following the Metropolis criterion.
/// </summary>
public sealed class SimulatedAnnealingSolver : ITspSolver
{
    private const double InitialTemperature = 1000.0;
    private const double FinalTemperature = 1.0;
    private const double CoolingRate = 0.995;
    private const int MaxIterations = 10000;

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

        return Task.FromResult(SolveInternal(cities, cancellationToken));
    }

    /// <summary>
    /// Internal implementation of the Simulated Annealing algorithm.
    /// </summary>
    /// <param name="cities">The array of cities to visit.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The best tour found by simulated annealing.</returns>
    private static Tour SolveInternal(City[] cities, CancellationToken cancellationToken)
    {
        // Generate initial random tour
        var currentTour = GenerateRandomTour(cities.Length);
        var currentDistance = CalculateTourDistance(cities, currentTour);
        
        // Keep track of the best solution found
        var bestTour = (int[])currentTour.Clone();
        var bestDistance = currentDistance;
        
        var temperature = InitialTemperature;
        var iteration = 0;

        while (temperature > FinalTemperature && iteration < MaxIterations && !cancellationToken.IsCancellationRequested)
        {
            // Generate neighbor by swapping two random cities
            var neighborTour = GenerateNeighbor(currentTour);
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
                }
            }
            
            // Cool down the temperature
            temperature *= CoolingRate;
            iteration++;
        }

        var resultTour = new Tour(bestTour);
        resultTour.Distance = bestDistance;
        return resultTour;
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
    /// Generates a neighbor solution by swapping two random cities in the tour.
    /// This is the perturbation mechanism for simulated annealing.
    /// </summary>
    /// <param name="currentTour">The current tour to perturb.</param>
    /// <returns>A new tour with two cities swapped.</returns>
    private static int[] GenerateNeighbor(int[] currentTour)
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
