# Keep an id two stored titles claim on neither

| Status   | Date       |
|----------|------------|
| accepted | 2026-10-08 |

## Context

A title's ids come from identification and from its enrichers, and an enricher can tag two different
stored titles with one id: both Hunter x Hunter shows (1999 and 2011) come back with the Metacritic
slug `hunterxhunter`. The write buffer's flush then found one title's ids on two rows, threw
`MediaIdentityConflictException` and rolled back the whole batch, so the feed load failed and nothing
it fetched was saved, on every load.

## Decision

When a title being saved has its own stored row and one of its ids sits on another stored row, the
title stays on its row and the id is removed from the other row and added to neither; for the rest of
that save it is not added back. Neither title can be shown to own it, so neither keeps it. A new title,
with no stored row, whose ids sit on two stored rows still throws, as identify already leaves such a
title unmatched.

Rejected: the first-stored row keeps it (the order of loads, not evidence, picks the owner); merging
the two rows (one shared id would fold two different shows into one); a persisted list of contested ids
so the id never returns (another table for what is mostly a link id; worth it only if the flip-flop
below shows up in practice).

## Consequences

* A provider's wrong id costs only that id: the load completes and its details are saved.
* The id is not remembered as contested, so when only one of the two titles refetches its details
  (they go stale after 14 days) it gains the id again until the other one refetches.
* The rule covers every id, not only link ids: a sink id two stored titles claim is dropped from both,
  and the title then has it looked up again when the sink needs it.
