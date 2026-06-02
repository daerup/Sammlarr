Feature: Watchlist outbox

  Background:
    Given the following media:
      | media     | ids                    | store |
      | Inception | tmdb:27205, plex:27205 | db    |

  Scenario: Adding a new item enqueues an Add operation
    When I add "Inception" to the watchlist
    Then the outbox contains 1 operation(s)
    And the outbox has a pending Add for "Inception"

  Scenario: Removing a synced item enqueues a Remove operation
    Given the following are on the watchlist:
      | media     |
      | Inception |
    When I remove "Inception" from the watchlist
    Then the outbox contains 1 operation(s)
    And the outbox has a pending Remove for "Inception"

  Scenario: Adding an item with a pending Remove cancels the Remove
    Given the following are on the watchlist:
      | media     |
      | Inception |
    And "Inception" has a pending Remove in the outbox
    When I add "Inception" to the watchlist
    Then the outbox is empty

  Scenario: Removing an item with a pending Add cancels the Add
    Given the following are on the watchlist:
      | media     |
      | Inception |
    And "Inception" has a pending Add in the outbox
    When I remove "Inception" from the watchlist
    Then the outbox is empty

  Scenario: Adding an empty collection does nothing
    When I add nothing to the watchlist
    Then the outbox is empty

  Scenario: Adding an already synced item does not enqueue an Add
    Given the following are on the watchlist:
      | media     |
      | Inception |
    When I add "Inception" to the watchlist
    Then the outbox is empty

  Scenario: Mixed batch partitions uncancels, pushes, and no-ops correctly
    Given additional media:
      | media      | ids                | store |
      | Fight Club | tmdb:550, plex:550 | db    |
      | The Matrix | tmdb:603, plex:603 | db    |
    And the following are on the watchlist:
      | media      |
      | Inception  |
      | The Matrix |
    And "Inception" has a pending Remove in the outbox
    When I add to the watchlist:
      | media      |
      | Inception  |
      | Fight Club |
      | The Matrix |
    Then the outbox contains 1 operation(s)
    And the outbox has a pending Add for "Fight Club"
