# Model a title as an identity plus optional details

| Status   | Date       |
|----------|------------|
| accepted | 2026-10-03 |

## Context

A title had seven shapes: complete and partial movies and shows, identified and unmatched wrappers,
and a cached form. Missing data was spelled `""`, `0`, `TimeSpan.Zero` or `DateOnly.MinValue`, a
provider's answer carried a placeholder `MediaId`, and freshness lived beside the media instead of
on the cache entry. Each shape meant another conversion, and the placeholder id once replaced a
title's real one during a merge.

## Decision

One `MediaDetails` record (`MovieDetails`, `ShowDetails`, `UnknownDetails`) whose fields are all
optional: a missing value is `null` from the provider mapper to the database column (missing genres
are an empty set, a missing rating is `RatingSummary.None`). A search provider or enricher answers
with `FoundMedia` (external ids and details, no `MediaId`); Core gives a title its `MediaId` and
calls it `MatchedMedia`, or `UnmatchedMedia` when it cannot match it, and the two form the
`MediaMatchResult` union. What is missing is computed (`MediaDetails.Missing`), never a
type. Fetch time and confirmed gaps sit on the cache entry (`CachedDetails`), which decides
freshness. `MediaId` is a Vogen value object, so it cannot be empty or defaulted.

Rejected: keeping the partial and complete pairs (every new field doubles the types and their
conversions); `Optional<T>` for each details field (the fields are read in markup and EF columns,
where `T?` is native); dropping `MediaType.Unknown` (search shows people as unmatched tiles).

## Consequences

* A title gains or loses a field without changing type, and a merge keeps the stored `MediaId` by construction.
* "Missing" has one meaning, so a blank string or a zero from a provider must be mapped to `null` at the edge, and the Architecture tier keeps the details columns nullable.
* Media details are a deliberate exception to `Optional<T>`: their missing values are `null`.
