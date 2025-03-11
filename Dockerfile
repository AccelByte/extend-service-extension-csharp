# gRPC gateway gen

FROM --platform=$BUILDPLATFORM rvolosatovs/protoc:4.1.0 AS grpc-gateway-gen
WORKDIR /build
COPY gateway gateway
COPY src src
COPY proto.sh .
RUN bash proto.sh

# gRPC server builder

FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:6.0-alpine3.19 AS grpc-server-builder
ARG TARGETARCH
RUN apk update && apk add --no-cache gcompat
WORKDIR /project
COPY src/AccelByte.Extend.ServiceExtension.Server/*.csproj .
RUN ([ "$TARGETARCH" = "amd64" ] && echo "linux-musl-x64" || echo "linux-musl-$TARGETARCH") > /tmp/dotnet-rid
RUN dotnet restore -r $(cat /tmp/dotnet-rid)
COPY src/AccelByte.Extend.ServiceExtension.Server .
RUN dotnet publish -c Release -r $(cat /tmp/dotnet-rid) --no-restore -o /build/

# gRPC gateway builder

FROM --platform=$BUILDPLATFORM golang:1.20-alpine3.19 AS grpc-gateway-builder
ARG TARGETARCH
WORKDIR /build
COPY gateway/go.mod gateway/go.sum .
RUN go mod download
COPY gateway/ .
RUN rm -rf pkg/pb
COPY --from=grpc-gateway-gen /build/gateway/pkg/pb pkg/pb
RUN GOARCH=$TARGETARCH go build -o grpc-gateway .

# Extend Service Extension app

FROM mcr.microsoft.com/dotnet/aspnet:6.0-alpine3.19
WORKDIR /app
COPY --from=grpc-gateway-builder /build/grpc-gateway .
COPY --from=grpc-gateway-gen /build/gateway/apidocs ./apidocs
RUN rm -fv apidocs/permission.swagger.json
COPY gateway/third_party ./third_party
COPY --from=grpc-server-builder /build/* .
COPY wrapper.sh .
# gRPC gateway HTTP port, gRPC server port, and /metrics HTTP port
EXPOSE 6565 8000 8080
CMD ["sh", "/app/wrapper.sh"]
