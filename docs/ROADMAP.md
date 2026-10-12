# Luma delivery roadmap

Status: the portable gallery development milestone is closed and merged into `main` through [PR #6](https://github.com/smile-plzz/Luma/pull/6). The first real-use follow-up, [PR #7](https://github.com/smile-plzz/Luma/pull/7), is also merged and shipped: import-lock correction and minimal UI refinement. [PR #8](https://github.com/smile-plzz/Luma/pull/8) is also merged and shipped: catalog-first reopening and reconnect updates that preserve the gallery. [PR #9](https://github.com/smile-plzz/Luma/pull/9) is merged and verified: resumable indexing and On this day memories. The current stage is real-life testing and everyday use. No additional feature phase is being started automatically.

| Milestone | Scope | Status |
| --- | --- | --- |
| Phase 1 — native library | Source identity, read-only scanning, thumbnails, search/tags/favorites, native file actions | Merged |
| Phase 2 — offline browsing | Bulk preparation, cancellation/resume, coverage, cached folders, cache clearing | Merged |
| Phase 3 — gallery redesign | Continuous gallery, customization/sorting, timeline, albums and Windows file control | Merged; automated gates passed; portable Windows shipment available |
| Real-life testing | Personal media, physical drives, Windows file interactions and daily usability | Current stage; user feedback pending |
| Future delivery work | External-move reconciliation, measured full-library responsiveness, signing/installer | Deferred; prioritize from real-world feedback |

## Current handoff

Use [TESTING_HANDOFF.md](TESTING_HANDOFF.md) for the exact tested commit, download, upgrade instructions, known limits and feedback format. [GALLERY_BUILD.md](GALLERY_BUILD.md) describes current behavior; [QA.md](QA.md) separates automated evidence from manual acceptance.

Keep the shipped code stable while testing. Prioritize reported data-integrity or startup failures first, then recurring browsing/performance problems, then visual/usability refinements. A future fix should reproduce the report, receive appropriate regression checks, and identify its own tested build before shipment.

The app opens originals in default applications. Cloud accounts, an embedded player/editor, automatic original-file rearrangement and full-original backup are outside this milestone. Preview pinning needs a separate capacity design. Signing and an installer are not required to begin testing the unsigned portable build.

## Resumable indexing and memory browsing

Implemented persistent indexing checkpoints with resume/restart/report controls and atomic catalog publication, plus On this day capture-date browsing with date selection and offline previews. All automated checks passed (58 core cases per OS, four Windows integration cases and native desktop smoke). The downloadable shipment is tracked in TESTING_HANDOFF.md. Smart collections, pinned previews and installer/update notifications remain separate future work.
