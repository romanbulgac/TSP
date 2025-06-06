namespace TspLab.Domain.Interfaces;

/// <summary>
/// Interface for crossover operators in genetic algorithms
/// </summary>
public interface ICrossover
{
    /// <summary>
    /// Name of the crossover operator
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Performs crossover between two parent tours
    /// </summary>
    /// <param name="parent1">First parent tour</param>
    /// <param name="parent2">Second parent tour</param>
    /// <param name="random">Random number generator</param>
    /// <returns>Two offspring tours</returns>
    (Tour offspring1, Tour offspring2) Crossover(Tour parent1, Tour parent2, Random random);
}
