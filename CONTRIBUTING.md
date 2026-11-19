# Contributing to Lyra

Thank you for your interest in contributing to Lyra! This document explains how to propose changes, the coding standards we follow, and the review process.

## Table of Contents

- [Code of Conduct](#code-of-conduct)
- [Getting Started](#getting-started)
- [Development Workflow](#development-workflow)
- [Coding Standards](#coding-standards)
- [Commit Messages](#commit-messages)
- [Testing Requirements](#testing-requirements)
- [Pull Request Process](#pull-request-process)
- [Reporting Issues](#reporting-issues)

## Code of Conduct

Be respectful, constructive, and collaborative. Disagreements should focus on technical merit, not individuals.

## Getting Started

1. Fork the repository and clone your fork.
2. Install the [.NET 10 SDK](https://dotnet.microsoft.com/download).
3. Create your local secrets file from the template and adjust values if needed:
   ```bash
   cp .env.example .env
   ```
4. Start local infrastructure dependencies (PostgreSQL, RabbitMQ, Redis) via Docker Compose — it reads the same root `.env`:
   ```bash
   docker compose up -d
   ```
5. Restore and build the solution:
   ```bash
   dotnet restore Lyra.slnx
   dotnet build Lyra.slnx
   ```
6. Read [`AGENTS.md`](AGENTS.md) — it defines the approved technology stack, architectural rules, and coding conventions that apply to every contribution (human or AI-assisted).

## Development Workflow

1. Create a feature branch from `main`:
   ```bash
   git checkout -b feature/short-description
   ```
2. Make focused, incremental commits. Keep each pull request scoped to a single concern (one service, one feature, or one fix).
3. Respect the **database-per-service** boundary: never share a `DbContext`, connection string, or schema across services.
4. If you add or modify a MassTransit event contract, update it only in `Lyra.Contracts` and bump consumers/producers accordingly.
5. Keep secrets (database connection strings, broker credentials, AI provider keys) out of source control — all of them live in the gitignored repository-root `.env` file (template: `.env.example`). Docker Compose reads it natively; `Lyra.Identity.Api` and `Lyra.Catalog.Api` load it at startup via `DotNetEnv`, so `Section__Key` variables map onto the standard .NET configuration. Never commit `.env` or move credentials back into `appsettings*.json`; use user secrets or environment variables for machine-specific overrides.

## Coding Standards

- **Language/Framework:** .NET 10.0 (C# 14), ASP.NET Core Minimal APIs.
- **API style:** Group endpoints with `MapGroup(...)`; keep route handlers thin and delegate to services/repositories registered via built-in DI.
- **Constructors:** Use primary constructors for services and MassTransit `IConsumer<T>` classes.
- **Async:** All I/O (EF Core queries, `HttpClient` calls, MassTransit publish/consume) must be `async`/`await` with `CancellationToken` propagation.
- **Multi-tenancy:** Catalog and RAG queries must always filter by the authenticated `UserId`.
- **Migrations:** EF Core migrations/SQL scripts must declare required Postgres extensions explicitly (`pg_trgm` for `catalog_db`, `vector` for `vector_db`).
- **Contracts:** Shared events/DTOs live only in `Lyra.Contracts` as immutable `record` types.
- Run `dotnet format` before committing to ensure consistent styling.

## Commit Messages

Use short, imperative, present-tense summaries, optionally prefixed with the affected service or area:

```
Catalog.Api: add fuzzy search endpoint for track titles
Enrichment.Worker: retry LRCLIB lookups with exponential backoff
```

## Testing Requirements

- Add or update unit tests for new business logic (e.g. fingerprinting, playlist ranking, mood/tag parsing).
- Use `MassTransit.Testing`'s in-memory harness for consumer/producer tests — do not depend on a live broker.
- Use Testcontainers (or an equivalent) for EF Core repository integration tests against a real PostgreSQL instance.
- Do not write tests that call live external services (LRCLIB, LLM providers); stub or fake these boundaries.
- Before opening a pull request, run:
  ```bash
  dotnet build Lyra.slnx
  dotnet test Lyra.slnx
  ```

## Pull Request Process

1. Ensure your branch builds and all tests pass locally.
2. Fill out the PR description: what changed, why, and how it was tested.
3. Link any related issue.
4. Request review from a maintainer. Address review feedback with additional commits (avoid force-pushing during review unless requested).
5. A maintainer will squash-merge once the PR is approved and CI is green.

## Reporting Issues

When filing a bug report, include:

- The affected service (e.g. `Lyra.Catalog.Api`).
- Steps to reproduce, expected behavior, and actual behavior.
- Relevant logs or stack traces (redact secrets/API keys).

For feature requests, describe the use case and how it fits within the database-per-service, event-driven architecture described in [`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md).
