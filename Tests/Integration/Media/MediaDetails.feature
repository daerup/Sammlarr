Feature: Media details

  Background:
    Given current time is "01.06.2024"
    And a Plex account with token "plex-token"
    And a TMDB account with API key "tmdb-key"

  Rule: Cached metadata is served until it goes stale

    Scenario: Fresh cached metadata is served without contacting providers
      Given the library has cached:
        | #          | ids                | title        | fetched    |
        | Fight Club | tmdb:550, plex:550 | Cached Title | 31.05.2024 |
      And Plex holds:
        | #          | with ids of | title  |
        | Fight Club | tmdb, plex  | <null> |
      And TMDB holds:
        | #          | with ids of | title      |
        | Fight Club | tmdb        | Live Title |
      When I get details for "Fight Club"
      Then the detail title is "Cached Title"

    Scenario: Stale cached metadata is re-fetched from providers and stored for future reads
      Given the library has cached:
        | #          | ids                | title        | fetched    |
        | Fight Club | tmdb:550, plex:550 | Cached Title | 24.05.2024 |
      And Plex holds:
        | #          | with ids of | title  |
        | Fight Club | tmdb, plex  | <null> |
      And TMDB holds:
        | #          | with ids of | title      |
        | Fight Club | tmdb        | Live Title |
      When I get details for "Fight Club"
      Then the detail title is "Live Title"
      And media is cached as:
        | media      | title      | fetched    |
        | Fight Club | Live Title | 01.06.2024 |

    Scenario: Stale cached metadata is served when the provider cannot be reached
      Given the library has cached:
        | #          | ids                | title        | fetched    |
        | Fight Club | tmdb:550, plex:550 | Cached Title | 24.05.2024 |
      And Plex holds:
        | #          | with ids of | title  |
        | Fight Club | tmdb, plex  | <null> |
      And TMDB holds:
        | #          | with ids of | title      |
        | Fight Club | tmdb        | Live Title |
      And Plex lookup fails for "Fight Club"
      When I get details for "Fight Club"
      Then the detail title is "Cached Title"
      And media is cached as:
        | media      | title        | fetched    |
        | Fight Club | Cached Title | 24.05.2024 |

    Scenario: Uncached media is fetched from providers and stored
      Given the library knows:
        | #          | ids                |
        | Fight Club | tmdb:550, plex:550 |
      And Plex holds:
        | #          | with ids of | title  |
        | Fight Club | tmdb, plex  | <null> |
      And TMDB holds:
        | #          | with ids of | title      |
        | Fight Club | tmdb        | Live Title |
      When I get details for "Fight Club"
      Then the detail title is "Live Title"
      And media is cached as:
        | media      | title      | fetched    |
        | Fight Club | Live Title | 01.06.2024 |

    Scenario Outline: Incomplete cached metadata is re-fetched only once past the hydration cooldown
      Given current time is "01.06.2024 11:30"
      And the library has cached:
        | #          | ids                | title        | genres | fetched              |
        | Fight Club | tmdb:550, plex:550 | Cached Title | <null> | 01.06.2024 <fetched> |
      And Plex holds:
        | #          | with ids of | title  |
        | Fight Club | tmdb, plex  | <null> |
      And TMDB holds:
        | #          | with ids of | title      |
        | Fight Club | tmdb        | Live Title |
      When I get details for "Fight Club"
      Then the detail title is "<expected>"
      And the detail is cached as "Fight Club" fetched at "01.06.2024 <cached>"

      Examples:
        | fetched | expected     | cached |
        | 11:00   | Cached Title | 11:00  |
        | 09:00   | Live Title   | 11:30  |

    Scenario: Cached metadata with an empty poster past the hydration cooldown is re-fetched
      Given current time is "01.06.2024 11:30"
      And the library has cached:
        | #          | ids                | title        | poster | fetched          |
        | Fight Club | tmdb:550, plex:550 | Cached Title | <null> | 01.06.2024 09:00 |
      And Plex holds:
        | #          | with ids of | title  |
        | Fight Club | tmdb, plex  | <null> |
      And TMDB holds:
        | #          | with ids of | title      |
        | Fight Club | tmdb        | Live Title |
      When I get details for "Fight Club"
      Then the detail title is "Live Title"

    Scenario: A batch request resolves each item according to its own cache state
      Given the library has cached:
        | #          | ids                | title       | fetched    |
        | Fight Club | tmdb:550, plex:550 | Fresh Title | 01.06.2024 |
        | The Matrix | tmdb:603, plex:603 | Old Matrix  | 24.05.2024 |
      And Plex holds:
        | #          | with ids of | title  |
        | Fight Club | tmdb, plex  | <null> |
        | The Matrix | tmdb, plex  | <null> |
      And TMDB holds:
        | #          | with ids of | title       |
        | Fight Club | tmdb        | Live Title  |
        | The Matrix | tmdb        | Live Matrix |
      When I get details for:
        | media      |
        | Fight Club |
        | The Matrix |
      Then the detail title for "Fight Club" is "Fresh Title"
      And the detail title for "The Matrix" is "Live Matrix"

  Rule: Details are enriched across providers and rated

    Scenario: Uncached media is enriched across the chain and the merged detail is cached
      Given the library knows:
        | #          | ids                |
        | Fight Club | tmdb:550, plex:550 |
      And Plex holds:
        | #          | with ids of | title      | summary                    |
        | Fight Club | tmdb, plex  | Fight Club | An insomniac office worker |
      And TMDB holds:
        | #          | with ids of | title           | summary            | genres          | vote_average | vote_count |
        | Fight Club | tmdb        | TMDB Fight Club | TMDB plot synopsis | Drama, Thriller | 8.4          | 23000      |
      When I get details for "Fight Club"
      Then the detail is:
        | title      | summary                    | genres          | rating |
        | Fight Club | An insomniac office worker | Drama, Thriller | tmdb   |
      And the detail is cached as "Fight Club" fetched at "01.06.2024"

    Scenario: Stale cached metadata is re-enriched and the refreshed detail replaces it
      Given the library has cached:
        | #          | ids                | title       | summary       | genres | fetched    |
        | Fight Club | tmdb:550, plex:550 | Stale Title | Stale summary | Stale  | 24.05.2024 |
      And Plex holds:
        | #          | with ids of | title      | summary                    |
        | Fight Club | tmdb, plex  | Fight Club | An insomniac office worker |
      And TMDB holds:
        | #          | with ids of | title           | summary            | genres          | vote_average | vote_count |
        | Fight Club | tmdb        | TMDB Fight Club | TMDB plot synopsis | Drama, Thriller | 8.4          | 23000      |
      When I get details for "Fight Club"
      Then the detail is:
        | title      | summary                    | genres          | rating |
        | Fight Club | An insomniac office worker | Drama, Thriller | tmdb   |
      And the detail is cached as "Fight Club" fetched at "01.06.2024"

    Scenario: Without an id for a provider the chain cannot enrich or rate from it
      Given the library knows:
        | #          | ids      |
        | The Matrix | plex:603 |
      And Plex holds:
        | #          | with ids of |
        | The Matrix | plex        |
      When I get details for "The Matrix"
      Then the detail is:
        | title      | rating |
        | The Matrix | none   |

    Scenario: A zero vote average during enrichment applies no rating
      Given the library knows:
        | #          | ids                |
        | Fight Club | tmdb:550, plex:550 |
      And Plex holds:
        | #          | with ids of | title      | summary                    |
        | Fight Club | tmdb, plex  | Fight Club | An insomniac office worker |
      And TMDB holds:
        | #          | with ids of | title           | summary            | genres          | vote_average | vote_count |
        | Fight Club | tmdb        | TMDB Fight Club | TMDB plot synopsis | Drama, Thriller | 0            | 0          |
      When I get details for "Fight Club"
      Then the detail is:
        | title      | summary                    | genres          | rating |
        | Fight Club | An insomniac office worker | Drama, Thriller | none   |

    Scenario: When the enricher returns nothing the Plex base detail is still served and cached
      Given the library knows:
        | #          | ids                |
        | Fight Club | tmdb:550, plex:550 |
      And Plex holds:
        | #          | with ids of | title      | summary                    |
        | Fight Club | tmdb, plex  | Fight Club | An insomniac office worker |
      And TMDB has no detail for "Fight Club"
      When I get details for "Fight Club"
      Then the detail is:
        | title      | summary                    | rating |
        | Fight Club | An insomniac office worker | none   |
      And the detail is cached as "Fight Club" fetched at "01.06.2024"

    Scenario: Plex metadata wins on fields it provides and TMDB does not override it
      Given the library knows:
        | #          | ids                |
        | Fight Club | tmdb:550, plex:550 |
      And Plex holds:
        | #          | with ids of | title      | summary      | genres        |
        | Fight Club | tmdb, plex  | Fight Club | Plex summary | Action, Crime |
      And TMDB holds:
        | #          | with ids of | title           | summary      | genres          | vote_average | vote_count |
        | Fight Club | tmdb        | TMDB Fight Club | TMDB summary | Drama, Thriller | 8.4          | 23000      |
      When I get details for "Fight Club"
      Then the detail is:
        | title      | summary      | genres        | rating |
        | Fight Club | Plex summary | Action, Crime | tmdb   |

    Scenario: TMDB fills the summary and genres that Plex omits
      Given the library knows:
        | #          | ids                |
        | Fight Club | tmdb:550, plex:550 |
      And Plex holds:
        | #          | with ids of | title      |
        | Fight Club | tmdb, plex  | Fight Club |
      And TMDB holds:
        | #          | with ids of | summary      | genres          | vote_average | vote_count |
        | Fight Club | tmdb        | TMDB summary | Drama, Thriller | 8.4          | 23000      |
      When I get details for "Fight Club"
      Then the detail is:
        | title      | summary      | genres          | rating |
        | Fight Club | TMDB summary | Drama, Thriller | tmdb   |

    Scenario: Fresh but incomplete cached metadata is re-hydrated to fill the missing fields
      Given the library has cached:
        | #          | ids                | title        | genres | fetched    |
        | Fight Club | tmdb:550, plex:550 | Cached Title | <null> | 31.05.2024 |
      And Plex holds:
        | #          | with ids of | title      | summary      |
        | Fight Club | tmdb, plex  | Fight Club | Plex summary |
      And TMDB holds:
        | #          | with ids of | genres          | vote_average | vote_count |
        | Fight Club | tmdb        | Drama, Thriller | 8.4          | 23000      |
      When I get details for "Fight Club"
      Then the detail is:
        | title      | summary      | genres          | rating |
        | Fight Club | Plex summary | Drama, Thriller | tmdb   |
      And the detail is cached as "Fight Club" fetched at "01.06.2024"

  Rule: Invalidation forces the next read to re-fetch

    Scenario: Invalidated metadata is re-fetched on the next read
      Given the library has cached:
        | #          | ids                | title        | fetched    |
        | Fight Club | tmdb:550, plex:550 | Cached Title | 31.05.2024 |
      And Plex holds:
        | #          | with ids of | title  |
        | Fight Club | tmdb, plex  | <null> |
      And TMDB holds:
        | #          | with ids of | title      |
        | Fight Club | tmdb        | Live Title |
      When I get details for "Fight Club"
      Then the detail title is "Cached Title"
      When I invalidate "Fight Club"
      And I get details for "Fight Club"
      Then the detail title is "Live Title"

    Scenario: Invalidating metadata preserves the media identity and its external ids
      Given the library has cached:
        | #          | ids                | title       | fetched    |
        | Fight Club | tmdb:550, plex:550 | Cached Text | 31.05.2024 |
      When I invalidate "Fight Club"
      Then the stored ids are "tmdb:550, plex:550"

    Scenario: Invalidating fresh metadata still forces a re-fetch on the next read
      Given the library has cached:
        | #          | ids                | title        | fetched    |
        | Fight Club | tmdb:550, plex:550 | Cached Title | 01.06.2024 |
      And Plex holds:
        | #          | with ids of | title  |
        | Fight Club | tmdb, plex  | <null> |
      And TMDB holds:
        | #          | with ids of | title      |
        | Fight Club | tmdb        | Live Title |
      When I invalidate "Fight Club"
      And I get details for "Fight Club"
      Then the detail title is "Live Title"

    Scenario: Several media can be invalidated in a single call
      Given the library has cached:
        | #          | ids                | title         | fetched    |
        | Fight Club | tmdb:550, plex:550 | Cached Title  | 31.05.2024 |
        | The Matrix | tmdb:603, plex:603 | Cached Matrix | 31.05.2024 |
      And Plex holds:
        | #          | with ids of | title  |
        | Fight Club | tmdb, plex  | <null> |
        | The Matrix | tmdb, plex  | <null> |
      And TMDB holds:
        | #          | with ids of | title       |
        | Fight Club | tmdb        | Live Title  |
        | The Matrix | tmdb        | Live Matrix |
      When I invalidate "Fight Club" and "The Matrix"
      And I get details for "Fight Club"
      Then the detail title is "Live Title"
      When I get details for "The Matrix"
      Then the detail title is "Live Matrix"
