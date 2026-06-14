Feature: TMDB rating retrieval

  Background:
    Given a TMDB account with API key "test-key"
    And media:
      | #          | ids                |
      | Fight Club | tmdb:550, plex:550 |

  Scenario Outline: A positive vote average delivers a rating
    Given TMDB holds:
      | #          | with ids of | vote_average | vote_count |
      | Fight Club | tmdb        | <average>    | <votes>    |
    When I fetch ratings for "Fight Club"
    Then the ratings are:
      | source | value     | votes   |
      | tmdb   | <average> | <votes> |

    Examples:
      | average | votes |
      | 8.4     | 23000 |
      | 6.5     | 0     |
      | 0.1     | 1     |

  Scenario: Vote average of zero returns no ratings
    Given TMDB holds:
      | #          | with ids of | vote_average | vote_count |
      | Fight Club | tmdb        | 0            | 0          |
    When I fetch ratings for "Fight Club"
    Then the result is empty

  Scenario: Ratings request with a non-TMDB ID returns no ratings
    When I fetch ratings using an IMDb ID
    Then the result is empty

  Scenario: Missing vote data returns no ratings
    Given TMDB holds:
      | #          | with ids of |
      | Fight Club | tmdb        |
    When I fetch ratings for "Fight Club"
    Then the result is empty

  Scenario: Ratings are retrieved for a show
    Given media:
      | #            | ids                  | type |
      | Breaking Bad | tmdb:1396, plex:1396 | show |
    And TMDB holds:
      | #            | with ids of | vote_average | vote_count |
      | Breaking Bad | tmdb        | 9.5          | 50000      |
    When I fetch ratings for "Breaking Bad"
    Then the ratings are:
      | source | value | votes |
      | tmdb   | 9.5   | 50000 |
