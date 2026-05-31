Feature: Media identity resolution

  Background:
    Given a Plex account with token "plex-token"

  Scenario: A Plex id resolves to the title's Tmdb id through a Plex lookup
    Given plex has:
      | media      | ids                |
      | Fight Club | tmdb:550, plex:550 |
    When I resolve "plex:550" to a tmdb id
    Then the matching id is "tmdb:550"

  Scenario: A Tmdb id and a title resolve to the Plex id through a Plex search
    Given plex search for "Fight Club" returns:
      | media      | id       |
      | Fight Club | plex:550 |
    And plex has:
      | media      | ids                |
      | Fight Club | tmdb:550, plex:550 |
    When I resolve "tmdb:550" titled "Fight Club" to a plex id
    Then the matching id is "plex:550"

  Scenario: A Plex id and a Tmdb id of one title resolve to a single media
    Given plex has:
      | media      | ids                |
      | Fight Club | tmdb:550, plex:550 |
    And plex search for "Fight Club" returns:
      | media      | id       |
      | Fight Club | plex:550 |
    When I resolve:
      | candidates | title      | to   |
      | plex:550   |            | tmdb |
      | tmdb:550   | Fight Club | plex |
    Then they resolve to one media

  Scenario: Resolving a Plex id stores every id Plex reported
    Given plex has:
      | media      | ids                |
      | Fight Club | tmdb:550, plex:550 |
    When I resolve "plex:550" to a tmdb id
    Then the id "tmdb:550" is stored
    And the id "plex:550" is stored

  Scenario: An id already in the library is translated from the cache without contacting Plex
    Given the following media:
      | media      | ids                |
      | Fight Club | tmdb:550, plex:550 |
    And media in db:
      | media      |
      | Fight Club |
    When I resolve "plex:550" to a tmdb id
    Then the matching id is "tmdb:550"

  Scenario: An id the providers cannot place resolves to nothing
    Given plex search for "Unknown Movie" returns nothing
    When I resolve "tmdb:999" titled "Unknown Movie" to a plex id
    Then no id is resolved
    And the media count is 0

  Scenario: Resolving a new id folds into the existing media that already holds a shared id
    Given the following media:
      | media      | ids      |
      | Fight Club | tmdb:550 |
    And media in db:
      | media      |
      | Fight Club |
    And plex has:
      | media      | ids                |
      | Fight Club | tmdb:550, plex:550 |
    When I resolve "plex:550" to a tmdb id
    Then the matching id is "tmdb:550"
    And the media count is 1
    And the id "plex:550" is stored

  Scenario: Two unknown Plex ids that resolve to the same Tmdb id collapse into one media
    Given plex has:
      | media           | ids                |
      | Fight Club      | tmdb:550, plex:550 |
      | Fight Club (4K) | tmdb:550, plex:999 |
    When I resolve:
      | candidates | to   |
      | plex:550   | tmdb |
      | plex:999   | tmdb |
    Then they resolve to one media
    And the media count is 1

  Scenario: A provider result bridging two separate media rows is rejected as a conflict
    Given the following media:
      | media             | ids      |
      | Fight Club (TMDB) | tmdb:550 |
      | Fight Club (Plex) | plex:550 |
    And media in db:
      | media             |
      | Fight Club (TMDB) |
      | Fight Club (Plex) |
    And plex search for "Fight Club" returns:
      | media             | id       |
      | Fight Club (Plex) | plex:550 |
    And plex has:
      | media             | ids                                |
      | Fight Club (Plex) | tmdb:550, plex:550, imdb:tt0137523 |
    When I try to resolve "imdb:tt0137523" titled "Fight Club" to a plex id
    Then an exception of type "MediaIdentityConflictException" occurred
    And the media count is 2

  Scenario: Candidates already split across two media rows are rejected as a conflict
    Given the following media:
      | media             | ids      |
      | Fight Club (TMDB) | tmdb:550 |
      | Fight Club (Plex) | plex:550 |
    And media in db:
      | media             |
      | Fight Club (TMDB) |
      | Fight Club (Plex) |
    When I try to resolve:
      | candidates         | to   |
      | tmdb:550, plex:550 | plex |
    Then an exception of type "MediaIdentityConflictException" occurred
    And the media count is 2
