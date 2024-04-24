#!/bin/bash

set -e

rm -rf gateway/apidocs gateway/pkg/pb
mkdir -p gateway/apidocs gateway/pkg/pb
protoc-wrapper -I/usr/include \
        --proto_path=src/AccelByte.Extend.ServiceExtension.Server/Protos \
        --go_out=gateway/pkg/pb \
        --go_opt=paths=source_relative \
        --go-grpc_out=require_unimplemented_servers=false:gateway/pkg/pb \
        --grpc-gateway_out=logtostderr=true:gateway/pkg/pb \
        --grpc-gateway_opt paths=source_relative \
        --openapiv2_out gateway/apidocs \
        --openapiv2_opt logtostderr=true \
        --openapiv2_opt use_go_templates=true \
        --go-grpc_opt=paths=source_relative src/AccelByte.Extend.ServiceExtension.Server/Protos/*.proto
