# Rate-limit requests in the app

| Status   | Date       |
|----------|------------|
| accepted | 2026-10-16 |

## Context

ADR 0020 left limits per address to the reverse proxy, with Identity's per-account lockout as the app's only guard. Not every install runs such a limit, and without one nothing stops a flood of requests or of guesses at
sign-in, invite, reset and setup forms across many accounts.

## Decision

Sammlarr uses ASP.NET Core's rate limiter. A global token bucket covers every request: one per signed-in user, else
one per address, sized so a page load never meets it. The anonymous posts (sign-in, join, reset, setup) also get a
fixed window per address. Over a limit Sammlarr answers 429 with `Retry-After` and a one-line text. Static assets and
health checks are left out.

Behind a reverse proxy every request comes from the proxy's address, so a per-address limit would put all visitors in
one bucket and let one attacker block everyone's sign-in. The example `compose.yaml` therefore turns on the
framework's forwarded headers (`ASPNETCORE_FORWARDEDHEADERS_ENABLED`), which take the address from `X-Forwarded-For`.
On a directly reached install a client can fake that header and dodge the per-address limits; the per-account lockout
still holds.

Rejected: limits only at the proxy (ADR 0020's position: many installs have none); trusting forwarded headers only
from configured proxy addresses (one more setting every install would have to get right).

## Consequences

* Every install has a request limit and a brake on form guessing, with or without a proxy.
* Supersedes ADR 0020's "Limits per address are the reverse proxy's"; fail2ban or a proxy limit can still sit in
  front.
* Someone who trips a limit on a form sees a plain text page, not the form with a message.
* People who share one address (a household behind NAT, a proxy without the variable) share its limits, so one of
  them can use up the sign-in limit for all.
* Sammlarr reads forwarded headers when the variable is on, so behind a TLS proxy it sees https: the cookie is
  `Secure`, HSTS is sent, and the Plex return address uses https even without `Sammlarr__PublicUrl`.
