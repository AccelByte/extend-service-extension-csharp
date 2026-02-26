---
name: update-sdk
description: Bump the AccelByte SDK to a specified version. Updates dependency files, runs tests, and prepares a commit.
user-invocable: true
allowed-tools: Bash, Read, Glob, Grep, Edit, Write
argument-hint: <version> (e.g. "0.87.0")
---

# Update SDK

Bump the AccelByte SDK dependency to a new version. Handles the dependency file update, build verification, and test run.

## Arguments

`$ARGUMENTS`

Parse for:
- **Version**: The target SDK version (required, e.g. `0.87.0`)

## Process

### Step 1: Identify the dependency file and current version
Read `AGENTS.md` to identify the language, then find the dependency file:

| Language | File | Pattern |
|---|---|---|
| Go | `go.mod` | `github.com/AccelByte/accelbyte-go-sdk v<version>` |
| Java | `build.gradle` or `pom.xml` | `net.accelbyte.sdk:sdk:<version>` |
| Python | `requirements.txt` or `setup.py` | `accelbyte-py-sdk==<version>` |
| C# | `*.csproj` | `AccelByte.Sdk" Version="<version>"` |

Show the current version and confirm the upgrade with the user.

### Step 2: Update the version
Edit the dependency file to replace the old version with the new one. Use exact string replacement (no regex).

### Step 3: Update lock files / download dependencies

| Language | Command |
|---|---|
| Go | `go mod tidy` |
| Java (Gradle) | `./gradlew dependencies` |
| Java (Maven) | `mvn dependency:resolve` |
| Python | `pip install -r requirements.txt` |
| C# | `dotnet restore` |

### Step 4: Build
Run the build command from AGENTS.md. If the build fails, show the errors — breaking changes in the SDK may require code updates.

### Step 5: Test
Run unit tests from AGENTS.md. Report pass/fail.

### Step 6: Show diff
Show `git diff` of all changed files. Typically this is just the dependency file and lock file.

### Step 7: Suggest commit message
```
chore: update AccelByte <Language> SDK to <version>
```

Do NOT commit automatically — let the user decide.

## Important

- **Always show the version change** (old → new) before making it
- **If the build breaks**, help the user fix breaking API changes
- **Don't commit** — just prepare the changes for the user to review
