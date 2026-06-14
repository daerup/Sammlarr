Feature: Provider credentials

  Rule: API keys are stored per provider

    Scenario: Looking up a key that was never stored returns nothing
      When I look up the TMDB API key
      Then no TMDB API key is stored

    Scenario: A stored key can be retrieved
      When I store the TMDB API key "tmdb-key-12345"
      And I look up the TMDB API key
      Then the stored TMDB API key is "tmdb-key-12345"

    Scenario: Storing a key replaces the previous value
      Given the TMDB API key is "old-key"
      When I store the TMDB API key "new-key"
      And I look up the TMDB API key
      Then the stored TMDB API key is "new-key"

    Scenario: Clearing a key removes it
      Given the TMDB API key is "secret-key"
      When I clear the TMDB API key
      And I look up the TMDB API key
      Then no TMDB API key is stored

    Scenario: Clearing a key that was never stored is safe
      When I clear the TMDB API key
      And I look up the TMDB API key
      Then no TMDB API key is stored

  Rule: A key is verified against the provider before it is trusted

    Scenario: TMDB rejects a key it does not recognise
      Given a TMDB account with API key "valid-tmdb-key"
      When I verify the TMDB API key "wrong-key"
      Then the TMDB key is rejected

    Scenario: TMDB accepts the configured key
      Given a TMDB account with API key "valid-tmdb-key"
      When I verify the TMDB API key "valid-tmdb-key"
      Then the TMDB key is accepted

    Scenario: MdbList rejects a key it does not recognise
      Given an MdbList account with API key "valid-mdblist-key"
      When I verify the MdbList API key "wrong-key"
      Then the MdbList key is rejected

    Scenario: MdbList accepts the configured key
      Given an MdbList account with API key "valid-mdblist-key"
      When I verify the MdbList API key "valid-mdblist-key"
      Then the MdbList key is accepted

    Scenario: Verifying a key fails gracefully when the provider is unreachable
      Given the TMDB API is unreachable
      When I verify the TMDB API key "any-key"
      Then the TMDB key is rejected

  Rule: Providers stay inert until their credential is configured

    Scenario: TMDB ratings are unavailable without a key
      When I request TMDB ratings for movie 550
      Then no TMDB ratings are returned

    Scenario: TMDB ratings load once the key is configured
      Given a TMDB account with API key "valid-tmdb-key"
      And the TMDB API returns rating data for movie 550
      When I request TMDB ratings for movie 550
      Then TMDB ratings are returned

    Scenario: Plex watchlist pushes fail without a token
      When I add Plex item "plex-100" to the watchlist
      Then the Plex push fails

    Scenario: Plex watchlist pushes succeed once the token is configured
      Given a Plex account with token "plex-access-token"
      When I add Plex item "plex-100" to the watchlist
      Then the Plex push succeeds

  Rule: Clearing a provider key purges data sourced from it

    Scenario: Clearing the TMDB key removes its cached ratings
      Given a Plex account with token "plex-token"
      And a TMDB account with API key "valid-tmdb-key"
      And the library knows:
        | #          | ids                |
        | Fight Club | tmdb:550, plex:550 |
      And Plex holds:
        | #          | with ids of |
        | Fight Club | tmdb, plex  |
      And TMDB holds:
        | #          | with ids of | vote_average | vote_count |
        | Fight Club | tmdb        | 8.4          | 23000      |
      When I get details for "Fight Club"
      And I clear the TMDB API key
      Then no TMDB ratings remain cached
