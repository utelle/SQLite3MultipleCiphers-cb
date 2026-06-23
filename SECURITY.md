# Security Policy

## Security Reports

This repository contains source amalgamations and build inputs used to produce release artifacts of [SQLite3MultipleCiphers](https://github.com/utelle/SQLite3MultipleCiphers).

While it includes [SQLite3MultipleCiphers](https://github.com/utelle/SQLite3MultipleCiphers) source code snapshots in form of amalgamated sources, it is not the authoritative source repository.

All security vulnerabilities must be reported in the main project repository:

https://github.com/utelle/SQLite3MultipleCiphers/security/policy

## Scope Clarification

This repository contains:

- Source code snapshots derived from the main repository
- Build configurations for cross-platform compilation
- Release preparation artifacts

Security-relevant logic originates from the main project and is maintained there.

Therefore, vulnerability assessment, triage, and coordinated disclosure are handled exclusively in the main repository.

## Note on Release Artifacts

If a security issue is suspected in generated build outputs or amalgamation snapshots, it should still be reported in the main repository to ensure consistent handling across all distribution channels.
