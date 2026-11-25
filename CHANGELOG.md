# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

- Initial repository scaffolding: `Lyra.slnx` solution file.
- Project documentation: `README.md`, `AGENTS.md`, `CONTRIBUTING.md`, `docs/ARCHITECTURE.md`, `LICENSE`.
- `Lyra.Contracts`: shared JWT authentication configuration (`AddLyraJwtAuthentication`), `JwtOptions`, and `ClaimsPrincipal` extensions.
- `Lyra.Identity.Api`: ASP.NET Core Identity on PostgreSQL (`identity_db`), RS256 JWT minting, JWKS (`/.well-known/jwks.json`), OIDC discovery (`/.well-known/openid-configuration`), registration, login, refresh token rotation with reuse detection, logout, and `/api/auth/me`.
- `Lyra.Gateway`: YARP reverse proxy routing `/api/auth/*`, `/api/catalog/*`, and `/api/playlists/*`, JWT bearer validation, rate limiting, and identity header stripping.
- `Lyra.Contracts`: immutable `SongImportedEvent` and `SongEnrichedEvent` pipeline contracts.
- `Lyra.Catalog.Api`: centralized `tracks` + `user_tracks` catalog schema, server-computed `TrackFingerprint` deduplication, `POST /api/catalog/tracks/batch` with MassTransit EF Core transactional outbox, paged library reads (`GET /api/catalog/tracks`), `pg_trgm` fuzzy search (`GET /api/catalog/tracks/search`), and Redis cache-aside with per-user version invalidation.
- `Lyra.PlaylistRag.Api`: JWT bearer authentication wiring and an authenticated `/ping` endpoint.
- `docker-compose.yml` multi-database initialization script for `identity_db`, `catalog_db` (with `pg_trgm`), and `vector_db` (with `vector`).
- `tests/Lyra.Identity.Api.Tests`: unit tests for `TokenService` and Testcontainers integration tests for `RefreshTokenService` and `AuthEndpoints`.
- `tests/Lyra.Catalog.Api.Tests`: unit tests for fingerprinting, repository/ingestion/query service integration tests against PostgreSQL, MassTransit in-memory harness event assertions, and endpoint tests covering the batch → list → search flow.
- Secrets management: repository-root `.env` (gitignored) + `.env.example` template; PostgreSQL/RabbitMQ credentials and connection strings removed from `appsettings.json` and `docker-compose.yml`; `Lyra.Identity.Api` and `Lyra.Catalog.Api` load `.env` via `DotNetEnv`; test fixtures fall back to `.env`/`LYRA_TEST_POSTGRES` instead of hardcoded credentials.
- `prototype/.gitignore`: Node/Vite ignore rules for the `prototype/lyra-player` React client (`node_modules/`, `dist/`, `coverage/`, `.env*` with `.env.example` exception, logs, editor noise).
- `Lyra.AI`: centralized `Microsoft.Extensions.AI` adapter library with per-provider `IChatClient` factories (`OpenAI`, `Gemini`, `OpenCode`, `Anthropic`).
- `Lyra.Identity.Api`: encrypted per-user AI provider connections (`GET`/`PUT`/`DELETE /api/auth/ai-connection`), Data Protection key encryption, and a private authenticated gRPC endpoint (`lyra.identity.v1.UserAiConnectionService/GetAiConnection`) secured by `InternalServices:ApiKey`.
- `Lyra.Enrichment.Worker`: resolves the user's AI connection over gRPC, caches enrichment results and per-track locks in Redis, and publishes `SongEnrichedEvent` without persisting state.
- `Lyra.Enrichment.Worker`: LRCLIB lookup normalizes title/artist for the query, tries exact `/api/get` (including a reliable album), then falls back to `/api/v2/search`. Candidates are scored 0–100 with title 55%, duration 30%, artist 15%; below-threshold and instrumental/no-lyrics candidates are rejected, and at most `MaxCandidateDetailFetches` highest-scoring eligible records are fetched through `/api/get/{id}` until usable lyrics are found. Ambiguity rejection was removed, 429 `Retry-After` pauses the throttle, searches progressively fall back from full artist to primary artist to title-only, and successful/negative results are cached with album in the key.
- `Lyra.Contracts` / `Lyra.Enrichment.Worker`: `SongLyricsFetchedEvent` is published as soon as lyrics are found, before the AI connection is resolved. A failed gRPC lookup or AI provider call no longer loses successfully retrieved lyrics. Cached enrichments without lyrics or resolved metadata are re-checked against LRCLIB and merged without repeating the AI call.
- `Lyra.Catalog.Api`: persists plain/synced lyrics and the selected LRCLIB record's resolved title, artist, album, duration, and instrumental flag without marking the track enriched, preserves partial updates, adds `GET /api/catalog/tracks/{trackId}/lyrics`, and exposes `hasLyrics` / `hasSyncedLyrics` on library responses.
- `prototype/lyra-player`: added a Spotify-style lyrics drawer with synced-line highlighting/auto-scroll and plain-lyrics fallback; persisted attached audio files in IndexedDB, added a **Connect local files** relink action, and fixed library playback to use the rendered track list instead of a stale queue.
- `Lyra.PlaylistRag.Api`: `vector_db` data layer with `user_tracks` and `track_embeddings`, pgvector migration, fixed 1536-dimension vector column, HNSW cosine index, and GIN/theme indexes.
- `Lyra.PlaylistRag.Api`: MassTransit event ingestion with `SongImportedConsumer` (per-user `LocalTrackId` mapping) and `SongEnrichedConsumer` (global track embedding upsert), explicit `playlist-rag-song-imported` / `playlist-rag-song-enriched` endpoints, retries, and idempotent PostgreSQL upserts.
- `Lyra.AI`: added `IAiEmbeddingGeneratorFactory` with OpenAI and OpenCode embedding providers. The OpenCode adapter uses the plan-specific inference endpoint and `x-opencode-session`, so OpenCode Go can generate embeddings with `text-embedding-3-small`.
- `Lyra.PlaylistRag.Api`: replaced the unconfigured embedding placeholder with the OpenCode Go embedding generator. `Rag:Provider`, `Rag:Model`, `Rag:Dimensions`, `Rag:OpenCodePlan`, and `Rag__ApiKey` are validated at startup; the fixed dimension must match `vector(1536)`.
- `tests/Lyra.AI.Tests`: embedding factory and OpenCode embedding HTTP tests covering plan endpoints, session header, model request body, plan/session validation, and dimension checks.
- `Lyra.PlaylistRag.Api`: `POST /api/playlists/generate` with structured query planning, deployment-wide query embeddings, user-scoped pgvector search, hard energy/artist/title filters, soft mood/theme ranking against free-text enrichment, max-two-per-artist diversification, one-based candidate-index selection, strict output validation, and problem+json mapping (`400`, `401`, `409`, `502`, `503`). Returned tracks contain only the caller's `LocalTrackId`s. Structured AI calls use low reasoning effort, tolerate fenced JSON, and retry once when a provider returns an empty message.
- `Lyra.PlaylistRag.Api` / `Lyra.AI`: shared Identity AI-connection gRPC client moved into `Lyra.AI.Identity`; PlaylistRag resolves the caller's BYOK chat connection for prompt planning and selection while using `Rag__ApiKey` only for embeddings.
- `prototype/lyra-player`: **Create playlist** page at `/playlists` with prompt ideas, track-count control, loading/error states, generated playlist hero, per-track reasons, local-file relinking, and queue playback; added the playlist API client and fake-backend coverage.
- `tests/Lyra.PlaylistRag.Api.Tests`: query planner/selector validation, generation service hard-failure policy, candidate-search ownership/filter/diversification tests, and authenticated endpoint tests including `409` and `502` paths.
- `tests/Lyra.PlaylistRag.Api.Tests`: pgvector migration/extension/HNSW/nearest-neighbour coverage, MassTransit consumer harness tests (including duplicate delivery and local metadata), and embedding-abstraction coverage.
- `Lyra.Catalog.Api`: consumes `SongEnrichedEvent`, applies the first successful global enrichment atomically, and invalidates every linked user's Redis library cache; race-safe cache fills now use opaque version tokens.
- `prototype/lyra-player`: AI provider settings page, enrichment status display, accessibility fixes, and backend/frontend test updates.
- `tests/Lyra.Catalog.Api.Tests`: enrichment and lyrics persistence service/consumer tests, lyrics ownership endpoint coverage, and race-safe cache tests.
- `tests/Lyra.Enrichment.Worker.Tests`: LRCLIB exact/v2 fallback, title-dominant scoring, threshold rejection, lyrics-flag filtering, detail-fetch limits, synced lyrics, caching, 429 `Retry-After`, and server-error tests.

### Changed

- `Lyra.Catalog.Api` library responses expose `isEnriched`, `hasLyrics`, and `hasSyncedLyrics` from the shared track row; enrichment and lyric updates are visible to every user who owns the track.

### Planned

- `Lyra.PlaylistRag.Api`: lazy Catalog gRPC backfill for libraries imported before PlaylistRag existed.
- React client: saved playlist history and server-side playlist persistence.
