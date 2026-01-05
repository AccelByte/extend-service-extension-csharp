#!/bin/bash

set -eou pipefail

shopt -s globstar

PROTO_DIR="${1:-src/AccelByte.Extend.ServiceExtension.Server/Protos}"
GATEWAY_DIR="${2:-gateway/pkg/pb}"
APIDOCS_DIR="${3:-gateway/apidocs}"

# Clean previously generated files.
rm -rf "${GATEWAY_DIR}"/* && \
  mkdir -p "${GATEWAY_DIR}"

# Generate gateway code.
protoc \
  -I"${PROTO_DIR}" \
  --go_out="${GATEWAY_DIR}" \
  --go_opt=paths=source_relative \
  --go-grpc_out=require_unimplemented_servers=false:"${GATEWAY_DIR}" \
  --go-grpc_opt=paths=source_relative \
  --grpc-gateway_out=logtostderr=true:"${GATEWAY_DIR}" \
  --grpc-gateway_opt paths=source_relative \
  "${PROTO_DIR}"/*.proto

# Clean previously generated files.
rm -rf "${APIDOCS_DIR}"/* && \
  mkdir -p "${APIDOCS_DIR}"

# Generate swagger.json file.
protoc \
  -I"${PROTO_DIR}" \
  --openapiv2_out "${APIDOCS_DIR}" \
  --openapiv2_opt logtostderr=true \
  --openapiv2_opt use_go_templates=true \
  "${PROTO_DIR}"/*.proto
