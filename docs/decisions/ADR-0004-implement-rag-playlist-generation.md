# ADR-0004: Implement RAG playlist generation in `Lyra.PlaylistRag.Api`

- **Status:** Proposed
- **Date:** 2026-11-25
- **Deciders:** owner (repository maintainer) — confirmation needed on provider cost ownership and roadmap

## Context

[ADR-0003](ADR-0003-playlist-rag-remains-planned.md) kept `Lyra.PlaylistRag.Api` as a stub and explicitly deferred three decisions to their own ADRs: the embedding provider, the vector schema, and the prompt/filter strategy. The `songs-enrichment-worker` branch implements all three, and the merge of `main` brings that implementation into the documented system. The code is the fact, so the documentation and the ADR trail have to catch up.

The implementation must preserve the boundaries already established: database-per-service (`vector_db` is owned only by `Lyra.PlaylistRag.Api`), per-user ownership filtering, BYOK AI connections resolved over Identity's private gRPC service, and no silent fallbacks when the model or provider fails.

## Decision

- **Ownership comes from imports, not enrichment.** `SongImportedConsumer` records `(UserId, TrackId) → LocalTrackId` in `user_tracks`. `SongEnrichedEvent.UserId` identifies the user who triggered enrichment, so it is never used as an ownership source.
- **Embeddings are deployment-wide.** `SongEnrichedConsumer` embeds each global track once with OpenCode Go `text-embedding-3-small` (1536 dimensions, `Rag__ApiKey`), storing the vector in `track_embeddings` with a `vector(1536)` column and an `HNSW` cosine-distance index. Per-user BYOK connections are used only for prompt planning and playlist selection.
- **`POST /api/playlists/generate` is a strict, user-scoped pipeline.** Resolve the caller's AI connection over Identity gRPC (`409` when missing), rewrite the prompt into a validated query plan (semantic query, moods, themes, energy range, optional artist/title, title/description), embed the semantic query, run a `pgvector` cosine search joined through `user_tracks` so results are strictly scoped to the authenticated `UserId`, apply energy and explicit artist/title as hard filters, use moods/themes as soft ranking signals, cap repeat artists, and bound the candidate set. The user's chat model then receives one-based candidate indices and selects/sequences the playlist; count, range, uniqueness, and reasons are validated.
- **Failures are explicit.** Invalid requests return `400`, missing authentication `401`, missing AI connection or unindexed library `409`, invalid model output `502`, and transient AI/Identity failures `503`. There is no similarity-only or low-confidence fallback playlist.
- **Responses contain only `LocalTrackId`s.** The backend never resolves or returns an audio file location.

Because the milestone was implemented before this record was written, this ADR is `Proposed`: the owner still has to confirm that the deployment-wide embedding provider and its cost are accepted, and that RAG playlist generation belongs in this milestone.

## Consequences

- `Lyra.PlaylistRag.Api` is an active service: it needs RabbitMQ, `vector_db`, `Rag__ApiKey`, and `InternalServices__ApiKey` to start, and it runs EF Core migrations at startup outside Testing.
- It owns two consumers, two tables, and a new REST endpoint, with test coverage for the consumers, vector data layer, query planner, selector, candidate search, and endpoints.
- The ownership map makes playlist generation correct when several users own the same global track.
- Documentation (`README.md`, `docs/ARCHITECTURE.md`, `AGENTS.md`) describes the service as implemented, replacing the "planned" text introduced by ADR-0003.
- Operational cost of embeddings is borne by the deployment, not by each user; track enrichment is embedded once globally, which keeps cost proportional to unique tracks rather than user libraries.

## Alternatives considered

- **Keep the stub (ADR-0003).** Rejected: the implementation exists, is tested, and is the point of the branch being merged.
- **Use each user's BYOK connection for embeddings.** Rejected: identical global tracks would be embedded repeatedly at each user's cost, and vectors would not be comparable across different embedding models.
- **Similarity-only fallback when the LLM fails.** Rejected: the product requirement is a coherent, explained playlist; a silent fallback would hide provider failures and return arbitrary tracks.
- **Deterministic metadata filters only.** Rejected: prompts such as "something for a rainy drive" need semantic search over enrichment summaries, not only mood/energy columns.
