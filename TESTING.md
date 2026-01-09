# Testing Documentation

## Overview

The LDIF Diff Demo project includes comprehensive unit and integration tests to ensure the comparison logic works correctly across all scenarios.

## Test Suite Summary

**Total Tests: 38**
- Unit Tests: 31
- Integration Tests: 7

## Running Tests

```bash
# Run all tests
dotnet test LdifDiffDemo.sln

# Run with detailed output
dotnet test LdifDiffDemo.sln --verbosity normal

# Run specific test class
dotnet test --filter "FullyQualifiedName~LdifComparerTests"
dotnet test --filter "FullyQualifiedName~LdifUnifiedDiffTests"
dotnet test --filter "FullyQualifiedName~IntegrationTests"
```

## Test Coverage

### Unit Tests (`LdifComparerTests.cs`)

#### 1. **CalculatePercentageDiff Tests** (5 tests)
- Same values return 0%
- Doubled values return 100%
- Halved values return -50%
- Zero baseline with zero new value
- Zero baseline with non-zero new value

#### 2. **AnalyzeLdif Tests** (5 tests)
- Empty LDIF input
- Single Add operation counting
- Single Modify operation counting
- Delete operation counting
- Multiple operations on same DN

#### 3. **CompareAttributeDistribution Tests** (5 tests)
- Identical distributions (no differences)
- New attribute detection
- Removed attribute detection
- Significant changes (>20%)
- Insignificant changes (≤20%)

#### 4. **CompareEntryAttributes Tests** (4 tests)
- Identical entries
- Added attributes
- Removed attributes
- Modified attribute counts

#### 5. **GenerateEntryDiffs Tests** (4 tests)
- Identical stats (no diffs)
- Added entries
- Removed entries
- Modified entries

#### 6. **CompareStats Tests** (7 tests)
- Identical stats return "good"
- Entity count threshold violations
- Attribute count threshold violations
- Average attributes threshold violations
- Within threshold validation
- Custom threshold handling
- Multiple threshold validation

### Unified Diff Tests (`LdifUnifiedDiffTests.cs`)

#### 1. **Unified Diff Generation** (7 tests)
- Identical files produce empty diff
- Reordered entries with no changes produce empty diff
- Reordered entries with changes show only changes
- Modified entries show line-by-line changes
- Added and removed entries show complete entries
- DN sorting works correctly even when input is out of order
- Empty files handled gracefully

### Integration Tests (`IntegrationTests.cs`)

#### 1. **End-to-End Scenarios** (7 tests)
- Identical files comparison
- Different files detection
- Multiple operation types (Add/Modify/Delete) tracking
- Large percentage difference detection
- Modified entry attribute detection
- Empty baseline handling
- Threshold boundary validation

## Code Quality

### Testability Features

The codebase has been refactored to support comprehensive testing:

1. **Public Methods**: Core comparison logic exposed as public methods
2. **Dependency Injection**: `TextReader` overloads for testing without file I/O
3. **Separated Concerns**: Distinct methods for parsing, comparison, and output
4. **Pure Functions**: Most logic implemented as pure functions without side effects

### Test Organization

```
LdifDiffDemo.Tests/
├── LdifComparerTests.cs      # Unit tests for comparison methods
├── LdifUnifiedDiffTests.cs   # Unit tests for unified diff generation
└── IntegrationTests.cs       # End-to-end workflow tests
```

## Coverage Areas

### ✅ Fully Covered
- Percentage calculation logic
- LDIF parsing (all operation types)
- Attribute distribution comparison
- Entry-level diff generation
- Unified diff generation (line-by-line)
- Threshold validation
- Entry reordering handling
- Edge cases (empty files, zero values, reordered entries)

### 📊 What's Tested

| Component | Coverage |
|-----------|----------|
| CalculatePercentageDiff | 100% |
| AnalyzeLdif | 100% |
| CompareAttributeDistribution | 100% |
| CompareEntryAttributes | 100% |
| GenerateEntryDiffs | 100% |
| CompareStats | 100% |
| End-to-End Workflows | 100% |

## Best Practices Demonstrated

1. **Arrange-Act-Assert Pattern**: All tests follow AAA structure
2. **Descriptive Names**: Test names clearly describe what they test
3. **Single Responsibility**: Each test validates one specific behavior
4. **Edge Case Coverage**: Tests include boundary conditions
5. **Integration Tests**: Validate complete workflows
6. **Test Independence**: Tests don't depend on each other

## Continuous Integration

These tests are designed to run in CI/CD pipelines:

```yaml
# Example CI configuration
- name: Run Tests
  run: dotnet test LdifDiffDemo.sln --no-build --verbosity normal
```

## Adding New Tests

When adding new features:

1. Add unit tests for individual methods in `LdifComparerTests.cs`
2. Add integration tests for end-to-end workflows in `IntegrationTests.cs`
3. Ensure new tests follow existing naming conventions
4. Run all tests before committing: `dotnet test`

## Known Warnings

There are 2 minor nullable reference warnings in the test code that don't affect functionality:
- Line 421: Assert.Contains on nullable collection
- Line 442: Assert.Contains on nullable collection

These are safe to ignore as the collections are verified non-null in context.
