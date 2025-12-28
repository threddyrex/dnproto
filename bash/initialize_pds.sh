#!/bin/bash


ource ../data/pds/setup.env

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




../src/bin/Debug/net10.0/dnproto /command InitializePds /datadir ../data/ /pdshostname $PDS_HOST_NAME /availableuserdomain $AVAILABLE_USER_DOMAIN /userhandle $USER_HANDLE /userdid $USER_DID /useremail $USER_EMAIL

