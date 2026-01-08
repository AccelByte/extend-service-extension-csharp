# Copyright (c) 2025 AccelByte Inc. All Rights Reserved.
# This is licensed software from AccelByte Inc, for limitations
# and restrictions contact your company contract manager.

SHELL := /bin/bash

PROJECT_NAME := $(shell basename "$$(pwd)")
DOTNET_IMAGE := mcr.microsoft.com/dotnet/sdk:8.0-jammy
GOLANG_IMAGE := golang:1.24-alpine3.21
PROTOC_IMAGE := proto-builder

IS_INSIDE_DEVCONTAINER := $(REMOTE_CONTAINERS)
BUILD_CACHE_VOLUME := $(shell echo '$(PROJECT_NAME)' | sed 's/[^a-zA-Z0-9_-]//g')-build-cache

.PHONY: build

build: build_server build_gateway

proto_image:
ifneq ($(IS_INSIDE_DEVCONTAINER),true)
	docker build --target proto-builder -t $(PROTOC_IMAGE) .
endif

proto: proto_image
ifneq ($(IS_INSIDE_DEVCONTAINER),true)
	docker run --tty --rm --user $$(id -u):$$(id -g) \
		--volume $$(pwd):/build \
		--workdir /build \
		--entrypoint /bin/bash \
		$(PROTOC_IMAGE) \
		proto.sh
else
	./proto.sh
endif

build_server: prepare_build_cache proto
ifneq ($(IS_INSIDE_DEVCONTAINER),true)
	docker run -t --rm -u $$(id -u):$$(id -g) \
		-e HOME="/tmp/build-cache/dotnet/cache" \
		-e DOTNET_CLI_HOME="/tmp/build-cache/dotnet/cache" \
		-e DOTNET_SKIP_WORKLOAD_INTEGRITY_CHECK=1 \
		-v $$(pwd):/data \
		-v $(BUILD_CACHE_VOLUME):/tmp/build-cache \
		-w /data/src \
		${DOTNET_IMAGE} \
		dotnet build
else
	cd src && dotnet build
endif

build_gateway: prepare_build_cache proto
ifneq ($(IS_INSIDE_DEVCONTAINER),true)
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
else
	cd gateway && go build -modcacherw -o grpc_gateway
endif

run_server: prepare_build_cache
ifneq ($(IS_INSIDE_DEVCONTAINER),true)
	docker run --rm -it -u $$(id -u):$$(id -g) \
		-e HOME="/tmp/build-cache/dotnet/cache" \
		-e DOTNET_CLI_HOME="/tmp/build-cache/dotnet/cache" \
		-e DOTNET_SKIP_WORKLOAD_INTEGRITY_CHECK=1 \
		--env-file .env \
		-v $(BUILD_CACHE_VOLUME):/tmp/build-cache \
		-v $$(pwd):/data \
		-w /data/src/AccelByte.Extend.ServiceExtension.Server \
		-p 6565:6565 \
		-p 8080:8080 \
		${DOTNET_IMAGE} \
		dotnet run
else
	cd src/AccelByte.Extend.ServiceExtension.Server && dotnet run
endif

run_gateway: prepare_build_cache proto
ifneq ($(IS_INSIDE_DEVCONTAINER),true)
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
else
	cd gateway && go run main.go --grpc-addr localhost:6565
endif

prepare_build_cache:
ifneq ($(IS_INSIDE_DEVCONTAINER),true)
	docker run -t --rm \
			-v $(BUILD_CACHE_VOLUME):/tmp/build-cache \
			busybox:1.37.0 \
			chown $$(id -u):$$(id -g) /tmp/build-cache		# Fix /tmp/build-cache folder owned by root
endif
