# Luma

A native Windows media library for existing folders and drives. Browse photos and videos, organize favorites and tags, and open originals in your default apps.

## Run the Windows app

1. Open the **Build and test** GitHub Actions run for the gallery redesign branch/PR.
2. Download the **Luma-Gallery-win-x64** artifact from a successful run.
3. Extract the entire ZIP to a writable folder and run **Luma.App.exe**. Keep the bundled files beside the executable.
4. Select **Add folder or drive**. Browse the indexed media; thumbnails are cached as you visit pages.

Windows 10 version 2004+ or Windows 11, x64. The portable build bundles .NET and Windows App SDK dependencies. It is unsigned, so Windows may show its usual downloaded-app warning. There is no installer or store package yet.

## What works

- Background read-only folder/drive scanning, cancellation, rescanning and safe stale-entry reconciliation after an error-free scan.
- Continuous virtualized gallery; year/month/day grouping; capture-date fallback; date jumps, search, source/folder/album filters, favorites and tags.
- Remembered thumbnail size, fit/crop, density, labels, format filters and sort direction.
- Virtual albums and stable catalog identities preserve organization across in-app moves.
- Default-app opening, Explorer reveal, copy/cut/paste, rename and Windows Recycle Bin deletion dialogs.
- Cached subfolder navigation with parent navigation, available offline without rescanning older libraries.
- Bulk offline preview preparation with cancellation/resume, coverage and cache usage, and confirmed cache clearing.
- PC-local SQLite catalog and bounded persistent thumbnails. Cached hits do not probe an external drive.
- Volume GUID/serial/folder identities that resolve a drive's current mount instead of trusting its old letter.
- Light/dark/system themes and configurable cache limit (1–32 GiB, applied at restart).

## Shortcuts

| Shortcut | Action |
| --- | --- |
| Ctrl+C / Ctrl+X | Copy / cut selected originals |
| Ctrl+V | Choose a destination folder and paste |
| Ctrl+A | Select loaded results (explicit full-result selection is also available) |
| F2 | Rename selected original |
| Delete | Confirm deletion through Windows shell |
| Double-click | Open in the default app |

Text inputs keep their normal editing shortcuts. Files on offline drives can be browsed from cache but cannot be opened or modified. Windows handles collisions and native file-operation confirmations.

## Storage

`%LOCALAPPDATA%\Luma` contains the catalog, settings and thumbnails on the PC. Original files stay on their source volumes. Default thumbnail limit: 2 GiB. Offline thumbnail retention depends on cache capacity. Use **Prepare offline previews** for the selected source (or all sources), then **Check offline coverage** before disconnecting. Preparation preserves existing thumbnails when space runs out; ordinary browsing still uses LRU eviction. Previews are not backups of originals.

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
