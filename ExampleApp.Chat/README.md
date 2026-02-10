# ExampleApp.Chat

Full chat application demonstrating StateleSSE with horizontal scaling.

## Structure

```
ExampleApp.Chat/
├── server/          # Chat API server
├── client/          # React frontend
└── loadbalancer/    # YARP load balancer
```

## Running (Single Instance)

```bash
cd server
dotnet run
```

## Running (Scaled with Load Balancer)

Uses PostgreSQL for both domain data and the SSE backplane (LISTEN/NOTIFY) — no Redis needed.

```bash
# Linux/Mac
./run-scaled.sh

# Or manually:
dotnet run --project server/server.csproj --urls=http://localhost:5001 &
dotnet run --project server/server.csproj --urls=http://localhost:5002 &
dotnet run --project loadbalancer/loadbalancer.csproj --urls=http://localhost:5000
```

This starts:
- **Load Balancer** on `:5000` (round-robin between servers)
- **Server 1** on `:5001`
- **Server 2** on `:5002`

Open multiple browser tabs to `http://localhost:5000` - requests will be distributed across both servers, but messages flow through PostgreSQL LISTEN/NOTIFY so all clients see all messages.
