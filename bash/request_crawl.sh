#!/bin/bash



curl --fail --silent --show-error --request POST --header "Content-Type: application/json" --data "{\"hostname\": \"$1\"}" https://bsky.network/xrpc/com.atproto.sync.requestCrawl

