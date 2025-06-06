namespace TspLab.Domain.Models;

/// <summary>
/// Represents the result of a genetic algorithm generation
/// </summary>
/// <param name="Generation">Current generation number</param>
/// <param name="BestFitness">Best fitness value in this generation</param>
/// <param name="AverageFitness">Average fitness of the population</param>
/// <param name="BestTour">Best tour found so far</param>
/// <param name="BestDistance">Distance of the best tour</param>
/// <param name="ElapsedMilliseconds">Time elapsed since start</param>
/// <param name="IsComplete">Whether the algorithm has completed</param>
public readonly record struct GeneticAlgorithmResult(
    int Generation,
    double BestFitness,
    double AverageFitness,
    Tour BestTour,
    double BestDistance,
    long ElapsedMilliseconds,
    bool IsComplete);

/// <summary>
/// Summary statistics for a completed genetic algorithm run
/// </summary>
/// <param name="TotalGenerations">Total number of generations run</param>
/// <param name="FinalBestFitness">Final best fitness achieved</param>
/// <param name="FinalBestDistance">Final best distance achieved</param>
/// <param name="FinalBestTour">Final best tour found</param>
/// <param name="TotalElapsedMilliseconds">Total execution time</param>
/// <param name="ConvergenceHistory">History of best fitness per generation</param>
public readonly record struct GeneticAlgorithmSummary(
    int TotalGenerations,
    double FinalBestFitness,
    double FinalBestDistance,
    Tour FinalBestTour,
    long TotalElapsedMilliseconds,
    IReadOnlyList<double> ConvergenceHistory);
