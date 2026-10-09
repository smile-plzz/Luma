# QA

## Automated coverage

`Luma.Tests` exercises persistent restart reads, LRU eviction after touch, reduced-budget startup, abandoned temporary cleanup, cancelled writes, oversized entries, invalid keys/path escape, key invalidation, concurrent writes, offline hits without source probes, offline misses without decoding, moved-root reconnect, duplicate decode coalescing, stale files, unplug during generation, persisted catalog availability and cancelled scans.

`Luma.Windows.Tests` exercises actual Windows volume GUID registration/resolution, stable registration with trailing separators, distinct folder IDs and malformed IDs. A generated BMP fixture also tests WinRT thumbnail generation through a resolved volume path and cached retrieval after deleting that fixture. Core CI runs on Windows and Linux; Windows integration runs on Windows.

These tests use small fixtures. They do not establish UI responsiveness or 100,000-file performance. Simulated root changes and disconnects do not replace hardware testing. Cancellation coverage includes both pre-cancelled calls and cancellation after 100 indexed files; identity loss during indexing also verifies transaction rollback. Low-disk faults still require manual coverage.

## Required before release

- Real USB drive: index, generate thumbnails, close/reopen app, unplug, browse saved thumbnails, replug under a changed drive letter.
- Use another drive at the old letter; confirm original source stays offline. Reformat and confirm it is not mistaken for the prior volume.
- Disconnect during enumeration and decoding; original files must be unchanged and existing cache remain usable.
- Decode JPEG/PNG, HEIC with and without codec, MP4/MOV, corrupt files, long/unicode paths, locked files and permission-denied folders.
- Kill process during cache write; reopen and verify complete prior data, no half-thumbnail, temporary cleanup.
- Fill local disk, reduce budget, and test concurrent app launch/cache ownership errors.
- Benchmark cold scan, warm thumbnail browsing, memory, eviction and 100k media items before selecting UI paging defaults.
- Verify default-app opening, clipboard interoperability, rename and native copy/move/delete dialogs on disposable files. These are implemented but their native shell interactions still need real-PC acceptance.

## Validation

The suite contains 20 core tests (run on both Windows and Linux) and 2 Windows integration tests. It covers cache restart/eviction/concurrency, offline behavior, identity resolution, Unicode source folders, changed serial rejection, real bitmap decoding, catalog paging/search/annotations, completed-scan reconciliation and transaction rollback during cancellation/disconnect.

The desktop pipeline publishes the self-contained x64 folder and launches it on Windows. Its UI Automation smoke test uses an isolated temporary catalog with synthetic BMP files to exercise the populated grid, search, selection and favorite filtering. It records a screenshot and accessible-control evidence. It never touches a user's normal Luma catalog.

Consult the latest run linked from the pull request for the result of the exact commit being downloaded. Windows/Linux core tests and Windows integration have passed during this change; the desktop packaging and smoke check are required gates for the downloadable artifact. The Linux development container cannot execute the full .NET test runner due to process-information failures, so CI is the execution source of truth. Physical USB and native destructive-operation QA remain separate acceptance checks.

## Verified Phase 1 run

[Run 37933213641](https://github.com/smile-plzz/Luma/actions/runs/37933213641), commit `790c8625a364054511a72d09c9ba2ea4a516d785`: all jobs passed. 20 core tests pass on each of Windows and Linux; both Windows integration tests pass; the self-contained desktop build and populated-library UI Automation smoke test pass. The `Luma-Phase1-win-x64` ZIP and desktop screenshot/control evidence are attached to that run.
