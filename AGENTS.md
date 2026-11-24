# AGENTS.md

This file defines conventions, guardrails, and context for AI coding agents (and human contributors) working on the **Lyra** codebase. Read this before generating or modifying any code in this repository.

## 0. Working agreement (read this first)

1. **Read the docs before you touch anything.** Start with `README.md`, the top of `CHANGELOG.md`, and `docs/ARCHITECTURE.md`; then read the document that matches the task (§8). Do not work from memory.
2. **Documentation ships in the same commit as the code.** A change that alters documented behaviour without updating the document is incomplete (§8).
3. **Architecture decisions get an ADR** in `docs/decisions/` before or with the change (§9). No rewriting accepted ADRs — supersede them.
4. **Log every change** in `CHANGELOG.md`, newest first, same commit.
5. **Preserve user changes.** Inspect the working tree before every write. Never reset, restore, stash-drop, or overwrite uncommitted work.

If a document and the code disagree, **the code is the fact and the document is the bug**: fix the document in the same change and log it.

## 1. Project Overview

Lyra is a backend microservices platform for an AI-powered local music mobile application.

- **Core Concept:** Audio files stay locally on the user's mobile device (Flutter client). The mobile app extracts ID3 metadata from local tracks and syncs it in batches to the backend.
- **Pipeline:** Incoming track metadata is stored for fast retrieval, then asynchronously enriched via RabbitMQ: fetching lyrics from LRCLIB, categorizing mood/themes/energy via an LLM, and generating vector embeddings stored in PostgreSQL (`pgvector`). Users can prompt a RAG endpoint to generate custom playlists from their own local catalog.

## 2. Solution & Microservices Architecture (Database-per-Service)

**Target Framework:** .NET 10.0 (C# 14) using Minimal APIs. Strict database-per-service isolation — **never** share `DbContext` instances or database schemas across services.

1. **`Lyra.Contracts`** (Class Library): Shared immutable `record` definitions for MassTransit events (`SongImportedEvent`, `SongLyricsFetchedEvent`, `SongEnrichedEvent`), common DTOs, and protobuf definitions for private service-to-service gRPC contracts.
2. **`Lyra.Gateway`** (ASP.NET Core Web App + YARP): Single entry point (`Yarp.ReverseProxy`). Validates JWTs, enforces rate limiting, and routes `/api/auth/*`, `/api/catalog/*`, and `/api/playlists/*` to downstream services.
3. **`Lyra.Identity.Api`** (ASP.NET Core Web API): User registration, login, JWT issuance (with `sub` / `userId` claims), encrypted per-user AI connections, and a private authenticated gRPC lookup service. Database: `identity_db` (PostgreSQL).
4. **`Lyra.Catalog.Api`** (ASP.NET Core Web API):
   - Receives batch track metadata (`POST /api/catalog/tracks/batch`) with deterministic `TrackFingerprint` (SHA-256 of normalized `Title + Artist + DurationSeconds`) for deduplication.
   - Uses PostgreSQL `pg_trgm` GIN indexes for fast fuzzy search and **Redis** (`StackExchange.Redis`) for cache-aside user library reads.
   - Publishes `SongImportedEvent` via MassTransit and returns `202 Accepted`.
   - Consumes `SongLyricsFetchedEvent` to store lyrics before AI completion, and `SongEnrichedEvent` to update enrichment status and invalidate Redis cache.
   - Database: `catalog_db` (PostgreSQL).
5. **`Lyra.Enrichment.Worker`** (Background Worker Service):
   - Stateless MassTransit consumer listening to `SongImportedEvent` (configured with low concurrency `PrefetchCount = 3` and exponential retry to respect external API rate limits).
   - Step 1: Query `https://lrclib.net/api/get` for plain and synced lyrics; on 404, use `/api/v2/search` with 0–100 scoring (title 55%, duration 30%, artist 15%), a hard `FuzzyMatchThreshold`, and detail fetches for the best eligible candidates. Normalize filename noise for the query only.
   - Step 2: Publish `SongLyricsFetchedEvent` immediately when lyrics are found, before resolving the user's AI connection, so AI failures cannot lose them; include the selected LRCLIB record's canonical metadata.
   - Step 3: Use `Microsoft.Extensions.AI` with Structured Outputs (JSON schema) to extract `PrimaryMood`, `Themes` (list), and `EnergyLevel` (1–10).
   - Step 4: Publish `SongEnrichedEvent` containing both lyric forms, resolved metadata, extracted tags, and a combined semantic summary string.
6. **`Lyra.PlaylistRag.Api`** (ASP.NET Core Web API):
   - Consumes `SongEnrichedEvent`, generates vector embeddings via `Microsoft.Extensions.AI` (`IEmbeddingGenerator`), and upserts them into PostgreSQL with `pgvector` (`HNSW` cosine distance index).
   - Exposes `POST /api/playlists/generate`: rewrites user prompt into metadata filters + semantic search query, executes `pgvector` similarity search strictly scoped to `UserId`, and uses an LLM to select/sequence the top tracks into a cohesive playlist returning the user's `LocalTrackId`s.
   - Database: `vector_db` (PostgreSQL + `pgvector`).

## 3. Infrastructure & Approved Stack

- **Database:** PostgreSQL 17 (`pgvector/pgvector:pg17` Docker image) via `Npgsql.EntityFrameworkCore.PostgreSQL` & `Pgvector.EntityFrameworkCore`.
- **Message Broker:** RabbitMQ 4 via **`MassTransit.RabbitMQ` v8.x** (stay on Apache 2.0 v8.5.x; do **not** upgrade to commercial v9).
- **Private service-to-service RPC:** gRPC over HTTP/2 via `Grpc.AspNetCore`/`Grpc.Net.Client`. Internal services use a separate non-Gateway endpoint authenticated with `InternalServices:ApiKey`; non-Development deployments must use TLS.
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
- Clients that are not .NET services (the React dev client today, the Flutter app later) live under `clients/<client-name>/`. Never under `prototype/` — that name is reserved for throwaway spikes and signals "safe to delete".
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
- Never discard uncommitted work. Before editing, read the file and its `git diff`; make narrow patches; recheck `git status` before each write and stop to ask if an edit would conflict with newly appeared work.
- Do not commit changes you did not author (for example a pre-existing `LICENSE` rename) unless the task explicitly asks for it.

## 8. Documentation-first workflow

`docs/ARCHITECTURE.md` is the detailed description of the backend; `README.md` is the public overview; `CHANGELOG.md` is the change log. Before any task:

1. Read `README.md`, the newest rows of `CHANGELOG.md`, and `docs/ARCHITECTURE.md`.
2. Read the specific document for the task from the table below.
3. If the behaviour you need is not described anywhere, describe it first (docs or ADR), then implement.

| Task | Read / update |
| --- | --- |
| Any change at all | `README.md`, `docs/ARCHITECTURE.md`, `AGENTS.md` |
| New or changed endpoint, event, or DB schema | `docs/ARCHITECTURE.md` (service catalog + data flow), and the service README row |
| New microservice or project | `README.md` (services + layout), `docs/ARCHITECTURE.md`, this file §2 |
| New config key, env var, or secret | `.env.example`, the owning `appsettings.json`, `docs/ARCHITECTURE.md` if it changes deployment |
| New contract record in `Lyra.Contracts` | `docs/ARCHITECTURE.md` (event flow), this file §4 |
| Gateway route, auth, rate limit change | `docs/ARCHITECTURE.md` §Gateway, this file §3 |
| Local infrastructure / compose change | `README.md` getting started, `docs/ARCHITECTURE.md` |
| A design question ("why is X like this?") | `docs/decisions/README.md` |
| Something nobody has decided yet | write a `Proposed` ADR, then ask |

Rules:

- Documentation updates land **in the same commit** as the code they describe.
- Anything that was documented as "planned" and is now built replaces the planned text.
- Docs never claim a feature exists when the project is still a stub. Say "planned" explicitly.

## 9. Architecture decision records

Every decision that is expensive to reverse — service boundaries, data ownership, message contracts, external dependencies, security posture, deployment topology, repository layout — needs an ADR in `docs/decisions/`.

- File name: `ADR-nnnn-short-slug.md`, next free number.
- Required sections: **Status / Date / Deciders / Context / Decision / Consequences / Alternatives considered**.
- Statuses: `Proposed`, `Accepted`, `Rejected`, `Superseded by ADR-nnnn`.
- Add a row to `docs/decisions/README.md` in the same commit.
- Never edit an accepted ADR's decision. Write a new ADR that supersedes it and update the old one's status line only.
- If the decision is not confirmed by the owner, mark it `Proposed` and say what confirmation is needed.
- Template: `docs/decisions/ADR-template.md`.

## 10. Change flow and commits

1. Describe the intended behaviour (docs or ADR) if it is missing.
2. Implement in a narrow, reviewable change.
3. Verify: `dotnet build Lyra.slnx` (warnings are errors), plus `dotnet test tests/<affected>` / `bun run check && bun run build` in `clients/lyra-player` when touched.
4. Update docs (§8), add a CHANGELOG row, and add an ADR when §9 applies — all in the same commit.
5. Commit subject: `<type>: <lowercase summary>` with `add:`, `fix:`, or `update:` (§11).
6. Push to a branch and open a pull request to `main`. `main` is deployable; the owner merges.

CI must stay green. A pull request that changes code without a `CHANGELOG.md` row is incomplete.

## 11. Commit messages

`<type>: <short summary>` — lowercase type, colon, imperative summary, no trailing period.

| Type | Use for |
| --- | --- |
| `add:` | a new feature, project, dependency, or capability |
| `fix:` | a defect or broken behaviour |
| `update:` | documentation, configuration, or behaviour change that is neither of the above |

One logical batch per commit. Documentation and the CHANGELOG row belong to the same commit as the code.
