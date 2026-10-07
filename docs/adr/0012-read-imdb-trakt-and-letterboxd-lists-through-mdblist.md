# Read IMDb, Trakt and Letterboxd lists through MDBList

| Status   | Date       |
|----------|------------|
| accepted | 2026-10-05 |

## Context

People keep lists on IMDb, Trakt and Letterboxd. IMDb has no public list API, Letterboxd's API is invitation-only, and
Trakt's needs an OAuth app per install. MDBList mirrors all three as external lists and serves them through the API
Sammlarr already uses for feeds.

## Decision

IMDb lists, watchlists and charts, Trakt public lists and Letterboxd lists are feeds through MDBList's external lists
(`GET /external/lists/{id}/items`), never native integrations. The gaps are accepted: a Trakt watchlist cannot be
followed (only your own, through Trakt sync on mdblist.com), Trakt's charts are MDBList lists with a Trakt filter, a free
MDBList account adds one external list refreshed weekly (supporters more, daily), and an owner's lists stop updating
120 days after their last login.

Rejected: native IMDb (scraping, against its terms), native Letterboxd (no open API), native Trakt (an OAuth app and
token storage for one more list source).

## Consequences

* One feed provider, one key and one rate budget cover four list sites.
* The external-list limits above are MDBList's, recorded in the risks, and a list site Sammlarr cannot reach through MDBList is not followed at all.