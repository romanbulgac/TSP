using TspLab.Domain.Entities;
using TspLab.Domain.Models;

namespace TspLab.Infrastructure.Services;

/// <summary>
/// Service for performing Multi-dimensional Scaling to reconstruct 2D coordinates from distance matrices
/// </summary>
public class MdsService
{
    /// <summary>
    /// Reconstructs 2D coordinates from a distance matrix using classical MDS
    /// </summary>
    /// <param name="distanceMatrix">Symmetric distance matrix</param>
    /// <param name="config">MDS configuration</param>
    /// <returns>Array of cities with reconstructed coordinates</returns>
    public City[] ReconstructCoordinates(double[,] distanceMatrix, MdsConfig? config = null)
    {
        config ??= new MdsConfig();
        var n = distanceMatrix.GetLength(0);
        
        if (n != distanceMatrix.GetLength(1))
            throw new ArgumentException("Distance matrix must be square");

        if (n < 3)
            throw new ArgumentException("At least 3 cities are required for MDS");

        // Step 1: Create squared distance matrix
        var squaredDistances = new double[n, n];
        for (int i = 0; i < n; i++)
        {
            for (int j = 0; j < n; j++)
            {
                squaredDistances[i, j] = distanceMatrix[i, j] * distanceMatrix[i, j];
            }
        }

        // Step 2: Apply double centering to get the Gram matrix
        var gramMatrix = ApplyDoubleCentering(squaredDistances);

        // Step 3: Perform eigenvalue decomposition
        var (eigenvalues, eigenvectors) = EigenDecomposition(gramMatrix);

        // Step 4: Extract the two largest positive eigenvalues and their eigenvectors
        var coordinates = ExtractCoordinates(eigenvalues, eigenvectors, n);

        // Step 5: Create City objects
        var cities = new City[n];
        for (int i = 0; i < n; i++)
        {
            cities[i] = new City(
                Id: i,
                Name: $"City{i:D2}",
                X: coordinates[i, 0],
                Y: coordinates[i, 1]
            );
        }

        return cities;
    }

    /// <summary>
    /// Calculates the stress (goodness of fit) between original distances and reconstructed distances
    /// </summary>
    public double CalculateStress(double[,] originalDistances, City[] cities)
    {
        var n = cities.Length;
        var stress = 0.0;
        var totalOriginalDistanceSquared = 0.0;

        for (int i = 0; i < n; i++)
        {
            for (int j = i + 1; j < n; j++)
            {
                var originalDistance = originalDistances[i, j];
                var reconstructedDistance = cities[i].DistanceTo(cities[j]);
                
                var diff = originalDistance - reconstructedDistance;
                stress += diff * diff;
                totalOriginalDistanceSquared += originalDistance * originalDistance;
            }
        }

        return totalOriginalDistanceSquared > 0 ? Math.Sqrt(stress / totalOriginalDistanceSquared) : 0.0;
    }

    private double[,] ApplyDoubleCentering(double[,] squaredDistances)
    {
        var n = squaredDistances.GetLength(0);
        var result = new double[n, n];

        // Calculate row means
        var rowMeans = new double[n];
        for (int i = 0; i < n; i++)
        {
            for (int j = 0; j < n; j++)
            {
                rowMeans[i] += squaredDistances[i, j];
            }
            rowMeans[i] /= n;
        }

        // Calculate overall mean
        var overallMean = rowMeans.Average();

        // Apply double centering: B = -0.5 * (D² - row_means - col_means + overall_mean)
        for (int i = 0; i < n; i++)
        {
            for (int j = 0; j < n; j++)
            {
                result[i, j] = -0.5 * (squaredDistances[i, j] - rowMeans[i] - rowMeans[j] + overallMean);
            }
        }

        return result;
    }

    private (double[] eigenvalues, double[,] eigenvectors) EigenDecomposition(double[,] matrix)
    {
        var n = matrix.GetLength(0);
        
        // For simplicity, we'll implement a basic power iteration method
        // In a production environment, you'd want to use a proper linear algebra library like MathNet.Numerics
        
        var eigenvalues = new double[n];
        var eigenvectors = new double[n, n];
        var workingMatrix = (double[,])matrix.Clone();

        // Find eigenvalues and eigenvectors using a simplified approach
        for (int k = 0; k < Math.Min(2, n); k++) // We only need the first 2 for 2D reconstruction
        {
            var (value, vector) = PowerIteration(workingMatrix, n);
            eigenvalues[k] = value;
            
            for (int i = 0; i < n; i++)
            {
                eigenvectors[i, k] = vector[i];
            }

            // Deflate the matrix (remove the found eigenvalue/eigenvector)
            DeflateMatrix(workingMatrix, value, vector);
        }

        return (eigenvalues, eigenvectors);
    }

    private (double eigenvalue, double[] eigenvector) PowerIteration(double[,] matrix, int n, int maxIterations = 1000)
    {
        var random = new Random(42);
        var vector = new double[n];
        
        // Initialize with random vector
        for (int i = 0; i < n; i++)
        {
            vector[i] = random.NextDouble() - 0.5;
        }

        // Normalize
        var norm = Math.Sqrt(vector.Sum(x => x * x));
        for (int i = 0; i < n; i++)
        {
            vector[i] /= norm;
        }

        double eigenvalue = 0;

        for (int iter = 0; iter < maxIterations; iter++)
        {
            // Multiply matrix by vector
            var newVector = new double[n];
            for (int i = 0; i < n; i++)
            {
                for (int j = 0; j < n; j++)
                {
                    newVector[i] += matrix[i, j] * vector[j];
                }
            }

            // Calculate eigenvalue (Rayleigh quotient)
            var numerator = 0.0;
            var denominator = 0.0;
            for (int i = 0; i < n; i++)
            {
                numerator += vector[i] * newVector[i];
                denominator += vector[i] * vector[i];
            }
            
            var newEigenvalue = denominator > 0 ? numerator / denominator : 0;

            // Normalize new vector
            norm = Math.Sqrt(newVector.Sum(x => x * x));
            if (norm > 1e-10)
            {
                for (int i = 0; i < n; i++)
                {
                    newVector[i] /= norm;
                }
            }

            // Check convergence
            if (Math.Abs(newEigenvalue - eigenvalue) < 1e-10)
                break;

            eigenvalue = newEigenvalue;
            vector = newVector;
        }

        return (eigenvalue, vector);
    }

    private void DeflateMatrix(double[,] matrix, double eigenvalue, double[] eigenvector)
    {
        var n = matrix.GetLength(0);
        
        // Subtract λvv^T from the matrix
        for (int i = 0; i < n; i++)
        {
            for (int j = 0; j < n; j++)
            {
                matrix[i, j] -= eigenvalue * eigenvector[i] * eigenvector[j];
            }
        }
    }

    private double[,] ExtractCoordinates(double[] eigenvalues, double[,] eigenvectors, int n)
    {
        var coordinates = new double[n, 2];

        // Use the two largest positive eigenvalues
        for (int dim = 0; dim < 2; dim++)
        {
            if (eigenvalues[dim] > 0)
            {
                var scale = Math.Sqrt(Math.Abs(eigenvalues[dim]));
                for (int i = 0; i < n; i++)
                {
                    coordinates[i, dim] = eigenvectors[i, dim] * scale;
                }
            }
        }

        // Center the coordinates
        var centerX = 0.0;
        var centerY = 0.0;
        for (int i = 0; i < n; i++)
        {
            centerX += coordinates[i, 0];
            centerY += coordinates[i, 1];
        }
        centerX /= n;
        centerY /= n;

        for (int i = 0; i < n; i++)
        {
            coordinates[i, 0] -= centerX;
            coordinates[i, 1] -= centerY;
        }

        // Scale to a reasonable range (0-1000)
        var maxDist = 0.0;
        for (int i = 0; i < n; i++)
        {
            var dist = Math.Sqrt(coordinates[i, 0] * coordinates[i, 0] + coordinates[i, 1] * coordinates[i, 1]);
            maxDist = Math.Max(maxDist, dist);
        }

        if (maxDist > 0)
        {
            var scale = 500.0 / maxDist; // Scale to fit in a 1000x1000 space centered at origin
            for (int i = 0; i < n; i++)
            {
                coordinates[i, 0] *= scale;
                coordinates[i, 1] *= scale;
            }
        }

        // Translate to positive quadrant
        for (int i = 0; i < n; i++)
        {
            coordinates[i, 0] += 500;
            coordinates[i, 1] += 500;
        }

        return coordinates;
    }
}
