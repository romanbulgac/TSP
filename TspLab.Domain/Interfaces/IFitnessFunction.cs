namespace TspLab.Domain.Interfaces;

/// <summary>
/// Interface for fitness evaluation functions
/// </summary>
public interface IFitnessFunction
{
    /// <summary>
    /// Name of the fitness function
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Calculates fitness for a tour given the city data
    /// </summary>
    /// <param name="tour">Tour to evaluate</param>
    /// <param name="cities">Array of cities</param>
    /// <returns>Fitness value (higher is better)</returns>
    double CalculateFitness(Tour tour, ReadOnlySpan<City> cities);

    /// <summary>
    /// Calculates total distance for a tour given the city data
    /// </summary>
    /// <param name="tour">Tour to evaluate</param>
    /// <param name="cities">Array of cities</param>
    /// <returns>Total distance</returns>
    double CalculateDistance(Tour tour, ReadOnlySpan<City> cities);
}
