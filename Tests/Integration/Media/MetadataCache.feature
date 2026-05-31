Feature: Media metadata cache freshness

Background:
    Given current time is "01.06.2024"
    And a Plex account with token "test-token"
    And a TMDB account with API key "test-key"
    And the following media:
        | media      | ids                |
        | Fight Club | tmdb:550, plex:550 |
    And plex has:
        | media      | title  |
        | Fight Club | <null> |
    And tmdb has:
        | media      | title      | vote_average | vote_count |
        | Fight Club | Live Title |          8.4 |      23000 |

Scenario: Fresh cached metadata is served without contacting providers
    Given media in cache:
        | media      | title        | fetched    |
        | Fight Club | Cached Title | 31.05.2024 |
    When I get details for "Fight Club"
    Then the detail title is "Cached Title"

Scenario: Stale cached metadata is re-fetched from providers and stored for future reads
    Given media in cache:
        | media      | title        | fetched    |
        | Fight Club | Cached Title | 24.05.2024 |
    When I get details for "Fight Club"
    Then the detail title is "Live Title"
    And media is cached as:
        | media      | title      | fetched    |
        | Fight Club | Live Title | 01.06.2024 |

Scenario: Stale cached metadata is served when the provider cannot be reached
    Given media in cache:
        | media      | title        | fetched    |
        | Fight Club | Cached Title | 24.05.2024 |
    And plex lookup fails for "Fight Club"
    When I get details for "Fight Club"
    Then the detail title is "Cached Title"
    And media is cached as:
        | media      | title        | fetched    |
        | Fight Club | Cached Title | 24.05.2024 |

Scenario: Uncached media is fetched from providers and stored
    Given media in db:
        | media      |
        | Fight Club |
    When I get details for "Fight Club"
    Then the detail title is "Live Title"
    And media is cached as:
        | media      | title      | fetched    |
        | Fight Club | Live Title | 01.06.2024 |

Scenario: Incomplete cached metadata within the hydration cooldown is still served
    Given current time is "01.06.2024 11:30"
    And media in cache:
        | media      | title        | genres | fetched          |
        | Fight Club | Cached Title | <null> | 01.06.2024 11:00 |
    When I get details for "Fight Club"
    Then the detail is:
        | title        |
        | Cached Title |
    And the detail is cached as "Fight Club" fetched at "01.06.2024 11:00"

Scenario: Incomplete cached metadata past the hydration cooldown is re-fetched
    Given current time is "01.06.2024 11:30"
    And media in cache:
        | media      | title        | genres | fetched          |
        | Fight Club | Cached Title | <null> | 01.06.2024 09:00 |
    When I get details for "Fight Club"
    Then the detail is:
        | title      |
        | Live Title |
    And the detail is cached as "Fight Club" fetched at "01.06.2024 11:30"
