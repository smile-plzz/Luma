# QA

## Automated coverage

`Luma.Tests` exercises persistent restart reads, LRU eviction after touch, reduced-budget startup, abandoned temporary cleanup, cancelled writes, oversized entries, invalid keys/path escape, key invalidation, concurrent writes, offline hits without source probes, offline misses without decoding, moved-root reconnect, duplicate decode coalescing, stale files, unplug during generation, persisted catalog availability and cancelled scans.

`Luma.Windows.Tests` exercises actual Windows volume GUID registration/resolution, stable registration with trailing separators, distinct folder IDs and malformed IDs. A generated BMP fixture also tests WinRT thumbnail generation through a resolved volume path and cached retrieval after deleting that fixture. Core CI runs on Windows and Linux; Windows integration runs on Windows.

These tests use small fixtures. They do not establish UI responsiveness or 100,000-file performance. Simulated root changes and disconnects do not replace hardware testing. Cancellation tests currently cover pre-cancelled catalog/write calls; in-flight catalog cancellation and low-disk faults still require injection/manual coverage.

## Required before release

- Real USB drive: index, generate thumbnails, close/reopen app, unplug, browse saved thumbnails, replug under a changed drive letter.
- Use another drive at the old letter; confirm original source stays offline. Reformat and confirm it is not mistaken for the prior volume.
- Disconnect during enumeration and decoding; original files must be unchanged and existing cache remain usable.
- Decode JPEG/PNG, HEIC with and without codec, MP4/MOV, corrupt files, long/unicode paths, locked files and permission-denied folders.
- Kill process during cache write; reopen and verify complete prior data, no half-thumbnail, temporary cleanup.
- Fill local disk, reduce budget, and test concurrent app launch/cache ownership errors.
- Benchmark cold scan, warm thumbnail browsing, memory, eviction and 100k media items before selecting UI paging defaults.
- Verify default-app opening and all future copy/move/delete operations independently before enabling them.

## Validation status for this change

Source whitespace checks (`git diff --check`) pass. Automated tests have been written but have not run successfully in the development container: .NET 8.0.425 reports a process-information error before the test runner can start. Direct MSBuild fails for the same environment reason. Windows execution is unavailable locally. CI results must be confirmed after publishing the branch; physical-drive QA is still outstanding.
