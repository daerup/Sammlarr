Feature: MDBList feed loading

  Background:
    Given the following media:
      | media      | ids                                    |
      | Fight Club | tmdb:550, plex:550                     |
      | Inception  | tmdb:27205, imdb:tt1375666, plex:27205 |

  Scenario: Loading a feed without an API key throws
    Given the MDBList feed "top-250" as "Top 250" contains:
      | ids      | title      | type  |
      | tmdb:550 | Fight Club | movie |
    When I try to load the feed "Top 250"
    Then an exception occurred with message containing "No MDBList API key"

  Scenario: Loaded feed item is enriched with metadata and rating
    Given an MdbList account with API key "test-key"
    And a Plex account with token "plex-token"
    And a TMDB account with API key "tmdb-key"
    And media in db:
      | media      |
      | Fight Club |
    And plex has:
      | media      |
      | Fight Club |
    And tmdb has:
      | media      | vote_average | vote_count |
      | Fight Club | 8.4          | 23000      |
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
    And media in db:
      | media      |
      | Fight Club |
      | Inception  |
    And plex has:
      | media      |
      | Fight Club |
      | Inception  |
    And tmdb has:
      | media      | vote_average | vote_count |
      | Fight Club | 8.4          | 23000      |
      | Inception  | 7.8          | 15000      |
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
    And media in db:
      | media      |
      | Fight Club |
    And plex has:
      | media      |
      | Fight Club |
    And tmdb has:
      | media      | vote_average | vote_count |
      | Fight Club | 8.4          | 23000      |
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
    And media in db:
      | media      |
      | Fight Club |
    And plex has:
      | media      |
      | Fight Club |
    And tmdb has:
      | media      | vote_average | vote_count |
      | Fight Club | 8.4          | 23000      |
    And the MDBList feed "top-250" as "Top 250" contains:
      | ids                      | title      | type  |
      | tmdb:550, imdb:tt0137523 | Fight Club | movie |
    When I load the feed "Top 250"
    Then "Fight Club" has the IMDb ID "tt0137523" stored

  Scenario: Feed item with no known IDs at all is still returned
    Given an MdbList account with API key "test-key"
    And the MDBList feed "mystery-list" as "Mystery" contains:
      | title         | type  |
      | Unknown Movie | movie |
    When I load the feed "Mystery"
    Then the feed items are:
      | title         |
      | Unknown Movie |

  Scenario: Duplicate feed entries are preserved in order
    Given an MdbList account with API key "test-key"
    And a Plex account with token "plex-token"
    And a TMDB account with API key "tmdb-key"
    And media in db:
      | media      |
      | Fight Club |
    And plex has:
      | media      |
      | Fight Club |
    And tmdb has:
      | media      | vote_average | vote_count |
      | Fight Club | 8.4          | 23000      |
    And the MDBList feed "dupes-list" as "Dupes" contains:
      | ids      | title      | type  |
      | tmdb:550 | Fight Club | movie |
      | tmdb:550 | Fight Club | movie |
    When I load the feed "Dupes"
    Then the item count is 2

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

  Scenario: Feed TMDB id conflicting with the resolved row is overridden by the provider
    Given an MdbList account with API key "test-key"
    And a Plex account with token "plex-token"
    And media in db:
      | media     |
      | Inception |
    And plex has:
      | media     |
      | Inception |
    And plex search for "Inception" returns:
      | media     | id         |
      | Inception | plex:27205 |
    And the MDBList feed "conflict-list" as "Conflict" contains:
      | ids        | title     | type  |
      | tmdb:99999 | Inception | movie |
    When I load the feed "Conflict"
    Then the item count is 1
    And "Inception" has the TMDB ID "27205" stored

  Scenario: Feed item known only by IMDb id is still returned
    Given an MdbList account with API key "test-key"
    And a Plex account with token "plex-token"
    And plex has:
      | media     |
      | Inception |
    And plex search for "Inception" returns:
      | media     | id         |
      | Inception | plex:27205 |
    And the MDBList feed "imdb-only-list" as "ImdbOnly" contains:
      | ids            | title     | type  |
      | imdb:tt1375666 | Inception | movie |
    When I load the feed "ImdbOnly"
    Then the item count is 1

  Scenario: Mixed-type feed surfaces movies and shows
    Given an MdbList account with API key "test-key"
    And a Plex account with token "plex-token"
    And a TMDB account with API key "tmdb-key"
    And additional media:
      | media        | type | ids                  |
      | Breaking Bad | show | tmdb:1396, plex:1396 |
    And media in db:
      | media        |
      | Inception    |
      | Breaking Bad |
    And plex has:
      | media        |
      | Inception    |
      | Breaking Bad |
    And tmdb has:
      | media        | vote_average | vote_count |
      | Inception    | 7.8          | 15000      |
      | Breaking Bad | 9.5          | 50000      |
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
    And media in db:
      | media     |
      | Inception |
    And plex has:
      | media     |
      | Inception |
    And tmdb has:
      | media     | vote_average | vote_count |
      | Inception | 7.8          | 15000      |
    And the MDBList feed "fuzzy-list" as "Fuzzy" contains:
      | ids        | title            | type  |
      | tmdb:27205 | INCEPTION (2010) | movie |
    When I load the feed "Fuzzy"
    Then the item count is 1
