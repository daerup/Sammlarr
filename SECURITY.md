# Security

## Reporting a vulnerability

Please do not open a public issue for a security problem. Report it privately through GitHub's
[private vulnerability reporting](https://github.com/daerup/Sammlarr/security/advisories/new) on this repository.

Say what you found, which version it affects (Settings › About shows it) and how to reproduce it.

## Verifying what you run

Each release attests its image's build provenance:
`gh attestation verify oci://ghcr.io/daerup/sammlarr:X.Y.Z --repo daerup/Sammlarr`.

Each release also rebuilds its tag on a fresh runner without cache, and its notes name the digest the rebuild matched and
the BuildKit version that built both. To repeat it, build the tag the way the `rebuild` job in
`.github/workflows/release.yml` does: `src/Web/Dockerfile` for `linux/amd64,linux/arm64` on the BuildKit image pinned
in `.github/actions/build-image`, with the build arguments `VERSION` and `SOURCE_DATE_EPOCH` (the tag's commit time),
the pushed image's labels, OCI media types and `rewrite-timestamp=true`. The digest you get, the index over both
platforms, is the one in the notes.

## Supported versions

Only the latest release is supported. With `:latest` in your compose file, update with `docker compose pull` and
`docker compose up -d`; with a pinned `:X.Y.Z`, change the tag to the new release first.
