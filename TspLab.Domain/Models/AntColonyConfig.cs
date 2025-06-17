using System.ComponentModel.DataAnnotations;

namespace TspLab.Domain.Models;

/// <summary>
/// Configuration parameters for Ant Colony Optimization algorithm
/// </summary>
public class AntColonyConfig
{
    /// <summary>
    /// Number of ants in the colony
    /// </summary>
    [Range(5, 500)]
    public int AntCount { get; set; } = 50;

    /// <summary>
    /// Maximum number of iterations
    /// </summary>
    [Range(50, 2000)]
    public int MaxIterations { get; set; } = 300;

    /// <summary>
    /// Pheromone influence parameter (alpha)
    /// </summary>
    [Range(0.1, 5.0)]
    public double Alpha { get; set; } = 1.0;

    /// <summary>
    /// Heuristic influence parameter (beta)
    /// </summary>
    [Range(0.1, 10.0)]
    public double Beta { get; set; } = 2.0;

    /// <summary>
    /// Pheromone evaporation rate (rho)
    /// </summary>
    [Range(0.01, 0.9)]
    public double EvaporationRate { get; set; } = 0.5;

    /// <summary>
    /// Initial pheromone level
    /// </summary>
    [Range(0.001, 1.0)]
    public double InitialPheromone { get; set; } = 0.1;

    /// <summary>
    /// Elite ant strategy: number of elite ants
    /// </summary>
    [Range(0, 10)]
    public int EliteAntCount { get; set; } = 1;

    /// <summary>
    /// Local search improvement after ant tour construction
    /// </summary>
    public bool UseLocalSearch { get; set; } = true;

    /// <summary>
    /// Progress report interval (iterations)
    /// </summary>
    [Range(1, 100)]
    public int ProgressReportInterval { get; set; } = 10;

    /// <summary>
    /// Creates a default configuration optimized for general use
    /// </summary>
    public static AntColonyConfig Default => new()
    {
        AntCount = 50,
        MaxIterations = 300,
        Alpha = 1.0,
        Beta = 2.0,
        EvaporationRate = 0.5,
        InitialPheromone = 0.1,
        EliteAntCount = 1,
        UseLocalSearch = true,
        ProgressReportInterval = 10
    };

    /// <summary>
    /// Validates the configuration parameters
    /// </summary>
    public bool IsValid()
    {
        return AntCount >= 5 && AntCount <= 500 &&
               MaxIterations >= 50 && MaxIterations <= 2000 &&
               Alpha >= 0.1 && Alpha <= 5.0 &&
               Beta >= 0.1 && Beta <= 10.0 &&
               EvaporationRate >= 0.01 && EvaporationRate <= 0.9 &&
               InitialPheromone >= 0.001 && InitialPheromone <= 1.0 &&
               EliteAntCount >= 0 && EliteAntCount <= 10 &&
               ProgressReportInterval >= 1 && ProgressReportInterval <= 100;
    }
}
