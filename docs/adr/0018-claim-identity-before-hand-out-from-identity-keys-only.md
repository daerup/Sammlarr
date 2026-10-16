# Claim identity before hand-out from identity keys only

| Status   | Date       |
|----------|------------|
| accepted | 2026-10-10 |

## Context

A feed load gave each new title a provisional id in memory, handed it out, and decided the stored identity only when it
wrote everything at the end of the run. Rating page slugs (Rotten Tomatoes, Metacritic, Letterboxd) counted as ids, and
remakes share them: both Annie films carry `annie`. So the end of a run merged two films into one row, lost ids it had
already handed out, or found one title's ids on two rows. Where should a title's identity be decided, and from which
ids?

## Decision

Identity is decided from identity keys only, and written before a title is handed out. Each id type is an identity key
(`IIdentityKey`: TMDB, IMDb, TheTVDB, Plex, MDBList, Trakt, MyAnimeList, TVmaze, AniList) or a page link (`IPageLink`:
Rotten Tomatoes, Metacritic, Letterboxd), by the exhaustive `MediaProvider.Role` switch; page links hang off a title's
details (`MediaPageLinks`), never off its identity. Every path that identifies a title claims its keys through one
claim lane per scope (`IIdentityClaims`), which commits what is queued in one transaction under the media write lock.
The pure rule, `ClaimRule.Decide`, extends at most one stored title and never merges two, never adds a second key of
one source or a key of another media type, and lets a claim pinned to a title drop what it cannot take. The database
holds the same line: a key belongs to one title, and a unique index allows one key per source per title. A claim that
would join two stored titles settles the title unmatched as a conflict, carrying the titles it touched; a search hit
like that uses the stored title that holds the search provider's own key, and is not addable when none does. A title
is handed out only with its stored `MediaId`.

Rejected: a provisional id in memory, settled when the run ends (an id handed out could be absorbed into another title
and never stored); merging the two stored titles on a conflict (one shared id would fold two shows into one; merging
waits for a second source, in `database-housekeeping`); keeping slugs as ids and dropping a contested one from both
titles (ADR 0015, superseded: the id came back whenever one of the titles refreshed); one write transaction per list
page (a page's identities would exist only once its slowest title settled, so the hand-out would wait for it).

## Consequences

* An id handed out is always stored, and nothing absorbs it later.
* Remakes that share a rating slug stay two titles, and both may show the same page link.
* A title whose ids sit on two stored titles settles as a conflict on every load until the pair is merged. Run history
  and the live view count it, and a Mirror feed counts the titles it owns among them as seen.
* Keys an enricher finds are claimed after the title's details, pinned to it; a key that would join two titles is
  dropped from the title.
* One shared key is still enough to extend a stored title; there is no multi-id agreement check.
* A database that holds two keys of one source on a title cannot take the unique index: migrating it fails, saying to
  delete it or use the staging Reset.

## Notes

* 2026-10-16: the migration history was reset to one baseline
  ([ADR 0003](0003-use-ef-core-migrations-for-the-schema.md)), so a database from before it no longer migrates at
  all, and the startup message about two keys of one source is gone.
