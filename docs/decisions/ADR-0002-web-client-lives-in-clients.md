# ADR-0002: Keep the React web client in `clients/`, not `prototype/`

- **Status:** Accepted
- **Date:** 2026-11-24
- **Deciders:** owner (repository maintainer)

## Context

`prototype/lyra-player` is a React 19 + Vite single-page app used to exercise the backend end to end: authentication, AI connection settings, batch import, library browsing, search, lyrics, and local audio playback through IndexedDB. It has 98 tracked files and its own Vitest suite, and it keeps receiving features in the changelog.

The folder name says "disposable", which no longer matches reality. It also hides the client from tooling and CI, because "prototype" is where throwaway spikes belong. The Flutter mobile app remains the product client; the web app is the development and reference client.

## Decision

Move the web client to `clients/lyra-player/` and keep `prototype/` for genuinely disposable spikes. `clients/<name>/` is the home for every non-.NET client, including the future Flutter app (`clients/lyra-mobile/`).

## Consequences

- The client is discoverable in the repository tree and can be covered by CI (typecheck, tests, production build) without special-casing a "prototype" path.
- README, `AGENTS.md`, and the client's own docs must point at the new path.
- Node binaries (`node_modules/`, 100+ MB) stay untracked; the lockfile is the reproducible artefact.
- Vite's `/api` dev proxy still hides the Gateway's lack of CORS in development. Hosting the built client separately would require a CORS policy on the Gateway — tracked as a separate decision.

## Alternatives considered

- **Leave it in `prototype/`.** Rejected: the name misleads contributors and agents, and CI would have to special-case it.
- **Move it into the solution root as a bare folder.** Rejected: mixes runtimes at the top level and leaves no clear parent for the Flutter client.
- **Delete it and rely on curl / the Flutter app.** Rejected: it is the only UI that exercises lyrics, enrichment status, and relinking local files end to end.
