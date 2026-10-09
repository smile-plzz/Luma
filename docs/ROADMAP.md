# Luma delivery roadmap

| Milestone | Scope | Acceptance |
| --- | --- | --- |
| Phase 1 — native library | Source identity, read-only scanning, thumbnails, search/tags/favorites, native file actions | Merged; Windows/Linux core, Windows integration and native UI smoke passed; real-device acceptance remains |
| Phase 2 — offline browsing | Bulk preparation, cancellation/resume, non-evicting preparation, coverage/usage, cached folders, cache clearing | Implemented on the Phase 2 branch; exact CI evidence is recorded in QA and the pull request |
| Phase 3 — metadata and organization | Incremental capture date, dimensions/duration, explicit timeline date semantics, albums and organization that survives in-app moves | Proposed next implementation milestone; existing catalogs must upgrade safely and unsupported metadata must fall back clearly |
| Phase 4 — scale and delivery | Measured 100k-item workloads, watcher/reconciliation strategy, responsive native file operations, signing/installer | Proposed; requires benchmarks and real-PC acceptance before release claims |

## Immediate next steps

1. Review the Phase 2 CI evidence and download its complete Windows artifact.
2. Run the removable-drive, limited-capacity, cancellation/restart and cache-clearing acceptance sequence in [QA](QA.md) using disposable fixtures.
3. Merge the Phase 2 pull request once its checks pass and acceptance is satisfactory.
4. Begin Phase 3 with a versioned metadata store and incremental extraction only for changed files. Test missing EXIF, timezone ambiguity, rotated images and video metadata before changing timeline ordering.

No milestone currently includes an embedded media player, cloud synchronization or copying full originals into the preview cache. Preview pinning needs a separate capacity/reservation design rather than an unlimited promise of retention.
