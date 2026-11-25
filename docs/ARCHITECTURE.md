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
- `SongLyricsFetchedEvent` — published by `Lyra.Enrichment.Worker` as soon as lyrics are found, before any AI provider call, so Catalog can persist them independently of AI success.
- `SongEnrichedEvent` — published by `Lyra.Enrichment.Worker` after lyrics + mood/tag extraction completes; includes the resolved LRCLIB metadata (`ResolvedTrackMetadata`).
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
- **Centralized track store:** a global `tracks` table holds one row per unique track (`TrackFingerprint` unique), and a `user_tracks` link table maps each user to the tracks they own with `LocalTrackId` and `ImportedAt`. Enrichment columns live on the shared `tracks` row, so a track is enriched once for all users. Resolved LRCLIB metadata is stored in separate `Resolved*` columns because the original local ID3 values may be unreliable; RAG playlist generation can prefer the canonical values.
- `POST /api/catalog/tracks/batch`:
  - Accepts up to 500 metadata items, validates the whole batch, and collapses in-batch duplicates.
  - Computes a deterministic `TrackFingerprint` (SHA-256 of normalized `Title + Artist + DurationSeconds`) server-side.
  - Upserts global track rows, links only tracks the user has not imported before, and publishes one `SongImportedEvent` per newly linked track.
  - Writes tracks, links, and outbox messages in one EF Core transaction via the MassTransit EF Core transactional outbox, then returns `202 Accepted`.
- `GET /api/catalog/tracks`: paged, newest-first user library, served cache-aside from Redis.
- `GET /api/catalog/tracks/search?q=`: fuzzy title/artist search using PostgreSQL `pg_trgm` `similarity(...)`, ordered by score and always joined through `user_tracks`.
- `GET /api/catalog/tracks/{trackId}/lyrics`: returns `IsEnriched`, `PlainLyrics`, and `SyncedLyrics` for one track the caller owns. A track owned by another user returns `404`.
- `TrackResponse` exposes `HasLyrics` / `HasSyncedLyrics` flags so list pages stay small while the player can fetch lyrics lazily.
- Redis cache-aside:
  - Library pages are keyed `catalog:tracks:{userId}:v{version}:p{page}:s{pageSize}`.
  - Each import bumps the per-user `catalog:tracks:{userId}:ver` counter, invalidating every cached page with one write instead of key scanning.
  - Search results are never cached.
- Uses PostgreSQL `pg_trgm` GIN indexes on `tracks.Title` and `tracks.Artist` for fuzzy matching.
- Consumes `SongLyricsFetchedEvent` to persist lyrics before AI enrichment completes:
  - Updates `Lyrics` and `SyncedLyrics` on the shared track row without changing `IsEnriched`, and stores the selected LRCLIB record's resolved title, artist, album, duration, and instrumental flag.
  - Preserves an existing non-null value when a later event carries only one form.
  - Invalidates the linked users' library caches so the new `HasLyrics` flags are visible.
- Consumes `SongEnrichedEvent` to apply the first successful global enrichment:
  - Validates the payload, writes `Lyrics`, `PrimaryMood`, `ThemesJson`, `EnergyLevel`, and `SemanticSummary` onto the shared track row, and sets `IsEnriched`.
  - The repository update is conditional on the track still being unenriched, so two users importing the same track cannot overwrite each other's accepted result. The consumer still saves its inbox row for duplicates.
  - Invalidates the Redis library version token of every user linked to that track, not just the user who triggered enrichment.
- Database: `catalog_db` (PostgreSQL).

### 2.5 `Lyra.Enrichment.Worker` (Background Worker Service)

- A stateless MassTransit consumer of `SongImportedEvent`.
- Configured with low concurrency (`PrefetchCount = 3`) and exponential retry to respect external API rate limits.
- Resolves the importing user's AI provider over Identity's private gRPC endpoint (`GetAiConnection`) and builds an `IChatClient` through `Lyra.AI`, **after** lyrics have been fetched and published.
- Processing pipeline per message:
  1. **Lyrics lookup (LRCLIB):**
     - Normalizes title/artist for the query only; stored metadata is untouched. File extensions (`.mp3`, `.flac`, `.wav`, …), filename/source noise such as `(mp3.pm)`, `[Official Video]`, `- Official Audio`, `(Remastered)`, repeated punctuation, and repeated whitespace are removed.
     - Calls `GET https://lrclib.net/api/get` with normalized title, artist, duration, and a reliable album name when present. A hit returns both plain and synced (LRC) lyrics plus the record's metadata immediately.
     - On `404` (or an exact record without lyrics), calls `GET https://lrclib.net/api/v2/search` with the normalized title/artist, scores every valid result, then keeps at most `MaxFuzzyCandidates` (default 25) top-ranked candidates for selection. The beta v2 endpoint returns metadata only (`duration`, `instrumental`, `hasWordSync`, `hasPlainLyrics`, `hasSyncedLyrics`, `hasLyricsfile`), so lyrics bodies are not downloaded during search.
     - Searches progressively: full artist, then the primary credited artist, then title-only. This handles local libraries where the artist field contains a download site, uploader, or incomplete multi-artist text.
     - Scores candidates on a 0–100 scale: **title 55%, duration 30%, artist 15%**. Title dominates because local artist metadata is unreliable. Missing/invalid duration scores zero rather than a neutral value.
     - Ranks by combined score, then title, duration, and artist similarity as deterministic tie-breakers. Candidates below `FuzzyMatchThreshold` (default `85`) are rejected and never fetched. `AmbiguityMargin` no longer exists; the highest-scoring acceptable candidate wins even when the runner-up is close.
     - Prefers candidates whose v2 flags indicate lyrics are available. Fetches at most `MaxCandidateDetailFetches` (default 3) highest-scoring eligible candidates through `GET /api/get/{id}` until one returns usable plain or synced lyrics.
     - Other HTTP failures (`408`, `429`, `5xx`, timeouts) raise `LrclibRequestException` so MassTransit retries instead of treating them as "no lyrics". A `429` `Retry-After` delay pauses the shared throttle before the next attempt.
     - Successful and definitive not-found lookups are cached in `IMemoryCache` (24 h / 6 h defaults) keyed by normalized title, artist, **album**, and duration. Transient failures are never cached.
     - Structured logs record exact success, exact `404`, search strategy and counts, candidate scores/rejection reasons, lyric-flag skips, selected candidate, detail failures, and external errors — never full lyrics.
  2. **Persist lyrics first:** If plain or synced lyrics were found, publishes `SongLyricsFetchedEvent` directly through the bus (outside the consumer outbox) before resolving AI credentials or calling the provider. Catalog stores the lyrics and the selected record's canonical metadata (`trackName`, `artistName`, `albumName`, `duration`, `instrumental`) immediately, so a gRPC or provider failure cannot lose them.
  3. **Mood/tag extraction:** Uses `Microsoft.Extensions.AI` with Structured Outputs (JSON schema) to extract `PrimaryMood`, `Themes` (list), and `EnergyLevel` (1–10) from available lyrics/metadata.
  4. **Publish:** Emits `SongEnrichedEvent` containing both lyric forms, the resolved metadata, extracted tags, and a combined semantic summary string.
- Reuses the lyrics from the lookup in the final event; the LLM prompt receives plain lyrics only. A cached enrichment result that lacks lyrics or resolved metadata is re-checked against LRCLIB so late-arriving lyrics and canonical metadata can be merged without repeating the paid AI call.
- Uses Redis for its short-lived result cache and per-track locks so duplicate deliveries do not duplicate paid LLM calls.
- Holds no persistent state of its own; all enrichment output flows downstream via events.

### 2.6 `Lyra.PlaylistRag.Api` (ASP.NET Core Web API)

- Consumes `SongImportedEvent` to build the per-user `(UserId, TrackId) → LocalTrackId` ownership map. Ownership never comes from `SongEnrichedEvent`, whose `UserId` only identifies the user who triggered enrichment.
- Consumes `SongEnrichedEvent`:
  - Uses the event's `ResolvedMetadata` (LRCLIB title/artist/album/duration/instrumental) as the canonical track shape for embedding summaries and later playlist filters, while keeping the user's `LocalTrackId` for playback.
  - Generates one deployment-wide embedding per global track using OpenCode Go `text-embedding-3-small` (1536 dimensions); user BYOK connections are reserved for prompt rewriting and playlist selection.
  - Upserts embeddings into PostgreSQL using `pgvector`, with an `HNSW` cosine-distance index.
- `POST /api/playlists/generate`:
  1. Resolves the caller's encrypted AI connection over Identity's private gRPC service. Missing connection returns `409`.
  2. Rewrites the natural-language prompt into a validated query plan (semantic query, moods, themes, energy range, optional artist/title, playlist title/description) using structured JSON output.
  3. Embeds the semantic query with the deployment-wide embedding model.
  4. Executes a `pgvector` cosine search joined through `user_tracks`, so results are strictly scoped to the authenticated `UserId`; applies energy and explicit artist/title as hard filters, uses moods/themes as soft ranking signals over free-text enrichment, caps repeat artists, and keeps a bounded candidate set.
  5. Sends one-based candidate indices (not GUIDs) to the user's chat model, then validates count, range, uniqueness and reasons. Invalid output returns `502`; there is no fallback playlist.
  6. Maps selected candidates back to the user's own `LocalTrackId`s.
- Distinguishes an unindexed library (`409`) from a valid query whose filters matched nothing (`200` with an empty track list). Transient AI/Identity failures return `503`; other provider rejections return `502`.
- The React client exposes this flow under **Create playlist** (`/playlists`) with prompt, track-count, loading, error, result and playback states.
- Database: `vector_db` (PostgreSQL + `pgvector`).

## 3. Event Flow

```
Mobile Client
   │ POST /api/catalog/tracks/batch
   ▼
Lyra.Catalog.Api ──SongImportedEvent──▶ RabbitMQ ──▶ Lyra.Enrichment.Worker
   ▲                                                        │
   │                                   SongLyricsFetchedEvent│ (before AI)
   │                                                        ▼
   │                                          Lyra.Catalog.Api (lyrics stored)
   │                                                        │
   │                                                        │ SongEnrichedEvent
   │                                                        ▼
   └───────────── SongEnrichedEvent ◀── RabbitMQ ◀── Lyra.PlaylistRag.Api
                                                            │
                                               embeddings upserted into vector_db
```

- `Lyra.Catalog.Api` consumes `SongLyricsFetchedEvent` (lyrics only, not enriched) and `SongEnrichedEvent` (full enrichment). `Lyra.PlaylistRag.Api` consumes `SongImportedEvent` for per-user ownership and `SongEnrichedEvent` independently for embeddings.

**Catalog ingestion and the outbox**

The catalog never publishes directly from the request thread while the transaction is open. `SongImportedEvent` messages are written to the MassTransit `OutboxMessage` tables inside the same `SaveChangesAsync` that persists `tracks` and `user_tracks`. MassTransit's outbox delivery service publishes them to RabbitMQ after the transaction commits and keeps retrying while the broker is unavailable. A broker outage therefore delays enrichment but never loses an import or a message.

**Catalog cache invalidation**

A successful batch import bumps the caller's `catalog:tracks:{userId}:ver` counter after the commit. Every cached library page embeds that version in its key, so one counter increment makes all of the user's pages unreachable without scanning or deleting individual keys. Fuzzy search bypasses the cache entirely.

`SongLyricsFetchedEvent` bumps the same per-user version tokens when it newly stores lyrics, so the `HasLyrics` / `HasSyncedLyrics` flags refresh in cached library pages. `SongEnrichedEvent` invalidation follows the same mechanism. After an enrichment is applied (or detected as a duplicate), the consumer looks up every user linked to the track and bumps each user's version token. Because the track row is global, one accepted enrichment becomes visible to all owners of that track without fetching their pages or scanning their cache keys.

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
- **Per-user AI credentials (BYOK):** AI provider keys are accepted only through the authenticated `/api/auth/ai-connection` endpoints, encrypted with ASP.NET Core Data Protection using the `Lyra.Identity.UserAiConnection.v1` purpose, and stored in `identity_db`. API responses expose provider/model/plan metadata but never the key. The Worker receives the decrypted key only over the private authenticated gRPC call and keeps no persistent copy.
- Playlists are generated and returned only in terms of `LocalTrackId`s — the backend never needs to resolve or return actual audio file locations.
