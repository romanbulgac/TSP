using TspLab.Domain.Entities;
using TspLab.Domain.Interfaces;

namespace TspLab.Infrastructure.Crossover;

/// <summary>
/// Order Crossover (OX) operator for TSP
/// Preserves the relative order of cities from one parent while filling in the remaining cities from the other parent
/// </summary>
public sealed class OrderCrossover : ICrossover
{
    /// <summary>
    /// Gets the name of this crossover operator
    /// </summary>
    public string Name => "OrderCrossover";

    /// <summary>
    /// Gets a description of this crossover operator
    /// </summary>
    public string Description => "Order Crossover (OX) - Preserves relative order of cities from one parent";

    /// <summary>
    /// Performs order crossover between two parent tours
    /// </summary>
    /// <param name="parent1">First parent tour</param>
    /// <param name="parent2">Second parent tour</param>
    /// <param name="random">Random number generator</param>
    /// <returns>Tuple containing two offspring tours</returns>
    /// <exception cref="ArgumentNullException">Thrown when parents are null</exception>
    /// <exception cref="ArgumentException">Thrown when parents have different lengths</exception>
    public (Tour offspring1, Tour offspring2) Crossover(Tour parent1, Tour parent2, Random random)
    {
        ArgumentNullException.ThrowIfNull(parent1);
        ArgumentNullException.ThrowIfNull(parent2);
        ArgumentNullException.ThrowIfNull(random);
        
        if (parent1.Length != parent2.Length)
            throw new ArgumentException("Parent tours must have the same length");

        if (parent1.Length < 3)
            throw new ArgumentException("Tours must have at least 3 cities");

        return (
            CreateOffspring(parent1, parent2, random),
            CreateOffspring(parent2, parent1, random)
        );
    }

    /// <summary>
    /// Creates an offspring using order crossover
    /// </summary>
    /// </summary>
    /// <param name="primary">Primary parent (order source)</param>
    /// <param name="secondary">Secondary parent (filler source)</param>
    /// <param name="random">Random number generator</param>
    /// <returns>New offspring tour</returns>
    private static Tour CreateOffspring(Tour primary, Tour secondary, Random random)
    {
        var length = primary.Length;
        var offspring = new int[length];
        var used = new bool[length];

        // Select random crossover points
        var start = random.Next(length);
        var end = random.Next(length);
        
        // Ensure start <= end
        if (start > end)
            (start, end) = (end, start);

        // Copy the selected segment from primary parent
        var primaryCities = primary.Cities;
        for (int i = start; i <= end; i++)
        {
            var cityIndex = primaryCities[i];
            offspring[i] = cityIndex;
            used[cityIndex] = true;
        }

        // Fill remaining positions with cities from secondary parent in order
        var secondaryCities = secondary.Cities;
        var fillIndex = (end + 1) % length;
        
        for (int i = 0; i < length; i++)
        {
            var secondaryIndex = (end + 1 + i) % length;
            var cityIndex = secondaryCities[secondaryIndex];
            
            if (!used[cityIndex])
            {
                offspring[fillIndex] = cityIndex;
                used[cityIndex] = true;
                fillIndex = (fillIndex + 1) % length;
            }
        }

        return new Tour(offspring);
    }
}
