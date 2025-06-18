using TspLab.Domain.Entities;
using TspLab.Domain.Interfaces;

namespace TspLab.Infrastructure.Mutation;

/// <summary>
/// Swap Mutation - randomly swaps two cities in the tour
/// </summary>
public sealed class SwapMutation : IMutation
{
    public string Name => "SwapMutation";
    public double MutationRate { get; set; } = 0.01;

    public void Mutate(Tour tour, Random random)
    {
        ArgumentNullException.ThrowIfNull(tour);
        ArgumentNullException.ThrowIfNull(random);

        if (random.NextDouble() > MutationRate) return;

        if (tour.Length < 2) return;

        var index1 = random.Next(tour.Length);
        var index2 = random.Next(tour.Length);

        // Ensure we're swapping different positions
        while (index1 == index2)
            index2 = random.Next(tour.Length);

        tour.Swap(index1, index2);
    }
}
