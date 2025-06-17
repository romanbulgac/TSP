using FluentAssertions;
using TspLab.Domain.Entities;
using TspLab.Domain.Models;
using TspLab.Infrastructure.Crossover;
using TspLab.Infrastructure.Mutation;
using TspLab.Infrastructure.Fitness;
using Microsoft.Extensions.Caching.Memory;

namespace TspLab.Tests;

/// <summary>
/// Unit tests for TSP domain entities
/// </summary>
public class TourTests
{
    [Fact]
    public void Tour_Constructor_ShouldCreateValidTour()
    {
        // Arrange
        var cities = new[] { 0, 1, 2, 3, 4 };

        // Act
        var tour = new Tour(cities);

        // Assert
        tour.Length.Should().Be(5);
        tour.Cities.ToArray().Should().BeEquivalentTo(cities);
        tour.IsValid().Should().BeTrue();
    }

    [Fact]
    public void Tour_Swap_ShouldSwapCities()
    {
        // Arrange
        var cities = new[] { 0, 1, 2, 3, 4 };
        var tour = new Tour(cities);

        // Act
        tour.Swap(0, 4);

        // Assert
        tour[0].Should().Be(4);
        tour[4].Should().Be(0);
        tour.IsValid().Should().BeTrue();
    }

    [Fact]
    public void Tour_ReverseSegment_ShouldReverseCorrectly()
    {
        // Arrange
        var cities = new[] { 0, 1, 2, 3, 4 };
        var tour = new Tour(cities);

        // Act
        tour.ReverseSegment(1, 3);

        // Assert
        tour.Cities.ToArray().Should().BeEquivalentTo(new[] { 0, 3, 2, 1, 4 });
        tour.IsValid().Should().BeTrue();
    }

    [Theory]
    [InlineData(new[] { 0, 1, 2 }, true)]
    [InlineData(new[] { 0, 1, 1 }, false)]
    [InlineData(new[] { 0, 1, 2, 3 }, true)]
    public void Tour_IsValid_ShouldReturnCorrectResult(int[] cities, bool expected)
    {
        // Arrange
        var tour = new Tour(cities);

        // Act
        var result = tour.IsValid();

        // Assert
        result.Should().Be(expected);
    }
}

/// <summary>
/// Unit tests for crossover operators
/// </summary>
public class CrossoverTests
{
    private readonly Random _random = new(42); // Fixed seed for reproducibility

    [Fact]
    public void OrderCrossover_ShouldProduceValidOffspring()
    {
        // Arrange
        var crossover = new OrderCrossover();
        var parent1 = new Tour(new[] { 0, 1, 2, 3, 4, 5 });
        var parent2 = new Tour(new[] { 3, 4, 5, 0, 1, 2 });

        // Act
        var (offspring1, offspring2) = crossover.Crossover(parent1, parent2, _random);

        // Assert
        offspring1.IsValid().Should().BeTrue();
        offspring2.IsValid().Should().BeTrue();
        offspring1.Length.Should().Be(parent1.Length);
        offspring2.Length.Should().Be(parent2.Length);
    }

    [Fact]
    public void PartiallyMappedCrossover_ShouldProduceValidOffspring()
    {
        // Arrange
        var crossover = new PartiallyMappedCrossover();
        var parent1 = new Tour(new[] { 0, 1, 2, 3, 4 });
        var parent2 = new Tour(new[] { 4, 3, 2, 1, 0 });

        // Act
        var (offspring1, offspring2) = crossover.Crossover(parent1, parent2, _random);

        // Assert
        offspring1.IsValid().Should().BeTrue();
        offspring2.IsValid().Should().BeTrue();
    }

    [Fact]
    public void CycleCrossover_ShouldProduceValidOffspring()
    {
        // Arrange
        var crossover = new CycleCrossover();
        var parent1 = new Tour(new[] { 0, 1, 2, 3, 4 });
        var parent2 = new Tour(new[] { 1, 2, 3, 4, 0 });

        // Act
        var (offspring1, offspring2) = crossover.Crossover(parent1, parent2, _random);

        // Assert
        offspring1.IsValid().Should().BeTrue();
        offspring2.IsValid().Should().BeTrue();
    }

    [Fact]
    public void EdgeRecombinationCrossover_ShouldProduceValidOffspring()
    {
        // Arrange
        var crossover = new EdgeRecombinationCrossover();
        var parent1 = new Tour(new[] { 0, 1, 2, 3, 4, 5 });
        var parent2 = new Tour(new[] { 0, 2, 4, 1, 3, 5 });

        // Act
        var (offspring1, offspring2) = crossover.Crossover(parent1, parent2, _random);

        // Assert
        offspring1.IsValid().Should().BeTrue();
        offspring2.IsValid().Should().BeTrue();
        offspring1.Length.Should().Be(parent1.Length);
        offspring2.Length.Should().Be(parent2.Length);
    }
}

/// <summary>
/// Unit tests for mutation operators
/// </summary>
public class MutationTests
{
    private readonly Random _random = new(42);

    [Fact]
    public void SwapMutation_WithHighRate_ShouldMutate()
    {
        // Arrange
        var mutation = new SwapMutation { MutationRate = 1.0 }; // Always mutate
        var originalCities = new[] { 0, 1, 2, 3, 4 };
        var tour = new Tour(originalCities);
        var originalTour = tour.Clone();

        // Act
        mutation.Mutate(tour, _random);

        // Assert
        tour.IsValid().Should().BeTrue();
        tour.Should().NotBe(originalTour); // Should be different after mutation
    }

    [Fact]
    public void InversionMutation_WithHighRate_ShouldMutate()
    {
        // Arrange
        var mutation = new InversionMutation { MutationRate = 1.0 };
        var tour = new Tour(new[] { 0, 1, 2, 3, 4 });

        // Act
        mutation.Mutate(tour, _random);

        // Assert
        tour.IsValid().Should().BeTrue();
    }

    [Fact]
    public void TwoOptMutation_WithHighRate_ShouldMutate()
    {
        // Arrange
        var mutation = new TwoOptMutation { MutationRate = 1.0 };
        var tour = new Tour(new[] { 0, 1, 2, 3, 4, 5 });

        // Act
        mutation.Mutate(tour, _random);

        // Assert
        tour.IsValid().Should().BeTrue();
    }
}

/// <summary>
/// Unit tests for fitness functions
/// </summary>
public class FitnessTests
{
    [Fact]
    public void DistanceFitnessFunction_ShouldCalculateCorrectDistance()
    {
        // Arrange
        var cache = new MemoryCache(new MemoryCacheOptions());
        var fitnessFunction = new DistanceFitnessFunction(cache);
        
        var cities = new[]
        {
            new City(0, "A", 0, 0),
            new City(1, "B", 3, 4),
            new City(2, "C", 6, 8)
        };
        
        var tour = new Tour(new[] { 0, 1, 2 });

        // Act
        var distance = fitnessFunction.CalculateDistance(tour, cities);
        var fitness = fitnessFunction.CalculateFitness(tour, cities);

        // Assert
        distance.Should().BeGreaterThan(0);
        fitness.Should().BeGreaterThan(0);
        fitness.Should().Be(1.0 / (distance + 1.0)); // Inverse relationship
    }

    [Fact]
    public void City_DistanceTo_ShouldCalculateEuclideanDistance()
    {
        // Arrange
        var city1 = new City(0, "A", 0, 0);
        var city2 = new City(1, "B", 3, 4);

        // Act
        var distance = city1.DistanceTo(city2);

        // Assert
        distance.Should().Be(5.0); // 3-4-5 triangle
    }
}