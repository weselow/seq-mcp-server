# Seq MCP Server

MCP (Model Context Protocol) сервер для Seq - позволяет LLM приложениям взаимодействовать с платформой структурированного логирования Seq.

> [English version](README-EN.md)

## CI/CD Status

![CI](https://github.com/weselow/seq-mcp-server/workflows/CI/badge.svg)
![Docker Build](https://github.com/weselow/seq-mcp-server/workflows/Docker%20Build%20and%20Push/badge.svg)
![Security](https://github.com/weselow/seq-mcp-server/workflows/Security%20and%20Code%20Quality/badge.svg)

## ✨ Возможности

- **Два режима работы**: HTTP/SSE сервер (Docker) для команд и stdio CLI (single-file exe) для индивидуального использования с MCP-клиентами
- **7 MCP инструментов**: Поиск событий, управление сигналами, SQL запросы, список приложений
- **9 MCP ресурсов**: Быстрый доступ к последним событиям (seq://)
- **8 MCP промптов**: Готовые шаблоны для анализа логов (на русском)
- **HTTP Transport**: Server-Sent Events (SSE) по спецификации MCP 2025-03-26
- **Мульти-арендность** (HTTP): один сервер для нескольких Seq-таргетов через `X-Seq-Url`/`X-Seq-ApiKey` (отключено по умолчанию, защита от SSRF при включении)
- **Интеграция с Seq**: Нативная интеграция с Seq.Api 2025.2.2
- **Scope Filtering**: Автоматическая фильтрация по проекту через HTTP заголовки/ENV
- **Health Check Endpoint**: Мониторинг состояния сервера и Seq подключения
- **Оптимизация токенов**: Краткие описания для экономии контекста LLM (экономия ~70% токенов)
- **Русский язык**: Все описания и промпты на русском для удобства российских пользователей

## 🏗️ Архитектура

- **Язык**: C# / .NET 9
- **Протокол**: MCP 2025-03-26 (HTTP/SSE для Docker, stdio JSON-RPC для exe)
- **Структура проектов**:
  - `src/SeqMcp.Core` — общая библиотека (Models, Services, Tools, Resources, Prompts)
  - `src/SeqMcp.Http` — ASP.NET Core веб-приложение (Docker)
  - `src/SeqMcp.Stdio` — single-file CLI exe для локального запуска
- **DI**: единая `ISeqConnectionFactory` (Singleton) с per-tenant `HttpClient`+`SeqConnection`, LRU-кэшем, lease/refcount для безопасного выселения
- **Тестирование**: xUnit, ~170 unit-тестов + integration через `Process.Start` для stdio
- **Логирование**: ILogger со структурированным логированием (в stdio — строго в stderr, stdout зарезервирован под JSON-RPC)

## 🚀 Быстрый старт

Два варианта запуска:

- **Docker (HTTP/SSE сервер)** — рекомендуется для команд, удалённых клиентов, общего деплоя. См. ниже.
- **Stdio exe** — single-file бинарь, MCP-клиент сам запускает процесс. API-ключ не покидает машину пользователя. См. раздел [🧷 Stdio mode](#-stdio-mode-локальный-exe).

### Запуск в Docker (рекомендуется)

Самый простой способ - использовать готовый Docker образ из GitHub Container Registry:

```bash
# 1. Запустить Seq MCP Server
docker run -d \
  --name seq-mcp \
  -p 5555:5555 \
  -e SEQ_URL=http://your-seq-server:5341 \
  -e SEQ_API_KEY=your-api-key-if-needed \
  ghcr.io/weselow/seq-mcp-server:latest

# 2. Проверить что запустился
curl http://localhost:5555/health
```

**Обязательные параметры:**
- `SEQ_URL` - адрес вашего Seq сервера (например, `http://localhost:5341`)

**Опциональные параметры:**
- `SEQ_API_KEY` - API ключ Seq (если требуется аутентификация)
- `SEQ_PROJECT_SCOPE` - имя проекта для фильтрации логов (например, `"MyWebApp"`)
- `SEQ_SCOPE_FIELD` - поле в логах для фильтрации (по умолчанию `"Application"`)
- `PORT` - порт MCP сервера (по умолчанию `5555`)

**Зачем нужна фильтрация (Scope Filtering)?**

Если в ваш Seq пишут несколько проектов (WebApp, BackgroundService, API), то без фильтрации LLM получит логи ВСЕХ проектов, что:
- Тратит токены на ненужные логи
- Замедляет поиск нужной информации
- Перегружает контекст LLM

С фильтрацией по `Application = 'MyWebApp'` вы получите только логи вашего проекта, экономя токены и улучшая точность анализа.

### Конфигурация Claude Desktop

После запуска MCP сервера (Docker или локально), добавьте его в конфигурацию Claude Desktop:

**Минимальная конфигурация:**
```json
{
  "mcpServers": {
    "seq": {
      "url": "http://localhost:5555/sse"    // ОБЯЗАТЕЛЬНО: URL MCP сервера
    }
  }
}
```

**Конфигурация с фильтрацией по проекту (рекомендуется):**

**Windows** (`%APPDATA%\Claude\claude_desktop_config.json`):
```json
{
  "mcpServers": {
    "seq": {
      "url": "http://localhost:5555/sse",                     // ОБЯЗАТЕЛЬНО: URL MCP сервера
      "headers": {
        "X-Seq-Project-Scope": "MyWebApp",                    // ОПЦИОНАЛЬНО: фильтр по проекту (экономит токены!)
        "X-Seq-Scope-Field": "Application"                    // ОПЦИОНАЛЬНО: поле для фильтрации (по умолчанию "Application")
      }
    }
  }
}
```

**Linux/macOS** (`~/.config/Claude/claude_desktop_config.json`):
```json
{
  "mcpServers": {
    "seq": {
      "url": "http://localhost:5555/sse",                     // ОБЯЗАТЕЛЬНО: URL MCP сервера
      "headers": {
        "X-Seq-Project-Scope": "MyWebApp",                    // ОПЦИОНАЛЬНО: фильтр по проекту (экономит токены!)
        "X-Seq-Scope-Field": "Application"                    // ОПЦИОНАЛЬНО: поле для фильтрации (по умолчанию "Application")
      }
    }
  }
}
```

**Важно:**
- Наш сервер использует **HTTP/SSE транспорт**, поэтому используется `url`, а не `command`
- **Фильтрация через заголовки** (`X-Seq-Project-Scope`) позволяет получать только логи нужного проекта - это экономит токены LLM и ускоряет анализ
- После изменения конфигурации перезапустите Claude Desktop
- Заголовки передаются в каждом HTTP запросе к MCP серверу

**Примечание:** Для запуска полного стека (Seq + MCP сервер) используйте Docker Compose - см. раздел [🐳 Docker](#-docker).

---

### Альтернатива: Запуск без Docker

**Требования:**
- .NET 9 SDK
- Запущенный Seq сервер

**Шаг 1: Сборка и публикация**
```bash
# Сборка всего решения
dotnet build

# Публикация HTTP-сервера
dotnet publish src/SeqMcp.Http/SeqMcp.Http.csproj -c Release -o ./publish
```

**Шаг 2: Запуск MCP сервера**

Запустите сервер в отдельном терминале:

**Windows (PowerShell):**
```powershell
# Минимальная конфигурация (только обязательные параметры)
$env:SEQ_URL="http://localhost:5341"              # ОБЯЗАТЕЛЬНО: URL Seq сервера
$env:SEQ_API_KEY="your-api-key"                   # ОПЦИОНАЛЬНО: API ключ (если Seq требует аутентификацию)
dotnet .\publish\SeqMcp.Http.dll
```

**Linux/macOS:**
```bash
# Минимальная конфигурация (только обязательные параметры)
export SEQ_URL="http://localhost:5341"            # ОБЯЗАТЕЛЬНО: URL Seq сервера
export SEQ_API_KEY="your-api-key"                 # ОПЦИОНАЛЬНО: API ключ (если Seq требует аутентификацию)
dotnet ./publish/SeqMcp.Http.dll
```

**Дополнительные переменные окружения (опционально):**
```bash
export PORT="5555"                                # Порт MCP сервера (по умолчанию 5555)
export SEQ_PROJECT_SCOPE="MyProject"              # Фильтр по проекту (лучше задавать через headers в Claude Desktop)
export SEQ_SCOPE_FIELD="Application"              # Поле для фильтрации (лучше задавать через headers)
```

**Шаг 3: Конфигурация Claude Desktop**

После запуска сервера, добавьте его в конфигурацию Claude Desktop. **Рекомендуется использовать фильтрацию через headers** (см. раздел "Конфигурация Claude Desktop" выше):

```json
{
  "mcpServers": {
    "seq": {
      "url": "http://localhost:5555/sse",
      "headers": {
        "X-Seq-Project-Scope": "MyWebApp",                    // ОПЦИОНАЛЬНО: фильтр по проекту - настройте под ваше приложение
        "X-Seq-Scope-Field": "Application"                    // ОПЦИОНАЛЬНО: поле для фильтрации (по умолчанию "Application")
      }
    }
  }
}
```

**Преимущества фильтрации через headers:**
- Не нужно перезапускать MCP сервер при изменении фильтра
- Можно быстро переключаться между проектами, меняя только конфиг Claude Desktop
- Фильтрация работает на уровне HTTP запросов

**Примечание:** Сервер должен быть запущен ПЕРЕД стартом Claude Desktop. Если вы перезапустите сервер, перезапустите также Claude Desktop для переподключения.

## Структура проекта

```
seq-mcp-server/
├── src/
│   ├── SeqMcp.Core/                # Общая библиотека
│   │   ├── Configuration/          # SeqOptions, SeqRequestContext, SeqOptionsLoader
│   │   ├── Hosting/                # Расширения DI для MCP-примитивов
│   │   ├── Services/               # SeqApiClient, SeqConnectionFactory, HealthCheckService
│   │   ├── Tools/                  # MCP инструменты (SeqTools)
│   │   ├── Resources/              # MCP ресурсы (SeqResources)
│   │   ├── Prompts/                # MCP промпты (SeqPrompts)
│   │   └── Models/                 # Модели данных (DTO)
│   ├── SeqMcp.Http/                # ASP.NET Core веб-приложение (Docker)
│   │   ├── Middleware/             # SeqHeadersMiddleware, RequestLoggingMiddleware
│   │   ├── Program.cs              # Точка входа HTTP-сервера, DI, /health
│   │   └── appsettings.json
│   └── SeqMcp.Stdio/               # Single-file CLI exe (stdio JSON-RPC)
│       ├── Program.cs              # Точка входа stdio-сервера
│       └── SeqMcp.Stdio.csproj
├── tests/
│   ├── SeqMcp.Tests/                       # Unit-тесты Core и Http
│   └── SeqMcp.Stdio.IntegrationTests/      # Интеграционные тесты stdio через Process.Start
└── docs/
    └── standards/                          # Стандарты разработки
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

# Мульти-арендность через HTTP-заголовки (опционально, см. раздел "Мульти-арендность")
export SEQ_ALLOW_URL_OVERRIDE="true"             # По умолчанию: false. Разрешает X-Seq-Url из запроса
export SEQ_BLOCK_PRIVATE_HOSTS="true"            # По умолчанию: false. Для публичных деплоев
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

### Мульти-арендность (HTTP-режим)

По умолчанию HTTP-сервер ходит в один Seq, заданный в конфигурации (`SEQ_URL` / `SEQ_API_KEY`). Чтобы один MCP-сервер мог обслуживать разных арендаторов с разными Seq-инстансами, есть опциональный режим переопределения через заголовки:

| Заголовок          | Назначение                                       | Требует флаг                          |
|--------------------|--------------------------------------------------|---------------------------------------|
| `X-Seq-Url`        | URL Seq для текущего запроса                     | `SEQ_ALLOW_URL_OVERRIDE=true`         |
| `X-Seq-ApiKey`     | API-ключ Seq для текущего запроса                | принимается всегда                    |
| `X-Seq-Project-Scope` | Проектная фильтрация (см. выше)              | принимается всегда                    |
| `X-Seq-Scope-Field`   | Поле для фильтрации (см. выше)               | принимается всегда                    |

**Включение режима переопределения:**

```bash
export SEQ_ALLOW_URL_OVERRIDE=true
# опционально — параноидальный режим (см. ниже)
export SEQ_BLOCK_PRIVATE_HOSTS=true
```

**Пример HTTP-запроса:**

```bash
curl -X POST http://localhost:5555/mcp/v1 \
  -H "X-Seq-Url: https://tenant-a.seq.example.com" \
  -H "X-Seq-ApiKey: per-tenant-api-key" \
  -H "Content-Type: application/json" \
  --data '{...MCP-запрос...}'
```

Если флаг `SEQ_ALLOW_URL_OVERRIDE` не задан или равен `false`, заголовок `X-Seq-Url` тихо игнорируется и в лог пишется однократное предупреждение (без значения заголовка). Ключи из `X-Seq-ApiKey` принимаются всегда и в логи не попадают.

**Защита от SSRF.** Когда URL приходит из заголовка, для исходящего HTTP-соединения активируется фильтр на уровне TCP-connect:

- блокируется loopback (`127.0.0.0/8`, `::1`) — всегда;
- блокируется link-local (`169.254.0.0/16` включая AWS IMDS `169.254.169.254`, `fe80::/10`) — всегда;
- блокируется RFC1918 (`10/8`, `172.16/12`, `192.168/16`) — только при `SEQ_BLOCK_PRIVATE_HOSTS=true`.

DNS resolve выполняется на каждом подключении, что закрывает DNS-rebinding: даже если домен сначала указывает на публичный IP, а потом на loopback, второй коннект всё равно проверит резолвленный IP.

Валидация URL на уровне middleware (до фабрики соединений):

- схема только `http` или `https`;
- не должно быть credentials в URL (`user:pass@host`);
- не должно быть fragment-части (`#...`);
- не должно быть null-байтов и управляющих символов (CR/LF — анти-инъекция);
- невалидный URL → `400 Bad Request` (без эха значения заголовка в теле ответа).

**Требования к развёртыванию мульти-арендности:**

- HTTP-сервер MCP **без аутентификации публично не выкатывать**. Использовать reverse-proxy (Nginx, Caddy, Traefik) с TLS и аутентификацией клиента (mTLS, OAuth2, API gateway).
- На reverse-proxy включить **rate-limit** на эндпоинт `/mcp` — клиент с заголовком `X-Seq-Url` может попытаться сканировать внутреннюю сеть.
- Для публичных деплоев включать `SEQ_BLOCK_PRIVATE_HOSTS=true`, чтобы RFC1918-адреса (внутренняя сеть) тоже были недоступны.
- `X-Seq-ApiKey` логировать запрещено: API-ключ — это секрет, эквивалентный паролю.

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

## 🧷 Stdio mode (локальный exe)

Кроме HTTP/SSE сервера сервер можно собрать как самостоятельный exe со stdio-транспортом — MCP-клиент сам запускает процесс и общается с ним через stdin/stdout. Это удобно для локального использования с Claude Desktop, Cline и другими MCP-клиентами: один процесс = один Seq, API-ключ не покидает машину пользователя.

### Сборка

```bash
# Windows
dotnet publish src/SeqMcp.Stdio/SeqMcp.Stdio.csproj -c Release -r win-x64 -p:PublishSingleFile=true

# Linux
dotnet publish src/SeqMcp.Stdio/SeqMcp.Stdio.csproj -c Release -r linux-x64 -p:PublishSingleFile=true

# macOS
dotnet publish src/SeqMcp.Stdio/SeqMcp.Stdio.csproj -c Release -r osx-x64 -p:PublishSingleFile=true
```

Результат — один self-contained exe в `src/SeqMcp.Stdio/bin/Release/net9.0/<rid>/publish/`.

### Конфигурация MCP-клиента

Пример для Claude Desktop (`claude_desktop_config.json`):

```json
{
  "mcpServers": {
    "seq": {
      "command": "/path/to/SeqMcp.Stdio.exe",
      "env": {
        "SEQ_URL": "http://localhost:5341",
        "SEQ_API_KEY": "your-api-key-if-needed"
      }
    }
  }
}
```

Логи stdio-сервера идут в stderr (stdout зарезервирован под JSON-RPC), поэтому их видно в логах MCP-клиента и они не ломают протокол.

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

- **Unit тесты** (`SeqMcp.Tests`): ~180 тестов (всегда выполняются; включают `Skip`-помеченные интеграционные тесты, требующие живого Seq)
- **Stdio integration тесты** (`SeqMcp.Stdio.IntegrationTests`): 4 теста, поднимающие реальный stdio-процесс через `Process.Start` и проверяющие JSON-RPC handshake
- **Покрытие**: Scope filtering, Health Check, Signal Management, multi-tenancy (override URL/ApiKey), SSRF-фильтр, stdio handshake

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
