namespace TspLab.Domain.Interfaces;

/// <summary>
/// Defines a common interface for TSP solving algorithms.
/// Provides a unified way to solve the Traveling Salesman Problem
/// using different algorithmic approaches.
/// </summary>
public interface ITspSolver
{
    /// <summary>
    /// Gets the name of the TSP solving algorithm.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets a description of how the algorithm works.
    /// </summary>
    string Description { get; }

    /// <summary>
    /// Solves the TSP problem for the given set of cities.
    /// </summary>
    /// <param name="cities">The array of cities to visit.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous solve operation. The task result contains the optimal tour.</returns>
    Task<Tour> SolveAsync(City[] cities, CancellationToken cancellationToken = default);
}
