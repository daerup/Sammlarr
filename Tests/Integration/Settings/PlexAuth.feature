Feature: Plex authentication

  Background:
    Given a Plex account with token "plex-token"

  Scenario: A recognised token resolves the signed-in Plex user
    Given plex reports the signed-in user "Cinephile"
    When I look up the signed-in Plex user
    Then the signed-in Plex user is "Cinephile"

  Scenario: An unrecognised token resolves no signed-in user
    Given plex rejects the user request
    When I look up the signed-in Plex user
    Then there is no signed-in Plex user
