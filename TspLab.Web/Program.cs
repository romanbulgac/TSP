using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.AspNetCore.SignalR.Client;
using TspLab.Web;
using TspLab.Web.Services;
using TspLab.Infrastructure.Extensions;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// Configure HTTP client for API calls with increased timeout and buffer limits
builder.Services.AddScoped(sp => 
{
    // Determine if we're running in Docker context by checking the current URL
    var baseUri = new Uri(builder.HostEnvironment.BaseAddress);
    Uri apiBaseAddress;
    
    // If running on port 8080, we're likely in Docker
    if (baseUri.Port == 8080)
    {
        // Docker environment - use the same host but API is proxied
        apiBaseAddress = new Uri($"{baseUri.Scheme}://{baseUri.Host}:{baseUri.Port}");
    }
    else
    {
        // Local development - API runs on port 5201
        apiBaseAddress = new Uri("http://localhost:5201");
    }
    
    var httpClient = new HttpClient
    {
        BaseAddress = apiBaseAddress,
        Timeout = TimeSpan.FromMinutes(10) // Increase timeout for large file uploads
    };
    
    // Set large buffer size for file uploads
    httpClient.MaxResponseContentBufferSize = 100 * 1024 * 1024; // 100 MB
    
    return httpClient;
});

// Register application services
builder.Services.AddScoped<TspApiService>();
builder.Services.AddScoped<SignalRService>();
builder.Services.AddSingleton<TspLibDataService>();
builder.Services.AddSingleton<CityDataService>();

// Register CSV export services
builder.Services.AddSingleton<TspLab.Application.Services.CsvExportService>();
builder.Services.AddSingleton<TspLab.Application.Services.BenchmarkDataCollector>();

// Register state management
builder.Services.AddScoped<TspLab.Domain.Interfaces.IAlgorithmStateManager, TspLab.Infrastructure.Services.BrowserStateManager>();

// Register infrastructure services (including TSP solvers)
builder.Services.AddInfrastructure();

await builder.Build().RunAsync();
