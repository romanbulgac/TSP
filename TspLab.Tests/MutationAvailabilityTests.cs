using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TspLab.Infrastructure.Extensions;
using TspLab.Application.Services;
using FluentAssertions;

namespace TspLab.Tests;

/// <summary>
/// Unit test to verify TwoOptMutation is available
/// </summary>
public class MutationAvailabilityTests
{
    [Fact]
    public void TwoOptMutation_ShouldBeAvailable()
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
        
        // Should have at least 3 mutation strategies
        strategies.Mutations.Count.Should().BeGreaterOrEqualTo(3);
    }

    [Fact]
    public void StrategyResolver_ShouldResolveTwoOptMutation()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddInfrastructure();
        services.AddLogging();
        var serviceProvider = services.BuildServiceProvider();
        
        var strategyResolver = serviceProvider.GetRequiredService<StrategyResolver>();

        // Act & Assert
        var mutation = strategyResolver.ResolveMutation("TwoOptMutation");
        mutation.Should().NotBeNull();
        mutation.Name.Should().Be("TwoOptMutation");
    }

    [Theory]
    [InlineData("TwoOptMutation")]
    [InlineData("SwapMutation")]
    [InlineData("InversionMutation")]
    public void AllMutations_ShouldBeResolvable(string mutationName)
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddInfrastructure();
        services.AddLogging();
        var serviceProvider = services.BuildServiceProvider();
        
        var strategyResolver = serviceProvider.GetRequiredService<StrategyResolver>();

        // Act & Assert
        var mutation = strategyResolver.ResolveMutation(mutationName);
        mutation.Should().NotBeNull();
        mutation.Name.Should().Be(mutationName);
    }

    [Fact]
    public void AllAvailableStrategies_ShouldBeDisplayed()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddInfrastructure();
        services.AddLogging();
        var serviceProvider = services.BuildServiceProvider();
        
        var tspSolverService = serviceProvider.GetRequiredService<TspSolverService>();

        // Act
        var strategies = tspSolverService.GetAvailableStrategies();

        // Assert & Output for debugging
        Console.WriteLine("Available Crossover Operators:");
        foreach (var crossover in strategies.Crossovers)
        {
            Console.WriteLine($"  - {crossover}");
        }

        Console.WriteLine("\nAvailable Mutation Operators:");
        foreach (var mutation in strategies.Mutations)
        {
            Console.WriteLine($"  - {mutation}");
        }

        Console.WriteLine("\nAvailable Fitness Functions:");
        foreach (var fitness in strategies.FitnessFunctions)
        {
            Console.WriteLine($"  - {fitness}");
        }

        // Verify we have expected strategies
        strategies.Crossovers.Should().NotBeEmpty();
        strategies.Mutations.Should().NotBeEmpty();
        strategies.FitnessFunctions.Should().NotBeEmpty();
    }
}
