Feature: Plex authentication

  Background:
    Given a Plex account with token "plex-token"

  Scenario Outline: The display name prefers friendly name, then title, then username
    Given Plex reports a signed-in user with friendly name "<friendly>", title "<title>", username "<username>"
    When I look up the signed-in Plex user
    Then the signed-in Plex user is "<display>"

    Examples:
      | friendly  | title        | username  | display      |
      | Cinephile | Cinephile    | cinephile | Cinephile    |
      |           | MovieWatcher | mw        | MovieWatcher |
      |           |              | john.doe  | john.doe     |

  Scenario: An unrecognised token resolves no signed-in user
    Given Plex rejects the user request
    When I look up the signed-in Plex user
    Then there is no signed-in Plex user

  Scenario: A server error resolves no signed-in user
    Given Plex returns a server error for the user request
    When I look up the signed-in Plex user
    Then there is no signed-in Plex user

  Scenario: An active subscription exposes its description
    Given Plex reports the signed-in user "Subscriber" with active subscription "Plex Pass"
    When I look up the signed-in Plex user
    Then the signed-in Plex user subscription description is "Plex Pass"

  Scenario: An inactive subscription has no description
    Given Plex reports the signed-in user "BasicUser" with inactive subscription
    When I look up the signed-in Plex user
    Then the signed-in Plex user subscription description is empty
