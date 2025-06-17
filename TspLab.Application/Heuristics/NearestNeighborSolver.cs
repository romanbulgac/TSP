using TspLab.Domain.Entities;
using TspLab.Domain.Interfaces;

namespace TspLab.Application.Heuristics;

/// <summary>
/// Implements the Nearest Neighbor heuristic for solving TSP.
/// This greedy algorithm starts from a random city and always visits
/// the nearest unvisited city next, then returns to the starting city.
/// </summary>
public sealed class NearestNeighborSolver : ITspSolver
{
    /// <inheritdoc />
    public string Name => "Nearest Neighbor";

    /// <inheritdoc />
    public string Description => "Greedy algorithm that starts from a random city and always visits the nearest unvisited city next";

    /// <inheritdoc />
    public Task<Tour> SolveAsync(City[] cities, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(cities);
        
        if (cities.Length == 0)
            throw new ArgumentException("Cannot solve TSP with no cities", nameof(cities));
        
        if (cities.Length == 1)
            return Task.FromResult(new Tour([0]));
        
        return Task.FromResult(SolveInternal(cities));
    }

    /// <summary>
    /// Internal implementation of the Nearest Neighbor algorithm.
    /// </summary>
    /// <param name="cities">The array of cities to visit.</param>
    /// <returns>The tour found by the nearest neighbor heuristic.</returns>
    private static Tour SolveInternal(City[] cities)
    {
        var cityCount = cities.Length;
        var visited = new bool[cityCount];
        var tourCities = new int[cityCount];
        
        // Start from a random city
        var startCity = Random.Shared.Next(cityCount);
        var currentCity = startCity;
        
        visited[currentCity] = true;
        tourCities[0] = currentCity;
        
        // Visit remaining cities using nearest neighbor
        for (var step = 1; step < cityCount; step++)
        {
            var nearestCity = FindNearestUnvisitedCity(cities, currentCity, visited);
            
            visited[nearestCity] = true;
            tourCities[step] = nearestCity;
            currentCity = nearestCity;
        }
        
        var tour = new Tour(tourCities);
        tour.Distance = CalculateTourDistance(cities, tourCities);
        
        return tour;
    }

    /// <summary>
    /// Finds the nearest unvisited city to the current city.
    /// </summary>
    /// <param name="cities">The array of all cities.</param>
    /// <param name="currentCityIndex">The index of the current city.</param>
    /// <param name="visited">Array tracking which cities have been visited.</param>
    /// <returns>The index of the nearest unvisited city.</returns>
    private static int FindNearestUnvisitedCity(City[] cities, int currentCityIndex, bool[] visited)
    {
        var nearestCity = -1;
        var shortestDistance = double.MaxValue;
        var currentCity = cities[currentCityIndex];
        
        for (var i = 0; i < cities.Length; i++)
        {
            if (!visited[i])
            {
                var distance = currentCity.DistanceTo(cities[i]);
                if (distance < shortestDistance)
                {
                    shortestDistance = distance;
                    nearestCity = i;
                }
            }
        }
        
        return nearestCity;
    }

    /// <summary>
    /// Calculates the total distance of a tour.
    /// </summary>
    /// <param name="cities">The array of all cities.</param>
    /// <param name="tourCities">The tour sequence as city indices.</param>
    /// <returns>The total distance of the tour.</returns>
    private static double CalculateTourDistance(City[] cities, int[] tourCities)
    {
        var totalDistance = 0.0;
        
        // Calculate distance between consecutive cities
        for (var i = 0; i < tourCities.Length - 1; i++)
        {
            totalDistance += cities[tourCities[i]].DistanceTo(cities[tourCities[i + 1]]);
        }
        
        // Add distance from last city back to first city
        if (tourCities.Length > 1)
        {
            totalDistance += cities[tourCities[^1]].DistanceTo(cities[tourCities[0]]);
        }
        
        return totalDistance;
    }
}
