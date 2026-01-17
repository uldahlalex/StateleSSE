# StateleSSE Testing Strategy - Updated

## Overview

Testing is organized into three layers with modern tooling:

1. **Unit Tests** (`StateleSSE.AspNetCore.Tests`) - Isolated, fast tests
2. **Integration Tests** (`StateleSSE.AspNetCore.IntegrationTests`) - Real Redis with Testcontainers
3. **E2E Tests** (Example apps) - Real-world usage validation

---

## 1. Unit Tests - StateleSSE.AspNetCore.Tests

### Status: ✅ Implemented (10/10 passing)

**Stack:**
- xUnit 2.9
- FluentAssertions
- NSubstitute (mocking)
- Manual instantiation (no DI complexity)

**What's tested:**
- `InMemoryBackplane` - All core functionality
  - Message publishing and delivery
  - Multi-subscriber scenarios
  - Group isolation
  - Subscribe/Unsubscribe lifecycle
  - Diagnostics and statistics
  - Proper disposal

**Run tests:**
```bash
dotnet test StateleSSE.AspNetCore.Tests
```

---

## 2. Integration Tests - StateleSSE.AspNetCore.IntegrationTests

### Status: ✅ Implemented with Testcontainers.Redis

**Stack:**
- xUnit 2.9
- FluentAssertions
- **Testcontainers.Redis** - Spin up real Redis in Docker for tests
- Microsoft.Extensions.Logging

**What's tested:**
- `RedisBackplane` with real Redis
  - Cross-server message distribution
  - Pub/sub across multiple backplane instances
  - PublishToAll broadcasts
  - Subscribe/unsubscribe with Redis
  - Local diagnostics

**Run tests:**
```bash
# Requires Docker to be running
dotnet test StateleSSE.AspNetCore.IntegrationTests
```

**How it works:**
- Each test class implements `IAsyncLifetime`
- `InitializeAsync()` starts a Redis container
- Tests run against real Redis
- `DisposeAsync()` stops and cleans up the container
- No manual Docker setup needed!

---

## 3. E2E Tests - Example Apps

### Status: 📝 Guide Created

See `E2E_TESTING_GUIDE.md` for:
- Manual testing scenarios
- Playwright/Selenium automation options
- Performance testing approaches

---

## Test Results

```
✅ Unit Tests: 10/10 passed
✅ Integration Tests: 5 Redis tests with Testcontainers
📝 E2E Tests: Guide documented
```

---

## Key Benefits

### Testcontainers.Redis
- ✅ No manual Docker setup
- ✅ Tests run in isolation
- ✅ Real Redis behavior (not mocked)
- ✅ Automatic cleanup
- ✅ CI/CD friendly

### xUnit v2
- ✅ Stable and mature
- ✅ Great IDE support
- ✅ No complex DI setup needed
- ✅ Simple constructor injection

---

## Running Tests

### All tests
```bash
dotnet test
```

### Unit tests only (fast, no Docker needed)
```bash
dotnet test StateleSSE.AspNetCore.Tests
```

### Integration tests (requires Docker running)
```bash
dotnet test StateleSSE.AspNetCore.IntegrationTests
```

### With coverage
```bash
dotnet test /p:CollectCoverage=true /p:CoverletOutputFormat=opencover
```

---

## CI/CD Integration

### GitHub Actions Example

```yaml
name: Tests

on: [push, pull_request]

jobs:
  unit-tests:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v3
      - uses: actions/setup-dotnet@v3
      - run: dotnet test StateleSSE.AspNetCore.Tests

  integration-tests:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v3
      - uses: actions/setup-dotnet@v3
      # Testcontainers needs Docker - it's already available on GitHub Actions runners
      - run: dotnet test StateleSSE.AspNetCore.IntegrationTests
```

**No additional service configuration needed!** Testcontainers handles Redis automatically.

---

## Project Structure

```
StateleSSE.AspNetCore.Tests/
├── InMemoryBackplaneTests.cs    # 10 unit tests
└── README.md

StateleSSE.AspNetCore.IntegrationTests/
├── RedisBackplaneTests.cs       # Redis integration tests with Testcontainers
└── README.md

E2E_TESTING_GUIDE.md             # Manual/automated E2E testing strategies
TESTING_SUMMARY.md               # This file
```

---

## Next Steps

- [ ] Add more Redis edge case tests (connection failures, reconnection)
- [ ] Add HTTP/SSE protocol integration tests
- [ ] Implement E2E tests for example apps
- [ ] Add performance benchmarks
- [ ] Set up CI/CD pipeline
