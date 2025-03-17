#!/bin/bash

kill_services()
{
    if [ "$SERVER_PID" ] && [ "$GATEWAY_PID" ]; then
        kill -TERM $SERVER_PID $GATEWAY_PID 2>/dev/null
    else
        KILL_SERVICES_ONCE_STARTED="yes"
    fi
}

trap 'kill_services' TERM INT

./AccelByte.Extend.ServiceExtension.Server & SERVER_PID=$!
./grpc-gateway & GATEWAY_PID=$!

if [ "$KILL_SERVICES_ONCE_STARTED" ]; then
    kill_services
fi

while true; do
    if kill -0 $SERVER_PID $GATEWAY_PID 2>/dev/null; then
        sleep 1s
    else
        kill_services
        break
    fi
done

wait $SERVER_PID
SERVER_EXIT_CODE=$?

wait $GATEWAY_PID
GATEWAY_EXIT_CODE=$?

echo Server exit code: $SERVER_EXIT_CODE
echo Gateway exit code: $GATEWAY_EXIT_CODE

if [ $SERVER_EXIT_CODE -gt 0 ]; then
    exit $SERVER_EXIT_CODE
else
    exit $GATEWAY_EXIT_CODE
fi
