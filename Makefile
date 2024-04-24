# Copyright (c) 2022-2023 AccelByte Inc. All Rights Reserved.
# This is licensed software from AccelByte Inc, for limitations
# and restrictions contact your company contract manager.

SHELL := /bin/bash

IMAGE_NAME := $(shell basename "$$(pwd)")-app
DOTNETVER := 6.0
BUILDER := grpc-plugin-server-builder

.PHONY: test

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
		golang:1.20-alpine3.19 \
		go build -o grpc_gateway

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
		golang:1.20-alpine3.19 \
		go run main.go --grpc-addr host.docker.internal:6565

image:
	docker build -t ${IMAGE_NAME} .

imagex:
	docker buildx inspect $(BUILDER) || docker buildx create --name $(BUILDER) --use
	docker buildx build -t ${IMAGE_NAME} --platform linux/amd64 .
	docker buildx build -t ${IMAGE_NAME} --load .
	docker buildx rm --keep-state $(BUILDER)

imagex_push:
	@test -n "$(IMAGE_TAG)" || (echo "IMAGE_TAG is not set (e.g. 'v0.1.0', 'latest')"; exit 1)
	@test -n "$(REPO_URL)" || (echo "REPO_URL is not set"; exit 1)
	docker buildx inspect $(BUILDER) || docker buildx create --name $(BUILDER) --use
	docker buildx build -t ${REPO_URL}:${IMAGE_TAG} --platform linux/amd64 --push .
	docker buildx rm --keep-state $(BUILDER)

test:
	@test -n "$(ENV_FILE_PATH)" || (echo "ENV_FILE_PATH is not set" ; exit 1)
	docker run --rm -u $$(id -u):$$(id -g) \
		-v $$(pwd):/data/ \
		-e HOME="/data/.cache" -e DOTNET_CLI_HOME="/data/.cache" \
		--env-file $(ENV_FILE_PATH) \
		mcr.microsoft.com/dotnet/sdk:$(DOTNETVER) \
		sh -c "mkdir -p /data/.tmp && cp -r /data/src /data/.tmp/src && cd /data/.tmp/src && dotnet test && rm -rf /data/.tmp"

test_sample_local_hosted:
	@test -n "$(ENV_PATH)" || (echo "ENV_PATH is not set"; exit 1)
	docker build --tag service-extension-test-functional -f test/sample/Dockerfile test/sample && \
	docker run --rm -t \
		--env-file $(ENV_PATH) \
		-e GOCACHE=/data/.cache/go-cache \
		-e GOPATH=/data/.cache/go-path \
		-e DOTNET_CLI_HOME="/data/.cache" \
		-e XDG_DATA_HOME="/data/.cache" \
		-u $$(id -u):$$(id -g) \
		-v $$(pwd):/data \
		-w /data service-extension-test-functional bash ./test/sample/test-local-hosted.sh

test_sample_accelbyte_hosted:
	@test -n "$(ENV_PATH)" || (echo "ENV_PATH is not set"; exit 1)
ifeq ($(shell uname), Linux)
	$(eval DARGS := -u $$(shell id -u) --group-add $$(shell getent group docker | cut -d ':' -f 3))
endif
	docker build --tag service-extension-test-functional -f test/sample/Dockerfile test/sample && \
	docker run --rm -t \
		--env-file $(ENV_PATH) \
		-e GOCACHE=/data/.cache/go-cache \
		-e GOPATH=/data/.cache/go-path \
		-e DOTNET_CLI_HOME="/data/.cache" \
		-e XDG_DATA_HOME="/data/.cache" \
		-e DOCKER_CONFIG="/tmp/.docker" \
		$(DARGS) \
		-v /var/run/docker.sock:/var/run/docker.sock \
		-v $$(pwd):/data \
		-w /data service-extension-test-functional bash ./test/sample/test-accelbyte-hosted.sh
