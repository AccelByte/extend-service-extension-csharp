# Conventions

## Commits

This project uses [Conventional Commits](https://www.conventionalcommits.org/): `<type>: <description>`.

Common types: `feat`, `fix`, `chore`, `docs`, `refactor`, `test`, `ci`, `build`. Include version numbers when bumping dependencies (e.g. `chore: update AccelByte Go SDK to 0.86.0`).

## Branches

The default branch is protected. Create a feature branch and open a PR for all changes.

## Code Style

- Preserve existing observability instrumentation (metrics, traces, logs) — it is pre-wired and required for Extend deployments.
- Preserve auth/authorization interceptors — they are pre-configured for AGS integration.
- Follow the existing code patterns in the repository when adding new functionality.
