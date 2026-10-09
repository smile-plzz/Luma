# External-drive caching

Store the catalog and thumbnails under `%LOCALAPPDATA%\Luma` on the internal system disk, never on the source drive. The cache API accepts a path; the future desktop host must enforce this storage policy.

## Identity and reconnect

Windows registration reads the volume GUID, volume serial and relative folder without writing marker files. Drive letters are not identities. Resolution uses the volume GUID path, so the same volume remains addressable after a letter change. A changed serial is treated as a different source. Network shares and junction source aliases are rejected. A cloned volume can duplicate identity; clone collision detection remains future work. Older caller-provided IDs are retained but cannot automatically resolve: explicit source re-registration/migration will be needed.

Catalog reads retain offline records and determine availability using the identity resolver and file existence. A missing drive never deletes catalog entries. Reads currently return a full list and check availability for each file; pagination and batched availability belong to the UI performance milestone.

## Thumbnails

Cache keys include source identity, relative path, file size, modification time, pixel size and a renderer version. No original file content is copied into the catalog. Encoded thumbnails are saved on the PC. A hit returns before any drive lookup. An offline miss returns null for a placeholder. The service serializes decoder work, rechecks cache after waiting, and validates file size/time and source identity before and after decoding. Windows uses its installed codecs; unsupported formats return null. No codec installation is attempted.

Files are written to a temporary sibling, flushed, then atomically renamed. Only complete entries become visible. A per-directory owner lock and in-process gate protect writes/reads. Startup removes abandoned temporary files and enforces the configured byte budget. LRU uses cache-file modification time and persists across restart. Oldest thumbnails may be evicted even when their source is offline; metadata is unaffected. The default budget is 2 GiB, with a maximum 16 MiB per thumbnail. Lower budgets are supported; budget is constructor configuration until Settings exists. One in-flight entry can temporarily exceed the retained byte budget. Startup enumerates cache files once; an in-memory ordered index makes subsequent LRU updates and evictions O(log n), avoiding directory scans on every write.

Missing or stale originals are not decoded under old keys. Existing cached thumbnails remain available until eviction; rescan updates keys after file changes. Same-size edits preserving modification times cannot be detected without hashing. A full error-free scan reconciler is still needed for deleted/renamed originals. Catalog scans never delete entries on partial scans or disconnects.

## Limits

No physical-drive QA has run in this development environment. Directory traversal skips child reparse points; registration rejects source ancestry junctions. File availability can still change immediately after a check; shell operations must revalidate identity at execution. This milestone adds no destructive file operations.
