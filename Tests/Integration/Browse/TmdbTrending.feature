Feature: TMDB trending media resolution

  Background:
    Given a TMDB account with API key "test-key"
    And the following media:
      | media        | ids                      |
      | Inception    | tmdb:27205, plex:27205   |
      | Interstellar | tmdb:157336, plex:157336 |

  Scenario: Trending page with a cached movie returns a resolved item
    Given TMDB trending page 1 includes:
      | title     | type  |
      | Inception | movie |
    And media in db:
      | media     |
      | Inception |
    When I request trending page 1
    Then the trending results are:
      | kind     | title     |
      | resolved | Inception |

  Scenario: Trending page with an uncached movie fetches and returns a resolved item
    Given a Plex account with token "plex-token"
    And TMDB trending page 1 includes:
      | title     | type  |
      | Inception | movie |
    And plex has:
      | media     |
      | Inception |
    And plex search for "Inception" returns:
      | media     | id         |
      | Inception | plex:27205 |
    When I request trending page 1
    Then the trending results are:
      | kind     | title     |
      | resolved | Inception |

  Scenario: Trending page with an uncached movie that Plex cannot resolve returns an unresolved item
    Given a Plex account with token "plex-token"
    And TMDB trending page 1 includes:
      | title     | type  |
      | Inception | movie |
    And plex search for "Inception" returns nothing
    When I request trending page 1
    Then the trending results are:
      | kind       | title     |
      | unresolved | Inception |

  Scenario: Trending page with an unknown media type returns an unresolved item
    Given TMDB trending page 1 includes:
      | title       | type   |
      | Some Person | person |
    When I request trending page 1
    Then the trending results are:
      | kind       | title       |
      | unresolved | Some Person |

  Scenario: Trending page preserves item order across resolved and unresolved items
    Given a Plex account with token "plex-token"
    And TMDB trending page 1 includes:
      | title             | type   |
      | Inception         | movie  |
      | Christopher Nolan | person |
      | Interstellar      | movie  |
    And media in db:
      | media     |
      | Inception |
    And plex has:
      | media        |
      | Interstellar |
    And plex search for "Interstellar" returns:
      | media        | id          |
      | Interstellar | plex:157336 |
    When I request trending page 1
    Then the trending results are:
      | kind       | title             |
      | resolved   | Inception         |
      | unresolved | Christopher Nolan |
      | resolved   | Interstellar      |
