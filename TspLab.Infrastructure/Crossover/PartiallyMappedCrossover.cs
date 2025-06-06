namespace TspLab.Infrastructure.Crossover;

/// <summary>
/// Partially Mapped Crossover (PMX) - maps cities between parents to avoid duplicates
/// </summary>
public sealed class PartiallyMappedCrossover : ICrossover
{
    public string Name => "PartiallyMappedCrossover";

    public (Tour offspring1, Tour offspring2) Crossover(Tour parent1, Tour parent2, Random random)
    {
        ArgumentNullException.ThrowIfNull(parent1);
        ArgumentNullException.ThrowIfNull(parent2);
        ArgumentNullException.ThrowIfNull(random);

        if (parent1.Length != parent2.Length)
            throw new ArgumentException("Parents must have the same length");

        var length = parent1.Length;
        var offspring1Cities = new int[length];
        var offspring2Cities = new int[length];

        // Copy parents to offspring
        for (int i = 0; i < length; i++)
        {
            offspring1Cities[i] = parent1[i];
            offspring2Cities[i] = parent2[i];
        }

        // Select two random crossover points
        var point1 = random.Next(length);
        var point2 = random.Next(length);
        if (point1 > point2) (point1, point2) = (point2, point1);

        // Create mapping for the segment
        var mapping1to2 = new Dictionary<int, int>();
        var mapping2to1 = new Dictionary<int, int>();

        for (int i = point1; i <= point2; i++)
        {
            var city1 = parent1[i];
            var city2 = parent2[i];

            mapping1to2[city1] = city2;
            mapping2to1[city2] = city1;

            // Swap cities in the crossover segment
            offspring1Cities[i] = city2;
            offspring2Cities[i] = city1;
        }

        // Fix conflicts outside the crossover segment
        FixConflicts(offspring1Cities, mapping2to1, point1, point2);
        FixConflicts(offspring2Cities, mapping1to2, point1, point2);

        return (new Tour(offspring1Cities), new Tour(offspring2Cities));
    }

    private static void FixConflicts(int[] offspring, Dictionary<int, int> mapping, int point1, int point2)
    {
        for (int i = 0; i < offspring.Length; i++)
        {
            if (i >= point1 && i <= point2) continue; // Skip crossover segment

            var city = offspring[i];
            while (mapping.ContainsKey(city))
            {
                city = mapping[city];
            }
            offspring[i] = city;
        }
    }
}
