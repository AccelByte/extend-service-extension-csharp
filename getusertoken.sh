#!/usr/bin/env bash
# Demo script to retrieve user access token for Extend Service Extension test.
# Requires: bash curl jq

set -e
set -o pipefail

test -n "$AB_CLIENT_ID" || (echo "AB_CLIENT_ID is not set"; exit 1)
test -n "$AB_CLIENT_SECRET" || (echo "AB_CLIENT_SECRET is not set"; exit 1)
test -n "$AB_BASE_URL" || (echo "AB_BASE_URL is not set"; exit 1)

test -n "$1" || (echo "username is required"; exit 1)
test -n "$2" || (echo "password is required"; exit 1)

get_code_verifier() 
{
    echo $RANDOM | sha256sum | cut -d ' ' -f 1   # For demo only: In reality, it needs to be secure random
}

get_code_challenge()
{
    echo -n "$1" | sha256sum | xxd -r -p | base64 | tr -d '\n' | sed -e 's/\+/-/g' -e 's/\//\_/g' -e 's/=//g'
}

function api_curl()
{
    curl -s -o http_response.out -w '%{http_code}' "$@" > http_code.out
    echo >> http_response.out
    cat http_response.out
}

CODE_VERIFIER="$(get_code_verifier)"
REQUEST_ID="$(curl -s -D - "${AB_BASE_URL}/iam/v3/oauth/authorize?response_type=code&code_challenge_method=S256&code_challenge=$(get_code_challenge "$CODE_VERIFIER")&client_id=$AB_CLIENT_ID" | grep -o 'request_id=[a-f0-9]\+' | cut -d= -f2)"

CODE_DATA="--data-urlencode password="$2" --data-urlencode request_id="$REQUEST_ID" --data-urlencode client_id="$AB_CLIENT_ID" --data-urlencode user_name="$1
CODE="$(curl -s -D - $AB_BASE_URL/iam/v3/authenticate -H 'Content-Type: application/x-www-form-urlencoded' $CODE_DATA | grep -o 'code=[a-f0-9]\+' | cut -d= -f2)"

PLAYER_ACCESS_TOKEN="$(api_curl ${AB_BASE_URL}/iam/v3/oauth/token -H 'Content-Type: application/x-www-form-urlencoded' -u "$AB_CLIENT_ID:$AB_CLIENT_SECRET" -d "code=$CODE&grant_type=authorization_code&client_id=$AB_CLIENT_ID&code_verifier=$CODE_VERIFIER" | jq --raw-output .access_token)"

if [ "$PLAYER_ACCESS_TOKEN" == "null" ]; then
    cat http_response.out
    rm http_response.out http_code.out
    exit 1
fi

rm http_response.out http_code.out
echo $PLAYER_ACCESS_TOKEN
