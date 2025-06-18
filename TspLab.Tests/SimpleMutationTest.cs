using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TspLab.Infrastructure.Extensions;
using TspLab.Application.Services;
using TspLab.Domain.Interfaces;

namespace TspLab.Tests;

/// <summary>
/// Simple test to check if TwoOptMutation is available
/// </summary>
public class SimpleMutationTest
{
    public static void RunTest()
    {
        Console.WriteLine("Testing Mutation Operators");
        Console.WriteLine("==========================");

        // Setup DI container manually
        var services = new ServiceCollection();
        
        // Add infrastructure services
        services.AddInfrastructure();
        services.AddLogging(builder => builder.AddConsole());

        var serviceProvider = services.BuildServiceProvider();
        
        // Get strategy resolver
        var strategyResolver = serviceProvider.GetRequiredService<StrategyResolver>();
        var tspSolverService = serviceProvider.GetRequiredService<TspSolverService>();

        Console.WriteLine("Getting available strategies...");
        
        try
        {
            // Get available strategies
            var strategies = tspSolverService.GetAvailableStrategies();

            Console.WriteLine($"Found {strategies.Crossovers.Count} crossover operators:");
            foreach (var crossover in strategies.Crossovers)
            {
                Console.WriteLine($"  - {crossover}");
            }

            Console.WriteLine();
            Console.WriteLine($"Found {strategies.Mutations.Count} mutation operators:");
            foreach (var mutation in strategies.Mutations)
            {
                Console.WriteLine($"  - {mutation}");
            }

            Console.WriteLine();
            Console.WriteLine($"Found {strategies.FitnessFunctions.Count} fitness functions:");
            foreach (var fitness in strategies.FitnessFunctions)
            {
                Console.WriteLine($"  - {fitness}");
            }

            Console.WriteLine();
            
            // Test specific mutations
            Console.WriteLine("Testing specific mutation resolvers:");
            
            string[] testMutations = { "TwoOptMutation", "SwapMutation", "InversionMutation" };
            
            foreach (var mutationName in testMutations)
            {
                try
                {
                    var mutation = strategyResolver.ResolveMutation(mutationName);
                    Console.WriteLine($"✓ {mutationName} resolved successfully: {mutation.Name}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"✗ {mutationName} resolution failed: {ex.Message}");
                }
            }

            // Check if TwoOptMutation is in the list
            Console.WriteLine();
            bool twoOptFound = strategies.Mutations.Contains("TwoOptMutation");
            Console.WriteLine($"TwoOptMutation in strategies list: {twoOptFound}");
            
            if (!twoOptFound)
            {
                Console.WriteLine("ERROR: TwoOptMutation is not appearing in the strategies list!");
                Console.WriteLine("This could be due to:");
                Console.WriteLine("1. Missing using directives");
                Console.WriteLine("2. DI registration issues");
                Console.WriteLine("3. Class not implementing IMutation correctly");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error getting strategies: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
        }

        Console.WriteLine();
        Console.WriteLine("Test completed.");
    }
}
