# StateleSSE.AspNetCore.Tests

Isolated unit tests for the StateleSSE.AspNetCore library.

## What's Tested

### InMemoryBackplane
- Message publishing to single and multiple subscribers
- Group isolation (messages only go to correct groups)
- Subscription/unsubscription lifecycle
- PublishToAll broadcasts
- Diagnostics and statistics
- Proper cleanup and disposal

### RedisBackplane (TODO)
- Cross-server message distribution
- Redis pub/sub integration
- Error handling and reconnection
- Channel routing

### SseControllerBase (TODO)
- Keepalive behavior
- Event streaming
- Proper cleanup on client disconnect

## Running Tests

```bash
dotnet test StateleSSE.AspNetCore.Tests
```

## Test Principles

- **Fast**: No external dependencies (no Redis, no HTTP)
- **Isolated**: Each test is independent
- **Focused**: One concept per test
- **Deterministic**: No flaky timeouts or race conditions

## Dependencies

- **xUnit**: Test framework
- **FluentAssertions**: Readable assertions
- **NSubstitute**: Mocking framework
