#!/bin/bash

./AccelByte.Extend.ServiceExtension.Server &

./grpc-gateway &

wait -n

exit $?

# https://docs.docker.com/config/containers/multi-service_container/#use-a-wrapper-script