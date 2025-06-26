# Docker Port Configuration

## Port Mapping

### Local Development
- **Web (Blazor WebAssembly)**: http://localhost:5277 (dotnet run)
- **API**: http://localhost:5201 (dotnet run)
- **SignalR Hub**: http://localhost:5201/tspHub

### Docker Environment
- **Web (via nginx)**: http://localhost:8080
- **API (internal)**: tsp-webapi:5001 (proxied through nginx)
- **SignalR Hub**: http://localhost:8080/tspHub (proxied through nginx)

## Service Configuration

### TspLab.WebApi (API Server)
- **Docker Port**: 5001
- **Local Port**: 5201
- **SignalR Hub Path**: `/tspHub`
- **CORS**: Allows both localhost:5277 and localhost:8080

### TspLab.Web (Blazor WebAssembly)
- **Docker Port**: 80 (nginx) → exposed as 8080
- **Local Port**: 5277
- **API Detection**: Automatically detects environment based on current port
  - Port 8080 → Docker mode (uses same host for API via nginx proxy)
  - Other ports → Local mode (uses http://localhost:5201 for API)

### Nginx Proxy (Docker only)
- **External Port**: 8080
- **Internal Port**: 80
- **API Proxy**: `/api/*` → `http://tsp-webapi:5001/api/*`
- **SignalR Proxy**: `/tspHub` → `http://tsp-webapi:5001/tspHub`
- **Static Files**: Serves Blazor WebAssembly files

## Usage

### Local Development
```bash
# Terminal 1 - API
cd TspLab.WebApi
dotnet run

# Terminal 2 - Web
cd TspLab.Web  
dotnet run
```

### Docker
```bash
# Build and run all services
docker-compose up --build

# Access web app at http://localhost:8080
# API and SignalR are automatically proxied through nginx
```

## Troubleshooting

### SignalR Connection Issues
1. Check console for connection URL in browser developer tools
2. Verify CORS settings in TspLab.WebApi/Program.cs
3. Ensure nginx proxy configuration matches hub path
4. Check that the SignalR hub is registered on the correct path in API

### Port Conflicts
- Local API: Change port in Properties/launchSettings.json
- Docker ports: Modify docker-compose.yml port mappings
- Nginx: Update nginx.conf upstream configuration
