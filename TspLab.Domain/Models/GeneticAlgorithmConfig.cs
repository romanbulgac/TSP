namespace TspLab.Domain.Models;

/// <summary>
/// Configuration parameters for the genetic algorithm
/// </summary>
public class GeneticAlgorithmConfig
{
    /// <summary>
    /// Gets a default configuration instance
    /// </summary>
    public static GeneticAlgorithmConfig Default => new GeneticAlgorithmConfig();

    /// <summary>
    /// Number of individuals in the population
    /// </summary>
    public int PopulationSize { get; set; } = 100;

    /// <summary>
    /// Maximum number of generations to run
    /// </summary>
    public int MaxGenerations { get; set; } = 1000;

    /// <summary>
    /// Probability of mutation (0.0 to 1.0)
    /// </summary>
    public double MutationRate { get; set; } = 0.01;

    /// <summary>
    /// Probability of crossover (0.0 to 1.0)
    /// </summary>
    public double CrossoverRate { get; set; } = 0.8;

    /// <summary>
    /// Percentage of elite individuals to preserve (0.0 to 1.0)
    /// </summary>
    public double ElitismRate { get; set; } = 0.1;

    /// <summary>
    /// Selection pressure for tournament selection
    /// </summary>
    public int TournamentSize { get; set; } = 5;

    /// <summary>
    /// Target fitness value to stop early (optional)
    /// </summary>
    public double? TargetFitness { get; set; }

    /// <summary>
    /// Number of generations to wait without improvement before stopping
    /// </summary>
    public int StagnationLimit { get; set; } = 100;

    /// <summary>
    /// Seed for random number generation (for reproducible results)
    /// </summary>
    public int? RandomSeed { get; set; }

    /// <summary>
    /// Whether to enable parallel processing
    /// </summary>
    public bool UseParallelProcessing { get; set; } = true;

    /// <summary>
    /// Maximum degree of parallelism (-1 for unlimited)
    /// </summary>
    public int MaxDegreeOfParallelism { get; set; } = -1;

    /// <summary>
    /// Interval for reporting progress (in generations)
    /// </summary>
    public int ProgressReportInterval { get; set; } = 10;

    /// <summary>
    /// Whether to track detailed statistics
    /// </summary>
    public bool TrackStatistics { get; set; } = true;

    /// <summary>
    /// Name of the crossover strategy to use
    /// </summary>
    public string CrossoverName { get; set; } = "OrderCrossover";

    /// <summary>
    /// Name of the mutation strategy to use
    /// </summary>
    public string MutationName { get; set; } = "SwapMutation";

    /// <summary>
    /// Name of the fitness function to use
    /// </summary>
    public string FitnessFunctionName { get; set; } = "DistanceFitness";

    /// <summary>
    /// Validates the configuration parameters
    /// </summary>
    /// <returns>True if valid, otherwise throws ArgumentException</returns>
    public bool IsValid()
    {
        return Validate();
    }

    /// <summary>
    /// Validates the configuration parameters
    /// </summary>
    /// <returns>True if valid, otherwise throws ArgumentException</returns>
    public bool Validate()
    {
        if (PopulationSize <= 0)
            throw new ArgumentException("Population size must be positive", nameof(PopulationSize));
        
        if (MaxGenerations <= 0)
            throw new ArgumentException("Max generations must be positive", nameof(MaxGenerations));
        
        if (MutationRate < 0.0 || MutationRate > 1.0)
            throw new ArgumentException("Mutation rate must be between 0.0 and 1.0", nameof(MutationRate));
        
        if (CrossoverRate < 0.0 || CrossoverRate > 1.0)
            throw new ArgumentException("Crossover rate must be between 0.0 and 1.0", nameof(CrossoverRate));
        
        if (ElitismRate < 0.0 || ElitismRate > 1.0)
            throw new ArgumentException("Elitism rate must be between 0.0 and 1.0", nameof(ElitismRate));
        
        if (TournamentSize <= 0)
            throw new ArgumentException("Tournament size must be positive", nameof(TournamentSize));
        
        if (StagnationLimit < 0)
            throw new ArgumentException("Stagnation limit cannot be negative", nameof(StagnationLimit));
        
        if (ProgressReportInterval <= 0)
            throw new ArgumentException("Progress report interval must be positive", nameof(ProgressReportInterval));

        return true;
    }
}
