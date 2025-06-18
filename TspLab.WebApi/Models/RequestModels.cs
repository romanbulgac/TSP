using TspLab.Domain.Entities;
using TspLab.Domain.Models;

namespace TspLab.WebApi.Models;

/// <summary>
/// Request model for solving TSP
/// </summary>
/// <param name="Cities">Array of cities to visit</param>
/// <param name="Config">Genetic algorithm configuration</param>
/// <param name="ConnectionId">SignalR connection ID for targeted updates (optional)</param>
public readonly record struct TspSolveRequest(
    City[] Cities,
    GeneticAlgorithmConfig? Config,
    string? ConnectionId = null);

/// <summary>
/// Request model for solving TSP using Ant Colony Optimization
/// </summary>
/// <param name="Cities">Array of cities to visit</param>
/// <param name="Config">ACO algorithm configuration</param>
/// <param name="ConnectionId">SignalR connection ID for targeted updates (optional)</param>
public readonly record struct AcoSolveRequest(
    City[] Cities,
    AntColonyConfig? Config,
    string? ConnectionId = null);

/// <summary>
/// Request model for generating random cities
/// </summary>
/// <param name="Count">Number of cities to generate</param>
/// <param name="Seed">Random seed for reproducible results (optional)</param>
public readonly record struct GenerateCitiesRequest(
    int Count,
    int? Seed = null);

/// <summary>
/// Request model for generating clustered cities
/// </summary>
/// <param name="Count">Number of cities to generate</param>
/// <param name="ClusterCount">Number of clusters to create</param>
/// <param name="Seed">Random seed for reproducible results (optional)</param>
public readonly record struct GenerateClusteredCitiesRequest(
    int Count,
    int ClusterCount = 3,
    int? Seed = null);

/// <summary>
/// Request model for solving TSP using Simulated Annealing
/// </summary>
/// <param name="Cities">Array of cities to visit</param>
/// <param name="Config">SA algorithm configuration</param>
/// <param name="ConnectionId">SignalR connection ID for targeted updates (optional)</param>
public readonly record struct SaSolveRequest(
    City[] Cities,
    SimulatedAnnealingConfig? Config,
    string? ConnectionId = null);

/// <summary>
/// Request model for resuming algorithm execution from saved state
/// </summary>
/// <param name="SessionId">Session ID of the saved state</param>
/// <param name="ConnectionId">SignalR connection ID for targeted updates (optional)</param>
public readonly record struct ResumeRequest(
    string SessionId,
    string? ConnectionId = null);


