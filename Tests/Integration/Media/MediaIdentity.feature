Feature: Media identity resolution

  Background:
    Given a Plex account with token "plex-token"

  Rule: Translating an id via a provider lookup

    Scenario: A Plex id resolves to the title's Tmdb id through a Plex lookup
      Given media:
        | #          | ids                |
        | Fight Club | tmdb:550, plex:550 |
      And Plex holds:
        | #          | with ids of |
        | Fight Club | tmdb, plex  |
      When I resolve "plex:550" to a tmdb id
      Then the resolution returns:
        | resolved id |
        | tmdb:550    |

    Scenario: A Plex id resolves to the title's Imdb id through a Plex lookup
      Given media:
        | #          | ids                                |
        | Fight Club | tmdb:550, imdb:tt0137523, plex:550 |
      And Plex holds:
        | #          | with ids of      |
        | Fight Club | tmdb, imdb, plex |
      When I resolve "plex:550" to a imdb id
      Then the resolution returns:
        | resolved id    |
        | imdb:tt0137523 |

    Scenario: A Plex id resolves to the title's Tvdb id through a Plex lookup
      Given media:
        | #               | ids                   |
        | Game of Thrones | tvdb:81189, plex:1396 |
      And Plex holds:
        | #               | with ids of |
        | Game of Thrones | tvdb, plex  |
      When I resolve "plex:1396" to a tvdb id
      Then the resolution returns:
        | resolved id |
        | tvdb:81189  |

    Scenario: Resolving to a provider the Plex lookup cannot supply resolves to nothing
      Given media:
        | #          | ids      |
        | Fight Club | plex:550 |
      And Plex holds:
        | #          | with ids of |
        | Fight Club | plex        |
      When I resolve "plex:550" to a tvdb id
      Then the resolution is empty

    Scenario: A provider error during resolution leaves the id unresolved
      Given media:
        | #       | ids      |
        | Mystery | plex:550 |
      And Plex lookup fails for "Mystery"
      When I resolve "plex:550" to a tmdb id
      Then the resolution is empty

    Scenario: Resolving a Plex id stores every id Plex reported
      Given media:
        | #          | ids                |
        | Fight Club | tmdb:550, plex:550 |
      And Plex holds:
        | #          | with ids of |
        | Fight Club | tmdb, plex  |
      When I resolve "plex:550" to a tmdb id
      Then the resolution returns:
        | stored ids         |
        | tmdb:550, plex:550 |

  Rule: Resolving by title search

    Scenario: A Tmdb id and a title resolve to the Plex id through a Plex search
      Given Plex search for "Fight Club" returns:
        | title      | id       |
        | Fight Club | plex:550 |
      And media:
        | #          | ids                |
        | Fight Club | tmdb:550, plex:550 |
      And Plex holds:
        | #          | with ids of |
        | Fight Club | tmdb, plex  |
      When I resolve "tmdb:550" titled "Fight Club" to a plex id
      Then the resolution returns:
        | resolved id |
        | plex:550    |

    Scenario: An id the providers cannot place resolves to nothing
      Given Plex search for "Unknown Movie" returns nothing
      When I resolve "tmdb:999" titled "Unknown Movie" to a plex id
      Then the resolution is empty

  Rule: Reusing the local cache

    Scenario: An id already in the library is translated from the cache without contacting Plex
      Given the library knows:
        | #          | ids                |
        | Fight Club | tmdb:550, plex:550 |
      When I resolve "plex:550" to a tmdb id
      Then the resolution returns:
        | resolved id |
        | tmdb:550    |

  Rule: Merging ids into one media

    Scenario: A Plex id and a Tmdb id of one title resolve to a single media
      Given media:
        | #          | ids                |
        | Fight Club | tmdb:550, plex:550 |
      And Plex holds:
        | #          | with ids of |
        | Fight Club | tmdb, plex  |
      And Plex search for "Fight Club" returns:
        | title      | id       |
        | Fight Club | plex:550 |
      When I resolve:
        | candidates | title      | to   |
        | plex:550   |            | tmdb |
        | tmdb:550   | Fight Club | plex |
      Then the resolution returns:
        | stored ids         |
        | tmdb:550, plex:550 |

    Scenario: Resolving a new id folds into the existing media that already holds a shared id
      Given the library knows:
        | #          | ids      |
        | Fight Club | tmdb:550 |
      And media:
        | #               | ids                |
        | Fight Club Plex | tmdb:550, plex:550 |
      And Plex holds:
        | #               | with ids of |
        | Fight Club Plex | tmdb, plex  |
      When I resolve "plex:550" to a tmdb id
      Then the resolution returns:
        | resolved id | stored ids |
        | tmdb:550    | plex:550   |

    Scenario: Two unknown Plex ids that resolve to the same Tmdb id collapse into one media
      Given media:
        | #              | ids                |
        | Fight Club     | tmdb:550, plex:550 |
        | Fight Club Dup | tmdb:550, plex:999 |
      And Plex holds:
        | #              | with ids of |
        | Fight Club     | tmdb, plex  |
        | Fight Club Dup | tmdb, plex  |
      When I resolve:
        | candidates | to   |
        | plex:550   | tmdb |
        | plex:999   | tmdb |
      Then the resolution returns:
        | stored ids                    |
        | tmdb:550, plex:550, plex:999  |

  Rule: Rejecting cross-media conflicts

    Scenario: A provider result bridging two separate media rows is rejected as a conflict
      Given the library knows:
        | #                 | ids      |
        | Fight Club (TMDB) | tmdb:550 |
        | Fight Club (Plex) | plex:550 |
      And Plex search for "Fight Club" returns:
        | title             | id       |
        | Fight Club (Plex) | plex:550 |
      And media:
        | #                 | ids                                |
        | Fight Club Bridge | tmdb:550, plex:550, imdb:tt0137523 |
      And Plex holds:
        | #                 | with ids of      |
        | Fight Club Bridge | tmdb, plex, imdb |
      When I try to resolve "imdb:tt0137523" titled "Fight Club" to a plex id
      Then an exception of type "MediaIdentityConflictException" occurred

    Scenario: Candidates already split across two media rows are rejected as a conflict
      Given the library knows:
        | #                 | ids      |
        | Fight Club (TMDB) | tmdb:550 |
        | Fight Club (Plex) | plex:550 |
      When I try to resolve:
        | candidates         | to   |
        | tmdb:550, plex:550 | plex |
      Then an exception of type "MediaIdentityConflictException" occurred
