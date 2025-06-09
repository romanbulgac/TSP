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
/// Request model for generating random cities
/// </summary>
/// <param name="Count">Number of cities to generate</param>
/// <param name="Seed">Random seed for reproducible results (optional)</param>
public readonly record struct GenerateCitiesRequest(
    int Count,
    int? Seed = null);
