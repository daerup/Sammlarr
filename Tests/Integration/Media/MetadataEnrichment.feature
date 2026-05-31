Feature: Media metadata enrichment

  Background:
    Given current time is "01.06.2024"
    And a Plex account with token "plex-token"
    And a TMDB account with API key "tmdb-key"
    And the following media:
      | media      | ids                |
      | Fight Club | tmdb:550, plex:550 |

  Scenario: Uncached media is enriched across the chain and the merged detail is cached
    Given media in db:
      | media      |
      | Fight Club |
    And plex has:
      | media      | title      | summary                    |
      | Fight Club | Fight Club | An insomniac office worker |
    And tmdb has:
      | media      | title           | summary            | genres          | vote_average | vote_count |
      | Fight Club | TMDB Fight Club | TMDB plot synopsis | Drama, Thriller | 8.4          | 23000      |
    When I get details for "Fight Club"
    Then the detail is:
      | title      | summary                    | genres          | rating |
      | Fight Club | An insomniac office worker | Drama, Thriller | tmdb   |
    And the detail is cached as "Fight Club" fetched at "01.06.2024"

  Scenario: Stale cached metadata is re-enriched and the refreshed detail replaces it
    Given media in cache:
      | media      | title       | summary       | genres | fetched    |
      | Fight Club | Stale Title | Stale summary | Stale  | 24.05.2024 |
    And plex has:
      | media      | title      | summary                    |
      | Fight Club | Fight Club | An insomniac office worker |
    And tmdb has:
      | media      | title           | summary            | genres          | vote_average | vote_count |
      | Fight Club | TMDB Fight Club | TMDB plot synopsis | Drama, Thriller | 8.4          | 23000      |
    When I get details for "Fight Club"
    Then the detail is:
      | title      | summary                    | genres          | rating |
      | Fight Club | An insomniac office worker | Drama, Thriller | tmdb   |
    And the detail is cached as "Fight Club" fetched at "01.06.2024"

  Scenario: Without an id for a provider the chain cannot enrich or rate from it
    Given additional media:
      | media      | ids      |
      | The Matrix | plex:603 |
    And media in db:
      | media      |
      | The Matrix |
    And plex has:
      | media      | ids      |
      | The Matrix | plex:603 |
    When I get details for "The Matrix"
    Then the detail is:
      | title      | rating |
      | The Matrix | none   |

  Scenario: When the enricher returns nothing the Plex base detail is still served and cached
    Given media in db:
      | media      |
      | Fight Club |
    And plex has:
      | media      | title      | summary                    |
      | Fight Club | Fight Club | An insomniac office worker |
    And tmdb has no detail for "Fight Club"
    When I get details for "Fight Club"
    Then the detail is:
      | title      | summary                    | rating |
      | Fight Club | An insomniac office worker | none   |
    And the detail is cached as "Fight Club" fetched at "01.06.2024"
