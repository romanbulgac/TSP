using TspLab.Domain.Entities;
using TspLab.Domain.Interfaces;

namespace TspLab.Infrastructure.Crossover;

/// <summary>
/// Edge Recombination Crossover (ERX) operator for TSP
/// Preserves edge information from both parents to construct offspring
/// </summary>
public sealed class EdgeRecombinationCrossover : ICrossover
{
    /// <summary>
    /// Gets the name of this crossover operator
    /// </summary>
    public string Name => "EdgeRecombinationCrossover";

    /// <summary>
    /// Gets a description of this crossover operator
    /// </summary>
    public string Description => "Edge Recombination Crossover (ERX) - Preserves edge information from both parents";

    /// <summary>
    /// Performs edge recombination crossover between two parent tours
    /// </summary>
    /// <param name="parent1">First parent tour</param>
    /// <param name="parent2">Second parent tour</param>
    /// <param name="random">Random number generator</param>
    /// <returns>Tuple containing two offspring tours</returns>
    /// <exception cref="ArgumentNullException">Thrown when any parameter is null</exception>
    /// <exception cref="ArgumentException">Thrown when parents have different lengths</exception>
    public (Tour offspring1, Tour offspring2) Crossover(Tour parent1, Tour parent2, Random random)
    {
        ArgumentNullException.ThrowIfNull(parent1);
        ArgumentNullException.ThrowIfNull(parent2);
        ArgumentNullException.ThrowIfNull(random);
        
        if (parent1.Length != parent2.Length)
            throw new ArgumentException("Parent tours must have the same length");

        if (parent1.Length < 3)
            throw new ArgumentException("Tours must have at least 3 cities");

        return (
            CreateOffspring(parent1, parent2, random),
            CreateOffspring(parent2, parent1, random)
        );
    }

    /// <summary>
    /// Creates an offspring using edge recombination crossover
    /// </summary>
    /// <param name="primary">Primary parent (starting point preference)</param>
    /// <param name="secondary">Secondary parent</param>
    /// <param name="random">Random number generator</param>
    /// <returns>New offspring tour</returns>
    private static Tour CreateOffspring(Tour primary, Tour secondary, Random random)
    {
        var length = primary.Length;
        var offspring = new int[length];
        var used = new bool[length];
        
        // Build edge table combining edges from both parents
        var edgeTable = BuildEdgeTable(primary, secondary);
        
        // Start with a random city from primary parent
        var currentCity = primary.Cities[random.Next(length)];
        offspring[0] = currentCity;
        used[currentCity] = true;
        
        // Remove the chosen city from all edge lists
        RemoveCityFromEdgeTable(edgeTable, currentCity);
        
        // Build the rest of the tour
        for (int i = 1; i < length; i++)
        {
            var nextCity = SelectNextCity(edgeTable, currentCity, used, random);
            offspring[i] = nextCity;
            used[nextCity] = true;
            currentCity = nextCity;
            
            // Remove the chosen city from all edge lists
            RemoveCityFromEdgeTable(edgeTable, currentCity);
        }
        
        return new Tour(offspring);
    }
    
    /// <summary>
    /// Builds an edge table that maps each city to its neighboring cities in both parents
    /// </summary>
    /// <param name="parent1">First parent tour</param>
    /// <param name="parent2">Second parent tour</param>
    /// <returns>Dictionary mapping each city to its set of neighbors</returns>
    private static Dictionary<int, HashSet<int>> BuildEdgeTable(Tour parent1, Tour parent2)
    {
        var edgeTable = new Dictionary<int, HashSet<int>>();
        var length = parent1.Length;
        
        // Initialize edge table
        for (int i = 0; i < length; i++)
        {
            edgeTable[i] = new HashSet<int>();
        }
        
        // Add edges from parent1
        AddEdgesFromParent(edgeTable, parent1);
        
        // Add edges from parent2
        AddEdgesFromParent(edgeTable, parent2);
        
        return edgeTable;
    }
    
    /// <summary>
    /// Adds edges from a parent tour to the edge table
    /// </summary>
    /// <param name="edgeTable">The edge table to update</param>
    /// <param name="parent">Parent tour to extract edges from</param>
    private static void AddEdgesFromParent(Dictionary<int, HashSet<int>> edgeTable, Tour parent)
    {
        var cities = parent.Cities;
        var length = cities.Length;
        
        for (int i = 0; i < length; i++)
        {
            var currentCity = cities[i];
            var prevCity = cities[(i - 1 + length) % length];
            var nextCity = cities[(i + 1) % length];
            
            // Add bidirectional edges
            edgeTable[currentCity].Add(prevCity);
            edgeTable[currentCity].Add(nextCity);
        }
    }
    
    /// <summary>
    /// Removes a city from all edge lists in the edge table
    /// </summary>
    /// <param name="edgeTable">The edge table to update</param>
    /// <param name="cityToRemove">City to remove from all edge lists</param>
    private static void RemoveCityFromEdgeTable(Dictionary<int, HashSet<int>> edgeTable, int cityToRemove)
    {
        foreach (var edgeList in edgeTable.Values)
        {
            edgeList.Remove(cityToRemove);
        }
    }
    
    /// <summary>
    /// Selects the next city to add to the offspring tour
    /// </summary>
    /// <param name="edgeTable">Current edge table</param>
    /// <param name="currentCity">Current city in the tour</param>
    /// <param name="used">Array indicating which cities have been used</param>
    /// <param name="random">Random number generator</param>
    /// <returns>Next city to add to the tour</returns>
    private static int SelectNextCity(Dictionary<int, HashSet<int>> edgeTable, int currentCity, bool[] used, Random random)
    {
        var availableNeighbors = edgeTable[currentCity]
            .Where(city => !used[city])
            .ToList();
        
        if (availableNeighbors.Count > 0)
        {
            // Prefer cities with fewer available edges (breaking ties randomly)
            var minEdgeCount = availableNeighbors.Min(city => edgeTable[city].Count(c => !used[c]));
            var candidates = availableNeighbors
                .Where(city => edgeTable[city].Count(c => !used[c]) == minEdgeCount)
                .ToList();
            
            return candidates[random.Next(candidates.Count)];
        }
        
        // If no neighbors are available, choose any unused city randomly
        var unusedCities = Enumerable.Range(0, used.Length)
            .Where(city => !used[city])
            .ToList();
        
        return unusedCities[random.Next(unusedCities.Count)];
    }
}
