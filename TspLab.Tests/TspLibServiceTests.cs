using Microsoft.Extensions.Logging;
using TspLab.Infrastructure.Services;
using Xunit;
using Xunit.Abstractions;

namespace TspLab.Tests;

/// <summary>
/// Tests for TspLibService validation functionality
/// </summary>
public class TspLibServiceTests
{
    private readonly ITestOutputHelper _output;
    private readonly ILogger<TspLibServiceTests> _logger;

    public TspLibServiceTests(ITestOutputHelper output)
    {
        _output = output;
        var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        _logger = loggerFactory.CreateLogger<TspLibServiceTests>();
    }

    [Fact]
    public void IsValidTspLibFile_Should_ReturnTrue_For_ValidXmlFile()
    {
        // Arrange
        var service = new TspLibService(_logger);
        var fileName = "test.xml";
        var fileContent = @"<?xml version=""1.0"" encoding=""UTF-8""?>
<travellingSalesmanProblemInstance>
  <name>test</name>
  <graph>
    <vertex>
      <edge cost=""0"">0</edge>
      <edge cost=""1"">1</edge>
    </vertex>
    <vertex>
      <edge cost=""1"">0</edge>
      <edge cost=""0"">1</edge>
    </vertex>
  </graph>
</travellingSalesmanProblemInstance>";

        // Act
        var result = service.IsValidTspLibFile(fileName, fileContent);

        // Assert
        Assert.True(result, "Valid XML file should be recognized as valid");
    }

    [Fact]
    public void IsValidTspLibFile_Should_ReturnTrue_For_Si1032XmlFile()
    {
        // Arrange
        var service = new TspLibService(_logger);
        var fileName = "si1032.xml";
        var filePath = "/Users/romanbulgac/Documents/University/Tema de Licenta/Code/ASP.NET BLAZOR/Backup 2/TSP/test-files/si1032.xml";
        
        // Act
        if (!File.Exists(filePath))
        {
            _output.WriteLine($"File not found: {filePath}");
            return; // Skip test if file doesn't exist
        }

        var fileContent = File.ReadAllText(filePath);
        _output.WriteLine($"File size: {fileContent.Length / (1024.0 * 1024.0):F2} MB");

        var result = service.IsValidTspLibFile(fileName, fileContent);

        // Assert
        Assert.True(result, "si1032.xml should be recognized as valid by validation layer");
    }

    [Fact]
    public void GetDetailedValidation_Should_Provide_Diagnostics_For_Si1032XmlFile()
    {
        // Arrange
        var service = new TspLibService(_logger);
        var fileName = "si1032.xml";
        var filePath = "/Users/romanbulgac/Documents/University/Tema de Licenta/Code/ASP.NET BLAZOR/Backup 2/TSP/test-files/si1032.xml";
        
        // Act
        if (!File.Exists(filePath))
        {
            _output.WriteLine($"File not found: {filePath}");
            return; // Skip test if file doesn't exist
        }

        var fileContent = File.ReadAllText(filePath);
        var (isValid, errorMessage, diagnostics) = service.GetDetailedValidation(fileName, fileContent);

        // Assert & Debug
        _output.WriteLine($"Validation result: {isValid}");
        _output.WriteLine($"Error message: {errorMessage}");
        _output.WriteLine("Diagnostics:");
        foreach (var kvp in diagnostics)
        {
            _output.WriteLine($"  {kvp.Key}: {kvp.Value}");
        }

        Assert.True(isValid, $"si1032.xml should be valid. Error: {errorMessage}");
    }

    [Fact]
    public void ProcessTspLibFile_Should_Work_For_Si1032XmlFile()
    {
        // Arrange
        var service = new TspLibService(_logger);
        var fileName = "si1032.xml";
        var filePath = "/Users/romanbulgac/Documents/University/Tema de Licenta/Code/ASP.NET BLAZOR/Backup 2/TSP/test-files/si1032.xml";
        
        // Act
        if (!File.Exists(filePath))
        {
            _output.WriteLine($"File not found: {filePath}");
            return; // Skip test if file doesn't exist
        }

        var fileContent = File.ReadAllText(filePath);
        
        // First check validation
        var isValid = service.IsValidTspLibFile(fileName, fileContent);
        _output.WriteLine($"Validation result: {isValid}");

        if (!isValid)
        {
            var (detailedValid, errorMessage, diagnostics) = service.GetDetailedValidation(fileName, fileContent);
            _output.WriteLine($"Detailed validation: {detailedValid}");
            _output.WriteLine($"Error: {errorMessage}");
            foreach (var kvp in diagnostics)
            {
                _output.WriteLine($"  {kvp.Key}: {kvp.Value}");
            }
        }

        // Try to process regardless of validation
        Exception? processingException = null;
        try
        {
            var result = service.ProcessTspLibFile(fileName, fileContent);
            _output.WriteLine($"Processing successful: {result.ProblemName} with {result.CityCount} cities");
        }
        catch (Exception ex)
        {
            processingException = ex;
            _output.WriteLine($"Processing failed: {ex.Message}");
        }

        // The validation should match the processing capability
        if (processingException == null)
        {
            Assert.True(isValid, "If processing succeeds, validation should also pass");
        }
    }
}
