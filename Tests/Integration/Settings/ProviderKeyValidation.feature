Feature: Provider API key validation

  Scenario: TMDB request fails when no key is stored
    When I request TMDB ratings for movie 550
    Then no TMDB ratings are returned

  Scenario: TMDB ratings returned when the correct key is stored
    Given a TMDB account with API key "valid-tmdb-key"
    And the TMDB API returns rating data for movie 550
    When I request TMDB ratings for movie 550
    Then TMDB ratings are returned

  Scenario: MdbList request fails when no key is stored
    When I send a request to the MdbList user endpoint
    Then an exception of type "InvalidOperationException" occurred

  Scenario: MdbList request succeeds when the correct key is stored
    Given an MdbList account with API key "valid-mdblist-key"
    When I send a request to the MdbList user endpoint
    Then the MdbList request succeeds

  Scenario: Plex push fails when no credentials are stored
    When I add Plex item "plex-100" to the watchlist
    Then the Plex push fails

  Scenario: Plex push succeeds when the correct token is stored
    Given a Plex account with token "plex-access-token"
    When I add Plex item "plex-100" to the watchlist
    Then the Plex push succeeds
