Feature: API key management

  Scenario: No key set returns null
    When I get the API key for TMDB
    Then the API key is null

  Scenario: Set key stores and retrieves successfully
    When I set the TMDB API key to "tmdb-key-12345"
    And I get the API key for TMDB
    Then the API key is "tmdb-key-12345"

  Scenario: Update key replaces existing value
    Given the TMDB API key is "old-key"
    When I set the TMDB API key to "new-key"
    And I get the API key for TMDB
    Then the API key is "new-key"

  Scenario: Clear key removes stored data
    Given the TMDB API key is "secret-key"
    When I clear the TMDB API key
    And I get the API key for TMDB
    Then the API key is null
