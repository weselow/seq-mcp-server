# Integration Tests Guide

> [Русская версия](INTEGRATION_TESTS.md)

## Overview

The project contains integration tests that verify interaction with a real Seq server. These tests are skipped by default and require a running Seq instance.

## Quick Start

### 1. Starting Seq Server

**Via Docker (recommended):**

```bash
# Start Seq in container
docker run -d \
  --name seq-test \
  -e ACCEPT_EULA=Y \
  -p 5341:80 \
  -p 5342:5341 \
  datalust/seq:latest

# Verify Seq is running
curl http://localhost:5341/api

# Open Seq UI
open http://localhost:5341
```

**Via Docker Compose:**

```bash
# Use docker-compose.yml from project root
docker-compose up -d seq

# Check status
docker-compose ps
```

### 2. Running Integration Tests

**All integration tests (with Skip):**

```bash
dotnet test
```

**Only integration tests (remove Skip):**

Manually remove `Skip` attribute from tests or use:

```bash
# Run specific test file
dotnet test --filter "FullyQualifiedName~SeqApiClientSignalManagementIntegrationTests"
```

**With explicit Seq URL:**

```bash
export SEQ_URL="http://localhost:5341"
dotnet test
```

### 3. Stopping Seq Server

```bash
# Docker
docker stop seq-test
docker rm seq-test

# Docker Compose
docker-compose down
```

## Integration Test Structure

### Existing Integration Tests

```
tests/SeqMcp.Tests/Services/
├── SeqApiClientSqlTests.cs                           # SQL query tests (1 integration test)
├── SeqApiClientSignalsTests.cs                       # Signal listing tests (1 integration test)
├── SeqApiClientErrorHandlingTests.cs                 # Error handling tests (3 integration tests)
└── SeqApiClientSignalManagementIntegrationTests.cs   # Signal CRUD tests (9 integration tests)
```

### New Integration Tests for Signal Management

**SeqApiClientSignalManagementIntegrationTests.cs:**

| Test | Description |
|------|-------------|
| `Should_CreateSignal_Successfully` | Create signal with filter |
| `Should_CreateSignal_WithoutFilter` | Create signal without filter |
| `Should_UpdateSignal_Successfully` | Full signal update |
| `Should_UpdateSignal_PartialUpdate` | Partial signal update |
| `Should_DeleteSignal_Successfully` | Delete signal |
| `Should_GetApplications_Successfully` | Get applications list |
| `Should_GetApplications_RespectLimit` | Verify applications limit |
| `Should_CreateUpdateDelete_FullLifecycle` | Full lifecycle: create → update → delete |

## Configuration

### Environment Variables

| Variable | Description | Default |
|----------|-------------|---------|
| `SEQ_URL` | Seq server URL | `http://localhost:5341` |
| `SEQ_SERVER_URL` | Alternative name for SEQ_URL | - |
| `SEQ_API_KEY` | Seq API key (if required) | - |

### Configuration Example

```bash
# .env for integration tests
export SEQ_URL="http://localhost:5341"
export SEQ_API_KEY="your-api-key-if-needed"
```

## Automatic Cleanup

Integration tests use `IAsyncLifetime` for automatic cleanup of created resources:

```csharp
public class SeqApiClientSignalManagementIntegrationTests : IAsyncLifetime
{
    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        // Automatic cleanup of created signals
        if (!string.IsNullOrEmpty(_createdSignalId))
        {
            await client.DeleteSignalAsync(_createdSignalId);
        }
    }
}
```

## Troubleshooting

### Seq Server Not Starting

```bash
# Check Docker logs
docker logs seq-test

# Check ports
netstat -an | grep 5341

# Ensure port is not occupied
lsof -i :5341
```

### Tests Fail with Connection Refused

**Problem:** Seq server is not accessible

**Solution:**
1. Verify Seq is running: `curl http://localhost:5341/api`
2. Check Docker: `docker ps | grep seq`
3. Verify SEQ_URL variable

### Tests Fail with Unauthorized

**Problem:** Seq requires API key

**Solution:**
1. Create API key in Seq UI: Settings → API Keys
2. Set variable: `export SEQ_API_KEY="your-key"`

### Integration Tests Not Running

**Problem:** Tests marked as `Skip`

**Solution:**

Manually remove `Skip` attribute from desired tests:

```csharp
// Before
[Fact(Skip = "Requires running Seq server at http://localhost:5341")]

// After
[Fact]
```

Or use filter for specific class:

```bash
dotnet test --filter "FullyQualifiedName~IntegrationTests"
```

## CI/CD Integration

### GitHub Actions Example

```yaml
name: Integration Tests

on: [push, pull_request]

jobs:
  integration-tests:
    runs-on: ubuntu-latest

    services:
      seq:
        image: datalust/seq:latest
        ports:
          - 5341:80
        env:
          ACCEPT_EULA: Y

    steps:
      - uses: actions/checkout@v3

      - name: Setup .NET
        uses: actions/setup-dotnet@v3
        with:
          dotnet-version: '9.0.x'

      - name: Wait for Seq
        run: |
          timeout 30 bash -c 'until curl -f http://localhost:5341/api; do sleep 1; done'

      - name: Run Integration Tests
        run: dotnet test --filter "IntegrationTests"
        env:
          SEQ_URL: http://localhost:5341
```

## Best Practices

### 1. Test Isolation

Each test should:
- Create unique resources (use GUID in names)
- Clean up created resources after execution
- Not depend on other tests

### 2. Timeout and Retry

```csharp
// Add timeout for long operations
using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
var result = await client.SomeMethodAsync(cancellationToken: cts.Token);

// Retry for unstable operations
await Policy
    .Handle<HttpRequestException>()
    .WaitAndRetryAsync(3, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)))
    .ExecuteAsync(() => client.SomeMethodAsync());
```

### 3. Checking Seq Availability

```csharp
using SeqMcp.Tests.Helpers;

[Fact]
public async Task MyIntegrationTest()
{
    // Skip test if Seq is not available
    if (await SeqTestHelper.ShouldSkipIntegrationTest())
    {
        return;
    }

    // Test logic...
}
```

## Test Statistics

| Category | Unit Tests | Integration Tests | Total |
|----------|-----------|-------------------|-------|
| **SeqApiClient** | 28 | 5 | 33 |
| **Signal Management** | 0 | 9 | 9 |
| **Health Check** | 8 | 0 | 8 |
| **Scope Filtering** | 7 | 0 | 7 |
| **TOTAL** | **43** | **14** | **57** |

**Coverage:**
- Unit tests: 43 tests (always run)
- Integration tests: 14 tests (require Seq server)
- Success rate: 100% (when Seq is available)

---

**Last Updated:** 2025-01-13
**Version:** 1.0
