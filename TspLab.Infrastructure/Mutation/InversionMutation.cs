namespace TspLab.Infrastructure.Mutation;

/// <summary>
/// Inversion Mutation - reverses a random segment of the tour
/// </summary>
public sealed class InversionMutation : IMutation
{
    public string Name => "InversionMutation";
    public double MutationRate { get; set; } = 0.01;

    public void Mutate(Tour tour, Random random)
    {
        ArgumentNullException.ThrowIfNull(tour);
        ArgumentNullException.ThrowIfNull(random);

        if (random.NextDouble() > MutationRate) return;

        if (tour.Length < 2) return;

        var start = random.Next(tour.Length);
        var end = random.Next(tour.Length);

        if (start == end) return;

        tour.ReverseSegment(start, end);
    }
}
