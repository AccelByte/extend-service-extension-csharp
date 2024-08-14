#!/usr/bin/env bash
# Demo script to retrieve user access token for Extend Service Extension test.
# Requires: bash curl jq

set -e
set -o pipefail
#set -x

test -n "$AB_CLIENT_ID" || (echo "AB_CLIENT_ID is not set"; exit 1)
test -n "$AB_CLIENT_SECRET" || (echo "AB_CLIENT_SECRET is not set"; exit 1)
test -n "$AB_BASE_URL" || (echo "AB_BASE_URL is not set"; exit 1)

test -n "$1" || (echo "Username is required"; exit 1)
test -n "$2" || (echo "Password is required"; exit 1)

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

PLAYER_ACCESS_TOKEN="$(api_curl ${AB_BASE_URL}/iam/v3/oauth/token \
    -H 'Content-Type: application/x-www-form-urlencoded' -u "$AB_CLIENT_ID:$AB_CLIENT_SECRET" \
    -d "code=$CODE&grant_type=authorization_code&client_id=$AB_CLIENT_ID&code_verifier=$CODE_VERIFIER" | jq --raw-output .access_token)"

if [ "$PLAYER_ACCESS_TOKEN" == "null" ]; then
    cat api_curl_http_response.out
    exit 1
fi

echo $PLAYER_ACCESS_TOKEN
