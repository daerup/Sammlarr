Feature: Watchlist

  Background:
    Given the following media:
      | media      | ids                    | store |
      | Fight Club | tmdb:550, plex:550     | db    |
      | Inception  | tmdb:27205, plex:27205 | db    |

  Scenario: Adding media with Manual origin persists the entry
    When I add "Inception" to the watchlist with Manual origin
    Then the watchlist contains 1 entry with Manual origin

  Scenario: Adding an entry that is already present keeps its original origin
    Given the following are on the watchlist:
      | media     | origin |
      | Inception | Remote |
    When I add "Inception" to the watchlist with Manual origin
    Then the watchlist contains 1 entry with Remote origin

  Scenario: Removing an entry hard deletes it
    Given the following are on the watchlist:
      | media     | origin |
      | Inception | Manual |
    When I remove "Inception" from the watchlist
    Then the watchlist is empty

  Scenario: Getting watchlist items enriches entries with metadata and rating
    Given a Plex account with token "test-token"
    And a TMDB account with API key "test-key"
    And plex has:
      | media      |
      | Fight Club |
    And tmdb has:
      | media      | vote_average | vote_count |
      | Fight Club | 8.4          | 23000      |
    And the following are on the watchlist:
      | media      |
      | Fight Club |
    When I get the watchlist items
    Then the watchlist items are:
      | title      | poster | rating |
      | Fight Club | yes    | tmdb   |

  Scenario: Getting watchlist IDs returns IDs of all watchlisted items
    Given the following are on the watchlist:
      | media      |
      | Fight Club |
      | Inception  |
    When I get the watchlist IDs
    Then the watchlist IDs include:
      | title      |
      | Fight Club |
      | Inception  |
