Feature: TMDB trending media resolution

  Background:
    Given a TMDB account with API key "test-key"
    And media:
      | #            | ids                      |
      | Inception    | tmdb:27205, plex:27205   |
      | Interstellar | tmdb:157336, plex:157336 |

  Scenario: Trending page with a cached movie returns a resolved item
    Given TMDB trending page 1 includes:
      | #         | type  |
      | Inception | movie |
    And the library knows:
      | #         | with ids of |
      | Inception | tmdb, plex  |
    When I request trending page 1
    Then the trending results are:
      | kind     | title     | ids                    |
      | resolved | Inception | tmdb:27205, plex:27205 |

  Scenario: Trending page with a cached show returns a resolved item
    Given the library knows:
      | #            | ids                  | type |
      | Breaking Bad | tmdb:1396, plex:1396 | show |
    And TMDB trending page 1 includes:
      | #            | type |
      | Breaking Bad | show |
    When I request trending page 1
    Then the trending results are:
      | kind     | title        | ids                  |
      | resolved | Breaking Bad | tmdb:1396, plex:1396 |

  Scenario: Trending page with an uncached movie fetches and returns a resolved item
    Given a Plex account with token "plex-token"
    And TMDB trending page 1 includes:
      | #         | type  |
      | Inception | movie |
    And Plex holds:
      | #         | with ids of |
      | Inception | tmdb, plex  |
    And Plex search for "Inception" returns:
      | title     | id         |
      | Inception | plex:27205 |
    When I request trending page 1
    Then the trending results are:
      | kind     | title     | ids                    |
      | resolved | Inception | tmdb:27205, plex:27205 |

  Scenario: Trending page with an uncached movie that Plex cannot resolve returns an unresolved item
    Given a Plex account with token "plex-token"
    And TMDB trending page 1 includes:
      | #         | type  |
      | Inception | movie |
    And Plex search for "Inception" returns nothing
    When I request trending page 1
    Then the trending results are:
      | kind       | title     |
      | unresolved | Inception |

  Scenario: Trending page with an unknown media type returns an unresolved item
    Given TMDB trending page 1 includes:
      | #           | type   |
      | Some Person | person |
    When I request trending page 1
    Then the trending results are:
      | kind       | title       |
      | unresolved | Some Person |

  Scenario: A trending page with no results returns no items
    Given TMDB trending page 1 is empty
    When I request trending page 1
    Then the trending results are empty

  Scenario: Trending page preserves item order across resolved and unresolved items
    Given a Plex account with token "plex-token"
    And TMDB trending page 1 includes:
      | #                 | type   |
      | Inception         | movie  |
      | Christopher Nolan | person |
      | Interstellar      | movie  |
    And the library knows:
      | #         | with ids of |
      | Inception | tmdb, plex  |
    And Plex holds:
      | #            | with ids of |
      | Interstellar | tmdb, plex  |
    And Plex search for "Interstellar" returns:
      | title        | id          |
      | Interstellar | plex:157336 |
    When I request trending page 1
    Then the trending results are:
      | kind       | title             | ids                      |
      | resolved   | Inception         | tmdb:27205, plex:27205   |
      | unresolved | Christopher Nolan |                          |
      | resolved   | Interstellar      | tmdb:157336, plex:157336 |
