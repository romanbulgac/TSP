using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TspLab.Domain.Interfaces;

namespace TspLab.Application.Services;

/// <summary>
/// Resolves genetic algorithm strategies based on configuration
/// </summary>
public sealed class StrategyResolver
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<StrategyResolver> _logger;

    public StrategyResolver(IServiceProvider serviceProvider, ILogger<StrategyResolver> logger)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Resolves a crossover operator by name
    /// </summary>
    /// <param name="name">Name of the crossover operator</param>
    /// <returns>Crossover operator instance</returns>
    /// <exception cref="InvalidOperationException">Thrown when operator is not found</exception>
    public ICrossover ResolveCrossover(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        var crossovers = _serviceProvider.GetServices<ICrossover>();
        var crossover = crossovers.FirstOrDefault(c => c.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

        if (crossover is null)
        {
            var availableNames = string.Join(", ", crossovers.Select(c => c.Name));
            _logger.LogError("Crossover '{Name}' not found. Available: {Available}", name, availableNames);
            throw new InvalidOperationException($"Crossover '{name}' not found. Available: {availableNames}");
        }

        _logger.LogDebug("Resolved crossover: {Name}", crossover.Name);
        return crossover;
    }

    /// <summary>
    /// Resolves a mutation operator by name
    /// </summary>
    /// <param name="name">Name of the mutation operator</param>
    /// <returns>Mutation operator instance</returns>
    /// <exception cref="InvalidOperationException">Thrown when operator is not found</exception>
    public IMutation ResolveMutation(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        var mutations = _serviceProvider.GetServices<IMutation>();
        var mutation = mutations.FirstOrDefault(m => m.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

        if (mutation is null)
        {
            var availableNames = string.Join(", ", mutations.Select(m => m.Name));
            _logger.LogError("Mutation '{Name}' not found. Available: {Available}", name, availableNames);
            throw new InvalidOperationException($"Mutation '{name}' not found. Available: {availableNames}");
        }

        _logger.LogDebug("Resolved mutation: {Name}", mutation.Name);
        return mutation;
    }

    /// <summary>
    /// Resolves a fitness function by name
    /// </summary>
    /// <param name="name">Name of the fitness function</param>
    /// <returns>Fitness function instance</returns>
    /// <exception cref="InvalidOperationException">Thrown when function is not found</exception>
    public IFitnessFunction ResolveFitnessFunction(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        var fitnessFunctions = _serviceProvider.GetServices<IFitnessFunction>();
        var fitnessFunction = fitnessFunctions.FirstOrDefault(f => f.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

        if (fitnessFunction is null)
        {
            var availableNames = string.Join(", ", fitnessFunctions.Select(f => f.Name));
            _logger.LogError("Fitness function '{Name}' not found. Available: {Available}", name, availableNames);
            throw new InvalidOperationException($"Fitness function '{name}' not found. Available: {availableNames}");
        }

        _logger.LogDebug("Resolved fitness function: {Name}", fitnessFunction.Name);
        return fitnessFunction;
    }

    /// <summary>
    /// Gets all available crossover operator names
    /// </summary>
    /// <returns>List of crossover operator names</returns>
    public IEnumerable<string> GetAvailableCrossovers()
    {
        return _serviceProvider.GetServices<ICrossover>().Select(c => c.Name);
    }

    /// <summary>
    /// Gets all available mutation operator names
    /// </summary>
    /// <returns>List of mutation operator names</returns>
    public IEnumerable<string> GetAvailableMutations()
    {
        return _serviceProvider.GetServices<IMutation>().Select(m => m.Name);
    }

    /// <summary>
    /// Gets all available fitness function names
    /// </summary>
    /// <returns>List of fitness function names</returns>
    public IEnumerable<string> GetAvailableFitnessFunctions()
    {
        return _serviceProvider.GetServices<IFitnessFunction>().Select(f => f.Name);
    }
}
