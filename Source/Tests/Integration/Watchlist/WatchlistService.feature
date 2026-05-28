Feature: Watchlist management

  Scenario: Add media with manual origin persists entry
    Given a media item exists with Plex id "plex-100"
    When I add it to the watchlist with Manual origin
    Then the watchlist contains 1 entry with Manual origin

  Scenario: Adding an existing entry is idempotent
    Given a media item is already on the watchlist with Remote origin and Plex id "plex-102"
    When I add it to the watchlist with Manual origin
    Then the watchlist contains 1 entry with Remote origin

  Scenario: Remove entry hard deletes it
    Given a media item is already on the watchlist with Manual origin and Plex id "plex-104"
    When I remove it from the watchlist
    Then the watchlist is empty
