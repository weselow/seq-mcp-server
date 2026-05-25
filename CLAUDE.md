# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## PERSISTENT_RULES:

At start read ALL project standards files. This is MANDATORY:

**Core Standards (READ FIRST):**
- @docs/standards/GLOBAL-implementation-standard.md

**Additional Standards (READ ALWAYS):**
- @docs/standards/tdd-standard.md

CRITICAL: After EVERY summarize or compacting conversation, you MUST:
1. Show message " == SUMMARIZE IS COMPLETED =="
2. Reload ALL standards files listed above
3. Focus on development rules, standards, dependency management, and code changes during compact


## Project Overview

MCP (Model Context Protocol) сервер для Seq — платформы структурированного логирования. Поддерживает два режима работы:

- **HTTP/SSE сервер** (Docker) — мульти-арендный сервис, который через заголовки `X-Seq-Url`/`X-Seq-ApiKey` может обслуживать несколько Seq-таргетов (отключено по умолчанию, защита от SSRF).
- **Stdio CLI exe** — single-file бинарь, который MCP-клиент сам запускает локально; один процесс = один Seq, API-ключ не покидает машину клиента.

## Architecture

Проекты:
- `src/SeqMcp.Core` — общая библиотека (Microsoft.NET.Sdk): Models, Configuration, Services, Tools, Resources, Prompts, Hosting helpers.
- `src/SeqMcp.Http` — ASP.NET Core веб-приложение (Microsoft.NET.Sdk.Web): Program.cs, Middleware (`SeqHeadersMiddleware`, `RequestLoggingMiddleware`), appsettings.
- `src/SeqMcp.Stdio` — single-file CLI exe (Microsoft.NET.Sdk): Program.cs с `Host.CreateApplicationBuilder` и stdio-транспортом.
- `tests/SeqMcp.Tests` — unit/integration тесты для Core+Http.
- `tests/SeqMcp.Stdio.IntegrationTests` — integration через `Process.Start`.

Ключевые компоненты:
- `SeqOptions` + `AddSeqOptions(IConfiguration)` — compat-загрузчик env/appsettings с per-field таблицей приоритетов.
- `ISeqConnectionFactory` (Singleton, IAsyncDisposable) — единственный владелец `HttpClient`/`SeqConnection`/`SocketsHttpHandler`; LRU + sliding TTL + lease/refcount + grace period для безопасного выселения.
- `SeqEndpoint(Url, ApiKey?, TrustMode)` — `TrustMode` в ключе кэша, SSRF `ConnectCallback` навешан только на `HeaderOverride`.

## Development Commands

```bash
# Сборка
dotnet build SeqMcp.sln -c Release

# Unit + middleware тесты
dotnet test tests/SeqMcp.Tests/SeqMcp.Tests.csproj

# Stdio integration тесты (через Process.Start)
dotnet test tests/SeqMcp.Stdio.IntegrationTests/SeqMcp.Stdio.IntegrationTests.csproj

# Форматирование
dotnet format SeqMcp.sln --verify-no-changes

# Single-file stdio publish
dotnet publish src/SeqMcp.Stdio -c Release -r win-x64 -p:PublishSingleFile=true -p:SelfContained=true
```

## Seq Integration

- Seq URL/API key читаются из `SeqOptions` (env `SEQ_URL`/`SEQ_API_KEY` или `Seq:Url`/`Seq:ApiKey` в appsettings).
- HTTP-режим поддерживает scope-фильтрацию через `X-Seq-Project-Scope`/`X-Seq-Scope-Field` (или env `SEQ_PROJECT_SCOPE`/`SEQ_SCOPE_FIELD`).
- Мульти-арендность включается через `SEQ_ALLOW_URL_OVERRIDE=true` — после чего заголовки `X-Seq-Url`/`X-Seq-ApiKey` маршрутизируют запрос на нужный Seq. По умолчанию выключена — поверхности атаки нет.


<!-- BEGIN BEADS INTEGRATION v:1 profile:minimal hash:ca08a54f -->
## Beads Issue Tracker

This project uses **bd (beads)** for issue tracking. Run `bd prime` to see full workflow context and commands.

### Quick Reference

```bash
bd ready              # Find available work
bd show <id>          # View issue details
bd update <id> --claim  # Claim work
bd close <id>         # Complete work
```

### Rules

- Use `bd` for ALL task tracking — do NOT use TodoWrite, TaskCreate, or markdown TODO lists
- Run `bd prime` for detailed command reference and session close protocol
- Use `bd remember` for persistent knowledge — do NOT use MEMORY.md files

## Session Completion

**When ending a work session**, you MUST complete ALL steps below. Work is NOT complete until `git push` succeeds.

**MANDATORY WORKFLOW:**

1. **File issues for remaining work** - Create issues for anything that needs follow-up
2. **Run quality gates** (if code changed) - Tests, linters, builds
3. **Update issue status** - Close finished work, update in-progress items
4. **PUSH TO REMOTE** - This is MANDATORY:
   ```bash
   git pull --rebase
   bd dolt push
   git push
   git status  # MUST show "up to date with origin"
   ```
5. **Clean up** - Clear stashes, prune remote branches
6. **Verify** - All changes committed AND pushed
7. **Hand off** - Provide context for next session

**CRITICAL RULES:**
- Work is NOT complete until `git push` succeeds
- NEVER stop before pushing - that leaves work stranded locally
- NEVER say "ready to push when you are" - YOU must push
- If push fails, resolve and retry until it succeeds
<!-- END BEADS INTEGRATION -->
