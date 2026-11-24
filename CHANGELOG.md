# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

- `2026-11-24` — Added a GitHub Actions CI pipeline (`.github/workflows/ci.yml`): backend restore/build (warnings as errors)/test with Testcontainers PostgreSQL, and client `bun install --frozen-lockfile`/typecheck/test/build. Why: nothing verified a push before it reached a deployable `main`. Docs: `README.md`, `CHANGELOG.md`. Confirmed: owner.

- `2026-11-24` — Adopted a docs-first workflow and architecture decision records: `docs/decisions/` (index, template, ADR-0001–0003) and `AGENTS.md` §0/§8–§11. Why: `README.md` and `docs/ARCHITECTURE.md` had drifted from the code (`Lyra.PlaylistRag.Api` was described as built), and there was no recorded reason for standing boundaries. Docs: `AGENTS.md`, `docs/decisions/README.md`, `docs/decisions/ADR-*.md`, `CHANGELOG.md`. Confirmed: owner.
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
- `Lyra.Catalog.Api`: consumes `SongEnrichedEvent`, applies the first successful global enrichment atomically, and invalidates every linked user's Redis library cache; race-safe cache fills now use opaque version tokens.
- `prototype/lyra-player`: AI provider settings page, enrichment status display, accessibility fixes, and backend/frontend test updates.
- `tests/Lyra.Catalog.Api.Tests`: enrichment and lyrics persistence service/consumer tests, lyrics ownership endpoint coverage, and race-safe cache tests.
- `tests/Lyra.Enrichment.Worker.Tests`: LRCLIB exact/v2 fallback, title-dominant scoring, threshold rejection, lyrics-flag filtering, detail-fetch limits, synced lyrics, caching, 429 `Retry-After`, and server-error tests.

### Changed

- `2026-11-24` — Hardened configuration and removed dead scaffolding: `Jwt:RequireHttpsMetadata` now falls back to the secure `true` default outside Development, with the four services overriding it to `false` only in `appsettings.Development.json`; `Lyra.Identity.Api`, `Lyra.Catalog.Api`, and `Lyra.Enrichment.Worker` load the repository-root `.env` only when the environment is unset or `Development`, so deployed environments read injected environment variables only; Identity and the Worker now refuse to start outside Development/Testing with an empty `InternalServices__ApiKey`; and unused package references, empty folders, and `Compile Remove` entries were removed from `Lyra.PlaylistRag.Api` and `Lyra.Identity.Api`. Docs: `.env.example`, `CHANGELOG.md`. Confirmed: owner.

- `2026-11-24` — Migrated `clients/lyra-player` from Vite + Vitest + Prettier to Vite+ (`vite-plus` 1.6.0, `vp` CLI): one dependency now provides the dev server, Rolldown build, Vitest 5 tests, Oxlint, and Oxfmt. Scripts are `vp dev|build|test|lint|fmt|check`, test imports come from `vite-plus/test`, `.prettierrc.mjs` is gone, and the formatting style is preserved (single quotes, 100 columns, trailing commas). Also fixed an invalid `var(var(--l))` in `player.css` that lightningcss rejected and raised the testing-library async timeout to cover Vitest 5's per-file worker startup. Docs: `README.md`, `AGENTS.md`, `CHANGELOG.md`. Confirmed: owner.
- `2026-11-24` — Renamed the licence file to `LICENSE.md` (plain MIT text unchanged) and updated the README link. Docs: `README.md`, `CHANGELOG.md`. Confirmed: owner.
- `2026-11-24` — Hardened `docker-compose.yml`: healthchecks for PostgreSQL (`pg_isready`), RabbitMQ (`rabbitmq-diagnostics ping`), and Redis (`redis-cli ping`); `restart: unless-stopped`; and every published port bound to `127.0.0.1` so local infrastructure is not reachable from the LAN. Docs: `CHANGELOG.md`. Confirmed: owner.
- `2026-11-24` — Moved the React client from `prototype/lyra-player` to `clients/lyra-player` per ADR-0002, moved its `.gitignore` to `clients/.gitignore`, and added `clients/lyra-player/.env.example` (`VITE_BACKEND_URL`, the gateway address used by the Vite dev proxy). Why: the client is maintained, not disposable, and `prototype/` is reserved for throwaway spikes; `clients/` is now the home for every non-.NET client, including the future Flutter app. Docs: `README.md`, `AGENTS.md`, `docs/decisions/ADR-0002-web-client-lives-in-clients.md`, `CHANGELOG.md`. Confirmed: owner.
- `2026-11-24` — Pinned the SDK in `global.json` (`10.0.404`), centralized build settings in `Directory.Build.props` (`net10.0`, nullable, implicit usings, warnings-as-errors, deterministic CI builds) and `.editorconfig`, and dropped an unused test fixture field that the new analyzer settings flagged. Why: every project had its own copy of the same defaults, and nothing stopped warnings from accumulating. Docs: `README.md`, `AGENTS.md`, `CHANGELOG.md`. Confirmed: owner.
- `2026-11-24` — Moved the two Junie implementation plans from `.junie/plans/` to `docs/plans/` with an index, and stopped tracking the `.junie/` tool directory (now gitignored). Why: the plans document real implementation work and belong next to the architecture docs, while the tool directory is local state. Docs: `docs/plans/README.md`, `AGENTS.md`, `.gitignore`, `CHANGELOG.md`. Confirmed: owner.
- `2026-11-24` — Stopped tracking vendored agent-skill packs and IDE state: `skills/` (3835 files, ~31 MB of vendored .NET agent skills) and `.idea/` are no longer in the index and are gitignored, so they stay local. Why: they made clones, code search, and review noisy. Docs: `AGENTS.md`, `.gitignore`, `CHANGELOG.md`. Confirmed: owner.
- `Lyra.Catalog.Api` library responses expose `isEnriched`, `hasLyrics`, and `hasSyncedLyrics` from the shared track row; enrichment and lyric updates are visible to every user who owns the track.

### Planned

- `Lyra.PlaylistRag.Api` for embedding generation, `pgvector` similarity search, and RAG playlist generation (`vector_db`).
