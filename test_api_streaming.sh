#!/bin/bash

echo "Testing the Tour.Clone() fix with TSP API..."
echo "==============================="

# Generate test cities
echo "1. Generating test cities..."
cities=$(curl -s -X POST "http://localhost:5001/api/tsp/cities/generate" \
  -H "Content-Type: application/json" \
  -d '{"count": 10}')

echo "Generated cities:"
echo "$cities" | jq '.'

# Create solve request
echo ""
echo "2. Testing TSP solve with streaming results..."

# Start the solve request (this will stream results via SignalR)
response=$(curl -s -X POST "http://localhost:5001/api/tsp/solve" \
  -H "Content-Type: application/json" \
  -d "{
    \"cities\": $cities,
    \"config\": {
      \"populationSize\": 20,
      \"generations\": 50,
      \"mutationRate\": 0.01,
      \"crossoverType\": \"OrderCrossover\",
      \"mutationType\": \"SwapMutation\"
    }
  }")

echo "API Response:"
echo "$response" | jq '.'

echo ""
echo "==============================="
echo "Note: This API uses SignalR for streaming results."
echo "The actual fitness values are sent via WebSocket."
echo "To fully test the fix, we need to check the web interface."
echo "==============================="
