# Seq MCP Server

MCP-сервер для [Seq](https://datalust.co/seq) — подключает Claude и другие LLM-клиенты к структурированным логам, чтобы их можно было искать, агрегировать и анализировать через инструменты MCP.

![CI](https://github.com/weselow/seq-mcp-server/workflows/CI/badge.svg)
![Docker Build](https://github.com/weselow/seq-mcp-server/workflows/Docker%20Build%20and%20Push/badge.svg)
![Security](https://github.com/weselow/seq-mcp-server/workflows/Security%20and%20Code%20Quality/badge.svg)

> [English version](README-EN.md)

## Когда это нужно

- Подключить Claude Desktop к своему Seq и анализировать логи без копипаста — **сценарий 1 (stdio exe)**.
- Поднять общий MCP-сервер для команды или удалённого клиента — **сценарий 2 (Docker HTTP/SSE)**.
- Обслуживать несколько клиентов с разными Seq из одного сервера — **сценарий 3 (multi-tenant HTTP)**.
- Попробовать всё локально, не имея отдельного Seq — **локальная песочница (Docker Compose)**.

## Быстрый старт

### Сценарий 1 — Claude Desktop + локальный Seq (stdio)

Один пользователь, один Seq на машине или в локальной сети. API-ключ остаётся на машине пользователя, сеть не нужна.

1. Скачайте бинарь под вашу ОС со страницы [Releases](https://github.com/weselow/seq-mcp-server/releases/latest):
   - `seq-mcp-stdio-win-x64.exe`
   - `seq-mcp-stdio-linux-x64`
   - `seq-mcp-stdio-osx-x64`

   Linux/macOS: `chmod +x seq-mcp-stdio-*`.

2. Добавьте в `claude_desktop_config.json`:

   ```json
   {
     "mcpServers": {
       "seq": {
         "command": "/path/to/seq-mcp-stdio-...",
         "env": {
           "SEQ_URL": "http://localhost:5341",
           "SEQ_API_KEY": "your-api-key-if-needed"
         }
       }
     }
   }
   ```

3. Перезапустите Claude Desktop.

Логи stdio-процесса идут в stderr — Claude Desktop их подхватывает в собственные логи, JSON-RPC по stdout остаётся чистым.

### Сценарий 2 — Docker HTTP/SSE для команды

Один сервер, к которому подключаются разные клиенты по URL. Подходит для команды с общим Seq.

```bash
docker run -d --name seq-mcp -p 5555:5555 \
  -e SEQ_URL=http://your-seq:5341 \
  -e SEQ_API_KEY=your-api-key \
  ghcr.io/weselow/seq-mcp-server:latest

curl http://localhost:5555/health
```

Конфигурация Claude Desktop:

```json
{
  "mcpServers": {
    "seq": {
      "url": "http://localhost:5555/sse",
      "headers": {
        "X-Seq-Project-Scope": "MyWebApp"
      }
    }
  }
}
```

`url` — обязателен. `headers` — опциональны, фильтруют логи по проекту (см. [Scope-фильтрация](#scope-фильтрация)).

Сервер слушает два транспорта:

- `GET /sse` — Legacy SSE (Claude Desktop, классические MCP-клиенты).
- `POST /` — Streamable HTTP по [MCP 2025-03-26](https://spec.modelcontextprotocol.io/) (новые клиенты).

### Сценарий 3 — Multi-tenant HTTP

Один MCP-сервер обслуживает разные Seq — клиент передаёт целевой URL в заголовке `X-Seq-Url`, ключ в `X-Seq-ApiKey`. Подходит для SaaS, агрегаторов, внутренних платформ.

**Включается явно** — по умолчанию `X-Seq-Url` игнорируется:

```bash
docker run -d --name seq-mcp -p 5555:5555 \
  -e SEQ_ALLOW_URL_OVERRIDE=true \
  -e SEQ_BLOCK_PRIVATE_HOSTS=true \
  ghcr.io/weselow/seq-mcp-server:latest
```

Пример запроса (Streamable HTTP):

```bash
curl -X POST http://localhost:5555/ \
  -H "X-Seq-Url: https://tenant-a.seq.example.com" \
  -H "X-Seq-ApiKey: per-tenant-api-key" \
  -H "Content-Type: application/json" \
  -H "Accept: application/json, text/event-stream" \
  --data '{...MCP-запрос...}'
```

Перед production-выкаткой прочитайте раздел [Безопасность мульти-арендности](#безопасность-мульти-арендности): без reverse-proxy с аутентификацией и rate-limit мульти-арендность открывать в интернет нельзя.

### Локальная песочница (Seq + MCP в Docker Compose)

Чтобы попробовать MCP-сервер без отдельного Seq:

```bash
docker-compose up -d
curl http://localhost:5555/health   # MCP
open http://localhost:8080          # Seq UI
```

Compose поднимает Seq (UI на `8080`, ingestion на `5341`) и MCP-сервер (`5555`) в одной сети. Persistent volume для данных Seq. Остановить и стереть данные — `docker-compose down -v`.

## Конфигурация

### Переменные окружения

| Переменная | Назначение | Дефолт |
|---|---|---|
| `SEQ_URL` | URL Seq-сервера | `http://localhost:8080` |
| `SEQ_API_KEY` | API-ключ Seq | — |
| `SEQ_PROJECT_SCOPE` | Имя проекта для scope-фильтрации | — |
| `SEQ_SCOPE_FIELD` | Поле, по которому фильтруется проект | `Application` |
| `SEQ_ALLOW_URL_OVERRIDE` | Разрешить `X-Seq-Url` из запроса (multi-tenant) | `false` |
| `SEQ_BLOCK_PRIVATE_HOSTS` | Блокировать RFC1918 в исходящих соединениях | `false` |
| `PORT` | Порт HTTP-сервера | `5555` |

Дефолт `SEQ_URL = http://localhost:8080` рассчитан на встроенный `docker-compose.yml`, где Seq отдаёт UI на этом порту. Для standalone Seq на машине пользователя задавайте `SEQ_URL=http://localhost:5341` (стандартный ingestion-порт).

Совместимое имя `SEQ_SERVER_URL` принимается как синоним `SEQ_URL` — оставлено для обратной совместимости со старыми деплоями.

### Источники конфигурации

Каждое поле читается из своего источника по своему правилу — это сознательное решение под совместимость:

| Поле | Победитель | Логика |
|---|---|---|
| `Url`, `ApiKey` | env > appsettings | Env переопределяет файл — типичный сценарий контейнера |
| `ProjectScope`, `ScopeField` | appsettings > env | Файл фиксирует «зашитую» в образ конфигурацию проекта |
| HTTP-заголовки (`X-Seq-*`) | всегда выше env и appsettings | Per-request override |

### Scope-фильтрация

Если в один Seq пишут несколько проектов, можно автоматически добавлять условие `<scope-field> = '<project>'` ко всем запросам. Это экономит токены LLM и ускоряет анализ.

Источники, по приоритету:

1. HTTP-заголовки `X-Seq-Project-Scope` + `X-Seq-Scope-Field`.
2. `SEQ_PROJECT_SCOPE` + `SEQ_SCOPE_FIELD` (env).
3. `Seq:ProjectScope` + `Seq:ScopeField` (appsettings.json).

Пользовательский фильтр сцепляется через `and`:

```
пользовательский: Level = 'Error'
итоговый:         (Application = 'MyWebApp') and (Level = 'Error')
```

## Безопасность мульти-арендности

Применимо к **сценарию 3**.

### Что защищает сервер сам

- **Без флага `SEQ_ALLOW_URL_OVERRIDE=true`** заголовок `X-Seq-Url` игнорируется тихо, с однократным предупреждением в лог (без эха значения).
- **Валидация URL** на уровне промежуточного слоя: схема `http`/`https`, без credentials, без fragment, без null-байтов и CR/LF. Невалидный URL — `400 Bad Request`, значение заголовка в ответе не отражается.
- **Фильтр исходящих TCP-коннектов** при URL из заголовка:
  - loopback (`127.0.0.0/8`, `::1`) — заблокирован всегда;
  - link-local (`169.254.0.0/16`, включая AWS IMDS `169.254.169.254`, `fe80::/10`) — заблокирован всегда;
  - RFC1918 (`10/8`, `172.16/12`, `192.168/16`) — заблокирован при `SEQ_BLOCK_PRIVATE_HOSTS=true`.
- **DNS resolve на каждом коннекте** — закрывает DNS-rebinding: даже если домен сначала указывает на публичный IP, а потом на loopback, второй коннект проверит новый адрес.
- **`X-Seq-ApiKey` не логируется** ни в каком виде — это секрет, эквивалентный паролю.

### Что должен сделать оператор перед public-выкаткой

- Поставить reverse-proxy (Nginx, Caddy, Traefik) с TLS и аутентификацией клиента (mTLS, OAuth2, API gateway). Без аутентификации мульти-арендный MCP в интернет не выставляется.
- Включить rate-limit на endpoint `/` (Streamable HTTP) и `/sse` на стороне proxy — клиент с `X-Seq-Url` может пытаться сканировать внутреннюю сеть.
- Включить `SEQ_BLOCK_PRIVATE_HOSTS=true` — иначе RFC1918-адреса (внутренняя сеть VPC) останутся достижимы.

## Что предоставляет MCP-сервер

### Инструменты (7)

| Имя | Назначение | Ключевые параметры |
|---|---|---|
| `seq_search_events` | Поиск событий с фильтром Seq | `filter`, `limit` |
| `seq_list_signals` | Список сохранённых сигналов | — |
| `seq_execute_sql` | SQL-запрос к логам | `query` |
| `seq_create_signal` | Создать сигнал/алерт | `title`, `description`, `filter`, `isProtected` |
| `seq_update_signal` | Обновить сигнал | `signalId`, `title?`, `description?`, `filter?` |
| `seq_delete_signal` | Удалить сигнал | `signalId` |
| `seq_get_apps` | Приложения, пишущие в Seq, и количество событий | `limit` |

Все инструменты возвращают структурированный JSON. Описания и параметры — на русском.

### Ресурсы (9)

URI-схема `seq://`:

- `events/latest` — последние 50 событий, все уровни.
- `events/errors` — последние 50 ошибок (`Error` + `Fatal`).
- `events/warnings` — последние 50 предупреждений.
- `events/exceptions` — последние 50 событий с исключениями.
- `events/last-hour` — события за последний час, до 100.
- `events/today` — события за сегодня, до 200.
- `performance/slow` — операции с `Elapsed > 1000ms`, последние 50.
- `signals` — все сохранённые сигналы.
- `stats/summary` — агрегация событий за последний час по уровням (SQL).

### Промпты (8)

Шаблоны типовых задач анализа, на русском:

| Имя | Параметр | Что делает |
|---|---|---|
| `seq_analyze_errors` | `period` (1h/24h/7d) | Топ-5 ошибок, паттерны, рекомендации |
| `seq_top_exceptions` | `count` (10) | Группировка исключений |
| `seq_activity_summary` | `period` | Сводка по уровням логирования |
| `seq_check_signals` | — | Проверка активных сигналов |
| `seq_performance_check` | `period` | Медленные операции, проблемы |
| `seq_trace_request` | `requestId` | Трассировка по RequestId/CorrelationId |
| `seq_security_audit` | `period` | Аудит auth/unauthorized событий |
| `seq_daily_report` | — | Ежедневный отчёт |

## Health Check

`GET http://localhost:5555/health` — состояние MCP-сервера и доступность Seq.

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

При недоступности Seq — `503 Service Unavailable` с тем же телом, но `status: "unhealthy"` и описанием в `seqConnection.message`. Подходит для liveness/readiness Kubernetes и для Prometheus scrape.

## Production deployment

### docker-compose

```yaml
services:
  seq-mcp:
    image: ghcr.io/weselow/seq-mcp-server:latest
    ports:
      - "5555:5555"
    environment:
      - SEQ_URL=http://your-seq:5341
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
        limits: { cpus: '0.5', memory: 512M }
        reservations: { cpus: '0.25', memory: 256M }
```

### Kubernetes

```yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: seq-mcp-server
spec:
  replicas: 2
  selector: { matchLabels: { app: seq-mcp } }
  template:
    metadata: { labels: { app: seq-mcp } }
    spec:
      containers:
        - name: seq-mcp
          image: ghcr.io/weselow/seq-mcp-server:latest
          ports: [{ containerPort: 5555 }]
          env:
            - { name: SEQ_URL, value: "http://seq-service:5341" }
            - name: SEQ_API_KEY
              valueFrom:
                secretKeyRef: { name: seq-credentials, key: api-key }
          livenessProbe:
            httpGet: { path: /health, port: 5555 }
            initialDelaySeconds: 10
            periodSeconds: 30
          readinessProbe:
            httpGet: { path: /health, port: 5555 }
            initialDelaySeconds: 5
            periodSeconds: 10
          resources:
            limits: { cpu: 500m, memory: 512Mi }
            requests: { cpu: 250m, memory: 256Mi }
```

## Архитектура

- **Язык / runtime**: C# / .NET 9.
- **MCP**: спецификация 2025-03-26, HTTP/SSE и stdio JSON-RPC.
- **Проекты**:
  - `src/SeqMcp.Core` — общая библиотека (модели, сервисы, инструменты, ресурсы, промпты, опции).
  - `src/SeqMcp.Http` — ASP.NET Core веб-приложение, контейнер для Docker.
  - `src/SeqMcp.Stdio` — single-file CLI exe, локальный stdio-транспорт.
- **DI**: единая `ISeqConnectionFactory` (Singleton) с per-tenant парами `HttpClient` + `SeqConnection`, LRU-кэш, lease/refcount для безопасного выселения.
- **HTTP-клиент**: тюнингованный `SocketsHttpHandler` — pooled connections, gzip, без редиректов и cookie.
- **Логи stdio-режима**: строго в stderr; stdout зарезервирован под JSON-RPC.

```
src/
├── SeqMcp.Core/
│   ├── Configuration/   — SeqOptions, SeqRequestContext, SeqOptionsLoader
│   ├── Hosting/         — DI-расширения для MCP-примитивов
│   ├── Services/        — SeqApiClient, SeqConnectionFactory, HealthCheckService
│   ├── Tools/           — SeqTools
│   ├── Resources/       — SeqResources
│   ├── Prompts/         — SeqPrompts
│   └── Models/          — DTO
├── SeqMcp.Http/
│   ├── Middleware/      — SeqHeadersMiddleware, RequestLoggingMiddleware
│   └── Program.cs       — DI, транспорт MCP, /health
└── SeqMcp.Stdio/
    └── Program.cs       — точка входа stdio-сервера

tests/
├── SeqMcp.Tests/                    — unit-тесты Core и Http (xUnit + FluentAssertions + Moq)
└── SeqMcp.Stdio.IntegrationTests/   — stdio через Process.Start, JSON-RPC handshake
```

## Сборка из исходников

### Требования

- .NET 9 SDK.
- Опционально: Docker (для образа), запущенный Seq (для integration-тестов).

### Stdio exe

```bash
dotnet publish src/SeqMcp.Stdio/SeqMcp.Stdio.csproj \
  -c Release -r <rid> -p:PublishSingleFile=true -p:SelfContained=true
# <rid>: win-x64 | linux-x64 | osx-x64
```

Результат — single-file бинарь в `src/SeqMcp.Stdio/bin/Release/net9.0/<rid>/publish/`.

### HTTP-сервер

```bash
dotnet publish src/SeqMcp.Http/SeqMcp.Http.csproj -c Release -o ./publish
SEQ_URL=http://localhost:5341 dotnet ./publish/SeqMcp.Http.dll
```

### Docker образ

```bash
docker build -t seq-mcp-server:local .
docker run -d -p 5555:5555 -e SEQ_URL=http://your-seq:5341 seq-mcp-server:local
```

### Тесты

```bash
dotnet test                                                    # все 172 теста
dotnet test --collect:"XPlat Code Coverage"                    # с покрытием
dotnet test --filter "FullyQualifiedName~SeqToolsTests"        # один класс
```

172 теста = 169 unit + 3 stdio integration. Часть тестов помечена `Skip` и требует живой Seq на `http://localhost:5341`. Подробности — [docs/INTEGRATION_TESTS.md](docs/INTEGRATION_TESTS.md).

## Разработка

Проект придерживается TDD по жёстким стандартам:

- [docs/standards/GLOBAL-implementation-standard.md](docs/standards/GLOBAL-implementation-standard.md) — общие принципы.
- [docs/standards/tdd-standard.md](docs/standards/tdd-standard.md) — цикл RED → GREEN → REFACTOR.

Правила:

1. Тесты пишутся первыми. Никогда не править тест ради компиляции — править код под тест.
2. Функции < 30 строк, цикломатическая сложность < 10, классы < 200 строк.
3. Покрытие методов > 60%.
4. Conventional commits в сообщениях.

CI/CD: три workflow — CI (build/test/lint), Docker (build/push в GHCR), Security (CodeQL + проверка зависимостей). Документация: [docs/CICD.md](docs/CICD.md).

## Зависимости

- `ModelContextProtocol` / `ModelContextProtocol.AspNetCore` 0.4.0-preview.2 — MCP SDK.
- `Seq.Api` 2025.2.2 — клиент Seq для signals и SQL.
- `Microsoft.Extensions.Hosting` / `Logging` 9.0.9.
- xUnit 2.9, FluentAssertions 8, Moq 4 — тесты.

## Лицензия

MIT.

## Ссылки

- [Спецификация Model Context Protocol](https://spec.modelcontextprotocol.io/)
- [Документация Seq](https://docs.datalust.co/docs)
- [ModelContextProtocol C# SDK](https://github.com/modelcontextprotocol/csharp-sdk)
