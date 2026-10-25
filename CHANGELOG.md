# Changelog

## [0.1.3](https://github.com/daerup/Sammlarr/compare/v0.1.2...v0.1.3) (2026-10-25)


### Features

* distinguish invalid Arr URL errors from API key refusals ([4fee50f](https://github.com/daerup/Sammlarr/commit/4fee50fb0e4d57bbcf766a43a404c7fbc76bea25))
* **setup:** let setup swap the admin to another Plex account ([4fee50f](https://github.com/daerup/Sammlarr/commit/4fee50fb0e4d57bbcf766a43a404c7fbc76bea25))


### Bug Fixes

* **auth:** reconnect only the Plex account that signed in ([4fee50f](https://github.com/daerup/Sammlarr/commit/4fee50fb0e4d57bbcf766a43a404c7fbc76bea25))
* hide switch confirmation when not needed ([4fee50f](https://github.com/daerup/Sammlarr/commit/4fee50fb0e4d57bbcf766a43a404c7fbc76bea25))
* **setup:** confirm the Plex account before it becomes the admin ([4fee50f](https://github.com/daerup/Sammlarr/commit/4fee50fb0e4d57bbcf766a43a404c7fbc76bea25))
* **setup:** refuse a managed Plex Home profile as the admin ([4fee50f](https://github.com/daerup/Sammlarr/commit/4fee50fb0e4d57bbcf766a43a404c7fbc76bea25))
* switch radarr and sonarr images from lscr.io to ghcr.io ([#20](https://github.com/daerup/Sammlarr/issues/20)) ([7782c56](https://github.com/daerup/Sammlarr/commit/7782c566f25aef27f17f65f7beb727cdc2a5ebbe))

## [0.1.2](https://github.com/daerup/Sammlarr/compare/v0.1.1...v0.1.2) (2026-10-23)


### Bug Fixes

* **ui:** fill every page down to the footer ([#18](https://github.com/daerup/Sammlarr/issues/18)) ([7f78685](https://github.com/daerup/Sammlarr/commit/7f786855e50d6356078c0b8c4bf846e34663414a))

## [0.1.1](https://github.com/daerup/Sammlarr/compare/v0.1.0...v0.1.1) (2026-10-23)


### Features

* **feeds:** explain every preview verdict in its own popover ([67a2c7c](https://github.com/daerup/Sammlarr/commit/67a2c7c41377ec2b3a3065a92a5b4f376b183516))
* **feeds:** label filter groups "all of" and "any of" above each card ([67a2c7c](https://github.com/daerup/Sammlarr/commit/67a2c7c41377ec2b3a3065a92a5b4f376b183516))
* **feeds:** nest filter groups five levels deep with colours from appsettings ([67a2c7c](https://github.com/daerup/Sammlarr/commit/67a2c7c41377ec2b3a3065a92a5b4f376b183516))
* **home:** split hand-added titles by origin in brand colours ([67a2c7c](https://github.com/daerup/Sammlarr/commit/67a2c7c41377ec2b3a3065a92a5b4f376b183516))
* **ui:** one wantlist toggle with the provenance dot in the preview and on Browse tiles ([67a2c7c](https://github.com/daerup/Sammlarr/commit/67a2c7c41377ec2b3a3065a92a5b4f376b183516))


### Bug Fixes

* **feeds:** drop the empty header above a group with one condition ([67a2c7c](https://github.com/daerup/Sammlarr/commit/67a2c7c41377ec2b3a3065a92a5b4f376b183516))
* **ui:** draw the provenance dot on the bookmark icon so it lines up everywhere ([67a2c7c](https://github.com/daerup/Sammlarr/commit/67a2c7c41377ec2b3a3065a92a5b4f376b183516))

## 0.1.0 (2026-10-20)


### Features

* arm64 images plus small release and app fixes ([1484be4](https://github.com/daerup/Sammlarr/commit/1484be4b24a0c4891cb0234f7715f81bd50930af))
* **feeds:** draw a fresh random pick after every successful run ([09b299f](https://github.com/daerup/Sammlarr/commit/09b299fdf57fa47ebf4ab9bf87dcd4dc29d7a4c4))
* **feeds:** filter and sort by runtime per episode or in total ([9a940e2](https://github.com/daerup/Sammlarr/commit/9a940e29cd99ccdacb2e8158869ef880925667fa))
* **feeds:** own filter builder with an operator menu per field ([22b36e9](https://github.com/daerup/Sammlarr/commit/22b36e9941e68b79751dfc564560a4ca853f7a08))
* **feeds:** pick genres from the library, match any, all or none ([3f36e14](https://github.com/daerup/Sammlarr/commit/3f36e14593c98e4d497f62e1dce3daa753bc7745))
* **feeds:** preview follows edits live, save stays in the editor ([05cf019](https://github.com/daerup/Sammlarr/commit/05cf019de5261e394234b6ecf2ce1d97579a1be1))
* **feeds:** relative release dates measured from each run's day ([84746de](https://github.com/daerup/Sammlarr/commit/84746dee13764fb2152acc5838897e2f39a82a47))
* **feeds:** seasons and episodes spell out counts and only match shows ([3595674](https://github.com/daerup/Sammlarr/commit/359567426c465746a8cb0cc32137c2cc6ece332f))
* **feeds:** sort only by fields with a meaningful order ([ffd56e4](https://github.com/daerup/Sammlarr/commit/ffd56e4e01ca2a27b97a33d722383c7a42ea4c96))
* **housekeeping:** show what fills the database file in a bar ([3a5c452](https://github.com/daerup/Sammlarr/commit/3a5c452e2fa883218d1af0d69ed1d70c449c2f8f))
* **housekeeping:** turn the clear-by-hand group into a Caches table ([3a5c452](https://github.com/daerup/Sammlarr/commit/3a5c452e2fa883218d1af0d69ed1d70c449c2f8f))
* store later seasons, parts and specials with their show ([61b1084](https://github.com/daerup/Sammlarr/commit/61b10847ce5983dffac429f761218515a66a3093))
* test notification targets on create and see updates sooner ([42bbeaa](https://github.com/daerup/Sammlarr/commit/42bbeaa7ea042b2f0cda94c30b50b8706d525d98))
* **ui:** draw every segmented bar the same way, with one hover list and a legend that hides parts ([3a5c452](https://github.com/daerup/Sammlarr/commit/3a5c452e2fa883218d1af0d69ed1d70c449c2f8f))
* **ui:** drop the footer credit and right-align the version ([0fd99da](https://github.com/daerup/Sammlarr/commit/0fd99da80df828f32c71b18643bd8db426125619))


### Bug Fixes

* **anilist:** show a refused lane as AniList being unavailable ([4879cf4](https://github.com/daerup/Sammlarr/commit/4879cf4d9a481072f0db5d8825e2a90bfa45561b))
* **auth:** keep line breaks in usernames out of sign-in warnings ([631bb29](https://github.com/daerup/Sammlarr/commit/631bb298bce66ac6f104c010b1d1e27d6849c2e5))
* **auth:** sign out with a POST form like the Identity template ([7b751f6](https://github.com/daerup/Sammlarr/commit/7b751f6dc67db3db9245070c904de1cda7d49a90))
* **auth:** stop re-issuing the auth cookie on every request ([62af4c5](https://github.com/daerup/Sammlarr/commit/62af4c517b2834c9190b4f0f8b34941b26839c32))
* **feeds:** focus the number input when its box is clicked beside the digits ([5ace8a4](https://github.com/daerup/Sammlarr/commit/5ace8a4e13707d684dcb4d743de11c9be07904f6))
* **feeds:** keep the looked-up list when the identifier reports late ([667dce7](https://github.com/daerup/Sammlarr/commit/667dce717ae7dd3aebc71120958a14a856112a18))
* **feeds:** leaving tip names the remote list and hides an empty count ([7649766](https://github.com/daerup/Sammlarr/commit/7649766a0adb5a52e85798f3712aa1bd55530bca))
* **feeds:** let the schedule time be typed and clamp it to a valid time ([8732baa](https://github.com/daerup/Sammlarr/commit/8732baae0a629bac2d79227addde7a01455eac92))
* **feeds:** reload the preview on the next edit after a failed plan ([3523ab9](https://github.com/daerup/Sammlarr/commit/3523ab9225c69c09c76933a7a4e2d583019a33bd))
* **feeds:** show the new field's operator label and value after a field switch ([e9d00aa](https://github.com/daerup/Sammlarr/commit/e9d00aae1509aefadb65b8f41901e8b2da0f7fd2))
* **filters:** pass filter values to Dynamic LINQ as parameters ([3a5c452](https://github.com/daerup/Sammlarr/commit/3a5c452e2fa883218d1af0d69ed1d70c449c2f8f))
* **plex:** send the sign-in product name with its key encoded once ([3a5c452](https://github.com/daerup/Sammlarr/commit/3a5c452e2fa883218d1af0d69ed1d70c449c2f8f))
* **ui:** close popovers over plain HTTP, plus setup and schedule polish ([c7c8ece](https://github.com/daerup/Sammlarr/commit/c7c8ece4a758a99b40a9af322163fb948ecc44d3))
* **ui:** keep circuits alive and preview rows rendering under load ([75bc822](https://github.com/daerup/Sammlarr/commit/75bc8220c9f772a8edb03cb24e912d2206f8cc1d))
* **ui:** keep dates in English in a German browser ([044a49b](https://github.com/daerup/Sammlarr/commit/044a49b9e2f6b2a9c596ba860b4f13b8c8d64b39))
* **wantlist:** stop logging a cancelled sync preview as a Plex failure ([4d9e95c](https://github.com/daerup/Sammlarr/commit/4d9e95cb290273f68f2e701da2bf7ae9722332fb))
* **wantlist:** stop the sync dry run once its page closes ([1b378ba](https://github.com/daerup/Sammlarr/commit/1b378bae011938db52bce8d53f03d1f178b3f962))
