# Copyright (c) 2025 AccelByte Inc. All Rights Reserved.
# This is licensed software from AccelByte Inc, for limitations
# and restrictions contact your company contract manager.

# ----------------------------------------
# Stage 1: Protoc Code Generation
# ----------------------------------------
FROM --platform=$BUILDPLATFORM ubuntu:22.04 AS proto-builder

# Avoid warnings by switching to noninteractive
ENV DEBIAN_FRONTEND=noninteractive

ARG PROTOC_VERSION=21.9
ARG GO_VERSION=1.24.10

# Configure apt and install packages
RUN apt-get update \
    && apt-get install -y --no-install-recommends \
    #
    # Install essential development tools
    build-essential \
    ca-certificates \
    git \
    unzip \
    wget \
    #
    # Detect architecture for downloads
    && ARCH_SUFFIX=$(case "$(uname -m)" in \
        x86_64) echo "x86_64" ;; \
        aarch64) echo "aarch_64" ;; \
        *) echo "x86_64" ;; \
       esac) \
    && GOARCH_SUFFIX=$(case "$(uname -m)" in \
        x86_64) echo "amd64" ;; \
        aarch64) echo "arm64" ;; \
        *) echo "amd64" ;; \
       esac) \
    #
    # Install Protocol Buffers compiler
    && wget -O protoc.zip https://github.com/protocolbuffers/protobuf/releases/download/v${PROTOC_VERSION}/protoc-${PROTOC_VERSION}-linux-${ARCH_SUFFIX}.zip \
    && unzip protoc.zip -d /usr/local \
    && rm protoc.zip \
    && chmod +x /usr/local/bin/protoc \
    #
    # Install Go 1.24
    && wget -O go.tar.gz https://go.dev/dl/go${GO_VERSION}.linux-${GOARCH_SUFFIX}.tar.gz \
    && tar -C /usr/local -xzf go.tar.gz \
    && rm go.tar.gz \
    #
    # Clean up
    && apt-get autoremove -y \
    && apt-get clean -y \
    && rm -rf /var/lib/apt/lists/*

# Set up Go environment
ENV GOROOT=/usr/local/go
ENV GOPATH=/go
ENV PATH=$GOPATH/bin:$GOROOT/bin:$PATH

# Install protoc Go tools and plugins
RUN go install -v google.golang.org/protobuf/cmd/protoc-gen-go@latest \
    && go install -v google.golang.org/grpc/cmd/protoc-gen-go-grpc@latest \
    && go install -v github.com/grpc-ecosystem/grpc-gateway/v2/protoc-gen-grpc-gateway@v2.26.3 \
    && go install -v github.com/grpc-ecosystem/grpc-gateway/v2/protoc-gen-openapiv2@v2.26.3

# Set working directory.
WORKDIR /build

# Copy proto sources and generator script.
COPY proto.sh .
COPY gateway gateway
COPY src src

# Make script executable and run it.
RUN chmod +x proto.sh && \
    ./proto.sh

# ----------------------------------------
# Stage 2: gRPC Gateway Builder
# ----------------------------------------
FROM --platform=$BUILDPLATFORM golang:1.24 AS grpc-gateway-builder

ARG TARGETOS
ARG TARGETARCH

ARG GOOS=$TARGETOS
ARG GOARCH=$TARGETARCH
ARG CGO_ENABLED=0

# Set working directory.
WORKDIR /build

# Copy gateway go module files.
COPY gateway/go.mod gateway/go.sum ./

# Download dependencies.
RUN go mod download

# Copy application code.
COPY gateway/ .

# Copy generated protobuf files from stage 1.
RUN rm -rf pkg/pb
COPY --from=proto-builder /build/gateway/pkg/pb ./pkg/pb

# Build application code.
RUN go build -v -o /output/$TARGETOS/$TARGETARCH/grpc_gateway .

# ----------------------------------------
# Stage 3: gRPC Server Builder
# ----------------------------------------
FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:8.0-alpine3.22 AS grpc-server-builder

ARG TARGETARCH

RUN apk update && apk add --no-cache gcompat

# Set working directory.
WORKDIR /project

# Copy project file and restore dependencies.
COPY src/AccelByte.Extend.ServiceExtension.Server/*.csproj .
RUN ([ "$TARGETARCH" = "amd64" ] && echo "linux-musl-x64" || echo "linux-musl-$TARGETARCH") > /tmp/dotnet-rid
RUN dotnet restore -r $(cat /tmp/dotnet-rid)

# Copy application code.
COPY src/AccelByte.Extend.ServiceExtension.Server .

# Build and publish application.
RUN dotnet publish -c Release -r $(cat /tmp/dotnet-rid) --no-restore -o /build/

# ----------------------------------------
# Stage 4: Runtime Container
# ----------------------------------------
FROM mcr.microsoft.com/dotnet/aspnet:8.0-alpine3.22

ARG TARGETOS
ARG TARGETARCH

# Set working directory.
WORKDIR /app

# Copy gateway binary from stage 2.
COPY --from=grpc-gateway-builder /output/$TARGETOS/$TARGETARCH/grpc_gateway .

# Copy apidocs from stage 1.
COPY --from=proto-builder /build/gateway/apidocs ./apidocs
RUN rm -fv apidocs/permission.swagger.json

# Copy gateway third party files.
COPY gateway/third_party ./third_party

# Copy server build from stage 3.
COPY --from=grpc-server-builder /build/* .

# Copy entrypoint script.
COPY wrapper.sh .
RUN chmod +x wrapper.sh

# Plugin Arch gRPC Server Port.
EXPOSE 6565

# gRPC Gateway Port.
EXPOSE 8000

# Prometheus /metrics Web Server Port.
EXPOSE 8080

# Entrypoint.
CMD ["sh", "/app/wrapper.sh"]
