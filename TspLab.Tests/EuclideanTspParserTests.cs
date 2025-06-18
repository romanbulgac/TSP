using Xunit;
using TspLab.Infrastructure.Services;

namespace TspLab.Tests;

public class EuclideanTspParserTests
{
    [Fact]
    public void ParseEuclidean2D_ShouldCalculateCorrectDistances()
    {
        // Arrange
        var tspContent = @"
NAME: test4
TYPE: TSP
COMMENT: Test instance with 4 cities in a square
DIMENSION: 4
EDGE_WEIGHT_TYPE: EUC_2D
NODE_COORD_SECTION
1 0.0 0.0
2 10.0 0.0
3 10.0 10.0
4 0.0 10.0
EOF";

        var parser = new TspLibParser();

        // Act
        var result = parser.ParseTsp(tspContent);

        // Assert
        Assert.Equal("test4", result.Name);
        Assert.Equal("Test instance with 4 cities in a square", result.Description);
        Assert.NotNull(result.DistanceMatrix);
        Assert.Equal(4, result.DistanceMatrix.GetLength(0));
        Assert.Equal(4, result.DistanceMatrix.GetLength(1));

        // Check diagonal (should be 0)
        Assert.Equal(0, result.DistanceMatrix[0, 0]);
        Assert.Equal(0, result.DistanceMatrix[1, 1]);
        Assert.Equal(0, result.DistanceMatrix[2, 2]);
        Assert.Equal(0, result.DistanceMatrix[3, 3]);

        // Check distances (10 units for adjacent corners, ~14.14 for diagonal)
        Assert.Equal(10.0, result.DistanceMatrix[0, 1], 2); // (0,0) to (10,0)
        Assert.Equal(10.0, result.DistanceMatrix[1, 2], 2); // (10,0) to (10,10)
        Assert.Equal(10.0, result.DistanceMatrix[2, 3], 2); // (10,10) to (0,10)
        Assert.Equal(10.0, result.DistanceMatrix[3, 0], 2); // (0,10) to (0,0)

        // Diagonal distances
        Assert.Equal(14.14, result.DistanceMatrix[0, 2], 1); // (0,0) to (10,10)
        Assert.Equal(14.14, result.DistanceMatrix[1, 3], 1); // (10,0) to (0,10)

        // Check symmetry
        Assert.Equal(result.DistanceMatrix[0, 1], result.DistanceMatrix[1, 0]);
        Assert.Equal(result.DistanceMatrix[0, 2], result.DistanceMatrix[2, 0]);
        Assert.Equal(result.DistanceMatrix[1, 3], result.DistanceMatrix[3, 1]);
    }

    [Fact]
    public void ParseEuclidean2D_Berlin52_ShouldParseSuccessfully()
    {
        // Arrange
        var filePath = "test-files/berlin52.tsp";
        if (!File.Exists(filePath))
            return; // Skip test if file doesn't exist

        var tspContent = File.ReadAllText(filePath);
        var parser = new TspLibParser();

        // Act
        var result = parser.ParseTsp(tspContent);

        // Assert
        Assert.Equal("berlin52", result.Name);
        Assert.Equal(52, result.DistanceMatrix.GetLength(0));
        Assert.True(result.IsValid());

        // Check that distances are reasonable
        for (int i = 0; i < 52; i++)
        {
            for (int j = 0; j < 52; j++)
            {
                if (i == j)
                    Assert.Equal(0, result.DistanceMatrix[i, j]);
                else
                    Assert.True(result.DistanceMatrix[i, j] > 0);
            }
        }
    }

    [Fact]
    public void ParseEuclidean2D_LargeFile_ShouldParseSuccessfully()
    {
        // Arrange
        var filePath = "test-files/test1000.tsp";
        if (!File.Exists(filePath))
            return; // Skip test if file doesn't exist

        var tspContent = File.ReadAllText(filePath);
        var parser = new TspLibParser();

        // Act
        var result = parser.ParseTsp(tspContent);

        // Assert
        Assert.Equal("test1000", result.Name);
        Assert.Equal(1000, result.DistanceMatrix.GetLength(0));
        Assert.True(result.IsValid());

        // Verify distances are non-negative and symmetric
        for (int i = 0; i < Math.Min(100, 1000); i++) // Test first 100 to avoid long test
        {
            for (int j = 0; j < Math.Min(100, 1000); j++)
            {
                Assert.True(result.DistanceMatrix[i, j] >= 0);
                Assert.Equal(result.DistanceMatrix[i, j], result.DistanceMatrix[j, i], 6);
            }
        }
    }

    [Fact]
    public void ParseEuclidean2D_MissingNodeCoordSection_ShouldThrowException()
    {
        // Arrange
        var tspContent = @"
NAME: invalid
TYPE: TSP
DIMENSION: 2
EDGE_WEIGHT_TYPE: EUC_2D
EOF";

        var parser = new TspLibParser();

        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() => parser.ParseTsp(tspContent));
        Assert.Contains("NODE_COORD_SECTION not found", exception.Message);
    }

    [Fact]
    public void ParseEuclidean2D_InvalidCoordinates_ShouldThrowException()
    {
        // Arrange
        var tspContent = @"
NAME: invalid
TYPE: TSP
DIMENSION: 2
EDGE_WEIGHT_TYPE: EUC_2D
NODE_COORD_SECTION
1 invalid_x invalid_y
EOF";

        var parser = new TspLibParser();

        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() => parser.ParseTsp(tspContent));
        Assert.Contains("Expected 2 coordinates, but found", exception.Message);
    }
}
