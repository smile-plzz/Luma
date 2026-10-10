# Luma delivery roadmap

| Milestone | Scope | Acceptance |
| --- | --- | --- |
| Phase 1 — native library | Source identity, read-only scanning, thumbnails, search/tags/favorites, native file actions | Merged; Windows/Linux core, Windows integration and native UI smoke passed; real-device acceptance remains |
| Phase 2 — offline browsing | Bulk preparation, cancellation/resume, non-evicting preparation, coverage/usage, cached folders, cache clearing | Merged; exact CI evidence is recorded in QA and the pull request |
| Phase 3 — gallery redesign | Apple-inspired native shell, continuous gallery, customization/sorting, meaningful timeline, albums and Windows file control | Implemented on the gallery branch; [build guide](GALLERY_BUILD.md) records behavior, migration and acceptance limits; CI evidence accompanies the PR |
| Phase 4 — scale and delivery | Measured 100k-item workloads, watcher/reconciliation strategy, responsive native file operations, signing/installer | Proposed; requires benchmarks and real-PC acceptance before release claims |

## Immediate next steps

1. Download the complete **Luma-Gallery-win-x64** artifact from the successful gallery workflow and extract it before launching `Luma.App.exe`.
2. Review the gallery pull request, build guide and attached CI/scale evidence; the implementation is on `feat/gallery-redesign` until merged.
3. Run the physical-drive reconnect, native file-operation and real-world photo/video acceptance checks in [QA](QA.md) and [the build guide](GALLERY_BUILD.md) on disposable bench fixtures.
4. Use those results to scope Phase 4 delivery work, including external-move reconciliation, signing and an installer. These remain separate from the completed portable gallery build.

The latest user testing changes the priority: a comfortable gallery and continuous browsing come before further expansion of the existing maintenance-heavy screen. Default to month-grouped chronological browsing, while preserving an optional plain grid and native Windows file-management conventions.

No milestone currently includes an embedded media player, cloud synchronization or copying full originals into the preview cache. Preview pinning needs a separate capacity/reservation design rather than an unlimited promise of retention.
