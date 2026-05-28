Feature: MDBList feed loading

  Scenario: Loading a feed without an API key throws
    Given the movie "Fight Club"
    And the MDBList feed "top-250" as "Top 250"
    And "Top 250" contains "Fight Club"
    When I try to load the feed "Top 250"
    Then an exception occurred with message containing "No MDBList API key"

  Scenario: Loaded feed item is enriched with metadata and rating
    Given an MdbList account with API key "test-key"
    And a Plex account with token "plex-token"
    And a TMDB account with API key "tmdb-key"
    And the movie "Fight Club"
    And "Fight Club" is cached in the media database with "TmdbId, PlexId"
    And the MDBList feed "top-250" as "Top 250"
    And "Top 250" contains "Fight Club"
    And the Plex provider can resolve "Fight Club"
    And the TMDB API returns vote average 8.4 and vote count 23000 for "Fight Club"
    When I load the feed "Top 250"
    Then the item count is 1
    And the first item title is "Fight Club"
    And the first item has a poster
    And the first item has a TMDB rating

  Scenario: Feed with multiple movies resolves all items
    Given an MdbList account with API key "test-key"
    And a Plex account with token "plex-token"
    And a TMDB account with API key "tmdb-key"
    And the movie "Fight Club"
    And "Fight Club" is cached in the media database with "TmdbId, PlexId"
    And the movie "Inception"
    And "Inception" is cached in the media database with "TmdbId, PlexId"
    And the MDBList feed "top-250" as "Top 250"
    And "Top 250" contains "Fight Club"
    And "Top 250" contains "Inception"
    And the Plex provider can resolve "Fight Club"
    And the Plex provider can resolve "Inception"
    And the TMDB API returns vote average 8.4 and vote count 23000 for "Fight Club"
    And the TMDB API returns vote average 7.8 and vote count 15000 for "Inception"
    When I load the feed "Top 250"
    Then the item count is 2

  Scenario: Subsequent feed load uses cached metadata
    Given an MdbList account with API key "test-key"
    And a Plex account with token "plex-token"
    And a TMDB account with API key "tmdb-key"
    And the movie "Fight Club"
    And "Fight Club" is cached in the media database with "TmdbId, PlexId"
    And the MDBList feed "top-250" as "Top 250"
    And "Top 250" contains "Fight Club"
    And the Plex provider can resolve "Fight Club"
    And the TMDB API returns vote average 8.4 and vote count 23000 for "Fight Club"
    When I load the feed "Top 250"
    And I reload the feed "Top 250" without provider stubs
    Then the item count is 1
    And the first item title is "Fight Club"

  Scenario: Feed item with additional IDs merges new identifiers into existing entry
    Given an MdbList account with API key "test-key"
    And a Plex account with token "plex-token"
    And a TMDB account with API key "tmdb-key"
    And the movie "Fight Club"
    And "Fight Club" is cached in the media database with "TmdbId, PlexId"
    And the MDBList feed "top-250" as "Top 250"
    And "Top 250" contains "Fight Club" with IMDb ID "tt0137523"
    And the Plex provider can resolve "Fight Club"
    And the TMDB API returns vote average 8.4 and vote count 23000 for "Fight Club"
    When I load the feed "Top 250"
    Then "Fight Club" has the external ID "tt0137523" stored
