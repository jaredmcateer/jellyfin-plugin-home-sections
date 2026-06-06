# pageHash build cache TTL

Each Modular Home visit generates a pageHash that ties paginated Section requests to one in-memory build. Previously these builds lived for the process lifetime with no eviction. We now expire builds after a configurable idle period (default 2 hours via `PageHashCacheTtlMinutes`), using `LastAccessed` on each cache entry. When a client sends a pageHash whose build was evicted, the server transparently starts a fresh build — no client change required. The 2-hour default balances memory use against movie-length Jellyfin sessions where users return to home mid-watch.
