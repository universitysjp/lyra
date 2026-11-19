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

### Planned

- `Lyra.Enrichment.Worker` for LRCLIB lyrics lookup and LLM-based mood/theme/energy extraction.
- `Lyra.PlaylistRag.Api` for embedding generation, `pgvector` similarity search, and RAG playlist generation (`vector_db`).
