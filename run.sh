#!/usr/bin/env bash
DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" >/dev/null 2>&1 && pwd )"

logname="${DIR}/logs/$(date +"%Y-%m-%d_%Hh%Mm%Ss").log"
echo "Logging to $logname"

dotnet run --project "$DIR/src/BerBo-T" 2>&1 | tee $logname
