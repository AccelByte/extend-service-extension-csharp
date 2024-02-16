# Copyright (c) 2022-2023 AccelByte Inc. All Rights Reserved.
# This is licensed software from AccelByte Inc, for limitations
# and restrictions contact your company contract manager.

SHELL := /bin/bash

IMAGE_NAME := $(shell basename "$$(pwd)")-app
DOTNETVER := 6.0.302
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
		-e GOCACHE=/tmp/cache \
		-v $$(pwd)/gateway:/data \
		-w /data/ golang:1.20 \
		go mod tidy

build: gen-gateway mod-gateway
	docker run --rm -u $$(id -u):$$(id -g) \
		-v $$(pwd):/data/ \
		-e HOME="/data/.testrun" -e DOTNET_CLI_HOME="/data/.testrun" \
		mcr.microsoft.com/dotnet/sdk:$(DOTNETVER) \
		sh -c "mkdir -p /data/.testrun && cp -r /data/src /data/.testrun/src && cd /data/.testrun/src && dotnet build && mkdir -p /data/.output && cp -r /data/.testrun/src/AccelByte.PluginArch.ServiceExtension.Demo.Server/bin/* /data/.output/ && rm -rf /data/.testrun"

image-service:
	docker build -f Dockerfile.service -t ${IMAGE_NAME}-service .

image-gateway:
	docker build -f Dockerfile.gateway -t ${IMAGE_NAME}-gateway .

image:
	docker build -t ${IMAGE_NAME} .

imagex:
	docker buildx inspect ${IMAGE_NAME}-builder \
			|| docker buildx create --name ${IMAGE_NAME}-builder --use 
	docker buildx build -t ${IMAGE_NAME} --platform linux/arm64,linux/amd64 .
	docker buildx build -t ${IMAGE_NAME} --load .
	#docker buildx rm ${IMAGE_NAME}-builder

imagex_push:
	@test -n "$(IMAGE_TAG)" || (echo "IMAGE_TAG is not set (e.g. 'v0.1.0', 'latest')"; exit 1)
	@test -n "$(REPO_URL)" || (echo "REPO_URL is not set"; exit 1)
	docker buildx inspect $(BUILDER) || docker buildx create --name $(BUILDER) --use
	docker buildx build -t ${REPO_URL}:${IMAGE_TAG} --platform linux/arm64,linux/amd64 --push .
	docker buildx rm --keep-state $(BUILDER)

test:
	@test -n "$(ENV_FILE_PATH)" || (echo "ENV_FILE_PATH is not set" ; exit 1)
	docker run --rm -u $$(id -u):$$(id -g) \
		-v $$(pwd):/data/ \
		-e HOME="/data/.testrun" -e DOTNET_CLI_HOME="/data/.testrun" \
		--env-file $(ENV_FILE_PATH) \
		mcr.microsoft.com/dotnet/sdk:$(DOTNETVER) \
		sh -c "mkdir -p /data/.testrun && cp -r /data/src /data/.testrun/src && cd /data/.testrun/src && dotnet test && rm -rf /data/.testrun"

test_functional_local_hosted:
	@test -n "$(ENV_PATH)" || (echo "ENV_PATH is not set"; exit 1)
	docker build --tag service-extension-test-functional -f test/functional/Dockerfile test/functional && \
	docker run --rm -t \
		--env-file $(ENV_PATH) \
		-e GOCACHE=/data/.cache/go-build \
		-e GOPATH=/data/.cache/mod \
		-e DOTNET_CLI_HOME="/data" \
		-e XDG_DATA_HOME="/data" \
		-u $$(id -u):$$(id -g) \
		-v $$(pwd):/data \
		-w /data service-extension-test-functional bash ./test/functional/test-local-hosted.sh

test_functional_accelbyte_hosted:
	@test -n "$(ENV_PATH)" || (echo "ENV_PATH is not set"; exit 1)
	docker build --tag service-extension-test-functional -f test/functional/Dockerfile test/functional && \
	docker run --rm -t \
		--env-file $(ENV_PATH) \
		-e GOCACHE=/data/.cache/go-build \
		-e GOPATH=/data/.cache/mod \
		-e DOTNET_CLI_HOME="/data" \
		-e XDG_DATA_HOME="/data" \
		-e DOCKER_CONFIG=/tmp/.docker \
		-u $$(id -u):$$(id -g) \
		--group-add $$(getent group docker | cut -d ':' -f 3) \
		-v /var/run/docker.sock:/var/run/docker.sock \
		-v $$(pwd):/data \
		-w /data service-extension-test-functional bash ./test/functional/test-accelbyte-hosted.sh
