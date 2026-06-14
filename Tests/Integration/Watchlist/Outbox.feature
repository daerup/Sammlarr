Feature: Watchlist outbox

  Background:
    Given the library knows:
      | #         | ids                    |
      | Inception | tmdb:27205, plex:27205 |

  Rule: Adding to the watchlist

    Scenario: Adding a new item enqueues an Add operation
      When I add "Inception" to the watchlist
      Then the outbox is:
        | operation | #         |
        | Add       | Inception |

    Scenario: Adding an item with a pending Remove cancels the Remove
      Given the local watchlist has:
        | #         |
        | Inception |
      And the outbox has:
        | operation | #         |
        | Remove    | Inception |
      When I add "Inception" to the watchlist
      Then the outbox is empty

    Scenario: Adding an empty collection leaves the outbox untouched
      Given the outbox has:
        | operation | #         |
        | Add       | Inception |
      When I add nothing to the watchlist
      Then the outbox is:
        | operation | #         |
        | Add       | Inception |

    Scenario: Adding an already synced item does not enqueue an Add
      Given the local watchlist has:
        | #         |
        | Inception |
      When I add "Inception" to the watchlist
      Then the outbox is empty

    Scenario: Adding an item that already has a pending Add does not duplicate it
      Given the outbox has:
        | operation | #         |
        | Add       | Inception |
      When I add "Inception" to the watchlist
      Then the outbox is:
        | operation | #         |
        | Add       | Inception |

  Rule: Removing from the watchlist

    Scenario: Removing a synced item enqueues a Remove operation
      Given the local watchlist has:
        | #         |
        | Inception |
      When I remove "Inception" from the watchlist
      Then the outbox is:
        | operation | #         |
        | Remove    | Inception |

    Scenario: Removing an item with a pending Add cancels the Add
      Given the local watchlist has:
        | #         |
        | Inception |
      And the outbox has:
        | operation | #         |
        | Add       | Inception |
      When I remove "Inception" from the watchlist
      Then the outbox is empty

    Scenario: Removing an item that already has a pending Remove does not duplicate it
      Given the local watchlist has:
        | #         |
        | Inception |
      And the outbox has:
        | operation | #         |
        | Remove    | Inception |
      When I remove "Inception" from the watchlist
      Then the outbox is:
        | operation | #         |
        | Remove    | Inception |

    Scenario: Removing an empty collection leaves the outbox untouched
      Given the outbox has:
        | operation | #         |
        | Add       | Inception |
      When I remove nothing from the watchlist
      Then the outbox is:
        | operation | #         |
        | Add       | Inception |

  Rule: Batch operations

    Scenario: Adding the same item twice in one batch enqueues a single Add
      When I add to the watchlist:
        | #         |
        | Inception |
        | Inception |
      Then the outbox is:
        | operation | #         |
        | Add       | Inception |

    Scenario: Mixed batch partitions uncancels, pushes, and no-ops correctly
      Given the library knows:
        | #          | ids                |
        | Fight Club | tmdb:550, plex:550 |
        | The Matrix | tmdb:603, plex:603 |
      And the local watchlist has:
        | #          |
        | Inception  |
        | The Matrix |
      And the outbox has:
        | operation | #         |
        | Remove    | Inception |
      When I add to the watchlist:
        | #          |
        | Inception  |
        | Fight Club |
        | The Matrix |
      Then the outbox is:
        | operation | #          |
        | Add       | Fight Club |
