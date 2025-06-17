using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.AspNetCore.SignalR.Client;
using TspLab.Web;
using TspLab.Web.Services;
using TspLab.Infrastructure.Extensions;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// Configure HTTP client for API calls
builder.Services.AddScoped(sp => new HttpClient 
{ 
    BaseAddress = new Uri("http://localhost:5001") // API server address
});

// Register application services
builder.Services.AddScoped<TspApiService>();
builder.Services.AddScoped<SignalRService>();

// Register infrastructure services (including TSP solvers)
builder.Services.AddInfrastructure();

await builder.Build().RunAsync();
