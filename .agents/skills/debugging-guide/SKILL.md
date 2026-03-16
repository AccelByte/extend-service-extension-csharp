---
name: debugging-guide
description: Debug issues in this C# Extend Service Extension app. Use when troubleshooting build failures, gRPC errors, auth problems, AccelByte SDK issues, or unexpected service behavior.
allowed-tools: Bash, Read, Glob, Grep
---

# Debugging Guide

Systematic debugging playbook for this C# Extend Service Extension app (ASP.NET Core + gRPC).

## Architecture Quick Reference

```
Game Client → AGS Gateway → [REST] → gRPC-Gateway (:gateway) → [gRPC] → This App (:6565)
```

| Port | Protocol | Purpose |
|------|----------|---------|
| 6565 | gRPC (HTTP/2) | gRPC service endpoint |
| 8080 | HTTP/1 | Prometheus metrics (`/metrics`), health check (`/healthz`) |

Key files to check when debugging:

| File | What it controls |
|---|---|
| `src/AccelByte.Extend.ServiceExtension.Server/Services/MyService.cs` | Service logic |
| `src/AccelByte.Extend.ServiceExtension.Server/Classes/DefaultAccelByteServiceProvider.cs` | SDK init, login |
| `src/AccelByte.Extend.ServiceExtension.Server/Classes/AuthorizationInterceptor.cs` | gRPC auth |
| `src/AccelByte.Extend.ServiceExtension.Server/Classes/ExceptionHandlingInterceptor.cs` | Error wrapping |
| `src/AccelByte.Extend.ServiceExtension.Server/appsettings.json` | App configuration |
| `src/AccelByte.Extend.ServiceExtension.Server/Protos/service.proto` | RPC definitions |

---

## Step 1 — Identify the failure layer

Start by reading the error output or logs and placing the failure in one of these layers:

| Symptom | Likely layer | Jump to |
|---|---|---|
| `dotnet build` fails | Build / codegen | [Build failures](#build-failures) |
| App crashes on startup | Startup / config | [Startup failures](#startup-failures) |
| `StatusCode.Unauthenticated` or `StatusCode.PermissionDenied` | Auth interceptor | [Auth errors](#auth-errors) |
| `StatusCode.Internal` or unhandled exception | Service logic | [Service logic errors](#service-logic-errors) |
| AccelByte SDK call returns null or throws | SDK / backend | [SDK errors](#sdk-errors) |
| Health check failing / Kubernetes probe error | Health endpoint | [Health check issues](#health-check-issues) |
| Proto changes not reflected | Code generation | [Proto generation issues](#proto-generation-issues) |
| Docker Compose won't start | Environment / config | [Docker issues](#docker-issues) |

---

## Build Failures

Run the build and capture output:

```bash
dotnet build src/extend-service-extension-server.sln /property:GenerateFullPaths=true
```

### CS errors after changing `.proto`

Proto files are compiled by the `Grpc.Tools` MSBuild integration. If generated C# is out of date:

```bash
# Force a clean rebuild
dotnet clean src/extend-service-extension-server.sln
dotnet build src/extend-service-extension-server.sln
```

If the proto itself has syntax errors, `protoc` (invoked by MSBuild) will emit errors like:
```
error : service.proto: Expected ";".
```
Open `src/AccelByte.Extend.ServiceExtension.Server/Protos/service.proto` and fix the syntax, then rebuild.

### Missing package / NuGet restore failure

```bash
dotnet restore src/extend-service-extension-server.sln
```

If restore fails behind a proxy or in offline mode, check NuGet configuration or network access.

### Method not found / signature mismatch after SDK update

After bumping `AccelByte.Sdk` in the `.csproj`, the API surface may have changed. Check `MyService.cs` for compile errors and update call sites to match the new SDK signatures.

---

## Startup Failures

Run the app locally:

```bash
cd src/AccelByte.Extend.ServiceExtension.Server
DOTNET_ENVIRONMENT=Development dotnet run
```

### "Missing AccelByte configuration section"

`DefaultAccelByteServiceProvider` throws this if the `AccelByte` section in `appsettings.json` is missing or empty. Environment variables (`AB_BASE_URL`, `AB_CLIENT_ID`, etc.) are mapped via `ReadEnvironmentVariables()` — confirm they are set:

```bash
# Check required env vars
echo "AB_BASE_URL=$AB_BASE_URL"
echo "AB_CLIENT_ID=$AB_CLIENT_ID"
echo "AB_CLIENT_SECRET=$AB_CLIENT_SECRET"
echo "AB_NAMESPACE=$AB_NAMESPACE"
```

If running locally without real credentials, set `PLUGIN_GRPC_SERVER_AUTH_ENABLED=false` to disable auth.

### SDK `LoginClient` fails at startup

`DefaultAccelByteServiceProvider` calls `Sdk.LoginClient(true)` during DI construction. If this fails, the whole app fails to start. Common causes:

- `AB_BASE_URL` is wrong or unreachable — verify with `curl $AB_BASE_URL/iam/version`
- `AB_CLIENT_ID` / `AB_CLIENT_SECRET` are invalid or don't have the required permissions
- Network / TLS issue — check if the host is reachable from the container

### Port already in use

The app binds gRPC on `:6565` and HTTP on `:8080` (see `appsettings.json`). If another process holds those ports:

```bash
ss -tlnp | grep -E '6565|8080'
# or
lsof -i :6565
lsof -i :8080
```

Kill the conflicting process or change the ports in `appsettings.json`.

---

## Auth Errors

The `AuthorizationInterceptor` validates every inbound gRPC call against the AGS IAM service. It reads permission annotations from the proto file to determine the required resource and action.

### `StatusCode.Unauthenticated`

The call arrived with no Bearer token or an expired one. Steps:

1. Confirm the caller is sending a valid JWT in the `authorization` metadata header.
2. If testing locally with auth disabled, set `PLUGIN_GRPC_SERVER_AUTH_ENABLED=false`.
3. Check `DebugLoggerServerInterceptor` output — it logs `REQUEST <method>` and the request headers, which will show the raw `authorization` header value.

### `StatusCode.PermissionDenied`

The token is valid but the caller lacks the permission declared in the proto:

```proto
option (permission.action) = READ;
option (permission.resource) = "ADMIN:NAMESPACE:{namespace}:CLOUDSAVE:RECORD";
```

Verify the client's OAuth token has the correct role/permission in IAM. The `{namespace}` placeholder in the resource string is filled at runtime by the interceptor — confirm the caller's namespace matches `AB_NAMESPACE`.

### Internal gRPC methods being rejected (health check / reflection)

`AuthorizationInterceptor.IsInternalMethod()` whitelists `/grpc.reflection.*` and `/grpc.health.*`. If you add new interceptor logic, make sure this whitelist is preserved — removing it breaks Kubernetes probes.

---

## Service Logic Errors

`ExceptionHandlingInterceptor` catches any `Exception` thrown from a service method and wraps it as `StatusCode.Internal` with the exception message. Check the logs for the actual exception.

### Finding the root cause

`DebugLoggerServerInterceptor` logs errors with `LogError(x, ...)`, which includes the full stack trace. Look for lines like:

```
Error thrown by /service.Service/GetGuildProgress.
```

Followed by the stack trace.

### Null response from AccelByte SDK

`MyService.cs` throws `"NULL response from cloudsave service."` when an SDK response is null. This means the CloudSave API returned nothing. Possible causes:

- The record key does not exist (for read operations) — this is expected; handle it gracefully
- The namespace is wrong — verify `request.Namespace` matches what's stored in CloudSave
- The SDK client's token expired — `AutoTokenRefresh` should handle this, but check if SDK login succeeded at startup

### gRPC `ServerCallContext` issues

The `context` parameter carries metadata (headers), cancellation tokens, and the method name. Avoid blocking `context.CancellationToken` — all service methods should be async. If a method does CPU-bound work synchronously, wrap it in `Task.Run`.

---

## SDK Errors

The `AccelByteSDK` is initialized in `DefaultAccelByteServiceProvider` and injected as `IAccelByteServiceProvider`. Access it via `_ABProvider.Sdk` in service methods.

### Diagnosing SDK call failures

Temporarily enable verbose SDK logging by setting the environment variable:
```bash
ABSERVER_Logging__LogLevel__Default=Debug
```
This shows the raw HTTP requests and responses from the SDK.

Alternatively, read `DefaultAccelByteServiceProvider.cs` and check what `Config` values are loaded — the `AppSettingConfigRepository` maps env vars to SDK config.

### SDK token refresh problems

The SDK uses `UseAutoTokenRefresh()`. If the app runs for a long time and starts getting auth errors on SDK calls (not on inbound gRPC calls), the token refresh feature may have failed. Check logs for any SDK-level errors and restart the app as a first remediation.

### CloudSave record not found

For `AdminGetGameRecordHandlerV1Op`, a missing record typically results in a non-null response with a null `Value`. Modify `MyService.cs` to check `response.Value == null` and return a meaningful gRPC status (e.g., `StatusCode.NotFound`) rather than letting a null dereference propagate.

---

## Health Check Issues

Health checks are configured in `Program.cs`:

```csharp
builder.Services.AddGrpcHealthChecks()
    .AddCheck("Health", () => HealthCheckResult.Healthy());
```

The health check service is mapped at `/grpc.health.v1.Health/Check` (gRPC) and `/healthz` (HTTP via gRPC-Gateway if configured).

### Kubernetes probe 503

If the app is running but Kubernetes probes fail:

1. Confirm the app started and bound port `8080` — check startup logs
2. Test health directly: `curl http://localhost:8080/healthz`
3. Verify `IsInternalMethod()` still whitelists `/grpc.health.*` — a regression here causes probes to get `Unauthenticated` and report unhealthy

---

## Proto Generation Issues

Proto files are compiled automatically by MSBuild via `Grpc.Tools`. For gateway proxy regeneration (gRPC-Gateway, OpenAPI spec), run:

```bash
./proto.sh
```

Or from VS Code: use the **Proto: Generate** task.

### Generated C# types missing or stale

After editing `service.proto`:

```bash
dotnet clean src/extend-service-extension-server.sln
dotnet build src/extend-service-extension-server.sln
```

The `.cs` files for gRPC stubs are generated into the build output, not checked in. If IntelliSense shows missing types, trigger a build.

### Gateway not reflecting proto changes

The gateway Go code in `gateway/pkg/pb/` is generated by `proto.sh`. After proto changes, run `./proto.sh` and restart the gateway.

---

## Docker Issues

```bash
# Build and start
docker compose up --build

# View logs
docker compose logs -f app

# Tear down
docker compose down -v
```

### App exits immediately in Docker

Check that `.env` exists and has all required values (copy from `.env.template`):

```bash
ls -la .env
```

The `docker-compose.yaml` passes these env vars to the container: `AB_BASE_URL`, `AB_CLIENT_ID`, `AB_CLIENT_SECRET`, `AB_NAMESPACE`, `PLUGIN_GRPC_SERVER_AUTH_ENABLED`, `BASE_PATH`.

### "host.docker.internal" not resolving

`docker-compose.yaml` includes `extra_hosts: host.docker.internal:host-gateway`. This is required for the Zipkin exporter to reach the host machine. On Linux, this is added automatically; on macOS/Windows it's built into Docker Desktop.

### Checking metrics and health from outside the container

```bash
# Metrics
curl http://localhost:8080/metrics

# gRPC health check (requires grpcurl)
grpcurl -plaintext localhost:6565 grpc.health.v1.Health/Check
```

---

## Log Interpretation

The app uses `Microsoft.Extensions.Logging` with structured log output. Set log level via environment variable:

```bash
# Verbose (shows all debug output including AccelByte SDK HTTP calls)
ABSERVER_Logging__LogLevel__Default=Debug

# Default (information level)
ABSERVER_Logging__LogLevel__Default=Information
```

Key log patterns to look for:

| Log pattern | Meaning |
|---|---|
| `REQUEST /service.Service/<Method>` | Inbound gRPC call (from `DebugLoggerServerInterceptor`) |
| `RESPONSE /service.Service/<Method>` | Successful response |
| `Error thrown by /service.Service/<Method>` | Unhandled exception in service method |
| `<method> - Error: <msg>` | Exception caught by `ExceptionHandlingInterceptor`, returned as `StatusCode.Internal` |

---

## Examples

For an end-to-end walkthrough of a real debugging session, see [examples/debug-session.md](examples/debug-session.md). It traces a `StatusCode.Internal` / HTTP 500 back through `ExceptionHandlingInterceptor` → `MyService` → a null AccelByte SDK response → a CloudSave 404, and shows the correct fix.

---

## Quick Checklist

When stuck, run through this list:

- [ ] Does `dotnet build` pass cleanly?
- [ ] Are all required env vars set (`AB_BASE_URL`, `AB_CLIENT_ID`, `AB_CLIENT_SECRET`, `AB_NAMESPACE`)?
- [ ] Is `PLUGIN_GRPC_SERVER_AUTH_ENABLED` set intentionally (`true` / `false`)?
- [ ] Did `Sdk.LoginClient` succeed at startup? (check logs for SDK errors)
- [ ] Is the error on the inbound gRPC path (interceptors) or inside the service method?
- [ ] For proto changes: did you rebuild after editing `.proto`? Did you run `./proto.sh` for gateway changes?
- [ ] For Docker issues: does `.env` exist with all required credentials?
