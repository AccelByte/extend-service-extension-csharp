#!/usr/bin/env bash

# Prerequisites: bash, curl, go, jq

set -e
set -o pipefail
#set -x

APP_BASE_URL=http://localhost:8000
APP_BASE_PATH="service"

function clean_up()
{
  kill -9 $GATEWAY_PID $SERVICE_PID
}

trap clean_up EXIT

echo '# Build and run Extend app locally'

(cd gateway && BASE_PATH=/$APP_BASE_PATH go run main.go) & GATEWAY_PID=$!
(cd src/AccelByte.Extend.ServiceExtension.Server && dotnet run) & SERVICE_PID=$!

(for _ in {1..12}; do curl http://localhost:8000 >/dev/null 2>&1 && exit 0 || sleep 10s; done; exit 1)
(for _ in {1..12}; do curl http://localhost:8080 >/dev/null 2>&1 && exit 0 || sleep 10s; done; exit 1)

if [ $? -ne 0 ]; then
  echo "Failed to run Extend app locally"
  exit 1
fi

echo '# Testing Extend app using demo script'

export SERVICE_BASE_URL=$APP_BASE_URL
export SERVICE_BASE_PATH=$APP_BASE_PATH

bash demo.sh
