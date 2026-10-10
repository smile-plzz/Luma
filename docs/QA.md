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

## Phase 2 validation baseline

The suite contains 26 core tests (run on both Windows and Linux) and 2 Windows integration tests. It covers cache restart/eviction/concurrency, offline behavior, identity resolution, Unicode source folders, changed serial rejection, real bitmap decoding, catalog paging/search/annotations, completed-scan reconciliation and transaction rollback during cancellation/disconnect.

The desktop pipeline publishes the self-contained x64 folder and launches it on Windows. Its UI Automation smoke test uses an isolated temporary catalog with synthetic BMP files to exercise the populated grid, search, selection and favorite filtering, cached folder navigation, and offline preparation with full fixture coverage. It records a screenshot and accessible-control evidence. It never touches a user's normal Luma catalog.

Consult the latest run linked from the pull request for the result of the exact commit being downloaded. Windows/Linux core tests and Windows integration have passed during this change; the desktop packaging and smoke check are required gates for the downloadable artifact. The Linux development container cannot execute the full .NET test runner due to process-information failures, so CI is the execution source of truth. Physical USB and native destructive-operation QA remain separate acceptance checks.

## Verified Phase 1 run

[Run 37933213641](https://github.com/smile-plzz/Luma/actions/runs/37933213641), commit `790c8625a364054511a72d09c9ba2ea4a516d785`: all jobs passed. 20 core tests pass on each of Windows and Linux; both Windows integration tests pass; the self-contained desktop build and populated-library UI Automation smoke test pass. The `Luma-Phase1-win-x64` ZIP and desktop screenshot/control evidence are attached to that run.

## Phase 2 coverage and acceptance

Six additional core regressions cover catalog-only folder discovery with literal `%`/`_` names, cancellation/resume and restart with no drive probes, partial coverage under a full cache without evicting another source, stale/missing-key coverage, cache clearing without changing originals or annotations, and offline misses without decoding. Native UI smoke selects a source, enters its cached `Trips` folder, returns to the parent, prepares all eight previews and asserts `8 / 8` coverage.

Before a general release, manually prepare a multi-page removable source, cancel/resume, disconnect it, restart Luma and check coverage. Repeat with insufficient cache space and another offline source already cached. Confirm that preparation does not evict that source, ordinary browsing may evict it, and the confirmation for clearing previews clearly affects all sources. Closing the app mid-preparation must allow the next launch to obtain cache ownership. Verify large catalogs and low-disk failures separately; the small-fixture CI does not certify performance or physical unplug behavior.

## Gallery redesign coverage

See [GALLERY_BUILD.md](GALLERY_BUILD.md) for the current implementation and limits. The gallery suite adds ten cursor sort/direction cases, seven metadata/organization/transfer regressions, and three catalog scale cases (1k/10k/100k). The native smoke now uses 260 fixtures, scrolls to the end and back without paging buttons, records peak decoded images, then performs the existing offline preparation/disconnection sequence. Windows integration also checks BMP/JPEG/PNG decoding, dimensions and missing capture-date fallback. Native smoke additionally creates/populates an album and checks view preferences after restarting the isolated library. Consult the exact gallery PR run for final counts and outcomes.

The current gallery suite contains 47 core test cases (including three scale cases) and four Windows integration cases. A source-list regression also checks that reconnecting under a different mount path updates the displayed path. Core cases run on both Windows and Linux. Native UI smoke is an additional independent gate.
