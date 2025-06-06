using Microsoft.Extensions.DependencyInjection;
using TspLab.Domain.Interfaces;
using TspLab.Application.Services;
using TspLab.Infrastructure.Crossover;
using TspLab.Infrastructure.Fitness;
using TspLab.Infrastructure.Mutation;

namespace TspLab.Infrastructure.Extensions;

/// <summary>
/// Dependency injection extensions for infrastructure services
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers all infrastructure services
    /// </summary>
    /// <param name="services">Service collection</param>
    /// <returns>Service collection for chaining</returns>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        // Register crossover operators
        services.AddTransient<ICrossover, OrderCrossover>();
        services.AddTransient<ICrossover, PartiallyMappedCrossover>();
        services.AddTransient<ICrossover, CycleCrossover>();

        // Register mutation operators
        services.AddTransient<IMutation, SwapMutation>();
        services.AddTransient<IMutation, InversionMutation>();
        services.AddTransient<IMutation, TwoOptMutation>();

        // Register fitness functions
        services.AddTransient<IFitnessFunction, DistanceFitnessFunction>();

        // Register application services
        services.AddTransient<GeneticEngine>();
        services.AddTransient<StrategyResolver>();
        services.AddTransient<TspSolverService>();

        // Add memory cache for fitness caching
        services.AddMemoryCache();

        return services;
    }
}
