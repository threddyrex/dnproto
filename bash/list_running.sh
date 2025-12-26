#!/bin/bash


echo ""
echo "SYSTEMCTL LIST UNITS:"
echo ""
systemctl list-units --type=service | grep dnproto
echo ""


echo ""
echo "PS AUX | GREP DNPROTO:"
echo ""
ps aux | grep dnproto
echo ""

