# LDIF Diff Example

This document shows how to read and display diffs from JSONL output.

## Step 1: Generate a JSONL Diff

```bash
dotnet run --project LdifDiffDemo/LdifDiffDemo.csproj -- \
  --compare test-data/few_changes.ldif test-data/many_changes.ldif \
  --output example-diff.jsonl
```

## Step 2: View the Raw JSONL

Each line in the JSONL file is a complete JSON object representing one diff entry:

```bash
head -3 example-diff.jsonl
```

**Output:**
```json
{"dn":"cn=Alice Smith,OU=Users,DC=example,DC=com","changeType":"Removed","baselineOperations":["Add"],"newOperations":null,"baselineAttrCount":5,"newAttrCount":null,"attributeDifferences":["  - objectClass: 1 value(s)","  - cn: 1 value(s)","  - givenName: 1 value(s)","  - mail: 1 value(s)","  - sn: 1 value(s)"]}
{"dn":"cn=Bob Jones,OU=Users,DC=example,DC=com","changeType":"Removed","baselineOperations":["Modify"],"newOperations":null,"baselineAttrCount":2,"newAttrCount":null,"attributeDifferences":["  - mail: 1 value(s)","  - telephoneNumber: 1 value(s)"]}
{"dn":"cn=Carol White,OU=Users,DC=example,DC=com","changeType":"Removed","baselineOperations":["Delete"],"newOperations":null,"baselineAttrCount":0,"newAttrCount":null,"attributeDifferences":[]}
```

## Step 3: Display in Human-Readable Format

### Option A: Use the Analyzer Tool

```bash
dotnet run --project LdifDiffAnalyzer/LdifDiffAnalyzer.csproj -- example-diff.jsonl --all
```

**Output:**
```
Loaded 13 entries from example-diff.jsonl

=== Summary Report ===

Total differences: 13
  Added: 10
  Removed: 3

=== Added Entries ===

Total added: 10

Sample entries (first 10):
  - cn=User01,OU=Users,DC=example,DC=com
    Operations: Add, Modify
    Attributes: 7
  ...
```

### Option B: Use Bash + jq

```bash
./show_diff.sh example-diff.jsonl
```

**Output:**
```
======================================================================

#1 [-] Removed
DN: cn=Alice Smith,OU=Users,DC=example,DC=com

  BEFORE:
    Operations: Add
    Attributes: 5

  ATTRIBUTE CHANGES:
      - objectClass: 1 value(s)
      - cn: 1 value(s)
      - givenName: 1 value(s)
      - mail: 1 value(s)
      - sn: 1 value(s)

#2 [-] Removed
DN: cn=Bob Jones,OU=Users,DC=example,DC=com

  BEFORE:
    Operations: Modify
    Attributes: 2

  ATTRIBUTE CHANGES:
      - mail: 1 value(s)
      - telephoneNumber: 1 value(s)

#3 [-] Removed
DN: cn=Carol White,OU=Users,DC=example,DC=com

  BEFORE:
    Operations: Delete
    Attributes: 0

#4 [+] Added
DN: cn=User01,OU=Users,DC=example,DC=com

  AFTER:
    Operations: Add, Modify
    Attributes: 7

  ATTRIBUTE CHANGES:
      + objectClass: 1 value(s)
      + cn: 1 value(s)
      + givenName: 1 value(s)
      + mail: 2 value(s)
      + sn: 1 value(s)
      + telephoneNumber: 1 value(s)
      + description: 0 value(s)
```

### Option C: Custom C# Code

```csharp
using System;
using System.IO;
using System.Text.Json;
using LdifDiffDemo;

foreach (var line in File.ReadLines("example-diff.jsonl"))
{
    var diff = JsonSerializer.Deserialize<EntryDiff>(line, new JsonSerializerOptions
    {
        PropertyNameCaseInsensitive = true
    });

    if (diff == null) continue;

    // Display the diff
    Console.WriteLine($"[{diff.ChangeType}] {diff.Dn}");
    
    if (diff.BaselineOperations != null)
    {
        Console.WriteLine($"  Before: {string.Join(", ", diff.BaselineOperations)} ({diff.BaselineAttrCount} attrs)");
    }
    
    if (diff.NewOperations != null)
    {
        Console.WriteLine($"  After: {string.Join(", ", diff.NewOperations)} ({diff.NewAttrCount} attrs)");
    }
    
    if (diff.AttributeDifferences?.Count > 0)
    {
        Console.WriteLine("  Changes:");
        foreach (var attr in diff.AttributeDifferences)
        {
            Console.WriteLine($"    {attr.Trim()}");
        }
    }
    
    Console.WriteLine();
}
```

## Understanding the Output

### Change Types

- **`[+] Added`**: Entry exists in new file but not in baseline
- **`[-] Removed`**: Entry exists in baseline but not in new file
- **`[~] Modified`**: Entry exists in both but with different attributes/operations

### Attribute Changes

- **`+ attribute: N value(s)`**: Attribute was added
- **`- attribute: N value(s)`**: Attribute was removed
- **`~ attribute: N → M value(s)`**: Attribute count changed from N to M

### Operations

LDIF operations that were performed on the entry:
- **Add**: Entry was added to the directory
- **Modify**: Entry attributes were modified
- **Delete**: Entry was deleted from the directory
- **ModDn**: Entry DN was modified (renamed/moved)

## Filtering and Analysis

### Find specific entries:
```bash
dotnet run --project LdifDiffAnalyzer/LdifDiffAnalyzer.csproj -- \
  example-diff.jsonl --find "User01"
```

### Export to CSV for Excel:
```bash
dotnet run --project LdifDiffAnalyzer/LdifDiffAnalyzer.csproj -- \
  example-diff.jsonl --csv report.csv
```

### Use jq for quick queries:
```bash
# Count by change type
jq -s 'group_by(.changeType) | map({changeType: .[0].changeType, count: length})' example-diff.jsonl

# Find all added entries
jq 'select(.changeType == "Added") | .dn' example-diff.jsonl

# Get entries with more than 5 attribute changes
jq 'select(.attributeDifferences | length > 5) | {dn, changes: (.attributeDifferences | length)}' example-diff.jsonl
```

## Real-World Example

Comparing two directory exports after a migration:

```bash
# 1. Generate the diff
dotnet run --project LdifDiffDemo/LdifDiffDemo.csproj -- \
  --compare before-migration.ldif after-migration.ldif \
  --output migration-diff.jsonl

# 2. Quick summary
dotnet run --project LdifDiffAnalyzer/LdifDiffAnalyzer.csproj -- migration-diff.jsonl

# 3. Find problematic changes
dotnet run --project LdifDiffAnalyzer/LdifDiffAnalyzer.csproj -- \
  migration-diff.jsonl --removed

# 4. Export for reporting
dotnet run --project LdifDiffAnalyzer/LdifDiffAnalyzer.csproj -- \
  migration-diff.jsonl --csv migration-report.csv
```
