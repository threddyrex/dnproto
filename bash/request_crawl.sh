#!/bin/bash


source ../data/pds/setup.env

echo ""
echo "CONFIG FROM ../data/pds/setup.env:"
echo ""
echo "  PDS_HOST_NAME: $PDS_HOST_NAME"
echo "  AVAILABLE_USER_DOMAIN: $AVAILABLE_USER_DOMAIN"
echo "  USER_HANDLE: $USER_HANDLE"
echo "  USER_DID: $USER_DID"
echo "  USER_EMAIL: $USER_EMAIL"
echo "  SYSTEMCTL_SERVICE_NAME: $SYSTEMCTL_SERVICE_NAME"
echo ""




curl --fail --silent --show-error --request POST --header "Content-Type: application/json" --data "{\"hostname\": \"$PDS_HOST_NAME\"}" https://bsky.network/xrpc/com.atproto.sync.requestCrawl

