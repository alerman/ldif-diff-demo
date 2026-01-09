# LDIF Diff Demo

A C# tool for analyzing and comparing LDIF (LDAP Data Interchange Format) files.

## Features

- **Analysis Mode**: Parse a single LDIF file and generate detailed statistics
- **Comparison Mode**: Compare two LDIF files and validate if changes are acceptable
- **Unified Diff Mode**: Generate line-by-line diffs similar to Linux `diff -u`

## Usage

### Unified Diff Mode (New!)

Generate a traditional line-by-line diff output similar to Linux `diff -u`:

```bash
dotnet run --project LdifDiffDemo/LdifDiffDemo.csproj -- --diff <baseline.ldif> <new.ldif> <output.diff>
```

Or using the short form:

```bash
dotnet run --project LdifDiffDemo/LdifDiffDemo.csproj -- -d <baseline.ldif> <new.ldif> <output.diff>
```

**Example:**

```bash
dotnet run --project LdifDiffDemo/LdifDiffDemo.csproj -- --diff test-data/baseline_test.ldif test-data/modified_test.ldif output.diff
```

**Output Format:**

The unified diff shows:

1. **Line-by-line changes** for entries that exist in both files:
   - Context lines prefixed with ` ` (space)
   - Removed lines prefixed with `-`
   - Added lines prefixed with `+`

2. **Complete entries** for wholly added/removed entries:
   - Added entries: all lines prefixed with `+`
   - Removed entries: all lines prefixed with `-`

**Example Output:**

```diff
--- baseline.ldif
+++ modified.ldif

@@ Entry Modified: cn=User01,OU=Users,DC=example,DC=com @@
 dn: cn=User01,OU=Users,DC=example,DC=com
 changetype: add
 objectClass: inetOrgPerson
 cn: User01
-mail: user01@example.com
-telephoneNumber: +1 555 000001
+mail: user01.updated@example.com
+telephoneNumber: +1 555 999999

@@ Entry Added: cn=NewUser,OU=Users,DC=example,DC=com @@
+dn: cn=NewUser,OU=Users,DC=example,DC=com
+changetype: add
+objectClass: inetOrgPerson
+cn: NewUser
+mail: new.user@example.com
+

@@ Entry Removed: cn=OldUser,OU=Users,DC=example,DC=com @@
-dn: cn=OldUser,OU=Users,DC=example,DC=com
-changetype: delete
-
```

### Comparison Mode (Recommended)

Compare two LDIF files to determine if the new file is "good" based on similarity thresholds:

```bash
dotnet run --project LdifDiffDemo/LdifDiffDemo.csproj -- --compare <baseline.ldif> <new.ldif>
```

Or using the short form:

```bash
dotnet run --project LdifDiffDemo/LdifDiffDemo.csproj -- -c <baseline.ldif> <new.ldif>
```

**Example:**

```bash
dotnet run --project LdifDiffDemo/LdifDiffDemo.csproj -- --compare test-data/few_changes.ldif test-data/many_changes.ldif
```

**With Detailed Diff Output:**

To save a detailed entry-by-entry diff to a file:

```bash
# Human-readable text format
dotnet run --project LdifDiffDemo/LdifDiffDemo.csproj -- --compare test-data/few_changes.ldif test-data/many_changes.ldif --output diff.txt

# Machine-readable JSONL format (recommended for parsing)
dotnet run --project LdifDiffDemo/LdifDiffDemo.csproj -- --compare test-data/few_changes.ldif test-data/many_changes.ldif --output diff.jsonl
```

The detailed diff file shows:
- Which entries were added, removed, or modified
- What operations were performed on each entry
- Attribute-level changes with value counts

**JSONL Format**: Use `.jsonl` extension for machine-readable JSON Lines format, perfect for:
- Parsing with C# analyzer tool (or jq, etc.)
- Importing into databases or data analysis tools
- Processing large diff files efficiently

## Analyzing JSONL Diff Files

Use the `LdifDiffAnalyzer` tool to parse and analyze JSONL diff output:

```bash
# Summary report (default)
dotnet run --project LdifDiffAnalyzer/LdifDiffAnalyzer.csproj -- diff.jsonl

# Show all reports
dotnet run --project LdifDiffAnalyzer/LdifDiffAnalyzer.csproj -- diff.jsonl --all

# Find entries matching pattern
dotnet run --project LdifDiffAnalyzer/LdifDiffAnalyzer.csproj -- diff.jsonl --find "user01"

# Export to CSV
dotnet run --project LdifDiffAnalyzer/LdifDiffAnalyzer.csproj -- diff.jsonl --csv output.csv
```

## Generating Test Files

Regenerate large test LDIF files using the C# generator:

```bash
dotnet run --project LdifTestGenerator/LdifTestGenerator.csproj
```

**Exit Codes:**
- `0`: Files are similar (within thresholds)
- `1`: Files differ significantly
- `2`: Error occurred during processing

### Analysis Mode

Parse a single LDIF file and output detailed change records to JSONL:

```bash
dotnet run --project LdifDiffDemo/LdifDiffDemo.csproj -- <input.ldif> [output.jsonl]
```

If no output file is specified, it defaults to the input filename with `.jsonl` extension.

## Comparison Thresholds

The comparison mode uses the following default thresholds:

- **Entity count**: ±10% deviation allowed
- **Total attributes**: ±10% deviation allowed
- **Average attributes per entry**: ±15% deviation allowed
- **Individual attribute changes**: Reports changes >20%

A file is considered "good" if all metrics are within these thresholds.

## What Gets Compared

The comparison mode analyzes:

1. **Total entries**: Number of operations (add, modify, delete, moddn)
2. **Total attributes**: Sum of all attribute values across all operations
3. **Average attributes per entry**: Total attributes divided by total entries
4. **Attribute distribution**: Per-attribute value counts and changes

## Example Output

### Good Comparison (Files are Similar)

```
=== LDIF Comparison Report ===

✓ New LDIF file appears good - within acceptable thresholds

Baseline Statistics:
  Total entries: 3
  Total attributes: 7
  Average attributes per entry: 2.33

New File Statistics:
  Total entries: 3
  Total attributes: 7
  Average attributes per entry: 2.33

=== End of Report ===
```

### Bad Comparison (Significant Differences)

```
=== LDIF Comparison Report ===

✗ New LDIF file has significant differences from baseline

Detected Differences:
Entity count difference: +566.7% (baseline: 3, new: 20)
Total attribute count difference: +900.0% (baseline: 7, new: 70)
Average attributes per entry difference: +50.0% (baseline: 2.33, new: 3.50)
Attribute distribution changes:
  ~ mail: +900.0% (2 → 20)
  ~ telephoneNumber: +900.0% (1 → 10)
  ...

Entry-level differences: 13 entries changed
(Use --output flag to save detailed differences to a file)

=== End of Report ===
```

### Detailed Diff File Output

When using `--output diff.txt`, you get an entry-by-entry breakdown:

```
=== Detailed Entry-Level Differences ===

[REMOVED] cn=Alice Smith,OU=Users,DC=example,DC=com
  Baseline: Add operation(s), 5 attribute(s)
  Attributes:
    - objectClass: 1 value(s)
    - cn: 1 value(s)
    - givenName: 1 value(s)
    - mail: 1 value(s)
    - sn: 1 value(s)

[ADDED] cn=User01,OU=Users,DC=example,DC=com
  New:      Add, Modify operation(s), 7 attribute(s)
  Attributes:
    + objectClass: 1 value(s)
    + cn: 1 value(s)
    + mail: 2 value(s)
    ...

Total differences: 13 entries
```

## Building

```bash
dotnet build LdifDiffDemo.sln
```

## Testing

The project includes comprehensive unit tests covering all comparison logic:

```bash
# Run all tests
dotnet test LdifDiffDemo.sln

# Run with detailed output
dotnet test LdifDiffDemo.sln --verbosity normal
```

Test coverage includes:
- Percentage calculation logic
- LDIF parsing for all operation types (add, modify, delete, moddn)
- Attribute distribution comparison
- Entry-level diff generation
- Threshold-based comparison validation

## Dependencies

- .NET 8.0 (main project)
- .NET 10.0 (test project)
- LdifHelper (NuGet package for LDIF parsing)
- xUnit (testing framework)
