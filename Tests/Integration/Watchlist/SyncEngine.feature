Feature: Watchlist sync engine

  Background:
    Given a Plex account with token "plex-token"
    And the following media:
      | media     | ids                    |
      | Inception | tmdb:27205, plex:27205 |
    And media in db:
      | media     |
      | Inception |

  Scenario: A remote-only item is pulled into the local watchlist
    Given the remote watchlist has:
      | media     |
      | Inception |
    When the watchlist syncs
    Then the watchlist contains 1 entry with Remote origin
    And the outbox is empty
    And the remote watchlist contains "Inception"

  Scenario: A local item missing from the remote is hard deleted
    Given the following are on the watchlist:
      | media     | origin |
      | Inception | Manual |
    And the remote watchlist is empty
    When the watchlist syncs
    Then the watchlist is empty
    And the remote watchlist is empty

  Scenario: An item on both the remote and the local watchlist is left untouched
    Given the following are on the watchlist:
      | media     | origin |
      | Inception | Remote |
    And the remote watchlist has:
      | media     |
      | Inception |
    When the watchlist syncs
    Then the watchlist contains 1 entry with Remote origin
    And the outbox is empty
    And the remote watchlist contains "Inception"

  Scenario: A pending Add is pushed to the provider and cleared from the outbox
    Given the remote watchlist is empty
    And "Inception" has a pending Add in the outbox
    When the watchlist syncs
    Then the outbox is empty
    And the remote watchlist contains "Inception"

  Scenario: A pending Add the provider already reflects is confirmed without re-pushing
    Given "Inception" has a pending Add in the outbox
    And the remote watchlist has:
      | media     |
      | Inception |
    When the watchlist syncs
    Then the watchlist contains 1 entry with Remote origin
    And the outbox is empty
    And the remote watchlist contains "Inception"

  Scenario: A pending Remove the provider already reflects is confirmed and the entry is dropped
    Given the following are on the watchlist:
      | media     | origin |
      | Inception | Manual |
    And "Inception" has a pending Remove in the outbox
    And the remote watchlist is empty
    When the watchlist syncs
    Then the watchlist is empty
    And the outbox is empty
    And the remote watchlist is empty

  Scenario: A pending Remove for an item still on the remote is pushed but kept locally until confirmed
    Given the following are on the watchlist:
      | media     | origin |
      | Inception | Remote |
    And "Inception" has a pending Remove in the outbox
    And the remote watchlist has:
      | media     |
      | Inception |
    When the watchlist syncs
    Then the outbox is empty
    And the watchlist contains 1 entry with Remote origin
    And the remote watchlist does not contain "Inception"

  Scenario: A push the provider rejects is kept in the outbox
    Given the remote watchlist is empty
    And the watchlist provider rejects pushes
    And "Inception" has a pending Add in the outbox
    When the watchlist syncs
    Then the outbox has a pending Add for "Inception"
    And the outbox records 1 failed attempt(s) for "Inception"
    And the remote watchlist is empty

  Scenario: A pending Add for media without a pushable id is kept in the outbox
    Given additional media:
      | media   | ids        |
      | Solaris | tmdb:11321 |
    And media in db:
      | media   |
      | Solaris |
    And the remote watchlist is empty
    And "Solaris" has a pending Add in the outbox
    When the watchlist syncs
    Then the outbox has a pending Add for "Solaris"
    And the outbox records 1 failed attempt(s) for "Solaris"
    And the remote watchlist is empty

  Scenario: A failed remote fetch aborts the sync without mutating local state
    Given the following are on the watchlist:
      | media     | origin |
      | Inception | Manual |
    And "Inception" has a pending Remove in the outbox
    And the remote watchlist is unavailable
    When the watchlist syncs
    Then the watchlist contains 1 entry with Manual origin
    And the outbox has a pending Remove for "Inception"
