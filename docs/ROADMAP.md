# Luma delivery roadmap

| Milestone | Scope | Acceptance |
| --- | --- | --- |
| Phase 1 — native library | Source identity, read-only scanning, thumbnails, search/tags/favorites, native file actions | Merged; Windows/Linux core, Windows integration and native UI smoke passed; real-device acceptance remains |
| Phase 2 — offline browsing | Bulk preparation, cancellation/resume, non-evicting preparation, coverage/usage, cached folders, cache clearing | Implemented on the Phase 2 branch; exact CI evidence is recorded in QA and the pull request |
| Phase 3 — gallery redesign | Apple-inspired native shell, continuous gallery, customization/sorting, meaningful timeline, albums and Windows file control | Implemented on the gallery branch; [build guide](GALLERY_BUILD.md) records behavior, migration and acceptance limits; CI evidence accompanies the PR |
| Phase 4 — scale and delivery | Measured 100k-item workloads, watcher/reconciliation strategy, responsive native file operations, signing/installer | Proposed; requires benchmarks and real-PC acceptance before release claims |

## Immediate next steps

1. Review the Phase 2 CI evidence and download its complete Windows artifact.
2. Run the removable-drive, limited-capacity, cancellation/restart and cache-clearing acceptance sequence in [QA](QA.md) using disposable fixtures.
3. Merge the Phase 2 pull request once its checks pass and acceptance is satisfactory.
4. Test the new gallery artifact using [the build guide](GALLERY_BUILD.md), review its CI/scale evidence, then complete physical-drive and native-operation acceptance before a general release.

The latest user testing changes the priority: a comfortable gallery and continuous browsing come before further expansion of the existing maintenance-heavy screen. Default to month-grouped chronological browsing, while preserving an optional plain grid and native Windows file-management conventions.

No milestone currently includes an embedded media player, cloud synchronization or copying full originals into the preview cache. Preview pinning needs a separate capacity/reservation design rather than an unlimited promise of retention.
