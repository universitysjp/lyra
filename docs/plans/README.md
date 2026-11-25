# Implementation Plans

This folder keeps historical, agent-generated implementation plans for provenance: they record how a milestone was actually planned and built, including scope decisions, testing scenarios, and delivery steps. They are **not** the standing description of the system — for what Lyra does today, read [`../ARCHITECTURE.md`](../ARCHITECTURE.md), and for why it is shaped that way, read [`../decisions/README.md`](../decisions/README.md). Treat these files as snapshots: some details may be out of date, and they were originally written inside the `.junie/` tool directory before being moved here.

| Plan | Covers |
| --- | --- |
| [identity-authentication-foundation.md](identity-authentication-foundation.md) | The catalog ingestion slice built on the completed identity foundation: global `tracks` + `user_tracks`, batch import with `TrackFingerprint` deduplication, `pg_trgm` search, and Redis cache-aside reads. |
| [byok-ai-enrichment-pipeline.md](byok-ai-enrichment-pipeline.md) | Bring-your-own-key AI: per-user encrypted provider connections in Identity, the `Lyra.AI` adapters, and the `Lyra.Enrichment.Worker` lyrics + structured-tag pipeline feeding enriched tracks back into Catalog. |
