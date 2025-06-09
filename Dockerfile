# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy project files
COPY ["TspLab.Web/TspLab.Web.csproj", "TspLab.Web/"]
COPY ["TspLab.WebApi/TspLab.WebApi.csproj", "TspLab.WebApi/"]
COPY ["TspLab.Application/TspLab.Application.csproj", "TspLab.Application/"]
COPY ["TspLab.Infrastructure/TspLab.Infrastructure.csproj", "TspLab.Infrastructure/"]
COPY ["TspLab.Domain/TspLab.Domain.csproj", "TspLab.Domain/"]

# Restore dependencies
RUN dotnet restore "TspLab.Web/TspLab.Web.csproj"
RUN dotnet restore "TspLab.WebApi/TspLab.WebApi.csproj"

# Copy all source code
COPY . .

# Build WebAssembly app
WORKDIR "/src/TspLab.Web"
RUN dotnet build "TspLab.Web.csproj" -c Release -o /app/build/wasm

# Publish WebAssembly app
RUN dotnet publish "TspLab.Web.csproj" -c Release -o /app/publish/wasm

# Build WebAPI
WORKDIR "/src/TspLab.WebApi"
RUN dotnet build "TspLab.WebApi.csproj" -c Release -o /app/build/api

# Publish WebAPI
RUN dotnet publish "TspLab.WebApi.csproj" -c Release -o /app/publish/api

# Runtime stage for API
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS api
WORKDIR /app
EXPOSE 5001

# Install curl for health checks
RUN apt-get update && apt-get install -y curl && rm -rf /var/lib/apt/lists/*

COPY --from=build /app/publish/api .
COPY --from=build /app/publish/wasm/wwwroot ./wwwroot

ENTRYPOINT ["dotnet", "TspLab.WebApi.dll"]

# Nginx stage for WebAssembly (alternative deployment)
FROM nginx:alpine AS wasm
COPY --from=build /app/publish/wasm/wwwroot /usr/share/nginx/html
COPY nginx.conf /etc/nginx/nginx.conf
EXPOSE 80
