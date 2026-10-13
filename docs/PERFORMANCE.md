# Large-library performance — v0.4.0

Measured in [run 38307725690](https://github.com/smile-plzz/Luma/actions/runs/38307725690), source `a1c03b8323e5b80644b090bf69da88ffec92ae64`. Fixture: 200,000 synthetic SQLite records, four runner processors, 120 items per page. These are single-run measurements, not percentiles or a physical-drive benchmark.

| Environment | First page including count | Next page, count reused | Month breakdown | Previous capture-sort expression only |
| --- | ---: | ---: | ---: | ---: |
| Windows | 8.78 ms | 0.60 ms | 245.90 ms | 67.99 ms |
| Linux | 7.58 ms | 1.29 ms | 530.85 ms | 88.63 ms |

The previous-expression comparison executes the former COALESCE(capture date, modification date) ordering with version-matched metadata, without its extra count or annotation/identity joins. It is a conservative comparison of sorting cost, not an end-to-end old-application benchmark. New filtered views still compute counts and month breakdowns; month-to-month navigation reuses its breakdown. Scanning and metadata writes can change timings.

[Windows raw measurement](performance/windows-200000.json) · [Linux raw measurement](performance/linux-200000.json)

## Improvements

Capture-date ordering uses a transactional index maintained when media or metadata changes. Stale capture metadata falls back to modification time. Upgrade builds the index locally once. Simple counts avoid unnecessary joins; subsequent pages reuse the view total.

Metadata enrichment reads only missing/stale versions in batches of 100. Readers close before native decoding; completed batches share a write transaction. Unchanged versions do not open originals. Scanning uses prepared statements, directory-enumeration attributes, a pending-folder index and a running checkpoint count. Recycled tiles cancel queued preview requests; decoded images remain tied to realized tiles.

## Limits and bench evaluation

Initial discovery still walks a new drive. Windows metadata extraction and preview codecs still open originals; HDD latency and file types can keep initial imports lengthy. No measured physical scan-time reduction is claimed. Known-drive reconnect remains catalog-first without an automatic scan.

Search, filtered counts and month breakdowns can visit many catalog records. Lightweight loaded records accumulate during unrestricted scrolling; Select all matching results deliberately loads the full match set. Use month browsing and smaller selections for a 200,000-item collection. Full-window data virtualization is not claimed.

Compare first usable gallery, warm month switching, scrolling, discovery and date-reading durations separately on the real bench. Record HDD/USB connection, file types, count and cache state. Exercise pause/restart, offline browsing and clipboard copies into a disposable folder. Database results do not certify USB throughput, 200,000-image decoding, high-DPI layouts or frame rate.

## Exclusions and packaging update

Reversible folder, source and global extension rules now filter browsing and future scans. Use Settings or Refresh → Manage exclusions to restore them; Reset does not remove rules. Originals and saved organization are retained. Changing rules invalidates affected paused traversal checkpoints, not the published catalog. See [usage and limits](EXCLUSIONS.md) and [executable branding](BRANDING.md).
