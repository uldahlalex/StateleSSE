#!/bin/bash
# Runs 2 server instances behind a load balancer to demonstrate horizontal scaling

echo "Starting Redis (required for cross-server communication)..."
echo "Make sure Redis is running on localhost:6379"
echo ""

# Start server instances in background
echo "Starting Server 1 on :5001..."
dotnet run --project server/server.csproj --urls=http://localhost:5001 &
PID1=$!

echo "Starting Server 2 on :5002..."
dotnet run --project server/server.csproj --urls=http://localhost:5002 &
PID2=$!

sleep 2

echo "Starting Load Balancer on :5000..."
dotnet run --project loadbalancer/loadbalancer.csproj --urls=http://localhost:5000 &
PID3=$!

echo ""
echo "==================================="
echo "Load Balancer: http://localhost:5000"
echo "Server 1:      http://localhost:5001"
echo "Server 2:      http://localhost:5002"
echo "==================================="
echo ""
echo "Press Ctrl+C to stop all servers"

trap "kill $PID1 $PID2 $PID3 2>/dev/null" EXIT
wait
