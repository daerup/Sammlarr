Feature: TMDB rating retrieval

  Background:
    Given a TMDB account with API key "test-key"
    And the movie "Fight Club"

  Scenario: TMDB responds with vote data delivers a rating
    Given the TMDB API returns vote average 8.4 and vote count 23000 for "Fight Club"
    When I fetch ratings for "Fight Club"
    Then the result contains 1 rating
    And the rating source is TMDB
    And the normalized value is 8.4
    And the vote count is 23000

  Scenario: Vote average of zero returns no ratings
    Given the TMDB API returns vote average 0 and vote count 0 for "Fight Club"
    When I fetch ratings for "Fight Club"
    Then the result is empty

  Scenario: Ratings request with a non-TMDB ID returns no ratings
    When I fetch ratings using an IMDb ID
    Then the result is empty

  Scenario: Missing vote data returns no ratings
    Given the TMDB API returns no vote data for "Fight Club"
    When I fetch ratings for "Fight Club"
    Then the result is empty
