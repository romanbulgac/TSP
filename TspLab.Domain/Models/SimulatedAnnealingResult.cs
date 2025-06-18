namespace TspLab.Domain.Models;

public class SimulatedAnnealingResult
{
    public List<int> BestTour { get; set; } = new();
    public double BestDistance { get; set; }
    public int Iteration { get; set; }
    public int TotalIterations { get; set; }
    public double CurrentTemperature { get; set; }
    public double InitialTemperature { get; set; }
    public double AcceptanceRate { get; set; }
    public int TotalAccepted { get; set; }
    public int TotalRejected { get; set; }
    public int Improvements { get; set; }
    public bool IsComplete { get; set; }
    public TimeSpan ElapsedTime { get; set; }
    public double Progress => TotalIterations > 0 ? (double)Iteration / TotalIterations : 0;
    public string Phase { get; set; } = "Initializing";
}
