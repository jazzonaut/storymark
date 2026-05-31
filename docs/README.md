# Storymark Docs

Markdown that isn't a root-level file lives here.

## Top-level

- [`vision-and-architecture.md`](vision-and-architecture.md) — what Storymark is and how the pieces fit.
- [`plan.md`](plan.md) — full implementation plan. Long. Skim headings, read the section you need.
- [`issues.md`](issues.md) — Phase 0–6 backlog as vertical-slice issues.
- [`delivery-plan.md`](delivery-plan.md) — phase-by-phase milestones at a glance.

## ADRs

[`adr/`](adr/) — one file per architectural decision, numbered. Start with [`adr/README.md`](adr/README.md).

## Conventions

[`conventions/`](conventions/) — code style, naming, commit/PR shape. Strict; CI enforces what can be enforced.

- [`01-typescript.md`](conventions/01-typescript.md)
- [`02-vue.md`](conventions/02-vue.md)
- [`03-dotnet.md`](conventions/03-dotnet.md)
- [`04-naming.md`](conventions/04-naming.md)
- [`05-commit-and-pr.md`](conventions/05-commit-and-pr.md)

## Deployment

[`deployment/`](deployment/) — server prereqs, CI/CD, Portainer, reverse-proxy options, auth options. Designed to be portable: the reference is a Linux box with Docker + Portainer + a reverse proxy, but each layer is independently swappable.

- [`00-prereqs.md`](deployment/00-prereqs.md)
- [`01-ci-cd.md`](deployment/01-ci-cd.md)
- [`02-portainer-gitops.md`](deployment/02-portainer-gitops.md)
- [`03-reverse-proxy-options.md`](deployment/03-reverse-proxy-options.md)
- [`04-auth-options.md`](deployment/04-auth-options.md)
- [`05-backup-and-restore.md`](deployment/05-backup-and-restore.md)
