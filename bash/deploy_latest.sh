#!/bin/bash


git pull
dotnet build
systemctl restart dnproto.threddyrex.net.service
