#!/bin/bash

SYSTEMCTL_SERVICE_NAME=$(sqlite3 ../data/pds/pds.db "SELECT Value FROM ConfigProperty WHERE Key='SystemctlServiceName';")
CADDY_ACCESS_LOG=$(sqlite3 ../data/pds/pds.db "SELECT Value FROM ConfigProperty WHERE Key='CaddyAccessLogFilePath';")


echo ""
echo "CONFIG FROM ../data/pds/pds.db:"
echo ""
echo "	SYSTEMCTL_SERVICE_NAME: $SYSTEMCTL_SERVICE_NAME"
echo "	CADDY_ACCESS_LOG: $CADDY_ACCESS_LOG"
echo ""


cd ..

echo "GIT PULL"
git pull
echo ""

echo "GIT GET REV"
git rev-parse --short HEAD > ./data/pds/code-rev.txt
echo ""

echo "DOTNET BUILD"
dotnet build
echo ""

echo "SYSTEMCTL RESTART"
systemctl restart $SYSTEMCTL_SERVICE_NAME
echo ""

cd bash/
