Feature: Blacklist management

  Background:
    Given the following media:
      | media      | ids                |
      | Fight Club | tmdb:550, plex:550 |
    And media in db:
      | media      |
      | Fight Club |

  Scenario: Adding a movie to the blacklist persists the entry
    When I add "Fight Club" to the blacklist
    Then the blacklist contains 1 entry

  Scenario: Adding a movie already on the blacklist is idempotent
    Given the following are blacklisted:
      | media      |
      | Fight Club |
    When I add "Fight Club" to the blacklist
    Then the blacklist contains 1 entry

  Scenario: Removing a movie from the blacklist deletes the entry
    Given the following are blacklisted:
      | media      |
      | Fight Club |
    When I remove "Fight Club" from the blacklist
    Then the blacklist is empty

  Scenario: Getting blacklist IDs returns IDs of all blacklisted items
    Given additional media:
      | media     | ids                    |
      | Inception | tmdb:27205, plex:27205 |
    And media in db:
      | media     |
      | Inception |
    And the following are blacklisted:
      | media      |
      | Fight Club |
      | Inception  |
    When I get the blacklist IDs
    Then the blacklist IDs include:
      | title      |
      | Fight Club |
      | Inception  |

  Scenario: Getting blacklist items enriches entries with metadata and rating
    Given a Plex account with token "test-token"
    And a TMDB account with API key "test-key"
    And plex has:
      | media      |
      | Fight Club |
    And tmdb has:
      | media      | vote_average | vote_count |
      | Fight Club | 8.4          | 23000      |
    And the following are blacklisted:
      | media      |
      | Fight Club |
    When I get the blacklist items
    Then the blacklist items are:
      | title      | poster | rating |
      | Fight Club | yes    | tmdb   |
