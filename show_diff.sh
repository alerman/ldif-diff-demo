#!/bin/bash
# Simple script to show LDIF diffs in a readable format

jsonl_file="$1"

if [ -z "$jsonl_file" ]; then
    echo "Usage: $0 <diff.jsonl>"
    exit 1
fi

echo "Reading diffs from: $jsonl_file"
echo
echo "======================================================================"
echo

num=0
while IFS= read -r line; do
    num=$((num + 1))
    
    # Extract fields using jq
    changeType=$(echo "$line" | jq -r '.changeType')
    dn=$(echo "$line" | jq -r '.dn')
    baselineOps=$(echo "$line" | jq -r '.baselineOperations // [] | join(", ")')
    newOps=$(echo "$line" | jq -r '.newOperations // [] | join(", ")')
    baselineCount=$(echo "$line" | jq -r '.baselineAttrCount // "0"')
    newCount=$(echo "$line" | jq -r '.newAttrCount // "0"')
    
    # Determine indicator
    case "$changeType" in
        "Added") indicator="[+]" ;;
        "Removed") indicator="[-]" ;;
        "Modified") indicator="[~]" ;;
        *) indicator="[?]" ;;
    esac
    
    # Display
    echo "#$num $indicator $changeType"
    echo "DN: $dn"
    echo
    
    if [ -n "$baselineOps" ] && [ "$baselineOps" != "" ]; then
        echo "  BEFORE:"
        echo "    Operations: $baselineOps"
        echo "    Attributes: $baselineCount"
    fi
    
    if [ -n "$newOps" ] && [ "$newOps" != "" ]; then
        echo "  AFTER:"
        echo "    Operations: $newOps"
        echo "    Attributes: $newCount"
    fi
    
    # Show attribute changes
    attrChanges=$(echo "$line" | jq -r '.attributeDifferences // [] | length')
    if [ "$attrChanges" -gt 0 ]; then
        echo
        echo "  ATTRIBUTE CHANGES:"
        echo "$line" | jq -r '.attributeDifferences[] | "    \(.)"'
    fi
    
    echo
    
done < "$jsonl_file"
