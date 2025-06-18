using System.Text.Json.Serialization;

namespace TspLab.Domain.Models;

/// <summary>
/// Represents a TSPLIB problem instance
/// </summary>
public class TspLibProblem
{
    public string Name { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int DoublePrecision { get; set; }
    public int IgnoredDigits { get; set; }
    public double[,] DistanceMatrix { get; set; } = new double[0, 0];
    public int CityCount => DistanceMatrix.GetLength(0);
    
    /// <summary>
    /// Validates that the distance matrix is properly formatted
    /// </summary>
    public bool IsValid()
    {
        if (DistanceMatrix.GetLength(0) != DistanceMatrix.GetLength(1))
            return false;
            
        var size = DistanceMatrix.GetLength(0);
        
        // Check symmetry and zero diagonal
        for (int i = 0; i < size; i++)
        {
            if (DistanceMatrix[i, i] != 0)
                return false;
                
            for (int j = i + 1; j < size; j++)
            {
                if (Math.Abs(DistanceMatrix[i, j] - DistanceMatrix[j, i]) > 1e-10)
                    return false;
            }
        }
        
        return true;
    }
}

/// <summary>
/// Request model for uploading TSPLIB files
/// </summary>
public class TspLibUploadRequest
{
    public string FileName { get; set; } = string.Empty;
    public string FileContent { get; set; } = string.Empty;
}

/// <summary>
/// Response model containing cities generated from TSPLIB data using MDS
/// </summary>
public class TspLibProcessedResult
{
    public string ProblemName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int CityCount { get; set; }
    public City[] Cities { get; set; } = Array.Empty<City>();
    
    [JsonIgnore]
    public double[,] OriginalDistanceMatrix { get; set; } = new double[0, 0];
    
    public double MdsStress { get; set; } // Measure of how well MDS preserved distances
}

/// <summary>
/// Configuration for Multi-dimensional Scaling
/// </summary>
public class MdsConfig
{
    public int MaxIterations { get; set; } = 1000;
    public double Tolerance { get; set; } = 1e-6;
    public int RandomSeed { get; set; } = 42;
    public bool UseMetricMds { get; set; } = true;
}

/// <summary>
/// Result of TSPLIB file validation
/// </summary>
public class TspLibValidationResult
{
    public bool IsValid { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int CityCount { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string? ErrorMessage { get; set; }
}
