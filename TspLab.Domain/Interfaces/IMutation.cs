namespace TspLab.Domain.Interfaces;

/// <summary>
/// Interface for mutation operators in genetic algorithms
/// </summary>
public interface IMutation
{
    /// <summary>
    /// Name of the mutation operator
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Mutation probability (0.0 to 1.0)
    /// </summary>
    double MutationRate { get; set; }

    /// <summary>
    /// Applies mutation to a tour
    /// </summary>
    /// <param name="tour">Tour to mutate</param>
    /// <param name="random">Random number generator</param>
    void Mutate(Tour tour, Random random);
}
