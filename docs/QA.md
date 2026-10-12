# QA

## Current verified shipment: indexing recovery and memories

[Run 38168157835](https://github.com/smile-plzz/Luma/actions/runs/38168157835), source `ef23349e82723c98636f9e3a6acad5d7c210f135`: all jobs passed. 58 core cases passed on each of Windows and Linux; four Windows integration tests and the native desktop smoke passed. `memories.txt` verifies capture-date browsing while offline; `reconnect.txt` verifies preserved selection/scroll without indexing. Light/dark screenshots were inspected. PR #9 is merged at `ab08a6c67faf894d09dce5a7a1feddd8b5a90d5e`; its source tree matches the tested commit. See [the handoff](TESTING_HANDOFF.md) for the ZIP and checksum.

## Current verified shipment

[Run 38151631336](https://github.com/smile-plzz/Luma/actions/runs/38151631336), commit `4e31cf2d0b6473a4dc51fdbc4cada0915912cb17`: all jobs passed. 51 core cases pass on each of Windows and Linux, four Windows integrations pass, and the complete desktop smoke passes. Its reconnect evidence verifies selection and scroll retention and no silent discovery of a new file. PR #8 is merged. See [the current handoff](TESTING_HANDOFF.md) for the verified ZIP.

## UI refinement shipment

[Run 38114261821](https://github.com/smile-plzz/Luma/actions/runs/38114261821), commit `55df818d8e0c2234681af2eb6962e735e2d90689`: all jobs passed. 49 core tests pass on Windows and Linux, four Windows integration cases pass, and the packaged desktop passes the complete UI smoke including theme switching. PR #7 is merged. Light/dark screenshots were inspected; physical-drive confirmation of the reported import issue remains pending.

## Original gallery shipment

[Run 38077921235](https://github.com/smile-plzz/Luma/actions/runs/38077921235), commit `6b94e38c5e4ee4cc0112419fc4b28a3fe6450173`: all jobs passed. 47 core cases pass on each of Windows and Linux; four Windows integration cases pass; self-contained publishing, compiled XAML resources and native UI smoke pass. This code is merged through PR #6. The [testing handoff](TESTING_HANDOFF.md) links the exact ZIP and evidence.

The 260-fixture native run peaked at 58 decoded images and restored 170px thumbnail size with filenames hidden after restart. Offline preparation reported 260/260 saved previews. These are synthetic functional checks, not physical-drive or full-library performance certification.

## Automated coverage

`Luma.Tests` exercises persistent restart reads, LRU eviction after touch, reduced-budget startup, abandoned temporary cleanup, cancelled writes, oversized entries, invalid keys/path escape, key invalidation, concurrent writes, offline hits without source probes, offline misses without decoding, moved-root reconnect, duplicate decode coalescing, stale files, unplug during generation, persisted catalog availability and cancelled scans.

`Luma.Windows.Tests` exercises actual Windows volume GUID registration/resolution, stable registration with trailing separators, distinct folder IDs and malformed IDs. A generated BMP fixture also tests WinRT thumbnail generation through a resolved volume path and cached retrieval after deleting that fixture. Core CI runs on Windows and Linux; Windows integration runs on Windows.

Native media tests use small fixtures; catalog scale tests use 1k, 10k and 100k synthetic SQLite records. They do not establish full-library UI responsiveness or 100,000-image decoding performance. Simulated root changes and disconnects do not replace hardware testing. Cancellation coverage includes both pre-cancelled calls and cancellation after 100 indexed files; identity loss during indexing also verifies transaction rollback. Low-disk faults still require manual coverage.

## Real-life acceptance still pending

- Real USB drive: index, generate thumbnails, close/reopen app, unplug, browse saved thumbnails, replug under a changed drive letter.
- Use another drive at the old letter; confirm original source stays offline. Reformat and confirm it is not mistaken for the prior volume.
- Disconnect during enumeration and decoding; original files must be unchanged and existing cache remain usable.
- Decode JPEG/PNG, HEIC with and without codec, MP4/MOV, corrupt files, long/unicode paths, locked files and permission-denied folders.
- Kill process during cache write; reopen and verify complete prior data, no half-thumbnail, temporary cleanup.
- Fill local disk, reduce budget, and test concurrent app launch/cache ownership errors.
- Benchmark cold scan, warm thumbnail browsing, memory, eviction and 100k media items to evaluate continuous-gallery responsiveness.
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

See [GALLERY_BUILD.md](GALLERY_BUILD.md) for the current implementation and limits. The gallery suite adds ten cursor sort/direction cases, seven metadata/organization/transfer regressions, and three catalog scale cases (1k/10k/100k). The native smoke now uses 260 fixtures, scrolls to the end and back without paging buttons, records peak decoded images, then performs the existing offline preparation/disconnection sequence. Windows integration also checks BMP/JPEG/PNG decoding, dimensions and missing capture-date fallback. Native smoke additionally creates/populates an album and checks view preferences after restarting the isolated library. The verified gallery run above records the final counts and outcomes.

The original gallery suite contained 47 core test cases (including three scale cases) and four Windows integration cases. A source-list regression also checks that reconnecting under a different mount path updates the displayed path. Core cases run on both Windows and Linux. Native UI smoke is an additional independent gate.

## Import lock regression

The first real-drive report showed `SQLite Error 5: database is locked` while browsing during a long scan. `QueryAsync` used an immediate transaction despite executing only SELECTs, competing with the scanner for the writer slot. It now uses a deferred read snapshot; count and rows remain consistent while WAL allows the scan to write.

Two regressions hold an active scan at a progress checkpoint while querying capture-date pages, cursor continuation, filtered album/favorites and month navigation. Queries must complete before the scanner is released, must see only committed rows, and must retain organization after either scan commit or cancellation. These raise core coverage to 49 cases per OS. Physical-drive confirmation remains pending.

## Catalog-first reconnect regression

Two additional core tests cover reopening saved sources across a fresh catalog instance without resolver calls or scan progress, preservation of organization, deferred discovery of external changes until explicit rescan, and reuse of empty registered sources. This raises the core suite to 51 cases per OS. Native smoke uses drive-status refresh for simulated disconnect/reconnect, verifies the exact selected card and scroll position survive, and checks that a newly added file is not silently indexed. Physical USB timing and varied Windows hardware still need user acceptance.

## Resumable indexing and memories follow-up

New automated cases exercise checkpoint reuse after restart/mount change, cancellation and disconnect without partial publication, completed-folder invalidation, checkpoint discard/removal, overlapping scanner exclusion, organization retention, offline capture-date memories, cursor traversal and filters, stale metadata and exact leap-day matching. The native desktop smoke now enters On this day with the source offline, verifies a previous-year capture fixture and excludes files without capture metadata. The successful run and downloadable artifact are recorded in TESTING_HANDOFF.md.

On the physical bench, interrupt a large multi-folder import, close and reopen Luma, reconnect the HDD (including a new drive letter), and choose Resume indexing. Verify saved progress, stable albums/favorites and final totals. Repeat with a large flat folder. Check Indexing report for protected folders. Try another date in memories, then disconnect and verify prepared previews. Test the source's actual filesystem and camera-date conventions; synthetic CI cannot certify those.

## Library management follow-up

Core tests cover successful and offline rebuilds, cancellation and identity loss retaining old previews, capacity protection for other sources, exact cleanup estimates and removal, and source removal updating counts and album membership while retaining originals. Native smoke exercises visible source management, removal cancellation and confirmation, refreshed album counts, all-source refresh, missing-preview repair, successful preview rebuild, Activity and cleanup after source removal. Final run and release are recorded in TESTING_HANDOFF.md after verification.
