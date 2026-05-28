Feature: TMDB trending media resolution

  Background:
    Given a TMDB account with API key "test-key"

  Scenario: Trending page with a cached movie returns a resolved item
    Given the movie "Inception"
    And "Inception" is cached in the media database with "TmdbId, PlexId"
    And the TMDB trending endpoint includes "Inception"
    When I request trending page 1
    Then the result contains 1 item(s)
    And item 1 is a resolved TrendingMedia with title "Inception"

  Scenario: Trending page with an uncached movie fetches and returns a resolved item
    Given a Plex account with token "plex-token"
    And the movie "Inception"
    And the Plex provider can resolve "Inception"
    And the TMDB trending endpoint includes "Inception"
    When I request trending page 1
    Then the result contains 1 item(s)
    And item 1 is a resolved TrendingMedia with title "Inception"

  Scenario: Trending page with an uncached movie that Plex cannot resolve returns an unresolved item
    Given a Plex account with token "plex-token"
    And the movie "Inception"
    And the Plex provider cannot resolve "Inception"
    And the TMDB trending endpoint includes "Inception"
    When I request trending page 1
    Then the result contains 1 item(s)
    And item 1 is an UnresolvedTrendingMedia with title "Inception"

  Scenario: Trending page with an unknown media type returns an unresolved item
    Given the person "Some Person"
    And the TMDB trending endpoint includes "Some Person"
    When I request trending page 1
    Then the result contains 1 item(s)
    And item 1 is an UnresolvedTrendingMedia with title "Some Person"

  Scenario: Trending page preserves item order across resolved and unresolved items
    Given a Plex account with token "plex-token"
    And the movie "Inception"
    And "Inception" is cached in the media database with "TmdbId, PlexId"
    And the person "Christopher Nolan"
    And the movie "Interstellar"
    And the Plex provider can resolve "Interstellar"
    And the TMDB trending endpoint includes "Inception"
    And the TMDB trending endpoint includes "Christopher Nolan"
    And the TMDB trending endpoint includes "Interstellar"
    When I request trending page 1
    Then the result contains 3 item(s)
    And item 1 is a resolved TrendingMedia with title "Inception"
    And item 2 is an UnresolvedTrendingMedia with title "Christopher Nolan"
    And item 3 is a resolved TrendingMedia with title "Interstellar"
