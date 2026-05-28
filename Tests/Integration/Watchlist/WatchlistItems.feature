Feature: Watchlist items

  Scenario: Getting watchlist items enriches entries with metadata and rating
    Given a Plex account with token "test-token"
    And a TMDB account with API key "test-key"
    And the movie "Fight Club"
    And "Fight Club" is cached in the media database with "TmdbId, PlexId"
    And "Fight Club" is on the watchlist
    And the Plex provider can resolve "Fight Club"
    And the TMDB API returns vote average 8.4 and vote count 23000 for "Fight Club"
    When I get the watchlist items
    Then the item count is 1
    And the first item title is "Fight Club"
    And the first item has a poster
    And the first item has a TMDB rating

  Scenario: Getting watchlist IDs returns IDs of all watchlisted items
    Given the movie "Fight Club"
    And "Fight Club" is cached in the media database with "TmdbId, PlexId"
    And "Fight Club" is on the watchlist
    And the movie "Inception"
    And "Inception" is cached in the media database with "TmdbId, PlexId"
    And "Inception" is on the watchlist
    When I get the watchlist IDs
    Then the IDs include "Fight Club"
    And the IDs include "Inception"
