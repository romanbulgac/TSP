using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using TspLab.Domain.Entities;
using TspLab.Domain.Interfaces;

namespace TspLab.Infrastructure.Fitness;

/// <summary>
/// Distance-based fitness function with caching for performance
/// </summary>
public sealed class DistanceFitnessFunction : IFitnessFunction
{
    private readonly IMemoryCache _cache;
    private readonly ILogger<DistanceFitnessFunction> _logger;
    private readonly object _lockObject = new();
    private MemoryCache memoryCache;

    public string Name => "DistanceFitness";

    public DistanceFitnessFunction(IMemoryCache cache, ILogger<DistanceFitnessFunction> logger)
    {
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public DistanceFitnessFunction(MemoryCache memoryCache)
    {
        this.memoryCache = memoryCache;
    }

    public double CalculateFitness(Tour tour, ReadOnlySpan<City> cities)
    {
        var distance = CalculateDistance(tour, cities);
        
        _logger.LogDebug("Calculating fitness for tour: Distance={Distance}", distance);
        
        // Ensure distance is valid
        if (distance <= 0 || double.IsNaN(distance) || double.IsInfinity(distance) || distance == double.MaxValue)
        {
            _logger.LogWarning("Invalid distance detected: {Distance}. Returning fitness=0", distance);
            return 0.0;
        }
        
        // Fitness is inverse of distance (higher fitness = shorter distance)
        // Use 1/distance formula with scaling for better visualization
        var fitness = 1000.0 / distance;
        
        _logger.LogDebug("Calculated fitness: Distance={Distance}, Fitness={Fitness}", distance, fitness);
        
        return fitness;
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

        // Validate tour length
        if (tourCities.Length == 0)
        {
            return double.MaxValue;
        }

        // Calculate total distance
        for (int i = 0; i < tourCities.Length; i++)
        {
            var fromCityIndex = tourCities[i];
            var toCityIndex = tourCities[(i + 1) % tourCities.Length];
            
            // Validate city indices
            if (fromCityIndex < 0 || fromCityIndex >= cities.Length ||
                toCityIndex < 0 || toCityIndex >= cities.Length)
            {
                _logger.LogError("Invalid city index: from={FromIndex}, to={ToIndex}, citiesLength={CitiesLength}", 
                    fromCityIndex, toCityIndex, cities.Length);
                return double.MaxValue;
            }
            
            var fromCity = cities[fromCityIndex];
            var toCity = cities[toCityIndex];
            var segmentDistance = fromCity.DistanceTo(toCity);
            
            // Validate segment distance
            if (double.IsNaN(segmentDistance) || double.IsInfinity(segmentDistance))
            {
                _logger.LogError("Invalid segment distance: {SegmentDistance}", segmentDistance);
                return double.MaxValue;
            }
            
            distance += segmentDistance;
        }

        // Validate total distance
        if (double.IsNaN(distance) || double.IsInfinity(distance) || distance < 0)
        {
            return double.MaxValue;
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
