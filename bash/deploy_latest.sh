#!/bin/bash

source ../data/pds/setup.env

echo "PDS_HOST_NAME: $PDS_HOST_NAME"
echo "AVAILABLE_USER_DOMAINS: $AVAILABLE_USER_DOMAINS"
echo "USER_HANDLE: $USER_HANDLE"
echo "USER_DID: $USER_DID"
echo "USER_EMAIL: $USER_EMAIL"
echo "SYSTEMCTL_SERVICE_NAME: $SYSTEMCTL_SERVICE_NAME"

cd ..

git pull
dotnet build
systemctl restart $SYSTEMCTL_SERVICE_NAME

cd bash/
