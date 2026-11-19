# AGENTS.md

This file defines conventions, guardrails, and context for AI coding agents (and human contributors) working on the **Lyra** codebase. Read this before generating or modifying any code in this repository.

## 1. Project Overview

Lyra is a backend microservices platform for an AI-powered local music mobile application.

- **Core Concept:** Audio files stay locally on the user's mobile device (Flutter client). The mobile app extracts ID3 metadata from local tracks and syncs it in batches to the backend.
- **Pipeline:** Incoming track metadata is stored for fast retrieval, then asynchronously enriched via RabbitMQ: fetching lyrics from LRCLIB, categorizing mood/themes/energy via an LLM, and generating vector embeddings stored in PostgreSQL (`pgvector`). Users can prompt a RAG endpoint to generate custom playlists from their own local catalog.

## 2. Solution & Microservices Architecture (Database-per-Service)

**Target Framework:** .NET 10.0 (C# 14) using Minimal APIs. Strict database-per-service isolation — **never** share `DbContext` instances or database schemas across services.

1. **`Lyra.Contracts`** (Class Library): Shared immutable `record` definitions for MassTransit events (`SongImportedEvent`, `SongEnrichedEvent`) and common DTOs.
2. **`Lyra.Gateway`** (ASP.NET Core Web App + YARP): Single entry point (`Yarp.ReverseProxy`). Validates JWTs, enforces rate limiting, and routes `/api/auth/*`, `/api/catalog/*`, and `/api/playlists/*` to downstream services.
3. **`Lyra.Identity.Api`** (ASP.NET Core Web API): User registration, login, and JWT issuance (with `sub` / `userId` claims). Database: `identity_db` (PostgreSQL).
4. **`Lyra.Catalog.Api`** (ASP.NET Core Web API):
   - Receives batch track metadata (`POST /api/catalog/tracks/batch`) with deterministic `TrackFingerprint` (SHA-256 of normalized `Title + Artist + DurationSeconds`) for deduplication.
   - Uses PostgreSQL `pg_trgm` GIN indexes for fast fuzzy search and **Redis** (`StackExchange.Redis`) for cache-aside user library reads.
   - Publishes `SongImportedEvent` via MassTransit and returns `202 Accepted`.
   - Consumes `SongEnrichedEvent` to update track enrichment status and invalidate Redis cache.
   - Database: `catalog_db` (PostgreSQL).
5. **`Lyra.Enrichment.Worker`** (Background Worker Service):
   - Stateless MassTransit consumer listening to `SongImportedEvent` (configured with low concurrency `PrefetchCount = 3` and exponential retry to respect external API rate limits).
   - Step 1: Query `https://lrclib.net/api/get` for plain lyrics (fallback gracefully to metadata-only if 404).
   - Step 2: Use `Microsoft.Extensions.AI` with Structured Outputs (JSON schema) to extract `PrimaryMood`, `Themes` (list), and `EnergyLevel` (1–10).
   - Step 3: Publish `SongEnrichedEvent` containing lyrics, extracted tags, and a combined semantic summary string.
6. **`Lyra.PlaylistRag.Api`** (ASP.NET Core Web API):
   - Consumes `SongEnrichedEvent`, generates vector embeddings via `Microsoft.Extensions.AI` (`IEmbeddingGenerator`), and upserts them into PostgreSQL with `pgvector` (`HNSW` cosine distance index).
   - Exposes `POST /api/playlists/generate`: rewrites user prompt into metadata filters + semantic search query, executes `pgvector` similarity search strictly scoped to `UserId`, and uses an LLM to select/sequence the top tracks into a cohesive playlist returning the user's `LocalTrackId`s.
   - Database: `vector_db` (PostgreSQL + `pgvector`).

## 3. Infrastructure & Approved Stack

- **Database:** PostgreSQL 17 (`pgvector/pgvector:pg17` Docker image) via `Npgsql.EntityFrameworkCore.PostgreSQL` & `Pgvector.EntityFrameworkCore`.
- **Message Broker:** RabbitMQ 4 via **`MassTransit.RabbitMQ` v8.x** (stay on Apache 2.0 v8.5.x; do **not** upgrade to commercial v9).
- **Caching:** Redis 7 via `Microsoft.Extensions.Caching.StackExchangeRedis`.
- **AI Abstraction:** `Microsoft.Extensions.AI` (configured via `appsettings.json` to use cloud APIs like Gemini/OpenAI — do **not** assume local Ollama due to 12 GB host RAM constraints).

Do not introduce alternative databases, brokers, caches, or AI abstraction libraries without explicit approval — this is a deliberately constrained, approved stack.

## 4. Coding Conventions & Rules

- Use **ASP.NET Core Minimal APIs** grouped via `MapGroup(...)` and clean service/repository interfaces registered with built-in DI.
- Use **Primary Constructors** for services and MassTransit `IConsumer<T>` classes.
- Use `async`/`await` with `CancellationToken` propagation on all I/O, EF Core queries, and `HttpClient` calls.
- Always filter Catalog and RAG vector queries by authenticated `UserId` so users only retrieve and generate playlists from tracks they own.
- When generating EF Core migrations or SQL scripts, include required Postgres extensions (`CREATE EXTENSION IF NOT EXISTS pg_trgm;` in `catalog_db`, `CREATE EXTENSION IF NOT EXISTS vector;` in `vector_db`).
- Keep responses concise and provide complete, compilable C# code with exact file paths inside the solution.
- Never share a `DbContext` or schema between services — each service owns its database exclusively.
- Place shared event/DTO `record` types only in `Lyra.Contracts`; shared authentication extensions and options (`AddLyraJwtAuthentication`) are also maintained here as an approved exception to ensure consistent token validation across services. Do not duplicate contract definitions inside individual services.
- Prefer `record` types for immutable DTOs and events; use classes only where mutable state or EF Core entity tracking requires it.

## 5. Repository Conventions

- Solution file: `Lyra.slnx` (new XML-based solution format). Add new projects to this file, not to a generated `.sln`.
- Source projects live under the repository root (`<ProjectName>/`), matching the project's namespace and assembly name exactly.
- Tests live under `tests/<ProjectName>.Tests/`, mirroring the corresponding project name.
- Each service owns a single `appsettings.json` / `appsettings.Development.json`; never hardcode connection strings, broker credentials, or AI provider API keys — keep all secrets in the gitignored repository-root `.env` file (template: `.env.example`). Docker Compose reads it directly; .NET services load it via `DotNetEnv` so `Section__Key` variables map onto the standard configuration system. Environment variables and user secrets remain valid overrides.
- Docker Compose definitions for local infrastructure (PostgreSQL instances, RabbitMQ, Redis) belong in `docker-compose.yml` at the repository root.

## 6. Testing Expectations

- Unit test business logic (fingerprinting, mood/tag extraction parsing, playlist ranking) in isolation.
- Integration-test MassTransit consumers using the in-memory test harness (`MassTransit.Testing`) rather than a live broker.
- Integration-test EF Core repositories against a real PostgreSQL instance (e.g. via Testcontainers) rather than mocking `DbContext`.
- Do not commit tests that depend on live external services (LRCLIB, LLM providers) — fake/stub these boundaries.

## 7. Agent Guardrails

- Do not assume any project exists until it has been created in the solution — verify current state of `Lyra.slnx` and the repository root before editing.
- Do not invent endpoints, event names, or fields not specified in this document or an explicit task description; ask for clarification instead of guessing when requirements are ambiguous.
- Always propagate `CancellationToken` through new async methods you introduce.
- When adding a new microservice, update `README.md`, `docs/ARCHITECTURE.md`, and this file's service list consistently.
