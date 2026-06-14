Feature: MDBList feed loading

  Rule: Transport failures surface

    Scenario: Loading a feed without an API key throws
      Given media:
        | #          | ids                |
        | Fight Club | tmdb:550, plex:550 |
      And the MDBList feed "top-250" as "Top 250" contains:
        | ids      | title      | type  |
        | tmdb:550 | Fight Club | movie |
      When I try to load the feed "Top 250"
      Then an exception occurred with message containing "No MDBList API key"

    Scenario: A feed that returns a server error surfaces the failure
      Given an MdbList account with API key "test-key"
      And the MDBList feed "broken-list" as "Broken" returns HTTP 500
      When I try to load the feed "Broken"
      Then an exception occurred with message containing "500"

    Scenario: A feed that returns a malformed body fails to load
      Given an MdbList account with API key "test-key"
      And the MDBList feed "corrupt-list" as "Corrupt" returns invalid JSON
      When I try to load the feed "Corrupt"
      Then an exception of type "JsonException" occurred

  Rule: Loading and pagination

    Scenario: A feed that returns no movies or shows loads no items
      Given an MdbList account with API key "test-key"
      And the MDBList feed "empty-list" as "Empty" is empty
      When I load the feed "Empty"
      Then the item count is 0

    Scenario: A multi-page feed loads every page
      Given an MdbList account with API key "test-key"
      And the MDBList feed "big-list" as "Big" is paginated:
        | offset | titles       | hasMore |
        | 0      | Item1, Item2 | true    |
        | 1000   | Item3        | false   |
      When I load the feed "Big"
      Then the item count is 3

    Scenario: Duplicate feed entries are preserved in order
      Given an MdbList account with API key "test-key"
      And a Plex account with token "plex-token"
      And a TMDB account with API key "tmdb-key"
      And the library knows:
        | #          | ids                |
        | Fight Club | tmdb:550, plex:550 |
      And Plex holds:
        | #          | with ids of |
        | Fight Club | tmdb, plex  |
      And TMDB holds:
        | #          | with ids of |
        | Fight Club | tmdb        |
      And the MDBList feed "dupes-list" as "Dupes" contains:
        | ids      | title      | type  |
        | tmdb:550 | Fight Club | movie |
        | tmdb:550 | Fight Club | movie |
      When I load the feed "Dupes"
      Then the item count is 2

  Rule: Items that can't fully resolve still surface

    Scenario: A feed whose items all fail strict validation returns them as unresolved
      Given an MdbList account with API key "test-key"
      And the MDBList feed "loose-list" as "Loose" contains:
        | title      | type  |
        | No Ids One | movie |
        | No Ids Two | movie |
      When I load the feed "Loose"
      Then the item count is 2
      And all feed items are unresolved

    Scenario: A feed item whose metadata cannot be resolved is returned as unresolved
      Given an MdbList account with API key "test-key"
      And a Plex account with token "plex-token"
      And the library knows:
        | #          | ids                |
        | Fight Club | tmdb:550, plex:550 |
      And Plex lookup fails for "Fight Club"
      And the MDBList feed "failing-list" as "Failing" contains:
        | ids      | title      | type  |
        | tmdb:550 | Fight Club | movie |
      When I load the feed "Failing"
      Then the item count is 1
      And all feed items are unresolved

    Scenario: Feed item with no known IDs at all is still returned
      Given an MdbList account with API key "test-key"
      And the MDBList feed "mystery-list" as "Mystery" contains:
        | title         | type  |
        | Unknown Movie | movie |
      When I load the feed "Mystery"
      Then the feed items are:
        | title         |
        | Unknown Movie |

    Scenario: Feed item missing release year is tolerated
      Given an MdbList account with API key "test-key"
      And the MDBList feed "no-year-list" as "NoYear" contains:
        | title    | type  | year |
        | Orphaned | movie |      |
      When I load the feed "NoYear"
      Then the feed items are:
        | title    |
        | Orphaned |

    Scenario: Feed item with explicit null title is tolerated
      Given an MdbList account with API key "test-key"
      And the MDBList feed "null-title-list" as "NullTitle" contains:
        | title  | type  | ids       |
        | <null> | movie | tmdb:4242 |
      When I try to load the feed "NullTitle"
      Then the item count is 1

    Scenario: Feed item known only by IMDb id is still returned
      Given an MdbList account with API key "test-key"
      And a Plex account with token "plex-token"
      And media:
        | #         | ids                                    |
        | Inception | tmdb:27205, imdb:tt1375666, plex:27205 |
      And Plex holds:
        | #         | with ids of      |
        | Inception | tmdb, imdb, plex |
      And Plex search for "Inception" returns:
        | title     | id         |
        | Inception | plex:27205 |
      And the MDBList feed "imdb-only-list" as "ImdbOnly" contains:
        | ids            | title     | type  |
        | imdb:tt1375666 | Inception | movie |
      When I load the feed "ImdbOnly"
      Then the feed items are:
        | title     | poster |
        | Inception | yes    |

  Rule: Resolved items are enriched and their identity merged

    Scenario: Loaded feed item is enriched with metadata and rating
      Given an MdbList account with API key "test-key"
      And a Plex account with token "plex-token"
      And a TMDB account with API key "tmdb-key"
      And the library knows:
        | #          | ids                |
        | Fight Club | tmdb:550, plex:550 |
      And Plex holds:
        | #          | with ids of |
        | Fight Club | tmdb, plex  |
      And TMDB holds:
        | #          | with ids of | vote_average | vote_count |
        | Fight Club | tmdb        | 8.4          | 23000      |
      And the MDBList feed "top-250" as "Top 250" contains:
        | ids      | title      | type  |
        | tmdb:550 | Fight Club | movie |
      When I load the feed "Top 250"
      Then the feed items are:
        | title      | poster | rating |
        | Fight Club | yes    | tmdb   |

    Scenario: Feed with multiple movies resolves all items
      Given an MdbList account with API key "test-key"
      And a Plex account with token "plex-token"
      And a TMDB account with API key "tmdb-key"
      And the library knows:
        | #          | ids                                    |
        | Fight Club | tmdb:550, plex:550                     |
        | Inception  | tmdb:27205, imdb:tt1375666, plex:27205 |
      And Plex holds:
        | #          | with ids of      |
        | Fight Club | tmdb, plex       |
        | Inception  | tmdb, imdb, plex |
      And TMDB holds:
        | #          | with ids of | vote_average | vote_count |
        | Fight Club | tmdb        | 8.4          | 23000      |
        | Inception  | tmdb, imdb  | 7.8          | 15000      |
      And the MDBList feed "top-250" as "Top 250" contains:
        | ids        | title      | type  |
        | tmdb:550   | Fight Club | movie |
        | tmdb:27205 | Inception  | movie |
      When I load the feed "Top 250"
      Then the feed items are:
        | title      | rating |
        | Fight Club | tmdb   |
        | Inception  | tmdb   |

    Scenario: Loading a feed caches the item's metadata
      Given an MdbList account with API key "test-key"
      And a Plex account with token "plex-token"
      And a TMDB account with API key "tmdb-key"
      And the library knows:
        | #          | ids                |
        | Fight Club | tmdb:550, plex:550 |
      And Plex holds:
        | #          | with ids of |
        | Fight Club | tmdb, plex  |
      And TMDB holds:
        | #          | with ids of |
        | Fight Club | tmdb        |
      And the MDBList feed "top-250" as "Top 250" contains:
        | ids      | title      | type  |
        | tmdb:550 | Fight Club | movie |
      When I load the feed "Top 250"
      Then media is cached as:
        | media      | title      |
        | Fight Club | Fight Club |

    Scenario: Feed item with additional IDs merges new identifiers into existing entry
      Given an MdbList account with API key "test-key"
      And a Plex account with token "plex-token"
      And a TMDB account with API key "tmdb-key"
      And the library knows:
        | #          | ids                |
        | Fight Club | tmdb:550, plex:550 |
      And Plex holds:
        | #          | with ids of |
        | Fight Club | tmdb, plex  |
      And TMDB holds:
        | #          | with ids of |
        | Fight Club | tmdb        |
      And the MDBList feed "top-250" as "Top 250" contains:
        | ids                      | title      | type  |
        | tmdb:550, imdb:tt0137523 | Fight Club | movie |
      When I load the feed "Top 250"
      Then "Fight Club" has the IMDb ID "tt0137523" stored

    Scenario: Feed TMDB id conflicting with the resolved row is overridden by the provider
      Given an MdbList account with API key "test-key"
      And a Plex account with token "plex-token"
      And the library knows:
        | #         | ids                                    |
        | Inception | tmdb:27205, imdb:tt1375666, plex:27205 |
      And Plex holds:
        | #         | with ids of      |
        | Inception | tmdb, imdb, plex |
      And Plex search for "Inception" returns:
        | title     | id         |
        | Inception | plex:27205 |
      And the MDBList feed "conflict-list" as "Conflict" contains:
        | ids        | title     | type  |
        | tmdb:99999 | Inception | movie |
      When I load the feed "Conflict"
      Then "Inception" has the TMDB ID "27205" stored

    Scenario: Mixed-type feed surfaces movies and shows
      Given an MdbList account with API key "test-key"
      And a Plex account with token "plex-token"
      And a TMDB account with API key "tmdb-key"
      And the library knows:
        | #            | ids                                    | type  |
        | Inception    | tmdb:27205, imdb:tt1375666, plex:27205 | movie |
        | Breaking Bad | tmdb:1396, plex:1396                   | show  |
      And Plex holds:
        | #            | with ids of      |
        | Inception    | tmdb, imdb, plex |
        | Breaking Bad | tmdb, plex       |
      And TMDB holds:
        | #            | with ids of |
        | Inception    | tmdb, imdb  |
        | Breaking Bad | tmdb        |
      And the MDBList feed "mixed-list" as "Mixed" contains:
        | ids        | title        | type  |
        | tmdb:27205 | Inception    | movie |
        | tmdb:1396  | Breaking Bad | show  |
      When I load the feed "Mixed"
      Then the item count is 2

    Scenario: Feed item title differs from cached row title
      Given an MdbList account with API key "test-key"
      And a Plex account with token "plex-token"
      And a TMDB account with API key "tmdb-key"
      And the library knows:
        | #         | ids                                    |
        | Inception | tmdb:27205, imdb:tt1375666, plex:27205 |
      And Plex holds:
        | #         | with ids of      |
        | Inception | tmdb, imdb, plex |
      And TMDB holds:
        | #         | with ids of |
        | Inception | tmdb, imdb  |
      And the MDBList feed "fuzzy-list" as "Fuzzy" contains:
        | ids        | title            | type  |
        | tmdb:27205 | INCEPTION (2010) | movie |
      When I load the feed "Fuzzy"
      Then "Inception" has the TMDB ID "27205" stored
