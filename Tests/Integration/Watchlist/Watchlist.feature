Feature: Watchlist management

  Background:
    Given the library knows:
      | #          | ids                    |
      | Fight Club | tmdb:550, plex:550     |
      | Inception  | tmdb:27205, plex:27205 |

  Scenario: Adding media with Manual origin persists the entry
    When I add "Inception" to the watchlist with Manual origin
    Then the watchlist contains 1 entry with Manual origin

  Scenario: Adding an entry that is already present keeps its original origin
    Given the local watchlist has:
      | #         | origin |
      | Inception | Remote |
    When I add "Inception" to the watchlist with Manual origin
    Then the watchlist contains 1 entry with Remote origin

  Scenario: Removing an entry hard deletes it
    Given the local watchlist has:
      | #         | origin |
      | Inception | Manual |
    When I remove "Inception" from the watchlist
    Then the watchlist is empty

  Scenario: Getting watchlist items enriches entries with metadata and rating
    Given a Plex account with token "test-token"
    And a TMDB account with API key "test-key"
    And Plex holds:
      | #          | with ids of |
      | Fight Club | tmdb, plex  |
    And TMDB holds:
      | #          | with ids of | vote_average | vote_count |
      | Fight Club | tmdb        | 8.4          | 23000      |
    And the local watchlist has:
      | #          |
      | Fight Club |
    When I get the watchlist items
    Then the watchlist items are:
      | title      | poster | rating |
      | Fight Club | yes    | tmdb   |

  Scenario: Getting watchlist IDs returns IDs of all watchlisted items
    Given the local watchlist has:
      | #          |
      | Fight Club |
      | Inception  |
    When I get the watchlist IDs
    Then the watchlist IDs include:
      | title      |
      | Fight Club |
      | Inception  |

  Scenario: Adding media without the watchlist provider's id is rejected
    Given the library knows:
      | #       | ids        |
      | Solaris | tmdb:11321 |
    When I try to add "Solaris" to the watchlist
    Then an exception of type "MissingWatchlistExternalIdException" occurred

  Scenario: Removing media without the watchlist provider's id is rejected
    Given the library knows:
      | #       | ids        |
      | Solaris | tmdb:11321 |
    And the local watchlist has:
      | #       |
      | Solaris |
    When I try to remove "Solaris" from the watchlist
    Then an exception of type "MissingWatchlistExternalIdException" occurred
