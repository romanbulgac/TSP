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

        if (tour.Length < 4) return; // Need at least 4 cities for 2-opt

        var i = random.Next(tour.Length - 1);
        var j = random.Next(i + 2, tour.Length + (i == 0 ? -1 : 0));

        if (j >= tour.Length) j = tour.Length - 1;

        // Reverse the segment between i+1 and j
        tour.ReverseSegment(i + 1, j);
    }
}
