using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using TspLab.Domain.Entities;
using TspLab.Domain.Models;
using TspLab.Domain.Interfaces;
using TspLab.Infrastructure.Crossover;
using TspLab.Infrastructure.Mutation;
using TspLab.Infrastructure.Fitness;
using Microsoft.Extensions.Caching.Memory;

namespace TspLab.Tests.Benchmarks;

/// <summary>
/// Benchmark tests for genetic algorithm performance with all crossover and mutation combinations
/// </summary>
[MemoryDiagnoser]
[SimpleJob]
public class GeneticAlgorithmBenchmarks
{
    private City[] _cities = null!;
    private Tour[] _population = null!;
    private Dictionary<string, ICrossover> _crossoverOperators = null!;
    private Dictionary<string, IMutation> _mutationOperators = null!;
    private DistanceFitnessFunction _fitnessFunction = null!;
    private Random _random = null!;

    // Parameters for benchmark combinations
    [ParamsSource(nameof(CrossoverNames))]
    public string CrossoverName { get; set; } = "OrderCrossover";

    [ParamsSource(nameof(MutationNames))]
    public string MutationName { get; set; } = "SwapMutation";

    // Properties to provide parameter values
    public static IEnumerable<string> CrossoverNames => new[]
    {
        "OrderCrossover",
        "CycleCrossover", 
        "PartiallyMappedCrossover",
        "EdgeRecombinationCrossover"
    };

    public static IEnumerable<string> MutationNames => new[]
    {
        "SwapMutation",
        "InversionMutation",
        "TwoOptMutation"
    };

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

        // Initialize all crossover operators
        _crossoverOperators = new Dictionary<string, ICrossover>
        {
            { "OrderCrossover", new OrderCrossover() },
            { "CycleCrossover", new CycleCrossover() },
            { "PartiallyMappedCrossover", new PartiallyMappedCrossover() },
            { "EdgeRecombinationCrossover", new EdgeRecombinationCrossover() }
        };

        // Initialize all mutation operators
        _mutationOperators = new Dictionary<string, IMutation>
        {
            { "SwapMutation", new SwapMutation { MutationRate = 0.01 } },
            { "InversionMutation", new InversionMutation { MutationRate = 0.01 } },
            { "TwoOptMutation", new TwoOptMutation { MutationRate = 0.01 } }
        };

        // Initialize fitness function
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
        var crossover = _crossoverOperators[CrossoverName];
        var offspring = new Tour[_population.Length];
        
        for (int i = 0; i < _population.Length; i += 2)
        {
            var parent1 = _population[i];
            var parent2 = _population[(i + 1) % _population.Length];
            var (child1, child2) = crossover.Crossover(parent1, parent2, _random);
            offspring[i] = child1;
            if (i + 1 < offspring.Length)
                offspring[i + 1] = child2;
        }
        return offspring;
    }

    [Benchmark]
    public void MutationBenchmark()
    {
        var mutation = _mutationOperators[MutationName];
        foreach (var tour in _population)
        {
            // Create a copy to avoid modifying the original population
            var tourCopy = tour.Clone();
            mutation.Mutate(tourCopy, _random);
        }
    }

    [Benchmark]
    public Tour[] CombinedGeneticOperators()
    {
        var crossover = _crossoverOperators[CrossoverName];
        var mutation = _mutationOperators[MutationName];
        var offspring = new Tour[_population.Length];
        
        // Crossover phase
        for (int i = 0; i < _population.Length; i += 2)
        {
            var parent1 = _population[i];
            var parent2 = _population[(i + 1) % _population.Length];
            var (child1, child2) = crossover.Crossover(parent1, parent2, _random);
            offspring[i] = child1;
            if (i + 1 < offspring.Length)
                offspring[i + 1] = child2;
        }
        
        // Mutation phase
        foreach (var tour in offspring)
        {
            mutation.Mutate(tour, _random);
        }
        
        return offspring;
    }

    [Benchmark]
    public double CompleteGeneticIteration()
    {
        var crossover = _crossoverOperators[CrossoverName];
        var mutation = _mutationOperators[MutationName];
        
        // Calculate fitness for current population
        var fitnessScores = new double[_population.Length];
        for (int i = 0; i < _population.Length; i++)
        {
            fitnessScores[i] = _fitnessFunction.CalculateFitness(_population[i], _cities);
        }
        
        // Create offspring through crossover
        var offspring = new Tour[_population.Length];
        for (int i = 0; i < _population.Length; i += 2)
        {
            var parent1 = _population[i];
            var parent2 = _population[(i + 1) % _population.Length];
            var (child1, child2) = crossover.Crossover(parent1, parent2, _random);
            offspring[i] = child1;
            if (i + 1 < offspring.Length)
                offspring[i + 1] = child2;
        }
        
        // Apply mutations
        foreach (var tour in offspring)
        {
            mutation.Mutate(tour, _random);
        }
        
        // Calculate best fitness from offspring
        var bestFitness = double.MinValue;
        foreach (var tour in offspring)
        {
            var fitness = _fitnessFunction.CalculateFitness(tour, _cities);
            if (fitness > bestFitness)
                bestFitness = fitness;
        }
        
        return bestFitness;
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
