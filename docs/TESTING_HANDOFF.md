# Luma: shipment and real-life testing

Development closeout: 11 October 2026 (Asia/Dhaka). The portable gallery milestone is merged into `main` through [PR #6](https://github.com/smile-plzz/Luma/pull/6). Real-life testing and everyday use now guide further refinements. This is a testing shipment, not a claim that physical-device acceptance is complete.

## Verified shipment

| Item | Value |
| --- | --- |
| Platform | Windows 10 2004+ / Windows 11, x64 |
| Package | Unsigned, self-contained portable ZIP; no installer |
| Download | [Luma-Gallery-win-x64](https://github.com/smile-plzz/Luma/actions/runs/38077921235/artifacts/11678984011) |
| Tested source | `6b94e38c5e4ee4cc0112419fc4b28a3fe6450173` |
| Merge commit | `7b7c8531c256fe0d72427c24fd863e2da4d2a599` |
| Automated evidence | [Successful run 38077921235](https://github.com/smile-plzz/Luma/actions/runs/38077921235) |
| Desktop evidence | [Screenshots, settings and smoke results](https://github.com/smile-plzz/Luma/actions/runs/38077921235/artifacts/11679028914) |
| ZIP SHA-256 | `e4b2222af9cb32fe5f37f6db4c4929267d8bdea8ad04f5179796b62cba76dafa` |

47 core tests passed on Windows and Linux, four Windows integration tests passed, and the packaged app passed native desktop automation. The UI run covers continuous scrolling across 260 fixtures and back, recycled images, search, favorites, albums, folders, offline preparation/disconnection and view preferences after restart. Catalog-only scale tests cover 1k/10k/100k records.

GitHub may require sign-in to download an Actions artifact. This specific artifact is currently scheduled to expire on 8 January 2027; retain the downloaded ZIP. If unavailable, use a newer successful **Build and test** run on `main`, or run that workflow manually. Identify that newer run when reporting feedback. Executables and bundled runtime files are delivered as build artifacts; source, build automation, tests and documentation are versioned in Git.

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
