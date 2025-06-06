namespace TspLab.Infrastructure.Crossover;

/// <summary>
/// Cycle Crossover (CX) - preserves absolute positions of cities from parents
/// </summary>
public sealed class CycleCrossover : ICrossover
{
    public string Name => "CycleCrossover";

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
        var visited = new bool[length];

        // Initialize offspring with invalid values
        Array.Fill(offspring1Cities, -1);
        Array.Fill(offspring2Cities, -1);

        var useParent1First = random.NextDouble() < 0.5;

        for (int start = 0; start < length; start++)
        {
            if (visited[start]) continue;

            // Find cycle starting from this position
            var cycle = FindCycle(parent1, parent2, start);
            
            // Mark all positions in cycle as visited
            foreach (var pos in cycle)
                visited[pos] = true;

            // Alternate which parent provides cities for each cycle
            if (useParent1First)
            {
                foreach (var pos in cycle)
                {
                    offspring1Cities[pos] = parent1[pos];
                    offspring2Cities[pos] = parent2[pos];
                }
            }
            else
            {
                foreach (var pos in cycle)
                {
                    offspring1Cities[pos] = parent2[pos];
                    offspring2Cities[pos] = parent1[pos];
                }
            }

            useParent1First = !useParent1First;
        }

        return (new Tour(offspring1Cities), new Tour(offspring2Cities));
    }

    private static List<int> FindCycle(Tour parent1, Tour parent2, int start)
    {
        var cycle = new List<int>();
        var current = start;

        do
        {
            cycle.Add(current);
            var cityAtCurrent = parent1[current];
            
            // Find where this city appears in parent2
            current = -1;
            for (int i = 0; i < parent2.Length; i++)
            {
                if (parent2[i] == cityAtCurrent)
                {
                    current = i;
                    break;
                }
            }
        } while (current != start && current != -1);

        return cycle;
    }
}
