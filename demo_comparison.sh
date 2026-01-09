#!/bin/bash
# Demo script showing all LDIF comparison modes

set -e

echo "=== LDIF Comparison Tool Demo ==="
echo ""

# Test files
BASELINE="test-data/baseline_test.ldif"
MODIFIED="test-data/modified_test.ldif"
DIFF_OUTPUT="demo_output.diff"
COMPARE_OUTPUT="demo_comparison.jsonl"

echo "Step 1: Generate Unified Diff (line-by-line changes)"
echo "Command: dotnet run --project LdifDiffDemo/LdifDiffDemo.csproj -- --diff $BASELINE $MODIFIED $DIFF_OUTPUT"
dotnet run --project LdifDiffDemo/LdifDiffDemo.csproj -- --diff "$BASELINE" "$MODIFIED" "$DIFF_OUTPUT"
echo ""

echo "Step 2: View the unified diff output"
echo "----------------------------------------"
cat "$DIFF_OUTPUT"
echo "----------------------------------------"
echo ""

echo "Step 3: Run comparison mode for validation"
echo "Command: dotnet run --project LdifDiffDemo/LdifDiffDemo.csproj -- --compare $BASELINE $MODIFIED --output $COMPARE_OUTPUT"
dotnet run --project LdifDiffDemo/LdifDiffDemo.csproj -- --compare "$BASELINE" "$MODIFIED" --output "$COMPARE_OUTPUT"
echo ""

echo "Step 4: Analyze the comparison data"
echo "Command: dotnet run --project LdifDiffAnalyzer/LdifDiffAnalyzer.csproj -- $COMPARE_OUTPUT"
dotnet run --project LdifDiffAnalyzer/LdifDiffAnalyzer.csproj -- "$COMPARE_OUTPUT"
echo ""

echo "=== Demo Complete ==="
echo ""
echo "Generated files:"
echo "  - $DIFF_OUTPUT (unified diff)"
echo "  - $COMPARE_OUTPUT (JSONL comparison data)"
echo ""
echo "You can now:"
echo "  1. View the diff: less $DIFF_OUTPUT"
echo "  2. Search for specific entries: grep 'User01' $DIFF_OUTPUT -A 10"
echo "  3. Analyze comparison data: dotnet run --project LdifDiffAnalyzer/LdifDiffAnalyzer.csproj -- $COMPARE_OUTPUT --all"
