# Requirements

### Overview & Goals

Catalog ingestion and enrichment are in place. This milestone builds **RAG playlist generation** in `Lyra.PlaylistRag.Api`:

- consume imported and enriched track events into `vector_db`;
- generate and store embeddings for enriched tracks;
- expose `POST /api/playlists/generate`;
- turn the authenticated user’s natural-language prompt into a semantic vector query plus metadata filters;
- use the user’s AI connection to select and sequence candidates;
- return only the caller’s own tracks as `LocalTrackId` values.

The feature must remain database-per-service: `Lyra.PlaylistRag.Api` owns `vector_db`; it never reads `catalog_db` or `identity_db` directly.

**No fallback policy:** this milestone uses hard thresholds and explicit failures. It does **not** include:

- a low-confidence lyrics fallback;
- a similarity-only playlist fallback when the LLM fails;
- a silent fallback to another AI or embedding provider;
- a partial playlist generated from invalid model output.

When a dependency or validation step fails, the API returns a clear error. It does not silently degrade.

### Scope

**In Scope**

1. **`vector_db` data layer**
   - `VectorDbContext`, `UserTrack`, `TrackEmbedding`.
   - EF Core migration with `CREATE EXTENSION IF NOT EXISTS vector`.
   - Fixed-dimension `vector` column and HNSW cosine index.
   - `ConnectionStrings__VectorDb` configuration and startup validation.

2. **Event ingestion**
   - `SongImportedEvent` consumer storing per-user `(UserId, TrackId, LocalTrackId)` mappings.
   - `SongEnrichedEvent` consumer generating and upserting one embedding per global `TrackId`.
   - Unique MassTransit endpoint names for PlaylistRag so queues do not collide with Worker/Catalog.
   - Idempotent upserts for duplicate delivery.

3. **Embedding support**
   - New `IAiEmbeddingGeneratorFactory` in `Lyra.AI`.
   - Provider adapters for the configured embedding provider.
   - Fixed model and dimension validation.
   - Sanitized errors; no fallback provider.

4. **Playlist generation**
   - `POST /api/playlists/generate`.
   - Structured prompt-to-query planning.
   - pgvector similarity search scoped to the authenticated `UserId`.
   - Metadata filters (mood, themes, energy, optional artist/title).
   - Candidate diversification.
   - LLM selection and sequencing with strict validation.
   - Mapping internal `TrackId` to the user’s `LocalTrackId`.

5. **Backfill**
   - Private Catalog gRPC `ListUserTracks`.
   - Lazy per-user backfill when PlaylistRag has no mapping for that user.
   - `POST /api/playlists/reindex` for explicit rebuild.

6. **Tests and documentation**
   - New `tests/Lyra.PlaylistRag.Api.Tests`.
   - Testcontainers PostgreSQL with pgvector.
   - MassTransit in-memory harness tests.
   - Endpoint tests with fake chat/embedding clients.
   - `README.md`, `docs/ARCHITECTURE.md`, `CHANGELOG.md`, `.http`, `.env.example`.

**Out of Scope**

- Low-confidence LRCLIB lyrics fallback. The Worker remains hard-threshold.
- Playlist persistence/history.
- Collaborative or shared playlists.
- Per-user embedding models with different dimensions.
- Mobile/Flutter changes.
- Additional vector databases or semantic-search libraries.
- Automatic retry of a failed LLM selection inside the request.

### User Stories

- As a listener, I can describe a mood or occasion and receive a playlist made only from tracks I own.
- As a listener, I can play every returned track because the response contains my own `LocalTrackId`.
- As a listener whose library existed before PlaylistRag, I can still generate a playlist after a one-time backfill.
- As a listener with no AI provider configured, I get a clear action (“Connect an AI provider”) instead of an empty or partial result.
- As a listener with no indexed tracks, I get a clear status instead of invented tracks.
- As an operator, I can see why a generation failed from structured logs and problem details.

### Functional Requirements

#### Event ingestion

- `SongImportedEvent` upserts `UserTrack` by `(UserId, TrackId)`.
- `SongImportedEvent` is the only source of `LocalTrackId` ownership.
- `SongEnrichedEvent` upserts one global `TrackEmbedding` per `TrackId`.
- Duplicate events are idempotent.
- An embedding failure throws and lets MassTransit retry; no empty vector is stored.
- Endpoint names are explicit and unique:
  - `playlist-rag-song-imported`
  - `playlist-rag-song-enriched`

#### Embedding content

Compose embedding text from:

- resolved title, artist, album;
- primary mood;
- themes;
- energy level;
- semantic summary.

Do not embed full lyrics.

Use `ResolvedMetadata` when present; otherwise use the event’s local metadata values.

#### Playlist generation

- Require a valid JWT with `userId`.
- Validate prompt length and limit.
- Load the user’s AI connection from Identity’s private gRPC service.
- Rewrite the prompt into a structured query plan.
- Validate the plan.
- Embed the semantic query.
- Search only the caller’s tracks.
- Apply metadata filters.
- Diversify by artist.
- Select and sequence candidates with the user’s chat model.
- Validate every returned candidate index.
- Return the caller’s `LocalTrackId`s.

#### No fallback

- No lyrics fallback.
- No “top similarity” playlist when the LLM fails.
- No silent provider substitution.
- No partial success with invalid model output.
- No response containing a track the user does not own.

### Non-Functional Requirements

- **Ownership:** every playlist query is scoped by authenticated `UserId`.
- **Isolation:** PlaylistRag never reads Catalog or Identity databases.
- **Consistency:** one embedding model and dimension per deployment.
- **Idempotency:** duplicate events do not create duplicate rows.
- **Cancellation:** propagate `CancellationToken` through all async I/O.
- **Security:** never log API keys, full prompts, or lyrics; validate all model output.
- **Observability:** log event consumption, embedding duration/model, candidate counts, generation failure reason, and selected count.
- **Performance:** one embedding per global track; one vector query per prompt; bounded candidate set.
- **Reliability:** retries for transient event failures; clear HTTP errors for request-time failures.
- **Tests:** no live AI, LRCLIB, or embedding provider calls.

---

# Technical Design

### Current Implementation

- `Lyra.PlaylistRag.Api` is a stub with `/api/playlists/ping`.
- `vector_db` exists and has the `vector` extension.
- `Lyra.AI` has chat factories only; no embedding generator.
- Identity exposes `GetAiConnection` over private gRPC.
- Catalog publishes `SongImportedEvent`.
- Worker publishes `SongLyricsFetchedEvent` and `SongEnrichedEvent`.
- `SongEnrichedEvent` already carries `ResolvedMetadata`.
- Gateway routes `/api/playlists/*` to port `5003`.

### Key Decisions

1. **One deployment-wide embedding model.**  
   Use a fixed provider/model/dimension for all users. Recommended default: OpenCode Go with `text-embedding-3-small`, 1536 dimensions, using a deployment-level `Rag__ApiKey`. This provides one vector space and one HNSW index.

2. **Use `SongImportedEvent` for ownership.**  
   `SongEnrichedEvent.UserId` is the user who triggered enrichment, not every owner of the track. PlaylistRag must build its own per-user mapping from `SongImportedEvent`.

3. **Consume both events.**  
   `SongImportedEvent` → `user_tracks`; `SongEnrichedEvent` → `track_embeddings`. One global embedding serves all owners.

4. **Backfill through Catalog gRPC.**  
   Existing libraries have no `SongImportedEvent` history. Add a private `CatalogLibraryService.ListUserTracks` and lazy per-user backfill.

5. **Share the Identity gRPC client.**  
   Move the Worker’s `IdentityUserAiConnectionClient` and related options into `Lyra.AI` under an `Identity` namespace; update the Worker; use it from PlaylistRag.

6. **Hard failure policy.**  
   No fallback playlist, no fallback query plan, no fallback provider. Fail with a problem+json response.

7. **Unique endpoint names.**  
   PlaylistRag queues must not collide with `SongImported` (Worker) or `SongEnriched` (Catalog).

8. **Candidate indices in the LLM prompt.**  
   Use short indices (`1..N`) instead of GUIDs to reduce prompt size and invalid IDs.

### Proposed Changes

#### `Lyra.Contracts`

- Add `Protos/catalog_library.proto` for the backfill service.
- No changes required to `SongImportedEvent` or `SongEnrichedEvent`.
- Keep playlist request/response DTOs in PlaylistRag, since they are not shared service contracts.

#### `Lyra.AI`

- Add `IAiEmbeddingGeneratorFactory`.
- Add provider-specific embedding adapters:
  - OpenCode Go first (`text-embedding-3-small`).
  - OpenAI available as an alternate deployment provider.
  - Gemini next, if required.
  - OpenCode only if the selected plan/model supports embeddings.
  - Anthropic: throw a clear “embeddings not supported” error.
- Add `AiEmbeddingOptions`:
  - request timeout;
  - max input characters;
  - expected dimensions.
- Validate generated vector length before returning.
- Move shared Identity AI-connection client/options here.

#### `Lyra.PlaylistRag.Api` — data layer

- `Data/VectorDbContext.cs`
- `Models/UserTrack.cs`
- `Models/TrackEmbedding.cs`
- `Repositories/IUserTrackRepository.cs`
- `Repositories/UserTrackRepository.cs`
- `Repositories/ITrackEmbeddingRepository.cs`
- `Repositories/TrackEmbeddingRepository.cs`
- `Migrations/*_InitialVectorDb.cs`

#### `Lyra.PlaylistRag.Api` — event ingestion

- `Consumers/SongImportedConsumer.cs`
- `Consumers/SongEnrichedConsumer.cs`
- `Services/Embeddings/ITrackEmbeddingService.cs`
- `Services/Embeddings/TrackEmbeddingService.cs`
- MassTransit registration with explicit endpoint names, retry, and low concurrency.

#### `Lyra.PlaylistRag.Api` — playlist generation

- `Endpoints/PlaylistEndpoints.cs`
- `DTOs/Requests/GeneratePlaylistRequest.cs`
- `DTOs/Responses/GeneratePlaylistResponse.cs`
- `DTOs/Responses/PlaylistTrackResponse.cs`
- `Services/Playlists/IPlaylistGenerationService.cs`
- `Services/Playlists/PlaylistGenerationService.cs`
- `Services/Playlists/IPlaylistQueryPlanner.cs`
- `Services/Playlists/PlaylistQueryPlanner.cs`
- `Services/Playlists/IPlaylistCandidateSearch.cs`
- `Services/Playlists/PgvectorPlaylistCandidateSearch.cs`
- `Services/Playlists/IPlaylistSelector.cs`
- `Services/Playlists/PlaylistSelector.cs`
- `Services/Playlists/IPlaylistResponseMapper.cs`
- `Services/Playlists/PlaylistResponseMapper.cs`
- `Services/Backfill/ICatalogLibraryClient.cs`
- `Services/Backfill/CatalogLibraryGrpcClient.cs`
- `Services/Backfill/IUserLibraryBackfillService.cs`
- `Services/Backfill/UserLibraryBackfillService.cs`

#### `Lyra.Catalog.Api` — backfill server

- `Grpc/CatalogLibraryGrpcService.cs`
- `Grpc/InternalServiceAuthenticationInterceptor.cs` (or reuse the Identity pattern)
- Private gRPC listener on `5005`.
- Repository method to page `user_tracks` for a user.

### Data Models / Contracts

#### `UserTrack`

```csharp
public sealed class UserTrack
{
    public Guid UserId { get; set; }
    public Guid TrackId { get; set; }
    public string LocalTrackId { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;
    public string Artist { get; set; } = string.Empty;
    public string? Album { get; set; }
    public int DurationSeconds { get; set; }

    public DateTimeOffset ImportedAt { get; set; }
}
```

Indexes:

- PK `(UserId, TrackId)`
- unique `(UserId, LocalTrackId)`
- `(UserId, ImportedAt)`

#### `TrackEmbedding`

```csharp
public sealed class TrackEmbedding
{
    public Guid TrackId { get; set; }

    public string Title { get; set; } = string.Empty;
    public string Artist { get; set; } = string.Empty;
    public string? Album { get; set; }
    public int DurationSeconds { get; set; }

    public string? PrimaryMood { get; set; }
    public string[] Themes { get; set; } = [];
    public int? EnergyLevel { get; set; }
    public string SemanticSummary { get; set; } = string.Empty;

    public Vector Embedding { get; set; } = new float[1536];
    public string EmbeddingModel { get; set; } = string.Empty;
    public int EmbeddingDimensions { get; set; }

    public DateTimeOffset EnrichedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
```

Indexes:

- PK `TrackId`
- HNSW cosine index on `Embedding`
- `PrimaryMood`
- `EnergyLevel`
- GIN on `Themes`

#### `PlaylistQueryPlan`

```csharp
public sealed record PlaylistQueryPlan(
    string SemanticQuery,
    IReadOnlyList<string> Moods,
    IReadOnlyList<string> Themes,
    int? MinEnergy,
    int? MaxEnergy,
    string? Artist,
    string? Title,
    string? PlaylistTitle);
```

#### `GeneratePlaylistRequest`

```csharp
public sealed record GeneratePlaylistRequest(string Prompt, int? Limit);
```

#### `GeneratePlaylistResponse`

```csharp
public sealed record GeneratePlaylistResponse(
    string Title,
    string Description,
    IReadOnlyList<PlaylistTrackResponse> Tracks);

public sealed record PlaylistTrackResponse(
    string LocalTrackId,
    string Title,
    string Artist,
    string? Album,
    int DurationSeconds,
    string? Reason);
```

### File Structure

```text
Lyra.PlaylistRag.Api/
  Consumers/
    SongImportedConsumer.cs
    SongEnrichedConsumer.cs
  Data/
    VectorDbContext.cs
  DTOs/
    Requests/GeneratePlaylistRequest.cs
    Responses/GeneratePlaylistResponse.cs
    Responses/PlaylistTrackResponse.cs
  Endpoints/
    PlaylistEndpoints.cs
  Migrations/
  Models/
    UserTrack.cs
    TrackEmbedding.cs
  Repositories/
  Services/
    Backfill/
    Embeddings/
    Playlists/
  Program.cs
  appsettings.json

Lyra.AI/
  Embeddings/
    IAiEmbeddingGeneratorFactory.cs
    AiEmbeddingGeneratorFactory.cs
    AiEmbeddingOptions.cs
    Providers/
  Identity/
    IUserAiConnectionClient.cs
    IdentityUserAiConnectionClient.cs
    IdentityOptions.cs
    InternalServicesOptions.cs

Lyra.Contracts/Protos/
  catalog_library.proto

tests/Lyra.PlaylistRag.Api.Tests/
  ...
```

### Architecture Diagram

```text
Catalog import
   │
   ├── SongImportedEvent ──▶ playlist-rag-song-imported
   │                              │
   │                              ▼
   │                         user_tracks
   │
Worker enrichment
   │
   ├── SongEnrichedEvent ──▶ playlist-rag-song-enriched
                                  │
                                  ▼
                            embedding generator
                                  │
                                  ▼
                            track_embeddings

Client ── POST /api/playlists/generate ──▶ PlaylistRag
                                              │
                                              ├── Identity gRPC: AI connection
                                              ├── LLM: prompt → query plan
                                              ├── embedding: semantic query
                                              ├── pgvector: user-scoped search
                                              ├── LLM: select/sequence
                                              └── user_tracks: LocalTrackId
```

### Risks

- **Embedding model changes require reindexing.** Store `EmbeddingModel` and `EmbeddingDimensions`; changing either requires a rebuild.
- **Anthropic has no embeddings.** Use the deployment-level embedding provider.
- **Existing libraries need backfill.** Without it, only newly imported tracks are searchable.
- **LLM hallucinations.** Candidate indices are validated; invalid output fails the request.
- **No fallback means visible failures.** Provider outages return errors rather than degraded playlists.
- **Event ordering.** `SongEnrichedEvent` may arrive before `SongImportedEvent`; the join by `TrackId` handles it, but the user may temporarily miss a track.
- **HNSW with joins/filters.** Query plans may need tuning on large libraries.
- **Prompt privacy.** Log length/hash, not full prompts.
- **Queue name collisions.** Explicit endpoint names are mandatory.

---

# Testing

### Validation Approach

- Unit tests for query planning, validation, scoring/selection, and mapping.
- Integration tests against a real PostgreSQL + pgvector instance using Testcontainers.
- MassTransit in-memory harness for event consumers.
- Endpoint tests with fake `IChatClient` and `IEmbeddingGenerator`.
- No live provider, LRCLIB, or embedding calls.

### Key Scenarios

1. `SongImportedEvent` stores the user’s `LocalTrackId`.
2. Duplicate `SongImportedEvent` is idempotent.
3. `SongEnrichedEvent` stores one global embedding.
4. Duplicate `SongEnrichedEvent` is idempotent.
5. Embedding failure retries and does not store an empty vector.
6. Vector search returns only the caller’s tracks.
7. Metadata filters restrict results correctly.
8. Candidate diversification caps tracks per artist.
9. Prompt plan validation rejects invalid ranges/values.
10. LLM selection with an invalid index returns `502` and no playlist.
11. LLM selection with duplicate indices returns `502`.
12. LLM selection with a track the user does not own returns `502`.
13. No AI connection returns `409`.
14. No indexed tracks returns `409`.
15. Empty filter result returns `200` with no tracks.
16. Prompt injection cannot escape the structured schema.
17. Backfill pages through Catalog and creates mappings.
18. Reindex is idempotent.
19. Anonymous request returns `401`.
20. Prompt/limit validation returns `400`.

### Test Changes

- Add `tests/Lyra.PlaylistRag.Api.Tests` to `Lyra.slnx`.
- Extend `PostgresTestFixture` or add a vector-specific fixture using `pgvector/pgvector:pg17`.
- Add fake `IChatClient` and fake embedding generator.
- Add a fake Identity gRPC client.
- Add a fake Catalog backfill client.
- Update `README.md`, `docs/ARCHITECTURE.md`, `CHANGELOG.md`, and `.http`.

---

# Delivery Steps

### Step 0: Align the Worker with the hard threshold

Prerequisite, not part of RAG:

- Remove the low-confidence LRCLIB fallback if it is still present.
- Keep `FuzzyMatchThreshold` as a hard threshold.
- Candidates below the threshold are not selected; tracks continue with metadata-only enrichment when no candidate passes.
- Update Worker tests/docs accordingly.

### Step 1: Add the vector data layer

- Add `VectorDbContext`, `UserTrack`, `TrackEmbedding`.
- Add the initial migration and pgvector extension/index.
- Add `ConnectionStrings__VectorDb` and startup validation.
- Add the PlaylistRag test project.
- Acceptance: migration applies; an integration test can insert and query a vector.

### Step 2: Consume import and enrichment events

- Add `SongImportedConsumer` and `SongEnrichedConsumer`.
- Add explicit endpoint names and retry configuration.
- Add repositories/services and idempotent upserts.
- Acceptance: MassTransit harness writes the expected rows.

### Step 3: Add embedding support to `Lyra.AI`

- Add `IAiEmbeddingGeneratorFactory`.
- Implement OpenCode Go embeddings with `text-embedding-3-small` (Go endpoint, `x-opencode-session`). Keep OpenAI as an alternate deployment provider.
- Validate input length and output dimensions.
- Add sanitized error handling.
- Acceptance: dimension mismatch is rejected; fake generator tests pass.

### Step 4: Implement playlist generation

- Add request/response DTOs and endpoint.
- Implement query planner, vector search, diversification, selection, validation, mapping.
- Add problem+json error mapping.
- Acceptance: endpoint tests cover happy path and failure policy.

### Step 5: Add backfill

- Add Catalog gRPC `ListUserTracks` on the private port.
- Add PlaylistRag client and lazy backfill.
- Add `POST /api/playlists/reindex`.
- Acceptance: an existing library becomes searchable without re-import.

### Step 6: Frontend, docs, and end-to-end verification

- Add a “Create playlist” page or panel.
- Prompt input, loading, error, playlist result, play queue.
- Update documentation and examples.
- Verify: import → enrich → embed → generate → play.

---

# Acceptance Criteria

- [ ] `vector_db` migrations create `user_tracks`, `track_embeddings`, `vector` extension, and HNSW index.
- [ ] `SongImportedEvent` populates user-scoped `LocalTrackId` mappings.
- [ ] `SongEnrichedEvent` creates one global embedding per track.
- [ ] Duplicate events are idempotent.
- [ ] Embedding failures retry and never store empty vectors.
- [ ] `POST /api/playlists/generate` requires authentication.
- [ ] Queries are strictly scoped to the authenticated `UserId`.
- [ ] Returned tracks contain the user’s own `LocalTrackId`.
- [ ] No AI connection returns `409`.
- [ ] No indexed tracks returns `409`.
- [ ] Invalid LLM output returns `502`; no fallback playlist is returned.
- [ ] Planner/embedding/search failures return clear problem details.
- [ ] Backfill works for libraries imported before this milestone.
- [ ] Unit, integration, MassTransit, and endpoint tests pass.
- [ ] Documentation and `.http` examples are updated.
