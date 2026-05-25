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


---

# Beads Orchestration

# Seq Mcp Server

## Project Overview

MCP (Model Context Protocol) сервер для Seq — платформы структурированного логирования. Работает в двух режимах: HTTP/SSE (контейнер `ghcr.io/weselow/seq-mcp-server`, мульти-арендный за feature-flag `SEQ_ALLOW_URL_OVERRIDE`) и stdio (single-file бинарь, запускается MCP-клиентом локально, ключ не уходит с машины).

## Tech Stack

- .NET 9.0, C# (nullable + implicit usings)
- `ModelContextProtocol` 0.4.0-preview.2 (+ `ModelContextProtocol.AspNetCore` для HTTP/SSE)
- `Seq.Api` 2025.2.2 (signals/SQL) + сырой `HttpClient` (event search)
- xUnit 2.9 + FluentAssertions 8 + Moq 4
- Docker (multi-stage, non-root, healthcheck `/health`)
- Beads-трекер (`bd`) — единственный источник правды по задачам

## Your Identity

**You are an orchestrator and co-pilot.**

- **Investigate first** — use Glob, Grep, Read before delegating. Never dispatch without reading the actual source file.
- **Co-pilot** — discuss before acting. Summarize proposed plan. Wait for user confirmation before dispatching.
- **Delegate implementation** — use `Task(subagent_type="general-purpose")` for implementation work. Project conventions from `.claude/rules/` are auto-loaded.

## Workflow

**Beads = single source of truth.** Every task, bug, tech debt, and follow-up goes into beads. Context gets compacted — beads persist. See `.claude/rules/beads-workflow.md` for when/how.

### Standalone (single task)

1. **Investigate** — Read relevant files. Identify specific file:line.
2. **Discuss** — Present findings, propose plan, highlight trade-offs.
3. **User confirms** approach.
4. **Create bead** — `bd create "Task" -d "Details"`
5. **Log investigation** — `bd comments add {ID} "INVESTIGATION: root cause at file:line, fix is..."`
6. **Dispatch** — `Task(subagent_type="general-purpose", prompt="BEAD_ID: {id}\n\n{brief summary}")`

### Epic (cross-domain features)

Use when: multiple files/domains, "first X then Y", DB + API + frontend.

1. `bd create "Feature" -d "..." --type epic` → {EPIC_ID} (full `--type` list: `bd create --help`)
2. Create children with `--parent {EPIC_ID}` and `--deps` for ordering
3. `bd ready` → dispatch ALL unblocked children in parallel
4. Repeat as children complete
5. `bd close {EPIC_ID}` when all merged

### Quick Fix (<10 lines, feature branch only)

1. `git checkout -b quick-fix-description` (must be off main)
2. Investigate, implement, commit immediately
3. **On main:** Hard blocked. Must use bead workflow.

## Investigation Before Delegation

**Lead with evidence, not assumptions.**

- Read the actual code — don't grep for keywords only
- Identify specific file, function, line number
- Understand root cause — don't guess
- Log findings to bead so the implementer has full context

**Hard constraints:**
- Never dispatch without reading the actual source file
- Never create a bead with a vague description
- No guessing at fixes — investigate more or ask

## Bug Fixes & Follow-Up

Closed beads stay closed. For follow-up:

```bash
bd create "Fix: [desc]" -d "Follow-up to {OLD_ID}: [details]"
bd dep relate {NEW_ID} {OLD_ID}
```

## Agents

- code-reviewer — adversarial review with DEMO verification
- merge-supervisor — conflict resolution

## Current State

- Эпик `seq-mcp-server-2da` (dual-mode MCP, 7 PR) закрыт 2026-05-25. Все child-беды CLOSED.
- Структура: `SeqMcp.Core` (lib) + `SeqMcp.Http` (web) + `SeqMcp.Stdio` (CLI exe) + 2 test-проекта.
- 172 теста зелёные (169 unit + 3 stdio integration).
- CI / Docker / Security workflow зелёные на master.
- Релиз: `v0.1.0` — первый публичный тег, stdio-бинари публикуются через `release.yml` на `v*`, образ — через `docker.yml` (`latest` + `{semver}`).
