Feature: Media metadata invalidation

  Background:
    Given current time is "01.06.2024"
    And a Plex account with token "test-token"
    And a TMDB account with API key "test-key"
    And the following media:
      | media      | ids                |
      | Fight Club | tmdb:550, plex:550 |
    And plex has:
      | media      | title  |
      | Fight Club | <null> |
    And tmdb has:
      | media      | title      | vote_average | vote_count |
      | Fight Club | Live Title | 8.4          | 23000      |

  Scenario: Invalidated metadata is re-fetched on the next read
    Given media in cache:
      | media      | title        | fetched    |
      | Fight Club | Cached Title | 31.05.2024 |
    When I get details for "Fight Club"
    Then the detail title is "Cached Title"
    When I invalidate "Fight Club"
    And I get details for "Fight Club"
    Then the detail title is "Live Title"
