# Luma

A local-first Windows media organizer: browse existing photos and videos, then open them in their default apps. **This repository currently contains the indexing/caching engine and Windows adapters, not a runnable desktop UI.**

## Implemented

- SQLite media catalog with read-only recursive indexing and cancellation rollback.
- Internal-disk thumbnail cache: atomic replacement, persistent LRU, configurable byte budget (default 2 GiB), bounded entries, and interrupted-write recovery.
- Cache-first thumbnail service: cached thumbnails are returned without accessing external drives; cache misses use a verified source and reject stale metadata.
- Windows volume GUID + serial + relative-folder identity, independent of drive letter. Separate folders on a drive remain separate sources.
- Windows system thumbnail generation for photos/videos supported by installed codecs.
- Automated core tests on Windows/Linux, plus Windows volume identity tests.

## Developer usage (Windows)

Reference `src/Luma.Windows/Luma.Windows.csproj` from a Windows .NET 8 host targeting Windows 10 build 19041 or later:

```csharp
var local = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Luma");
var resolver = new Luma.Windows.WindowsSourceResolver();
var catalog = new Luma.Core.MediaCache(Path.Combine(local, "catalog.db"), resolver);
await catalog.InitializeAsync();
var source = resolver.Register(@"E:\Photos");
await catalog.ScanAsync(source.Id, source.RootPath);
using var thumbnails = new Luma.Core.ThumbnailCache(Path.Combine(local, "thumbnails"));
var service = new Luma.Core.ThumbnailService(thumbnails, resolver, new Luma.Windows.WindowsThumbnailGenerator());
var media = await catalog.ListAsync(source.Id);
foreach (var item in media.Take(50))
{
    var encodedImage = await service.GetAsync(item); // null => show an offline/unsupported placeholder
}
```

Persist source IDs when registering sources. After restart, use the same ID; the resolver locates the volume without trusting its old drive letter. Call indexing and catalog enumeration from a background worker: filesystem and SQLite operations may block. Keep one cache instance per directory; a lock prevents concurrent process owners. Dispose it only after requests finish.

```powershell
dotnet test tests/Luma.Tests/Luma.Tests.csproj -c Release
dotnet test tests/Luma.Windows.Tests/Luma.Windows.Tests.csproj -c Release
```

See [external-drive behavior](docs/EXTERNAL_DRIVES.md) and [QA coverage](docs/QA.md).

## Remaining work

Native WinUI interface, source-management settings, virtualized/paged browsing, capture-date extraction, tags/favorites, Windows shell file operations, source watchers, safe stale-entry reconciliation, and packaging. Physical unplug/replug and real photo/video codec QA must pass before a release. There is no installer yet.
