# Security

## Reporting a vulnerability

Please do not open a public issue for a security problem. Report it privately through GitHub's
[private vulnerability reporting](https://github.com/daerup/Sammlarr/security/advisories/new) on this repository.

Say what you found, which version it affects (Settings › About shows it) and how to reproduce it.

## Verifying what you run

Every image from 0.1.0 on is signed with cosign and carries GitHub build provenance and two SBOM attestations (the
app's packages and the image's), all made by the release's own run of `.github/workflows/release.yml`. Check a version
before you run it, with the [GitHub CLI](https://cli.github.com) and
[cosign](https://docs.sigstore.dev/cosign/system_config/installation/):

```sh
gh attestation verify oci://ghcr.io/daerup/sammlarr:0.1.0 --repo daerup/Sammlarr
gh attestation verify oci://ghcr.io/daerup/sammlarr:0.1.0 --repo daerup/Sammlarr --predicate-type https://cyclonedx.org/bom
cosign verify ghcr.io/daerup/sammlarr:0.1.0 \
  --certificate-identity https://github.com/daerup/Sammlarr/.github/workflows/release.yml@refs/tags/v0.1.0 \
  --certificate-oidc-issuer https://token.actions.githubusercontent.com
```

Replace `0.1.0` with the version you run.

Each release also rebuilds its tag on a fresh runner without cache, and its notes name the digest the rebuild matched
and the BuildKit version that built both. To repeat it, build the tag the way the `rebuild` job in
`.github/workflows/release.yml` does: `src/Web/Dockerfile` for `linux/amd64` on the BuildKit image pinned in
`.github/actions/build-image`, with the build arguments `VERSION` and `SOURCE_DATE_EPOCH` (the tag's commit time), the
pushed image's labels, OCI media types and `rewrite-timestamp=true`. The digest you get is the one in the notes.

## Supported versions

Only the latest release is supported. With `:latest` in your compose file, update with `docker compose pull` and
`docker compose up -d`; with a pinned `:X.Y.Z`, change the tag to the new release first.
