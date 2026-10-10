# Gallery build: usage, architecture and acceptance

This build replaces the paged Phase 2 interface with a native chronological gallery, configurable views, versioned metadata and virtual albums. It keeps the existing catalog, volume identity and offline preview cache.

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

Scanning discovers files first and then enriches changed versions with Windows image/video properties. **Drive tools → Refresh metadata** enriches older catalogs without a full scan. Completed metadata survives cancellation and is retained offline. Unchanged versions are not decoded repeatedly.

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

Availability checks update source indicators without repeatedly rebuilding/reordering the gallery. Navigate or Reset to refresh the displayed item availability. A file action always resolves identity and validates its version again. No original is moved merely by scanning, enriching metadata or organizing an album.

## Verification

Core coverage includes cursor traversal with tied sort values in both directions, catalog migrations, albums following relocations, metadata invalidation/enrichment, folder depth and format filters, date jumps, collision-free transfer, cancellation, and all previous offline/cache regressions. Scale cases exercise 1k, 10k and 100k synthetic SQLite records and save first/next-batch and month-index timings with OS information. Those figures measure catalog queries, not USB throughput or full-library image decoding.

The Windows integration suite checks actual volume identity plus BMP, JPEG and PNG thumbnail decoding and dimensions. The native UI smoke uses 260 isolated synthetic images, scrolls across several internal batches and back, checks the peak decoded-image count, and exercises search, favorites, virtual albums, folders, clearing/preparing previews and a simulated unavailable source. It restarts the isolated library and verifies thumbnail/label preferences are restored. It writes screenshot, coverage and image-lifetime evidence.

Physical USB reconnects, low-disk faults, native Recycle Bin prompts, Explorer drag/drop and a diverse real-world codec set still need acceptance on the user's Windows bench. Synthetic CI screenshots are functional evidence, not a substitute for visual review with personal photos/videos. The build does not claim a measured 100k-image scrolling/frame-rate guarantee. Lightweight loaded records accumulate during exploration; decoded images are recycled. There is no cloud account, embedded player/editor, automatic original-file rearrangement, signing certificate or installer.
