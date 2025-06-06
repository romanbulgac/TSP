using Microsoft.Extensions.Caching.Memory;

namespace TspLab.Infrastructure.Fitness;

/// <summary>
/// Distance-based fitness function with caching for performance
/// </summary>
public sealed class DistanceFitnessFunction : IFitnessFunction
{
    private readonly IMemoryCache _cache;
    private readonly object _lockObject = new();

    public string Name => "DistanceFitness";

    public DistanceFitnessFunction(IMemoryCache cache)
    {
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
    }

    public double CalculateFitness(Tour tour, ReadOnlySpan<City> cities)
    {
        var distance = CalculateDistance(tour, cities);
        
        // Fitness is inverse of distance (higher fitness = shorter distance)
        // Add small constant to avoid division by zero
        return 1.0 / (distance + 1.0);
    }

    public double CalculateDistance(Tour tour, ReadOnlySpan<City> cities)
    {
        ArgumentNullException.ThrowIfNull(tour);

        // Create cache key from tour
        var cacheKey = CreateCacheKey(tour);

        if (_cache.TryGetValue(cacheKey, out double cachedDistance))
            return cachedDistance;

        var distance = 0.0;
        var tourCities = tour.Cities;

        // Calculate total distance
        for (int i = 0; i < tourCities.Length; i++)
        {
            var fromCity = cities[tourCities[i]];
            var toCity = cities[tourCities[(i + 1) % tourCities.Length]];
            distance += fromCity.DistanceTo(toCity);
        }

        // Cache the result with a sliding expiration
        lock (_lockObject)
        {
            _cache.Set(cacheKey, distance, TimeSpan.FromMinutes(5));
        }

        return distance;
    }

    private static string CreateCacheKey(Tour tour)
    {
        // Simple hash-based cache key
        var hash = tour.GetHashCode();
        return $"distance_{hash}";
    }
}
