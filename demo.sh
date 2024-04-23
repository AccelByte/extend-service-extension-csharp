#!/usr/bin/env bash

set -e
set -o pipefail
#set -x

test -n "$AB_CLIENT_ID" || (echo "AB_CLIENT_ID is not set"; exit 1)
test -n "$AB_CLIENT_SECRET" || (echo "AB_CLIENT_SECRET is not set"; exit 1)
test -n "$AB_NAMESPACE" || (echo "AB_NAMESPACE is not set"; exit 1)

SERVICE_BASE_URL="${SERVICE_BASE_URL:-http://localhost:8000}"
SERVICE_BASE_PATH="${SERVICE_BASE_PATH:-service}"

GUILD_ID='63d5802dd554c87f4e0b0707ae2f0af44c6f7d08f1ff3dec21a02728b10476e4'

get_code_verifier() 
{
  echo $RANDOM | sha256sum | cut -d ' ' -f 1   # For testing only: In reality, it needs to be secure random
}

get_code_challenge()
{
  echo -n "$1" | sha256sum | xxd -r -p | base64 | tr -d '\n' | sed -E -e 's/\+/-/g' -e 's/\//\_/g' -e 's/=//g'
}

api_curl()
{
  curl -s -D api_curl_http_header.out -o api_curl_http_response.out -w '%{http_code}' "$@" > api_curl_http_code.out
  echo >> api_curl_http_response.out
  cat api_curl_http_response.out
}

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

echo 'Updating guild progression ...'

api_curl -X 'POST' \
  "${SERVICE_BASE_URL}/${SERVICE_BASE_PATH}/v1/admin/namespace/$AB_NAMESPACE/progress" \
  -H "Authorization: Bearer $ACCESS_TOKEN" \
  -H 'accept: application/json' \
  -H 'Content-Type: application/json' \
  -d "{\"guildProgress\":{\"guildId\":\"$GUILD_ID\",\"namespace\":\"$AB_NAMESPACE\",\"objectives\":{\"additionalProp1\":0,\"additionalProp2\":0,\"additionalProp3\":0}}}"
echo
echo

if [ "$(cat api_curl_http_code.out)" -ge "400" ]; then
  exit 1
fi

echo 'Getting guild progression ...'

curl -X 'GET' \
  "${SERVICE_BASE_URL}/${SERVICE_BASE_PATH}/v1/admin/namespace/$AB_NAMESPACE/progress/$GUILD_ID" \
  -H "Authorization: Bearer $ACCESS_TOKEN" \
  -H 'accept: application/json'
echo
echo

if [ "$(cat api_curl_http_code.out)" -ge "400" ]; then
  exit 1
fi
