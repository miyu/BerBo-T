#!/usr/bin/env bash
echo "Based on parsing latest log (DB audit table will be more correct if long-running)"

echo "Not New Contributor"
./catLatest.sh | grep "IsNewContributor" | grep -e "IsNewContributor" | grep False | wc -l

echo "New Contributor"
./catLatest.sh | grep "IsNewContributor" | grep -e "IsNewContributor" | grep True | wc -l

