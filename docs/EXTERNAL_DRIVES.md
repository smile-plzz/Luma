# External-drive caching

Store the catalog and thumbnails under `%LOCALAPPDATA%\Luma` on the internal system disk, never on the source drive. The desktop host selects this location automatically; only the lower-level cache API accepts a custom path.

## Identity and reconnect

Windows registration reads the volume GUID, volume serial and relative folder without writing marker files. Drive letters are not identities. Resolution checks the canonical volume GUID and serial, then asks Windows for its current mount path so the same volume remains addressable to Shell and WinRT after a letter change. A changed serial is treated as a different source. Network shares and junction source aliases are rejected. A cloned volume can duplicate identity; clone collision detection remains future work. Older caller-provided IDs are retained but cannot automatically resolve: explicit source re-registration/migration will be needed.

Catalog reads retain offline records and determine availability using the identity resolver and file existence. A missing drive never deletes catalog entries. The UI uses paged queries (120 items) and resolves source availability once per source per page. The legacy ListAsync API checks individual files. Original-file operations revalidate identity and existence.

## Thumbnails

Cache keys include source identity, relative path, file size, modification time, pixel size and a renderer version. No original file content is copied into the catalog. Encoded thumbnails are saved on the PC. A hit returns before any drive lookup. An offline miss returns null for a placeholder. The service serializes decoder work, rechecks cache after waiting, and validates file size/time and source identity before and after decoding. Windows uses its installed codecs; unsupported formats return null. No codec installation is attempted.

Files are written to a temporary sibling, flushed, then atomically renamed. Only complete entries become visible. A per-directory owner lock and in-process gate protect writes/reads. Startup removes abandoned temporary files and enforces the configured byte budget. LRU uses cache-file modification time and persists across restart. Oldest thumbnails may be evicted even when their source is offline; metadata is unaffected. The default budget is 2 GiB, with a maximum 16 MiB per thumbnail. Lower budgets are supported; the desktop Settings dialog persists a 1–32 GiB budget applied on next startup. One in-flight entry can temporarily exceed the retained byte budget. Startup enumerates cache files once; an in-memory ordered index makes subsequent LRU updates and evictions O(log n), avoiding directory scans on every write.

Missing or stale originals are not decoded under old keys. Existing cached thumbnails remain available until eviction; rescan updates keys after file changes. Same-size edits preserving modification times cannot be detected without hashing. A full error-free scan removes missing catalog entries; partial scans retain them. Catalog scans never delete entries on partial scans or disconnects.

## Limits

Windows CI covers volume resolution and bitmap generation. Physical USB replug testing remains a user acceptance requirement. Directory traversal skips child reparse points; registration rejects source ancestry junctions. File availability can still change immediately after a check; shell operations must revalidate identity at execution. This milestone adds no destructive file operations.
