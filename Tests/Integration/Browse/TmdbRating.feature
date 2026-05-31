Feature: TMDB rating retrieval

  Background:
    Given a TMDB account with API key "test-key"
    And the following media:
      | media      | ids                     |
      | Fight Club | tmdb:550, plex:550 |

  Scenario: TMDB responds with vote data delivers a rating
    Given tmdb has:
      | media      | vote_average | vote_count |
      | Fight Club | 8.4          | 23000      |
    When I fetch ratings for "Fight Club"
    Then the ratings are:
      | source | value | votes |
      | tmdb   | 8.4   | 23000 |

  Scenario: Vote average of zero returns no ratings
    Given tmdb has:
      | media      | vote_average | vote_count |
      | Fight Club | 0            | 0          |
    When I fetch ratings for "Fight Club"
    Then the result is empty

  Scenario: Ratings request with a non-TMDB ID returns no ratings
    When I fetch ratings using an IMDb ID
    Then the result is empty

  Scenario: Missing vote data returns no ratings
    Given TMDB returns no vote data for "Fight Club"
    When I fetch ratings for "Fight Club"
    Then the result is empty
