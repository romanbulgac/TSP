using TspLab.Domain.Models;
using TspLab.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace TspLab.Infrastructure.Services;

/// <summary>
/// Service for processing TSPLIB files and generating cities using MDS
/// </summary>
public class TspLibService
{
    private readonly TspLibParser _parser;
    private readonly MdsService _mdsService;
    private readonly ILogger? _logger;

    public TspLibService(ILogger? logger = null)
    {
        _parser = new TspLibParser();
        _mdsService = new MdsService();
        _logger = logger;
    }

    /// <summary>
    /// Processes a TSPLIB file and returns cities with MDS-reconstructed coordinates
    /// </summary>
    /// <param name="fileName">Name of the uploaded file</param>
    /// <param name="fileContent">File content as string</param>
    /// <returns>Processed result with cities and metadata</returns>
    public TspLibProcessedResult ProcessTspLibFile(string fileName, string fileContent)
    {
        try
        {
            _logger?.LogInformation("Processing TSPLIB file: {FileName}", fileName);

            // Parse the file based on extension
            TspLibProblem problem;
            if (fileName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
            {
                problem = _parser.ParseXml(fileContent);
            }
            else if (fileName.EndsWith(".tsp", StringComparison.OrdinalIgnoreCase))
            {
                problem = _parser.ParseTsp(fileContent);
            }
            else
            {
                throw new ArgumentException($"Unsupported file format. Expected .xml or .tsp, got: {fileName}");
            }

            _logger?.LogInformation("Parsed problem '{ProblemName}' with {CityCount} cities", 
                problem.Name, problem.CityCount);

            // Generate coordinates using MDS
            var cities = _mdsService.ReconstructCoordinates(problem.DistanceMatrix);
            
            // Calculate stress to measure reconstruction quality
            var stress = _mdsService.CalculateStress(problem.DistanceMatrix, cities);
            
            _logger?.LogInformation("MDS reconstruction completed with stress: {Stress:F6}", stress);

            return new TspLibProcessedResult
            {
                ProblemName = problem.Name,
                Description = problem.Description,
                CityCount = problem.CityCount,
                Cities = cities,
                OriginalDistanceMatrix = problem.DistanceMatrix,
                MdsStress = stress
            };
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error processing TSPLIB file: {FileName}", fileName);
            throw;
        }
    }

    /// <summary>
    /// Validates if a file appears to be a valid TSPLIB file
    /// </summary>
    /// <param name="fileName">File name</param>
    /// <param name="fileContent">File content</param>
    /// <returns>True if file appears to be a valid TSPLIB file</returns>
    public bool IsValidTspLibFile(string fileName, string fileContent)
    {
        try
        {
            _logger?.LogInformation("Validating TSPLIB file: {FileName}, size: {Size} bytes", 
                fileName, fileContent?.Length ?? 0);

            if (string.IsNullOrWhiteSpace(fileContent))
            {
                _logger?.LogWarning("File content is empty or null");
                return false;
            }

            // Log file size for diagnostic purposes
            var fileSizeMB = fileContent.Length / (1024.0 * 1024.0);
            _logger?.LogInformation("File size: {SizeMB:F2} MB", fileSizeMB);

            if (fileName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
            {
                return ValidateXmlFile(fileContent);
            }
            else if (fileName.EndsWith(".tsp", StringComparison.OrdinalIgnoreCase))
            {
                return ValidateTspFile(fileContent);
            }

            _logger?.LogWarning("Unsupported file extension: {FileName}", fileName);
            return false;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error validating TSPLIB file: {FileName}", fileName);
            return false;
        }
    }

    /// <summary>
    /// Validates XML TSPLIB file with better error reporting
    /// </summary>
    private bool ValidateXmlFile(string fileContent)
    {
        try
        {
            // Check file size first
            var fileSizeMB = fileContent.Length / (1024.0 * 1024.0);
            _logger?.LogInformation("Validating XML file of size: {SizeMB:F2} MB", fileSizeMB);
            
            if (fileSizeMB > 80)
            {
                _logger?.LogError("XML file too large: {SizeMB:F1}MB (limit: 80MB)", fileSizeMB);
                return false;
            }

            // Basic string checks first (fast)
            if (!fileContent.Contains("travellingSalesmanProblemInstance"))
            {
                _logger?.LogWarning("XML file missing 'travellingSalesmanProblemInstance' root element");
                return false;
            }

            if (!fileContent.Contains("<graph>"))
            {
                _logger?.LogWarning("XML file missing '<graph>' element");
                return false;
            }

            if (!fileContent.Contains("<vertex>"))
            {
                _logger?.LogWarning("XML file missing '<vertex>' elements");
                return false;
            }

            // Try to parse XML structure without full processing
            try
            {
                var xmlDoc = System.Xml.Linq.XDocument.Parse(fileContent);
                var root = xmlDoc.Root;
                
                if (root?.Name.LocalName != "travellingSalesmanProblemInstance")
                {
                    _logger?.LogWarning("Invalid XML root element: {RootName}", root?.Name.LocalName);
                    return false;
                }

                var graphElement = root.Element("graph");
                if (graphElement == null)
                {
                    _logger?.LogWarning("Missing graph element in XML");
                    return false;
                }

                var vertices = graphElement.Elements("vertex");
                var vertexCount = vertices.Count();
                _logger?.LogInformation("XML validation successful: {VertexCount} vertices found", vertexCount);

                return vertexCount > 0;
            }
            catch (System.Xml.XmlException xmlEx)
            {
                _logger?.LogError(xmlEx, "XML parsing error: {Message}", xmlEx.Message);
                return false;
            }
        }
        catch (OutOfMemoryException memEx)
        {
            _logger?.LogError(memEx, "Out of memory while validating XML file");
            return false;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Unexpected error validating XML file: {Message}", ex.Message);
            return false;
        }
    }

    /// <summary>
    /// Validates TSP file format
    /// </summary>
    private bool ValidateTspFile(string fileContent)
    {
        try
        {
            var hasDimension = fileContent.Contains("DIMENSION");
            var hasEdgeWeights = fileContent.Contains("EDGE_WEIGHT_SECTION");
            var hasNodeCoords = fileContent.Contains("NODE_COORD_SECTION");

            if (!hasDimension)
            {
                _logger?.LogWarning("TSP file missing DIMENSION");
                return false;
            }

            if (!hasEdgeWeights && !hasNodeCoords)
            {
                _logger?.LogWarning("TSP file missing both EDGE_WEIGHT_SECTION and NODE_COORD_SECTION");
                return false;
            }

            _logger?.LogInformation("TSP validation successful: dimension={HasDimension}, edges={HasEdges}, coords={HasCoords}",
                hasDimension, hasEdgeWeights, hasNodeCoords);

            return true;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error validating TSP file: {Message}", ex.Message);
            return false;
        }
    }

    /// <summary>
    /// Gets detailed validation information for debugging
    /// </summary>
    /// <param name="fileName">File name</param>
    /// <param name="fileContent">File content</param>
    /// <returns>Detailed validation result</returns>
    public (bool isValid, string errorMessage, Dictionary<string, object> diagnostics) GetDetailedValidation(string fileName, string fileContent)
    {
        var diagnostics = new Dictionary<string, object>();
        
        try
        {
            _logger?.LogInformation("Starting detailed validation for {FileName}, size: {Size} bytes", 
                fileName, fileContent?.Length ?? 0);

            if (string.IsNullOrWhiteSpace(fileContent))
            {
                diagnostics["issue"] = "empty_content";
                return (false, "File content is empty or null", diagnostics);
            }

            var fileSizeMB = fileContent.Length / (1024.0 * 1024.0);
            diagnostics["fileSizeMB"] = Math.Round(fileSizeMB, 2);
            diagnostics["contentLength"] = fileContent.Length;
            diagnostics["fileExtension"] = Path.GetExtension(fileName).ToLowerInvariant();

            if (fileName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
            {
                return GetDetailedXmlValidation(fileContent, diagnostics);
            }
            else if (fileName.EndsWith(".tsp", StringComparison.OrdinalIgnoreCase))
            {
                return GetDetailedTspValidation(fileContent, diagnostics);
            }

            diagnostics["issue"] = "unsupported_extension";
            return (false, $"Unsupported file extension: {Path.GetExtension(fileName)}", diagnostics);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error in detailed validation for {FileName}", fileName);
            diagnostics["exception"] = ex.Message;
            diagnostics["exceptionType"] = ex.GetType().Name;
            return (false, $"Validation error: {ex.Message}", diagnostics);
        }
    }

    private (bool isValid, string errorMessage, Dictionary<string, object> diagnostics) GetDetailedXmlValidation(string fileContent, Dictionary<string, object> diagnostics)
    {
        try
        {
            // Check basic XML structure
            diagnostics["hasTspInstance"] = fileContent.Contains("travellingSalesmanProblemInstance");
            diagnostics["hasGraph"] = fileContent.Contains("<graph>");
            diagnostics["hasVertex"] = fileContent.Contains("<vertex>");

            if (!diagnostics["hasTspInstance"].Equals(true))
            {
                diagnostics["issue"] = "missing_tsp_instance_root";
                return (false, "XML file missing 'travellingSalesmanProblemInstance' root element", diagnostics);
            }

            if (!diagnostics["hasGraph"].Equals(true))
            {
                diagnostics["issue"] = "missing_graph_element";
                return (false, "XML file missing '<graph>' element", diagnostics);
            }

            if (!diagnostics["hasVertex"].Equals(true))
            {
                diagnostics["issue"] = "missing_vertex_elements";
                return (false, "XML file missing '<vertex>' elements", diagnostics);
            }

            // Try to parse XML structure
            try
            {
                var xmlDoc = System.Xml.Linq.XDocument.Parse(fileContent);
                var root = xmlDoc.Root;
                
                diagnostics["xmlParsed"] = true;
                diagnostics["rootName"] = root?.Name.LocalName ?? "null";

                if (root?.Name.LocalName != "travellingSalesmanProblemInstance")
                {
                    diagnostics["issue"] = "invalid_root_element";
                    return (false, $"Invalid XML root element: {root?.Name.LocalName}", diagnostics);
                }

                var graphElement = root.Element("graph");
                if (graphElement == null)
                {
                    diagnostics["issue"] = "missing_graph_element_parsed";
                    return (false, "Missing graph element in parsed XML", diagnostics);
                }

                var vertices = graphElement.Elements("vertex");
                var vertexCount = vertices.Count();
                diagnostics["vertexCount"] = vertexCount;
                
                if (vertexCount == 0)
                {
                    diagnostics["issue"] = "no_vertices_found";
                    return (false, "No vertices found in graph", diagnostics);
                }

                _logger?.LogInformation("XML validation successful: {VertexCount} vertices found", vertexCount);
                diagnostics["validationSuccess"] = true;
                
                return (true, "Valid TSPLIB XML file", diagnostics);
            }
            catch (System.Xml.XmlException xmlEx)
            {
                diagnostics["xmlParsed"] = false;
                diagnostics["xmlError"] = xmlEx.Message;
                diagnostics["xmlLineNumber"] = xmlEx.LineNumber;
                diagnostics["xmlLinePosition"] = xmlEx.LinePosition;
                diagnostics["issue"] = "xml_parse_error";
                return (false, $"XML parsing error: {xmlEx.Message} at line {xmlEx.LineNumber}, position {xmlEx.LinePosition}", diagnostics);
            }
        }
        catch (OutOfMemoryException memEx)
        {
            _logger?.LogError(memEx, "Out of memory while validating XML file");
            diagnostics["issue"] = "out_of_memory";
            return (false, "Out of memory while validating XML file", diagnostics);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Unexpected error validating XML file: {Message}", ex.Message);
            diagnostics["issue"] = "unexpected_error";
            diagnostics["exception"] = ex.Message;
            return (false, $"Unexpected error validating XML file: {ex.Message}", diagnostics);
        }
    }

    private (bool isValid, string errorMessage, Dictionary<string, object> diagnostics) GetDetailedTspValidation(string fileContent, Dictionary<string, object> diagnostics)
    {
        try
        {
            diagnostics["hasDimension"] = fileContent.Contains("DIMENSION");
            diagnostics["hasEdgeWeights"] = fileContent.Contains("EDGE_WEIGHT_SECTION");
            diagnostics["hasNodeCoords"] = fileContent.Contains("NODE_COORD_SECTION");

            if (!diagnostics["hasDimension"].Equals(true))
            {
                diagnostics["issue"] = "missing_dimension";
                return (false, "TSP file missing DIMENSION", diagnostics);
            }

            if (!diagnostics["hasEdgeWeights"].Equals(true) && !diagnostics["hasNodeCoords"].Equals(true))
            {
                diagnostics["issue"] = "missing_sections";
                return (false, "TSP file missing both EDGE_WEIGHT_SECTION and NODE_COORD_SECTION", diagnostics);
            }

            _logger?.LogInformation("TSP validation successful: dimension={HasDimension}, edges={HasEdges}, coords={HasCoords}",
                diagnostics["hasDimension"], diagnostics["hasEdgeWeights"], diagnostics["hasNodeCoords"]);

            diagnostics["validationSuccess"] = true;
            return (true, "Valid TSPLIB TSP file", diagnostics);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error validating TSP file: {Message}", ex.Message);
            diagnostics["issue"] = "tsp_validation_error";
            diagnostics["exception"] = ex.Message;
            return (false, $"Error validating TSP file: {ex.Message}", diagnostics);
        }
    }

    /// <summary>
    /// Extracts basic information from a TSPLIB file without full processing
    /// </summary>
    /// <param name="fileName">File name</param>
    /// <param name="fileContent">File content</param>
    /// <returns>Basic problem information</returns>
    public (string name, string description, int cityCount) GetFileInfo(string fileName, string fileContent)
    {
        try
        {
            if (fileName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
            {
                var problem = _parser.ParseXml(fileContent);
                return (problem.Name, problem.Description, problem.CityCount);
            }
            else if (fileName.EndsWith(".tsp", StringComparison.OrdinalIgnoreCase))
            {
                var lines = fileContent.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                var name = "";
                var description = "";
                var dimension = 0;

                foreach (var line in lines)
                {
                    var trimmedLine = line.Trim();
                    if (trimmedLine.StartsWith("NAME"))
                        name = ExtractValue(trimmedLine);
                    else if (trimmedLine.StartsWith("COMMENT"))
                        description = ExtractValue(trimmedLine);
                    else if (trimmedLine.StartsWith("DIMENSION"))
                        dimension = int.Parse(ExtractValue(trimmedLine));
                }

                return (name, description, dimension);
            }

            return ("Unknown", "Unknown format", 0);
        }
        catch
        {
            return ("Error", "Failed to parse file", 0);
        }
    }

    private static string ExtractValue(string line)
    {
        var colonIndex = line.IndexOf(':');
        return colonIndex >= 0 ? line.Substring(colonIndex + 1).Trim() : string.Empty;
    }
}
