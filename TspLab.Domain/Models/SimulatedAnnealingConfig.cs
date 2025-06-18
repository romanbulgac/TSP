using System.ComponentModel.DataAnnotations;

namespace TspLab.Domain.Models;

/// <summary>
/// Configuration parameters for Simulated Annealing algorithm
/// </summary>
public class SimulatedAnnealingConfig
{
    /// <summary>
    /// Initial temperature for the annealing process
    /// </summary>
    [Range(100.0, 10000.0)]
    public double InitialTemperature { get; set; } = 1000.0;

    /// <summary>
    /// Final temperature to stop the algorithm
    /// </summary>
    [Range(0.01, 10.0)]
    public double FinalTemperature { get; set; } = 0.1;

    /// <summary>
    /// Cooling rate (temperature multiplier each iteration)
    /// </summary>
    [Range(0.9, 0.9999)]
    public double CoolingRate { get; set; } = 0.995;

    /// <summary>
    /// Maximum number of iterations
    /// </summary>
    [Range(1000, 100000)]
    public int MaxIterations { get; set; } = 50000;

    /// <summary>
    /// Probability of using 2-opt move vs simple swap
    /// </summary>
    [Range(0.0, 1.0)]
    public double TwoOptProbability { get; set; } = 0.7;

    /// <summary>
    /// Whether to use nearest neighbor for initial solution
    /// </summary>
    public bool UseNearestNeighborInitialization { get; set; } = true;

    /// <summary>
    /// Whether to enable adaptive reheating when stuck
    /// </summary>
    public bool EnableAdaptiveReheating { get; set; } = true;

    /// <summary>
    /// Interval for checking stagnation and potential reheating
    /// </summary>
    [Range(100, 5000)]
    public int ReheatCheckInterval { get; set; } = 1000;

    /// <summary>
    /// Creates a default configuration optimized for general use
    /// </summary>
    public static SimulatedAnnealingConfig Default => new()
    {
        InitialTemperature = 1000.0,
        FinalTemperature = 0.1,
        CoolingRate = 0.995,
        MaxIterations = 50000,
        TwoOptProbability = 0.7,
        UseNearestNeighborInitialization = true,
        EnableAdaptiveReheating = true,
        ReheatCheckInterval = 1000
    };

    /// <summary>
    /// Creates a configuration optimized for the given problem size
    /// </summary>
    /// <param name="cityCount">Number of cities in the problem</param>
    /// <returns>Optimized SA configuration</returns>
    public static SimulatedAnnealingConfig ForProblemSize(int cityCount)
    {
        return new SimulatedAnnealingConfig
        {
            InitialTemperature = Math.Max(1000.0, cityCount * 50.0),
            FinalTemperature = Math.Max(0.1, cityCount * 0.01),
            CoolingRate = cityCount > 50 ? 0.9995 : 0.995,
            MaxIterations = Math.Max(10000, cityCount * cityCount * 10),
            TwoOptProbability = cityCount > 100 ? 0.8 : 0.7,
            UseNearestNeighborInitialization = true,
            EnableAdaptiveReheating = cityCount > 30,
            ReheatCheckInterval = Math.Max(500, cityCount * 10)
        };
    }

    /// <summary>
    /// Validates the configuration parameters
    /// </summary>
    public bool IsValid()
    {
        return InitialTemperature >= 100.0 && InitialTemperature <= 10000.0 &&
               FinalTemperature >= 0.01 && FinalTemperature <= 10.0 &&
               FinalTemperature < InitialTemperature &&
               CoolingRate >= 0.9 && CoolingRate <= 0.9999 &&
               MaxIterations >= 1000 && MaxIterations <= 100000 &&
               TwoOptProbability >= 0.0 && TwoOptProbability <= 1.0 &&
               ReheatCheckInterval >= 100 && ReheatCheckInterval <= 5000;
    }
}
