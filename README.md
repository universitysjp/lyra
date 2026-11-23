# Lyra

**Lyra** is an event-driven .NET microservices backend for an AI-powered local music mobile application. Audio files never leave the user's device — the Flutter mobile client extracts ID3 metadata from locally stored tracks and syncs it in batches to Lyra, which enriches each track asynchronously (lyrics, mood/theme tagging, vector embeddings) and exposes a Retrieval-Augmented Generation (RAG) endpoint for generating custom playlists from the user's own catalog.

## Table of Contents

- [Core Concept](#core-concept)
- [Architecture](#architecture)
- [Services](#services)
- [Technology Stack](#technology-stack)
- [Solution Layout](#solution-layout)
- [Getting Started](#getting-started)
- [Contributing](#contributing)
- [Documentation](#documentation)
- [License](#license)

## Core Concept

1. The Flutter mobile app reads local audio files and extracts ID3 metadata (title, artist, duration, etc.) — **no audio data is ever uploaded**.
2. Metadata is synced in batches to `Lyra.Catalog.Api`, deduplicated via a deterministic `TrackFingerprint` (SHA-256 of normalized `Title + Artist + DurationSeconds`), and stored for fast retrieval.
3. A `SongImportedEvent` is published to RabbitMQ, triggering asynchronous enrichment by `Lyra.Enrichment.Worker`:
   - Fetch lyrics from [LRCLIB](https://lrclib.net/).
   - Categorize mood, themes, and energy level via an LLM using `Microsoft.Extensions.AI`.
4. A `SongEnrichedEvent` is published, consumed by `Lyra.PlaylistRag.Api`, which generates vector embeddings and stores them in PostgreSQL (`pgvector`).
5. Users can prompt the RAG endpoint (`POST /api/playlists/generate`) to generate custom playlists drawn exclusively from their own local catalog.

## Architecture

Lyra follows a **database-per-service** microservices architecture with asynchronous, event-driven communication via RabbitMQ (MassTransit), synchronous public request routing via a YARP API gateway, and authenticated private service-to-service gRPC over HTTP/2.

```
                         ┌──────────────────┐
 Flutter Mobile Client ─▶│   Lyra.Gateway    │  (YARP reverse proxy, JWT, rate limiting)
                         └─────────┬─────────┘
                 ┌─────────────────┼─────────────────────┐
                 ▼                 ▼                      ▼
       Lyra.Identity.Api   Lyra.Catalog.Api      Lyra.PlaylistRag.Api
        (identity_db)        (catalog_db)             (vector_db)
                                   │   ▲                    ▲
                        SongImportedEvent │          SongEnrichedEvent
                                   ▼   │                    │
                           Lyra.Enrichment.Worker ──────────┘
                         (LRCLIB + LLM mood/tag extraction)

               RabbitMQ (MassTransit)   ◀──── event backbone ────▶
               Redis                    ◀──── catalog cache-aside
```

See [`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md) for a detailed breakdown of each service, its responsibilities, and data contracts.

## Services

| Service | Type | Responsibility | Database |
|---|---|---|---|
| `Lyra.Contracts` | Class Library | Shared event/DTO definitions and private gRPC protobuf contracts (`SongImportedEvent`, `SongEnrichedEvent`) | — |
| `Lyra.Gateway` | ASP.NET Core + YARP | Single entry point; JWT validation, rate limiting, routing | — |
| `Lyra.Identity.Api` | ASP.NET Core Web API | Registration, login, JWT issuance | `identity_db` |
| `Lyra.Catalog.Api` | ASP.NET Core Web API | Batch track ingestion, dedup, fuzzy search, Redis cache-aside | `catalog_db` |
| `Lyra.Enrichment.Worker` | Background Worker Service | Lyrics fetch (LRCLIB) + LLM mood/theme/energy extraction | — |
| `Lyra.PlaylistRag.Api` | ASP.NET Core Web API | Embedding generation, `pgvector` similarity search, RAG playlist generation | `vector_db` |

## Technology Stack

- **Runtime:** .NET 10.0 (C# 14), ASP.NET Core Minimal APIs
- **Database:** PostgreSQL 17 (`pgvector/pgvector:pg17`) via `Npgsql.EntityFrameworkCore.PostgreSQL` and `Pgvector.EntityFrameworkCore`
- **Messaging:** RabbitMQ 4 via `MassTransit.RabbitMQ` v8.5.x (Apache 2.0 — do **not** upgrade to the commercial v9 line)
- **Caching:** Redis 7 via `Microsoft.Extensions.Caching.StackExchangeRedis`
- **AI:** `Microsoft.Extensions.AI`, backed by cloud providers (e.g. Gemini/OpenAI) — no local Ollama inference, due to host RAM constraints
- **Gateway:** `Yarp.ReverseProxy`

## Solution Layout

```
Lyra.slnx
Lyra.Contracts/
Lyra.Gateway/
Lyra.Identity.Api/
Lyra.Catalog.Api/
Lyra.Enrichment.Worker/
Lyra.PlaylistRag.Api/
tests/
  Lyra.Identity.Api.Tests/
  Lyra.Catalog.Api.Tests/
docs/
  ARCHITECTURE.md
docker-compose.yml
.env.example        # secrets template; copy to .env (gitignored)
AGENTS.md
README.md
CONTRIBUTING.md
CHANGELOG.md
```

> **Note:** Lyra is in early scaffolding. Projects are added incrementally to `Lyra.slnx`; see [`CHANGELOG.md`](CHANGELOG.md) for current progress.

## Getting Started

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Docker](https://www.docker.com/) & Docker Compose (for PostgreSQL, RabbitMQ, Redis)
- An API key for your chosen `Microsoft.Extensions.AI` provider (e.g. Gemini or OpenAI)

### Clone, Restore & Configure Secrets

```bash
git clone <repository-url>
cd Lyra
dotnet restore Lyra.slnx
cp .env.example .env   # then edit .env with your local credentials
```

All usernames, passwords, and connection strings live in the repository-root `.env` file, which is **gitignored** (never commit it). `.env.example` is the committed template documenting every variable. Docker Compose reads `.env` automatically, and `Lyra.Identity.Api` / `Lyra.Catalog.Api` load it at startup via [DotNetEnv](https://www.nuget.org/packages/DotNetEnv), mapping `Section__Key` variables (e.g. `ConnectionStrings__IdentityDb`, `RabbitMq__Password`) onto standard .NET configuration. No credentials are stored in `appsettings*.json`.

### Run Infrastructure Dependencies

```bash
docker compose up -d postgres rabbitmq redis
```

### Build & Test

```bash
dotnet build Lyra.slnx
dotnet test Lyra.slnx
```

### Run a Service

```bash
dotnet run --project Lyra.Catalog.Api
```

### Catalog API

All catalog endpoints require a bearer token issued by `Lyra.Identity.Api`. Through the gateway, call `http://localhost:5000/api/catalog/...`.

| Method | Endpoint | Description |
|---|---|---|
| `POST` | `/api/catalog/tracks/batch` | Import up to 500 local track metadata items. Returns `202` with `submitted`/`imported`/`duplicates` counts. |
| `GET` | `/api/catalog/tracks` | Paged library (`page`, `pageSize`; defaults `1`/`50`, max page size `200`), newest first, served from Redis cache-aside. |
| `GET` | `/api/catalog/tracks/search?q=` | Fuzzy title/artist search via `pg_trgm`, ordered by similarity. Blank `q` returns `400`. |

Batch imports are deduplicated by a server-computed `TrackFingerprint`. A global `tracks` table stores unique tracks, while `user_tracks` links users to what they imported, so repeated syncs never duplicate a library. One `SongImportedEvent` is published per newly linked track through the MassTransit EF Core outbox.

## Contributing

Contributions are welcome. Please read [`CONTRIBUTING.md`](CONTRIBUTING.md) before opening a pull request.

## Documentation

- [`AGENTS.md`](AGENTS.md) — conventions and guardrails for AI coding agents working in this repository.
- [`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md) — detailed service/event architecture.
- [`CONTRIBUTING.md`](CONTRIBUTING.md) — contribution workflow and coding standards.
- [`CHANGELOG.md`](CHANGELOG.md) — notable changes across versions.

## License

See [`LICENSE`](LICENSE).
