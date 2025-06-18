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
    var httpClient = new HttpClient
    {
        BaseAddress = new Uri("http://localhost:5001"), // API server address
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

// Register state management
builder.Services.AddScoped<TspLab.Domain.Interfaces.IAlgorithmStateManager, TspLab.Infrastructure.Services.BrowserStateManager>();

// Register infrastructure services (including TSP solvers)
builder.Services.AddInfrastructure();

await builder.Build().RunAsync();
