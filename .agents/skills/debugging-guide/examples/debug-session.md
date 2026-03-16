# Example: Debugging a 500 Internal Server Error

This annotated example shows a realistic debugging session for an Extend Service Extension
(C#) app where `GetGuildProgress` returns `500 Internal Server Error`.

---

## The Report

A developer sends this message:

> *"My service is running but when I call GET `/v1/admin/namespace/mygame/progress/guild_001`
> I get 500. The service started fine."*

---

## Step 1 — Collect logs

Ask for or check the log output. The developer shares:

```
info: AccelByte.Extend.ServiceExtension.Server.DebugLoggerServerInterceptor[0]
      REQUEST /service.Service/GetGuildProgress
info: AccelByte.Extend.ServiceExtension.Server.DebugLoggerServerInterceptor[0]
      REQUEST <headers omitted>
fail: AccelByte.Extend.ServiceExtension.Server.DebugLoggerServerInterceptor[0]
      Error thrown by /service.Service/GetGuildProgress.
      System.Exception: NULL response from cloudsave service.
         at AccelByte.Extend.ServiceExtension.Server.Services.MyService.GetGuildProgress(...)
fail: AccelByte.Extend.ServiceExtension.Server.ExceptionHandlingInterceptor[0]
      /service.Service/GetGuildProgress - Error: NULL response from cloudsave service.
```

**What this tells us:**
- The request reached `GetGuildProgress` — auth passed (otherwise we'd see no `REQUEST` log).
- The SDK call to CloudSave returned `null`.
- `MyService` threw `"NULL response from cloudsave service."`.
- `ExceptionHandlingInterceptor` caught it and returned `StatusCode.Internal` (HTTP 500).

---

## Step 2 — Read the service method

Looking at `Services/MyService.cs`:

```csharp
public override Task<GetGuildProgressResponse> GetGuildProgress(
    GetGuildProgressRequest request, ServerCallContext context)
{
    string gpKey = $"guildProgress_{request.GuildId.Trim()}";

    var response = _ABProvider.Sdk.Cloudsave.AdminGameRecord.AdminGetGameRecordHandlerV1Op
        .Execute<GuildProgressData>(gpKey, request.Namespace);
    if (response == null)
        throw new Exception("NULL response from cloudsave service.");

    GuildProgressData savedData = response.Value!;
    return Task.FromResult(new GetGuildProgressResponse()
    {
        GuildProgress = savedData.ToGuildProgressGrpcData()
    });
}
```

**Problem identified:** When CloudSave returns a 404 (the record doesn't exist yet), the
AccelByte SDK returns `null` for the response. The code treats every `null` the same way —
throwing a generic exception that becomes `StatusCode.Internal`. A missing record should
instead return `StatusCode.NotFound`.

---

## Step 3 — Verify the SDK response behaviour

SDK operations like `AdminGetGameRecordHandlerV1Op.Execute<T>()` return `null` when the
HTTP response is a 4xx (the record is not found) rather than throwing a typed exception
for not-found conditions. Confirm this is the root cause by checking the SDK call returns
null specifically on a 404, not due to a real service failure.

Enable verbose SDK logging temporarily to see the raw HTTP response:

```bash
# Set env var and restart
ABSERVER_Logging__LogLevel__Default=Debug dotnet run --project src/AccelByte.Extend.ServiceExtension.Server/
```

Look in the output for the outbound HTTP request and the `404 Not Found` response from
the CloudSave endpoint.

---

## Step 4 — The fix

In `Services/MyService.cs`, distinguish "record not found" from a real internal error:

```csharp
// Before
var response = _ABProvider.Sdk.Cloudsave.AdminGameRecord.AdminGetGameRecordHandlerV1Op
    .Execute<GuildProgressData>(gpKey, request.Namespace);
if (response == null)
    throw new Exception("NULL response from cloudsave service.");

// After
var response = _ABProvider.Sdk.Cloudsave.AdminGameRecord.AdminGetGameRecordHandlerV1Op
    .Execute<GuildProgressData>(gpKey, request.Namespace);
if (response == null)
    throw new RpcException(new Status(StatusCode.NotFound,
        $"Guild progress not found for guild '{request.GuildId}' in namespace '{request.Namespace}'."));

if (response.Value == null)
    throw new RpcException(new Status(StatusCode.Internal,
        "CloudSave returned a response with no value."));
```

Using `RpcException` directly bypasses `ExceptionHandlingInterceptor`'s generic wrapping:
the interceptor only catches `Exception`, so a `RpcException` propagates its status code
unchanged to the caller.

---

## Step 5 — Verify

```bash
# Should now return 404, not 500
curl -s -o /dev/null -w "%{http_code}" \
  http://localhost:8080/v1/admin/namespace/mygame/progress/nonexistent_guild
# Expected: 404
```

And in the logs:
```
info: AccelByte.Extend.ServiceExtension.Server.DebugLoggerServerInterceptor[0]
      REQUEST /service.Service/GetGuildProgress
warn: Grpc.AspNetCore.Server.ServerCallHandler[5]
      Error status code 'NotFound' raised.
```

No more `StatusCode.Internal` / HTTP 500.

---

## Key takeaways from this session

1. **Read the log first** — `DebugLoggerServerInterceptor` logs every inbound call and
   `ExceptionHandlingInterceptor` logs every error with the message. You often don't need
   a debugger to find the failure layer.
2. **Follow the error upward** — the HTTP 500 came from `StatusCode.Internal`, which came
   from a generic `Exception`, which came from a `null` SDK response, which came from a
   CloudSave 404. Trace each layer back to the root.
3. **Throw `RpcException` for known conditions** — `ExceptionHandlingInterceptor` maps
   any `Exception` to `StatusCode.Internal`. For known conditions (not found, invalid
   input), throw `RpcException` directly with the correct status code so callers receive
   meaningful gRPC/HTTP status codes.
