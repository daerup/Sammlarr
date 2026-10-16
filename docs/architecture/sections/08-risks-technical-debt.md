# 8. Risks and Technical Debt

Open risks in what is built, then deferred work. The idea to take away: most risks come from the outside services
Sammlarr depends on and from running in one process on one SQLite file. Limits and wait budgets named here are set per
service in `src/Web/appsettings.json` ([Lanes and HTTP resilience](06-crosscutting.md#lanes-and-http-resilience)).

## Risks

### Running the instance

| Risk | Impact | Mitigation |
|---|---|---|
| Key ring at rest | The data-protection keys sit unencrypted in `/data/dp-keys`, beside the secrets they decrypt. A bind-mounted `/data` must be writable by uid 1654. | Restrict access to the volume (the compose's named volume takes the image's ownership); an at-rest key protector is possible hardening. |
| Single instance only | A second replica contends on one SQLite file, `JobRunner` requeues every `Running` job at startup as abandoned, and the cached `ProviderConfig` and Radarr + Sonarr settings are never invalidated across processes. | Run one instance ([ADR 0004](../../adr/0004-store-all-state-in-one-sqlite-file.md)). |
| No job deadline | All feed runs, syncs and details fills share one queue that runs one job at a time, and a job has no deadline, so a hung job stalls all queued work until a restart. | Attempt timeouts, `MaxWait`, `EnrichWait` and a list load's `StallAfter` watchdog bound most waits. Fix: a per-job deadline in `JobRunner`. |
| Sync push assumes clean acks | `WantlistSyncEngine.ApplyBatchAsync` looks each sink ack up by media id; an ack for a title it did not send, or a second one, throws and fails the sync, and the batch stays pending to fail again. | None yet; guard the lookup. |
| In-app toasts for new event types | `WebUiNotificationSeeder` subscribes a user's in-app target only when the user is created, so an event type added later never reaches existing users. | Subscribe it by hand in Settings › Notifications. |
| Datasets hold the write lock | Replacing IMDb's ratings table holds the write lock for seconds a day, against the 5 s `busy_timeout`; the `VACUUM` that may follow rewrites the file under the lock and needs its size free on disk. | Loads run at 04:30 server time, after housekeeping at 03:00, one at a time (`DatasetGate`); a failed `VACUUM` waits for the next compaction. |

### Identity

| Risk | Impact | Mitigation |
|---|---|---|
| Identity on one shared key | A claim extends a stored title on one shared key without checking its other ids, so two titles a provider tags with the same key become one row. A title stored twice stays twice (its later entries settle as conflicts) until the nightly pass merges it, or for good if no second source can answer. | Provider keys are trusted; page links are no keys ([ADR 0018](../../adr/0018-claim-identity-before-hand-out-from-identity-keys-only.md)). The `DuplicateTitles` step merges a noted pair once a source outside the claim confirms. A multi-id agreement check is possible hardening. |

### Outside services

| Risk | Impact | Mitigation |
|---|---|---|
| Plex 429 storm | Titles that need Plex settle "couldn't look up" for that run. | Each Plex route has its own lane; Plex publishes no limits, so the configured ones are guesses. A 429 blocks or slows only its lane, other titles settle meanwhile, and nothing is cached unmatched, so the next run retries. |
| Plex's match endpoint is undocumented | Other ids become Plex ids only through `library/metadata/matches` (`src/API/Plex/PlexIdConverter.cs`). If Plex changes it, new titles for a Plex sink fail to identify: a 404 or empty answer settles "not found" (cached for an hour), a 5xx "couldn't look up". | `PlexMatchTests` and the E2E mapping `plex-discover/library-metadata-matches.json` pin Sammlarr's side of the format. A run where every Plex title settles "not found" points here. |
| Trakt's API Use Policy | Trakt allows a self-hosted tool on its own credentials but forbids mirroring its lists or passing its data on, has blocked services without notice, and deletes an API app unused for 30 days. | Each person brings their own client ID; Sammlarr reads only public lists for them and passes nothing on; the key row warns about the 30 days. |
| Trakt's rating for every title | Each title with a Trakt or IMDb id but no Trakt rating costs a Trakt request, so a large first load without MDBList waits on Trakt's lane. | `TraktTitles` reuses an answer for ten minutes under every id it carries; MDBList usually fills the rating first. |
| AniList's rate | Every anime a feed brings asks AniList, so a large anime feed's details wait on AniList's narrow lane. | `AniListTitles` reuses what a feed page read and asks the rest in batches. |
| MDBList's daily allowance | Feeds and enrichment share each key's daily allowance; once spent, MDBList answers the key with 429 until its reset. | `MdblistBudget` reads the allowance from MDBList's headers: title lookups stop at the last `MDBList:FeedReserve` requests, kept for list pages; the details fill stops once less than `BackgroundReserveShare` (half) of the day's allowance is left. Only missing fields are asked, in batches; a key's 429 blocks only its own lanes. |
| Lists through MDBList | IMDb and Letterboxd lists come only as MDBList external lists ([ADR 0012](../../adr/0012-read-imdb-trakt-and-letterboxd-lists-through-mdblist.md)): one on a free account, refreshed weekly. | The MDBList key row says so; a supporter account lifts the limits. |
| Fribb's anime id map has no licence | `anime-list-full.json` comes from a repository with no licence, but it is built from anime-offline-database, which is ODbL 1.0; an install with more than one person uses it publicly. | Downloaded at runtime, never shipped; Settings › About › Data sources says so and carries the ODbL notice with links to the licence, anime-offline-database and the project it continues. |

### Terms and marks

| Risk | Impact | Mitigation |
|---|---|---|
| AniList's competing-service clause | AniList's terms bar its API inside "competing" anime and manga list or tracker services, and a broad reading could cover a list curator. | None; Sammlarr only reads public lists, search and browse pages and details, and keeps no tracking of its own. |
| MyAnimeList's approval clause | MyAnimeList's API agreement (§3(a)(vii)) bars combining the API with software or services it has not approved in writing. | None yet; ask MyAnimeList for approval, or drop the provider if it objects. |
| TMDB's six-month cache cap | TMDB's terms cap caching at six months. The `CachedDetails` housekeeping step drops details unrefreshed for `FreshDays` + `DetailsUnusedDays` (194 days by default), and titles on a wantlist, the blocklist, the outbox or a kept run keep theirs until a load refreshes them. | None yet; a window of at most 180 days and a refresh for protected titles would close it. |
| IMDb's logo | IMDb's "Used with permission" covers its datasets, not its logo, which needs IMDb's own permission. | Kept by choice; the External IDs popover shows the logo beside its links, never as one. |
| Rotten Tomatoes' marks | Its licensing page routes any use of its marks and scores through a request, and none was filed. | Settings › About › Data sources shows ® and "Fandango Media, LLC", both verdicts flip at 60%, and a score links to the title's page when its Rotten Tomatoes id is known (a search otherwise); the request can follow before 1.0. |
| Letterboxd's square | Letterboxd's terms want its logo unaltered and taken from its brand page; the square is a Simple Icons glyph split into coloured dots. | Kept by choice; swap in Letterboxd's own file if it objects. |
| GitHub's mark | GitHub's logo rules forbid changing the mark's colours; the anime-lists tile uses Simple Icons' copy filled with two soft greys (`github-mark-light.svg`, `github-mark-dark.svg`). | Kept by choice; swap in GitHub's own black and white marks from github.com/logos if it objects. |

## Deferred work

| Item | State |
|---|---|
| Tooltip guard | `src/Web/wwwroot/js/tooltip-guard.js` works around two BlazorBlueprint 4.1.0 tooltip bugs (opening on a focus no Tab caused, staying visible after a raced close); `UiInvariantTests` catch markup changes. A screen reader moving focus without Tab gets no tooltip, and a nested trigger needs `AsChild="false" Class="inline-flex" Focusable="false"`. Remove it once `BbTooltip` is fixed. |
| Job queue surfacing | Home shows your own jobs only (`QueueSnapshot.OthersPending` is computed, not rendered), and an abandoned push has no retry action. |
| E2E circuit crash | Now and then a full E2E run trips the circuit-crash guard (`CircuitFailureSink`) while every test passes. Two causes are fixed: `PosterTrack`'s JS dispose ended in `TaskCanceledException` when the browser closed mid-dispose, and the wantlist sync dry run went on to the database after its page closed, where the circuit's disposed scope threw `ObjectDisposedException`. Others may remain; the guard prints each crash's whole exception, which names the component. |
| Result-based errors | Blocklist writes throw `UnknownMediaException` and wantlist writes fail on the foreign key, instead of returning a `Result`. |
| Release pipeline | Built: CI on pull requests and pushes to `main` (build, tests with a coverage gate, formatting, the licence and vulnerable-package gates, a reproducible-publish check, E2E on `main` and the Release PR, a Trivy scan of the Release PR's image), the PR-title check, release-please versioning, the MinVer version and the update badge, and on a `v*` tag the image, GHCR push, SBOMs, notices and a rebuild check ([Deployment View](05-deployment.md)). Not built: signing and provenance attestations (they need a public repository), an arm64 image. |
