# gRPC Server Builder
FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:6.0-alpine3.19 as grpc-server-builder
RUN apk update && apk add --no-cache gcompat
WORKDIR /build
COPY src/AccelByte.PluginArch.ServiceExtension.Demo.Server/*.csproj .
RUN dotnet restore
COPY src/AccelByte.PluginArch.ServiceExtension.Demo.Server .
RUN dotnet publish -c Release -o /output


# gRPC Gateway Builder
FROM --platform=$BUILDPLATFORM golang:1.20-alpine3.19 as grpc-gateway-builder
ARG TARGETOS
ARG TARGETARCH
WORKDIR /build
COPY gateway/go.mod gateway/go.sum .
RUN go mod download && go mod verify
COPY gateway/ .
RUN GOOS=$TARGETOS GOARCH=$TARGETARCH go build -v -o /output/grpc_gateway .


# Extend App
FROM mcr.microsoft.com/dotnet/aspnet:6.0-alpine3.19
RUN apk update && apk add --no-cache supervisor procps
COPY supervisord.conf /etc/supervisor/supervisord.conf
WORKDIR /app
COPY --from=grpc-gateway-builder /output/grpc_gateway .
COPY gateway/*.swagger.json ./apidocs/
COPY gateway/third_party ./third_party
COPY --from=grpc-server-builder /output/* .
# gRPC server port, gRPC gateway port, Prometheus /metrics port
EXPOSE 6565 8000 8080
ENTRYPOINT ["supervisord", "-c", "/etc/supervisor/supervisord.conf"]
