using System.Globalization;
using System.Xml.Linq;
using TspLab.Domain.Models;

namespace TspLab.Infrastructure.Services;

/// <summary>
/// Service for parsing TSPLIB format files
/// </summary>
public class TspLibParser
{
    /// <summary>
    /// Parses a TSPLIB XML file and extracts the distance matrix
    /// </summary>
    /// <param name="xmlContent">XML content as string</param>
    /// <returns>Parsed TSPLIB problem</returns>
    /// <exception cref="ArgumentException">Thrown when XML is invalid</exception>
    public TspLibProblem ParseXml(string xmlContent)
    {
        try
        {
            // Check file size and memory constraints
            var fileSizeMB = xmlContent.Length / (1024.0 * 1024.0);
            if (fileSizeMB > 80) // Increased limit to 80MB for larger files
            {
                throw new ArgumentException($"XML file is very large ({fileSizeMB:F1}MB). This may cause memory issues. Consider using a smaller file or the .tsp format instead.");
            }

            // Force garbage collection before parsing large files
            if (fileSizeMB > 10)
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
            }

            XDocument doc;
            try
            {
                doc = XDocument.Parse(xmlContent);
            }
            catch (System.Xml.XmlException xmlEx)
            {
                throw new ArgumentException($"Invalid XML format: {xmlEx.Message}. Line {xmlEx.LineNumber}, Position {xmlEx.LinePosition}");
            }
            catch (OutOfMemoryException)
            {
                throw new ArgumentException($"File too large to process ({fileSizeMB:F1}MB). The system ran out of memory. Try using a smaller file or the .tsp format.");
            }
            
            var root = doc.Root ?? throw new ArgumentException("Invalid XML: no root element");

            if (root.Name.LocalName != "travellingSalesmanProblemInstance")
                throw new ArgumentException("Invalid TSPLIB XML: missing travellingSalesmanProblemInstance root");

            var problem = new TspLibProblem
            {
                Name = GetElementValue(root, "name"),
                Source = GetElementValue(root, "source"),
                Description = GetElementValue(root, "description"),
                DoublePrecision = GetElementIntValue(root, "doublePrecision"),
                IgnoredDigits = GetElementIntValue(root, "ignoredDigits")
            };

            // Parse the graph section
            var graphElement = root.Element("graph") ?? 
                throw new ArgumentException("Invalid TSPLIB XML: missing graph element");

            var vertices = graphElement.Elements("vertex").ToArray();
            var cityCount = vertices.Length;

            if (cityCount == 0)
                throw new ArgumentException("Invalid TSPLIB XML: no vertices found");

            if (cityCount > 10000) // Warn for very large problems
            {
                throw new ArgumentException($"Problem size is very large ({cityCount} cities). This may cause performance issues. Consider using a smaller problem or optimizing your system.");
            }

            // Initialize distance matrix
            var distanceMatrix = new double[cityCount, cityCount];

            // Parse edges for each vertex with progress reporting for large files
            var processedVertices = 0;
            for (int i = 0; i < vertices.Length; i++)
            {
                var vertex = vertices[i];
                var edges = vertex.Elements("edge").ToArray();

                foreach (var edge in edges)
                {
                    var targetIndex = int.Parse(edge.Value);
                    var cost = double.Parse(edge.Attribute("cost")?.Value ?? "0", 
                        CultureInfo.InvariantCulture);

                    // Handle scientific notation properly
                    if (edge.Attribute("cost")?.Value.Contains("e") == true)
                    {
                        cost = double.Parse(edge.Attribute("cost")?.Value ?? "0", 
                            NumberStyles.Float, CultureInfo.InvariantCulture);
                    }

                    distanceMatrix[i, targetIndex] = cost;
                }

                processedVertices++;
                
                // Report progress for large files (every 1000 vertices)
                if (cityCount > 1000 && processedVertices % 1000 == 0)
                {
                    // This could be logged or reported to UI if needed
                    var progress = (double)processedVertices / cityCount * 100;
                    // Console.WriteLine($"Processing progress: {progress:F1}% ({processedVertices}/{cityCount})");
                }
            }

            // Ensure matrix is symmetric and has zero diagonal
            for (int i = 0; i < cityCount; i++)
            {
                distanceMatrix[i, i] = 0;
                for (int j = i + 1; j < cityCount; j++)
                {
                    // Use the maximum of both directions to handle any asymmetries
                    var distance = Math.Max(distanceMatrix[i, j], distanceMatrix[j, i]);
                    distanceMatrix[i, j] = distance;
                    distanceMatrix[j, i] = distance;
                }
            }

            problem.DistanceMatrix = distanceMatrix;

            if (!problem.IsValid())
                throw new ArgumentException("Generated distance matrix is not valid");

            return problem;
        }
        catch (Exception ex) when (!(ex is ArgumentException))
        {
            throw new ArgumentException($"Failed to parse TSPLIB XML: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Parses a TSPLIB .tsp file format (standard text format)
    /// </summary>
    /// <param name="tspContent">TSP file content</param>
    /// <returns>Parsed TSPLIB problem</returns>
    public TspLibProblem ParseTsp(string tspContent)
    {
        var lines = tspContent.Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(l => l.Trim())
            .Where(l => !string.IsNullOrEmpty(l))
            .ToArray();

        var problem = new TspLibProblem();
        var currentSection = string.Empty;
        var dimension = 0;
        var edgeWeightType = string.Empty;

        // Parse header information
        foreach (var line in lines)
        {
            if (line.StartsWith("NAME"))
                problem.Name = ExtractValue(line);
            else if (line.StartsWith("COMMENT"))
                problem.Description = ExtractValue(line);
            else if (line.StartsWith("DIMENSION"))
                dimension = int.Parse(ExtractValue(line));
            else if (line.StartsWith("EDGE_WEIGHT_TYPE"))
                edgeWeightType = ExtractValue(line);
            else if (line.StartsWith("EDGE_WEIGHT_SECTION"))
            {
                currentSection = "EDGE_WEIGHT_SECTION";
                break;
            }
        }

        if (dimension == 0)
            throw new ArgumentException("DIMENSION not found in TSP file");

        if (edgeWeightType != "EXPLICIT" && edgeWeightType != "EUC_2D")
            throw new ArgumentException($"Unsupported EDGE_WEIGHT_TYPE: {edgeWeightType}. Supported types: EXPLICIT, EUC_2D.");

        double[,] distanceMatrix;

        if (edgeWeightType == "EUC_2D")
        {
            // Parse coordinates and calculate Euclidean distances
            distanceMatrix = ParseEuclidean2D(lines, dimension);
        }
        else
        {
            // Parse explicit distance matrix
            distanceMatrix = ParseExplicitMatrix(lines, dimension);
        }

        problem.DistanceMatrix = distanceMatrix;
        problem.Source = "TSPLIB";

        if (!problem.IsValid())
            throw new ArgumentException("Generated distance matrix is not valid");

        return problem;
    }

    private double[,] ParseEuclidean2D(string[] lines, int dimension)
    {
        var coordinates = new (double x, double y)[dimension];
        
        // Find NODE_COORD_SECTION
        var coordStartIndex = Array.FindIndex(lines, l => l.StartsWith("NODE_COORD_SECTION")) + 1;
        if (coordStartIndex == 0)
            throw new ArgumentException("NODE_COORD_SECTION not found in EUC_2D format file");

        // Parse coordinates
        var coordIndex = 0;
        for (int i = coordStartIndex; i < lines.Length && coordIndex < dimension; i++)
        {
            var line = lines[i];
            if (line.StartsWith("EOF") || line.StartsWith("DISPLAY_DATA_SECTION"))
                break;

            var parts = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 3)
            {
                // Format: nodeId x y [optional comments]
                if (double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var x) &&
                    double.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var y))
                {
                    coordinates[coordIndex] = (x, y);
                    coordIndex++;
                }
            }
        }

        if (coordIndex != dimension)
            throw new ArgumentException($"Expected {dimension} coordinates, but found {coordIndex}");

        // Calculate Euclidean distance matrix
        var distanceMatrix = new double[dimension, dimension];
        for (int i = 0; i < dimension; i++)
        {
            for (int j = 0; j < dimension; j++)
            {
                if (i == j)
                {
                    distanceMatrix[i, j] = 0;
                }
                else
                {
                    var dx = coordinates[i].x - coordinates[j].x;
                    var dy = coordinates[i].y - coordinates[j].y;
                    var distance = Math.Sqrt(dx * dx + dy * dy);
                    distanceMatrix[i, j] = distance;
                }
            }
        }

        return distanceMatrix;
    }

    private double[,] ParseExplicitMatrix(string[] lines, int dimension)
    {
        // Parse distance matrix
        var distanceMatrix = new double[dimension, dimension];
        var matrixStartIndex = Array.FindIndex(lines, l => l.StartsWith("EDGE_WEIGHT_SECTION")) + 1;
        
        var matrixValues = new List<double>();
        for (int i = matrixStartIndex; i < lines.Length; i++)
        {
            var line = lines[i];
            if (line.StartsWith("EOF") || line.StartsWith("DISPLAY_DATA_SECTION"))
                break;

            var values = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var value in values)
            {
                if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var dist))
                    matrixValues.Add(dist);
            }
        }

        // Fill the matrix (assuming lower triangular format)
        var valueIndex = 0;
        for (int i = 0; i < dimension; i++)
        {
            for (int j = 0; j <= i; j++)
            {
                if (i == j)
                {
                    distanceMatrix[i, j] = 0;
                    // IMPORTANT: Still need to increment valueIndex for diagonal values
                    // in LOWER_DIAG_ROW format as they are included in the data
                    if (valueIndex < matrixValues.Count)
                        valueIndex++;
                }
                else if (valueIndex < matrixValues.Count)
                {
                    var distance = matrixValues[valueIndex++];
                    distanceMatrix[i, j] = distance;
                    distanceMatrix[j, i] = distance;
                }
            }
        }

        return distanceMatrix;
    }

    private static string GetElementValue(XElement parent, string elementName)
    {
        return parent.Element(elementName)?.Value ?? string.Empty;
    }

    private static int GetElementIntValue(XElement parent, string elementName)
    {
        var value = GetElementValue(parent, elementName);
        return string.IsNullOrEmpty(value) ? 0 : int.Parse(value);
    }

    private static string ExtractValue(string line)
    {
        var colonIndex = line.IndexOf(':');
        return colonIndex >= 0 ? line.Substring(colonIndex + 1).Trim() : string.Empty;
    }
}
