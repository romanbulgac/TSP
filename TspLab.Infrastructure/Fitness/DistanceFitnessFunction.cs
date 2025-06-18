using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using TspLab.Domain.Entities;
using TspLab.Domain.Interfaces;

namespace TspLab.Infrastructure.Fitness;

/// <summary>
/// Distance-based fitness function with caching for performance
/// Supports both Euclidean distance calculation and pre-computed distance matrices
/// </summary>
public sealed class DistanceFitnessFunction : IFitnessFunction
{
    private readonly IMemoryCache _cache;
    private readonly ILogger<DistanceFitnessFunction> _logger;
    private readonly object _lockObject = new();
    private MemoryCache? memoryCache;
    
    // Distance matrix for TSPLIB problems (if available)
    private double[,]? _distanceMatrix;

    public string Name => "DistanceFitness";

    public DistanceFitnessFunction(IMemoryCache cache, ILogger<DistanceFitnessFunction> logger)
    {
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public DistanceFitnessFunction(MemoryCache memoryCache)
    {
        this.memoryCache = memoryCache;
        _cache = memoryCache;
        _logger = null!; // Will need to handle null logger case
    }

    /// <summary>
    /// Constructor with distance matrix support for TSPLIB problems
    /// </summary>
    public DistanceFitnessFunction(IMemoryCache cache, ILogger<DistanceFitnessFunction> logger, double[,]? distanceMatrix)
        : this(cache, logger)
    {
        _distanceMatrix = distanceMatrix;
    }

    /// <summary>
    /// Sets the distance matrix for TSPLIB problems
    /// </summary>
    public void SetDistanceMatrix(double[,]? distanceMatrix)
    {
        _distanceMatrix = distanceMatrix;
    }

    public double CalculateFitness(Tour tour, ReadOnlySpan<City> cities)
    {
        var distance = CalculateDistance(tour, cities);

        // Ensure distance is valid
        if (distance <= 0 || double.IsNaN(distance) || double.IsInfinity(distance) || distance == double.MaxValue)
        {
            _logger?.LogWarning("Invalid distance detected: {Distance}. Returning fitness=0", distance);
            return 0.0;
        }

        // Fitness is inverse of distance (higher fitness = shorter distance)
        // Use standard 1/(distance + 1) formula to match test expectations
        var fitness = 100000.0 / (distance + 1.0);


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
                _logger?.LogError("Invalid city index: from={FromIndex}, to={ToIndex}, citiesLength={CitiesLength}",
                    fromCityIndex, toCityIndex, cities.Length);
                return double.MaxValue;
            }

            var fromCity = cities[fromCityIndex];
            var toCity = cities[toCityIndex];
            
            double segmentDistance;
            
            // Use distance matrix if available, otherwise calculate Euclidean distance
            if (_distanceMatrix != null && 
                fromCityIndex < _distanceMatrix.GetLength(0) && 
                toCityIndex < _distanceMatrix.GetLength(1))
            {
                segmentDistance = _distanceMatrix[fromCityIndex, toCityIndex];
            }
            else
            {
                segmentDistance = fromCity.DistanceTo(toCity);
            }

            // Validate segment distance
            if (double.IsNaN(segmentDistance) || double.IsInfinity(segmentDistance))
            {
                _logger?.LogError("Invalid segment distance: {SegmentDistance}", segmentDistance);
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
