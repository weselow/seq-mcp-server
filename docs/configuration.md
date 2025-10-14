# Конфигурация MCP Сервера

> [English version](configuration-en.md)

## Источники конфигурации

Сервер поддерживает **3 источника конфигурации** с приоритетом (от высшего к низшему):

1. **Переменные окружения** (Environment Variables) - высший приоритет
2. **appsettings.json** - конфигурационный файл
3. **Значения по умолчанию** - встроенные в код

## Переменные окружения

### SEQ_URL или SEQ_SERVER_URL
URL адрес Seq сервера.

**По умолчанию:** `http://localhost:5341`

```bash
export SEQ_URL="http://localhost:5341"
# или
export SEQ_SERVER_URL="http://seq.example.com"
```

### SEQ_API_KEY
API ключ для доступа к Seq (если требуется аутентификация).

**По умолчанию:** пустая строка (без аутентификации)

```bash
export SEQ_API_KEY="your-secret-api-key"
```

### PORT
Порт на котором запустится MCP сервер.

**По умолчанию:** `5555`

```bash
export PORT="5555"
```

## appsettings.json

Файл конфигурации находится в `src/SeqMcp/appsettings.json`:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*",
  "McpServer": {
    "Port": 5555
  },
  "Seq": {
    "Url": "http://localhost:5341",
    "ApiKey": ""
  }
}
```

### Редактирование конфигурации

**Для изменения порта MCP сервера:**
```json
{
  "McpServer": {
    "Port": 7777  // Изменить на нужный порт
  }
}
```

**Для изменения Seq URL:**
```json
{
  "Seq": {
    "Url": "http://seq.mycompany.com",
    "ApiKey": "your-api-key-here"  // Необязательно
  }
}
```

## Использование с Claude Code

### HTTP тип (рекомендуется)

**Важно:** При использовании `"type": "http"` в `.mcp.json`:

1. **Сервер должен быть запущен ЗАРАНЕЕ** как отдельный процесс
2. **Переменные окружения НЕ передаются** через `env` в `.mcp.json`
3. **Конфигурация берётся из:**
   - Переменных окружения процесса сервера
   - appsettings.json

**Шаг 1: Запустить MCP сервер**

```bash
# Windows (PowerShell)
cd M:\repos\seq-mcp-server\publish
.\SeqMcp.exe

# Linux/macOS
cd /path/to/seq-mcp-server/publish
./SeqMcp
```

Или с переменными окружения:

```bash
# Windows (PowerShell)
$env:SEQ_URL="http://localhost:5341"
$env:SEQ_API_KEY="your-key"
$env:PORT="5555"
.\SeqMcp.exe

# Linux/macOS
SEQ_URL="http://localhost:5341" SEQ_API_KEY="your-key" PORT="5555" ./SeqMcp
```

**Шаг 2: Настроить `.mcp.json`**

```json
{
  "mcpServers": {
    "seq": {
      "type": "http",
      "url": "http://localhost:5555/sse"
    }
  }
}
```

### Альтернатива: Command тип (НЕ рекомендуется для HTTP)

Если использовать `"command"` вместо `"type": "http"`:

```json
{
  "mcpServers": {
    "seq": {
      "command": "M:\\repos\\seq-mcp-server\\publish\\SeqMcp.exe",
      "env": {
        "SEQ_URL": "http://localhost:8080",
        "SEQ_API_KEY": "your-key",
        "PORT": "5555"
      }
    }
  }
}
```

**Проблемы:**
- Claude Code запускает процесс при каждом запросе
- HTTP сервер не успевает стартовать
- Приводит к ошибкам подключения

**Решение:** Используйте `"type": "http"` как описано выше.

## Для разработки

### Использование appsettings.Development.json

Создайте `src/SeqMcp/appsettings.Development.json` для настроек разработки:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Debug",
      "Microsoft.AspNetCore": "Information"
    }
  },
  "McpServer": {
    "Port": 5556  // Другой порт для dev
  },
  "Seq": {
    "Url": "http://localhost:5341",  // Локальный Seq
    "ApiKey": ""
  }
}
```

Запуск в режиме разработки:

```bash
cd src/SeqMcp
dotnet run --environment Development
```

## Проверка конфигурации

При запуске сервер выводит используемую конфигурацию:

```
info: SeqMcp[0]
      Seq MCP Server starting...
info: SeqMcp[0]
      Server URL: http://localhost:5555
info: SeqMcp[0]
      Seq URL: http://localhost:5341
info: SeqMcp[0]
      Transport: HTTP/SSE
```

Проверьте что:
- ✅ `Server URL` соответствует порту в `.mcp.json`
- ✅ `Seq URL` указывает на ваш Seq сервер
- ✅ `Transport: HTTP/SSE` активен

## Troubleshooting

### Проблема: "Failed to reconnect to seq"

**Причина:** Сервер не запущен или порт не совпадает.

**Решение:**
1. Запустите сервер: `./publish/SeqMcp.exe`
2. Проверьте в логах порт: `Server URL: http://localhost:5555`
3. Убедитесь что в `.mcp.json` указан тот же порт: `"url": "http://localhost:5555/sse"`

### Проблема: Сервер не подключается к Seq

**Причина:** Неправильный Seq URL или API ключ.

**Решение:**
1. Проверьте Seq URL в логах: `Seq URL: http://localhost:5341`
2. Проверьте доступность Seq: `curl http://localhost:5341/api`
3. Если Seq требует аутентификацию, добавьте API ключ в `appsettings.json` или переменную `SEQ_API_KEY`

### Проблема: Порт уже используется

**Причина:** Другой процесс занял порт 5555.

**Решение:**

**Вариант 1: Изменить порт в appsettings.json**
```json
{
  "McpServer": {
    "Port": 6666  // Другой порт
  }
}
```

**Вариант 2: Использовать переменную окружения**
```bash
PORT=6666 ./SeqMcp
```

Не забудьте обновить `.mcp.json`:
```json
{
  "url": "http://localhost:6666/sse"
}
```

---

**См. также:**
- [Руководство по развёртыванию](deployment.md)
- [README](../README.md)
