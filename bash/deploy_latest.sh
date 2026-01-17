#!/bin/bash

source ../data/pds/setup.env

echo ""
echo "CONFIG FROM ../data/pds/setup.env:"
echo ""
echo "	PDS_HOST_NAME: $PDS_HOST_NAME"
echo "	AVAILABLE_USER_DOMAIN: $AVAILABLE_USER_DOMAIN"
echo "	USER_HANDLE: $USER_HANDLE"
echo "	USER_DID: $USER_DID"
echo "	USER_EMAIL: $USER_EMAIL"
echo "	SYSTEMCTL_SERVICE_NAME: $SYSTEMCTL_SERVICE_NAME"
echo "	CADDY_ACCESS_LOG: $CADDY_ACCESS_LOG"
echo ""

cd ..

echo "GIT PULL"
git pull
echo ""

echo "GIT GET REV"
git rev-parse --short HEAD > .\data\pds\code-rev.txt
echo ""

echo "DOTNET BUILD"
dotnet build
echo ""

echo "SYSTEMCTL RESTART"
systemctl restart $SYSTEMCTL_SERVICE_NAME
echo ""

cd bash/
