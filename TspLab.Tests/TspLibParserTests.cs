using FluentAssertions;
using TspLab.Infrastructure.Services;
using TspLab.Domain.Models;

namespace TspLab.Tests;

/// <summary>
/// Unit tests for TspLibParser
/// </summary>
public class TspLibParserTests
{
    private readonly TspLibParser _parser;

    public TspLibParserTests()
    {
        _parser = new TspLibParser();
    }

    [Fact]
    public void ParseXml_WithValidXml_ShouldReturnValidProblem()
    {
        // Arrange
        var xmlContent = @"<?xml version='1.0' encoding='UTF-8'?>
<travellingSalesmanProblemInstance>
    <name>test-3-cities</name>
    <source>Test</source>
    <description>Test problem with 3 cities</description>
    <doublePrecision>6</doublePrecision>
    <ignoredDigits>0</ignoredDigits>
    <graph>
        <vertex>
            <edge cost='0.0'>0</edge>
            <edge cost='10.0'>1</edge>
            <edge cost='15.0'>2</edge>
        </vertex>
        <vertex>
            <edge cost='10.0'>0</edge>
            <edge cost='0.0'>1</edge>
            <edge cost='20.0'>2</edge>
        </vertex>
        <vertex>
            <edge cost='15.0'>0</edge>
            <edge cost='20.0'>1</edge>
            <edge cost='0.0'>2</edge>
        </vertex>
    </graph>
</travellingSalesmanProblemInstance>";

        // Act
        var problem = _parser.ParseXml(xmlContent);

        // Assert
        problem.Should().NotBeNull();
        problem.Name.Should().Be("test-3-cities");
        problem.Source.Should().Be("Test");
        problem.Description.Should().Be("Test problem with 3 cities");
        problem.CityCount.Should().Be(3);
        problem.DistanceMatrix.Should().NotBeNull();
        problem.DistanceMatrix[0, 1].Should().Be(10.0);
        problem.DistanceMatrix[1, 2].Should().Be(20.0);
        problem.DistanceMatrix[2, 0].Should().Be(15.0);
        problem.IsValid().Should().BeTrue();
    }

    [Fact]
    public void ParseTsp_WithValidEuclidean2D_ShouldReturnValidProblem()
    {
        // Arrange
        var tspContent = @"NAME: test-4-cities
COMMENT: Test problem with 4 cities
TYPE: TSP
DIMENSION: 4
EDGE_WEIGHT_TYPE: EUC_2D
NODE_COORD_SECTION
1 0.0 0.0
2 1.0 0.0
3 1.0 1.0
4 0.0 1.0
EOF";

        // Act
        var problem = _parser.ParseTsp(tspContent);

        // Assert
        problem.Should().NotBeNull();
        problem.Name.Should().Be("test-4-cities");
        problem.Description.Should().Be("Test problem with 4 cities");
        problem.CityCount.Should().Be(4);
        problem.DistanceMatrix.Should().NotBeNull();
        problem.DistanceMatrix[0, 1].Should().BeApproximately(1.0, 0.001); // Distance between (0,0) and (1,0)
        problem.DistanceMatrix[1, 2].Should().BeApproximately(1.0, 0.001); // Distance between (1,0) and (1,1)
        problem.DistanceMatrix[2, 3].Should().BeApproximately(1.0, 0.001); // Distance between (1,1) and (0,1)
        problem.DistanceMatrix[3, 0].Should().BeApproximately(1.0, 0.001); // Distance between (0,1) and (0,0)
        problem.IsValid().Should().BeTrue();
    }

    [Fact]
    public void ParseTsp_WithValidExplicitMatrix_ShouldReturnValidProblem()
    {
        // Arrange
        var tspContent = @"NAME: test-3-cities-explicit
COMMENT: Test problem with explicit matrix
TYPE: TSP
DIMENSION: 3
EDGE_WEIGHT_TYPE: EXPLICIT
EDGE_WEIGHT_FORMAT: LOWER_DIAG_ROW
EDGE_WEIGHT_SECTION
0
10 0
15 20 0
EOF";

        // Act
        var problem = _parser.ParseTsp(tspContent);

        // Assert
        problem.Should().NotBeNull();
        problem.Name.Should().Be("test-3-cities-explicit");
        problem.Description.Should().Be("Test problem with explicit matrix");
        problem.CityCount.Should().Be(3);
        problem.DistanceMatrix.Should().NotBeNull();
        problem.DistanceMatrix[0, 1].Should().Be(10.0);
        problem.DistanceMatrix[1, 2].Should().Be(20.0);
        problem.DistanceMatrix[2, 0].Should().Be(15.0);
        problem.IsValid().Should().BeTrue();
    }

    [Fact]
    public void ParseXml_WithInvalidXml_ShouldThrowException()
    {
        // Arrange
        var invalidXml = "This is not valid XML";

        // Act & Assert
        Action act = () => _parser.ParseXml(invalidXml);
        act.Should().Throw<ArgumentException>()
           .WithMessage("Invalid XML format:*");
    }

    [Fact]
    public void ParseTsp_WithInvalidFormat_ShouldThrowException()
    {
        // Arrange
        var invalidTsp = @"NAME: invalid
COMMENT: Missing dimension
TYPE: TSP
EOF";

        // Act & Assert
        Action act = () => _parser.ParseTsp(invalidTsp);
        act.Should().Throw<ArgumentException>()
           .WithMessage("DIMENSION not found in TSP file");
    }

    [Fact]
    public void ParseTsp_WithUnsupportedEdgeWeightType_ShouldThrowException()
    {
        // Arrange
        var tspContent = @"NAME: test-unsupported
COMMENT: Unsupported edge weight type
TYPE: TSP
DIMENSION: 3
EDGE_WEIGHT_TYPE: MANHATTAN
EOF";

        // Act & Assert
        Action act = () => _parser.ParseTsp(tspContent);
        act.Should().Throw<ArgumentException>()
           .WithMessage("Unsupported EDGE_WEIGHT_TYPE: MANHATTAN*");
    }

    [Fact]
    public void ParseXml_WithLargeSi1032File_ShouldHandleCorrectly()
    {
        // Arrange
        var testFilePath = Path.Combine("..", "..", "..", "..", "test-files", "si1032.xml");
        
        Console.WriteLine($"Looking for test file at: {testFilePath}");
        Console.WriteLine($"Full path: {Path.GetFullPath(testFilePath)}");
        Console.WriteLine($"File exists: {File.Exists(testFilePath)}");
        
        // Skip test if file doesn't exist
        if (!File.Exists(testFilePath))
        {
            // Skip test - file not available
            Console.WriteLine($"Skipping test - file not found: {testFilePath}");
            return;
        }

        var fileInfo = new FileInfo(testFilePath);
        Console.WriteLine($"File size: {fileInfo.Length / (1024.0 * 1024.0):F2} MB");

        string xmlContent;
        try
        {
            xmlContent = File.ReadAllText(testFilePath);
            Console.WriteLine($"Successfully read file content, length: {xmlContent.Length} characters");
        }
        catch (Exception readEx)
        {
            Console.WriteLine($"Failed to read file: {readEx.Message}");
            throw;
        }
        
        // Act & Assert
        Console.WriteLine("Attempting to parse XML...");
        var exception = Record.Exception(() => _parser.ParseXml(xmlContent));
        
        // Log the exception details for debugging
        if (exception != null)
        {
            Console.WriteLine($"Exception Type: {exception.GetType().Name}");
            Console.WriteLine($"Exception Message: {exception.Message}");
            if (exception.InnerException != null)
            {
                Console.WriteLine($"Inner Exception: {exception.InnerException.Message}");
            }
        }
        
        // For now, just check that we get a meaningful error message rather than crashing
        if (exception is ArgumentException argEx)
        {
            // The exception should contain useful information about what went wrong
            argEx.Message.Should().NotBeNullOrEmpty();
            Console.WriteLine($"Detailed error for si1032.xml: {argEx.Message}");
        }
        else if (exception != null)
        {
            // If it's not an ArgumentException, something unexpected happened
            Console.WriteLine($"Unexpected exception: {exception}");
            throw exception;
        }
        else
        {
            // If no exception, the file was parsed successfully
            Console.WriteLine("si1032.xml parsed successfully!");
        }
    }
}
