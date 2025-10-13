# Integration Tests Guide

## Обзор

Проект содержит интеграционные тесты, которые проверяют взаимодействие с реальным Seq сервером. Эти тесты по умолчанию пропускаются (Skip) и требуют запущенного экземпляра Seq.

## Быстрый старт

### 1. Запуск Seq сервера

**Через Docker (рекомендуется):**

```bash
# Запустить Seq в контейнере
docker run -d \
  --name seq-test \
  -e ACCEPT_EULA=Y \
  -p 5341:80 \
  -p 5342:5341 \
  datalust/seq:latest

# Проверить что Seq запущен
curl http://localhost:5341/api

# Открыть UI Seq
open http://localhost:5341
```

**Через Docker Compose:**

```bash
# Использовать docker-compose.yml из корня проекта
docker-compose up -d seq

# Проверить статус
docker-compose ps
```

### 2. Запуск интеграционных тестов

**Все интеграционные тесты (с пропуском Skip):**

```bash
dotnet test
```

**Только интеграционные тесты (убрать Skip):**

Вручную удалите атрибут `Skip` из тестов или используйте:

```bash
# Запустить конкретный тестовый файл
dotnet test --filter "FullyQualifiedName~SeqApiClientSignalManagementIntegrationTests"
```

**С явным указанием Seq URL:**

```bash
export SEQ_URL="http://localhost:5341"
dotnet test
```

### 3. Остановка Seq сервера

```bash
# Docker
docker stop seq-test
docker rm seq-test

# Docker Compose
docker-compose down
```

## Структура интеграционных тестов

### Существующие интеграционные тесты

```
tests/SeqMcp.Tests/Services/
├── SeqApiClientSqlTests.cs                           # SQL query tests (1 integration test)
├── SeqApiClientSignalsTests.cs                       # Signal listing tests (1 integration test)
├── SeqApiClientErrorHandlingTests.cs                 # Error handling tests (3 integration tests)
└── SeqApiClientSignalManagementIntegrationTests.cs   # Signal CRUD tests (9 integration tests)
```

### Новые интеграционные тесты для управления сигналами

**SeqApiClientSignalManagementIntegrationTests.cs:**

| Тест | Описание |
|------|----------|
| `Should_CreateSignal_Successfully` | Создание сигнала с фильтром |
| `Should_CreateSignal_WithoutFilter` | Создание сигнала без фильтра |
| `Should_UpdateSignal_Successfully` | Полное обновление сигнала |
| `Should_UpdateSignal_PartialUpdate` | Частичное обновление сигнала |
| `Should_DeleteSignal_Successfully` | Удаление сигнала |
| `Should_GetApplications_Successfully` | Получение списка приложений |
| `Should_GetApplications_RespectLimit` | Проверка лимита приложений |
| `Should_CreateUpdateDelete_FullLifecycle` | Полный lifecycle: создание → обновление → удаление |

## Конфигурация

### Переменные окружения

| Переменная | Описание | По умолчанию |
|-----------|----------|--------------|
| `SEQ_URL` | URL Seq сервера | `http://localhost:5341` |
| `SEQ_SERVER_URL` | Альтернативное имя для SEQ_URL | - |
| `SEQ_API_KEY` | API ключ для Seq (если требуется) | - |

### Пример конфигурации

```bash
# .env для интеграционных тестов
export SEQ_URL="http://localhost:5341"
export SEQ_API_KEY="your-api-key-if-needed"
```

## Автоматическая очистка

Интеграционные тесты используют `IAsyncLifetime` для автоматической очистки созданных ресурсов:

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

### Seq сервер не запускается

```bash
# Проверить логи Docker
docker logs seq-test

# Проверить порты
netstat -an | grep 5341

# Убедиться что порт не занят
lsof -i :5341
```

### Тесты падают с Connection Refused

**Проблема:** Seq сервер не доступен

**Решение:**
1. Проверить что Seq запущен: `curl http://localhost:5341/api`
2. Проверить Docker: `docker ps | grep seq`
3. Проверить переменную SEQ_URL

### Тесты падают с Unauthorized

**Проблема:** Seq требует API ключ

**Решение:**
1. Создать API ключ в Seq UI: Settings → API Keys
2. Установить переменную: `export SEQ_API_KEY="your-key"`

### Integration тесты не запускаются

**Проблема:** Тесты помечены как `Skip`

**Решение:**

Вручную удалите `Skip` атрибут из нужных тестов:

```csharp
// Before
[Fact(Skip = "Requires running Seq server at http://localhost:5341")]

// After
[Fact]
```

Или используйте фильтр для конкретного класса:

```bash
dotnet test --filter "FullyQualifiedName~IntegrationTests"
```

## CI/CD Integration

### GitHub Actions пример

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

### 1. Изоляция тестов

Каждый тест должен:
- Создавать уникальные ресурсы (используйте GUID в именах)
- Очищать созданные ресурсы после выполнения
- Не зависеть от других тестов

### 2. Timeout и Retry

```csharp
// Добавить timeout для долгих операций
using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
var result = await client.SomeMethodAsync(cancellationToken: cts.Token);

// Retry для нестабильных операций
await Policy
    .Handle<HttpRequestException>()
    .WaitAndRetryAsync(3, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)))
    .ExecuteAsync(() => client.SomeMethodAsync());
```

### 3. Проверка доступности Seq

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

## Статистика тестов

| Категория | Unit Tests | Integration Tests | Total |
|-----------|-----------|-------------------|-------|
| **SeqApiClient** | 28 | 5 | 33 |
| **Signal Management** | 0 | 9 | 9 |
| **Health Check** | 8 | 0 | 8 |
| **Scope Filtering** | 7 | 0 | 7 |
| **ИТОГО** | **43** | **14** | **57** |

**Покрытие:**
- Unit tests: 43 тестов (всегда запускаются)
- Integration tests: 14 тестов (требуют Seq сервер)
- Успешность: 100% (при доступности Seq)

---

**Дата обновления:** 2025-01-13
**Версия:** 1.0
