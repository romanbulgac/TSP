using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using TspLab.Application.Services;
using TspLab.Domain.Models;
using TspLab.WebApi.Hubs;
using TspLab.WebApi.Models;
using System.Collections.Concurrent;

namespace TspLab.WebApi.Controllers;

/// <summary>
/// Controller for TSP solving operations
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Tags("TSP Solver")]
public class TspController : ControllerBase
{
    private readonly TspSolverService _solverService;
    private readonly AntColonyService _antColonyService;
    private readonly IHubContext<TspHub> _hubContext;
    private readonly ILogger<TspController> _logger;
    
    // Store cancellation tokens for active operations
    private static readonly ConcurrentDictionary<string, CancellationTokenSource> _activeSolvers = new();

    public TspController(
        TspSolverService solverService,
        AntColonyService antColonyService,
        IHubContext<TspHub> hubContext,
        ILogger<TspController> logger)
    {
        _solverService = solverService;
        _antColonyService = antColonyService;
        _hubContext = hubContext;
        _logger = logger;
    }

    /// <summary>
    /// Get available genetic algorithm strategies
    /// </summary>
    /// <returns>Available strategies for crossover, mutation, and fitness functions</returns>
    [HttpGet("strategies")]
    [ProducesResponseType(typeof(AvailableStrategies), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public IActionResult GetAvailableStrategies()
    {
        try
        {
            var strategies = _solverService.GetAvailableStrategies();
            return Ok(strategies);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting available strategies");
            return Problem("Failed to get available strategies");
        }
    }

    /// <summary>
    /// Solve TSP using genetic algorithm with streaming results
    /// </summary>
    /// <param name="request">TSP solve request containing cities and configuration</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Confirmation message when solving is complete</returns>
    [HttpPost("solve")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> SolveTsp(
        [FromBody] TspSolveRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            if (request.Cities == null || request.Cities.Length < 3)
                return BadRequest("At least 3 cities are required");

            // Use default config if none provided
            var config = request.Config ?? GeneticAlgorithmConfig.Default;

            if (!config.IsValid())
                return BadRequest("Invalid configuration");

            var connectionId = request.ConnectionId ?? "all";
            
            // Create a linked cancellation token source for this request
            var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            
            // Try to add the cancellation token source - if one already exists, cancel it first
            if (_activeSolvers.TryRemove(connectionId, out var existingCts))
            {
                existingCts.Cancel();
                existingCts.Dispose();
                _logger.LogInformation("Cancelled existing solving operation for connection {ConnectionId}", connectionId);
            }
            
            _activeSolvers[connectionId] = cts;

            try
            {
                await foreach (var result in _solverService.SolveAsync(request.Cities, config, cts.Token))
                {
                    // Check if cancellation was requested before sending
                    if (cts.Token.IsCancellationRequested)
                        break;
                    
                    // Only send updates based on progress report interval or if complete
                    var shouldSendUpdate = result.IsComplete || 
                                         result.Generation % config.ProgressReportInterval == 0 ||
                                         result.Generation == 0; // Always send first generation
                        
                    if (shouldSendUpdate)
                    {
                        try
                        {
                            // Send result to specific connection or all connections
                            if (connectionId == "all")
                            {
                                await _hubContext.Clients.All.SendAsync("ReceiveGAResult", result, cancellationToken);
                            }
                            else
                            {
                                await _hubContext.Clients.Client(connectionId).SendAsync("ReceiveGAResult", result, cancellationToken);
                            }
                        }
                        catch (ObjectDisposedException)
                        {
                            // CancellationTokenSource was disposed, break the loop
                            _logger.LogWarning("CancellationTokenSource was disposed for connection {ConnectionId}", connectionId);
                            break;
                        }
                    }
                }

                return Ok(new { Message = "TSP solving completed" });
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("TSP solving was cancelled for connection {ConnectionId}", connectionId);
                return Ok(new { Message = "TSP solving was cancelled" });
            }
            catch (ObjectDisposedException)
            {
                _logger.LogWarning("CancellationTokenSource was disposed during operation for connection {ConnectionId}", connectionId);
                return Ok(new { Message = "TSP solving was cancelled" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error solving TSP");
                return Problem("Failed to solve TSP");
            }
            finally
            {
                // Remove and safely dispose the cancellation token source for this connection
                if (_activeSolvers.TryRemove(connectionId, out var removedCts))
                {
                    try
                    {
                        removedCts.Dispose();
                    }
                    catch (ObjectDisposedException)
                    {
                        // Already disposed, ignore
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error initiating TSP solve");
            return Problem("Failed to initiate TSP solve");
        }
    }

    /// <summary>
    /// Stop ongoing TSP solving operation
    /// </summary>
    /// <param name="connectionId">Optional connection ID to stop specific operation</param>
    /// <returns>Confirmation message</returns>
    [HttpPost("stop")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public IActionResult StopSolving([FromQuery] string? connectionId)
    {
        try
        {
            connectionId ??= "all";

            if (_activeSolvers.TryRemove(connectionId, out var cts))
            {
                try
                {
                    if (!cts.Token.IsCancellationRequested)
                    {
                        cts.Cancel();
                        _logger.LogInformation("Cancelled solving operation for connection {ConnectionId}", connectionId);
                    }
                }
                catch (ObjectDisposedException)
                {
                    _logger.LogWarning("CancellationTokenSource was already disposed for connection {ConnectionId}", connectionId);
                }
                finally
                {
                    try
                    {
                        cts.Dispose();
                    }
                    catch (ObjectDisposedException)
                    {
                        // Already disposed, ignore
                    }
                }
                
                return Ok(new { Message = $"TSP solving stopped for connection {connectionId}" });
            }
            else
            {
                _logger.LogWarning("No active solving operation found for connection {ConnectionId}", connectionId);
                return NotFound($"No active solving operation found for connection {connectionId}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error stopping TSP solving");
            return Problem("Failed to stop TSP solving");
        }
    }

    /// <summary>
    /// Generate random cities for testing
    /// </summary>
    /// <param name="request">Request containing count and optional seed</param>
    /// <returns>Array of randomly generated cities</returns>
    [HttpPost("cities/generate")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public IActionResult GenerateCities([FromBody] GenerateCitiesRequest request)
    {
        try
        {
            if (request.Count < 3 || request.Count > 1000)
                return BadRequest("City count must be between 3 and 1000");

            var cities = TspSolverService.GenerateRandomCities(request.Count, request.Seed);
            return Ok(cities);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating cities");
            return Problem("Failed to generate cities");
        }
    }

    /// <summary>
    /// Solve TSP using Ant Colony Optimization with streaming results
    /// </summary>
    /// <param name="request">ACO solve request containing cities and configuration</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Confirmation message when solving is complete</returns>
    [HttpPost("solve/aco")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> SolveTspWithAco(
        [FromBody] AcoSolveRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            if (request.Cities == null || request.Cities.Length < 3)
                return BadRequest("At least 3 cities are required");

            // Use default config if none provided
            var config = request.Config ?? AntColonyService.CreateDefaultConfig(request.Cities.Length);

            if (!config.IsValid())
                return BadRequest("Invalid ACO configuration");

            var connectionId = request.ConnectionId ?? "all";
            
            _logger.LogInformation("Starting ACO solve for {CityCount} cities, connectionId: {ConnectionId}", 
                request.Cities.Length, connectionId);
            
            // Create a linked cancellation token source for this request
            var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            
            // Try to add the cancellation token source - if one already exists, cancel it first
            if (_activeSolvers.TryRemove(connectionId, out var existingCts))
            {
                existingCts.Cancel();
                existingCts.Dispose();
                _logger.LogInformation("Cancelled existing ACO solving operation for connection {ConnectionId}", connectionId);
            }
            
            _activeSolvers[connectionId] = cts;

            try
            {
                AntColonyResult? lastResult = null;
                await foreach (var result in _antColonyService.SolveAsync(request.Cities, config, cts.Token))
                {
                    // Check if cancellation was requested before sending
                    if (cts.Token.IsCancellationRequested)
                        break;
                    
                    lastResult = result;
                    
                    // Only send updates based on progress report interval or if complete
                    var shouldSendUpdate = result.IsComplete || 
                                         result.Iteration % config.ProgressReportInterval == 0 ||
                                         result.Iteration == 0; // Always send first iteration
                        
                    if (shouldSendUpdate)
                    {
                        try
                        {
                            _logger.LogInformation("Sending ACO result for iteration {Iteration}, IsComplete: {IsComplete}, connectionId: {ConnectionId}", 
                                result.Iteration, result.IsComplete, connectionId);
                            
                            // Send result to specific connection or all connections
                            if (connectionId == "all")
                            {
                                await _hubContext.Clients.All.SendAsync("ReceiveAcoResult", result, cancellationToken);
                            }
                            else
                            {
                                await _hubContext.Clients.Client(connectionId).SendAsync("ReceiveAcoResult", result, cancellationToken);
                            }
                        }
                        catch (ObjectDisposedException)
                        {
                            // CancellationTokenSource was disposed, break the loop
                            _logger.LogWarning("CancellationTokenSource was disposed for ACO connection {ConnectionId}", connectionId);
                            break;
                        }
                    }
                }

                // Ensure we always send a final completion signal if we have a result but it wasn't marked as complete
                if (lastResult != null && !lastResult.IsComplete && !cts.Token.IsCancellationRequested)
                {
                    _logger.LogWarning("ACO completed but last result was not marked as complete. Sending final completion signal.");
                    
                    lastResult.IsComplete = true;
                    
                    try
                    {
                        if (connectionId == "all")
                        {
                            await _hubContext.Clients.All.SendAsync("ReceiveAcoResult", lastResult, cancellationToken);
                        }
                        else
                        {
                            await _hubContext.Clients.Client(connectionId).SendAsync("ReceiveAcoResult", lastResult, cancellationToken);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error sending final ACO completion signal");
                    }
                }

                return Ok(new { Message = "ACO solving completed" });
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("ACO solving was cancelled for connection {ConnectionId}", connectionId);
                return Ok(new { Message = "ACO solving was cancelled" });
            }
            catch (ObjectDisposedException)
            {
                _logger.LogWarning("CancellationTokenSource was disposed during ACO operation for connection {ConnectionId}", connectionId);
                return Ok(new { Message = "ACO solving was cancelled" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error solving TSP with ACO");
                return Problem("Failed to solve TSP with ACO");
            }
            finally
            {
                // Remove and safely dispose the cancellation token source for this connection
                if (_activeSolvers.TryRemove(connectionId, out var removedCts))
                {
                    try
                    {
                        removedCts.Dispose();
                    }
                    catch (ObjectDisposedException)
                    {
                        // Already disposed, ignore
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error initiating ACO solve");
            return Problem("Failed to initiate ACO solve");
        }
    }

    /// <summary>
    /// Generate clustered cities for testing ACO performance
    /// </summary>
    /// <param name="request">Request containing count and clustering parameters</param>
    /// <returns>Array of clustered cities</returns>
    [HttpPost("cities/generate/clustered")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public IActionResult GenerateClusteredCities([FromBody] GenerateClusteredCitiesRequest request)
    {
        try
        {
            if (request.Count < 3 || request.Count > 1000)
                return BadRequest("City count must be between 3 and 1000");

            if (request.ClusterCount < 1 || request.ClusterCount > request.Count / 2)
                return BadRequest("Invalid cluster count");

            var cities = AntColonyService.GenerateClusteredCities(request.Count, request.ClusterCount, request.Seed);
            return Ok(cities);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating clustered cities");
            return Problem("Failed to generate clustered cities");
        }
    }
}
