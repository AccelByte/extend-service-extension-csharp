# extend-service-extension-csharp

An Extend Service Extension app written in C#. Exposes custom REST endpoints through AGS's API gateway via gRPC-Gateway.

This is a template project — clone it, replace the sample logic in the service implementation, and deploy.

## Build & Test

```bash
dotnet build src/                    # Build the solution
dotnet test src/                     # Run tests
docker compose up --build            # Run locally with Docker
```

## Architecture

Game clients reach this app through AGS via auto-generated REST endpoints:

```
Game Client → AGS Gateway → [REST] → gRPC-Gateway → [gRPC] → This App
```

The proto file defines both the gRPC service and the REST mapping (via `google.api.http` annotations). The gRPC-Gateway automatically generates an OpenAPI spec and REST proxy from the proto.

The sample implementation demonstrates a custom guild progress service with CRUD operations, storing data in CloudSave via the AccelByte SDK, exposed as REST endpoints through gRPC-Gateway HTTP annotations.

### Key Files

| Path | Purpose |
|---|---|
| `src/AccelByte.Extend.ServiceExtension.Server/Program.cs` | Entry point — starts gRPC server, wires interceptors and observability |
| `src/AccelByte.Extend.ServiceExtension.Server/Classes/DefaultAccelByteServiceProvider.cs` | **Service implementation** — your custom logic goes here |
| `src/AccelByte.Extend.ServiceExtension.Server/Services/MyService.cs` | **Service implementation** — your custom logic goes here |
| `src/AccelByte.Extend.ServiceExtension.Server/Protos/permission.proto` | gRPC service definition (user-defined, add your endpoints here) |
| `src/AccelByte.Extend.ServiceExtension.Server/Protos/service.proto` | gRPC service definition (user-defined, add your endpoints here) |
| `src/AccelByte.Extend.ServiceExtension.Server/Protos/` | Generated code from proto (do not hand-edit) |
| `docker-compose.yaml` | Local development setup |
| `.env.template` | Environment variable template |

## Rules

See `.agents/rules/` for coding conventions, commit standards, and proto file policies.

## Environment

Copy `.env.template` to `.env` and fill in your credentials.

| Variable | Description |
|---|---|
| `AB_BASE_URL` | AccelByte base URL (e.g. `https://test.accelbyte.io`) |
| `AB_NAMESPACE` | Target namespace |
| `AB_CLIENT_ID` | OAuth client ID |
| `AB_CLIENT_SECRET` | OAuth client secret |
| `PLUGIN_GRPC_SERVER_AUTH_ENABLED` | Enable gRPC auth (`true` by default) |
| `BASE_PATH` | Custom base path for REST endpoints |

## Dependencies

- [AccelByte .NET SDK](https://github.com/AccelByte/accelbyte-csharp-sdk) (`AccelByte.Sdk` on NuGet) — AGS platform SDK and gRPC plugin utilities
