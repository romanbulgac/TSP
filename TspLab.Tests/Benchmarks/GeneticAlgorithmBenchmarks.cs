using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using TspLab.Domain.Entities;
using TspLab.Domain.Models;
using TspLab.Infrastructure.Crossover;
using TspLab.Infrastructure.Mutation;
using TspLab.Infrastructure.Fitness;
using Microsoft.Extensions.Caching.Memory;

namespace TspLab.Tests.Benchmarks;

/// <summary>
/// Benchmark tests for genetic algorithm performance
/// </summary>
[MemoryDiagnoser]
[SimpleJob]
public class GeneticAlgorithmBenchmarks
{
    private City[] _cities = null!;
    private Tour[] _population = null!;
    private OrderCrossover _crossover = null!;
    private SwapMutation _mutation = null!;
    private DistanceFitnessFunction _fitnessFunction = null!;
    private Random _random = null!;

    [GlobalSetup]
    public void Setup()
    {
        // Generate test cities
        _random = new Random(42);
        _cities = GenerateRandomCities(50);
        
        // Create test population
        _population = new Tour[100];
        for (int i = 0; i < _population.Length; i++)
        {
            var cities = Enumerable.Range(0, _cities.Length).ToArray();
            Shuffle(cities, _random);
            _population[i] = new Tour(cities);
        }

        // Initialize operators
        _crossover = new OrderCrossover();
        _mutation = new SwapMutation { MutationRate = 0.01 };
        _fitnessFunction = new DistanceFitnessFunction(new MemoryCache(new MemoryCacheOptions()));
    }

    [Benchmark]
    public double FitnessEvaluation()
    {
        var totalFitness = 0.0;
        for (int i = 0; i < _population.Length; i++)
        {
            totalFitness += _fitnessFunction.CalculateFitness(_population[i], _cities);
        }
        return totalFitness;
    }

    [Benchmark]
    public Tour[] CrossoverBenchmark()
    {
        var offspring = new Tour[_population.Length];
        for (int i = 0; i < _population.Length; i += 2)
        {
            var parent1 = _population[i];
            var parent2 = _population[(i + 1) % _population.Length];
            var (child1, child2) = _crossover.Crossover(parent1, parent2, _random);
            offspring[i] = child1;
            if (i + 1 < offspring.Length)
                offspring[i + 1] = child2;
        }
        return offspring;
    }

    [Benchmark]
    public void MutationBenchmark()
    {
        foreach (var tour in _population)
        {
            _mutation.Mutate(tour, _random);
        }
    }

    [Benchmark]
    public double NearestNeighborHeuristic()
    {
        return SolveNearestNeighbor(_cities);
    }

    [Benchmark]
    public double TwoOptImprovement()
    {
        var tour = new Tour(Enumerable.Range(0, _cities.Length).ToArray());
        return Improve2Opt(tour, _cities);
    }

    private static City[] GenerateRandomCities(int count)
    {
        var random = new Random(42);
        var cities = new City[count];
        for (int i = 0; i < count; i++)
        {
            cities[i] = new City(i, $"City_{i}", random.NextDouble() * 1000, random.NextDouble() * 1000);
        }
        return cities;
    }

    private static void Shuffle<T>(T[] array, Random random)
    {
        for (int i = array.Length - 1; i > 0; i--)
        {
            int j = random.Next(i + 1);
            (array[i], array[j]) = (array[j], array[i]);
        }
    }

    /// <summary>
    /// Nearest Neighbor heuristic for comparison
    /// </summary>
    private static double SolveNearestNeighbor(City[] cities)
    {
        var visited = new bool[cities.Length];
        var current = 0;
        visited[0] = true;
        var totalDistance = 0.0;

        for (int i = 0; i < cities.Length - 1; i++)
        {
            var nearest = -1;
            var minDistance = double.MaxValue;

            for (int j = 0; j < cities.Length; j++)
            {
                if (!visited[j])
                {
                    var distance = cities[current].DistanceTo(cities[j]);
                    if (distance < minDistance)
                    {
                        minDistance = distance;
                        nearest = j;
                    }
                }
            }

            if (nearest != -1)
            {
                totalDistance += minDistance;
                visited[nearest] = true;
                current = nearest;
            }
        }

        // Return to start
        totalDistance += cities[current].DistanceTo(cities[0]);
        return totalDistance;
    }

    /// <summary>
    /// 2-opt improvement heuristic for comparison
    /// </summary>
    private static double Improve2Opt(Tour tour, City[] cities)
    {
        var improved = true;
        var bestDistance = CalculateTourDistance(tour, cities);

        while (improved)
        {
            improved = false;
            for (int i = 0; i < tour.Length - 1; i++)
            {
                for (int j = i + 2; j < tour.Length; j++)
                {
                    var testTour = tour.Clone();
                    testTour.ReverseSegment(i + 1, j);
                    var testDistance = CalculateTourDistance(testTour, cities);

                    if (testDistance < bestDistance)
                    {
                        bestDistance = testDistance;
                        tour = testTour;
                        improved = true;
                    }
                }
            }
        }

        return bestDistance;
    }

    private static double CalculateTourDistance(Tour tour, City[] cities)
    {
        var distance = 0.0;
        for (int i = 0; i < tour.Length; i++)
        {
            var from = cities[tour[i]];
            var to = cities[tour[(i + 1) % tour.Length]];
            distance += from.DistanceTo(to);
        }
        return distance;
    }
}

/// <summary>
/// Program entry point for running benchmarks
/// </summary>
public class BenchmarkProgram
{
    // Commented out to avoid multiple entry points in test project
    // Uncomment when running benchmarks separately
    /*
    public static void Main(string[] args)
    {
        var summary = BenchmarkRunner.Run<GeneticAlgorithmBenchmarks>();
        Console.WriteLine(summary);
    }
    */
}
