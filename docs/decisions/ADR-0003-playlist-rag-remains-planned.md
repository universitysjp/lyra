# ADR-0003: `Lyra.PlaylistRag.Api` stays planned until it is implemented

- **Status:** Proposed
- **Date:** 2026-11-24
- **Deciders:** owner (repository maintainer) — confirmation needed

## Context

`README.md` and `docs/ARCHITECTURE.md` describe `Lyra.PlaylistRag.Api` as a working service: it consumes `SongEnrichedEvent`, writes embeddings to `vector_db` with an HNSW cosine index, and serves `POST /api/playlists/generate`.

The project itself is a 727-byte `Program.cs` with JWT wiring and one `/ping` endpoint. Its `.csproj` references `MassTransit.RabbitMQ`, `Microsoft.EntityFrameworkCore.Design`, `Npgsql.EntityFrameworkCore.PostgreSQL`, `Pgvector.EntityFrameworkCore`, and `Microsoft.Extensions.AI` — none of which the code uses. There is no `DbContext`, no consumer, no embedding call, and no test project. `CHANGELOG.md` correctly lists the feature under **Planned**.

Two options: implement the service now, or make the documentation honest and keep the skeleton until the feature is scheduled.

## Decision

Keep `Lyra.PlaylistRag.Api` as a stub, mark every description of it as **planned** in `README.md` and `docs/ARCHITECTURE.md`, and remove package references the stub does not use. Implementing RAG playlists is its own scoped change with its own ADRs (embedding provider, vector schema, prompt strategy).

## Consequences

- Readers stop believing a feature exists, and nobody builds a client against a `POST /api/playlists/generate` that returns 404.
- The Gateway route and JWT wiring stay in place, so adding the service later is additive.
- The stub keeps compiling and stays in `Lyra.slnx`.
- **Open question for the owner:** is RAG playlist generation still on the roadmap for this milestone? If not, decide whether the project should be removed from the solution instead of shipping a live-but-empty service.
- Follow-up work when it is built: choose a system-level embedding provider rather than relying on each user's BYOK connection, and record the `EmbeddingModel`/dimensions alongside each vector.

## Alternatives considered

- **Implement it now.** Rejected for this change: it needs product decisions (embedding provider, cost ownership, prompt/filter strategy) that are not made, and it would dwarf the documentation fix.
- **Delete the project.** Rejected: the Gateway route, JWT wiring, and `${vector_db}` are already provisioned, and the stub is the agreed landing place for the feature.
- **Leave the docs as they are.** Rejected: that is the drift this ADR exists to stop.
