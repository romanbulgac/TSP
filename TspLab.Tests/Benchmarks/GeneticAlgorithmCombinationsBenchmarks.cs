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
/// Comprehensive benchmark for testing all combinations of crossover and mutation operators
/// This benchmark focuses on solution quality and convergence characteristics
/// </summary>
[MemoryDiagnoser]
[SimpleJob]
public class GeneticAlgorithmCombinationsBenchmarks
{
    private City[] _cities = null!;
    private Dictionary<string, ICrossover> _crossoverOperators = null!;
    private Dictionary<string, IMutation> _mutationOperators = null!;
    private DistanceFitnessFunction _fitnessFunction = null!;
    private Random _random = null!;

    // Test parameters
    [Params(20, 50, 100)]
    public int CityCount { get; set; }

    [Params(30, 50, 100)]
    public int PopulationSize { get; set; }

    [Params(50, 100)]
    public int Generations { get; set; }

    [ParamsSource(nameof(CrossoverNames))]
    public string CrossoverName { get; set; } = "OrderCrossover";

    [ParamsSource(nameof(MutationNames))]
    public string MutationName { get; set; } = "SwapMutation";

    // Available operators
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
        _random = new Random(42);
        
        // Initialize all crossover operators
        _crossoverOperators = new Dictionary<string, ICrossover>
        {
            { "OrderCrossover", new OrderCrossover() },
            { "CycleCrossover", new CycleCrossover() },
            { "PartiallyMappedCrossover", new PartiallyMappedCrossover() },
            { "EdgeRecombinationCrossover", new EdgeRecombinationCrossover() }
        };

        // Initialize all mutation operators with same rate for fair comparison
        _mutationOperators = new Dictionary<string, IMutation>
        {
            { "SwapMutation", new SwapMutation { MutationRate = 0.02 } },
            { "InversionMutation", new InversionMutation { MutationRate = 0.02 } },
            { "TwoOptMutation", new TwoOptMutation { MutationRate = 0.02 } }
        };

        // Initialize fitness function
        _fitnessFunction = new DistanceFitnessFunction(new MemoryCache(new MemoryCacheOptions()));
    }

    [IterationSetup]
    public void IterationSetup()
    {
        // Generate new cities for each iteration to ensure fair testing
        _cities = GenerateRandomCities(CityCount);
    }

    [Benchmark]
    public GeneticAlgorithmResult RunGeneticAlgorithm()
    {
        var crossover = _crossoverOperators[CrossoverName];
        var mutation = _mutationOperators[MutationName];
        
        // Initialize population
        var population = InitializePopulation(PopulationSize);
        var fitnessScores = new double[PopulationSize];
        
        var bestDistance = double.MaxValue;
        var bestTour = population[0];
        var generationsBestDistance = new List<double>();
        
        // Run genetic algorithm
        for (int generation = 0; generation < Generations; generation++)
        {
            // Calculate fitness for all individuals
            for (int i = 0; i < PopulationSize; i++)
            {
                fitnessScores[i] = _fitnessFunction.CalculateFitness(population[i], _cities);
                var distance = CalculateTourDistance(population[i], _cities);
                
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    bestTour = population[i].Clone();
                }
            }
            
            generationsBestDistance.Add(bestDistance);
            
            // Create new population through selection, crossover, and mutation
            var newPopulation = new Tour[PopulationSize];
            
            // Elitism: keep best 10% of population
            var eliteCount = PopulationSize / 10;
            var sortedIndices = Enumerable.Range(0, PopulationSize)
                .OrderByDescending(i => fitnessScores[i])
                .ToArray();
            
            for (int i = 0; i < eliteCount; i++)
            {
                newPopulation[i] = population[sortedIndices[i]].Clone();
            }
            
            // Fill rest of population with offspring
            for (int i = eliteCount; i < PopulationSize; i += 2)
            {
                var parent1 = TournamentSelection(population, fitnessScores, 3);
                var parent2 = TournamentSelection(population, fitnessScores, 3);
                
                var (child1, child2) = crossover.Crossover(parent1, parent2, _random);
                
                mutation.Mutate(child1, _random);
                newPopulation[i] = child1;
                
                if (i + 1 < PopulationSize)
                {
                    mutation.Mutate(child2, _random);
                    newPopulation[i + 1] = child2;
                }
            }
            
            population = newPopulation;
        }
        
        // Calculate final statistics
        var finalBestFitness = _fitnessFunction.CalculateFitness(bestTour, _cities);
        
        return new GeneticAlgorithmResult
        {
            Generation = Generations,
            BestDistance = bestDistance,
            BestFitness = finalBestFitness,
            BestTour = bestTour.Cities.ToArray(),
            IsComplete = true,
            ElapsedMilliseconds = 0 // Not measuring time in this benchmark
        };
    }

    private Tour[] InitializePopulation(int size)
    {
        var population = new Tour[size];
        
        for (int i = 0; i < size; i++)
        {
            var cities = Enumerable.Range(0, _cities.Length).ToArray();
            
            // Use different initialization strategies for diversity
            if (i == 0)
            {
                // Use nearest neighbor for first individual
                cities = SolveNearestNeighborTour(_cities);
            }
            else
            {
                // Random shuffle for others
                Shuffle(cities, _random);
            }
            
            population[i] = new Tour(cities);
        }
        
        return population;
    }

    private Tour TournamentSelection(Tour[] population, double[] fitnessScores, int tournamentSize)
    {
        var best = _random.Next(population.Length);
        var bestFitness = fitnessScores[best];
        
        for (int i = 1; i < tournamentSize; i++)
        {
            var candidate = _random.Next(population.Length);
            if (fitnessScores[candidate] > bestFitness)
            {
                best = candidate;
                bestFitness = fitnessScores[candidate];
            }
        }
        
        return population[best];
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

    private static int[] SolveNearestNeighborTour(City[] cities)
    {
        var visited = new bool[cities.Length];
        var tour = new int[cities.Length];
        var current = 0;
        visited[0] = true;
        tour[0] = 0;

        for (int i = 1; i < cities.Length; i++)
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
                visited[nearest] = true;
                tour[i] = nearest;
                current = nearest;
            }
        }

        return tour;
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
