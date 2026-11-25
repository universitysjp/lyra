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
   - Fetch plain and synced lyrics from [LRCLIB](https://lrclib.net/). Metadata is normalized for the query only (file extensions and filename noise such as `(mp3.pm)` are stripped), then an exact lookup is attempted. If LRCLIB returns `404`, the v2 search endpoint supplies candidate metadata which is scored title-first (55% title, 30% duration, 15% artist) before the best qualifying candidate is fetched.
   - Store whatever lyrics were found **before** resolving the AI provider, so a missing/broken AI connection never loses successful lyrics.
   - Categorize mood, themes, and energy level via an LLM using `Microsoft.Extensions.AI` and the caller's encrypted, per-user AI provider connection.
4. A `SongEnrichedEvent` is published and consumed independently by `Lyra.Catalog.Api` (marks tracks enriched and invalidates linked users' cached libraries) and `Lyra.PlaylistRag.Api` (generates vector embeddings into PostgreSQL `pgvector`).
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
| `Lyra.Identity.Api` | ASP.NET Core Web API | Registration, login, JWT issuance, encrypted per-user AI provider connections, private gRPC lookup | `identity_db` |
| `Lyra.Catalog.Api` | ASP.NET Core Web API | Batch track ingestion, dedup, fuzzy search, Redis cache-aside, enrichment state | `catalog_db` |
| `Lyra.Enrichment.Worker` | Background Worker Service | Lyrics fetch (LRCLIB exact + fuzzy fallback) + LLM mood/theme/energy extraction | — |
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
Lyra.AI/                 # Shared Microsoft.Extensions.AI provider adapter
Lyra.Enrichment.Worker/
Lyra.PlaylistRag.Api/
prototype/lyra-player/   # React/Vite client used to exercise the backend
tests/
  Lyra.Identity.Api.Tests/
  Lyra.Catalog.Api.Tests/
  Lyra.AI.Tests/
  Lyra.Enrichment.Worker.Tests/
  Lyra.PlaylistRag.Api.Tests/
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
| `GET` | `/api/catalog/tracks/{trackId}/lyrics` | Plain and synced (LRC) lyrics for one owned track. Always scoped through the caller's `user_tracks` link; returns `404` for tracks the caller does not own. |

Batch imports are deduplicated by a server-computed `TrackFingerprint`. A global `tracks` table stores unique tracks, while `user_tracks` links users to what they imported, so repeated syncs never duplicate a library. One `SongImportedEvent` is published per newly linked track through the MassTransit EF Core outbox.

Once `Lyra.Enrichment.Worker` finds lyrics, it publishes `SongLyricsFetchedEvent` immediately. `Lyra.Catalog.Api` stores the plain/synced text plus LRCLIB's resolved title, artist, album, duration, and instrumental flag **without** marking the track enriched. When the AI step later succeeds, `SongEnrichedEvent` fills the mood/themes/energy/summary and flips `isEnriched` to `true`. The resolved metadata is stored separately from the original local ID3 values because downloaded/local metadata is often wrong; downstream playlist generation can use the canonical LRCLIB fields. A duplicate enrichment is ignored for data purposes but still invalidates cache so recovery is idempotent. Library responses expose `hasLyrics` / `hasSyncedLyrics` so the client knows what can be displayed without shipping lyrics in every page.

### AI Provider Configuration

Enrichment needs an LLM, and each user supplies their own provider credentials (BYOK). Keys are encrypted at rest in `identity_db`; they are never stored in `.env`, Redis, or returned by the API.

| Method | Endpoint | Description |
|---|---|---|
| `GET` | `/api/auth/ai-connection` | Returns non-secret metadata (`provider`, `model`, `openCodePlan`, timestamps). `404` when unconfigured. |
| `PUT` | `/api/auth/ai-connection` | Creates or replaces the connection with `provider`, `model`, `apiKey`, and optional `openCodePlan`. |
| `DELETE` | `/api/auth/ai-connection` | Removes the connection. |

Supported providers are `OpenAI`, `Gemini`, `OpenCode`, and `Anthropic`. The React client exposes these under **Settings → AI provider** (`/settings`). The Worker reads the caller's connection from Identity over the private gRPC endpoint using `InternalServices__ApiKey`, so both services must share the same key value.

For `OpenCode`, choose a model that is eligible for external clients on the selected plan. OpenCode free-tier chat models are not available to external clients such as Lyra, so a free model name fails enrichment even though the connection saves successfully.

### RAG Embeddings

Playlist generation uses a **deployment-wide embedding model**, separate from each user's chat connection:

```env
Rag__Provider=OpenCode
Rag__OpenCodePlan=Go
Rag__Model=text-embedding-3-small
Rag__Dimensions=1536
Rag__ApiKey=<your OpenCode Go key>
```

`text-embedding-3-small` produces 1536-dimensional vectors, matching the `vector(1536)` column and HNSW index in `vector_db`. The embedding key is used only by `Lyra.PlaylistRag.Api`; user BYOK connections remain for prompt rewriting and playlist selection. The service fails fast if the provider, model, dimensions, or key are invalid.

### Run the Enrichment Pipeline

Run Identity, Catalog, PlaylistRag, and the Worker together (RabbitMQ, PostgreSQL, and Redis must be up):

```bash
dotnet run --project Lyra.Identity.Api     # REST :5001, internal gRPC :5004
dotnet run --project Lyra.Catalog.Api      # REST :5002
dotnet run --project Lyra.PlaylistRag.Api  # REST :5003
dotnet run --project Lyra.Enrichment.Worker
```

Then:

1. Register/login through Identity and save an AI provider connection (`PUT /api/auth/ai-connection`) or use **Settings → AI provider** in the React client.
2. Import a track batch through Catalog.
3. Watch the Worker log for the lyrics lookup, `SongLyricsFetchedEvent` publish, LLM extraction, and `SongEnrichedEvent` publish.
4. Read the library again; the track is returned with `isEnriched: true` and cached pages were invalidated.

Changing `InternalServices__ApiKey` in `.env` requires restarting Identity (it reads the key at startup). The Worker reconnects automatically.

LRCLIB matching is configurable in `Lyra.Enrichment.Worker/appsettings.json` under `Lrclib`:

| Setting | Default | Purpose |
|---|---|---|
| `MaxFuzzyCandidates` | `25` | Safety cap on ranked candidates kept after scoring the complete v2 result set. |
| `MaxCandidateDetailFetches` | `3` | How many highest-scoring eligible candidates are fetched until one returns usable lyrics. |
| `FuzzyMatchThreshold` | `85` | Minimum combined score on the 0–100 scale. Candidates below it are never fetched. |
| `DurationToleranceSeconds` | `20` | Duration window used by the 30% duration component; lower it for stricter synced-lyric alignment. |
| `SuccessCacheMinutes` | `1440` | In-memory cache lifetime for successful lookups. |
| `NotFoundCacheMinutes` | `360` | In-memory cache lifetime for definitive not-found results. |

Transient failures (`408`, `429`, `5xx`, network timeouts) are never cached; they surface as `LrclibRequestException` so MassTransit retries them.

### Local Files and Lyrics in the React Client

The React prototype stores attached audio `File` objects in IndexedDB, keyed by the same `localTrackId` the importer generates. This survives page reloads, so library rows stay playable instead of reverting to **Attach file**. Use **Connect local files** on the library page to relink files imported before persistence existed; files are matched by name, size, and last-modified timestamp.

When a track is playing, the player exposes a lyrics button. Synced LRC lines highlight and auto-scroll with playback and can be clicked to seek; plain lyrics are rendered as static text. The panel fetches only the current track's lyrics from the dedicated endpoint.

### Generate a Playlist in the React Client

Open **Create playlist** (`/playlists`) after at least one track has been enriched. Enter a mood, activity, or kind of sound, choose a track count, and select **Build playlist**. The client calls `POST /api/playlists/generate` through the Gateway and renders the returned title, description, reasons, and tracks. Playback uses the caller's own local files, just like library playback.

Generation is intentionally strict: a missing AI provider returns `409`, an unindexed library returns `409`, and invalid model output returns `502` rather than a fallback playlist. Prompt rewrites and track selection use the signed-in user's BYOK chat connection; query embeddings use the deployment-wide `Rag__ApiKey`.

## Contributing

Contributions are welcome. Please read [`CONTRIBUTING.md`](CONTRIBUTING.md) before opening a pull request.

## Documentation

- [`AGENTS.md`](AGENTS.md) — conventions and guardrails for AI coding agents working in this repository.
- [`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md) — detailed service/event architecture.
- [`CONTRIBUTING.md`](CONTRIBUTING.md) — contribution workflow and coding standards.
- [`CHANGELOG.md`](CHANGELOG.md) — notable changes across versions.

## License

See [`LICENSE`](LICENSE).
