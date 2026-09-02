# Architecture Decision Log

This folder records significant architecture decisions as
[MADR](https://adr.github.io/madr/) files, one Markdown file per decision.

## How to add an ADR

1. Copy [`0000-template.md`](0000-template.md) to the next free number:
   `NNNN-imperative-verb-phrase.md` (lowercase, dash-separated).
2. Fill it in: status, context, drivers, considered options, outcome, consequences.
3. Add a row to the log below.

Decisions are append-only. To change one, add a new ADR and mark the old one
`superseded by` the new number rather than editing history.

## Log

| # | Title | Status | Date |
|---|-------|--------|------|
| 0001 | [Drive deployment from the Aspire AppHost](0001-drive-deployment-from-the-aspire-apphost.md) | accepted | 2026-09-02 |
