# Copyright (c) 2022-2023 AccelByte Inc. All Rights Reserved.
# This is licensed software from AccelByte Inc, for limitations
# and restrictions contact your company contract manager.

SHELL := /bin/bash

IMAGE_NAME := $(shell basename "$$(pwd)")-app
DOTNETVER := 6.0
BUILDER := grpc-plugin-server-builder

.PHONY: build image imagex test

gen-gateway:
	rm -rfv gateway/pkg/pb/*
	mkdir -p gateway/pkg/pb
	docker run -t --rm -u $$(id -u):$$(id -g) \
		-v $$(pwd)/src:/source \
		-v $$(pwd)/gateway:/data \
		-w /data/ rvolosatovs/protoc:latest \
			--proto_path=/source/AccelByte.PluginArch.ServiceExtension.Demo.Server/Protos \
			--go_out=pkg/pb \
			--go_opt=paths=source_relative \
			--go-grpc_out=require_unimplemented_servers=false:pkg/pb \
			--grpc-gateway_out=logtostderr=true:pkg/pb \
			--grpc-gateway_opt paths=source_relative \
			--openapiv2_out . \
			--openapiv2_opt logtostderr=true \
			--openapiv2_opt use_go_templates=true \
			--go-grpc_opt=paths=source_relative /source/AccelByte.PluginArch.ServiceExtension.Demo.Server/Protos/*.proto

mod-gateway:
	docker run -t --rm -u $$(id -u):$$(id -g) \
		-e GOCACHE=/data/.cache/go-cache \
		-v $$(pwd):/data \
		-w /data/gateway golang:1.20-alpine3.19 \
		go mod tidy

build: gen-gateway mod-gateway
	docker run --rm -u $$(id -u):$$(id -g) \
		-v $$(pwd):/data/ \
		-e HOME="/data/.cache" -e DOTNET_CLI_HOME="/data/.cache" \
		mcr.microsoft.com/dotnet/sdk:$(DOTNETVER) \
		sh -c "mkdir -p /data/.tmp && cp -r /data/src /data/.tmp/src && cd /data/.tmp/src && dotnet build && mkdir -p /data/.output && cp -r /data/.tmp/src/AccelByte.PluginArch.ServiceExtension.Demo.Server/bin/* /data/.output/ && rm -rf /data/.tmp"

image-service:
	docker build -f Dockerfile.service -t ${IMAGE_NAME}-service .

image-gateway:
	docker build -f Dockerfile.gateway -t ${IMAGE_NAME}-gateway .

image:
	docker build -t ${IMAGE_NAME} .

imagex:
	docker buildx inspect $(BUILDER) || docker buildx create --name $(BUILDER) --use
	docker buildx build -t ${IMAGE_NAME} --platform linux/arm64/v8,linux/amd64 .
	docker buildx build -t ${IMAGE_NAME} --load .
	docker buildx rm --keep-state $(BUILDER)

imagex_push:
	@test -n "$(IMAGE_TAG)" || (echo "IMAGE_TAG is not set (e.g. 'v0.1.0', 'latest')"; exit 1)
	@test -n "$(REPO_URL)" || (echo "REPO_URL is not set"; exit 1)
	docker buildx inspect $(BUILDER) || docker buildx create --name $(BUILDER) --use
	docker buildx build -t ${REPO_URL}:${IMAGE_TAG} --platform linux/arm64/v8,linux/amd64 --push .
	docker buildx rm --keep-state $(BUILDER)

test:
	@test -n "$(ENV_FILE_PATH)" || (echo "ENV_FILE_PATH is not set" ; exit 1)
	docker run --rm -u $$(id -u):$$(id -g) \
		-v $$(pwd):/data/ \
		-e HOME="/data/.cache" -e DOTNET_CLI_HOME="/data/.cache" \
		--env-file $(ENV_FILE_PATH) \
		mcr.microsoft.com/dotnet/sdk:$(DOTNETVER) \
		sh -c "mkdir -p /data/.tmp && cp -r /data/src /data/.tmp/src && cd /data/.tmp/src && dotnet test && rm -rf /data/.tmp"

test_functional_local_hosted:
	@test -n "$(ENV_PATH)" || (echo "ENV_PATH is not set"; exit 1)
	docker build --tag service-extension-test-functional -f test/functional/Dockerfile test/functional && \
	docker run --rm -t \
		--env-file $(ENV_PATH) \
		-e GOCACHE=/data/.cache/go-cache \
		-e GOPATH=/data/.cache/go-path \
		-e DOTNET_CLI_HOME="/data/.cache" \
		-e XDG_DATA_HOME="/data/.cache" \
		-u $$(id -u):$$(id -g) \
		-v $$(pwd):/data \
		-w /data service-extension-test-functional bash ./test/functional/test-local-hosted.sh

test_functional_accelbyte_hosted:
	@test -n "$(ENV_PATH)" || (echo "ENV_PATH is not set"; exit 1)
ifeq ($(shell uname), Linux)
	$(eval DARGS := -u $$(shell id -u):$$(shell id -g) --group-add $$(shell getent group docker | cut -d ':' -f 3))
endif
	docker build --tag service-extension-test-functional -f test/functional/Dockerfile test/functional && \
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
		-w /data service-extension-test-functional bash ./test/functional/test-accelbyte-hosted.sh
