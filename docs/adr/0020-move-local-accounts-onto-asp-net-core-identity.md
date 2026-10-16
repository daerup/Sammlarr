# Move local accounts onto ASP.NET Core Identity

| Status   | Date       |
|----------|------------|
| accepted | 2026-10-16 |

## Context

Sammlarr's own accounts ran on hand-written code around ASP.NET Core's password hasher: a decoy hash for unknown names,
a per-account gate against parallel guesses, a lockout counter on the user row, stored invite and reset links, a
session-revocation timestamp checked on each request, and a sign-in rate limit per address. The code had gaps: old
hashes were never rehashed, expired links were never purged, and the account page's password check bypassed the
lockout.

## Decision

Accounts are Identity's. `UserRow` is an `IdentityUser<Guid>` with Sammlarr's own columns, `SammlarrDbContext` an
`IdentityDbContext` with Identity's tables, and `Admin` and `Member` are Identity roles. `SignInManager` checks
passwords with Identity's lockout defaults (five tries, five minutes) and rehashes. `UserManager` changes passwords and
issues invite and reset links as password-reset tokens (one day, void once the security stamp changes). A Plex account
is stored as an external login. The cookie is Identity's application cookie, with the stamp checked on every request.

Sammlarr keeps what Identity has no notion of: the Plex PIN round trip, the first-admin setup code, the last-admin
rule, one sign-in method per install, invited users who have not joined yet, the admin password reset from
configuration, and the "Your access changed" prompt in an open circuit. Limits per address are the reverse proxy's.

Rejected: a custom `IUserStore` over the old table (keeps a schema Identity does not expect and still hand-maps every
field); OpenID Connect only (a self-hoster would have to run an identity provider just to sign in).

## Consequences

* Hashing, rehashing, lockout, token protection and cookie validation follow framework updates instead of Sammlarr's
  own code.
* Identity's defaults replace stricter custom ones: the lock lasts five minutes instead of fifteen, an unknown name
  answers without hashing, so timing tells real names apart, and parallel wrong guesses are not serialized, since
  Identity's counter uses optimistic concurrency. The proxy's limit per address slows both.
* Anyone who knows a name can lock it again, five minutes at a time.
* Invite and reset links are stateless: nothing to purge, but a second reset link does not cancel the first, and a link
  cannot name the admin who sent it.
* Identity's token provider reads the real clock, so the one-day expiry is not testable with a fake clock.
* The tables are `AspNetUsers`, `AspNetRoles` and their kin. A database from before the move migrates, but its accounts
  lose their roles, user names and Plex logins, so setup starts again.

## Notes

* 2026-10-16: the migration history was reset to one baseline the same day
  ([ADR 0003](0003-use-ef-core-migrations-for-the-schema.md)), so a database from before the move no longer migrates
  at all; it is deleted and set up again.
