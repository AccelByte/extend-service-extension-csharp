---
name: add-endpoint
description: Add a new gRPC endpoint to this Extend app. Handles proto definition, code generation, and service implementation scaffolding.
user-invocable: true
allowed-tools: Bash, Read, Glob, Grep, Edit, Write
argument-hint: <endpoint description, e.g. "GetPlayerStats returns aggregated stats for a player">
---

# Add Endpoint

Add a new gRPC endpoint to this Extend app. This skill handles the full flow: proto definition → code generation → service implementation scaffolding.

## Arguments

`$ARGUMENTS`

Parse for:
- **Endpoint description**: What the new endpoint does (required)
- Method name, request/response types if provided (optional — will prompt if missing)

## Determine Scenario

Read `AGENTS.md` to identify the Extend scenario. The workflow differs:

| Scenario | Proto editable? | Flow |
|---|---|---|
| **Override** | NO — proto is AccelByte-provided | You can only implement existing methods. If the user wants a method not in the proto, explain this limitation. |
| **Service Extension** | YES — user-defined proto | Full flow: define proto → generate → implement |
| **Event Handler** | NO — events come from AccelByte API Proto | You can only handle existing event types. Help the user pick the right event. |

## Process — Service Extension (full flow)

### Step 1: Examine existing proto
Read the `.proto` file(s) to understand the current service definition, naming conventions, and import patterns.

### Step 2: Define the new endpoint
Add the new RPC method to the proto file:
- Follow existing naming conventions (PascalCase methods, snake_case fields)
- Add HTTP annotations for gRPC-Gateway REST mapping
- Define request/response message types

Show the user the proto diff and ask for confirmation.

### Step 3: Generate code
Run the proto code generation command from AGENTS.md's Build & Test section.

### Step 4: Scaffold implementation
Add a stub implementation of the new method in the service file:
- Return a placeholder response
- Add a TODO comment for the user to fill in business logic
- Follow the existing code patterns (error handling, logging, auth checks)

### Step 5: Verify
- Build the project (command from AGENTS.md)
- Run tests to ensure nothing is broken
- Show the user what was created

## Process — Override

### Step 1: List available methods
Read the proto file and list all defined RPC methods. Show which ones already have implementations and which are stubs.

### Step 2: Implement the method
If the user wants a method that exists in the proto:
- Add or update the implementation in the service file
- Follow existing patterns for request parsing, response construction, and error handling

If the method doesn't exist in the proto, explain that Override protos are AccelByte-provided and cannot be modified.

## Process — Event Handler

### Step 1: Identify the event
Help the user identify which AGS event to handle. Check the existing handler registration to see what events are already wired.

### Step 2: Add handler
- Register the new event type in the handler configuration
- Add a handler method with proper idempotency patterns
- Include a TODO for the user's business logic

## Important

- **Always show diffs** before and after changes
- **Always verify the build passes** after making changes
- **Follow existing code style** in the repo — match indentation, naming, error handling patterns
- **Don't modify proto files in Override or Event Handler repos** — they're AccelByte-provided
