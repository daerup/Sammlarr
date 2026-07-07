Feature: Blacklist management

  Background:
    Given the library knows:
      | #          | ids                |
      | Fight Club | tmdb:550, plex:550 |

  Scenario: Adding a movie to the blacklist persists the entry
    When I add "Fight Club" to the blacklist
    Then the blacklist is:
      | #          |
      | Fight Club |

  Scenario: Adding a movie already on the blacklist is idempotent
    Given the blacklist has:
      | #          |
      | Fight Club |
    When I add "Fight Club" to the blacklist
    Then the blacklist is:
      | #          |
      | Fight Club |

  Scenario: Removing a movie from the blacklist deletes the entry
    Given the blacklist has:
      | #          |
      | Fight Club |
    When I remove "Fight Club" from the blacklist
    Then the blacklist is empty

  Scenario: Getting blacklist IDs returns IDs of all blacklisted items
    Given the library knows:
      | #         | ids                    |
      | Inception | tmdb:27205, plex:27205 |
    And the blacklist has:
      | #          |
      | Fight Club |
      | Inception  |
    When I get the blacklist IDs
    Then the blacklist IDs include:
      | title      |
      | Fight Club |
      | Inception  |

  Scenario: Getting blacklist items enriches entries with metadata and rating
    Given a Plex account with token "test-token"
    And a TMDB account with API key "test-key"
    And Plex holds:
      | #          | with ids of |
      | Fight Club | tmdb, plex  |
    And TMDB holds:
      | #          | with ids of | vote_average | vote_count |
      | Fight Club | tmdb        | 8.4          | 23000      |
    And the blacklist has:
      | #          |
      | Fight Club |
    When I get the blacklist items
    Then the blacklist items are:
      | title      | poster | rating |
      | Fight Club | yes    | tmdb   |

  Scenario: Adding a media that is not in the library is rejected
    When I try to add a media that is not in the library to the blacklist
    Then an exception of type "UnknownMediaException" occurred
