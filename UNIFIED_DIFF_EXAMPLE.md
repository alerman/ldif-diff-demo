# Unified Diff Mode Example

This document demonstrates the new unified diff mode that shows line-by-line changes similar to Linux `diff -u`.

## Quick Start

```bash
dotnet run --project LdifDiffDemo/LdifDiffDemo.csproj -- --diff baseline.ldif new.ldif output.diff
```

Or using the short form:

```bash
dotnet run --project LdifDiffDemo/LdifDiffDemo.csproj -- -d baseline.ldif new.ldif output.diff
```

## Example with Test Files

### Command

```bash
dotnet run --project LdifDiffDemo/LdifDiffDemo.csproj -- --diff \
  test-data/baseline_test.ldif \
  test-data/modified_test.ldif \
  output.diff
```

### Input: baseline_test.ldif

```ldif
dn: cn=User01,OU=Users,DC=example,DC=com
changetype: add
objectClass: inetOrgPerson
cn: User01
sn: TestUser
givenName: User
mail: user01@example.com
telephoneNumber: +1 555 000001

dn: cn=User02,OU=Users,DC=example,DC=com
changetype: add
objectClass: inetOrgPerson
cn: User02
sn: TestUser
givenName: User
mail: user02@example.com
```

### Input: modified_test.ldif

```ldif
dn: cn=User01,OU=Users,DC=example,DC=com
changetype: add
objectClass: inetOrgPerson
cn: User01
sn: TestUser
givenName: User
mail: user01.updated@example.com
telephoneNumber: +1 555 999999

dn: cn=User02,OU=Users,DC=example,DC=com
changetype: add
objectClass: inetOrgPerson
cn: User02
sn: TestUser
givenName: User
mail: user02@example.com
telephoneNumber: +1 555 000002
```

### Output: output.diff

```diff
--- baseline_test.ldif
+++ modified_test.ldif

@@ Entry Modified: cn=User01,OU=Users,DC=example,DC=com @@
 dn: cn=User01,OU=Users,DC=example,DC=com
 changetype: add
 objectClass: inetOrgPerson
 cn: User01
 sn: TestUser
 givenName: User
-mail: user01@example.com
-telephoneNumber: +1 555 000001
+mail: user01.updated@example.com
+telephoneNumber: +1 555 999999

@@ Entry Modified: cn=User02,OU=Users,DC=example,DC=com @@
 dn: cn=User02,OU=Users,DC=example,DC=com
 changetype: add
 objectClass: inetOrgPerson
 cn: User02
 sn: TestUser
 givenName: User
 mail: user02@example.com
+telephoneNumber: +1 555 000002
```

## Output Format Explained

### Entry Modified

For entries that exist in both files but have changed:

```diff
@@ Entry Modified: dn @@
 context line (unchanged)
-removed line
+added line
 more context
```

- Lines prefixed with ` ` (space) are context lines (unchanged)
- Lines prefixed with `-` were removed from the baseline
- Lines prefixed with `+` were added in the new file

### Entry Added

For entries that only exist in the new file:

```diff
@@ Entry Added: dn @@
+dn: cn=NewUser,OU=Users,DC=example,DC=com
+changetype: add
+objectClass: inetOrgPerson
+cn: NewUser
+mail: new.user@example.com
+
```

All lines are prefixed with `+` to indicate the entire entry is new.

### Entry Removed

For entries that only exist in the baseline file:

```diff
@@ Entry Removed: dn @@
-dn: cn=OldUser,OU=Users,DC=example,DC=com
-changetype: delete
-
```

All lines are prefixed with `-` to indicate the entire entry was removed.

## Large File Example

The unified diff mode works efficiently with large files:

```bash
dotnet run --project LdifDiffDemo/LdifDiffDemo.csproj -- --diff \
  test-data/baseline_large.ldif \
  test-data/similar_large.ldif \
  large_output.diff
```

This generates a detailed line-by-line diff for 5000+ entries.

## When to Use Each Mode

### Use Unified Diff Mode (`--diff`) when:
- You need to see exactly what changed line-by-line
- You want traditional diff output for code review
- You need to manually review changes in detail
- You want to pipe output to other diff tools

### Use Comparison Mode (`--compare`) when:
- You need to validate if changes are within acceptable thresholds
- You want high-level statistics about changes
- You need automated pass/fail validation
- You want structured JSONL output for analysis

### Use both:
Many workflows benefit from using both modes:
1. First use `--compare` to validate the changes are acceptable
2. Then use `--diff` to review the specific changes

```bash
# Step 1: Validate changes are acceptable
dotnet run --project LdifDiffDemo/LdifDiffDemo.csproj -- --compare \
  baseline.ldif new.ldif --output comparison.jsonl

# Step 2: Review detailed line-by-line changes
dotnet run --project LdifDiffDemo/LdifDiffDemo.csproj -- --diff \
  baseline.ldif new.ldif changes.diff

# Step 3: Analyze the comparison data
dotnet run --project LdifDiffAnalyzer/LdifDiffAnalyzer.csproj -- \
  comparison.jsonl --all
```

## Tips

1. **Pipe to less for review**: `cat output.diff | less -R`
2. **Filter specific entries**: `grep "user01" output.diff -A 10`
3. **Count changes**: `grep "^+" output.diff | wc -l`
4. **Find all modified entries**: `grep "@@ Entry Modified:" output.diff`
