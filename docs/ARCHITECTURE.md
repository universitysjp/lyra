# Lyra Architecture

This document describes the architecture of the Lyra backend in detail: service responsibilities, data flow, event contracts, and infrastructure choices. For a high-level overview, see the main [`README.md`](../README.md); for coding conventions, see [`AGENTS.md`](../AGENTS.md).

## 1. Design Principles

- **Database-per-service:** Each service owns its own PostgreSQL database and schema. No service ever opens a `DbContext` against another service's database.
- **Local-first audio:** Audio files never leave the user's device. Only metadata (and derived text/vector data) is sent to the backend.
- **Event-driven enrichment:** Expensive or rate-limited operations (lyrics lookup, LLM calls, embedding generation) happen asynchronously via RabbitMQ, decoupled from the synchronous ingestion path.
- **User-scoped data access:** Every catalog and vector query is filtered by the authenticated `UserId`, so a user can only ever read or generate playlists from tracks they personally imported.

## 2. Service Catalog

### 2.1 `Lyra.Contracts` (Class Library)

Holds shared, immutable `record` definitions for cross-service contracts:

- `SongImportedEvent` — published by `Lyra.Catalog.Api` after a track batch is ingested.
- `SongEnrichedEvent` — published by `Lyra.Enrichment.Worker` after lyrics + mood/tag extraction completes.
- Common DTOs shared across API boundaries where appropriate.

No service-specific logic lives here — only contracts.

### 2.2 `Lyra.Gateway` (ASP.NET Core Web App + YARP)

- Single public entry point for the mobile client.
- Validates JWTs issued by `Lyra.Identity.Api`.
- Enforces rate limiting.
- Routes:
  - `/api/auth/*` → `Lyra.Identity.Api`
  - `/api/catalog/*` → `Lyra.Catalog.Api`
  - `/api/playlists/*` → `Lyra.PlaylistRag.Api`

### 2.3 `Lyra.Identity.Api` (ASP.NET Core Web API)

- Handles user registration and login.
- Issues JWTs containing `sub` / `userId` claims consumed by downstream services.
- Stores each user's encrypted AI-provider connection (provider, model, API key, optional OpenCode plan). Public AI settings management is exposed under `/api/auth/ai-connection`.
- Exposes a private gRPC service over HTTP/2 on port `5004` in Development. Trusted services call `lyra.identity.v1.UserAiConnectionService/GetAiConnection` with `x-lyra-internal-key`; the service is not routed by `Lyra.Gateway`. Non-Development deployments must configure an HTTPS gRPC endpoint.
- Database: `identity_db` (PostgreSQL).

### 2.4 `Lyra.Catalog.Api` (ASP.NET Core Web API)

- Owns the catalog pipeline entry point and `catalog_db`.
- **Centralized track store:** a global `tracks` table holds one row per unique track (`TrackFingerprint` unique), and a `user_tracks` link table maps each user to the tracks they own with `LocalTrackId` and `ImportedAt`. Enrichment columns live on the shared `tracks` row, so a track is enriched once for all users.
- `POST /api/catalog/tracks/batch`:
  - Accepts up to 500 metadata items, validates the whole batch, and collapses in-batch duplicates.
  - Computes a deterministic `TrackFingerprint` (SHA-256 of normalized `Title + Artist + DurationSeconds`) server-side.
  - Upserts global track rows, links only tracks the user has not imported before, and publishes one `SongImportedEvent` per newly linked track.
  - Writes tracks, links, and outbox messages in one EF Core transaction via the MassTransit EF Core transactional outbox, then returns `202 Accepted`.
- `GET /api/catalog/tracks`: paged, newest-first user library, served cache-aside from Redis.
- `GET /api/catalog/tracks/search?q=`: fuzzy title/artist search using PostgreSQL `pg_trgm` `similarity(...)`, ordered by score and always joined through `user_tracks`.
- Redis cache-aside:
  - Library pages are keyed `catalog:tracks:{userId}:v{version}:p{page}:s{pageSize}`.
  - Each import bumps the per-user `catalog:tracks:{userId}:ver` counter, invalidating every cached page with one write instead of key scanning.
  - Search results are never cached.
- Uses PostgreSQL `pg_trgm` GIN indexes on `tracks.Title` and `tracks.Artist` for fuzzy matching.
- Consumes `SongEnrichedEvent` in a later milestone to mark tracks as enriched and invalidate the relevant cache entries.
- Database: `catalog_db` (PostgreSQL).

### 2.5 `Lyra.Enrichment.Worker` (Background Worker Service)

- A stateless MassTransit consumer of `SongImportedEvent`.
- Configured with low concurrency (`PrefetchCount = 3`) and exponential retry to respect external API rate limits.
- Processing pipeline per message:
  1. **Lyrics lookup:** `GET https://lrclib.net/api/get` for plain lyrics; on `404`, fall back gracefully to metadata-only enrichment.
  2. **Mood/tag extraction:** Uses `Microsoft.Extensions.AI` with Structured Outputs (JSON schema) to extract `PrimaryMood`, `Themes` (list), and `EnergyLevel` (1–10) from available lyrics/metadata.
  3. **Publish:** Emits `SongEnrichedEvent` containing lyrics (if any), extracted tags, and a combined semantic summary string.
- Holds no persistent state of its own; all enrichment output flows downstream via events.

### 2.6 `Lyra.PlaylistRag.Api` (ASP.NET Core Web API)

- Consumes `SongEnrichedEvent`:
  - Generates vector embeddings via `Microsoft.Extensions.AI` (`IEmbeddingGenerator`) from the semantic summary.
  - Upserts embeddings into PostgreSQL using `pgvector`, with an `HNSW` cosine-distance index.
- `POST /api/playlists/generate`:
  1. Rewrites the user's natural-language prompt into metadata filters plus a semantic search query.
  2. Executes a `pgvector` similarity search strictly scoped to the authenticated `UserId`.
  3. Uses an LLM to select and sequence the top matching tracks into a cohesive playlist.
  4. Returns the user's `LocalTrackId`s (never server-side track IDs, since the client owns the audio files locally).
- Database: `vector_db` (PostgreSQL + `pgvector`).

## 3. Event Flow

```
Mobile Client
   │ POST /api/catalog/tracks/batch
   ▼
Lyra.Catalog.Api ──SongImportedEvent──▶ RabbitMQ ──▶ Lyra.Enrichment.Worker
   ▲                                                        │
   │                                                        │ SongEnrichedEvent
   │                                                        ▼
   └───────────── SongEnrichedEvent ◀── RabbitMQ ◀── Lyra.PlaylistRag.Api
                                                            │
                                               embeddings upserted into vector_db
```

- `Lyra.Catalog.Api` and `Lyra.PlaylistRag.Api` both consume `SongEnrichedEvent` independently (fan-out via separate queues/subscriptions), each updating its own database.

**Catalog ingestion and the outbox**

The catalog never publishes directly from the request thread while the transaction is open. `SongImportedEvent` messages are written to the MassTransit `OutboxMessage` tables inside the same `SaveChangesAsync` that persists `tracks` and `user_tracks`. MassTransit's outbox delivery service publishes them to RabbitMQ after the transaction commits and keeps retrying while the broker is unavailable. A broker outage therefore delays enrichment but never loses an import or a message.

**Catalog cache invalidation**

A successful batch import bumps the caller's `catalog:tracks:{userId}:ver` counter after the commit. Every cached library page embeds that version in its key, so one counter increment makes all of the user's pages unreachable without scanning or deleting individual keys. Fuzzy search bypasses the cache entirely. When the enrichment milestone starts flipping `tracks.IsEnriched`, it must bump the same per-user versions for every user linked to the enriched track.

## 4. Data Stores

| Database | Owning Service | Key Extensions / Indexes |
|---|---|---|
| `identity_db` | `Lyra.Identity.Api` | — |
| `catalog_db` | `Lyra.Catalog.Api` | `pg_trgm` (GIN index for fuzzy search) |
| `vector_db` | `Lyra.PlaylistRag.Api` | `vector` (pgvector, HNSW cosine distance index) |

All PostgreSQL databases run on PostgreSQL 17 using the `pgvector/pgvector:pg17` image so that `vector_db` has `pgvector` available out of the box; other databases simply don't enable the extension.

## 5. Infrastructure

- **RabbitMQ 4** via `MassTransit.RabbitMQ` v8.5.x (Apache 2.0) — the event backbone connecting `Lyra.Catalog.Api`, `Lyra.Enrichment.Worker`, and `Lyra.PlaylistRag.Api`.
- **Redis 7** via `Microsoft.Extensions.Caching.StackExchangeRedis` — cache-aside layer in `Lyra.Catalog.Api`.
- **`Microsoft.Extensions.AI`** — abstraction over chat/completion and embedding generation, configured for cloud providers (e.g. Gemini, OpenAI) rather than local inference, due to the 12 GB host RAM constraint.
- **YARP (`Yarp.ReverseProxy`)** — public reverse proxy and routing layer in `Lyra.Gateway`.
- **gRPC over HTTP/2** — private synchronous service-to-service RPC. Protobuf definitions live in `Lyra.Contracts/Protos`; Identity serves the AI-connection lookup on its internal gRPC endpoint, and callers authenticate with `InternalServices:ApiKey`. gRPC is deliberately excluded from Gateway routes.
- **Secrets & configuration** — all usernames, passwords, and connection strings live in the gitignored repository-root `.env` file (template: `.env.example`). Docker Compose consumes it natively (`POSTGRES_USER`, `POSTGRES_PASSWORD`, `RABBITMQ_DEFAULT_USER`, `RABBITMQ_DEFAULT_PASS`); .NET services load it at startup via `DotNetEnv`, mapping `Section__Key` variables (e.g. `ConnectionStrings__IdentityDb`, `RabbitMq__Password`) onto the standard .NET configuration system. `appsettings.json` contains no credentials, and the Catalog service fails fast at startup if RabbitMQ credentials are not configured.

## 6. Security Model

- **RS256 & JWKS Key Distribution:** Access tokens are signed by `Lyra.Identity.Api` using an asymmetric RSA key pair (RS256) embedding the `kid` header. The private key never leaves the Identity service. Public keys are discovered by downstream services via `/.well-known/openid-configuration` and `/.well-known/jwks.json`.
- **Shared Authentication Primitives:** Downstream APIs (`Lyra.Gateway`, `Lyra.Catalog.Api`, `Lyra.PlaylistRag.Api`) use `AddLyraJwtAuthentication()` from `Lyra.Contracts` to configure `JwtBearer` authentication with zero clock skew, strict lifetime checks, and issuer/audience validation.
- **Access Token Shape & Lifecycle:** Access tokens live 15 minutes and embed `sub`, `userId`, `email`, `jti`, `iss`, and `aud`. The `userId` claim is the single canonical source of truth for data ownership.
- **Refresh Token Rotation & Reuse Detection:**
  - Refresh tokens are 256-bit cryptographically secure random values with a 30-day lifetime.
  - They are stored hashed (SHA-256) in `identity_db` (`refresh_tokens` table) with revocation metadata.
  - On `/api/auth/refresh`, the presented token is validated and rotated: the old token is marked revoked with `ReplacedByTokenId`, and a new token pair is returned.
  - If an already revoked/rotated token is reused, the service detects token family compromise and immediately revokes all active refresh tokens for that user.
- **Defense in Depth & Header Stripping:** `Lyra.Gateway` removes client-supplied identity headers (`X-User-Id`, `X-UserId`, `userId`) and rate limits endpoints (strict fixed window for auth, token bucket for APIs). Downstream microservices resolve the authenticated user ID exclusively via `User.GetUserId()`.
- **Private gRPC:** internal gRPC uses a separate HTTP/2 listener and the `x-lyra-internal-key` metadata credential. Development may use loopback h2c; non-Development startup rejects a non-HTTPS gRPC endpoint. No Gateway route matches the gRPC service path.
- Playlists are generated and returned only in terms of `LocalTrackId`s — the backend never needs to resolve or return actual audio file locations.
