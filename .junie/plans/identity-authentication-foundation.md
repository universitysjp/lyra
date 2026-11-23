---
sessionId: session-261113-060525-d3uj
---

# Requirements

### Overview & Goals

The authentication foundation is complete. This milestone builds the **catalog ingestion slice** — the entry point of the Lyra pipeline — turning `Lyra.Catalog.Api` from a `/api/catalog/ping` stub into the service that owns `catalog_db`.

When this is done:

- The Flutter client can push batches of locally extracted ID3 metadata to `POST /api/catalog/tracks/batch` and get `202 Accepted`.
- A global `tracks` table stores one row per unique track (by `TrackFingerprint`), shared across all users. A `user_tracks` link table maps each user to the tracks they own, carrying `LocalTrackId`, `ImportedAt`, and per-user state.
- Every newly stored track emits a `SongImportedEvent` onto RabbitMQ — the trigger the (not yet built) `Lyra.Enrichment.Worker` will consume.
- The user can read back and fuzzy-search their own library, served from a Redis cache-aside layer backed by `pg_trgm` indexes.

### Scope

**In Scope**

- `Lyra.Contracts`: the `SongImportedEvent` and `SongEnrichedEvent` records (both defined now so downstream milestones have a stable contract).
- `Lyra.Catalog.Api`: `catalog_db` EF Core model with a centralized `tracks` table and a `user_tracks` link table, batch ingestion with fingerprint deduplication, user library read, fuzzy search.
- MassTransit RabbitMQ wiring with the **EF Core transactional outbox**, so events are persisted in the same transaction as the tracks.
- Redis cache-aside for user library reads.
- Initial EF Core migration including `CREATE EXTENSION IF NOT EXISTS pg_trgm;` and the GIN trigram indexes.
- A `tests/Lyra.Catalog.Api.Tests/` project (Testcontainers PostgreSQL + MassTransit in-memory test harness).
- Doc updates: `CHANGELOG.md`, `README.md`, `docs/ARCHITECTURE.md`.

**Out of Scope**

- `Lyra.Enrichment.Worker` (LRCLIB lookup, LLM mood/tag extraction) — the next milestone.
- `Lyra.PlaylistRag.Api` embeddings and `POST /api/playlists/generate`.
- Consuming `SongEnrichedEvent` in `Lyra.Catalog.Api` (the record is defined, the consumer arrives with the worker milestone).
- Any change to authentication, the gateway's security model, or `Lyra.Identity.Api`.

### User Stories

- As a mobile user, I want to sync the metadata of my local tracks in batches so that my library exists on the backend without uploading audio.
- As a mobile user, I want re-syncing the same tracks to be harmless so that repeated imports never duplicate my library.
- As a mobile user, I want to browse my synced library quickly so that the app feels instant even on a large collection.
- As a mobile user, I want to fuzzy-search by title or artist so that typos and partial names still find my tracks.
- As a service owner, I want every imported track to reliably produce a `SongImportedEvent` so that enrichment never silently skips a track.

### Functional Requirements

| Endpoint | Behavior |
|---|---|
| `POST /api/catalog/tracks/batch` | Requires auth. Accepts up to 500 track metadata items; computes `TrackFingerprint`, upserts global track rows and links them to the user (or skips if already linked), returns `202 Accepted` with imported/duplicate/total counts. `400` on an empty or oversized batch. |
| `GET /api/catalog/tracks` | Requires auth. Paged (`page`, `pageSize`, default 50, max 200) list of the caller's tracks, newest first, served cache-aside from Redis. |
| `GET /api/catalog/tracks/search?q=` | Requires auth. Fuzzy title/artist match via `pg_trgm` similarity, ordered by score; `400` when `q` is blank. Not cached. |

- `TrackFingerprint` is `SHA-256(normalizedTitle + "|" + normalizedArtist + "|" + durationSeconds)` where normalization is trim + lowercase + collapsed inner whitespace; uniqueness is on the global `tracks` table by `TrackFingerprint` alone.
- A batch is processed in a single transaction: tracks, user links, and outbox messages commit together, or neither does.
- Duplicates within the submitted batch itself are collapsed before insert.
- Every endpoint filters strictly by the `userId` claim through the `user_tracks` link table; a user can never observe another user's tracks.
- Writing a batch invalidates that user's cached library pages.
- Errors surface as RFC 7807 problem details through a catalog-local exception middleware, matching the Identity service's behavior.

### Non-Functional Requirements

- `catalog_db` is owned exclusively by `Lyra.Catalog.Api`; no other service opens a `DbContext` against it.
- No connection string, broker credential, or Redis endpoint is hardcoded — configuration, user secrets, environment variables only.
- All I/O is `async` with `CancellationToken` propagation.
- Ingestion of a 500-track batch is a single bulk round trip plus one existence query, not per-track queries.
- A RabbitMQ outage must not fail ingestion or lose events — the outbox drains when the broker returns.

# Technical Design

### Current Implementation

The authentication milestone shipped and sets the conventions this milestone follows:

- `Lyra.Identity.Api` is the reference layout: `Models/`, `Data/IdentityDbContext.cs`, `Services/Authentication/*` (interface + sealed primary-constructor implementation), `Endpoints/*Endpoints.cs` exposing `Map*Endpoints()` over `MapGroup(...)`, `DTOs/Requests` + `DTOs/Responses`, typed `Exceptions/*`, `Middleware/ExceptionHandlingMiddleware`, and all user-facing strings externalized into `AppStrings` via `Configuration/AppStringOptions`. Files use namespace-first declarations with usings inside the namespace, and `Program.cs` is numbered-comment composition only.
- `Lyra.Catalog.Api/Program.cs` is 32 lines: OpenAPI, `AddLyraJwtAuthentication`, and a single `GET /api/catalog/ping` echoing `User.GetUserId()`. No `DbContext`, no MassTransit, no Redis.
- `Lyra.Catalog.Api.csproj` **already references** `MassTransit.RabbitMQ` 8.5.7, `Npgsql.EntityFrameworkCore.PostgreSQL`, `Microsoft.EntityFrameworkCore.Design`, `Microsoft.Extensions.Caching.StackExchangeRedis` and `Lyra.Contracts`.
- `Lyra.Contracts` contains only `Authentication/` — **no event records exist yet**.
- `infra/postgres/init-databases.sh` already creates `catalog_db` and enables `pg_trgm` in it; `docker-compose.yml` already runs RabbitMQ 4 and Redis 7.
- `Lyra.Gateway/appsettings.json` already routes `/api/catalog/*` to `http://localhost:5002` behind an authenticated policy, so no gateway change is needed for the new endpoints.
- `Lyra.Catalog.Api/appsettings.json` has only `Jwt` — no connection string, broker or Redis configuration.

### Key Decisions

1. **Centralized track store with user-track link table** — A global `tracks` table holds one row per unique track (unique by `TrackFingerprint`). A `user_tracks` table links users to tracks, carrying `LocalTrackId`, `ImportedAt`, and per-user state. This eliminates track metadata duplication across users: if 100 users import "Bohemian Rhapsody", only one `tracks` row exists. Enrichment data (lyrics, mood, energy) lives on the global `tracks` row and is shared — enrichment runs once per unique track, not per user.
2. **MassTransit EF Core transactional outbox** — `AddEntityFrameworkOutbox<CatalogDbContext>()` plus `UseBusOutbox()`. `SongImportedEvent`s are written to `InboxState`/`OutboxState`/`OutboxMessage` tables inside the same `SaveChangesAsync` as the tracks and delivered by MassTransit's delivery service. Ingestion therefore never half-commits and a broker outage degrades to delayed enrichment rather than lost tracks.
3. **Catalog mirrors the Identity layering** — `Endpoints/` → `Services/` → `Repositories/` → `Data/`, with its own `Exceptions/`, `Middleware/ExceptionHandlingMiddleware` and `AppStrings`. The plumbing is duplicated rather than promoted to `Lyra.Contracts`, keeping the approved Contracts exception limited to authentication.
4. **Repository layer over `CatalogDbContext`** — `ITrackRepository` isolates EF Core/`pg_trgm` queries so `TrackIngestionService` and `TrackQueryService` stay testable, and so the raw-SQL similarity query has one home.
5. **Fingerprint computed server-side** — the client sends raw ID3 metadata; `TrackFingerprint` is derived in `FingerprintGenerator` so the rule can never drift between client versions. Deduplication is enforced both in code (batch pre-collapse + existence query) and by a unique index on `TrackFingerprint` in the global `tracks` table.
6. **Redis cache-aside keyed per user and page** — `catalog:tracks:{userId}:p{page}:s{pageSize}` through `IDistributedCache`, with a per-user `catalog:tracks:{userId}:ver` version counter so a batch import invalidates every page of that user with one increment instead of key scanning. Search results are not cached.
7. **`SongEnrichedEvent` defined now, consumed later** — publishing and consuming it belongs to the enrichment milestone, but the record lands in `Lyra.Contracts` now so the worker and PlaylistRag have a stable shape to build against.

### Proposed Changes

**`Lyra.Contracts` — event records**

An `Events/` folder gains the two immutable `record` contracts named in `AGENTS.md` §2. They carry `UserId` so every downstream consumer can keep data user-scoped without a lookup, and the catalog's `TrackId` plus the client's `LocalTrackId` so PlaylistRag can ultimately answer in the client's own identifiers.

**`Lyra.Catalog.Api` — the catalog service**

`Program.cs` is rewritten as numbered composition in the Identity style: `AppStringOptions`, `CatalogOptions`, Npgsql `CatalogDbContext`, Redis `AddStackExchangeRedisCache`, MassTransit with the EF Core outbox, the repository and services, `AddLyraJwtAuthentication`, the exception middleware, then `app.MapTrackEndpoints()`.

- `Models/Track` is an EF-tracked class on the global `tracks` table (`Id`, `Title`, `Artist`, `Album`, `DurationSeconds`, `TrackFingerprint`, `IsEnriched`, `Lyrics`, `PrimaryMood`, `Themes`, `EnergyLevel`, `SemanticSummary`). `IsEnriched` defaults to `false` and is the hook the enrichment milestone will flip. Enrichment data is shared across all users who have this track.
- `Models/UserTrack` is an EF-tracked class on the `user_tracks` link table (`UserId`, `TrackId`, `LocalTrackId`, `ImportedAt`). The composite key is `(UserId, TrackId)`; a unique index on `(UserId, LocalTrackId)` prevents the same client track from being linked twice.
- `Data/CatalogDbContext` configures the unique `TrackFingerprint` index on `tracks`, a `(UserId, ImportedAt)` index on `user_tracks` for paging, GIN trigram indexes on `Title` and `Artist` in `tracks`, and adds the MassTransit outbox entity configurations.
- `Services/Catalog/FingerprintGenerator` implements the normalization + SHA-256 rule as a pure, unit-testable static helper.
- `Services/Catalog/TrackIngestionService` validates the batch, collapses in-batch duplicates by fingerprint, upserts global track rows (insert if new fingerprint, skip if existing), links tracks to the user via `user_tracks` (skip if already linked), publishes one `SongImportedEvent` per newly linked track through `IPublishEndpoint` (captured by the outbox), and commits once.
- `Services/Catalog/TrackQueryService` serves the paged library through `IDistributedCache` cache-aside and delegates fuzzy search straight to the repository.
- `Repositories/TrackRepository` owns the EF Core queries, including the `pg_trgm` similarity search executed via `FromSql` with `similarity(title, {q})` ordering and a similarity threshold, always joining through `user_tracks` to keep results user-scoped.
- `Endpoints/TrackEndpoints` is a `MapGroup("/api/catalog/tracks").RequireAuthorization()` exposing the three endpoints from the Requirements tab, each resolving the owner via `User.GetUserId()`.
- `Exceptions/` gains `ValidationException` and `NotFoundException`; `Middleware/ExceptionHandlingMiddleware` maps them to problem details, mirroring Identity.
- An initial EF Core migration creates `tracks`, `user_tracks`, the outbox tables, the indexes, and emits `CREATE EXTENSION IF NOT EXISTS pg_trgm;` before the GIN indexes. Migrations are applied at startup in Development only, as in Identity.

**Configuration, infrastructure & docs**

`Lyra.Catalog.Api/appsettings.json` gains `ConnectionStrings:CatalogDb` and `ConnectionStrings:Redis`, a `RabbitMq` section (host/username/password), a `Catalog` section (`MaxBatchSize`, `DefaultPageSize`, `MaxPageSize`, `LibraryCacheMinutes`, `SearchSimilarityThreshold`) and an `AppStrings` block. `Lyra.Catalog.Api.http` gets the full request sequence (login via Identity → batch → list → search). `docker-compose.yml` and `infra/postgres/init-databases.sh` already satisfy this milestone and are left untouched. `docs/ARCHITECTURE.md` §2.4 and §3 are updated with the outbox and cache invalidation detail, `README.md` gains the catalog endpoints, and `CHANGELOG.md` moves the Catalog line from *Planned* to *Added*.

### Data Models / Contracts

```csharp
// Lyra.Contracts/Events/SongImportedEvent.cs
public sealed record SongImportedEvent(
    Guid TrackId,
    Guid UserId,
    string LocalTrackId,
    string Title,
    string Artist,
    string? Album,
    int DurationSeconds,
    DateTimeOffset ImportedAt);

// Lyra.Contracts/Events/SongEnrichedEvent.cs  (defined now, published by the worker milestone)
public sealed record SongEnrichedEvent(
    Guid TrackId,
    Guid UserId,
    string? Lyrics,
    string PrimaryMood,
    IReadOnlyList<string> Themes,
    int EnergyLevel,
    string SemanticSummary,
    DateTimeOffset EnrichedAt);
```

```csharp
// Lyra.Catalog.Api/Models/Track.cs  (global track store, one row per unique track)
public class Track
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Artist { get; set; } = string.Empty;
    public string? Album { get; set; }
    public int DurationSeconds { get; set; }
    public string TrackFingerprint { get; set; } = string.Empty;  // unique globally
    public bool IsEnriched { get; set; }
    // Enrichment data (populated by the enrichment milestone, shared across users)
    public string? Lyrics { get; set; }
    public string? PrimaryMood { get; set; }
    public string? Themes { get; set; }  // JSON array
    public int? EnergyLevel { get; set; }
    public string? SemanticSummary { get; set; }
}

// Lyra.Catalog.Api/Models/UserTrack.cs  (user-track link table)
public class UserTrack
{
    public Guid UserId { get; set; }
    public Guid TrackId { get; set; }
    public string LocalTrackId { get; set; } = string.Empty;
    public DateTimeOffset ImportedAt { get; set; }
    public Track Track { get; set; } = null!;
}
```

```csharp
// Lyra.Catalog.Api/DTOs/Requests + Responses
public sealed record TrackMetadataItem(
    string LocalTrackId, string Title, string Artist, string? Album, int DurationSeconds);
public sealed record ImportTracksRequest(IReadOnlyList<TrackMetadataItem> Tracks);
public sealed record ImportTracksResponse(int Submitted, int Imported, int Duplicates);
public sealed record TrackResponse(
    Guid Id, string LocalTrackId, string Title, string Artist, string? Album,
    int DurationSeconds, bool IsEnriched, DateTimeOffset ImportedAt);
public sealed record PagedResponse<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount);
```

```csharp
// Lyra.Catalog.Api/Services/Catalog
public static class FingerprintGenerator
{
    public static string Compute(string title, string artist, int durationSeconds);
}

public interface ITrackIngestionService
{
    Task<ImportTracksResponse> ImportBatchAsync(
        Guid userId, ImportTracksRequest request, CancellationToken ct);
}

public interface ITrackQueryService
{
    Task<PagedResponse<TrackResponse>> GetLibraryAsync(
        Guid userId, int page, int pageSize, CancellationToken ct);
    Task<IReadOnlyList<TrackResponse>> SearchAsync(
        Guid userId, string query, CancellationToken ct);
}

// Lyra.Catalog.Api/Repositories/ITrackRepository.cs
public interface ITrackRepository
{
    // Upsert global tracks: returns tracks that were newly inserted vs already existing
    Task<Dictionary<string, Guid>> UpsertTracksAsync(
        IReadOnlyCollection<Track> tracks, CancellationToken ct);
    // Find which tracks are already linked to this user (by fingerprint)
    Task<HashSet<string>> GetLinkedFingerprintsAsync(
        Guid userId, IReadOnlyCollection<string> fingerprints, CancellationToken ct);
    // Link tracks to user (skip already linked)
    Task LinkUserTracksAsync(
        Guid userId, IReadOnlyCollection<(Guid TrackId, string LocalTrackId, DateTimeOffset ImportedAt)> links, CancellationToken ct);
    // Paged user library (join through user_tracks)
    Task<(IReadOnlyList<TrackResponse> Items, int TotalCount)> GetPagedAsync(
        Guid userId, int page, int pageSize, CancellationToken ct);
    // Fuzzy search (join through user_tracks, pg_trgm on tracks)
    Task<IReadOnlyList<TrackResponse>> SearchFuzzyAsync(
        Guid userId, string query, double threshold, int limit, CancellationToken ct);
}
```

### File Structure

```
Lyra.Contracts/
  Events/SongImportedEvent.cs                        (new)
  Events/SongEnrichedEvent.cs                        (new)
Lyra.Catalog.Api/
  Program.cs                                         (rewritten)
  Configuration/AppStringOptions.cs                  (new)
  Configuration/CatalogOptions.cs                    (new)
  Models/Track.cs                                    (new, global track store)
  Models/UserTrack.cs                                (new, user-track link table)
  Data/CatalogDbContext.cs                           (new)
  Repositories/ITrackRepository.cs, TrackRepository.cs (new)
  Services/Catalog/FingerprintGenerator.cs           (new)
  Services/Catalog/ITrackIngestionService.cs, TrackIngestionService.cs (new)
  Services/Catalog/ITrackQueryService.cs, TrackQueryService.cs (new)
  Services/Caching/ILibraryCache.cs, LibraryCache.cs (new)
  Endpoints/TrackEndpoints.cs                        (new)
  DTOs/Requests/ImportTracksRequest.cs               (new)
  DTOs/Responses/ImportTracksResponse.cs, TrackResponse.cs, PagedResponse.cs (new)
  Exceptions/ValidationException.cs, NotFoundException.cs (new)
  Middleware/ExceptionHandlingMiddleware.cs          (new)
  Migrations/                                        (new, EF Core)
  appsettings*.json, Lyra.Catalog.Api.http           (updated)
tests/
  Lyra.Catalog.Api.Tests/                            (new)
Lyra.slnx                                            (test project registered)
README.md, docs/ARCHITECTURE.md, CHANGELOG.md        (updated)
```

### Architecture Diagram

```mermaid
graph LR
  C[Flutter client] -->|Bearer JWT| G[Lyra.Gateway]
  G -->|/api/catalog/tracks/*| EP[TrackEndpoints]
  EP --> IS[TrackIngestionService]
  EP --> QS[TrackQueryService]
  IS --> RP[TrackRepository]
  QS --> RP
  QS --> RD[(Redis library cache)]
  RP --> TK[tracks (global, unique fingerprint)]
  RP --> UL[user_tracks (UserId + TrackId + LocalTrackId)]
  TK --> DB[(catalog_db)]
  UL --> DB
  IS -->|IPublishEndpoint| DB
  OB[MassTransit outbox delivery] --> DB
  OB --> MQ[RabbitMQ SongImportedEvent]
  MQ -.next milestone.-> W[Lyra.Enrichment.Worker]
```

### Risks

- **Centralized track store adds a join for every query.** Every library read and search must join `user_tracks` to `tracks`. Mitigated by indexes on `(UserId, ImportedAt)` in `user_tracks` and GIN trigram indexes on `tracks`. The join is a small, bounded operation compared to the benefit of eliminating metadata duplication.
- **Outbox adds three tables and a delivery service.** Mitigated by using MassTransit's built-in `AddEntityFrameworkOutbox` configuration rather than a hand-rolled mechanism, and by covering publish-on-commit in the harness tests.
- **`pg_trgm` similarity cannot be expressed in LINQ.** The search query uses parameterized `FromSql` with `similarity(...)`; it must stay parameterized to avoid SQL injection and must keep the `UserId` filter inside the SQL (via the `user_tracks` join), not applied afterwards.
- **Cache staleness.** Library pages are versioned per user and the version is bumped on every successful import, so a stale page can only ever be served to a user who has not written — but the enrichment milestone must remember to bump the same version when `IsEnriched` flips on a global track.
- **Large batches.** A 500-item cap plus a single bulk upsert keeps the request bounded; oversized batches are rejected with `400` rather than silently truncated.
- **Fingerprint collisions across differing metadata.** Two genuinely different tracks with identical normalized title, artist and duration collapse into one global row — accepted, since this is exactly the deduplication rule `AGENTS.md` prescribes.

# Testing

### Validation Approach

- `dotnet build Lyra.slnx` must succeed after each stage, and the existing `tests/Lyra.Identity.Api.Tests` suite must stay green — this milestone must not regress authentication.
- Pure unit tests for `FingerprintGenerator` (no infrastructure).
- Repository and ingestion tests against a real PostgreSQL via `Testcontainers.PostgreSql` with `pg_trgm` enabled, mirroring `tests/Lyra.Identity.Api.Tests/PostgresTestFixture`, never a mocked `DbContext`.
- Event publication verified with the MassTransit in-memory test harness (`MassTransit.Testing`), not a live broker.
- Endpoint tests through `WebApplicationFactory` with a test authentication handler injecting a `userId` claim, so auth setup is not re-tested here.
- Manual smoke-check through `Lyra.Catalog.Api.http` and the gateway against `docker compose up`.

### Key Scenarios

1. A batch of 3 new tracks → `202` with `Imported = 3, Duplicates = 0`; 3 rows in `tracks` (global) and 3 rows in `user_tracks` linking the caller to those tracks.
2. Re-posting the same batch → `202` with `Imported = 0, Duplicates = 3`; no new rows in either table.
3. A second user imports the same 3 tracks → `202` with `Imported = 3, Duplicates = 0`; the `tracks` table still has only 3 rows, but `user_tracks` has 6 rows (3 per user).
4. `FingerprintGenerator.Compute` returns the same hash for `" The  Song "` / `"the song"` and the same artist/duration, and a different hash when duration differs.
5. A successful import publishes exactly one `SongImportedEvent` per newly linked track, each carrying the persisted `TrackId`, the caller's `UserId` and the submitted `LocalTrackId`.
6. `GET /api/catalog/tracks` returns the caller's tracks newest-first with correct `TotalCount`; a second identical call is served from Redis without a database round trip.
7. An import after a cached read invalidates the cache — the next read includes the new tracks.
8. `GET /api/catalog/tracks/search?q=<typo>` returns the intended track via trigram similarity and excludes unrelated ones, always joining through `user_tracks`.
9. User B can never see User A's tracks, even though they share the same `tracks` rows.

### Edge Cases

- Empty batch or a batch above `MaxBatchSize` → `400` problem details, nothing written, nothing published.
- A batch containing the same track twice → collapsed to one link and one event.
- Missing title or artist in an item → `400`, the whole batch rejected atomically.
- A second user importing identical metadata → the same `tracks` row is reused; only a new `user_tracks` row is created. The `tracks` table grows only for truly new fingerprints.
- User B calling the list and search endpoints never sees user A's tracks, even though they share `tracks` rows — the `user_tracks` join enforces scoping.
- Blank or whitespace-only `q` → `400`; a query matching nothing → `200` with an empty list.
- `page`/`pageSize` out of range → clamped to the configured bounds rather than erroring.
- Any `/api/catalog/tracks/*` call without a bearer token → `401`.
- Broker unavailable during import → the request still returns `202` and the outbox rows persist for later delivery.

### Test Changes

- New `tests/Lyra.Catalog.Api.Tests/` project (xUnit, `Microsoft.AspNetCore.Mvc.Testing`, `Testcontainers.PostgreSql`, `MassTransit.Testing`), registered in `Lyra.slnx`.
- `FingerprintGeneratorTests` — normalization, determinism, and collision/difference behavior.
- `TrackRepositoryTests` — existing-fingerprint lookup, paging order and counts, trigram search relevance and user scoping, against Testcontainers.
- `TrackIngestionServiceTests` — dedupe counts, in-batch collapsing, and `SongImportedEvent` publication via the in-memory harness.
- `TrackQueryServiceTests` — cache hit/miss and invalidation after an import.
- `TrackEndpointsTests` — the batch → list ��� search flow plus the edge cases above.
- No tests touching live LRCLIB, LLM providers, RabbitMQ or Redis servers — these boundaries are faked or containerized.

# Delivery Steps

###   Step 1: Define the cross-service event contracts and the catalog data layer
`Lyra.Contracts` carries the pipeline events and `Lyra.Catalog.Api` can create and migrate its own `catalog_db` schema with a centralized track store.

- Add `Lyra.Contracts/Events/SongImportedEvent.cs` and `Events/SongEnrichedEvent.cs` as immutable `record` contracts with the fields listed in the Technical Design tab.
- Add `Lyra.Catalog.Api/Models/Track.cs` as an EF-tracked entity for the global `tracks` table (unique `TrackFingerprint`, enrichment columns like `Lyrics`, `PrimaryMood`, `Themes`, `EnergyLevel`, `SemanticSummary`).
- Add `Lyra.Catalog.Api/Models/UserTrack.cs` as an EF-tracked entity for the `user_tracks` link table (`UserId`, `TrackId`, `LocalTrackId`, `ImportedAt`, composite key on `(UserId, TrackId)`).
- Add `Lyra.Catalog.Api/Data/CatalogDbContext.cs` with the unique `TrackFingerprint` index on `tracks`, the `(UserId, ImportedAt)` paging index on `user_tracks`, GIN trigram indexes on `Title` and `Artist` in `tracks`, and the MassTransit outbox entity configurations.
- Add `Configuration/CatalogOptions.cs` (`MaxBatchSize`, `DefaultPageSize`, `MaxPageSize`, `LibraryCacheMinutes`, `SearchSimilarityThreshold`) and `Configuration/AppStringOptions.cs` mirroring the Identity pattern.
- Register the Npgsql `CatalogDbContext` in `Program.cs`, add `ConnectionStrings:CatalogDb` to `appsettings.json`, and generate the initial EF Core migration emitting `CREATE EXTENSION IF NOT EXISTS pg_trgm;` before the GIN indexes.

###   Step 2: Implement batch ingestion with fingerprint deduplication and outbox publishing
`POST /api/catalog/tracks/batch` upserts global tracks, links them to the caller, and reliably emits one `SongImportedEvent` per newly linked track.

- Add `Services/Catalog/FingerprintGenerator.cs` implementing trim/lowercase/whitespace-collapse normalization plus SHA-256 over `title|artist|duration`.
- Add `Repositories/ITrackRepository.cs` + `TrackRepository.cs` with `UpsertTracksAsync` (upsert global tracks by fingerprint), `GetLinkedFingerprintsAsync` (find which tracks are already linked to the user), and `LinkUserTracksAsync` (create user-track links, skipping existing ones).
- Add `Services/Catalog/TrackIngestionService.cs` (sealed, primary constructor): validate the batch against `MaxBatchSize`, collapse in-batch duplicates, upsert global track rows, query which tracks are already linked to the user, link the remainder, publish `SongImportedEvent` per newly linked track via `IPublishEndpoint`, and commit once.
- Wire MassTransit in `Program.cs` with `AddMassTransit`, `AddEntityFrameworkOutbox<CatalogDbContext>()`, `UsingRabbitMq(...).UseBusOutbox()`, driven by a new `RabbitMq` configuration section.
- Add `DTOs/Requests/ImportTracksRequest.cs`, `DTOs/Responses/ImportTracksResponse.cs`, `Exceptions/ValidationException.cs` and `Middleware/ExceptionHandlingMiddleware.cs` returning RFC 7807 problem details.
- Add `Endpoints/TrackEndpoints.cs` as `MapGroup("/api/catalog/tracks").RequireAuthorization()` with the `POST /batch` endpoint returning `202 Accepted`, resolving the owner via `User.GetUserId()`.

###   Step 3: Serve the user library with Redis cache-aside and pg_trgm fuzzy search
The caller can page through and fuzzy-search their own library, with reads served from Redis and invalidated on import, always joining through `user_tracks`.

- Extend `ITrackRepository` with `GetPagedAsync` (newest-first, with total count, joining `user_tracks` to `tracks`) and `SearchFuzzyAsync` using a parameterized `FromSql` `similarity(...)` query that keeps the `UserId` filter inside the SQL via the `user_tracks` join.
- Add `Services/Caching/ILibraryCache.cs` + `LibraryCache.cs` over `IDistributedCache`, keyed `catalog:tracks:{userId}:v{version}:p{page}:s{pageSize}` with a per-user version counter and an `InvalidateAsync(userId)` that bumps it.
- Add `Services/Catalog/TrackQueryService.cs` implementing cache-aside paging and uncached search, clamping `page`/`pageSize` to the configured bounds.
- Call `ILibraryCache.InvalidateAsync` from `TrackIngestionService` after a successful commit.
- Add `GET /` and `GET /search` to `TrackEndpoints` with `TrackResponse` / `PagedResponse<T>` DTOs, rejecting a blank `q` with `400`.
- Register `AddStackExchangeRedisCache` in `Program.cs`, add `ConnectionStrings:Redis` plus the `Catalog` and `AppStrings` sections to `appsettings*.json`, and remove the obsolete `/api/catalog/ping` endpoint.

###   Step 4: Cover the catalog with tests and refresh the project documentation
The ingestion slice is verified end to end and the repository docs describe the centralized track store as implemented.

- Create `tests/Lyra.Catalog.Api.Tests/` (xUnit, `Microsoft.AspNetCore.Mvc.Testing`, `Testcontainers.PostgreSql`, `MassTransit.Testing`) with a `PostgresTestFixture` enabling `pg_trgm`, and register it in `Lyra.slnx`.
- Add `FingerprintGeneratorTests` for normalization and determinism.
- Add `TrackRepositoryTests` for upsert tracks, linked-fingerprint lookup, paging order/count, and trigram search relevance, all user-scoped through `user_tracks`.
- Add `TrackIngestionServiceTests` asserting dedupe counts, global track upsert, user-track linking, and `SongImportedEvent` publication through the in-memory harness, and `TrackQueryServiceTests` for cache hit/miss and invalidation.
- Add `TrackEndpointsTests` covering batch → list → search plus the edge cases in the Testing tab (including two users importing the same tracks), using a test authentication handler for the `userId` claim.
- Update `Lyra.Catalog.Api.http` with the full request sequence, expand `docs/ARCHITECTURE.md` §2.4/§3 with the centralized track store, outbox, and cache-invalidation behavior, document the catalog endpoints in `README.md`, and move the Catalog entry in `CHANGELOG.md` from *Planned* to *Added*.