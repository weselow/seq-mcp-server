# Seq MCP Server

MCP (Model Context Protocol) сервер для Seq - позволяет LLM приложениям взаимодействовать с платформой структурированного логирования Seq.

> [English version](README-EN.md)

## CI/CD Status

![CI](https://github.com/weselow/seq-mcp-server/workflows/CI/badge.svg)
![Docker Build](https://github.com/weselow/seq-mcp-server/workflows/Docker%20Build%20and%20Push/badge.svg)
![Security](https://github.com/weselow/seq-mcp-server/workflows/Security%20and%20Code%20Quality/badge.svg)

## ✨ Возможности

- **7 MCP инструментов**: Поиск событий, управление сигналами, SQL запросы, список приложений
- **9 MCP ресурсов**: Быстрый доступ к последним событиям (seq://)
- **8 MCP промптов**: Готовые шаблоны для анализа логов (на русском)
- **HTTP Transport**: Server-Sent Events (SSE) по спецификации MCP 2025-03-26
- **Интеграция с Seq**: Нативная интеграция с Seq.Api 2025.2.2
- **Scope Filtering**: Автоматическая фильтрация по проекту через HTTP заголовки/ENV
- **Production-Ready HttpClient**: Оптимизированный Singleton с connection pooling
- **Health Check Endpoint**: Мониторинг состояния сервера и Seq подключения
- **Оптимизация токенов**: Краткие описания для экономии контекста LLM (экономия ~70% токенов)
- **Русский язык**: Все описания и промпты на русском для удобства российских пользователей

## 🏗️ Архитектура

- **Язык**: C# / .NET 9 (ASP.NET Core)
- **Протокол**: MCP 2025-03-26 (Streamable HTTP/SSE)
- **Тестирование**: xUnit с покрытием 94.4% методов, 41.9% строк кода
- **Дизайн**: Clean Architecture со строгим TDD подходом
- **DI**: Microsoft.Extensions.DependencyInjection
- **Логирование**: ILogger со структурированным логированием

## Структура проекта

```
seq-mcp-server/
├── src/
│   └── SeqMcp/
│       ├── Configuration/      # Конфигурация (SeqServerConfig, SeqRequestContext)
│       ├── Services/           # Обёртка для Seq API клиента (SeqApiClient)
│       ├── Tools/              # MCP инструменты (SeqTools)
│       ├── Resources/          # MCP ресурсы (SeqResources)
│       ├── Prompts/            # MCP промпты (SeqPrompts)
│       ├── Models/             # Модели данных (DTO)
│       └── Program.cs          # Точка входа и DI конфигурация
├── tests/
│   └── SeqMcp.Tests/           # Unit и интеграционные тесты
└── docs/
    └── standards/              # Стандарты разработки
```

## ⚙️ Конфигурация

### Переменные окружения

```bash
# Подключение к Seq серверу (поддерживаются оба варианта)
export SEQ_URL="http://localhost:8080"           # По умолчанию: http://localhost:8080
export SEQ_SERVER_URL="http://localhost:8080"    # Альтернативное имя (для совместимости)
export SEQ_API_KEY="your-api-key"                # Опционально, для Seq с аутентификацией

# Порт MCP сервера
export PORT="5555"                                # По умолчанию: 5555

# Фильтрация по проекту (опционально)
export SEQ_PROJECT_SCOPE="MyProject"             # Опционально: фильтр по имени проекта
export SEQ_SCOPE_FIELD="Application"             # По умолчанию: "Application"
```

**Примечание:** Можно использовать либо `SEQ_URL`, либо `SEQ_SERVER_URL` - они взаимозаменяемы. Приоритет имеет `SEQ_URL`.

### Фильтрация по проекту (Scope Filtering)

Сервер поддерживает автоматическую фильтрацию событий по проекту/приложению. Это полезно когда несколько проектов логируют в один Seq сервер.

**Приоритет конфигурации** (от высшего к низшему):
1. **HTTP заголовки** (для HTTP MCP транспорта)
2. **Переменные окружения** (`SEQ_PROJECT_SCOPE`, `SEQ_SCOPE_FIELD`)
3. **appsettings.json** (`Seq:ProjectScope`, `Seq:ScopeField`)
4. **Без фильтрации** (если ничего не задано)

**Примеры использования:**

**Через HTTP заголовки:**
```bash
curl -H "X-Seq-Project-Scope: MyProject" \
     -H "X-Seq-Scope-Field: Application" \
     http://localhost:5555/mcp/v1
```

**Через переменные окружения:**
```bash
export SEQ_PROJECT_SCOPE="MyProject"
export SEQ_SCOPE_FIELD="Application"
dotnet run
```

**Через appsettings.json:**
```json
{
  "Seq": {
    "Url": "http://localhost:8080",
    "ApiKey": "your-api-key",
    "ProjectScope": "MyProject",
    "ScopeField": "Application"
  }
}
```

При включенной фильтрации, все запросы автоматически добавляют условие:
```
Application = 'MyProject'
```

Если пользователь добавляет свой фильтр:
```
Level = 'Error'
```

Итоговый фильтр будет:
```
(Application = 'MyProject') and (Level = 'Error')
```

### Health Check Endpoint

Сервер предоставляет `/health` endpoint для мониторинга состояния и проверки доступности Seq сервера.

**URL**: `GET http://localhost:5555/health`

**Ответ при успешном состоянии (200 OK):**
```json
{
  "status": "healthy",
  "version": "1.0.0.0",
  "uptimeSeconds": 3600,
  "seqConnection": {
    "isHealthy": true,
    "message": "Connected to Seq server",
    "responseTimeMs": 45
  },
  "metrics": {
    "total_requests": 150,
    "uptime_seconds": 3600,
    "seq_response_time_ms": 45
  }
}
```

**Ответ при проблемах (503 Service Unavailable):**
```json
{
  "status": "unhealthy",
  "version": "1.0.0.0",
  "uptimeSeconds": 3600,
  "seqConnection": {
    "isHealthy": false,
    "message": "Connection failed: Connection refused",
    "responseTimeMs": 1000
  },
  "metrics": {
    "total_requests": 150,
    "uptime_seconds": 3600,
    "seq_response_time_ms": 1000
  }
}
```

**Метрики:**
- `total_requests` - общее количество health check запросов с момента запуска
- `uptime_seconds` - время работы сервера в секундах
- `seq_response_time_ms` - время ответа Seq сервера в миллисекундах

**Использование для мониторинга:**
```bash
# Проверка доступности
curl http://localhost:5555/health

# Kubernetes liveness probe
livenessProbe:
  httpGet:
    path: /health
    port: 5555
  initialDelaySeconds: 10
  periodSeconds: 30

# Prometheus monitoring
http://localhost:5555/health
```

## 🚀 Быстрый старт

### Запуск в Docker (рекомендуется)

Самый простой способ - использовать готовый Docker образ из GitHub Container Registry:

```bash
# 1. Запустить Seq MCP Server (укажите URL вашего Seq сервера)
docker run -d \
  --name seq-mcp \
  -p 5555:5555 \
  -e SEQ_URL=http://your-seq-server:5341 \
  ghcr.io/weselow/seq-mcp-server:latest

# 2. Проверить что запустился
curl http://localhost:5555/health
```

**Обязательные параметры:**
- `SEQ_URL` - адрес вашего Seq сервера (например, `http://localhost:5341`)
- `SEQ_API_KEY` - API ключ (если Seq требует аутентификацию)

**Опциональные параметры для фильтрации:**
- `SEQ_PROJECT_SCOPE` - имя проекта для фильтрации логов (например, `"MyWebApp"`)
- `SEQ_SCOPE_FIELD` - поле в логах для фильтрации (по умолчанию `"Application"`)

**Зачем нужна фильтрация (Scope Filtering)?**

Если в ваш Seq пишут несколько проектов (WebApp, BackgroundService, API), то без фильтрации LLM получит логи ВСЕХ проектов, что:
- Тратит токены на ненужные логи
- Замедляет поиск нужной информации
- Перегружает контекст LLM

С фильтрацией по `Application = 'MyWebApp'` вы получите только логи вашего проекта, экономя токены и улучшая точность анализа.

### Пример конфигурации MCP сервера для Claude Desktop

После запуска Docker контейнера, добавьте сервер в конфигурацию Claude Desktop:

**Windows** (`%APPDATA%\Claude\claude_desktop_config.json`):
```json
{
  "mcpServers": {
    "seq": {
      "url": "http://localhost:5555/sse"
    }
  }
}
```

**Linux/macOS** (`~/.config/Claude/claude_desktop_config.json`):
```json
{
  "mcpServers": {
    "seq": {
      "url": "http://localhost:5555/sse"
    }
  }
}
```

**Примечание:** Все настройки (SEQ_URL, фильтрация) задаются при запуске Docker контейнера через `-e` переменные. В конфигурации Claude Desktop указывается только URL MCP сервера.

### Полный пример с Docker Compose

Запустить Seq и MCP сервер вместе:

```bash
# 1. Клонировать репозиторий
git clone https://github.com/weselow/seq-mcp-server.git
cd seq-mcp-server

# 2. Запустить всё одной командой
docker-compose up -d

# 3. Проверить
curl http://localhost:5555/health

# Seq UI доступен: http://localhost:8080
```

В `docker-compose.yml` включено:
- Seq сервер (порт 8080 UI, 5341 ingestion)
- Seq MCP Server (порт 5555)
- Health checks и автозависимости

---

### Альтернатива: Запуск без Docker

**Требования:**
- .NET 9 SDK
- Запущенный Seq сервер

**Сборка и запуск:**
```bash
# Сборка
dotnet build

# Публикация
dotnet publish src/SeqMcp/SeqMcp.csproj -c Release -o ./publish

# Запуск
export SEQ_URL="http://localhost:5341"
./publish/SeqMcp
```

**Конфигурация для Claude Desktop (без Docker):**

**Windows:**
```json
{
  "mcpServers": {
    "seq": {
      "command": "M:\\repos\\seq-mcp-server\\publish\\SeqMcp.exe",
      "env": {
        "SEQ_URL": "http://localhost:5341",
        "SEQ_API_KEY": "ваш-api-ключ-если-нужен",
        "SEQ_PROJECT_SCOPE": "MyProject",
        "SEQ_SCOPE_FIELD": "Application"
      }
    }
  }
}
```

**Linux/macOS:**
```json
{
  "mcpServers": {
    "seq": {
      "command": "/path/to/seq-mcp-server/publish/SeqMcp",
      "env": {
        "SEQ_URL": "http://localhost:5341",
        "SEQ_API_KEY": "ваш-api-ключ-если-нужен",
        "SEQ_PROJECT_SCOPE": "MyProject",
        "SEQ_SCOPE_FIELD": "Application"
      }
    }
  }
}
```

## 🐳 Docker

### Быстрый старт с Docker Compose (рекомендуется)

Самый простой способ запустить Seq MCP Server вместе с Seq сервером:

```bash
# 1. Создайте .env файл (опционально)
cp .env.example .env
# Отредактируйте .env при необходимости

# 2. Запустите оба сервиса
docker-compose up -d

# 3. Проверьте логи
docker-compose logs -f seq-mcp

# 4. Проверьте health check
curl http://localhost:5555/health

# 5. Откройте Seq UI
http://localhost:8080
```

**Что включено:**
- `seq` - Seq log server (порт 8080 UI, 5341 ingestion)
- `seq-mcp` - Seq MCP Server (порт 5555)
- Health checks для обоих сервисов
- Автоматическая зависимость (MCP ждёт готовности Seq)
- Persistent volume для данных Seq

**Управление:**
```bash
# Остановить
docker-compose down

# Остановить и удалить данные
docker-compose down -v

# Перезапустить
docker-compose restart seq-mcp

# Посмотреть статус
docker-compose ps
```

### Сборка Docker образа

```bash
# Собрать образ
docker build -t seq-mcp-server:latest .

# Запустить контейнер
docker run -d \
  --name seq-mcp \
  -p 5555:5555 \
  -e SEQ_URL=http://your-seq-server:80 \
  -e SEQ_API_KEY=your-api-key \
  seq-mcp-server:latest

# Проверить логи
docker logs -f seq-mcp

# Проверить health check
docker exec seq-mcp curl http://localhost:5555/health
```

### Переменные окружения Docker

| Переменная | Описание | По умолчанию |
|-----------|----------|--------------|
| `SEQ_URL` | URL Seq сервера | `http://localhost:8080` |
| `SEQ_API_KEY` | API ключ Seq (опционально) | - |
| `SEQ_PROJECT_SCOPE` | Scope для фильтрации | - |
| `SEQ_SCOPE_FIELD` | Поле для scope фильтрации | `Application` |
| `PORT` | Порт MCP сервера | `5555` |
| `ASPNETCORE_ENVIRONMENT` | Окружение ASP.NET Core | `Production` |

### Docker в production

**Docker Compose файл для production:**

```yaml
version: '3.8'

services:
  seq-mcp:
    image: your-registry/seq-mcp-server:latest
    container_name: seq-mcp-prod
    ports:
      - "5555:5555"
    environment:
      - SEQ_URL=http://your-seq-server:80
      - SEQ_API_KEY=${SEQ_API_KEY}
      - SEQ_PROJECT_SCOPE=ProductionApp
    restart: always
    healthcheck:
      test: ["CMD", "curl", "-f", "http://localhost:5555/health"]
      interval: 30s
      timeout: 10s
      retries: 3
    deploy:
      resources:
        limits:
          cpus: '0.5'
          memory: 512M
        reservations:
          cpus: '0.25'
          memory: 256M
```

**Kubernetes Deployment:**

```yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: seq-mcp-server
spec:
  replicas: 2
  selector:
    matchLabels:
      app: seq-mcp
  template:
    metadata:
      labels:
        app: seq-mcp
    spec:
      containers:
      - name: seq-mcp
        image: your-registry/seq-mcp-server:latest
        ports:
        - containerPort: 5555
        env:
        - name: SEQ_URL
          value: "http://seq-service:80"
        - name: SEQ_API_KEY
          valueFrom:
            secretKeyRef:
              name: seq-credentials
              key: api-key
        livenessProbe:
          httpGet:
            path: /health
            port: 5555
          initialDelaySeconds: 10
          periodSeconds: 30
        readinessProbe:
          httpGet:
            path: /health
            port: 5555
          initialDelaySeconds: 5
          periodSeconds: 10
        resources:
          limits:
            cpu: 500m
            memory: 512Mi
          requests:
            cpu: 250m
            memory: 256Mi
```

### Тесты

```bash
# Запуск всех тестов
dotnet test

# Запуск с покрытием
dotnet test --collect:"XPlat Code Coverage"
```

## Разработка

Проект следует строгим практикам TDD. Смотрите `docs/standards/`:

- `GLOBAL-implementation-standard.md` - Основные принципы разработки
- `tdd-standard.md` - TDD процесс и правила

### Ключевые принципы

1. Цикл **RED → GREEN → REFACTOR**
2. Тесты ПЕРВЫМИ, код вторым
3. Никогда не изменять тесты для исправления ошибок компиляции
4. Функции < 30 строк, сложность < 10

## 🛠️ MCP инструменты

Сервер предоставляет 7 инструментов для работы с Seq:

### 1. seq_search_events

Поиск и получение событий логов Seq с опциональной фильтрацией.

**Параметры:**
- `filter` (строка, опционально): Фильтр Seq запроса (например, `"Level = 'Error'"`, `"@Exception is not null"`). По умолчанию: "" (все события)
- `limit` (целое, опционально): Максимальное количество возвращаемых событий. По умолчанию: 100

**Возвращает:** JSON со структурированными событиями логов включая:
- ID события
- Временная метка
- Уровень лога (Information, Warning, Error и т.д.)
- Отрендеренное сообщение
- Детали исключения (если присутствует)

**Пример:**
```json
{
  "Events": [...],
  "TotalCount": 42
}
```

### 2. seq_list_signals

Список всех сохранённых сигналов Seq (алертов/сохранённых поисков).

**Параметры:** Нет

**Возвращает:** JSON с сигналами включая:
- ID сигнала
- Название
- Описание
- Запрос фильтра

**Пример:**
```json
{
  "Signals": [...],
  "TotalCount": 5
}
```

### 3. seq_execute_sql

Выполнение SQL запроса к данным логов Seq.

**Параметры:**
- `query` (строка, обязательно): SQL запрос используя синтаксис Seq SQL (например, `"select count(*) from stream where Level = 'Error'"`)

**Возвращает:** JSON с результатами запроса:
- Оригинальный запрос
- Данные результата (JSON строка)
- Количество строк

**Пример:**
```json
{
  "Query": "select count(*) from stream",
  "Result": "{...}",
  "RowCount": 1
}
```

### 4. seq_create_signal

Создание нового сигнала/алерта в Seq.

**Параметры:**
- `title` (строка, обязательно): Название сигнала
- `description` (строка, опционально): Описание сигнала
- `filter` (строка, опционально): Фильтр Seq для сигнала
- `isProtected` (boolean, опционально): Защищённый сигнал (по умолчанию false)

**Возвращает:** JSON с результатом создания:
- ID созданного сигнала
- Название
- Сообщение об успехе

**Пример:**
```json
{
  "SignalId": "signal-12345",
  "Title": "High Error Rate",
  "Message": "Signal 'High Error Rate' created successfully"
}
```

### 5. seq_update_signal

Обновление существующего сигнала.

**Параметры:**
- `signalId` (строка, обязательно): ID сигнала для обновления
- `title` (строка, опционально): Новое название
- `description` (строка, опционально): Новое описание
- `filter` (строка, опционально): Новый фильтр

**Возвращает:** JSON с результатом обновления:
- ID сигнала
- Сообщение об успехе

**Пример:**
```json
{
  "SignalId": "signal-12345",
  "Message": "Signal 'signal-12345' updated successfully"
}
```

### 6. seq_delete_signal

Удаление сигнала по ID.

**Параметры:**
- `signalId` (строка, обязательно): ID сигнала для удаления

**Возвращает:** JSON с результатом удаления:
- ID сигнала
- Сообщение об успехе

**Пример:**
```json
{
  "SignalId": "signal-12345",
  "Message": "Signal 'signal-12345' deleted successfully"
}
```

### 7. seq_get_apps

Получение списка приложений, логирующих в Seq.

**Параметры:**
- `limit` (целое, опционально): Максимальное количество приложений (по умолчанию 50)

**Возвращает:** JSON со списком приложений:
- Список приложений с именами и количеством событий
- Общее количество

**Пример:**
```json
{
  "Applications": [
    {
      "Name": "WebApp",
      "EventCount": 15420
    },
    {
      "Name": "BackgroundService",
      "EventCount": 8932
    }
  ],
  "TotalCount": 2
}
```

## 📦 MCP ресурсы

Ресурсы предоставляют быстрый доступ к данным через URI схему `seq://`:

### 1. seq://events/latest
Последние 50 событий из Seq (все уровни)

### 2. seq://events/errors
Последние 50 ошибок (уровни Error + Fatal)

### 3. seq://events/warnings
Последние 50 предупреждений (уровень Warning)

### 4. seq://events/exceptions
События с исключениями (последние 50)

### 5. seq://signals
Все сохранённые сигналы Seq

### 6. seq://events/last-hour
События за последний час (все уровни, до 100)

### 7. seq://events/today
События за сегодня (все уровни, до 200)

### 8. seq://performance/slow
Медленные операции с Elapsed > 1000ms (последние 50)

### 9. seq://stats/summary
Статистика событий за последний час по уровням (SQL агрегация)

## 💡 MCP промпты (шаблоны)

Готовые промпты для типичных задач анализа логов (на русском):

### 1. seq_analyze_errors
**Параметр**: `period` (1h, 24h, 7d)

Анализ ошибок за период с выводом топ-5, паттернов и рекомендаций

### 2. seq_top_exceptions
**Параметр**: `count` (по умолчанию: 10)

Топ исключений с группировкой и анализом

### 3. seq_activity_summary
**Параметр**: `period` (1h, 24h, 7d)

Сводка активности по уровням логирования

### 4. seq_check_signals
Проверка всех активных сигналов

### 5. seq_performance_check
**Параметр**: `period` (1h, 24h)

Анализ производительности и проблем

### 6. seq_trace_request
**Параметр**: `requestId` (обязательно)

Трассировка запроса по RequestId/CorrelationId

### 7. seq_security_audit
**Параметр**: `period` (1h, 24h, 7d)

Аудит событий безопасности (auth, unauthorized и т.д.)

### 8. seq_daily_report
Ежедневный отчёт о состоянии логов

## 🔌 Интеграция с Claude Desktop

Конфигурация Claude Desktop описана в разделе [🚀 Быстрый старт](#-быстрый-старт) выше.

**Два способа подключения:**

1. **Docker (рекомендуется)** - запустите контейнер и укажите `"url": "http://localhost:5555/sse"` в конфиге
2. **Без Docker** - опубликуйте проект и укажите `"command"` с путём к `.exe` в конфиге

Подробные примеры конфигурации для Windows/Linux/macOS смотрите в разделе "Быстрый старт".

## 📋 TODO / Дорожная карта

- [x] ~~Завершить интеграцию с Seq.Api~~
- [x] ~~Добавить инструмент `seq_list_signals`~~
- [x] ~~Добавить инструмент `seq_execute_sql`~~
- [x] ~~Реализация MCP протокола~~
- [x] ~~HTTP/SSE транспорт~~
- [x] ~~Обработка ошибок с логированием~~
- [x] ~~MCP Resources (seq://events, seq://signals)~~
- [x] ~~MCP Prompts (шаблоны запросов)~~
- [x] ~~Scope filtering (фильтрация по проекту)~~
- [x] ~~Production-ready HttpClient с connection pooling~~
- [x] ~~Health Check endpoint~~
- [x] ~~Docker контейнеризация (Dockerfile, docker-compose, .dockerignore)~~
- [x] ~~Дополнительные MCP Resources (last-hour, today, slow, stats)~~
- [x] ~~Дополнительные MCP Tools (create_signal, update_signal, delete_signal, get_apps)~~
- [x] ~~Интеграционные тесты с живым Seq сервером (13 integration тестов)~~
- [x] ~~CI/CD pipeline (GitHub Actions) - 3 workflows: CI, Docker, Security~~

**CI/CD Pipeline**: См. [docs/CICD.md](docs/CICD.md) для полной документации

## 📦 Зависимости

- **ModelContextProtocol.AspNetCore** 0.4.0-preview.2 - Официальный MCP SDK для ASP.NET Core
- **Seq.Api** 2025.2.2 - Официальный Seq HTTP API клиент
- **Microsoft.Extensions.Logging** 9.0.9 - Структурированное логирование
- **xUnit** - Фреймворк для тестирования
- **FluentAssertions** - Fluent утверждения для тестов
- **Moq** - Фреймворк для моков

## 🧪 Тестирование

### Запуск тестов

```bash
# Запуск всех тестов
dotnet test

# Запуск с покрытием
dotnet test --collect:"XPlat Code Coverage"

# Запуск конкретного тестового класса
dotnet test --filter "FullyQualifiedName~SeqToolsTests"
```

### Статистика тестов

- **Unit тесты**: 35 тестов (всегда выполняются)
- **Integration тесты**: 13 тестов (требуют Seq сервер, по умолчанию Skip)
- **Всего**: 48 тестов
- **Успешность**: 100% (35/35 unit тестов прошли)
- **Покрытие**: Scope filtering (7), Health Check (8), Signal Management (9 integration)

**Запуск интеграционных тестов:**
```bash
# 1. Запустить Seq через Docker
docker run -d --name seq-test -e ACCEPT_EULA=Y -p 5341:80 datalust/seq

# 2. Запустить тесты (integration тесты останутся Skip)
dotnet test

# 3. Для запуска integration тестов удалите атрибут Skip из тестов
```

Подробная документация: [docs/INTEGRATION_TESTS.md](docs/INTEGRATION_TESTS.md)

## 🤝 Участие в разработке

1. Следуйте TDD стандартам в `docs/standards/`
2. Всегда пишите тесты ПЕРВЫМИ (RED → GREEN → REFACTOR)
3. Поддерживайте >60% покрытие методов
4. Все тесты должны проходить перед PR
5. Используйте conventional commits

## 📄 Лицензия

MIT

## 🔗 Ссылки

- [Спецификация Model Context Protocol](https://spec.modelcontextprotocol.io/)
- [Документация Seq](https://docs.datalust.co/docs)
- [ModelContextProtocol C# SDK](https://github.com/modelcontextprotocol/csharp-sdk)

---

**Статус**: ✅ **Готов к production** - Полнофункциональный MCP сервер с 7 инструментами, 9 ресурсами, 8 промптами, HTTP транспортом, обработкой ошибок и полным тестированием
