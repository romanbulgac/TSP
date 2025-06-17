using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.StaticFiles;
using Serilog;
using TspLab.Application.Services;
using TspLab.Infrastructure.Extensions;
using TspLab.WebApi.Hubs;
using TspLab.WebApi.Models;
using TspLab.Domain.Models;

var builder = WebApplication.CreateBuilder(args);

// Add Serilog
builder.Host.UseSerilog((context, config) =>
{
    config.ReadFrom.Configuration(context.Configuration)
          .WriteTo.Console()
          .Enrich.FromLogContext();
});

// Add services to the container
builder.Services.AddInfrastructure();
builder.Services.AddSignalR();

// Add controllers
builder.Services.AddControllers();

// Add API documentation
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Add CORS for Blazor WebAssembly
builder.Services.AddCors(options =>
{
    options.AddPolicy("BlazorWasm", policy =>
    {
        policy.WithOrigins("http://localhost:5177", "https://localhost:5177")
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials(); // Important for SignalR
    });
});

// Add health checks
builder.Services.AddHealthChecks();

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Only use HTTPS redirection in non-Docker environments
if (!Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER")?.Equals("true", StringComparison.OrdinalIgnoreCase) == true)
{
    app.UseHttpsRedirection();
}

app.UseCors("BlazorWasm");

// Serve static files for Blazor WebAssembly with proper MIME types
var provider = new FileExtensionContentTypeProvider();
provider.Mappings[".dat"] = "application/octet-stream";
provider.Mappings[".json"] = "application/json";
provider.Mappings[".pdb"] = "application/octet-stream";
provider.Mappings[".wasm"] = "application/wasm";
provider.Mappings[".blat"] = "application/octet-stream";
provider.Mappings[".dll"] = "application/octet-stream";
provider.Mappings[".br"] = "application/x-brotli";

app.UseStaticFiles(new StaticFileOptions
{
    ContentTypeProvider = provider,
    OnPrepareResponse = ctx =>
    {
        // Enable caching for static assets
        var path = ctx.File.Name.ToLowerInvariant();
        if (path.Contains("_framework/") || path.EndsWith(".css") || path.EndsWith(".js"))
        {
            ctx.Context.Response.Headers.CacheControl = "public, max-age=31536000, immutable";
        }
    }
});

app.UseRouting();

// Map controllers
app.MapControllers();

// Map SignalR hub
app.MapHub<TspHub>("/tspHub");

// Map health checks (keeping the simple health check endpoint as well)
app.MapHealthChecks("/health");

// Fallback to index.html for client-side routing (only for non-API routes)
app.MapFallbackToFile("index.html");

app.Run();
