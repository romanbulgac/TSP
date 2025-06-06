using System.Net.Http.Json;
using TspLab.Domain.Entities;
using TspLab.Domain.Models;

namespace TspLab.Web.Services;

/// <summary>
/// Service for calling TSP API endpoints
/// </summary>
public sealed class TspApiService
{
    private readonly HttpClient _httpClient;

    public TspApiService(HttpClient httpClient)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
    }

    /// <summary>
    /// Gets available genetic algorithm strategies
    /// </summary>
    /// <returns>Available strategies</returns>
    public async Task<AvailableStrategies?> GetAvailableStrategiesAsync()
    {
        try
        {
            return await _httpClient.GetFromJsonAsync<AvailableStrategies>("/api/tsp/strategies");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error getting strategies: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Generates random cities for testing
    /// </summary>
    /// <param name="count">Number of cities to generate</param>
    /// <param name="seed">Random seed (optional)</param>
    /// <returns>Array of generated cities</returns>
    public async Task<City[]?> GenerateRandomCitiesAsync(int count, int? seed = null)
    {
        try
        {
            var request = new GenerateCitiesRequest(count, seed);
            var response = await _httpClient.PostAsJsonAsync("/api/tsp/cities/generate", request);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<City[]>();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error generating cities: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Starts TSP solving via SignalR (non-blocking)
    /// </summary>
    /// <param name="cities">Cities to solve</param>
    /// <param name="config">GA configuration</param>
    /// <param name="connectionId">SignalR connection ID</param>
    /// <returns>Success status</returns>
    public async Task<bool> StartTspSolvingAsync(City[] cities, GeneticAlgorithmConfig config, string? connectionId = null)
    {
        try
        {
            var request = new TspSolveRequest(cities, config, connectionId);
            var response = await _httpClient.PostAsJsonAsync("/api/tsp/solve", request);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error starting TSP solving: {ex.Message}");
            return false;
        }
    }
}

/// <summary>
/// Available strategies for genetic algorithm components
/// </summary>
/// <param name="Crossovers">Available crossover operators</param>
/// <param name="Mutations">Available mutation operators</param>
/// <param name="FitnessFunctions">Available fitness functions</param>
public readonly record struct AvailableStrategies(
    IReadOnlyList<string> Crossovers,
    IReadOnlyList<string> Mutations,
    IReadOnlyList<string> FitnessFunctions);

/// <summary>
/// Request model for solving TSP
/// </summary>
/// <param name="Cities">Array of cities to visit</param>
/// <param name="Config">Genetic algorithm configuration</param>
/// <param name="ConnectionId">SignalR connection ID for targeted updates (optional)</param>
public readonly record struct TspSolveRequest(
    City[] Cities,
    GeneticAlgorithmConfig Config,
    string? ConnectionId = null);

/// <summary>
/// Request model for generating random cities
/// </summary>
/// <param name="Count">Number of cities to generate</param>
/// <param name="Seed">Random seed for reproducible results (optional)</param>
public readonly record struct GenerateCitiesRequest(
    int Count,
    int? Seed = null);
