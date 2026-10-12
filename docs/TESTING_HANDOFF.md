# Luma: shipment and real-life testing

Shipment updated: 12 October 2026 (Asia/Dhaka). The portable gallery milestone is merged into `main` through PR #6, followed by UI/import refinements in PR #7 and the shipped reconnect optimization in [PR #8](https://github.com/smile-plzz/Luma/pull/8). Resumable indexing and On this day memories are merged in [PR #9](https://github.com/smile-plzz/Luma/pull/9). Real-life testing and everyday use now guide further refinements. This is a testing shipment, not a claim that physical-device acceptance is complete.

## What changed in this shipment

- [PR #10](https://github.com/smile-plzz/Luma/pull/10) adds visible Manage sources, explicit refresh scopes, Storage & previews, safe rebuild/repair, unused-preview cleanup and Activity. Removal refreshes album counts and never deletes originals.

- Indexing saves persistent checkpoints every 100 media records and at folder boundaries. Resume after cancellation, app restart or reconnect; the published gallery stays intact until indexing finishes. Drive tools includes Resume indexing, Restart selected indexing and Indexing report.
- Protected folders produce a report and prevent pruning older catalog entries. Completed folders are reused; changed folder timestamps conservatively invalidate the checkpoint. This is not a filesystem journal.
- On this day shows matching capture dates from earlier years, with year labels, a date picker, existing filters and offline previews. Missing or stale capture metadata is excluded; modification dates are not treated as memories.

- Known drives reopen from their saved catalog without scanning, including empty registered folders. Reconnect updates availability and visible missing previews while retaining selection and scroll. New or changed files are discovered with explicit **Rescan sources**.
- Metadata enrichment after a scan is limited to the sources actually scanned.

- Fixed the gallery read transaction that caused SQLite lock errors while a long external-drive scan was running. Regression checks cover concurrent browsing with both scan completion and cancellation.
- Refined light/dark surfaces, rounded controls, toolbar, typography and photo tiles; folder controls appear when a source is selected.
- Removed placeholder icons beneath loaded transparent previews and simplified gallery status text.
- Retained existing catalogs, albums, settings and original-file behavior. No database reset or source re-import is required for this update.

## Verified shipment

| Item | Value |
| --- | --- |
| Platform | Windows 10 2004+ / Windows 11, x64 |
| Package | Unsigned, self-contained portable ZIP; no installer |
| Download | [Luma v0.2.0 Windows ZIP](https://github.com/smile-plzz/Luma/releases/download/v0.2.0/Luma-v0.2.0-win-x64.zip) |
| Tested source | `3119f225944b9362b21c8e97383ad33ce16de2d7` |
| Merge commit | `106dcb700b58a5ae663b0648b2dc58e83e2f3d53` |
| Automated evidence | [Successful run 38215576271](https://github.com/smile-plzz/Luma/actions/runs/38215576271) |
| Desktop evidence | [Screenshots, settings and smoke results](https://github.com/smile-plzz/Luma/actions/runs/38215576271/artifacts/11732242447) |
| ZIP SHA-256 | `75e9baddd0148a9a126242764e413fcb92889145e94221ad3a1a3d940c7cf6f3` |

63 core tests passed on Windows and Linux, four Windows integration tests passed, and the packaged app passed native desktop automation. The UI run covers continuous scrolling across 260 fixtures and back, recycled images, search, favorites, albums, folders, offline preparation/disconnection, view preferences after restart and theme switching. Light/dark screenshots were inspected. Native reconnect checks verified preserved selection/scroll and no automatic indexing of an added file. Native management checks passed for removal cancellation/confirmation, refreshed album counts, all-source refresh, preview repair/rebuild, Activity and cleanup. The offline memories smoke passed; core tests cover resume across restart/mount changes, checkpoint invalidation, cancellation, concurrent scanner exclusion, filtering and leap days. Catalog-only scale tests cover 1k/10k/100k records.

The versioned [v0.2.0 Release](https://github.com/smile-plzz/Luma/releases/tag/v0.2.0) contains the exact verified ZIP plus SHA256SUMS.txt. Release assets do not have the Actions artifact's automatic expiration. Source code archives are separate and do not contain the EXE. The release workflow verifies the successful source run and ZIP digest before publication. Source, build automation, release notes, tests and documentation remain versioned in Git; binaries are under Releases.

## First run and upgrade

1. Close any running Luma. If you have an existing library, copy `%LOCALAPPDATA%\Luma` to a backup location while the app is closed.
2. Extract the entire ZIP into a fresh writable folder. Run `Luma.App.exe` with all bundled files beside it; do not copy only the EXE.
3. Add a representative folder containing your usual JPEG/PNG photos and MP4/MOV videos. Scanning catalogs originals without rearranging them; metadata enrichment follows discovery.
4. Explore the timeline, folders, sorting, View options, search, favorites and albums. Open originals in their usual applications. Close and reopen to check retained settings and organization.
5. Add the external drive, prepare offline previews in **Drive tools**, and check coverage before disconnecting. Saved previews are not original-file backups and may later be evicted by normal browsing.

User data remains in `%LOCALAPPDATA%\Luma` when replacing the executable folder. Do not run an older app against a library updated by this build: older versions do not maintain the new album identities during file operations. For rollback, close Luma and restore the matching pre-upgrade data backup along with the prior app. Preserve the newer data separately first; rollback discards organization added since that backup. Original-file moves/deletes are not undone by restoring the catalog.

## Bench checklist

| Area | Try | Record |
| --- | --- | --- |
| Daily browsing | Scroll a real collection, search, change sorting/grouping and tile size, restart | Delays, confusing controls, missing previews, lost preferences |
| Everyday formats | JPEG/PNG, HEIC, MP4/MOV and your camera formats | Extension, Windows codec availability, preview versus default-app behavior |
| External drive | Prepare previews, disconnect, restart, browse offline, reconnect under another letter | Coverage before/after, source status, whether originals reopen |
| Indexing recovery | Cancel a large import, restart Luma, reconnect and Resume indexing; inspect the report | Saved progress, final totals, skipped folders and retained organization |
| Memories | Open On this day, choose another date, search and browse offline | Capture-date accuracy, year groups and prepared previews |
| Maintenance | Manage sources; cancel then confirm removal on a disposable source; repair/rebuild previews; clean unused previews | Original files retained, correct album counts, scope, reclaimed space and offline previews |
| Organization | Favorites, tags, album create/rename/remove, selected-item actions | Whether membership and annotations remain correct after restart |
| File control | On disposable copies: copy/move, collision, cancel, rename, Explorer drag/drop, Recycle Bin | Intended and actual paths, skipped items, recovery behavior |
| Usability | Typical window size, keyboard navigation, light/dark mode and display scaling | Screenshot, resolution/scaling, exact interaction |
| Performance | Record collection size, disk type, cold versus warm cache, RAM and task duration | Reproducible timings; avoid interpreting catalog benchmarks as frame-rate results |

For lower-capacity cache, interrupted work, limited disk space and source-identity edge cases, follow [QA.md](QA.md). Hardware and native shell acceptance remain pending until actually exercised. Do not reformat a personal drive for an identity test; use a disposable test volume if that scenario is needed.

## Known limits and refinement priorities

- Windows-installed codecs determine previews. No embedded player/editor or automatic codec installation is included.
- There is no filesystem watcher. Rescan after changing originals outside Luma. External moves do not automatically preserve catalog organization.
- Cached previews are bounded and evictable; preparation is resumable but is not pinning or backup.
- Capture dates have documented timezone/provenance limits; video encoding time is not treated as verified capture time.
- Lightweight loaded records accumulate during browsing; decoded thumbnails are recycled. Full 100k-photo scrolling and physical USB performance are not certified.
- Transfers skip collisions. Filesystem operations and catalog updates are not one atomic transaction; inspect `%LOCALAPPDATA%\Luma\last-transfer.json` if a transfer reports partial failure.
- The package is unsigned and portable. Signing, installer packaging and external-move reconciliation are deferred until feedback establishes their priority.

No new feature work is required before starting this testing stage. Startup/data-integrity defects take priority, followed by recurring performance/usability issues, then additional capabilities.

## Sending feedback

Send feedback in the project conversation or open a [GitHub issue](https://github.com/smile-plzz/Luma/issues/new?template=testing-feedback.md). Include the build/run, Windows version, drive type, approximate file count, exact steps, expected and actual result, and reproducibility. A screenshot or short recording helps. For transfer problems, include the relevant sanitized operation-report entries. Avoid posting your entire catalog or personal media; paths and filenames may contain private information.

See [the build guide](GALLERY_BUILD.md) for detailed behavior and [the roadmap](ROADMAP.md) for the closed milestone and deferred work.
