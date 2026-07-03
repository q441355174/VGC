# Test Fixture Policy

This directory stores small, source-reviewed fixtures used by deterministic tests.

Current fixtures cover QGC-style plan and metadata compatibility. Replay and SITL release-hardening fixtures are cataloged in `docs/V1_30_SITL_REPLAY_FIXTURE_CATALOG.md`.

Replay fixtures:

- `replay/synthetic-heartbeat-minimal.tlog` is a tiny synthetic QGC-style telemetry log with two MAVLink v1 HEARTBEAT frames.
- `replay/synthetic-heartbeat-minimal.json` records the source, format, message inventory, hash, license, and privacy status.

Rules:

- Prefer synthetic fixtures generated for VGC tests.
- Do not copy QGC source code or binary assets.
- Do not add real flight logs without source, license/permission, and privacy review notes.
- Keep large logs outside the default repository unless they are required for a release gate.
