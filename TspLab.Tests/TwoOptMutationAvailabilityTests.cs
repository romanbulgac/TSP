using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TspLab.Infrastructure.Extensions;
using TspLab.Application.Services;
using TspLab.Domain.Interfaces;
using Xunit;
using FluentAssertions;

namespace TspLab.Tests;

/// <summary>
/// Tests to verify TwoOptMutation is properly registered and available
/// </summary>
public class TwoOptMutationAvailabilityTests
{
    [Fact]
    public void TwoOptMutation_Should_Be_Registered_In_DI_Container()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddInfrastructure();
        services.AddLogging();
        
        var serviceProvider = services.BuildServiceProvider();

        // Act
        var mutations = serviceProvider.GetServices<IMutation>();
        var mutationNames = mutations.Select(m => m.Name).ToList();

        // Assert
        mutationNames.Should().Contain("TwoOptMutation");
        mutationNames.Should().Contain("SwapMutation");
        mutationNames.Should().Contain("InversionMutation");
        
        Console.WriteLine("Available mutations:");
        foreach (var name in mutationNames)
        {
            Console.WriteLine($"  - {name}");
        }
    }

    [Fact]
    public void StrategyResolver_Should_Return_TwoOptMutation()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddInfrastructure();
        services.AddLogging();
        
        var serviceProvider = services.BuildServiceProvider();
        var strategyResolver = serviceProvider.GetRequiredService<StrategyResolver>();

        // Act
        var availableMutations = strategyResolver.GetAvailableMutations().ToList();

        // Assert
        availableMutations.Should().Contain("TwoOptMutation");
        availableMutations.Should().Contain("SwapMutation");
        availableMutations.Should().Contain("InversionMutation");

        Console.WriteLine("Available mutations from StrategyResolver:");
        foreach (var name in availableMutations)
        {
            Console.WriteLine($"  - {name}");
        }
    }

    [Fact]
    public void TspSolverService_Should_Return_All_Mutation_Strategies()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddInfrastructure();
        services.AddLogging();
        
        var serviceProvider = services.BuildServiceProvider();
        var tspSolverService = serviceProvider.GetRequiredService<TspSolverService>();

        // Act
        var strategies = tspSolverService.GetAvailableStrategies();

        // Assert
        strategies.Mutations.Should().Contain("TwoOptMutation");
        strategies.Mutations.Should().Contain("SwapMutation");
        strategies.Mutations.Should().Contain("InversionMutation");
        
        Console.WriteLine("Available strategies from TspSolverService:");
        Console.WriteLine("Crossovers:");
        foreach (var crossover in strategies.Crossovers)
        {
            Console.WriteLine($"  - {crossover}");
        }
        Console.WriteLine("Mutations:");
        foreach (var mutation in strategies.Mutations)
        {
            Console.WriteLine($"  - {mutation}");
        }
        Console.WriteLine("Fitness Functions:");
        foreach (var fitness in strategies.FitnessFunctions)
        {
            Console.WriteLine($"  - {fitness}");
        }
    }

    [Fact]
    public void TwoOptMutation_Should_Be_Resolvable_By_Name()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddInfrastructure();
        services.AddLogging();
        
        var serviceProvider = services.BuildServiceProvider();
        var strategyResolver = serviceProvider.GetRequiredService<StrategyResolver>();

        // Act & Assert
        var twoOptMutation = strategyResolver.ResolveMutation("TwoOptMutation");
        twoOptMutation.Should().NotBeNull();
        twoOptMutation.Name.Should().Be("TwoOptMutation");
        
        Console.WriteLine($"Successfully resolved TwoOptMutation: {twoOptMutation.Name}");
    }
}
