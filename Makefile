# Copyright (c) 2022-2025 AccelByte Inc. All Rights Reserved.
# This is licensed software from AccelByte Inc, for limitations
# and restrictions contact your company contract manager.

SHELL := /bin/bash

DOTNETVER := 6.0-jammy

.PHONY: build

proto:
	docker run -t --rm -u $$(id -u):$$(id -g) \
		-v $$(pwd):/build \
		-w /build \
		--entrypoint /bin/bash \
		rvolosatovs/protoc:4.1.0 \
			proto.sh

build: build_server build_gateway

build_server:
	rm -rf .output .tmp
	mkdir -p .output
	cp -r src .tmp/
	docker run -t --rm -u $$(id -u):$$(id -g) \
		-e HOME="/data/.cache" \
		-e DOTNET_CLI_HOME="/data/.cache" \
		-v $$(pwd):/data \
		-w /data/.tmp \
		mcr.microsoft.com/dotnet/sdk:$(DOTNETVER) \
		dotnet build
	cp -r .tmp/AccelByte.Extend.ServiceExtension.Server/bin/* \
			.output/


build_gateway: proto
	docker run -t --rm -u $$(id -u):$$(id -g) \
		-e GOCACHE=/data/.cache/go-cache \
		-e GOPATH=/data/.cache/go-path \
		-v $$(pwd):/data \
		-w /data/gateway \
		golang:1.24 \
		go build -modcacherw -o grpc_gateway

run_server:
	rm -rf .output .tmp
	mkdir -p .output
	cp -r src .tmp/
	docker run --rm -it -u $$(id -u):$$(id -g) \
		-e HOME="/data/.cache" \
		-e DOTNET_CLI_HOME="/data/.cache" \
		--env-file .env \
		-v $$(pwd):/data \
		-w /data/.tmp/AccelByte.Extend.ServiceExtension.Server \
		-p 6565:6565 \
		-p 8080:8080 \
		mcr.microsoft.com/dotnet/sdk:$(DOTNETVER) \
		dotnet run

run_gateway: proto
	docker run -it --rm -u $$(id -u):$$(id -g) \
		-e GOCACHE=/data/.cache/go-cache \
		-e GOPATH=/data/.cache/go-path \
		--env-file .env \
		-v $$(pwd):/data \
		-w /data/gateway \
		-p 8000:8000 \
		--add-host host.docker.internal:host-gateway \
		golang:1.24 \
		go run main.go --grpc-addr host.docker.internal:6565