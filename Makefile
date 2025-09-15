# Copyright (c) 2022-2025 AccelByte Inc. All Rights Reserved.
# This is licensed software from AccelByte Inc, for limitations
# and restrictions contact your company contract manager.

SHELL := /bin/bash

PROJECT_NAME := $(shell basename "$$(pwd)")
DOTNET_IMAGE := mcr.microsoft.com/dotnet/sdk:8.0-jammy
GOLANG_IMAGE := golang:1.24-alpine3.21
PROTOC_IMAGE := rvolosatovs/protoc:4.1.0

BUILD_CACHE_VOLUME := $(shell echo '$(PROJECT_NAME)' | sed 's/[^a-zA-Z0-9_-]//g')-build-cache

.PHONY: build

build: build_server build_gateway

proto:
	docker run -t --rm \
		-u $$(id -u):$$(id -g) \
		-v $$(pwd):/data \
		-w /data \
		--entrypoint /bin/bash \
		${PROTOC_IMAGE} \
		proto.sh

build_server:
	rm -rf .output .tmp
	mkdir -p .output
	cp -r src .tmp/
	docker run -t --rm -u $$(id -u):$$(id -g) \
		-e HOME="/tmp/build-cache/dotnet/cache" \
		-e DOTNET_CLI_HOME="/tmp/build-cache/dotnet/cache" \
		-e DOTNET_SKIP_WORKLOAD_INTEGRITY_CHECK=1 \
		-v $$(pwd):/data \
		-v $(BUILD_CACHE_VOLUME):/tmp/build-cache \
		-w /data/.tmp \
		${DOTNET_IMAGE} \
		dotnet build
	cp -r .tmp/AccelByte.Extend.ServiceExtension.Server/bin/* \
			.output/


build_gateway: proto
	docker run -t --rm \
			-v $(BUILD_CACHE_VOLUME):/tmp/build-cache \
			$(GOLANG_IMAGE) \
			chown $$(id -u):$$(id -g) /tmp/build-cache		# Fix /tmp/build-cache folder owned by root
	docker run -t --rm -u $$(id -u):$$(id -g) \
			-e GOCACHE=/tmp/build-cache/go/cache \
			-e GOMODCACHE=/tmp/build-cache/go/modcache \
			-v $(BUILD_CACHE_VOLUME):/tmp/build-cache \
			-v $$(pwd):/data \
			-w /data/gateway \
			${GOLANG_IMAGE} \
			go build -modcacherw -o grpc_gateway

run_server:
	rm -rf .output .tmp
	mkdir -p .output
	cp -r src .tmp/
	docker run --rm -it -u $$(id -u):$$(id -g) \
		-e HOME="/tmp/build-cache/dotnet/cache" \
		-e DOTNET_CLI_HOME="/tmp/build-cache/dotnet/cache" \
		-e DOTNET_SKIP_WORKLOAD_INTEGRITY_CHECK=1 \
		--env-file .env \
		-v $(BUILD_CACHE_VOLUME):/tmp/build-cache \
		-v $$(pwd):/data \
		-w /data/.tmp/AccelByte.Extend.ServiceExtension.Server \
		-p 6565:6565 \
		-p 8080:8080 \
		${DOTNET_IMAGE} \
		dotnet run

run_gateway: proto
	docker run -t --rm \
			-v $(BUILD_CACHE_VOLUME):/tmp/build-cache \
			$(GOLANG_IMAGE) \
			chown $$(id -u):$$(id -g) /tmp/build-cache		# Fix /tmp/build-cache folder owned by root
	docker run -it --rm -u $$(id -u):$$(id -g) \
			-e GOCACHE=/tmp/build-cache/go/cache \
			-e GOMODCACHE=/tmp/build-cache/go/modcache \
			--env-file .env \
			-v $(BUILD_CACHE_VOLUME):/tmp/build-cache \
			-v $$(pwd):/data \
			-w /data/gateway \
			-p 8000:8000 \
			--add-host host.docker.internal:host-gateway \
			${GOLANG_IMAGE} \
			go run main.go --grpc-addr host.docker.internal:6565