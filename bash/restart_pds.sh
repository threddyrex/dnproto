#!/bin/bash


SYSTEMCTL_SERVICE_NAME=$(sqlite3 ../data/pds/pds.db "SELECT Value FROM ConfigProperty WHERE Key='SystemctlServiceName';")
CADDY_ACCESS_LOG=$(sqlite3 ../data/pds/pds.db "SELECT Value FROM ConfigProperty WHERE Key='CaddyAccessLogFilePath';")


echo ""
echo "CONFIG FROM ../data/pds/pds.db:"
echo ""
echo "	SYSTEMCTL_SERVICE_NAME: $SYSTEMCTL_SERVICE_NAME"
echo "	CADDY_ACCESS_LOG: $CADDY_ACCESS_LOG"
echo ""


echo "SYSTEMCTL RESTART"
systemctl restart $SYSTEMCTL_SERVICE_NAME
echo ""

