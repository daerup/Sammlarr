# Security

## Reporting a vulnerability

Please do not open a public issue for a security problem. Report it privately through GitHub's
[private vulnerability reporting](https://github.com/daerup/Sammlarr/security/advisories/new) on this repository.

Say what you found, which version it affects (Settings › About shows it) and how to reproduce it. You will get an
answer within a week, and a fix ships in the next release; the release notes credit you unless you prefer not.

## Supported versions

Only the latest release gets fixes. With `:latest` in your compose file, update with `docker compose pull` and
`docker compose up -d`; with a pinned `:X.Y.Z`, change the tag to the new release first.
