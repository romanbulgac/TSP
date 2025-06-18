using TspLab.Domain.Entities;

namespace TspLab.Web.Services;

/// <summary>
/// Service for sharing city data between components
/// </summary>
public class CityDataService
{
    private City[]? _cities;
    private string _sourceName = "";

    public event Action? OnCitiesChanged;

    /// <summary>
    /// Set cities for use in other components
    /// </summary>
    public void SetCities(City[] cities, string sourceName = "")
    {
        _cities = cities?.ToArray(); // Create a copy to avoid reference issues
        _sourceName = sourceName;
        OnCitiesChanged?.Invoke();
    }

    /// <summary>
    /// Get the current cities
    /// </summary>
    public City[]? GetCities()
    {
        return _cities?.ToArray(); // Return a copy to avoid modifications
    }

    /// <summary>
    /// Get the source name of the cities
    /// </summary>
    public string GetSourceName()
    {
        return _sourceName;
    }

    /// <summary>
    /// Check if cities are available
    /// </summary>
    public bool HasCities()
    {
        return _cities != null && _cities.Length > 0;
    }

    /// <summary>
    /// Clear the cities data
    /// </summary>
    public void ClearCities()
    {
        _cities = null;
        _sourceName = "";
        OnCitiesChanged?.Invoke();
    }
}
