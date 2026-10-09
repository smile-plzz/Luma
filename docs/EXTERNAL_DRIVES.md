# External drive caching architecture

Luma stores its SQLite catalog and (future) thumbnail cache on the internal system disk, **not** the external drive.

- Identify sources by durable volume identity, not drive letter. UI source registration must resolve the Windows volume GUID and detect collisions/reformatting.
- Reconnecting the same source at a different letter updates its root path without losing indexed entries.
- A disconnected source remains browsable via metadata; show an offline indicator and disable Open, Cut, Delete, and Paste into the disconnected source.
- Thumbnail cache keys are derived from source ID + relative path + size + last-write ticks. Thumbnail generation and persistent thumbnail storage are planned, **not implemented** here.
- Never delete cached entries just because a source is missing. A future reconciler should delete stale records only after a completed, error-free scan.
- Scanning is read-only and skips directory symlinks/reparse points. Permission errors are tolerated; cancellation rolls back the current scan transaction.
- WAL improves local catalog concurrency. Do not place the database on removable or network volumes.
- QA matrix: unplug during scan, reconnect under new letter, 100k+ files, duplicate names, invalid media, locked files, access denied, timestamp changes, cancellation, interrupted power, low disk space.

Current limitations: no thumbnail generator, no filesystem watcher, no stale-entry reconciliation, no volume GUID resolver, no native UI, and no Windows execution test.
