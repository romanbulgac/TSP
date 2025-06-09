namespace TspLab.Domain.Entities;

/// <summary>
/// Represents a tour (solution) in the TSP
/// </summary>
public sealed class Tour : IEquatable<Tour>
{
    private readonly int[] _cities;
    private double? _fitness;
    private double? _distance;

    /// <summary>
    /// Initializes a new tour with the given city sequence
    /// </summary>
    /// <param name="cities">Array of city indices representing the tour order</param>
    public Tour(int[] cities)
    {
        ArgumentNullException.ThrowIfNull(cities);
        if (cities.Length == 0)
            throw new ArgumentException("Tour cannot be empty", nameof(cities));
            
        _cities = new int[cities.Length];
        Array.Copy(cities, _cities, cities.Length);
    }

    /// <summary>
    /// Gets the city indices in tour order
    /// </summary>
    public ReadOnlySpan<int> Cities => _cities.AsSpan();

    /// <summary>
    /// Gets the number of cities in the tour
    /// </summary>
    public int Length => _cities.Length;

    /// <summary>
    /// Gets or sets the fitness value (cached)
    /// </summary>
    public double Fitness
    {
        get => _fitness ?? 0.0;
        set => _fitness = value;
    }

    /// <summary>
    /// Gets or sets the total distance (cached)
    /// </summary>
    public double Distance
    {
        get => _distance ?? 0.0;
        set => _distance = value;
    }

    /// <summary>
    /// Gets the city at the specified index
    /// </summary>
    public int this[int index] => _cities[index];

    /// <summary>
    /// Creates a copy of this tour
    /// </summary>
    public Tour Clone() 
    {
        var clone = new Tour(_cities);
        clone._fitness = _fitness;
        clone._distance = _distance;
        return clone;
    }

    /// <summary>
    /// Swaps two cities in the tour
    /// </summary>
    public void Swap(int index1, int index2)
    {
        if (index1 < 0 || index1 >= _cities.Length)
            throw new ArgumentOutOfRangeException(nameof(index1));
        if (index2 < 0 || index2 >= _cities.Length)
            throw new ArgumentOutOfRangeException(nameof(index2));

        (_cities[index1], _cities[index2]) = (_cities[index2], _cities[index1]);
        InvalidateCache();
    }

    /// <summary>
    /// Reverses a segment of the tour
    /// </summary>
    public void ReverseSegment(int start, int end)
    {
        if (start < 0 || start >= _cities.Length)
            throw new ArgumentOutOfRangeException(nameof(start));
        if (end < 0 || end >= _cities.Length)
            throw new ArgumentOutOfRangeException(nameof(end));

        if (start > end)
            (start, end) = (end, start);

        Array.Reverse(_cities, start, end - start + 1);
        InvalidateCache();
    }

    /// <summary>
    /// Validates that the tour contains all cities exactly once
    /// </summary>
    public bool IsValid()
    {
        var seen = new HashSet<int>();
        return _cities.All(city => seen.Add(city));
    }

    private void InvalidateCache()
    {
        _fitness = null;
        _distance = null;
    }

    public bool Equals(Tour? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        return _cities.SequenceEqual(other._cities);
    }

    public override bool Equals(object? obj) => obj is Tour tour && Equals(tour);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (var city in _cities)
            hash.Add(city);
        return hash.ToHashCode();
    }

    public override string ToString() => $"Tour[{string.Join(" -> ", _cities)}]";
}
