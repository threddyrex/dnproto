#!/bin/bash

# get seq from command line
../src/bin/Debug/net10.0/dnproto /command GetFirehoseEvent /datadir ../data/ /seq $1
    
