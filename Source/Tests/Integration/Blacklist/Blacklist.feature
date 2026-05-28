Feature: Blacklist management

  Scenario: Adding a movie to the blacklist persists the entry
    Given the movie "Fight Club"
    And "Fight Club" is cached in the media database with "TmdbId, PlexId"
    When I add "Fight Club" to the blacklist
    Then the blacklist contains 1 entry

  Scenario: Removing a movie from the blacklist deletes the entry
    Given the movie "Fight Club"
    And "Fight Club" is cached in the media database with "TmdbId, PlexId"
    And "Fight Club" is on the blacklist
    When I remove "Fight Club" from the blacklist
    Then the blacklist is empty

  Scenario: Getting blacklist IDs returns IDs of all blacklisted items
    Given the movie "Fight Club"
    And "Fight Club" is cached in the media database with "TmdbId, PlexId"
    And "Fight Club" is on the blacklist
    And the movie "Inception"
    And "Inception" is cached in the media database with "TmdbId, PlexId"
    And "Inception" is on the blacklist
    When I get the blacklist IDs
    Then the IDs include "Fight Club"
    And the IDs include "Inception"

  Scenario: Getting blacklist items enriches entries with metadata and rating
    Given a Plex account with token "test-token"
    And a TMDB account with API key "test-key"
    And the movie "Fight Club"
    And "Fight Club" is cached in the media database with "TmdbId, PlexId"
    And "Fight Club" is on the blacklist
    And the Plex provider can resolve "Fight Club"
    And the TMDB API returns vote average 8.4 and vote count 23000 for "Fight Club"
    When I get the blacklist items
    Then the item count is 1
    And the first item title is "Fight Club"
    And the first item has a poster
    And the first item has a TMDB rating
