Feature: Watchlist outbox

  Scenario: Adding a new item enqueues an Add operation
    Given the movie "Inception"
    And "Inception" is cached in the media database with "TmdbId, PlexId"
    When I add "Inception" to the watchlist
    Then the outbox contains 1 operation(s)
    And the outbox has a pending Add for "Inception"

  Scenario: Removing a synced item enqueues a Remove operation
    Given the movie "Inception"
    And "Inception" is cached in the media database with "TmdbId, PlexId"
    And "Inception" is on the watchlist
    When I remove "Inception" from the watchlist
    Then the outbox contains 1 operation(s)
    And the outbox has a pending Remove for "Inception"

  Scenario: Adding an item with a pending Remove cancels the Remove
    Given the movie "Inception"
    And "Inception" is cached in the media database with "TmdbId, PlexId"
    And "Inception" is on the watchlist
    And "Inception" has a pending Remove in the outbox
    When I add "Inception" to the watchlist
    Then the outbox is empty

  Scenario: Removing an item with a pending Add cancels the Add
    Given the movie "Inception"
    And "Inception" is cached in the media database with "TmdbId, PlexId"
    And "Inception" is on the watchlist
    And "Inception" has a pending Add in the outbox
    When I remove "Inception" from the watchlist
    Then the outbox is empty

  Scenario: Adding an empty collection does nothing
    When I add nothing to the watchlist
    Then the outbox is empty

  Scenario: Adding an already synced item does not enqueue an Add
    Given the movie "Inception"
    And "Inception" is cached in the media database with "TmdbId, PlexId"
    And "Inception" is on the watchlist
    When I add "Inception" to the watchlist
    Then the outbox is empty

  Scenario: Mixed batch partitions uncancels, pushes, and no-ops correctly
    Given the movie "Inception"
    And "Inception" is cached in the media database with "TmdbId, PlexId"
    And the movie "Fight Club"
    And "Fight Club" is cached in the media database with "TmdbId, PlexId"
    And the movie "The Matrix"
    And "The Matrix" is cached in the media database with "TmdbId, PlexId"
    And "Inception" is on the watchlist
    And "The Matrix" is on the watchlist
    And "Inception" has a pending Remove in the outbox
    When I add "Inception", "Fight Club", and "The Matrix" to the watchlist
    Then the outbox contains 1 operation(s)
    And the outbox has a pending Add for "Fight Club"
