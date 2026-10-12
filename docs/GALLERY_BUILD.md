# Gallery build: usage, architecture and acceptance

This build replaces the paged Phase 2 interface with a native chronological gallery, configurable views, versioned metadata and virtual albums. It keeps the existing catalog, volume identity and offline preview cache.

The implementation is merged into `main`; development is closed for this milestone and real-life testing is beginning. See [the shipment and testing handoff](TESTING_HANDOFF.md) for the exact verified download and feedback procedure.

## Install and upgrade

Download **Luma-Gallery-win-x64** from a successful Build and test workflow run, extract the entire ZIP into a fresh writable folder and launch `Luma.App.exe`. Keep all bundled DLLs and resources beside it. The repository file listing contains source code; the runnable Windows package is an Actions artifact. Windows 10 2004+ / Windows 11 x64 are supported. The portable package is unsigned.

Existing data remains in `%LOCALAPPDATA%\Luma`. Before upgrading an important library, close Luma and copy that folder as a backup. New identity, metadata and album tables are additive; initialization can run repeatedly without replacing organization. Do not run an older build against a library after using the new organization features: older builds do not maintain album identity during file operations.

## Browse

- **Library** opens a chronological gallery, grouped by month and sorted by date taken with modification-time fallback. Photos, Videos and Favorites filter it.
- Scroll continuously in both directions. Database batches remain internal; there are no Next/Previous buttons. File records already loaded stay available for navigation and selection, while native container recycling releases decoded images outside the realized region.
- **View** changes thumbnail size, compact spacing, fit/crop, filename/detail visibility, date grouping, everyday formats and nested-folder inclusion. These preferences persist. Ctrl+mouse-wheel resizes thumbnails.
- Sort by date taken, date modified, name, size or extension; use the direction button for ascending/descending. Name/size/type show an ungrouped grid. Group by year, month, day or none when using date ordering.
- **Jump to month** reads available dates from the catalog and starts at the end of the chosen month, continuing toward older media. **Reset** restores the full result set and clears search/source/folder/album filters.
- Choose a drive/source, enter its cached subfolders and use **Up to parent folder**. A friendly source label can be assigned in Drive tools. Folder counts include descendants even when the grid's nested-folder option is off.
- **Details** opens the optional inspector for the selected file, metadata provenance and tags. The selection toolbar appears only when needed.

Everyday formats include JPEG/JPG, PNG, HEIC/HEIF, GIF, WebP, BMP, MP4 and MOV. Disable the everyday-only view to include other supported indexed extensions. A recognized extension does not guarantee a Windows preview codec; unsupported previews remain placeholders and originals open in their associated application.

## Timeline metadata

Scanning discovers files first, makes the gallery available, and then enriches changed versions with Windows image/video properties. Enrichment completion does not jump or reorder the active gallery; navigate or Reset to apply new dates. **Drive tools → Refresh metadata** enriches older catalogs without a full scan. Completed metadata survives cancellation and is retained offline. Unchanged versions are not decoded repeatedly.

Photos use the date taken reported by Windows when plausible. The original camera timezone is not verified, and that limitation is shown in the inspector. Video encoding dates are not labeled capture dates; videos use modification time for timeline ordering. Dimensions, orientation and duration are stored when available. Missing or unsupported properties fall back explicitly.

Metadata is joined only when its recorded size and modification time match the catalog version. Rescan after editing originals. Same-size edits preserving modification timestamps remain undetectable without content hashing.

## Albums and selection

Create, rename and delete virtual albums in the sidebar. Select files and use **Add to album** from the inspector or context menu. Removing membership or deleting an album never deletes original files. To add by drag-and-drop, select the destination album and drop Luma items onto the album selector; this changes membership only and works with offline catalog items.

Ctrl-click and Shift-selection follow native Windows behavior. Ctrl+A selects the currently loaded results and reports that scope. **Select all matching results** explicitly confirms the total, loads lightweight records and selects the whole result set. Changing filters cancels that expansion. Image decoding remains tied to realized tiles. File actions always state a selected-file count.

Favorites support multi-selection. Saving tags for multiple files confirms that their tag lists will be replaced. Source removal also removes its album memberships and annotations; deleting an album does not remove a source.

## File control

Open/default-app, Explorer reveal, copy path, clipboard copy/cut/paste, rename and native Recycle Bin deletion remain available. Cut selections appear dimmed until a new clipboard operation or successful in-app paste. Text inputs retain their editing shortcuts.

**Copy to folder** and **Move to folder** provide direct context-menu actions. Dropping real files on cached folders defaults to copy; hold Shift for move. A destination/count confirmation precedes the transfer. Existing target filenames are skipped rather than overwritten; choose another destination/name to resolve a conflict. Only real files are transferred; add folders as sources or use Explorer for folder-level moves.

Copies stream to a private temporary sibling, honor cancellation, preserve modification time, revalidate the source, and publish by rename. A move removes the original only after publishing the complete copy and checking the source again. Existing-file conflicts and partial failures keep a per-file outcome. Successful in-app moves/renames retain stable IDs, favorites, tags and album membership. A destination outside registered sources is registered so moved items remain discoverable.

The latest transfer report is `%LOCALAPPDATA%\Luma\last-transfer.json`. If files moved but a catalog update failed, the app reports it and avoids an automatic reconciliation that could discard the old organization. Use the report to restore the original path or reconcile deliberately. Filesystem changes and database transactions are not a single rollbackable operation. External Explorer moves, including moves completed after dragging out of Luma, do not currently preserve catalog organization automatically.

## Offline behavior and activity

Drive tools contains scanning, metadata refresh, offline preparation, coverage and cache clearing. The footer reports work and provides cancellation. The existing Phase 2 non-evicting bulk preparation and bounded browsing LRU remain in force: preparation preserves existing previews at capacity, but later browsing may evict previews. Cached media and virtual organization remain useful without the drive; original-file operations require a connected, unchanged file.

Availability checks update source indicators and loaded-card availability in place without rebuilding the gallery or losing selection/scroll. They also retry visible missing previews when the source reconnects. Checks run periodically (30 seconds), when the window becomes active, or on **Drive tools → Refresh drive status**. A file action always resolves identity and validates its version again. No original is moved merely by scanning, enriching metadata or organizing an album.

## Verification

Core coverage includes cursor traversal with tied sort values in both directions, catalog migrations, albums following relocations, metadata invalidation/enrichment, folder depth and format filters, date jumps, collision-free transfer, cancellation, and all previous offline/cache regressions. Scale cases exercise 1k, 10k and 100k synthetic SQLite records and save first/next-batch and month-index timings with OS information. Those figures measure catalog queries, not USB throughput or full-library image decoding.

The Windows integration suite checks actual volume identity plus BMP, JPEG and PNG thumbnail decoding and dimensions. The native UI smoke uses 260 isolated synthetic images, scrolls across several internal batches and back, checks the peak decoded-image count, and exercises search, favorites, virtual albums, folders, clearing/preparing previews and a simulated unavailable source. It restarts the isolated library and verifies thumbnail/label preferences are restored. It writes screenshot, coverage and image-lifetime evidence.

Physical USB reconnects, low-disk faults, native Recycle Bin prompts, Explorer drag/drop and a diverse real-world codec set still need acceptance on the user's Windows bench. Synthetic CI screenshots are functional evidence, not a substitute for visual review with personal photos/videos. The build does not claim a measured 100k-image scrolling/frame-rate guarantee. Lightweight loaded records accumulate during exploration; decoded images are recycled. There is no cloud account, embedded player/editor, automatic original-file rearrangement, signing certificate or installer.

## Browsing while importing

The gallery reads the last committed catalog while a scan indexes a drive. Newly discovered files appear after that source scan commits; cancelling retains the previous committed catalog. The import-lock fix uses a deferred read transaction so browsing does not compete for the scan's SQLite write lock.

If an older build shows `SQLite Error 5: database is locked` during indexing, let indexing finish (or use Cancel), then choose Reset. Do not delete the catalog or rescan repeatedly in response to that message. The [current verified shipment](TESTING_HANDOFF.md) includes this fix and supports concurrent browsing. This workaround addresses the reported gallery/scan conflict; persistent locking with no scan running should be reported separately.

## Visual refinement after initial testing

The polished gallery uses a soft neutral sidebar, a quieter photo canvas, rounded borderless controls and a single compact view toolbar. Light and dark palettes adapt with the existing theme setting, and high-contrast colors use system resources. Folder navigation is shown after selecting a source; maintenance remains in Drive tools. File counts and progress stay visible without database/loading terminology dominating the gallery. Loaded transparent previews no longer show a placeholder icon underneath them.

The interaction model stays the same: Windows selection and shortcuts, the optional inspector, accessible control names, continuous browsing and default-app opening. This is a frontend refinement, with the separately documented import-lock correction included in the same follow-up build.

## Fast reopening of external drives

After the first completed scan, adding the same registered folder/drive again reuses its PC-local catalog instead of enumerating the HDD. Sources with zero indexed media are remembered too. Browsing reads catalog data and saved thumbnail files immediately; cache hits do not probe the original drive. Missing previews are generated only for visible items unless you request bulk preparation.

Reconnecting updates source/card availability and retries visible missing previews without clearing cards, selection or scroll position. Original-file actions still resolve the actual volume identity and validate the file before use. The status refresh checks registered roots; it does not walk source directories or discover files. It is available in Drive tools and runs on window activation and every 30 seconds when idle.

Use **Rescan sources** to discover additions, deletions or edits made outside Luma. This remains an explicit directory walk, not a filesystem journal or incremental change detector. Metadata enrichment following a scan is now limited to the sources actually scanned, and unchanged file versions continue to reuse saved metadata. For reliable offline browsing, prepare previews and check coverage before unplugging; bounded cache eviction rules are unchanged.

## Resumable indexing

Indexing saves file records in batches of 100 and folder completion checkpoints in the PC-local database. Cancellation, app closure, process termination or a disconnect retains committed checkpoints. The previous published gallery, albums and annotations remain available; the new snapshot replaces it only when traversal finishes. Up to the current uncommitted batch is repeated after interruption.

Open **Drive tools → Resume indexing** after reconnecting to continue all paused, connected sources. **Rescan sources** resumes a pending checkpoint for the selected source (or all connected sources). Adding a source with unfinished indexing also resumes it. Completed source reopening and ordinary drive reconnect remain catalog-only. Paused sources appear in Drive tools after restarting Luma, including an interrupted first import.

Resume resolves the same volume identity at its current mount path. Completed folders are checked by directory attributes, without enumerating their files. In the interrupted folder, filenames are enumerated again but committed file records are reused. This is not a filesystem journal: file content edits that leave directory timestamps unchanged require a fresh rescan after completion. If a completed folder's timestamp changed or it disappeared, Luma conservatively starts a fresh snapshot. Do not edit the source while indexing if you want maximum checkpoint reuse.

**Restart selected indexing** confirms discarding the selected source's checkpoint and starts a fresh traversal. The published gallery and originals are retained. **Indexing report** shows paused progress and inaccessible folders. Access-denied folders are skipped with a warning; an incomplete traversal never prunes existing catalog entries. Other I/O errors pause the job for retry. Fix access and rescan to obtain a complete snapshot. A process-level lease prevents overlapping scanners; the operating system releases it after termination. Removing a source clears its checkpoints. In-app relocation invalidates affected paused snapshots so old paths cannot be republished.

## On this day

Choose **On this day** in the sidebar to see media captured on this month and day in earlier years, grouped by year with “years ago” labels. It defaults to the PC's current local date. The date picker lets you revisit another day; the selected date's year is the comparison year. Search, source, folder, album and format filters continue to apply. Reset returns to the full library. Normal selection, favorites, albums and file controls remain available.

Memories read saved, version-matched capture metadata and cached previews. They work offline without scanning or decoding originals. A missing preview shows a placeholder until the source is available. Files without capture dates, modification-date fallbacks, dates from the selected year or future years, and stale metadata are excluded. Use **Refresh metadata** on a connected source to enrich an older catalog. Current Windows metadata support does not treat video encoding dates as capture dates, so such videos stay in the regular library.

Dates follow the PC's local timezone, consistent with the timeline; original camera timezones may be unknown. February 29 memories appear only when February 29 is selected, without shifting them to February 28 or March 1. The empty state explains missing dates and offers another day. No uploads, face analysis, automatic albums or duplicate media files are created.
