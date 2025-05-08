# Copyright (c) 2022-2023 AccelByte Inc. All Rights Reserved.
# This is licensed software from AccelByte Inc, for limitations
# and restrictions contact your company contract manager.

SHELL := /bin/bash

IMAGE_NAME := $(shell basename "$$(pwd)")-app
BUILDER := extend-builder

DOTNETVER := 6.0-jammy

TEST_SAMPLE_CONTAINER_NAME := sample-service-extension-test

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