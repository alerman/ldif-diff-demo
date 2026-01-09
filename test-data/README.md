# Test Data

This directory contains LDIF test files for testing and demonstrating the LDIF comparison tool.

## Small Test Files (Manual)

These files are manually created for basic testing:

- **`few_changes.ldif`** (22 lines) - Small file with 1 add, 1 modify, 1 delete
- **`many_changes.ldif`** (192 lines) - 10 adds, 10 modifies
- **`no_changes.ldif`** (2 lines) - Empty LDIF file

### Unified Diff Test Files

- **`baseline_test.ldif`** (25 lines) - Baseline file with 3 user entries
- **`modified_test.ldif`** (26 lines) - Modified version with line-by-line changes
- **`example_output.diff`** - Example unified diff output

## Large Test Files (Generated)

These files are generated using `generate_test_ldifs.py` for performance and stress testing:

### `baseline_large.ldif`
- **Size**: 1.8 MB (80,002 lines)
- **Entries**: 5,000 user entries
- **Operations**: All Add operations
- **Purpose**: Baseline for comparison testing

### `similar_large.ldif`
- **Size**: 1.8 MB (80,602 lines)
- **Entries**: 5,000 Add + 100 Modify operations
- **Difference**: ~2% change from baseline
- **Purpose**: Test validation that should PASS (within 10% threshold)

### `different_large.ldif`
- **Size**: 2.4 MB (107,002 lines)
- **Entries**: 6,500 user entries (+30%)
- **Difference**: 30% more entries, 2 new attributes (mobile, description)
- **Purpose**: Test validation that should FAIL (exceeds 10% threshold)

### `complex_operations.ldif`
- **Size**: 557 KB (28,605 lines)
- **Entries**: 2,000 Add + 667 Modify + 200 Delete
- **Total Operations**: 2,867
- **Purpose**: Test handling of mixed operation types

### `very_large.ldif`
- **Size**: 13 MB (500,002 lines)
- **Entries**: 25,000 user entries
- **Attributes per Entry**: 17 attributes
- **Purpose**: Stress test with large file

## Test Scenarios

### Scenario 1: Unified Diff (Line-by-Line Changes)
```bash
dotnet run --project ../LdifDiffDemo/LdifDiffDemo.csproj -- --diff baseline_test.ldif modified_test.ldif output.diff
```
**Expected**: Generates a unified diff showing line-by-line changes for matching entries

### Scenario 2: Similar Files (Should Pass)
```bash
dotnet run --project ../LdifDiffDemo/LdifDiffDemo.csproj -- --compare baseline_large.ldif similar_large.ldif
```
**Expected**: ✓ Pass (changes within 10% threshold)

### Scenario 3: Significantly Different Files (Should Fail)
```bash
dotnet run --project ../LdifDiffDemo/LdifDiffDemo.csproj -- --compare baseline_large.ldif different_large.ldif --output diff-output.txt
```
**Expected**: ✗ Fail (30% increase exceeds threshold)

### Scenario 4: Single File Analysis
```bash
dotnet run --project ../LdifDiffDemo/LdifDiffDemo.csproj -- very_large.ldif very_large_analysis.jsonl
```
**Expected**: Analyzes 25,000 entries and outputs detailed JSONL

### Scenario 5: Complex Operations
```bash
dotnet run --project ../LdifDiffDemo/LdifDiffDemo.csproj -- complex_operations.ldif complex_analysis.jsonl
```
**Expected**: Handles add/modify/delete operations correctly

## Performance Benchmarks

Based on tests on RHEL system:

| File | Size | Entries | Operations | Processing Time |
|------|------|---------|------------|-----------------|
| baseline_large.ldif | 1.8 MB | 5,000 | 5,000 | ~2.7s |
| similar_large.ldif | 1.8 MB | 5,100 | 5,100 | ~2.7s |
| different_large.ldif | 2.4 MB | 6,500 | 6,500 | ~2.8s |
| complex_operations.ldif | 557 KB | 2,867 | 2,867 | ~1.0s |
| very_large.ldif | 13 MB | 25,000 | 25,000 | ~3.5s |

## Regenerating Test Files

To regenerate the large test files:

```bash
dotnet run --project ../LdifTestGenerator/LdifTestGenerator.csproj
```

This will recreate all generated test files with deterministic random data (fixed seed).

## File Structure

Each user entry in the generated files includes:
- `cn` (Common Name / User ID)
- `sn` (Surname)
- `givenName` (First Name)
- `displayName` (Full Name)
- `mail` (Email Address)
- `telephoneNumber` (Phone)
- `department` (Department)
- `l` (Location)
- `employeeNumber` (Employee Number)
- `title` (Job Title)
- `objectClass` (3 values: inetOrgPerson, organizationalPerson, person)

Additional attributes in very_large.ldif:
- `mobile` (Mobile Phone)
- `description` (Description)
- `userPrincipalName` (UPN)
