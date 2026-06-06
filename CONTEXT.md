# Home Screen Sections

A Jellyfin plugin that replaces the vanilla web client home screen with Modular Home — a configurable, row-based layout where each row is a Section instance.

## Language

**Modular Home**:
The plugin-controlled home screen layout that wholesale replaces Jellyfin's default home view for users who enable it.
_Avoid_: HSS home, custom home

**Section**:
A registered row type identified by a stable key (e.g. LatestMovies, BecauseYouWatched). Defines how to create instances and fetch row content.
_Avoid_: Section type, row type, plugin section

**Section instance**:
A concrete row rendered on Modular Home. Multiple Section instances may share one order index (e.g. shuffled BecauseYouWatched rows).
_Avoid_: Row, home row, section copy

**Order index**:
An admin-configured position group that determines render order. Section types assigned to the same order index are resolved together and may produce multiple Section instances.
_Avoid_: Order slot, position, group index

**Vacant order index**:
An order index with no Section types assigned. Treated as immediately satisfied when evaluating prefix cohesion — the resolver never waits on or builds it.
_Avoid_: Gap, empty slot, hole

**pageHash**:
A client-generated identifier that ties paginated Section requests to one build session. All scroll pages on a single home visit reuse the same pageHash.
_Avoid_: Page ID, session token, cache key

**Cohesive prefix**:
The longest initial run of order indices (from lowest) where each index is either vacant or fully built. Pagination slices flattened Section instances across that run — not across order-index groups.
_Avoid_: Complete prefix, satisfied range, ready block

**Jellyseerr internal URL**:
The backend URL the plugin uses for Jellyseerr API requests from the server.
_Avoid_: Jellyseerr URL, API URL

**Jellyseerr display URL**:
The base URL embedded in Discover card links so users open Jellyseerr in the browser. When unset, Modular Home falls back to the internal URL.
_Avoid_: External URL, frontend URL

**Discover item**:
Media returned from Jellyseerr discover endpoints that is not yet present in the Jellyfin library.
_Avoid_: Jellyseerr result, discover row

**Modular Home user settings**:
Per-user persisted choices for which Sections appear on Modular Home, stored independently of admin defaults.
_Avoid_: User preferences, home config, user config

**Effective Modular Home settings**:
The settings the server actually applies for a user — persisted user choices merged with admin defaults and locked Sections.
_Avoid_: Resolved settings, computed settings, merged config

**Locked section**:
A Section whose enabled state is controlled by admin configuration and cannot be changed by the user (`AllowUserOverride == false`).
_Avoid_: Admin section, forced section, pinned section
