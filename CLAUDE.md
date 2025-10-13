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
