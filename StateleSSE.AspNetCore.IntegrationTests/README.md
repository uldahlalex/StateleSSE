# StateleSSE.AspNetCore.IntegrationTests

Integration tests for StateleSSE.AspNetCore using real ASP.NET Core infrastructure.

## What's Tested

### SSE HTTP Protocol
- Correct headers (Content-Type, Cache-Control, Connection)
- Event streaming format
- Keepalive behavior
- Client reconnection handling

### WebApplicationFactory Tests
- Full request/response cycle
- Real HTTP client connections
- Service registration and DI
- Middleware integration

### Redis Integration (TODO)
- Multi-server scenarios
- Message distribution across instances
- Redis connection failures and recovery

## Running Tests

```bash
dotnet test StateleSSE.AspNetCore.IntegrationTests
```

### With Redis

For Redis integration tests, ensure Redis is running:

```bash
docker run -d -p 6379:6379 redis:latest
dotnet test StateleSSE.AspNetCore.IntegrationTests --filter Category=Redis
```

## Test Principles

- **Real infrastructure**: Uses TestServer and real HTTP
- **Integration focused**: Tests component interactions
- **Realistic scenarios**: Simulates actual usage patterns

## Dependencies

- **Microsoft.AspNetCore.Mvc.Testing**: WebApplicationFactory
- **FluentAssertions**: Readable assertions
- **StackExchange.Redis**: Redis integration tests
