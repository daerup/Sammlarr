# Changelog

## 0.1.0 (2026-10-16)


### Bug Fixes

* **auth:** keep line breaks in usernames out of sign-in warnings ([4ec81eb](https://github.com/daerup/Sammlarr/commit/4ec81eb36bc259bfbac8f0a86a045f9f854a0841))
* **wantlist:** stop the sync dry run once its page closes ([6545c96](https://github.com/daerup/Sammlarr/commit/6545c96c265e597f486b9de0d291dbd2d56eebc1))

## Before 0.1.0

Sammlarr was built from March 2026 on without releases. This changelog starts with 0.1.0; what came
before it is in the git history. By 0.1.0 Sammlarr:

- follows movie and show lists, charts and searches from MDBList, TMDB, Trakt, AniList and MyAnimeList (and IMDb and
  Letterboxd lists through MDBList) as feeds, each with its own filters, sort, limit and schedule,
- matches each title to its IMDb, TMDB and TVDB ids and sends what is left to your Plex watchlist or to Radarr and
  Sonarr, with an anime folder of their own,
- shows a feed run live as it happens, its history, and what every service was asked on Settings › Usage,
- serves several people, each with their own feeds and wantlist, signed in through Plex or with Sammlarr accounts.
