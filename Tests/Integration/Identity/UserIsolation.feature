Feature: Per-user data isolation

  Data created by one user is invisible to another; each user gets their own scope.

  Scenario: Feeds are private to each user
    Given I am signed in as "Alice"
    And a feed named "Weekly"
    When I sign out
    And I sign in as "Bob"
    Then I have no feeds
    When I sign out
    And I sign in as "Alice"
    Then the feed "Weekly" is listed

  Scenario: API keys are private to each user
    Given I am signed in as "Alice"
    And a stored TMDB API key
    When I sign out
    And I sign in as "Bob"
    Then I have no stored API keys

  Scenario: Credentials are private to each user
    Given I am signed in as "Alice"
    And a stored Plex credential
    When I sign out
    And I sign in as "Bob"
    Then I have no stored credentials
