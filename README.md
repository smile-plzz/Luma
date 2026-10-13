# Luma

**[Download Luma for Windows](https://github.com/smile-plzz/Luma/releases/latest/download/Luma-v0.5.0-win-x64.zip)** · [Release notes](https://github.com/smile-plzz/Luma/releases/latest)

A native Windows media library for existing folders and drives. Browse photos and videos, organize favorites and tags, and open originals in your default apps.

Development of the portable gallery milestone is complete and merged into `main`. Luma is now in real-life testing and everyday-use evaluation; the current shipment includes the UI refinement, import-lock correction and cached-drive reconnect optimization from PRs #7 and #8, plus resumable indexing and On this day memories from PR #9. Source management, explicit refresh scopes, preview repair/rebuild, storage cleanup and Activity shipped in v0.2.0. The v0.3.0 update shows all supported formats by default, exposes removable filters and filtered counts, makes Reset complete, and simplifies navigation and maintenance. The v0.4.0 update adds month navigation with counts, simple multi-file Select mode, and large-library indexing and browsing optimizations. The v0.5.0 update adds reversible source, folder and file-type exclusions, scan filtering and original Windows executable branding. See the [performance measurements](docs/PERFORMANCE.md). Start with the [testing handoff](docs/TESTING_HANDOFF.md) for the verified shipment, setup and feedback checklist.

## Run the Windows app

1. Open the [verified Windows download](https://github.com/smile-plzz/Luma/releases/latest) in **Releases**.
2. Download the **Luma-v0.5.0-win-x64.zip** under Assets. The automatically generated Source code archives do not contain the EXE.
3. Extract the entire ZIP to a writable folder and run **Luma.App.exe**. Keep the bundled files beside the executable.
4. Select **Add folder or drive**. Browse the indexed media; thumbnails are cached as you browse.

Windows 10 version 2004+ or Windows 11, x64. The portable build bundles .NET and Windows App SDK dependencies. It is unsigned, so Windows may show its usual downloaded-app warning. There is no installer or store package yet.

## What works

- **Library exclusions**: right-click to hide a folder, whole source or file extension, skip it during scans, and restore it without losing organization. [Exclusion guide](docs/EXCLUSIONS.md).
- Original app icon and Windows executable branding. [Identity and icon source](docs/BRANDING.md).

- **Manage sources**: browse, rename, refresh/resume or remove a source without deleting original files.
- **Refresh** menu: current source, all connected sources, or status-only checks.
- **Storage & previews**: scoped coverage, missing-preview repair, safe rebuild, clearing and confirmed unused-preview cleanup.
- **Activity**: live progress, pause/cancel, saved indexing checkpoints and recent results.

- Background read-only folder/drive scanning, cancellation, rescanning and safe stale-entry reconciliation after an error-free scan.
- Continuous virtualized gallery; year/month/day grouping; capture-date fallback; date jumps, search, source/folder/album filters, favorites and tags.
- Remembered thumbnail size, fit/crop, density, labels, format filters and sort direction.
- Virtual albums and stable catalog identities preserve organization across in-app moves.
- Default-app opening, Explorer reveal, copy/cut/paste, rename and Windows Recycle Bin deletion dialogs.
- Cached subfolder navigation with parent navigation, available offline without rescanning older libraries.
- Bulk offline preview preparation with cancellation/resume, coverage and cache usage, and confirmed cache clearing.
- PC-local SQLite catalog and bounded persistent thumbnails. Cached hits do not probe an external drive.
- Catalog-first reopening of known drives, in-place reconnect status, and visible-preview retry without a full scan. Use **Refresh → Refresh all connected sources** explicitly for new or changed files.
- Volume GUID/serial/folder identities that resolve a drive's current mount instead of trusting its old letter.
- Light/dark/system themes and configurable cache limit (1–32 GiB, applied at restart).

## Shortcuts

| Shortcut | Action |
| --- | --- |
| Ctrl+C / Ctrl+X | Copy / cut selected originals |
| Ctrl+V | Choose a destination folder and paste |
| Ctrl+mouse wheel | Resize gallery thumbnails |
| Ctrl+A | Select loaded results (explicit full-result selection is also available) |
| F2 | Rename selected original |
| Delete | Confirm deletion through Windows shell |
| Double-click | Open in the default app |

Text inputs keep their normal editing shortcuts. Files on offline drives can be browsed from cache but cannot be opened or modified. Luma skips existing filenames during its copy/move transfers; native Windows dialogs handle Recycle Bin deletion. See the build guide for operation reports and partial failures.

## Storage

`%LOCALAPPDATA%\Luma` contains the catalog, settings and thumbnails on the PC. Original files stay on their source volumes. Default thumbnail limit: 2 GiB. Offline thumbnail retention depends on cache capacity. Open **Storage & previews**, choose the selected source (or all sources), and **Repair missing previews**. Check the coverage shown there before disconnecting. Preparation preserves existing thumbnails when space runs out; ordinary browsing still uses LRU eviction. Previews are not backups of originals.

## Build and test

On Windows with .NET 8 SDK and Windows build tools (Visual Studio's Windows application development workload):

```powershell
dotnet test tests/Luma.Tests/Luma.Tests.csproj -c Release
dotnet test tests/Luma.Windows.Tests/Luma.Windows.Tests.csproj -c Release
dotnet publish src/Luma.App/Luma.App.csproj -c Release -r win-x64 -p:Platform=x64 -o artifacts/Luma-win-x64
./scripts/Smoke-Test.ps1 -Executable artifacts/Luma-win-x64/Luma.App.exe
```

Core tests also run on Linux. GitHub Actions builds the app, runs Windows integration tests, verifies its native window and uploads build/test evidence. See the specific workflow result before treating a binary as validated.

[Gallery build guide](docs/GALLERY_BUILD.md) · [Phase 2 guide](docs/PHASE_2.md) · [Gallery redesign plan](docs/GALLERY_REDESIGN.md) · [Roadmap](docs/ROADMAP.md) · [Phase 1 scope and limits](docs/PHASE_1.md) · [External-drive design](docs/EXTERNAL_DRIVES.md) · [QA](docs/QA.md)

### Indexing recovery and memories

Interrupted imports save progress on your PC. Use **Activity → Resume paused indexing** after reconnecting; **Manage sources → More → Restart indexing** starts fresh and Activity lists skipped folders. Your published gallery remains available while indexing finishes. **On this day** brings together matching capture dates from earlier years and works with saved offline previews. [Usage and limits](docs/GALLERY_BUILD.md#resumable-indexing).

## Final experience refinement

Saved browsing position, explicit discovery/date-reading progress, accessible action hints and theme-matched dialogs complete the planned build. See [behavior and limits](docs/FINAL_EXPERIENCE.md). Exact shipment evidence is recorded after verification.
