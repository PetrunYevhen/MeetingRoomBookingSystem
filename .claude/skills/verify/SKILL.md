---
name: verify
description: Run the repository's full verification checklist — backend restore/build/test/format plus frontend install/lint/format/build — and report exactly which step failed. Use before committing, before opening a PR, or whenever the user asks to verify, check, or validate the repo.
---

# Verify the repository

Runs the same checklist CI runs (`.github/workflows/ci.yml`), in the same order, so a
green local run means a green pipeline.

## Preconditions

- Docker must be running: the integration tests start their own SQL Server through
  Testcontainers. If `docker info` fails, stop and tell the user — do not skip
  `dotnet test` and report success.
- No local database needs to be started by hand.

## Steps

Run from the repository root, stopping at the first failure:

```sh
dotnet restore --locked-mode
dotnet build --no-restore
dotnet test --no-build
dotnet format --verify-no-changes --no-restore
npm --prefix src/Frontend ci
npm --prefix src/Frontend run lint
npm --prefix src/Frontend run format:check
npm --prefix src/Frontend run build
```

`dotnet test` takes the longest (a real SQL Server container starts once for the run);
run it in the background and continue with the frontend steps while it finishes.

## Reporting

- Name the failing command and quote its error verbatim. Never paraphrase a compiler or
  test failure.
- `dotnet format` failures are auto-fixable: rerun without `--verify-no-changes`, then
  re-verify. Same for `format:check` → `npm --prefix src/Frontend run format`.
- On success, report the test counts (unit and integration) rather than just "passed".
