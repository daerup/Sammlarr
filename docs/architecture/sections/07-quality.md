# 7. Quality Requirements

## Quality Tree

The quality goals from [Introduction and Goals](01-introduction-goals.md), in priority order, refined into the
attributes that make them concrete and where each is enforced.

| Goal | Refined into | Enforced by |
|---|---|---|
| Correctness of list building | Identify matches the right title, from identity keys only (a rating page link never matches or merges); one title is one library row, its identity written before it is handed out; ids that would join two stored titles settle the title unmatched as a conflict, never a silent merge and never a failed run; a bad, unfinished or empty load never evicts the wrong items | The identity claim lane and its pure rule ([Runtime View](04-runtime.md), scenario C), keyed by the one-key-per-source index; the Mirror conclusiveness rule, under which a load that did not finish, came back empty or left a title Sammlarr couldn't look up drops nothing ([Runtime View](04-runtime.md)) |
| Resilience of provider calls | A 429 storm or outage degrades to partial results, never a failed run; each service is paced by its own published limits, so a blocked service slows only the titles that need it; transient faults retry through the lane, sustained ones take the lane down | The lanes and the HTTP pipeline ([Cross-cutting Concepts](06-crosscutting.md)); scenario R |
| Per-user data isolation | Reads are filtered to the current user; writes are stamped to the owner; the filter fails closed; background work runs under the right user | EF query filters, the ownership stamper and `IUserScope` ([Cross-cutting Concepts](06-crosscutting.md)); scenario I |
| Maintainability | Strong typing and unions over primitives; strict layer dependencies; provider wiring generated, not hand-written; least visibility; one test naming convention | Architecture tests; the module and generator patterns ([Cross-cutting Concepts](06-crosscutting.md)); the `SAM0001`, `SAM0002` and `SAM0003` analyzers and `AbsenceTests`; the tiers in `testing.md` |

## Quality Scenarios

**Scenario C (identity claims)**: A list yields entries with overlapping ids: two that both carry TMDB 5, one of them also
IMDb tt9, and one whose ids sit on two titles already stored. _Response:_ each entry is claimed before it is handed out,
from its identity keys only. The first claim for TMDB 5 creates or extends one row; the second finds that row and
extends it with tt9, so both entries hand out the same `MediaId`. A claim extends at most one stored row, never adds a
second key of one source or a key of another media type, and never merges two rows, so the entry whose ids sit on two
stored titles settles unmatched as a conflict, carrying both titles (`ConflictsWith`), while the run completes and the
two rows stay as they are. An enricher's key that would join two titles is dropped from the title it was found for.
MediaType is part of a key, so a movie and a show that share a number never meet. Rating page links (Rotten Tomatoes,
Metacritic, Letterboxd) are no keys: remakes that share a slug stay two titles. _Measure:_ `ClaimRuleTests`, the
`ClaimRuleLaws` property test (no key on two rows, no row with two keys of a source, a pinned claim names exactly the
keys it leaves to another row), `IdentityClaimsTests` and
`FeedRunIdentityTests` pass. _Limit:_ one shared key is enough to extend a row, with no multi-id agreement check, so a
provider that tags two titles with the same key folds the second into the first; and a title stored twice stays
twice until the nightly pass merges it on a second source's word (`DuplicateTitlesTests`, `MediaMergeTests`;
[Risks and Technical Debt](08-risks-technical-debt.md)).

**Scenario I (isolation)**: User B requests feeds/wantlist/blocklist while User A's rows exist.
_Response:_ B sees only B's rows; a query with no authenticated user throws rather than returning
everyone's. _Measure:_ the isolation integration tests pass; the current-user lookup is fail-closed.

**Scenario R (resilience)**: A feed loads while Plex returns 429 or is unreachable. _Response:_ the run completes and no
exception escapes. A 429 blocks only the Plex lane it came from, for its `Retry-After`, or halves that lane's rate
without one; repeated failures take the lane down for a while. A title asks a blocked Plex only while the block ends within its `MaxWait` (90
seconds) and then waits in the lane; a longer block, a down lane or an unavailable answer settles the
title couldn't look up, and its other routes skip Plex for the rest of its resolution, so the storm is not amplified.
Titles that need nothing from Plex settle meanwhile. Nothing is cached as unmatched, so the next run retries them, and
a Mirror run with such a title drops nothing. If nothing settles for `StallAfter` (3 minutes), the watchdog settles the
rest couldn't look up and the run is recorded. _Measure:_ `FeedRunApplyTests.RunAsync_WithTmdbBlocked_SettlesTheTitlesThatDontNeedTmdb`,
`FeedRunTests.Run_WithAServiceBlockedForever_EndsAfterStallAfterWithEveryTitleSettled`, `AnsweringTests` and the lane
tests in `Tests/Shared/Concurrency` pass.

**Scenario P (performance)**: A feed of N items is loaded again. _Response:_ the library is read once per burst; a stored,
fresh title settles at once with no call; only misses are routed, and only the fields the feed needs are asked. No
count of titles is capped: every title runs its own chain, and each service is paced only by its own lanes, so a
fully cached run is bound by its list pages and the library reads. _Measure:_
`CachedRunTests.RunAsync_ForAFullyCachedList_SendsOnlyItsPage` passes; `CachedRunBenchmark` reports the time and the
remote calls of a cached run of 4,000 titles, and `ListLoadBenchmark` how soon half of a list with stalling titles is
out.
