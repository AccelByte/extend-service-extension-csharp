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
ARG GOOS=$TARGETOS
ARG GOARCH=$TARGETARCH
ARG CGO_ENABLED=0
WORKDIR /build
COPY gateway/go.mod gateway/go.sum .
RUN go mod download
COPY gateway/ .
RUN go build -v -o /output/$TARGETOS/$TARGETARCH/grpc_gateway .


# Extend App
FROM mcr.microsoft.com/dotnet/aspnet:6.0-alpine3.19
ARG TARGETOS
ARG TARGETARCH
RUN apk --no-cache add bash
WORKDIR /app
COPY --from=grpc-gateway-builder /output/$TARGETOS/$TARGETARCH/grpc_gateway .
COPY gateway/apidocs ./apidocs
COPY gateway/third_party ./third_party
COPY --from=grpc-server-builder /output/* .
COPY wrapper.sh .
RUN chmod +x wrapper.sh
# gRPC server port, gRPC gateway port, Prometheus /metrics port
EXPOSE 6565 8000 8080
CMD ./wrapper.sh
