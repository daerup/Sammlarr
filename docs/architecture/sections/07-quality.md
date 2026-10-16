# 7. Quality Requirements

This chapter turns the four quality goals of [Introduction and Goals](01-introduction-goals.md) into scenarios: a
stimulus, the response, and the tests that hold it. The idea to take away: each quality claim is pinned by a named
test. Tiers and how to run them: [Testing](../testing.md).

## Quality tree

| Goal, in priority order | Refined into | Held by |
|---|---|---|
| Correctness of list building | A title is matched from identity keys only; one title is one library row; ids that would join two stored titles settle as a conflict, never a merge; a Mirror feed drops nothing after a load it cannot trust | Scenarios C and M |
| Resilience of provider calls | A 429 or a lookup outage slows a run and leaves titles unmatched, never dropped (only an unreadable list fails a run); a blocked service slows only the titles that need it | Scenario R |
| Per-user data isolation | Reads filtered to the current user, writes stamped with the owner, a query without a user throws, background work runs as the owner | Scenario I |
| Maintainability | Unions and strong types, strict layering, least visibility, one test naming rule; role enums and module wiring by hand, the compiler finding every switch a new service joins ([ADR 0010](../../adr/0010-split-the-ids-from-the-services-sammlarr-calls.md)); options registration, `SuppliedBy` and `AsyncMirror` generated | `tests/Architecture` (`AbsenceTests` among them); `RoleRegistrationTests`; analyzers `SAM0001` (test names), `SAM0002` and `SAM0003` (`Optional` and `Result` through their helpers) |

Scenario P, load speed, supports the first two goals.

## Scenarios

### C: identity claims

- **Stimulus**: a list yields two entries carrying TMDB 5, one of them also IMDb tt9, and a third whose ids sit on two
  stored titles.
- **Response**: each entry is claimed from its identity keys before it is handed out. Both TMDB 5 entries land on one
  row, which gains tt9, so they hand out the same `MediaId`. A claim extends at most one row, so the third entry settles
  unmatched as a conflict naming both titles (`UnmatchedMedia.ConflictsWith`) and the run completes; an enricher's key
  that would join two titles is dropped. Keys carry the media type, so a movie and a show sharing a number never meet;
  rating page links (Rotten Tomatoes, Metacritic, Letterboxd) are not keys, so remakes sharing a slug stay two titles.
- **Measure**: `ClaimRuleTests` and the CsCheck laws in `ClaimRuleLaws` (`tests/Domain/Media`); `IdentityClaimsTests`
  and `FeedRunIdentityTests` (`tests/Integration`); the nightly merge in `DuplicateTitlesTests` and `MediaMergeTests`.
- **Limit**: one shared key is enough to extend a row ("Identity on one shared key" in
  [Risks and Technical Debt](08-risks-technical-debt.md)).

### M: Mirror drops only on a conclusive load

- **Stimulus**: a Mirror feed's load is unfinished (still loading, or a list page failed), finds nothing, resolves
  nothing, admits nothing, or leaves a
  title that couldn't be looked up.
- **Response**: the plan is inconclusive and drops nothing. On a conclusive load an owned title goes only after it has
  been missing for `AbsenceThreshold` runs.
- **Measure**: `FeedPlanTests` and the property tests in `FeedPlanProperties` (`tests/Domain/Feed`).

### R: a service rate-limits or goes down

- **Stimulus**: during a feed run Plex, or any service, answers 429 or cannot be reached.
- **Response**: the run completes. A 429 blocks only its lane, for its `Retry-After`, or halves the lane's rate
  without one; repeated failures take the lane down. A title waits only while the block ends within the service's
  `MaxWait`; otherwise that path gives up and the title's later paths skip the service, so a storm is not multiplied.
  With no path left, the title settles "couldn't look up", which is not cached, so the next run retries. Other titles
  settle meanwhile.
  If nothing settles for `StallAfter`, the watchdog settles the rest and the run is recorded.
- **Measure**: `FeedRunApplyTests.RunAsync_WithTmdbBlocked_SettlesTheTitlesThatDontNeedTmdb`,
  `FeedRunTests.Run_WithAServiceBlockedForever_EndsAfterStallAfterWithEveryTitleSettled`, `AnsweringTests`, and
  `LaneGateTests` and `KeyedLaneTests` (`tests/API/Http`). Mechanism:
  [Lanes and HTTP resilience](06-crosscutting.md#lanes-and-http-resilience).

### I: per-user isolation

- **Stimulus**: user B opens feeds, wantlist or blocklist while user A's rows exist.
- **Response**: B sees only B's rows. A query on a user-owned table with no signed-in user throws.
- **Measure**: `UserIsolationTests` and `UserOwnedAccessGuardTests` (`tests/Integration/Identity`; the guard fails
  closed and covers every user-owned entity).

### P: a cached list loads again

- **Stimulus**: a feed whose titles are all stored and fresh runs again.
- **Response**: the library is read once per burst; a fresh stored title settles with no call; only misses are routed,
  and only for the fields the feed needs. Nothing caps the titles in flight, so a fully cached run is bound only by
  its list pages and the library reads.
- **Measure**: `CachedRunTests.RunAsync_ForAFullyCachedList_SendsOnlyItsPage`; by hand, the `[Explicit]` benchmark
  `ListLoadBenchmark` (how soon half of a list with stalling titles is out).
