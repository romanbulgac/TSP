using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.Http.Features;
using TspLab.Application.Services;
using TspLab.Domain.Models;
using TspLab.Domain.Interfaces;
using TspLab.Infrastructure.Services;
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
    private readonly SimulatedAnnealingService _simulatedAnnealingService;
    private readonly IHubContext<TspHub> _hubContext;
    private readonly ILogger<TspController> _logger;
    private readonly IAlgorithmStateManager _stateManager;

    // Store cancellation tokens for active operations
    private static readonly ConcurrentDictionary<string, CancellationTokenSource> _activeSolvers = new();
    
    // Lock objects for synchronizing access to active solvers per connection
    private static readonly ConcurrentDictionary<string, object> _connectionLocks = new();
    
    // Semaphores to ensure only one algorithm runs per connection at a time
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> _connectionSemaphores = new();

    public TspController(
        TspSolverService solverService,
        AntColonyService antColonyService,
        SimulatedAnnealingService simulatedAnnealingService,
        IHubContext<TspHub> hubContext,
        ILogger<TspController> logger,
        IAlgorithmStateManager stateManager)
    {
        _solverService = solverService;
        _antColonyService = antColonyService;
        _simulatedAnnealingService = simulatedAnnealingService;
        _hubContext = hubContext;
        _logger = logger;
        _stateManager = stateManager;
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
            
            _logger.LogWarning("SolveTsp called for connection {ConnectionId} with {CityCount} cities", connectionId, request.Cities.Length);

            // Create a linked cancellation token source for this request
            var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

            // Get or create a semaphore for this connection to ensure sequential execution
            var semaphore = _connectionSemaphores.GetOrAdd(connectionId, _ => new SemaphoreSlim(1, 1));
            
            // Wait for exclusive access to this connection's algorithm execution
            await semaphore.WaitAsync(cancellationToken);
            
            try
            {
                // Get or create a lock object for this connection to prevent race conditions
                var lockObject = _connectionLocks.GetOrAdd(connectionId, _ => new object());

                // Use lock to ensure atomic check-and-set operation
                lock (lockObject)
                {
                    // Try to add the cancellation token source - if one already exists, cancel it first
                    if (_activeSolvers.TryRemove(connectionId, out var existingCts))
                    {
                        existingCts.Cancel();
                        existingCts.Dispose();
                        _logger.LogInformation("Cancelled existing solving operation for connection {ConnectionId}", connectionId);
                    }

                    _activeSolvers[connectionId] = cts;
                    _logger.LogWarning("Starting GA solver for connection {ConnectionId}", connectionId);
                }

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
                    // Use the same lock to ensure atomicity
                    var cleanupLock = _connectionLocks.GetOrAdd(connectionId, _ => new object());
                    lock (cleanupLock)
                    {
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
            }
            finally
            {
                // Always release the semaphore
                semaphore.Release();
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

            // Use the same locking mechanism for consistency
            var lockObject = _connectionLocks.GetOrAdd(connectionId, _ => new object());
            lock (lockObject)
            {
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

            // Get or create a lock object for this connection to prevent race conditions
            var lockObject = _connectionLocks.GetOrAdd(connectionId, _ => new object());

            // Use lock to ensure atomic check-and-set operation
            lock (lockObject)
            {
                // Try to add the cancellation token source - if one already exists, cancel it first
                if (_activeSolvers.TryRemove(connectionId, out var existingCts))
                {
                    existingCts.Cancel();
                    existingCts.Dispose();
                    _logger.LogInformation("Cancelled existing ACO solving operation for connection {ConnectionId}", connectionId);
                }

                _activeSolvers[connectionId] = cts;
                _logger.LogInformation("Starting ACO solver for connection {ConnectionId}", connectionId);
            }

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
                // Use the same lock to ensure atomicity
                var cleanupLock = _connectionLocks.GetOrAdd(connectionId, _ => new object());
                lock (cleanupLock)
                {
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
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error initiating ACO solve");
            return Problem("Failed to initiate ACO solve");
        }
    }

    /// <summary>
    /// Solve TSP using Simulated Annealing with streaming results
    /// </summary>
    /// <param name="request">SA solve request containing cities and configuration</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Confirmation message when solving is complete</returns>
    [HttpPost("solve/sa")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> SolveTspWithSa(
        [FromBody] SaSolveRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            if (request.Cities == null || request.Cities.Length < 3)
                return BadRequest("At least 3 cities are required");

            // Use default config if none provided
            var config = request.Config ?? SimulatedAnnealingService.CreateDefaultConfig(request.Cities.Length);

            if (!config.IsValid())
                return BadRequest("Invalid SA configuration");

            var connectionId = request.ConnectionId ?? "all";

            _logger.LogInformation("Starting SA solve for {CityCount} cities, connectionId: {ConnectionId}",
                request.Cities.Length, connectionId);

            // Create a linked cancellation token source for this request
            var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

            // Get or create a lock object for this connection to prevent race conditions
            var lockObject = _connectionLocks.GetOrAdd(connectionId, _ => new object());

            // Use lock to ensure atomic check-and-set operation
            lock (lockObject)
            {
                // Try to add the cancellation token source - if one already exists, cancel it first
                if (_activeSolvers.TryRemove(connectionId, out var existingCts))
                {
                    existingCts.Cancel();
                    existingCts.Dispose();
                    _logger.LogInformation("Cancelled existing SA solving operation for connection {ConnectionId}", connectionId);
                }

                _activeSolvers[connectionId] = cts;
                _logger.LogInformation("Starting SA solver for connection {ConnectionId}", connectionId);
            }

            try
            {
                SimulatedAnnealingResult? lastResult = null;
                await foreach (var result in _simulatedAnnealingService.SolveAsync(request.Cities, config, cts.Token))
                {
                    // Check if cancellation was requested before sending
                    if (cts.Token.IsCancellationRequested)
                        break;

                    lastResult = result;

                    // Only send updates based on progress report interval or if complete
                    var shouldSendUpdate = result.IsComplete ||
                                         result.Iteration % 100 == 0 || // Report every 100 iterations
                                         result.Iteration == 0; // Always send first iteration

                    if (shouldSendUpdate)
                    {
                        try
                        {
                            _logger.LogInformation("Sending SA result for iteration {Iteration}, IsComplete: {IsComplete}, connectionId: {ConnectionId}",
                                result.Iteration, result.IsComplete, connectionId);

                            // Send result to specific connection or all connections
                            if (connectionId == "all")
                            {
                                await _hubContext.Clients.All.SendAsync("ReceiveSaResult", result, cancellationToken);
                            }
                            else
                            {
                                await _hubContext.Clients.Client(connectionId).SendAsync("ReceiveSaResult", result, cancellationToken);
                            }
                        }
                        catch (ObjectDisposedException)
                        {
                            // CancellationTokenSource was disposed, break the loop
                            _logger.LogWarning("CancellationTokenSource was disposed for SA connection {ConnectionId}", connectionId);
                            break;
                        }
                    }
                }

                // Ensure we always send a final completion signal if we have a result but it wasn't marked as complete
                if (lastResult != null && !lastResult.IsComplete && !cts.Token.IsCancellationRequested)
                {
                    _logger.LogWarning("SA completed but last result was not marked as complete. Sending final completion signal.");

                    lastResult.IsComplete = true;

                    try
                    {
                        if (connectionId == "all")
                        {
                            await _hubContext.Clients.All.SendAsync("ReceiveSaResult", lastResult, cancellationToken);
                        }
                        else
                        {
                            await _hubContext.Clients.Client(connectionId).SendAsync("ReceiveSaResult", lastResult, cancellationToken);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error sending final SA completion signal");
                    }
                }

                return Ok(new { Message = "SA solving completed" });
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("SA solving was cancelled for connection {ConnectionId}", connectionId);
                return Ok(new { Message = "SA solving was cancelled" });
            }
            catch (ObjectDisposedException)
            {
                _logger.LogWarning("CancellationTokenSource was disposed during SA operation for connection {ConnectionId}", connectionId);
                return Ok(new { Message = "SA solving was cancelled" });
            }
            finally
            {
                // Remove and safely dispose the cancellation token source for this connection
                // Use the same lock to ensure atomicity
                var cleanupLock = _connectionLocks.GetOrAdd(connectionId, _ => new object());
                lock (cleanupLock)
                {
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
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error initiating SA solve");
            return Problem("Failed to initiate SA solve");
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

    /// <summary>
    /// Upload and process a TSPLIB file to generate cities using MDS
    /// </summary>
    /// <param name="request">TSPLIB upload request</param>
    /// <returns>Processed cities with MDS-reconstructed coordinates</returns>
    [HttpPost("tsplib/upload")]
    [RequestSizeLimit(100 * 1024 * 1024)] // 100MB limit
    [ProducesResponseType(typeof(Domain.Models.TspLibProcessedResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public IActionResult ProcessTspLibFile([FromBody] Domain.Models.TspLibUploadRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.FileName))
                return BadRequest("File name is required");

            if (string.IsNullOrWhiteSpace(request.FileContent))
                return BadRequest("File content is required");

            var tspLibService = new TspLibService(_logger);

            // Validate file format
            if (!tspLibService.IsValidTspLibFile(request.FileName, request.FileContent))
            {
                _logger.LogWarning("File validation failed for {FileName}", request.FileName);
                return BadRequest("Invalid file - File does not appear to be a valid TSPLIB format. Please check the file structure and size limits.");
            }

            // Process the file
            var result = tspLibService.ProcessTspLibFile(request.FileName, request.FileContent);

            _logger.LogInformation("Successfully processed TSPLIB file '{FileName}' with {CityCount} cities and stress {Stress:F6}",
                request.FileName, result.CityCount, result.MdsStress);

            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning("Invalid TSPLIB file: {Message}", ex.Message);
            return BadRequest($"Invalid TSPLIB file: {ex.Message}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing TSPLIB file");
            return Problem("Failed to process TSPLIB file");
        }
    }

    /// <summary>
    /// Upload TSPLIB file using multipart form data (for very large files)
    /// </summary>
    /// <param name="file">The uploaded file</param>
    /// <returns>Processed cities with MDS-reconstructed coordinates</returns>
    [HttpPost("tsplib/upload-multipart")]
    [RequestSizeLimit(100 * 1024 * 1024)] // 100MB limit
    [ProducesResponseType(typeof(Domain.Models.TspLibProcessedResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> ProcessTspLibFileMultipart(IFormFile file)
    {
        try
        {
            if (file == null || file.Length == 0)
                return BadRequest("No file uploaded");

            if (file.Length > 100 * 1024 * 1024) // 100MB
                return BadRequest("File too large. Maximum size is 100MB");

            _logger.LogInformation("Processing multipart TSPLIB file: {FileName} ({Size} bytes)", 
                file.FileName, file.Length);

            string fileContent;
            using (var stream = file.OpenReadStream())
            using (var reader = new StreamReader(stream))
            {
                fileContent = await reader.ReadToEndAsync();
            }

            var tspLibService = new TspLibService(_logger);

            // Validate file format
            if (!tspLibService.IsValidTspLibFile(file.FileName, fileContent))
            {
                _logger.LogWarning("Multipart file validation failed for {FileName}", file.FileName);
                return BadRequest("Invalid file - File does not appear to be a valid TSPLIB format. Please check the file structure and size limits.");
            }

            // Process the file
            var result = tspLibService.ProcessTspLibFile(file.FileName, fileContent);

            _logger.LogInformation("Successfully processed multipart TSPLIB file '{FileName}' with {CityCount} cities and stress {Stress:F6}",
                file.FileName, result.CityCount, result.MdsStress);

            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning("Invalid TSPLIB file: {Message}", ex.Message);
            return BadRequest($"Invalid TSPLIB file: {ex.Message}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing multipart TSPLIB file");
            return Problem("Failed to process TSPLIB file");
        }
    }

    /// <summary>
    /// Validate a TSPLIB file and get basic information
    /// </summary>
    /// <param name="request">TSPLIB upload request</param>
    /// <returns>Basic file information</returns>
    [HttpPost("tsplib/validate")]
    [RequestSizeLimit(100 * 1024 * 1024)] // 100MB limit
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public IActionResult ValidateTspLibFile([FromBody] Domain.Models.TspLibUploadRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.FileName))
                return BadRequest("File name is required");

            if (string.IsNullOrWhiteSpace(request.FileContent))
                return BadRequest("File content is required");

            var tspLibService = new TspLibService(_logger);
            var isValid = tspLibService.IsValidTspLibFile(request.FileName, request.FileContent);

            if (!isValid)
                return BadRequest("Invalid TSPLIB file format");

            var (name, description, cityCount) = tspLibService.GetFileInfo(request.FileName, request.FileContent);

            return Ok(new Domain.Models.TspLibValidationResult
            {
                IsValid = true,
                Name = name,
                Description = description,
                CityCount = cityCount,
                FileName = request.FileName,
                ErrorMessage = null
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating TSPLIB file");
            return BadRequest($"Error validating file: {ex.Message}");
        }
    }

    /// <summary>
    /// Get detailed validation information for debugging large files
    /// </summary>
    /// <param name="request">TSPLIB upload request</param>
    /// <returns>Detailed validation result with diagnostics</returns>
    [HttpPost("tsplib/validate-detailed")]
    [RequestSizeLimit(100 * 1024 * 1024)] // 100MB limit
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public IActionResult ValidateTspLibFileDetailed([FromBody] Domain.Models.TspLibUploadRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.FileName))
                return BadRequest("File name is required");

            if (string.IsNullOrWhiteSpace(request.FileContent))
                return BadRequest("File content is required");

            var tspLibService = new TspLibService(_logger);
            var (isValid, errorMessage, diagnostics) = tspLibService.GetDetailedValidation(request.FileName, request.FileContent);

            _logger.LogInformation("Detailed validation for {FileName}: Valid={IsValid}, Error={ErrorMessage}, Diagnostics={@Diagnostics}",
                request.FileName, isValid, errorMessage, diagnostics);

            return Ok(new
            {
                IsValid = isValid,
                ErrorMessage = errorMessage,
                FileName = request.FileName,
                Diagnostics = diagnostics
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in detailed validation for TSPLIB file");
            return BadRequest($"Error validating file: {ex.Message}");
        }
    }

    /// <summary>
    /// Pause the current genetic algorithm execution and save state
    /// </summary>
    /// <param name="connectionId">Connection ID of the active operation</param>
    /// <returns>Confirmation with state ID</returns>
    [HttpPost("pause")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public IActionResult PauseExecution([FromQuery] string connectionId)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(connectionId))
                return BadRequest("Connection ID is required");

            if (_activeSolvers.TryGetValue(connectionId, out var cts))
            {
                // Cancel the operation (this will trigger pause in the genetic engine)
                cts.Cancel();
                _logger.LogInformation("Paused execution for connection {ConnectionId}", connectionId);
                
                return Ok(new { 
                    Message = "Execution paused successfully",
                    ConnectionId = connectionId 
                });
            }

            return NotFound($"No active execution found for connection {connectionId}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error pausing execution for connection {ConnectionId}", connectionId);
            return Problem("Failed to pause execution");
        }
    }

    /// <summary>
    /// Resume a genetic algorithm execution from saved state
    /// </summary>
    /// <param name="request">Resume request containing session ID and optional connection ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Confirmation message</returns>
    [HttpPost("resume")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> ResumeExecution(
        [FromBody] ResumeRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.SessionId))
                return BadRequest("Session ID is required");

            var connectionId = request.ConnectionId ?? "all";

            // Check if there's already an active operation for this connection
            if (_activeSolvers.ContainsKey(connectionId))
                return BadRequest($"An operation is already active for connection {connectionId}");

            // Load state from state manager
            var state = await _stateManager.LoadStateAsync(request.SessionId, cancellationToken);
            if (state == null)
                return NotFound($"No saved state found for session {request.SessionId}");

            _logger.LogInformation("Resuming execution for session {SessionId} from generation {Generation}", 
                request.SessionId, state.CurrentGeneration);

            // Create a new cancellation token source for this request
            var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _activeSolvers[connectionId] = cts;

            try
            {
                // Resume execution using the state-aware solver method
                await foreach (var result in _solverService.SolveWithStateAsync(state, state.Cities, state.Config, cts.Token))
                {
                    // Check if cancellation was requested before sending
                    if (cts.Token.IsCancellationRequested)
                        break;

                    // Only send updates based on progress report interval or if complete
                    var shouldSendUpdate = result.IsComplete ||
                                         result.Generation % state.Config.ProgressReportInterval == 0 ||
                                         result.Generation == state.CurrentGeneration; // Always send first resumed generation

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

                return Ok(new { 
                    Message = "Execution resumed and completed successfully",
                    SessionId = request.SessionId,
                    ConnectionId = connectionId 
                });
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Resumed execution was cancelled for session {SessionId}", request.SessionId);
                return Ok(new { Message = "Resumed execution was cancelled" });
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
            _logger.LogError(ex, "Error resuming execution for session {SessionId}", request.SessionId);
            return Problem("Failed to resume execution");
        }
    }

    /// <summary>
    /// Get all available saved states
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of available saved states</returns>
    [HttpGet("states")]
    [ProducesResponseType(typeof(List<GeneticAlgorithmStateSummary>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetAvailableStates(CancellationToken cancellationToken)
    {
        try
        {
            var states = await _stateManager.GetAvailableStatesAsync(cancellationToken);
            return Ok(states);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting available states");
            return Problem("Failed to get available states");
        }
    }

    /// <summary>
    /// Delete a saved state
    /// </summary>
    /// <param name="sessionId">Session ID to delete</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Confirmation message</returns>
    [HttpDelete("states/{sessionId}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> DeleteState(string sessionId, CancellationToken cancellationToken)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(sessionId))
                return BadRequest("Session ID is required");

            var state = await _stateManager.LoadStateAsync(sessionId, cancellationToken);
            if (state == null)
                return NotFound($"No saved state found for session {sessionId}");

            await _stateManager.DeleteStateAsync(sessionId, cancellationToken);
            
            _logger.LogInformation("Deleted state for session {SessionId}", sessionId);
            return Ok(new { Message = $"State deleted for session {sessionId}" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting state for session {SessionId}", sessionId);
            return Problem("Failed to delete state");
        }
    }
}
