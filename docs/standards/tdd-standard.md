# СТАНДАРТ TEST-DRIVEN DEVELOPMENT (TDD)

> **Обязательный стандарт разработки через тестирование для .NET проектов**

**Версия**: 1.0
**Дата**: 2024-09-28
**Статус**: ОБЯЗАТЕЛЬНЫЙ для всех модулей
**Применение**: Все агенты implementation и разработка кода

## Связь с другими стандартами

Данный стандарт дополняет:
- @docs/standards/GLOBAL-implementation-standard.md

## 🎯 Цели TDD стандарта

1. **Качество кода**: Обеспечение высокого качества через тестирование
2. **Архитектурная чистота**: TDD способствует лучшему дизайну кода
3. **Регрессионная защита**: Предотвращение поломок при изменениях
4. **Документация**: Тесты как живая документация поведения системы
5. **Уверенность в рефакторинге**: Безопасные изменения кода

## 🚨 КРИТИЧЕСКИЕ ПРАВИЛА

### ⚠️ ЗАПРЕЩЕНО

**НИКОГДА не изменяйте тесты для устранения ошибок компиляции - ВСЕГДА изменяйте код**

```csharp
// ❌ НЕПРАВИЛЬНО: Изменение теста под сломанный код
Assert.Equal(new { X = 0, Y = 0, Width = 800, Height = 600 }, result.Crop);

// ✅ ПРАВИЛЬНО: Сохранить тест, исправить имплементацию
Assert.Equal("fill", result.Crop);
```

### ✅ ОБЯЗАТЕЛЬНО

1. **Тесты сначала** - писать тесты ДО реализации функциональности
2. **Следовать циклу** - RED → GREEN → REFACTOR
3. **Проверять результат** - всегда запускать тесты после написания кода
4. **Покрытие >60%** - обязательное минимальное покрытие тестами

## 🔄 TDD Цикл (ОБЯЗАТЕЛЬНО для каждой имплементации)

### **1. RED - Написать падающий тест ПЕРВЫМ**

```bash
# ВСЕГДА создавать тест ДО имплементации
# Создать файл теста в tests/[Project].Tests/[Feature]/[FeatureTest].cs

# Запустить тест - он ДОЛЖЕН упасть
dotnet test --filter "FullyQualifiedName~[TestClass]"
# Убедиться что тест красный (провалился)
```

**Пример создания падающего теста:**
```csharp
// tests/SeqMcp.Tests/Services/SeqApiClientTests.cs
public class SeqApiClientTests
{
    [Fact]
    public async Task SearchEventsAsync_ShouldReturnFilteredEvents()
    {
        // Arrange
        var client = new SeqApiClient("http://localhost:5341");
        var filter = "Level='Error'";

        // Act
        var result = await client.SearchEventsAsync(filter, limit: 10);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Count <= 10);
        Assert.All(result, e => Assert.Equal("Error", e.Level));
    }
}
```

### **2. GREEN - Написать минимальный код для прохождения теста**

```bash
# Реализовать функциональность
# НИКОГДА не изменять expectations теста

# Запустить тест - он ДОЛЖЕН пройти
dotnet test --filter "FullyQualifiedName~[TestClass]"
# Убедиться что все тесты зеленые
```

**Пример минимальной имплементации:**
```csharp
// src/SeqMcp/Services/SeqApiClient.cs
public class SeqApiClient
{
    private readonly string _baseUrl;
    private readonly HttpClient _httpClient;

    public SeqApiClient(string baseUrl)
    {
        _baseUrl = baseUrl;
        _httpClient = new HttpClient();
    }

    public async Task<List<SeqEvent>> SearchEventsAsync(string filter, int limit)
    {
        var url = $"{_baseUrl}/api/events/signal?filter={Uri.EscapeDataString(filter)}&count={limit}";
        var response = await _httpClient.GetAsync(url);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<List<SeqEvent>>(json);
    }
}
```

### **3. REFACTOR - Улучшить код, сохраняя зеленые тесты**

```bash
# Рефакторинг при зеленых тестах
# Запускать тесты после каждого изменения
dotnet test --filter "FullyQualifiedName~[TestClass]"
```

**Пример рефакторинга:**
```csharp
public class SeqApiClient
{
    private readonly string _baseUrl;
    private readonly HttpClient _httpClient;

    public SeqApiClient(string baseUrl)
    {
        _baseUrl = baseUrl;
        _httpClient = new HttpClient();
    }

    public async Task<List<SeqEvent>> SearchEventsAsync(string filter, int limit)
    {
        var url = BuildSearchUrl(filter, limit);
        var response = await ExecuteRequestAsync(url);
        return await DeserializeEventsAsync(response);
    }

    private string BuildSearchUrl(string filter, int limit)
    {
        return $"{_baseUrl}/api/events/signal?filter={Uri.EscapeDataString(filter)}&count={limit}";
    }

    private async Task<HttpResponseMessage> ExecuteRequestAsync(string url)
    {
        var response = await _httpClient.GetAsync(url);
        response.EnsureSuccessStatusCode();
        return response;
    }

    private async Task<List<SeqEvent>> DeserializeEventsAsync(HttpResponseMessage response)
    {
        var json = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<List<SeqEvent>>(json);
    }
}
```

## 🚫 ЗАПРЕЩЕННЫЕ действия

### 1. Изменение теста при ошибке компиляции

```csharp
// ❌ НЕПРАВИЛЬНО: Адаптация теста под код
[Fact]
public void HandleCropConfiguration_ShouldProcessObject()
{
    var options = new { Crop = new { X = 0, Y = 0, Width = 800, Height = 600 } };
    // Изменили тест, чтобы подогнать под существующий код
}

// ✅ ПРАВИЛЬНО: Сохранить изначальные требования
[Fact]
public void HandleCropConfiguration_ShouldProcessString()
{
    var options = new { Crop = "fill" };
    // Сохранили изначальное требование, меняем код
}
```

### 2. Написание кода без запуска тестов

```bash
# ❌ НЕПРАВИЛЬНО
# Написать код
# Написать еще код
# Написать еще больше кода
# Запустить тесты в конце

# ✅ ПРАВИЛЬНО
# Написать код
dotnet test --filter "FullyQualifiedName~[TestClass]"
# Написать еще код
dotnet test --filter "FullyQualifiedName~[TestClass]"
# После каждого изменения проверять
```

### 3. Предположение что тесты проходят

```bash
# ❌ НЕПРАВИЛЬНО: "Тесты должны проходить"
# Не запускать тесты

# ✅ ПРАВИЛЬНО: Всегда проверять фактически
dotnet test --filter "FullyQualifiedName~[TestClass]"
# Убедиться визуально в результате
```

## ✅ Правильный TDD подход

### При ошибке компиляции:

1. **Внимательно прочитать сообщение об ошибке**
2. **Определить какой API ожидает тест**
3. **Имплементировать этот API в коде**
4. **Запустить тесты для проверки**
5. **НИКОГДА не изменять тест под текущий код**

### Пример правильного исправления:

**Тест ожидает строковый crop:**
```csharp
// Тест (НЕ МЕНЯТЬ):
[Fact]
public void Transform_ShouldHandleStringCrop()
{
    var options = new TransformOptions { Crop = "fill" };
    var result = Transform(image, options);
    Assert.Equal("fill", result.Crop);
}

// Имплементация (ИСПРАВИТЬ):
public class TransformOptions
{
    public object Crop { get; set; }  // Поддержать строку И объект
}

private CropConfig NormalizeCrop(object crop)
{
    if (crop is string cropString)
    {
        return CropPresets[cropString]; // Конвертировать fill в config
    }
    return crop as CropConfig;
}
```

## 📊 Типы тестов

### 1. Unit тесты (Domain & Application слои)
- **Назначение**: Тестирование отдельных функций/методов в изоляции
- **Покрытие**: >80% для критической бизнес-логики
- **TDD**: ОБЯЗАТЕЛЕН

```csharp
// Unit тест для сервиса
public class SeqApiClientTests
{
    [Fact]
    public async Task SearchEventsAsync_ShouldReturnFilteredEvents()
    {
        // Arrange
        var client = new SeqApiClient("http://localhost:5341");
        var filter = "Level='Error'";

        // Act
        var events = await client.SearchEventsAsync(filter, limit: 10);

        // Assert
        Assert.NotNull(events);
        Assert.True(events.Count <= 10);
        Assert.All(events, e => Assert.Equal("Error", e.Level));
    }
}
```

### 2. Integration тесты (Infrastructure & Presentation слои)
- **Назначение**: Тестирование взаимодействия компонентов
- **Покрытие**: >60% для API endpoints и внешних сервисов
- **TDD**: ОБЯЗАТЕЛЕН

```csharp
// Integration тест для API endpoint
public class SeqApiClientIntegrationTests
{
    [Fact(Skip = "Requires running Seq server")]
    public async Task CreateSignal_ShouldSucceed()
    {
        // Arrange
        var client = new SeqApiClient("http://localhost:5341");
        var signal = new SignalRequest
        {
            Title = "Test Signal",
            Filter = "Level='Error'"
        };

        // Act
        var result = await client.CreateSignalAsync(signal);

        // Assert
        Assert.NotNull(result);
        Assert.NotEmpty(result.Id);
        Assert.Equal("Test Signal", result.Title);
    }
}
```

### 3. E2E тесты (Полные сценарии)
- **Назначение**: Тестирование критических пользовательских сценариев
- **Покрытие**: Основные business flows
- **TDD**: Рекомендуется

```csharp
// E2E тест полного сценария
public class SignalManagementFlowTests
{
    [Fact]
    public async Task FullSignalLifecycle_ShouldSucceed()
    {
        var client = new SeqApiClient("http://localhost:5341");

        // 1. Создать signal
        var signal = await client.CreateSignalAsync(new SignalRequest
        {
            Title = "Critical Errors",
            Filter = "Level='Error' AND Application='MyApp'"
        });

        // 2. Обновить signal
        await client.UpdateSignalAsync(signal.Id, new SignalRequest
        {
            Title = "Critical Errors - Updated",
            Filter = "Level='Error'"
        });

        // 3. Удалить signal
        await client.DeleteSignalAsync(signal.Id);

        // Assert
        Assert.NotNull(signal);
    }
}
```

## 🎯 Обязательная проверка перед завершением задачи

**Каждая задача ДОЛЖНА пройти все проверки:**

```bash
# 1. Специфичный тест модуля/класса
dotnet test --filter "FullyQualifiedName~[ClassName]"

# 2. Все тесты проекта
dotnet test

# 3. Проверка покрытия (>60%)
dotnet test /p:CollectCoverage=true /p:CoverletOutputFormat=opencover

# 4. Компиляция проекта
dotnet build --configuration Release

# 5. Проверка форматирования
dotnet format --verify-no-changes
```

**Задача НЕ может быть завершена если:**
- ❌ Тесты не проходят
- ❌ Покрытие <60%
- ❌ Компиляция не проходит
- ❌ Форматирование не соответствует
- ❌ TDD цикл не был соблюден

## 📁 Структура тестов

### Обязательная организация тестов:

```
tests/[Project].Tests/
├── Services/                 # Unit тесты сервисов
│   ├── SeqApiClientTests.cs
│   ├── SeqApiClientSqlTests.cs
│   └── SeqApiClientSignalsTests.cs
├── Integration/              # Integration тесты
│   ├── SeqApiClientIntegrationTests.cs
│   └── SeqApiClientSignalManagementIntegrationTests.cs
├── Controllers/              # Тесты контроллеров
│   └── HealthControllerTests.cs
├── Middleware/               # Тесты middleware
│   └── ScopeHeaderMiddlewareTests.cs
├── Helpers/                  # Test helpers
│   └── SeqTestHelper.cs
└── Fixtures/                 # Test fixtures
    └── SeqFixture.cs
```

### Naming Conventions:

- **Test files**: `[ClassName]Tests.cs`
- **Test classes**: `public class [ClassName]Tests`
- **Test methods**: `public async Task MethodName_Scenario_ExpectedBehavior()`
- **Integration tests**: `[ClassName]IntegrationTests.cs`

## 🔧 Test Utilities и Mocks

### Создание качественных моков (с Moq):

```csharp
// tests/SeqMcp.Tests/Helpers/MockFactory.cs
using Moq;

public static class MockFactory
{
    public static Mock<ISeqApiClient> CreateSeqApiClientMock()
    {
        var mock = new Mock<ISeqApiClient>();

        mock.Setup(x => x.SearchEventsAsync(It.IsAny<string>(), It.IsAny<int>()))
            .ReturnsAsync(new List<SeqEvent>
            {
                new SeqEvent { Level = "Error", Message = "Test error" }
            });

        return mock;
    }

    public static SeqEvent CreateTestEvent(string level = "Information")
    {
        return new SeqEvent
        {
            Id = Guid.NewGuid().ToString(),
            Level = level,
            Message = "Test message",
            Timestamp = DateTime.UtcNow
        };
    }
}
```

### Test Helper функции:

```csharp
// tests/SeqMcp.Tests/Helpers/SeqTestHelper.cs
public static class SeqTestHelper
{
    public static async Task<bool> ShouldSkipIntegrationTest()
    {
        var seqUrl = Environment.GetEnvironmentVariable("SEQ_URL")
                     ?? "http://localhost:5341";

        try
        {
            using var client = new HttpClient();
            var response = await client.GetAsync($"{seqUrl}/api");
            return !response.IsSuccessStatusCode;
        }
        catch
        {
            return true; // Skip if Seq is not available
        }
    }

    public static string GenerateUniqueSignalTitle()
    {
        return $"Test Signal {Guid.NewGuid().ToString("N")[..8]}";
    }
}
```

## 📈 Метрики качества тестов

### Обязательные показатели:

1. **Test Coverage**: >60% для production модулей
2. **Test Success Rate**: 100% (все тесты должны проходить)
3. **Test Performance**: Unit тесты <100ms, Integration <1s
4. **Test Maintainability**: Рефакторинг тестов при изменении API

### Мониторинг качества:

```bash
# Детальный отчет по покрытию
dotnet test /p:CollectCoverage=true /p:CoverletOutputFormat=opencover /p:CoverletOutput=./coverage/

# Производительность тестов
dotnet test --logger "console;verbosity=detailed"

# Только конкретный namespace
dotnet test --filter "FullyQualifiedName~SeqMcp.Tests.Services"
```

## 🚀 TDD для различных слоев архитектуры

### Services Layer (строгий TDD):
- API client implementations (SeqApiClient)
- Business logic services
- Data transformations
- Error handling

### Controllers Layer (строгий TDD):
- HTTP request/response handling
- Route handlers
- Input validation
- Status code logic

### Middleware Layer (TDD для логики):
- Request/response processing
- Header extraction
- Scope filtering
- Error handling

### Models/DTOs (TDD при валидации):
- Data validation logic
- Transformation methods
- Serialization/deserialization

## 📝 Чек-лист TDD соответствия

### ✅ Готовность задачи (TDD perspective):

- [ ] ✅ Тесты написаны ДО имплементации
- [ ] ✅ Соблюден цикл RED → GREEN → REFACTOR
- [ ] ✅ Все тесты проходят (100% success rate)
- [ ] ✅ Покрытие тестами >60%
- [ ] ✅ Тесты документируют поведение системы
- [ ] ✅ Нет изменений тестов для исправления компиляции
- [ ] ✅ C# компилируется без ошибок
- [ ] ✅ Форматирование проходит (dotnet format)
- [ ] ✅ Integration тесты покрывают API endpoints
- [ ] ✅ Моки созданы для внешних зависимостей (Moq)

---

**⚠️ ВАЖНО**: Данный стандарт является ОБЯЗАТЕЛЬНЫМ для всех implementation задач. Нарушение TDD процесса считается критической ошибкой в разработке.

**📧 Контакты**: По вопросам TDD стандарта обращаться к senior-code-reviewer agent.

**🔄 Версионирование**: При изменениях стандарта все команды должны быть уведомлены заранее.