using TspLab.Domain.Entities;
using TspLab.Domain.Models;

namespace TspLab.Web.Services;

/// <summary>
/// Service for sharing TSPLIB data between components
/// </summary>
public class TspLibDataService
{
    private readonly List<TspLibTestProblem> _tspLibProblems = new();

    public event Action? OnDataChanged;

    /// <summary>
    /// Add a TSPLIB problem to the available test problems
    /// </summary>
    public void AddTspLibProblem(TspLibProcessedResult result)
    {
        // Remove existing problem with the same name
        _tspLibProblems.RemoveAll(p => p.Name == result.ProblemName);
        
        // Add new problem
        _tspLibProblems.Add(new TspLibTestProblem
        {
            Name = result.ProblemName,
            Description = result.Description,
            CityCount = result.CityCount,
            Cities = result.Cities,
            MdsStress = result.MdsStress,
            IsSelected = false,
            IsFromTspLib = true
        });

        OnDataChanged?.Invoke();
    }

    /// <summary>
    /// Get all available TSPLIB problems
    /// </summary>
    public IReadOnlyList<TspLibTestProblem> GetTspLibProblems()
    {
        return _tspLibProblems.AsReadOnly();
    }

    /// <summary>
    /// Remove a TSPLIB problem
    /// </summary>
    public void RemoveTspLibProblem(string name)
    {
        _tspLibProblems.RemoveAll(p => p.Name == name);
        OnDataChanged?.Invoke();
    }

    /// <summary>
    /// Clear all TSPLIB problems
    /// </summary>
    public void ClearTspLibProblems()
    {
        _tspLibProblems.Clear();
        OnDataChanged?.Invoke();
    }
}

/// <summary>
/// Extended test problem that includes TSPLIB data
/// </summary>
public class TspLibTestProblem
{
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public int CityCount { get; set; }
    public City[] Cities { get; set; } = Array.Empty<City>();
    public double MdsStress { get; set; }
    public bool IsSelected { get; set; }
    public bool IsFromTspLib { get; set; }
}
