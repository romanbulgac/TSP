using TspLab.Domain.Entities;

namespace TspLab.Domain.Models;

/// <summary>
/// Represents the complete state of a genetic algorithm execution
/// for pause/resume functionality
/// </summary>
public sealed class GeneticAlgorithmState
{
    /// <summary>
    /// Unique identifier for this execution session
    /// </summary>
    public string SessionId { get; set; } = string.Empty;

    /// <summary>
    /// Current generation number
    /// </summary>
    public int CurrentGeneration { get; set; }

    /// <summary>
    /// The algorithm configuration
    /// </summary>
    public GeneticAlgorithmConfig Config { get; set; } = new();

    /// <summary>
    /// Cities being solved
    /// </summary>
    public City[] Cities { get; set; } = Array.Empty<City>();

    /// <summary>
    /// Current population state
    /// </summary>
    public PopulationState Population { get; set; } = new();

    /// <summary>
    /// Best tour found so far
    /// </summary>
    public TourWithFitness? BestTour { get; set; }

    /// <summary>
    /// Convergence history data
    /// </summary>
    public List<double> ConvergenceHistory { get; set; } = new();

    /// <summary>
    /// Timestamp when execution was paused
    /// </summary>
    public DateTime PausedAt { get; set; }

    /// <summary>
    /// Total elapsed milliseconds
    /// </summary>
    public long ElapsedMilliseconds { get; set; }

    /// <summary>
    /// Generations without improvement (for stagnation detection)
    /// </summary>
    public int GenerationsWithoutImprovement { get; set; }

    /// <summary>
    /// Last improvement generation
    /// </summary>
    public int LastImprovementGeneration { get; set; }

    /// <summary>
    /// Random seed for reproducibility
    /// </summary>
    public int? RandomSeed { get; set; }

    /// <summary>
    /// Algorithm status
    /// </summary>
    public AlgorithmStatus Status { get; set; } = AlgorithmStatus.Created;

    /// <summary>
    /// Creates a copy of this state with a new session ID
    /// </summary>
    public GeneticAlgorithmState WithSessionId(string newSessionId)
    {
        return new GeneticAlgorithmState
        {
            SessionId = newSessionId,
            CurrentGeneration = CurrentGeneration,
            Config = Config,
            Cities = Cities,
            Population = Population,
            BestTour = BestTour,
            ConvergenceHistory = new List<double>(ConvergenceHistory),
            PausedAt = PausedAt,
            ElapsedMilliseconds = ElapsedMilliseconds,
            GenerationsWithoutImprovement = GenerationsWithoutImprovement,
            LastImprovementGeneration = LastImprovementGeneration,
            RandomSeed = RandomSeed,
            Status = Status
        };
    }
}

/// <summary>
/// Represents the population state for genetic algorithm
/// </summary>
public sealed class PopulationState
{
    /// <summary>
    /// All tours in the current population with their fitness values
    /// </summary>
    public List<TourWithFitness> Tours { get; set; } = new();

    /// <summary>
    /// Size of the population
    /// </summary>
    public int Size => Tours.Count;
}

/// <summary>
/// Tour with associated fitness and distance values
/// </summary>
public sealed class TourWithFitness
{
    /// <summary>
    /// City sequence of the tour
    /// </summary>
    public int[] CitySequence { get; set; } = Array.Empty<int>();

    /// <summary>
    /// Fitness value of the tour
    /// </summary>
    public double Fitness { get; set; }

    /// <summary>
    /// Total distance of the tour
    /// </summary>
    public double Distance { get; set; }

    /// <summary>
    /// Converts to Tour entity
    /// </summary>
    public Tour ToTour()
    {
        var tour = new Tour(CitySequence);
        tour.Fitness = Fitness;
        tour.Distance = Distance;
        return tour;
    }

    /// <summary>
    /// Creates from Tour entity
    /// </summary>
    public static TourWithFitness FromTour(Tour tour)
    {
        return new TourWithFitness
        {
            CitySequence = tour.Cities.ToArray(),
            Fitness = tour.Fitness,
            Distance = tour.Distance
        };
    }
}

/// <summary>
/// Algorithm execution status
/// </summary>
public enum AlgorithmStatus
{
    Created,
    Running,
    Paused,
    Completed,
    Cancelled,
    Error
}

/// <summary>
/// Checkpoint configuration for automatic saving
/// </summary>
public sealed class CheckpointConfig
{
    /// <summary>
    /// Enable automatic checkpointing
    /// </summary>
    public bool EnableAutoCheckpoint { get; set; } = true;

    /// <summary>
    /// Save checkpoint every N generations
    /// </summary>
    public int CheckpointInterval { get; set; } = 50;

    /// <summary>
    /// Alias for CheckpointInterval for backward compatibility
    /// </summary>
    public int GenerationInterval => CheckpointInterval;

    /// <summary>
    /// Maximum number of checkpoints to keep
    /// </summary>
    public int MaxCheckpoints { get; set; } = 10;

    /// <summary>
    /// Save to browser localStorage (true) or server persistence (false)
    /// </summary>
    public bool UseBrowserStorage { get; set; } = true;
}
