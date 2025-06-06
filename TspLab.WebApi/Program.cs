using Microsoft.AspNetCore.SignalR;
using Serilog;
using TspLab.Application.Services;
using TspLab.Infrastructure.Extensions;
using TspLab.WebApi.Hubs;
using TspLab.WebApi.Models;

var builder = WebApplication.CreateBuilder(args);

// Add Serilog
builder.Host.UseSerilog((context, config) =>
{
    config.ReadFrom.Configuration(context.Configuration)
          .WriteTo.Console()
          .Enrich.FromLogContext();
});

// Add services to the container
builder.Services.AddInfrastructure();
builder.Services.AddSignalR();

// Add API documentation
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Add CORS for Blazor WebAssembly
builder.Services.AddCors(options =>
{
    options.AddPolicy("BlazorWasm", policy =>
    {
        policy.WithOrigins("https://localhost:5001", "http://localhost:5000")
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials();
    });
});

// Add health checks
builder.Services.AddHealthChecks();

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseCors("BlazorWasm");

// Map SignalR hub
app.MapHub<TspHub>("/tspHub");

// Map health checks
app.MapHealthChecks("/health");

// API Endpoints
var api = app.MapGroup("/api/tsp").WithTags("TSP Solver");

api.MapGet("/strategies", GetAvailableStrategies)
   .WithName("GetAvailableStrategies")
   .WithSummary("Get available genetic algorithm strategies");

api.MapPost("/solve", SolveTsp)
   .WithName("SolveTsp")
   .WithSummary("Solve TSP using genetic algorithm with streaming results");

api.MapPost("/cities/generate", GenerateCities)
   .WithName("GenerateCities")
   .WithSummary("Generate random cities for testing");

app.Run();

/// <summary>
/// Gets available genetic algorithm strategies
/// </summary>
static async Task<IResult> GetAvailableStrategies(TspSolverService solverService)
{
    try
    {
        var strategies = solverService.GetAvailableStrategies();
        return Results.Ok(strategies);
    }
    catch (Exception ex)
    {
        Log.Error(ex, "Error getting available strategies");
        return Results.Problem("Failed to get available strategies");
    }
}

/// <summary>
/// Solves TSP using genetic algorithm with SignalR streaming
/// </summary>
static async Task<IResult> SolveTsp(
    TspSolveRequest request,
    TspSolverService solverService,
    IHubContext<TspHub> hubContext,
    CancellationToken cancellationToken)
{
    try
    {
        if (request.Cities == null || request.Cities.Length < 3)
            return Results.BadRequest("At least 3 cities are required");

        if (!request.Config.IsValid())
            return Results.BadRequest("Invalid configuration");

        var connectionId = request.ConnectionId ?? "all";
        
        await foreach (var result in solverService.SolveAsync(request.Cities, request.Config, cancellationToken))
        {
            // Send result to specific connection or all connections
            if (connectionId == "all")
            {
                await hubContext.Clients.All.SendAsync("ReceiveGAResult", result, cancellationToken);
            }
            else
            {
                await hubContext.Clients.Client(connectionId).SendAsync("ReceiveGAResult", result, cancellationToken);
            }
        }

        return Results.Ok(new { Message = "TSP solving completed" });
    }
    catch (OperationCanceledException)
    {
        return Results.Ok(new { Message = "TSP solving was cancelled" });
    }
    catch (Exception ex)
    {
        Log.Error(ex, "Error solving TSP");
        return Results.Problem("Failed to solve TSP");
    }
}

/// <summary>
/// Generates random cities for testing
/// </summary>
static IResult GenerateCities(GenerateCitiesRequest request)
{
    try
    {
        if (request.Count < 3 || request.Count > 1000)
            return Results.BadRequest("City count must be between 3 and 1000");

        var cities = TspSolverService.GenerateRandomCities(request.Count, request.Seed);
        return Results.Ok(cities);
    }
    catch (Exception ex)
    {
        Log.Error(ex, "Error generating cities");
        return Results.Problem("Failed to generate cities");
    }
}
