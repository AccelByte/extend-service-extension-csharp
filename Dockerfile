# .NET App Builder
# FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:6.0.302 as builder
FROM mcr.microsoft.com/dotnet/sdk:6.0.302 as builder
ARG PROJECT_PATH=src/AccelByte.PluginArch.ServiceExtension.Demo.Server
WORKDIR /build

COPY $PROJECT_PATH/*.csproj ./
RUN dotnet restore
COPY $PROJECT_PATH ./
RUN dotnet publish -c Release -o output

# GoLang App Builder
# FROM --platform=$BUILDPLATFORM golang:1.20 as go-builder
FROM golang:1.20 as go-builder
ARG GATEWAY_PATH=gateway
WORKDIR /build

COPY $GATEWAY_PATH/go.mod $GATEWAY_PATH/go.sum ./
RUN go mod download && go mod verify
COPY $GATEWAY_PATH/ ./
RUN CGO_ENABLED=0 go build -v -o /build_output/grpc_gateway ./
RUN ls -lha /build_output

# Service Image
FROM mcr.microsoft.com/dotnet/sdk:6.0.302
ARG GATEWAY_PATH=gateway
ARG SWAGGER_JSON=guildService.swagger.json

RUN apt-get update && \
    apt-get install -y supervisor --no-install-recommends && \
    rm -rf /var/lib/apt/lists/*
COPY supervisord.conf /etc/supervisor/supervisord.conf

WORKDIR /app
COPY --from=builder /build/output/* ./
COPY --from=go-builder /build_output/grpc_gateway ./
COPY $GATEWAY_PATH/$SWAGGER_JSON ./apidocs/
COPY $GATEWAY_PATH/third_party ./third_party
RUN chmod +x /app/grpc_gateway
RUN chmod +x /app/AccelByte.PluginArch.ServiceExtension.Demo.Server

# Plugin arch gRPC server port
EXPOSE 6565
# Prometheus /metrics web server port
EXPOSE 8080
# gRPC gateway Http port
EXPOSE 8000
ENTRYPOINT ["supervisord", "-c", "/etc/supervisor/supervisord.conf"]
