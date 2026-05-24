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

This is an MCP (Model Context Protocol) server for Seq, a structured logging platform. The server enables LLM applications to interact with Seq for querying logs, searching events, and retrieving structured log data.

## Architecture

**Note**: This repository is currently empty. Once implemented, this section should describe:
- Main entry point and server initialization
- Resource handlers for Seq log queries
- Tool implementations for log search and filtering
- Connection management to Seq API
- Configuration and authentication handling

## Development Commands

Once package.json is set up, common commands will likely include:
- `npm install` - Install dependencies
- `npm run build` - Build the TypeScript project
- `npm run dev` - Run in development mode with watch
- `npm test` - Run tests
- `npm run lint` - Run linting

## MCP Server Specifics

- MCP servers use stdio transport by default
- Follow the MCP SDK patterns for TypeScript
- Resources should expose Seq log streams and saved queries
- Tools should provide log search, filtering, and signal querying capabilities
- Prompts can help format log queries in Seq's query syntax

## Seq Integration

- Seq API endpoint configuration (typically http://localhost:5341 or production URL)
- API key authentication required for non-public Seq instances
- Use Seq HTTP API for queries: `/api/events`, `/api/signals`, etc.
- Support Seq query language for filtering events


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
