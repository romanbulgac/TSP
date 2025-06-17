namespace TspLab.Domain.Models;

/// <summary>
/// Result from a single iteration of the Ant Colony Optimization algorithm
/// </summary>
public class AntColonyResult
{
    /// <summary>
    /// Current iteration number
    /// </summary>
    public int Iteration { get; set; }

    /// <summary>
    /// Best tour found so far
    /// </summary>
    public int[] BestTour { get; set; } = Array.Empty<int>();

    /// <summary>
    /// Distance of the best tour
    /// </summary>
    public double BestDistance { get; set; }

    /// <summary>
    /// Best tour found in this iteration
    /// </summary>
    public int[] IterationBestTour { get; set; } = Array.Empty<int>();

    /// <summary>
    /// Distance of the best tour in this iteration
    /// </summary>
    public double IterationBestDistance { get; set; }

    /// <summary>
    /// Average distance of all ants in this iteration
    /// </summary>
    public double AverageDistance { get; set; }

    /// <summary>
    /// Elapsed time in milliseconds since algorithm start
    /// </summary>
    public long ElapsedMilliseconds { get; set; }

    /// <summary>
    /// Indicates if the algorithm has completed
    /// </summary>
    public bool IsComplete { get; set; }

    /// <summary>
    /// Number of iterations since last improvement
    /// </summary>
    public int StagnationCount { get; set; }

    /// <summary>
    /// Current configuration being used
    /// </summary>
    public AntColonyConfig? Config { get; set; }

    /// <summary>
    /// Additional statistics about convergence
    /// </summary>
    public Dictionary<string, object> Statistics { get; set; } = new();
}
