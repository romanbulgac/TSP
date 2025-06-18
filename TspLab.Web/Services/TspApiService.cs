using System.Net.Http.Json;
using TspLab.Domain.Entities;
using TspLab.Domain.Models;
using TspLab.Domain.Interfaces;
using System.Text.Json;

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
            Console.WriteLine($"Requesting {count} cities with seed {seed}");
            var request = new GenerateCitiesRequest(count, seed);
            var response = await _httpClient.PostAsJsonAsync("/api/tsp/cities/generate", request);
            Console.WriteLine($"Response status: {response.StatusCode}");
            response.EnsureSuccessStatusCode();
            var cities = await response.Content.ReadFromJsonAsync<City[]>();
            Console.WriteLine($"Received {cities?.Length ?? 0} cities");
            return cities;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error generating cities: {ex.Message}");
            Console.WriteLine($"Exception type: {ex.GetType().Name}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
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

    /// <summary>
    /// Stops TSP solving operation
    /// </summary>
    /// <param name="connectionId">SignalR connection ID</param>
    /// <returns>Success status</returns>
    public async Task<bool> StopTspSolvingAsync(string? connectionId = null)
    {
        try
        {
            var url = $"/api/tsp/stop?connectionId={connectionId ?? "all"}";
            var response = await _httpClient.PostAsync(url, null);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error stopping TSP solving: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Pauses the current genetic algorithm execution
    /// </summary>
    /// <param name="connectionId">SignalR connection ID</param>
    /// <returns>Success status</returns>
    public async Task<bool> PauseTspSolvingAsync(string connectionId)
    {
        try
        {
            var url = $"/api/tsp/pause?connectionId={connectionId}";
            var response = await _httpClient.PostAsync(url, null);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error pausing TSP solving: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Resumes genetic algorithm execution from saved state
    /// </summary>
    /// <param name="sessionId">Session ID to resume from</param>
    /// <param name="connectionId">SignalR connection ID</param>
    /// <returns>Success status</returns>
    public async Task<bool> ResumeTspSolvingAsync(string sessionId, string? connectionId = null)
    {
        try
        {
            var request = new ResumeRequest(sessionId, connectionId);
            var response = await _httpClient.PostAsJsonAsync("/api/tsp/resume", request);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error resuming TSP solving: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Starts ACO solving via SignalR (non-blocking)
    /// </summary>
    /// <param name="cities">Cities to solve</param>
    /// <param name="config">ACO configuration</param>
    /// <param name="connectionId">SignalR connection ID</param>
    /// <returns>Success status</returns>
    public async Task<bool> StartAcoSolvingAsync(City[] cities, AntColonyConfig config, string? connectionId = null)
    {
        try
        {
            var request = new AcoSolveRequest(cities, config, connectionId);
            var response = await _httpClient.PostAsJsonAsync("/api/tsp/solve/aco", request);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error starting ACO solving: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Starts SA solving via SignalR (non-blocking)
    /// </summary>
    /// <param name="cities">Cities to solve</param>
    /// <param name="config">SA configuration</param>
    /// <param name="connectionId">SignalR connection ID</param>
    /// <returns>Success status</returns>
    public async Task<bool> StartSaSolvingAsync(City[] cities, SimulatedAnnealingConfig config, string? connectionId = null)
    {
        try
        {
            var request = new SaSolveRequest(cities, config, connectionId);
            var response = await _httpClient.PostAsJsonAsync("/api/tsp/solve/sa", request);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error starting SA solving: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Generates clustered cities for testing
    /// </summary>
    /// <param name="count">Number of cities to generate</param>
    /// <param name="clusterCount">Number of clusters</param>
    /// <param name="seed">Random seed (optional)</param>
    /// <returns>Array of generated clustered cities</returns>
    public async Task<City[]?> GenerateClusteredCitiesAsync(int count, int clusterCount = 3, int? seed = null)
    {
        try
        {
            Console.WriteLine($"Requesting {count} clustered cities with {clusterCount} clusters and seed {seed}");
            var request = new GenerateClusteredCitiesRequest(count, clusterCount, seed);
            var response = await _httpClient.PostAsJsonAsync("/api/tsp/cities/generate/clustered", request);
            Console.WriteLine($"Response status: {response.StatusCode}");
            response.EnsureSuccessStatusCode();
            var cities = await response.Content.ReadFromJsonAsync<City[]>();
            Console.WriteLine($"Received {cities?.Length ?? 0} clustered cities");
            return cities;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error generating clustered cities: {ex.Message}");
            Console.WriteLine($"Exception type: {ex.GetType().Name}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
            return null;
        }
    }

    /// <summary>
    /// Uploads and processes a TSPLIB file
    /// </summary>
    /// <param name="fileName">Name of the file</param>
    /// <param name="fileContent">File content as string</param>
    /// <returns>Processed result with cities</returns>
    public async Task<TspLibProcessedResult?> UploadTspLibFileAsync(string fileName, string fileContent)
    {
        try
        {
            Console.WriteLine($"Uploading TSPLIB file: {fileName}");
            var request = new TspLibUploadRequest { FileName = fileName, FileContent = fileContent };
            var response = await _httpClient.PostAsJsonAsync("/api/tsp/tsplib/upload", request);
            Console.WriteLine($"Response status: {response.StatusCode}");
            
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"Error uploading TSPLIB file: {errorContent}");
                return null;
            }
            
            var result = await response.Content.ReadFromJsonAsync<TspLibProcessedResult>();
            if (result != null)
                Console.WriteLine($"Successfully processed TSPLIB file with {result.CityCount} cities");
            return result;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error uploading TSPLIB file: {ex.Message}");
            Console.WriteLine($"Exception type: {ex.GetType().Name}");
            return null;
        }
    }

    /// <summary>
    /// Upload TSPLIB file using multipart form data (more memory efficient for large files)
    /// </summary>
    /// <param name="fileName">Original file name</param>
    /// <param name="fileContent">File content as string</param>
    /// <returns>Processed result with city coordinates</returns>
    public async Task<TspLibProcessedResult?> UploadTspLibFileMultipartAsync(string fileName, string fileContent)
    {
        try
        {
            Console.WriteLine($"Uploading TSPLIB file via multipart: {fileName}");
            
            // Create multipart form content
            using var form = new MultipartFormDataContent();
            var content = new StringContent(fileContent, System.Text.Encoding.UTF8);
            form.Add(content, "file", fileName);
            
            var response = await _httpClient.PostAsync("/api/tsp/tsplib/upload-multipart", form);
            
            if (response.IsSuccessStatusCode)
            {
                var jsonResponse = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"Upload successful: {jsonResponse}");
                return JsonSerializer.Deserialize<TspLibProcessedResult>(jsonResponse, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"Upload failed: {response.StatusCode} - {errorContent}");
                return null;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error uploading TSPLIB file via multipart: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Validates a TSPLIB file and gets basic information
    /// </summary>
    /// <param name="fileName">Name of the file</param>
    /// <param name="fileContent">File content as string</param>
    /// <returns>Validation result with basic info</returns>
    public async Task<TspLibValidationResult?> ValidateTspLibFileAsync(string fileName, string fileContent)
    {
        try
        {
            Console.WriteLine($"Validating TSPLIB file: {fileName}");
            var request = new TspLibUploadRequest { FileName = fileName, FileContent = fileContent };
            var response = await _httpClient.PostAsJsonAsync("/api/tsp/tsplib/validate", request);
            
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"Validation failed: {errorContent}");
                return new TspLibValidationResult 
                { 
                    IsValid = false, 
                    Name = "", 
                    Description = "", 
                    CityCount = 0, 
                    FileName = fileName, 
                    ErrorMessage = errorContent 
                };
            }
            
            var jsonContent = await response.Content.ReadAsStringAsync();
            Console.WriteLine($"Raw validation response: {jsonContent}");
            var result = JsonSerializer.Deserialize<TspLibValidationResult>(jsonContent, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
            if (result != null)
                Console.WriteLine($"Validation result: {result.IsValid}");
            return result;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error validating TSPLIB file: {ex.Message}");
            return new TspLibValidationResult 
            { 
                IsValid = false, 
                Name = "", 
                Description = "", 
                CityCount = 0, 
                FileName = fileName, 
                ErrorMessage = ex.Message 
            };
        }
    }

    /// <summary>
    /// Gets detailed validation information for debugging large files
    /// </summary>
    /// <param name="fileName">Name of the file</param>
    /// <param name="fileContent">File content as string</param>
    /// <returns>Detailed validation result with diagnostics</returns>
    public async Task<object?> ValidateTspLibFileDetailedAsync(string fileName, string fileContent)
    {
        try
        {
            Console.WriteLine($"Running detailed validation for TSPLIB file: {fileName}");
            var request = new TspLibUploadRequest { FileName = fileName, FileContent = fileContent };
            var response = await _httpClient.PostAsJsonAsync("/api/tsp/tsplib/validate-detailed", request);
            
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"Detailed validation failed: {errorContent}");
                return new 
                { 
                    IsValid = false, 
                    ErrorMessage = errorContent,
                    FileName = fileName,
                    Diagnostics = new Dictionary<string, object>()
                };
            }
            
            var result = await response.Content.ReadFromJsonAsync<object>();
            Console.WriteLine($"Detailed validation completed");
            return result;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in detailed validation: {ex.Message}");
            return new 
            { 
                IsValid = false, 
                ErrorMessage = ex.Message,
                FileName = fileName,
                Diagnostics = new Dictionary<string, object> { ["Exception"] = ex.Message }
            };
        }
    }

    /// <summary>
    /// Gets all available saved states
    /// </summary>
    /// <returns>List of saved states or null if failed</returns>
    public async Task<List<GeneticAlgorithmStateSummary>?> GetSavedStatesAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync("/api/tsp/states");
            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<List<GeneticAlgorithmStateSummary>>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
            }
            return null;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error getting saved states: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Deletes a saved state
    /// </summary>
    /// <param name="sessionId">Session ID to delete</param>
    /// <returns>Success status</returns>
    public async Task<bool> DeleteSavedStateAsync(string sessionId)
    {
        try
        {
            var response = await _httpClient.DeleteAsync($"/api/tsp/states/{sessionId}");
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error deleting saved state: {ex.Message}");
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
/// Request model for resuming genetic algorithm execution
/// </summary>
/// <param name="SessionId">Session ID to resume from</param>
/// <param name="ConnectionId">SignalR connection ID for targeted updates (optional)</param>
public readonly record struct ResumeRequest(
    string SessionId,
    string? ConnectionId = null);


