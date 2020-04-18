#!/usr/bin/env bash

bash './tailLatest.sh' | awk '/=== Begin Exception ===/,/=== End Exception ===/'

