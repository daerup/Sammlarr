Feature: Media search

  Background:
    Given a Plex account with token "plex-token"

  Scenario: A search for a known movie returns it with the reconciled identity
    Given the library knows:
      | #          | ids                |
      | Fight Club | tmdb:550, plex:550 |
    And Plex search for "Fight Club" returns:
      | title      | id       |
      | Fight Club | plex:550 |
    When I search for "Fight Club" with types "Movie"
    Then the search results are:
      | title      |
      | Fight Club |
    And the search result "Fight Club" has the id "tmdb:550"
    And the search result "Fight Club" has the id "plex:550"

  Scenario: A search with no matches returns no results
    Given Plex search for "Nonexistent" returns nothing
    When I search for "Nonexistent" with types "Movie"
    Then the search results are empty

  Scenario: A search returns both movies and shows
    Given the library knows:
      | #            | ids                  | type  |
      | Fight Club   | tmdb:550, plex:550   | movie |
      | Breaking Bad | tmdb:1396, plex:1396 | show  |
    And Plex search for "thriller" returns:
      | title        | id        | type  |
      | Fight Club   | plex:550  | movie |
      | Breaking Bad | plex:1396 | show  |
    When I search for "thriller" with types "Movie,Show"
    Then the search results are:
      | title        | type  |
      | Fight Club   | Movie |
      | Breaking Bad | Show  |
