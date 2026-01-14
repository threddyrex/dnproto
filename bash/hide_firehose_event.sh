#!/bin/bash

# get seq from command line
../src/bin/Debug/net10.0/dnproto /command HideFirehoseEvent /datadir ../data/ /seq $1
    
