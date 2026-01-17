# E2E Testing Guide for StateleSSE

End-to-end tests validate real-world usage of StateleSSE.AspNetCore through the example applications.

## Strategy

### Use Example Apps as Test Harnesses

The example apps (ExampleApp.Chat, ExampleApp.Kahoot) serve as real-world test cases:

1. **ExampleApp.Chat**: Tests basic SSE pub/sub, InMemory and Redis backplanes
2. **ExampleApp.Kahoot**: Tests complex multi-group scenarios, MQTT integration

## E2E Test Structure

### Option 1: Playwright/Selenium Tests

Create browser-based tests that interact with the example apps:

```csharp
// ExampleApp.Chat.E2ETests/ChatE2ETests.cs
[Fact]
public async Task MultipleClients_ReceiveMessagesInRealTime()
{
    // Start server
    // Open two browser instances
    // Client 1 sends message
    // Client 2 receives message
    // Verify via DOM inspection
}
```

### Option 2: Manual Test Scripts

Create test scenarios documented in markdown:

```markdown
## Test: Multi-Server Chat Synchronization

1. Start Redis: `docker run -d -p 6379:6379 redis`
2. Start Server 1: `cd ExampleApp.Chat/server && dotnet run --urls http://localhost:5001`
3. Start Server 2: `cd ExampleApp.Chat/server && dotnet run --urls http://localhost:5002`
4. Open Browser 1 to http://localhost:5001
5. Open Browser 2 to http://localhost:5002
6. Send message from Browser 1
7. Verify message appears in Browser 2
```

### Option 3: Integration Tests in Example Apps

Add test projects alongside example apps:

```
ExampleApp.Chat/
├── server/
├── client/
└── tests/           # New test project
    └── ChatIntegrationTests.cs
```

## Recommended E2E Test Cases

### Chat App Tests
- [ ] Single client connects and receives messages
- [ ] Multiple clients receive same broadcast
- [ ] Client disconnects and reconnects without errors
- [ ] Messages sent during disconnect are not lost (with Redis)
- [ ] InMemory backplane works for single server
- [ ] Redis backplane synchronizes across multiple servers

### Kahoot App Tests
- [ ] Players join game and receive PlayerJoined events
- [ ] Questions broadcast to all players
- [ ] Answer submissions update leaderboard in real-time
- [ ] Game state synchronizes across multiple game instances
- [ ] MQTT integration delivers events to SSE clients
- [ ] Round transitions work correctly

## Running E2E Tests

### Manual Testing

1. Start dependencies:
```bash
docker-compose up -d  # Redis, MQTT broker if needed
```

2. Run example app:
```bash
cd ExampleApp.Chat/server
dotnet run
```

3. Open browser and follow test scenarios

### Automated Testing

1. Install Playwright:
```bash
dotnet add package Microsoft.Playwright
pwsh bin/Debug/net10.0/playwright.ps1 install
```

2. Run E2E tests:
```bash
dotnet test ExampleApp.Chat.E2ETests
```

## Test Data & Fixtures

Create helper classes for common scenarios:

```csharp
public class ChatTestFixture : IAsyncLifetime
{
    public WebApplication Server { get; private set; }

    public async Task InitializeAsync()
    {
        // Start test server
        // Initialize test data
    }

    public async Task DisposeAsync()
    {
        // Cleanup
    }
}
```

## CI/CD Integration

### GitHub Actions Example

```yaml
name: E2E Tests

on: [push, pull_request]

jobs:
  e2e-tests:
    runs-on: ubuntu-latest

    services:
      redis:
        image: redis:latest
        ports:
          - 6379:6379

    steps:
      - uses: actions/checkout@v3
      - uses: actions/setup-dotnet@v3

      - name: Run Chat E2E Tests
        run: |
          cd ExampleApp.Chat/server
          dotnet run &
          sleep 5
          dotnet test ../tests
```

## Performance Testing

Add load tests to validate SSE performance:

```csharp
[Fact]
public async Task ThousandClients_AllReceiveMessages()
{
    var clients = Enumerable.Range(0, 1000)
        .Select(_ => ConnectSseClient())
        .ToList();

    await PublishMessage("test");

    var receivedCount = await Task.WhenAll(
        clients.Select(c => c.WaitForMessage())
    );

    receivedCount.Should().AllBe(true);
}
```

## Next Steps

1. Decide on E2E testing approach (Playwright vs Manual vs Integration)
2. Create test fixtures for example apps
3. Document test scenarios
4. Set up CI/CD pipeline
5. Add performance benchmarks
