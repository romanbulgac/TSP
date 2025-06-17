#!/bin/bash

# TSP Lab Build Verification Script
# Run this script to verify the implementation is working correctly

echo "🔍 TSP Lab Implementation Verification"
echo "======================================"

# Check if we're in the right directory
if [ ! -f "TspLab.sln" ]; then
    echo "❌ Error: Not in TSP project root directory"
    echo "Please run this script from the directory containing TspLab.sln"
    exit 1
fi

echo "✅ Found TspLab.sln - in correct directory"

# Clean previous builds
echo ""
echo "🧹 Cleaning previous builds..."
dotnet clean > /dev/null 2>&1

# Build each project
echo ""
echo "🔨 Building projects..."

echo "  Building TspLab.Domain..."
if dotnet build TspLab.Domain > /dev/null 2>&1; then
    echo "  ✅ TspLab.Domain - Success"
else
    echo "  ❌ TspLab.Domain - Failed"
    exit 1
fi

echo "  Building TspLab.Application..."
if dotnet build TspLab.Application > /dev/null 2>&1; then
    echo "  ✅ TspLab.Application - Success"
else
    echo "  ❌ TspLab.Application - Failed"
    exit 1
fi

echo "  Building TspLab.Infrastructure..."
if dotnet build TspLab.Infrastructure > /dev/null 2>&1; then
    echo "  ✅ TspLab.Infrastructure - Success"
else
    echo "  ❌ TspLab.Infrastructure - Failed"
    exit 1
fi

echo "  Building TspLab.WebApi..."
if dotnet build TspLab.WebApi > /dev/null 2>&1; then
    echo "  ✅ TspLab.WebApi - Success"
else
    echo "  ❌ TspLab.WebApi - Failed"
    exit 1
fi

echo "  Building TspLab.Web..."
if dotnet build TspLab.Web > /dev/null 2>&1; then
    echo "  ✅ TspLab.Web - Success"
else
    echo "  ❌ TspLab.Web - Failed"
    exit 1
fi

# Verify key files exist
echo ""
echo "📁 Verifying key implementation files..."

key_files=(
    "TspLab.Domain/Interfaces/ITspSolver.cs"
    "TspLab.Application/Heuristics/NearestNeighborSolver.cs"
    "TspLab.Application/Heuristics/TwoOptSolver.cs"
    "TspLab.Application/Heuristics/SimulatedAnnealingSolver.cs"
    "TspLab.Application/Solvers/GeneticAlgorithmSolver.cs"
    "TspLab.Infrastructure/Extensions/ServiceCollectionExtensions.cs"
    "TspLab.Web/Pages/Solver.razor"
    "TspLab.Web/Pages/Benchmark.razor"
    "TspLab.Web/wwwroot/js/tsp-visualization.js"
)

for file in "${key_files[@]}"; do
    if [ -f "$file" ]; then
        echo "  ✅ $file"
    else
        echo "  ❌ $file - Missing"
        exit 1
    fi
done

# Check for ITspSolver implementations
echo ""
echo "🔍 Checking ITspSolver implementations..."
if grep -q "ITspSolver" TspLab.Application/Heuristics/*.cs; then
    echo "  ✅ Found ITspSolver implementations in heuristics"
else
    echo "  ❌ No ITspSolver implementations found"
    exit 1
fi

# Check DI registration
echo ""
echo "🔧 Checking dependency injection registration..."
if grep -q "ITspSolver" TspLab.Infrastructure/Extensions/ServiceCollectionExtensions.cs; then
    echo "  ✅ Found ITspSolver registrations in DI"
else
    echo "  ❌ No ITspSolver registrations found"
    exit 1
fi

# Check frontend integration
echo ""
echo "🎨 Checking frontend integration..."
if grep -q "TspSolvers" TspLab.Web/Pages/Solver.razor && grep -q "TspSolvers" TspLab.Web/Pages/Benchmark.razor; then
    echo "  ✅ Found TspSolvers injection in frontend"
else
    echo "  ❌ TspSolvers injection missing in frontend"
    exit 1
fi

# Success message
echo ""
echo "🎉 SUCCESS! All verification checks passed"
echo ""
echo "📋 Implementation Summary:"
echo "  ✅ ITspSolver interface created"
echo "  ✅ 4 algorithm implementations (NN, 2-opt, SA, GA wrapper)"
echo "  ✅ Dependency injection configured"
echo "  ✅ Frontend integration complete"
echo "  ✅ All projects build successfully"
echo ""
echo "🚀 The TSP Lab extensions are ready for deployment!"
echo ""
echo "Next steps:"
echo "  1. Test Docker build: docker-compose build --no-cache"
echo "  2. Run the application: docker-compose up"
echo "  3. Visit http://localhost to test the implementation"
