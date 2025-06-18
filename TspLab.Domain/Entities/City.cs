namespace TspLab.Domain.Entities;

/// <summary>
/// Represents a city in the TSP with coordinates and metadata
/// </summary>
public sealed record City(
    int Id,
    string Name,
    double X,
    double Y)
{
    /// <summary>
    /// Calculates the Euclidean distance to another city
    /// </summary>
    /// <param name="other">The other city</param>
    /// <returns>Euclidean distance between the cities</returns>
    public double DistanceTo(City other)
    {
        ArgumentNullException.ThrowIfNull(other);

        var dx = X - other.X;
        var dy = Y - other.Y;
        return Math.Sqrt(dx * dx + dy * dy);
    }

    /// <summary>
    /// Calculates the squared distance to another city (faster for comparisons)
    /// </summary>
    /// <param name="other">The other city</param>
    /// <returns>Squared Euclidean distance between the cities</returns>
    public double SquaredDistanceTo(City other)
    {
        ArgumentNullException.ThrowIfNull(other);

        var dx = X - other.X;
        var dy = Y - other.Y;
        return dx * dx + dy * dy;
    }

    /// <summary>
    /// Gets a string representation of the city
    /// </summary>
    /// <returns>String representation including name and coordinates</returns>
    public override string ToString()
    {
        return $"{Name} ({X:F2}, {Y:F2})";
    }
}
