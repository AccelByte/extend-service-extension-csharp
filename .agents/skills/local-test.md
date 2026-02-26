---
name: local-test
description: Run the full local test cycle — build, unit tests, Docker Compose integration test, and cleanup.
user-invocable: true
allowed-tools: Bash, Read, Glob, Grep
argument-hint: [--skip-docker] [--verbose]
---

# Local Test

Run the full local test cycle for this Extend app: build → unit tests → Docker Compose integration test → cleanup.

## Arguments

`$ARGUMENTS`

Parse for:
- `--skip-docker`: Skip the Docker Compose integration test (unit tests only)
- `--verbose`: Show full command output instead of summaries

## Process

### Step 1: Read build commands
Read `AGENTS.md` to get the correct build, test, and Docker commands for this repo.

### Step 2: Check prerequisites
```bash
docker info > /dev/null 2>&1 || echo "Docker is not running"
```
If Docker is not running and the user didn't pass `--skip-docker`, warn them.

### Step 3: Check environment
Verify `.env` exists (copied from `.env.template`). If missing, warn the user:
> `.env` file not found. Copy `.env.template` to `.env` and fill in your credentials before running integration tests.

### Step 4: Build
Run the build command from AGENTS.md. Report success/failure.

### Step 5: Unit tests
Run the unit test command from AGENTS.md. Report:
- Number of tests passed/failed
- Any test failures with relevant output

### Step 6: Docker Compose integration test (unless --skip-docker)
```bash
docker compose up --build -d
```
Wait for the service to be healthy (check logs or health endpoint), then:
```bash
docker compose down -v
```

Report whether the service started successfully.

### Step 7: Summary
Show a results table:

```
Build:        ✓ passed
Unit tests:   ✓ 12 passed, 0 failed
Docker:       ✓ service started cleanly
```

If anything failed, show the relevant error output and suggest fixes.
