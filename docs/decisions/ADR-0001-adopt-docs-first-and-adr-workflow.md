# ADR-0001: Adopt a docs-first workflow and architecture decision records

- **Status:** Accepted
- **Date:** 2026-11-24
- **Deciders:** owner (repository maintainer)

## Context

Lyra has seven .NET projects, a message broker, three databases, a private gRPC channel, Redis, and an AI provider boundary. The documented behaviour in `README.md` and `docs/ARCHITECTURE.md` had already drifted from the code (`Lyra.PlaylistRag.Api` was described as a working RAG service while it only exposed `/ping`). Contributors and coding agents were working from memory and from whichever file they happened to open, so the drift kept growing.

Pull requests also had no consistent record of *why* a boundary exists, which makes the architecture easy to erode one small change at a time.

## Decision

1. Read the existing documentation before changing code, and update the relevant document in the **same commit** as the code.
2. Record every expensive-to-reverse decision as an ADR in `docs/decisions/`, with status, context, decision, consequences, and rejected alternatives.
3. Log every change as a row in `CHANGELOG.md` in the same commit.
4. Treat the code as the source of truth: when code and docs disagree, fix the docs in the same change.

## Consequences

- Every change is slightly larger because the doc update and changelog row are part of it. Reviewers can see intent without archaeology.
- Architecture questions have a durable answer that survives staff changes and context loss.
- `docs/decisions/` becomes a real review surface. A proposal can be argued about before code exists.
- Requires discipline and a PR check. Without enforcement the changelog will rot like the docs did.

## Alternatives considered

- **Wiki or issue tracker only.** Rejected: not versioned with the code, not reviewable in the same diff, and drifts silently.
- **Commit messages as the record.** Rejected: a commit explains a delta, not the standing rule. Readers would have to reconstruct the decision from history.
- **No process, rely on code review.** Rejected: that is the status quo that produced the drift.
