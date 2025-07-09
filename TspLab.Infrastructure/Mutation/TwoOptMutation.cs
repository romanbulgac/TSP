using TspLab.Domain.Entities;
using TspLab.Domain.Interfaces;

namespace TspLab.Infrastructure.Mutation;

/// <summary>
/// Two-Opt Mutation - removes two edges and reconnects the tour
/// </summary>
public sealed class TwoOptMutation : IMutation
{
    public string Name => "TwoOptMutation";
    public double MutationRate { get; set; } = 0.01;

    public void Mutate(Tour tour, Random random)
    {
        ArgumentNullException.ThrowIfNull(tour);
        ArgumentNullException.ThrowIfNull(random);

        if (random.NextDouble() > MutationRate) return;

        if (tour.Length < 3) return; // Need at least 3 cities for 2-opt

        // Select two distinct positions that will define the edges to remove
        var i = random.Next(tour.Length);
        var j = random.Next(tour.Length);

        // Ensure i and j are different and properly ordered
        while (j == i)
        {
            j = random.Next(tour.Length);
        }

        // Ensure i < j for consistent processing
        if (i > j)
        {
            (i, j) = (j, i);
        }

        // Skip if the segment is too small (would result in no change)
        if (j - i < 2) return;

        // Apply 2-opt: reverse the segment between i and j (inclusive)
        // This removes edges (i-1,i) and (j,j+1) and adds (i-1,j) and (i,j+1)
        tour.ReverseSegment(i, j);
    }
}
