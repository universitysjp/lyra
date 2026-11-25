# Architecture Decision Records

This folder records the decisions that shape Lyra: service boundaries, data ownership, message contracts, external dependencies, security posture, deployment topology, and repository layout.

An ADR explains **why** the system looks the way it does. It is not a tutorial and it is not a changelog. If you want to know *what* a service does, read [`../ARCHITECTURE.md`](../ARCHITECTURE.md).

## Rules

- One decision per file: `ADR-nnnn-short-slug.md`, using the next free number.
- Use [`ADR-template.md`](ADR-template.md).
- Statuses: `Proposed`, `Accepted`, `Rejected`, `Superseded by ADR-nnnn`.
- Never rewrite an accepted decision. Write a new ADR that supersedes it and change the old ADR's status line to `Superseded by ADR-nnnn`.
- A decision nobody has confirmed yet is `Proposed`, with the open question written down.
- Add the row to the index below in the same commit that adds the ADR.

## When an ADR is required

- Adding, splitting, or removing a service or project.
- Changing what a service owns (database, schema, cache, queue).
- Changing an event or gRPC contract in a backwards-incompatible way.
- Introducing or replacing infrastructure (database, broker, cache, AI provider abstraction).
- Changing authentication, authorization, secret handling, or network exposure.
- Changing repository layout or build/deploy topology.
- Choosing to deliberately *not* build something that the docs previously described.

Bug fixes, refactors inside one service, and documentation-only changes do not need an ADR.

## Index

| ADR | Title | Status | Date |
| --- | --- | --- | --- |
| [0001](ADR-0001-adopt-docs-first-and-adr-workflow.md) | Adopt docs-first workflow and ADRs | Accepted | 2026-11-24 |
| [0002](ADR-0002-web-client-lives-in-clients.md) | Web client lives in `clients/`, not `prototype/` | Accepted | 2026-11-24 |
| [0003](ADR-0003-playlist-rag-remains-planned.md) | `Lyra.PlaylistRag.Api` remains planned until implemented | Superseded by ADR-0004 | 2026-11-24 |
| [0004](ADR-0004-implement-rag-playlist-generation.md) | Implement RAG playlist generation in `Lyra.PlaylistRag.Api` | Proposed | 2026-11-25 |
