#!/usr/bin/env bash

# Prerequisites: bash, curl, go, jq

set -e
set -o pipefail
#set -x

APP_NAME=int-ese

get_code_verifier() 
{
  echo $RANDOM | sha256sum | cut -d ' ' -f 1   # For testing only: In reality, it needs to be secure random
}

get_code_challenge()
{
  echo -n "$1" | sha256sum | xxd -r -p | base64 | tr -d '\n' | sed -e 's/+/-/g' -e 's/\//\_/g' -e 's/=//g'
}

api_curl()
{
  curl -s -D api_curl_http_header.out -o api_curl_http_response.out -w '%{http_code}' "$@" > api_curl_http_code.out
  echo >> api_curl_http_response.out
  cat api_curl_http_response.out
}

function clean_up()
{
    echo Deleting Extend app ...

    api_curl -X DELETE "${AB_BASE_URL}/csm/v1/admin/namespaces/$AB_NAMESPACE/apps/$APP_NAME" \
        -H "Authorization: Bearer $ACCESS_TOKEN" || true      # Ignore delete error

    echo Delete OAuth client

    OAUTH_CLIENT_LIST=$(api_curl "${AB_BASE_URL}/iam/v3/admin/namespaces/$AB_NAMESPACE/clients?clientName=extend-$APP_NAME&limit=20" \
        -H "Authorization: Bearer $ACCESS_TOKEN")

    OAUTH_CLIENT_LIST_COUNT=$(echo "$OAUTH_CLIENT_LIST" | jq '.data | length')

    if [ "$OAUTH_CLIENT_LIST_COUNT" -eq 0 ] || [ "$OAUTH_CLIENT_LIST_COUNT" -gt 1 ]; then
        echo "Failed to to clean up OAuth client (name: extend-$APP_NAME)"
        exit 1
    fi

    OAUTH_CLIENT_ID="$(echo "$OAUTH_CLIENT_LIST" | jq -r '.data[0].clientId')"

    api_curl "${AB_BASE_URL}/iam/v3/admin/namespaces/$AB_NAMESPACE/clients/$OAUTH_CLIENT_ID" \
        -X 'DELETE' \
        -H "Authorization: Bearer $ACCESS_TOKEN"
}

APP_NAME="${APP_NAME}-$(echo $RANDOM | sha1sum | head -c 8)"   # Add random suffix to make it easy to clean up

echo '# Downloading extend-helper-cli'

case "$(uname -s)" in
    Darwin*)
      curl -sL --output extend-helper-cli https://github.com/AccelByte/extend-helper-cli/releases/latest/download/extend-helper-cli-darwin_amd64
        ;;
    *)
      curl -sL --output extend-helper-cli https://github.com/AccelByte/extend-helper-cli/releases/latest/download/extend-helper-cli-linux_amd64
        ;;
esac

chmod +x ./extend-helper-cli

echo '# Preparing test environment'

echo 'Logging in user ...'

CODE_VERIFIER="$(get_code_verifier)"

api_curl "${AB_BASE_URL}/iam/v3/oauth/authorize?scope=commerce+account+social+publishing+analytics&response_type=code&code_challenge_method=S256&code_challenge=$(get_code_challenge "$CODE_VERIFIER")&client_id=$AB_CLIENT_ID"

if [ "$(cat api_curl_http_code.out)" -ge "400" ]; then
  exit 1
fi

REQUEST_ID="$(cat api_curl_http_header.out | grep -o 'request_id=[a-f0-9]\+' | cut -d= -f2)"

api_curl ${AB_BASE_URL}/iam/v3/authenticate \
    -H 'Content-Type: application/x-www-form-urlencoded' \
    -d "user_name=${AB_USERNAME}&password=${AB_PASSWORD}&request_id=$REQUEST_ID&client_id=$AB_CLIENT_ID"

if [ "$(cat api_curl_http_code.out)" -ge "400" ]; then
  exit 1
fi

CODE="$(cat api_curl_http_header.out | grep -o 'code=[a-f0-9]\+' | cut -d= -f2)"

if [ "$CODE" == "null" ]; then
    cat api_curl_http_response.out
    exit 1
fi

ACCESS_TOKEN="$(api_curl ${AB_BASE_URL}/iam/v3/oauth/token \
    -H 'Content-Type: application/x-www-form-urlencoded' -u "$AB_CLIENT_ID:$AB_CLIENT_SECRET" \
    -d "code=$CODE&grant_type=authorization_code&client_id=$AB_CLIENT_ID&code_verifier=$CODE_VERIFIER" | jq --raw-output .access_token)"

if [ "$ACCESS_TOKEN" == "null" ]; then
    cat api_curl_http_response.out
    exit 1
fi

echo 'Creating Extend app ...'

api_curl "${AB_BASE_URL}/csm/v1/admin/namespaces/${AB_NAMESPACE}/apps/$APP_NAME" \
  -X 'PUT' \
  -H "Authorization: Bearer $ACCESS_TOKEN" \
  -H 'content-type: application/json' \
  --data-raw '{"scenario":"service-extension","description":"Extend integration test"}'

if [ "$(cat api_curl_http_code.out)" -ge "400" ]; then
  exit 1
fi

trap clean_up EXIT

for _ in {1..60}; do
    STATUS=$(api_curl "${AB_BASE_URL}/csm/v1/admin/namespaces/${AB_NAMESPACE}/apps?limit=500&offset=0" \
            -H "Authorization: Bearer $ACCESS_TOKEN" \
            -H 'content-type: application/json' \
            --data-raw "{\"appNames\":[\"${APP_NAME}\"],\"statuses\":[],\"scenario\":\"service-extension\"}" \
            | jq -r '.data[0].status')
    if [ "$STATUS" = "S" ]; then
        break
    fi
    echo "Waiting until Extend app created (status: $STATUS)"
    sleep 10
done

if ! [ "$STATUS" = "S" ]; then
    echo "Failed to create Extend app (status: $STATUS)"
    exit 1
fi

echo '# Build and push Extend app'

APP_DETAILS=$(api_curl "${AB_BASE_URL}/csm/v1/admin/namespaces/${AB_NAMESPACE}/apps/$APP_NAME" \
  -H "Authorization: Bearer $ACCESS_TOKEN")

if [ "$(cat api_curl_http_code.out)" -ge "400" ]; then
  cat api_curl_http_response.out
  exit 1
fi

APP_REPO_URL=$(echo "$APP_DETAILS" | jq -r '.appRepoUrl')
APP_REPO_HOST=$(echo "$APP_REPO_URL" | cut -d/ -f1)
APP_BASE_PATH=$(echo "$APP_DETAILS" | jq -r '.basePath')

#./extend-helper-cli dockerlogin --namespace $AB_NAMESPACE --app $APP_NAME -p | docker login -u AWS --password-stdin $APP_REPO_HOST
./extend-helper-cli dockerlogin --namespace $AB_NAMESPACE --app $APP_NAME --login

#make imagex_push REPO_URL=$APP_REPO_URL IMAGE_TAG=v0.0.1
./extend-helper-cli image-upload --namespace $AB_NAMESPACE --app $APP_NAME --image-tag v0.0.1

echo "Deploying Extend app ..."

SECRETS_DATA=$(api_curl "${AB_BASE_URL}/csm/v1/admin/namespaces/${AB_NAMESPACE}/apps/$APP_NAME/secrets?limit=200&offset=0" \
        -H "Authorization: Bearer $ACCESS_TOKEN" \
        -H 'content-type: application/json')

CLIENT_ID_UUID=$(echo "$SECRETS_DATA" | jq -r '.data[] | select(.configName=="AB_CLIENT_ID") | .configId')
CLIENT_SECRET_UUID=$(echo "$SECRETS_DATA" | jq -r '.data[] | select(.configName=="AB_CLIENT_SECRET") | .configId')

api_curl -X PUT "${AB_BASE_URL}/csm/v1/admin/namespaces/${AB_NAMESPACE}/apps/$APP_NAME/secrets/$CLIENT_ID_UUID" \
        -H "Authorization: Bearer $ACCESS_TOKEN" \
        -H 'content-type: application/json' \
        --data-raw "{\"value\":\"$AB_CLIENT_ID\"}"

if [ "$(cat api_curl_http_code.out)" -ge "400" ]; then
  exit 1
fi

api_curl -X PUT "${AB_BASE_URL}/csm/v1/admin/namespaces/${AB_NAMESPACE}/apps/$APP_NAME/secrets/$CLIENT_SECRET_UUID" \
        -H "Authorization: Bearer $ACCESS_TOKEN" \
        -H 'content-type: application/json' \
        --data-raw "{\"value\":\"$AB_CLIENT_SECRET\"}"

if [ "$(cat api_curl_http_code.out)" -ge "400" ]; then
  exit 1
fi

api_curl "${AB_BASE_URL}/csm/v1/admin/namespaces/${AB_NAMESPACE}/apps/$APP_NAME/deployments" \
        -H "Authorization: Bearer $ACCESS_TOKEN" \
        -H 'content-type: application/json' \
        --data-raw '{"imageTag":"v0.0.1","description":""}'

if [ "$(cat api_curl_http_code.out)" -ge "400" ]; then
  exit 1
fi

for _ in {1..60}; do
    STATUS=$(api_curl "${AB_BASE_URL}/csm/v1/admin/namespaces/${AB_NAMESPACE}/apps?limit=500&offset=0" \
            -H "Authorization: Bearer $ACCESS_TOKEN" \
            -H 'content-type: application/json' \
            --data-raw "{\"appNames\":[\"${APP_NAME}\"],\"statuses\":[],\"scenario\":\"service-extension\"}" \
            | jq -r '.data[0].app_release_status')
    if [ "$STATUS" = "R" ]; then
        break
    fi
    echo "Waiting until Extend app deployed (status: $STATUS)"
    sleep 10
done

if ! [ "$STATUS" = "R" ]; then
    echo "Failed to deploy Extend app (status: $STATUS)"
    exit 1
fi

sleep 60

echo '# Testing Extend app using demo script'

export SERVICE_BASE_URL=$AB_BASE_URL
export SERVICE_BASE_PATH=$APP_BASE_PATH

bash demo.sh
