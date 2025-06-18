using TspLab.Domain.Entities;
using TspLab.Domain.Interfaces;

namespace TspLab.Application.Heuristics;

/// <summary>
/// Implements the 2-opt local search algorithm for solving TSP.
/// Starts with a Nearest Neighbor solution and iteratively improves it
/// by reversing segments of the tour to eliminate edge crossings.
/// </summary>
public sealed class TwoOptSolver : ITspSolver
{
    private readonly NearestNeighborSolver _nearestNeighborSolver;

    /// <summary>
    /// Initializes a new instance of the TwoOptSolver class.
    /// </summary>
    public TwoOptSolver()
    {
        _nearestNeighborSolver = new NearestNeighborSolver();
    }

    /// <inheritdoc />
    public string Name => "2-opt";

    /// <inheritdoc />
    public string Description => "Local search algorithm that starts with Nearest Neighbor and improves by reversing tour segments to eliminate crossings";

    /// <inheritdoc />
    public async Task<Tour> SolveAsync(City[] cities, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(cities);

        if (cities.Length == 0)
            throw new ArgumentException("Cannot solve TSP with no cities", nameof(cities));

        if (cities.Length <= 2)
        {
            // For trivial cases, return the simple tour
            var trivialTour = new Tour(Enumerable.Range(0, cities.Length).ToArray());
            trivialTour.Distance = cities.Length == 2 ? 2 * cities[0].DistanceTo(cities[1]) : 0;
            return trivialTour;
        }

        // Start with Nearest Neighbor solution
        var initialTour = await _nearestNeighborSolver.SolveAsync(cities, cancellationToken);

        // Apply 2-opt improvements
        var improvedTour = await Task.Run(() => ApplyTwoOptImprovement(cities, initialTour, cancellationToken), cancellationToken);

        return improvedTour;
    }

    /// <summary>
    /// Applies 2-opt improvements to a tour until no further improvements can be made.
    /// </summary>
    /// <param name="cities">The array of all cities.</param>
    /// <param name="initialTour">The initial tour to improve.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The improved tour after 2-opt optimization.</returns>
    private static Tour ApplyTwoOptImprovement(City[] cities, Tour initialTour, CancellationToken cancellationToken)
    {
        var currentTour = initialTour.Cities.ToArray();
        var currentDistance = initialTour.Distance;
        var improved = true;
        var maxIterations = 1000; // Prevent infinite loops
        var iteration = 0;

        while (improved && iteration < maxIterations && !cancellationToken.IsCancellationRequested)
        {
            improved = false;
            iteration++;

            // Try all possible 2-opt swaps
            for (var i = 1; i < currentTour.Length - 2; i++)
            {
                for (var j = i + 1; j < currentTour.Length; j++)
                {
                    if (j == currentTour.Length - 1 && i == 1)
                        continue; // Skip if it would just reverse the entire tour

                    var newDistance = CalculateNewDistanceAfter2Opt(cities, currentTour, i, j, currentDistance);

                    if (newDistance < currentDistance)
                    {
                        // Apply the 2-opt swap
                        Reverse2OptSegment(currentTour, i, j);
                        currentDistance = newDistance;
                        improved = true;
                    }
                }
            }
        }

        var optimizedTour = new Tour(currentTour);
        optimizedTour.Distance = currentDistance;
        return optimizedTour;
    }

    /// <summary>
    /// Calculates the new tour distance if a 2-opt swap were applied.
    /// </summary>
    /// <param name="cities">The array of all cities.</param>
    /// <param name="tour">The current tour.</param>
    /// <param name="i">The first index of the segment to reverse.</param>
    /// <param name="j">The last index of the segment to reverse.</param>
    /// <param name="currentDistance">The current tour distance.</param>
    /// <returns>The new distance after the hypothetical 2-opt swap.</returns>
    private static double CalculateNewDistanceAfter2Opt(City[] cities, int[] tour, int i, int j, double currentDistance)
    {
        var cityCount = tour.Length;

        // Get the four cities involved in the 2-opt swap
        var city1 = cities[tour[(i - 1 + cityCount) % cityCount]]; // City before the segment
        var city2 = cities[tour[i]];                                // First city of segment
        var city3 = cities[tour[j]];                                // Last city of segment
        var city4 = cities[tour[(j + 1) % cityCount]];             // City after the segment

        // Calculate the change in distance
        var oldDistance = city1.DistanceTo(city2) + city3.DistanceTo(city4);
        var newDistance = city1.DistanceTo(city3) + city2.DistanceTo(city4);

        return currentDistance - oldDistance + newDistance;
    }

    /// <summary>
    /// Reverses a segment of the tour between indices i and j (inclusive).
    /// This is the core 2-opt operation that eliminates edge crossings.
    /// </summary>
    /// <param name="tour">The tour array to modify.</param>
    /// <param name="i">The start index of the segment to reverse.</param>
    /// <param name="j">The end index of the segment to reverse.</param>
    private static void Reverse2OptSegment(int[] tour, int i, int j)
    {
        while (i < j)
        {
            (tour[i], tour[j]) = (tour[j], tour[i]);
            i++;
            j--;
        }
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
