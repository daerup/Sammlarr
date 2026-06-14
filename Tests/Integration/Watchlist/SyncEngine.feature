Feature: Watchlist sync engine

  Background:
    Given a Plex account with token "plex-token"
    And the library knows:
      | #         | ids                    |
      | Inception | tmdb:27205, plex:27205 |

  Scenario: A remote-only item is pulled into the local watchlist
    Given the remote watchlist has:
      | #         |
      | Inception |
    When the watchlist syncs
    Then the watchlist contains 1 entry with Remote origin
    And the outbox is empty
    And the remote watchlist contains "Inception"
    And the sync result is:
      | inserted |
      | 1        |

  Scenario: A local item missing from the remote is hard deleted
    Given the local watchlist has:
      | #         | origin |
      | Inception | Manual |
    And the remote watchlist is empty
    When the watchlist syncs
    Then the watchlist is empty
    And the remote watchlist is empty
    And the sync result is:
      | hard deleted |
      | 1            |

  Scenario: An item on both the remote and the local watchlist is left untouched
    Given the local watchlist has:
      | #         | origin |
      | Inception | Manual |
    And the remote watchlist has:
      | #         |
      | Inception |
    When the watchlist syncs
    Then the watchlist contains 1 entry with Manual origin
    And the outbox is empty
    And the remote watchlist contains "Inception"
    And the sync is unchanged

  Scenario: A pending Add is pushed to the provider and cleared from the outbox
    Given the remote watchlist is empty
    And the outbox has:
      | operation | #         |
      | Add       | Inception |
    When the watchlist syncs
    Then the outbox is empty
    And the remote watchlist contains "Inception"
    And the sync result is:
      | pushed adds |
      | 1           |

  Scenario: A pending Add the provider already reflects is confirmed without re-pushing
    Given the outbox has:
      | operation | #         |
      | Add       | Inception |
    And the remote watchlist has:
      | #         |
      | Inception |
    When the watchlist syncs
    Then the watchlist contains 1 entry with Remote origin
    And the outbox is empty
    And the remote watchlist contains "Inception"
    And the sync result is:
      | inserted | confirmed |
      | 1        | 1         |

  Scenario: A pending Remove the provider already reflects is confirmed and the entry is dropped
    Given the local watchlist has:
      | #         | origin |
      | Inception | Manual |
    And the outbox has:
      | operation | #         |
      | Remove    | Inception |
    And the remote watchlist is empty
    When the watchlist syncs
    Then the watchlist is empty
    And the outbox is empty
    And the remote watchlist is empty

  Scenario: A pending Remove for an item still on the remote is pushed but kept locally until confirmed
    Given the local watchlist has:
      | #         | origin |
      | Inception | Remote |
    And the outbox has:
      | operation | #         |
      | Remove    | Inception |
    And the remote watchlist has:
      | #         |
      | Inception |
    When the watchlist syncs
    Then the outbox is empty
    And the watchlist contains 1 entry with Remote origin
    And the remote watchlist does not contain "Inception"

  Scenario: A push the provider rejects is kept in the outbox
    Given the remote watchlist is empty
    And the watchlist provider rejects pushes
    And the outbox has:
      | operation | #         |
      | Add       | Inception |
    When the watchlist syncs
    Then the outbox is:
      | operation | #         | attempts |
      | Add       | Inception | 1        |
    And the remote watchlist is empty
    And the sync result is:
      | remote failures |
      | 1               |

  Scenario: A Remove the provider rejects is kept in the outbox and stays on the remote
    Given the local watchlist has:
      | #         | origin |
      | Inception | Remote |
    And the remote watchlist has:
      | #         |
      | Inception |
    And the watchlist provider rejects pushes
    And the outbox has:
      | operation | #         |
      | Remove    | Inception |
    When the watchlist syncs
    Then the outbox is:
      | operation | #         | attempts |
      | Remove    | Inception | 1        |
    And the watchlist contains 1 entry with Remote origin
    And the remote watchlist contains "Inception"
    And the sync result is:
      | remote failures |
      | 1               |

  Scenario: A pending Add for media without a pushable id is kept in the outbox
    Given the library knows:
      | #       | ids        |
      | Solaris | tmdb:11321 |
    And the remote watchlist is empty
    And the outbox has:
      | operation | #       |
      | Add       | Solaris |
    When the watchlist syncs
    Then the outbox is:
      | operation | #       | attempts |
      | Add       | Solaris | 1        |
    And the remote watchlist is empty

  Scenario: A failed remote fetch aborts the sync without mutating local state
    Given the local watchlist has:
      | #         | origin |
      | Inception | Manual |
    And the outbox has:
      | operation | #         |
      | Remove    | Inception |
    And the remote watchlist is unavailable
    When the watchlist syncs
    Then the watchlist contains 1 entry with Manual origin
    And the outbox is:
      | operation | #         |
      | Remove    | Inception |

  Scenario: A dry run previews pull and push changes without applying them
    Given the library knows:
      | #          | ids                |
      | Fight Club | tmdb:550, plex:550 |
    And the local watchlist has:
      | #         | origin |
      | Inception | Manual |
    And the outbox has:
      | operation | #         |
      | Add       | Inception |
    And the remote watchlist has:
      | #          |
      | Fight Club |
    When I perform a dry run
    Then the dry run plans:
      | direction | operation | #          |
      | Pull      | Add       | Fight Club |
      | Push      | Add       | Inception  |
    And the watchlist contains 1 entry with Manual origin
    And the outbox is:
      | operation | #         |
      | Add       | Inception |

  Scenario: A dry run reports an error when the remote is unavailable and leaves state untouched
    Given the local watchlist has:
      | #         | origin |
      | Inception | Manual |
    And the remote watchlist is unavailable
    When I perform a dry run
    Then the dry run reports an error
    And the watchlist contains 1 entry with Manual origin
