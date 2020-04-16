#!/usr/bin/env bash
temp=$(find -type f -printf '%T+ %p\n' | sort -r | head -1)
latestLog=${temp##* }
cat $latestLog

