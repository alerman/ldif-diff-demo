using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;

namespace LdifDiffDemo.Tests
{
    public class LdifComparerTests
    {

        #region AnalyzeLdif Tests

        [Fact]
        public void AnalyzeLdif_EmptyInput_ReturnsEmptyStats()
        {
            var ldif = "version: 1\n";
            using var reader = new StringReader(ldif);
            
            var stats = LdifComparer.AnalyzeLdif(reader);
            
            Assert.Equal(0, stats.TotalEntries);
            Assert.Equal(0, stats.TotalAddOperations);
            Assert.Equal(0, stats.TotalAttributes);
            Assert.Empty(stats.Entries);
        }

        [Fact]
        public void AnalyzeLdif_SingleAddOperation_CountsCorrectly()
        {
            var ldif = @"version: 1

dn: cn=Test User,dc=example,dc=com
changetype: add
objectClass: person
cn: Test User
sn: User
mail: test@example.com
";
            using var reader = new StringReader(ldif);
            
            var stats = LdifComparer.AnalyzeLdif(reader);
            
            Assert.Equal(1, stats.TotalEntries);
            Assert.Equal(1, stats.TotalAddOperations);
            Assert.Equal(4, stats.TotalAttributes);
            Assert.Equal(4.0, stats.AverageAttributesPerEntry);
            Assert.Single(stats.Entries);
            Assert.True(stats.Entries.ContainsKey("cn=Test User,dc=example,dc=com"));
        }

        [Fact]
        public void AnalyzeLdif_SingleModifyOperation_CountsCorrectly()
        {
            var ldif = @"version: 1

dn: cn=Test User,dc=example,dc=com
changetype: modify
replace: mail
mail: newemail@example.com
-
add: telephoneNumber
telephoneNumber: +1-555-1234
-
";
            using var reader = new StringReader(ldif);
            
            var stats = LdifComparer.AnalyzeLdif(reader);
            
            Assert.Equal(1, stats.TotalEntries);
            Assert.Equal(1, stats.TotalModifyOperations);
            Assert.Equal(2, stats.TotalAttributes);
            Assert.Contains("mail", stats.AttributeCounts.Keys);
            Assert.Contains("telephoneNumber", stats.AttributeCounts.Keys);
        }

        [Fact]
        public void AnalyzeLdif_DeleteOperation_CountsCorrectly()
        {
            var ldif = @"version: 1

dn: cn=Test User,dc=example,dc=com
changetype: delete
";
            using var reader = new StringReader(ldif);
            
            var stats = LdifComparer.AnalyzeLdif(reader);
            
            Assert.Equal(1, stats.TotalEntries);
            Assert.Equal(1, stats.TotalDeleteOperations);
            Assert.Equal(0, stats.TotalAttributes);
            Assert.Single(stats.Entries);
            var entry = stats.Entries["cn=Test User,dc=example,dc=com"];
            Assert.Contains("Delete", entry.OperationTypes);
        }

        [Fact]
        public void AnalyzeLdif_MultipleOperationsSameDn_TracksAllOperations()
        {
            var ldif = @"version: 1

dn: cn=Test User,dc=example,dc=com
changetype: add
objectClass: person
cn: Test User

dn: cn=Test User,dc=example,dc=com
changetype: modify
replace: mail
mail: test@example.com
-
";
            using var reader = new StringReader(ldif);
            
            var stats = LdifComparer.AnalyzeLdif(reader);
            
            Assert.Equal(2, stats.TotalEntries);
            Assert.Equal(1, stats.TotalAddOperations);
            Assert.Equal(1, stats.TotalModifyOperations);
            Assert.Single(stats.Entries); // Same DN
            
            var entry = stats.Entries["cn=Test User,dc=example,dc=com"];
            Assert.Equal(2, entry.OperationTypes.Count);
            Assert.Contains("Add", entry.OperationTypes);
            Assert.Contains("Modify", entry.OperationTypes);
            Assert.Equal(3, entry.TotalAttributeCount); // 2 from Add + 1 from Modify
        }

        #endregion

        #region CompareAttributeDistribution Tests

        [Fact]
        public void CompareAttributeDistribution_IdenticalDistributions_ReturnsNoDifferences()
        {
            var baseline = new Dictionary<string, int>
            {
                { "mail", 10 },
                { "cn", 10 }
            };
            var newCounts = new Dictionary<string, int>
            {
                { "mail", 10 },
                { "cn", 10 }
            };
            
            var diffs = LdifComparer.CompareAttributeDistribution(baseline, newCounts);
            
            Assert.Empty(diffs);
        }

        [Fact]
        public void CompareAttributeDistribution_NewAttribute_ReportsAddition()
        {
            var baseline = new Dictionary<string, int>
            {
                { "mail", 10 }
            };
            var newCounts = new Dictionary<string, int>
            {
                { "mail", 10 },
                { "telephoneNumber", 5 }
            };
            
            var diffs = LdifComparer.CompareAttributeDistribution(baseline, newCounts);
            
            Assert.Single(diffs);
            Assert.Contains("telephoneNumber", diffs[0]);
            Assert.Contains("new attribute", diffs[0]);
        }

        [Fact]
        public void CompareAttributeDistribution_RemovedAttribute_ReportsRemoval()
        {
            var baseline = new Dictionary<string, int>
            {
                { "mail", 10 },
                { "telephoneNumber", 5 }
            };
            var newCounts = new Dictionary<string, int>
            {
                { "mail", 10 }
            };
            
            var diffs = LdifComparer.CompareAttributeDistribution(baseline, newCounts);
            
            Assert.Single(diffs);
            Assert.Contains("telephoneNumber", diffs[0]);
            Assert.Contains("removed", diffs[0]);
        }

        [Fact]
        public void CompareAttributeDistribution_SignificantChange_ReportsChange()
        {
            var baseline = new Dictionary<string, int>
            {
                { "mail", 10 }
            };
            var newCounts = new Dictionary<string, int>
            {
                { "mail", 100 } // 900% increase
            };
            
            var diffs = LdifComparer.CompareAttributeDistribution(baseline, newCounts);
            
            Assert.Single(diffs);
            Assert.Contains("mail", diffs[0]);
            Assert.Contains("900", diffs[0]);
        }

        [Fact]
        public void CompareAttributeDistribution_InsignificantChange_IgnoresChange()
        {
            var baseline = new Dictionary<string, int>
            {
                { "mail", 100 }
            };
            var newCounts = new Dictionary<string, int>
            {
                { "mail", 110 } // Only 10% increase
            };
            
            var diffs = LdifComparer.CompareAttributeDistribution(baseline, newCounts);
            
            Assert.Empty(diffs); // Should be empty because threshold is 20%
        }

        #endregion

        #region CompareEntryAttributes Tests

        [Fact]
        public void CompareEntryAttributes_IdenticalEntries_ReturnsNoDifferences()
        {
            var baseline = new EntryInfo
            {
                Dn = "cn=test,dc=example,dc=com",
                Attributes = new Dictionary<string, int>
                {
                    { "mail", 1 },
                    { "cn", 1 }
                }
            };
            var newEntry = new EntryInfo
            {
                Dn = "cn=test,dc=example,dc=com",
                Attributes = new Dictionary<string, int>
                {
                    { "mail", 1 },
                    { "cn", 1 }
                }
            };
            
            var diffs = LdifComparer.CompareEntryAttributes(baseline, newEntry);
            
            Assert.Empty(diffs);
        }

        [Fact]
        public void CompareEntryAttributes_AddedAttribute_ReportsDifference()
        {
            var baseline = new EntryInfo
            {
                Attributes = new Dictionary<string, int> { { "mail", 1 } }
            };
            var newEntry = new EntryInfo
            {
                Attributes = new Dictionary<string, int>
                {
                    { "mail", 1 },
                    { "telephoneNumber", 1 }
                }
            };
            
            var diffs = LdifComparer.CompareEntryAttributes(baseline, newEntry);
            
            Assert.Single(diffs);
            Assert.Contains("telephoneNumber", diffs[0]);
            Assert.Contains("+", diffs[0]);
        }

        [Fact]
        public void CompareEntryAttributes_RemovedAttribute_ReportsDifference()
        {
            var baseline = new EntryInfo
            {
                Attributes = new Dictionary<string, int>
                {
                    { "mail", 1 },
                    { "telephoneNumber", 1 }
                }
            };
            var newEntry = new EntryInfo
            {
                Attributes = new Dictionary<string, int> { { "mail", 1 } }
            };
            
            var diffs = LdifComparer.CompareEntryAttributes(baseline, newEntry);
            
            Assert.Single(diffs);
            Assert.Contains("telephoneNumber", diffs[0]);
            Assert.Contains("-", diffs[0]);
        }

        [Fact]
        public void CompareEntryAttributes_ModifiedAttribute_ReportsDifference()
        {
            var baseline = new EntryInfo
            {
                Attributes = new Dictionary<string, int> { { "mail", 1 } }
            };
            var newEntry = new EntryInfo
            {
                Attributes = new Dictionary<string, int> { { "mail", 2 } }
            };
            
            var diffs = LdifComparer.CompareEntryAttributes(baseline, newEntry);
            
            Assert.Single(diffs);
            Assert.Contains("mail", diffs[0]);
            Assert.Contains("~", diffs[0]);
            Assert.Contains("1", diffs[0]);
            Assert.Contains("2", diffs[0]);
        }

        #endregion

        #region GenerateEntryDiffs Tests

        [Fact]
        public void GenerateEntryDiffs_IdenticalStats_ReturnsNoDifferences()
        {
            var baseline = new LdifStats();
            baseline.Entries["cn=test,dc=example,dc=com"] = new EntryInfo
            {
                Dn = "cn=test,dc=example,dc=com",
                OperationTypes = new List<string> { "Add" },
                TotalAttributeCount = 2,
                Attributes = new Dictionary<string, int>
                {
                    { "mail", 1 },
                    { "cn", 1 }
                }
            };
            
            var newStats = new LdifStats();
            newStats.Entries["cn=test,dc=example,dc=com"] = new EntryInfo
            {
                Dn = "cn=test,dc=example,dc=com",
                OperationTypes = new List<string> { "Add" },
                TotalAttributeCount = 2,
                Attributes = new Dictionary<string, int>
                {
                    { "mail", 1 },
                    { "cn", 1 }
                }
            };
            
            var diffs = LdifComparer.GenerateEntryDiffs(baseline, newStats);
            
            Assert.Empty(diffs);
        }

        [Fact]
        public void GenerateEntryDiffs_AddedEntry_ReportsAddition()
        {
            var baseline = new LdifStats();
            var newStats = new LdifStats();
            newStats.Entries["cn=new,dc=example,dc=com"] = new EntryInfo
            {
                Dn = "cn=new,dc=example,dc=com",
                OperationTypes = new List<string> { "Add" },
                TotalAttributeCount = 1,
                Attributes = new Dictionary<string, int> { { "cn", 1 } }
            };
            
            var diffs = LdifComparer.GenerateEntryDiffs(baseline, newStats);
            
            Assert.Single(diffs);
            Assert.Equal("Added", diffs[0].ChangeType);
            Assert.Equal("cn=new,dc=example,dc=com", diffs[0].Dn);
            Assert.NotNull(diffs[0].NewOperations);
            Assert.Contains("Add", diffs[0].NewOperations);
        }

        [Fact]
        public void GenerateEntryDiffs_RemovedEntry_ReportsRemoval()
        {
            var baseline = new LdifStats();
            baseline.Entries["cn=old,dc=example,dc=com"] = new EntryInfo
            {
                Dn = "cn=old,dc=example,dc=com",
                OperationTypes = new List<string> { "Delete" },
                TotalAttributeCount = 0
            };
            var newStats = new LdifStats();
            
            var diffs = LdifComparer.GenerateEntryDiffs(baseline, newStats);
            
            Assert.Single(diffs);
            Assert.Equal("Removed", diffs[0].ChangeType);
            Assert.Equal("cn=old,dc=example,dc=com", diffs[0].Dn);
            Assert.NotNull(diffs[0].BaselineOperations);
            Assert.Contains("Delete", diffs[0].BaselineOperations);
        }

        [Fact]
        public void GenerateEntryDiffs_ModifiedEntry_ReportsModification()
        {
            var baseline = new LdifStats();
            baseline.Entries["cn=test,dc=example,dc=com"] = new EntryInfo
            {
                Dn = "cn=test,dc=example,dc=com",
                OperationTypes = new List<string> { "Add" },
                TotalAttributeCount = 1,
                Attributes = new Dictionary<string, int> { { "mail", 1 } }
            };
            
            var newStats = new LdifStats();
            newStats.Entries["cn=test,dc=example,dc=com"] = new EntryInfo
            {
                Dn = "cn=test,dc=example,dc=com",
                OperationTypes = new List<string> { "Add" },
                TotalAttributeCount = 2,
                Attributes = new Dictionary<string, int>
                {
                    { "mail", 1 },
                    { "telephoneNumber", 1 }
                }
            };
            
            var diffs = LdifComparer.GenerateEntryDiffs(baseline, newStats);
            
            Assert.Single(diffs);
            Assert.Equal("Modified", diffs[0].ChangeType);
            Assert.Equal("cn=test,dc=example,dc=com", diffs[0].Dn);
            Assert.NotEmpty(diffs[0].AttributeDifferences);
        }

        #endregion

        #region CompareStats Tests

        [Fact]
        public void CompareStats_IdenticalStats_ReturnsGoodResult()
        {
            var stats = new LdifStats
            {
                TotalEntries = 10,
                TotalAttributes = 50,
                AttributeCounts = new Dictionary<string, int> { { "mail", 10 } }
            };
            
            var result = LdifComparer.CompareStats(stats, stats);
            
            Assert.True(result.IsGood);
            Assert.Empty(result.Differences);
            Assert.Contains("good", result.Message, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void CompareStats_EntityCountExceedsThreshold_ReportsBad()
        {
            var baseline = new LdifStats { TotalEntries = 10, TotalAttributes = 50 };
            var newStats = new LdifStats { TotalEntries = 20, TotalAttributes = 50 }; // 100% increase
            
            var result = LdifComparer.CompareStats(baseline, newStats, entityThresholdPercent: 10.0);
            
            Assert.False(result.IsGood);
            Assert.NotEmpty(result.Differences);
            Assert.Contains(result.Differences, d => d.Contains("Entity count"));
        }

        [Fact]
        public void CompareStats_AttributeCountExceedsThreshold_ReportsBad()
        {
            var baseline = new LdifStats { TotalEntries = 10, TotalAttributes = 50 };
            var newStats = new LdifStats { TotalEntries = 10, TotalAttributes = 100 }; // 100% increase
            
            var result = LdifComparer.CompareStats(baseline, newStats, attributeThresholdPercent: 10.0);
            
            Assert.False(result.IsGood);
            Assert.NotEmpty(result.Differences);
            Assert.Contains(result.Differences, d => d.Contains("attribute count"));
        }

        [Fact]
        public void CompareStats_AverageAttributesExceedsThreshold_ReportsBad()
        {
            var baseline = new LdifStats { TotalEntries = 10, TotalAttributes = 50 }; // Avg 5
            var newStats = new LdifStats { TotalEntries = 10, TotalAttributes = 100 }; // Avg 10
            
            var result = LdifComparer.CompareStats(baseline, newStats, avgAttributesThresholdPercent: 15.0);
            
            Assert.False(result.IsGood);
            Assert.NotEmpty(result.Differences);
            Assert.Contains(result.Differences, d => d.Contains("Average attributes"));
        }

        [Fact]
        public void CompareStats_WithinThresholds_ReturnsGood()
        {
            var baseline = new LdifStats
            {
                TotalEntries = 100,
                TotalAttributes = 500,
                AttributeCounts = new Dictionary<string, int> { { "mail", 100 } }
            };
            var newStats = new LdifStats
            {
                TotalEntries = 105, // 5% increase
                TotalAttributes = 525, // 5% increase
                AttributeCounts = new Dictionary<string, int> { { "mail", 105 } }
            };
            
            var result = LdifComparer.CompareStats(baseline, newStats);
            
            Assert.True(result.IsGood);
            Assert.Empty(result.Differences);
        }

        [Fact]
        public void CompareStats_CustomThresholds_RespectsThresholds()
        {
            var baseline = new LdifStats
            {
                TotalEntries = 100,
                TotalAttributes = 500,
                AttributeCounts = new Dictionary<string, int> { { "mail", 100 } }
            };
            var newStats = new LdifStats
            {
                TotalEntries = 120, // 20% increase
                TotalAttributes = 600, // 20% increase
                AttributeCounts = new Dictionary<string, int> { { "mail", 120 } } // 20% increase
            };
            
            // Should be bad with 10% threshold
            var result1 = LdifComparer.CompareStats(baseline, newStats, entityThresholdPercent: 10.0);
            Assert.False(result1.IsGood);
            
            // Should be good with 25% threshold (all values within threshold)
            var result2 = LdifComparer.CompareStats(baseline, newStats, entityThresholdPercent: 25.0, attributeThresholdPercent: 25.0);
            Assert.True(result2.IsGood);
        }

        #endregion

        #region PrintComparison Tests

        [Fact]
        public void PrintComparison_CorrectlyCountsEntriesAddedRemovedModified()
        {
            // Arrange
            var result = new ComparisonResult
            {
                IsGood = true,
                Message = "Test",
                BaselineStats = new LdifStats(),
                NewStats = new LdifStats(),
                EntryDiffs = new List<EntryDiff>
                {
                    new EntryDiff { Dn = "cn=user1,dc=example,dc=com", ChangeType = "Added", NewOperations = new List<string> { "Add" }, NewAttrCount = 3 },
                    new EntryDiff { Dn = "cn=user2,dc=example,dc=com", ChangeType = "Added", NewOperations = new List<string> { "Add" }, NewAttrCount = 2 },
                    new EntryDiff { Dn = "cn=user3,dc=example,dc=com", ChangeType = "Removed", BaselineOperations = new List<string> { "Delete" }, BaselineAttrCount = 4 },
                    new EntryDiff { Dn = "cn=user4,dc=example,dc=com", ChangeType = "Modified", BaselineOperations = new List<string> { "Add" }, NewOperations = new List<string> { "Add" }, BaselineAttrCount = 2, NewAttrCount = 3 },
                    new EntryDiff { Dn = "cn=user5,dc=example,dc=com", ChangeType = "Modified", BaselineOperations = new List<string> { "Modify" }, NewOperations = new List<string> { "Modify" }, BaselineAttrCount = 1, NewAttrCount = 2 }
                }
            };

            // Act
            var originalOut = Console.Out;
            try
            {
                using var consoleOutput = new StringWriter();
                Console.SetOut(consoleOutput);
                LdifComparer.PrintComparison(result);
                var output = consoleOutput.ToString();

                // Assert - entry-level counts
                Assert.Contains("Entry-level differences: 5 entries changed", output);
                Assert.Contains("Entries added: 2", output);
                Assert.Contains("Entries removed: 1", output);
                Assert.Contains("Entries modified: 2", output);
            }
            finally
            {
                Console.SetOut(originalOut);
            }
        }

        [Fact]
        public void PrintComparison_AccuratelyCountsAttributesAddedRemovedModified()
        {
            // Arrange
            var result = new ComparisonResult
            {
                IsGood = true,
                Message = "Test",
                BaselineStats = new LdifStats(),
                NewStats = new LdifStats(),
                EntryDiffs = new List<EntryDiff>
                {
                    new EntryDiff
                    {
                        Dn = "cn=user1,dc=example,dc=com",
                        ChangeType = "Modified",
                        AttributeDifferences = new List<string>
                        {
                            "  + mail: 1 value(s)",
                            "  + telephoneNumber: 2 value(s)",
                            "  - oldAttr: 1 value(s)",
                            "  ~ cn: 1 → 2 value(s)"
                        }
                    },
                    new EntryDiff
                    {
                        Dn = "cn=user2,dc=example,dc=com",
                        ChangeType = "Modified",
                        AttributeDifferences = new List<string>
                        {
                            "  + sn: 1 value(s)",
                            "  - description: 3 value(s)",
                            "  - title: 1 value(s)",
                            "  ~ givenName: 2 → 1 value(s)"
                        }
                    }
                }
            };

            // Act
            var originalOut = Console.Out;
            try
            {
                using var consoleOutput = new StringWriter();
                Console.SetOut(consoleOutput);
                LdifComparer.PrintComparison(result);
                var output = consoleOutput.ToString();

                // Assert - attribute-level counts
                Assert.Contains("Attributes added: 3", output);
                Assert.Contains("Attributes removed: 3", output);
                Assert.Contains("Attributes modified: 2", output);
            }
            finally
            {
                Console.SetOut(originalOut);
            }
        }

        [Fact]
        public void PrintComparison_DoesNotPrintAttributeSummaryWhenNoAttributeChanges()
        {
            // Arrange - entries with differences but no attribute changes
            var result = new ComparisonResult
            {
                IsGood = true,
                Message = "Test",
                BaselineStats = new LdifStats(),
                NewStats = new LdifStats(),
                EntryDiffs = new List<EntryDiff>
                {
                    new EntryDiff
                    {
                        Dn = "cn=user1,dc=example,dc=com",
                        ChangeType = "Added",
                        NewOperations = new List<string> { "Add" },
                        NewAttrCount = 3,
                        AttributeDifferences = new List<string>() // Empty - no attribute differences
                    },
                    new EntryDiff
                    {
                        Dn = "cn=user2,dc=example,dc=com",
                        ChangeType = "Removed",
                        BaselineOperations = new List<string> { "Delete" },
                        BaselineAttrCount = 2,
                        AttributeDifferences = new List<string>() // Empty
                    }
                }
            };

            // Act
            var originalOut = Console.Out;
            try
            {
                using var consoleOutput = new StringWriter();
                Console.SetOut(consoleOutput);
                LdifComparer.PrintComparison(result);
                var output = consoleOutput.ToString();

                // Assert - attribute summary should NOT appear
                Assert.DoesNotContain("Attributes added:", output);
                Assert.DoesNotContain("Attributes removed:", output);
                Assert.DoesNotContain("Attributes modified:", output);
                
                // But entry-level summary should still be present
                Assert.Contains("Entry-level differences: 2 entries changed", output);
                Assert.Contains("Entries added: 1", output);
                Assert.Contains("Entries removed: 1", output);
            }
            finally
            {
                Console.SetOut(originalOut);
            }
        }

        [Fact]
        public void PrintComparison_HandlesEmptyEntryDiffsList()
        {
            // Arrange
            var result = new ComparisonResult
            {
                IsGood = true,
                Message = "Test - no differences",
                BaselineStats = new LdifStats { TotalEntries = 10, TotalAttributes = 50 },
                NewStats = new LdifStats { TotalEntries = 10, TotalAttributes = 50 },
                EntryDiffs = new List<EntryDiff>() // Empty list
            };

            // Act & Assert - should not throw
            var originalOut = Console.Out;
            try
            {
                using var consoleOutput = new StringWriter();
                Console.SetOut(consoleOutput);
                var exception = Record.Exception(() => LdifComparer.PrintComparison(result));
                
                Assert.Null(exception);
                
                var output = consoleOutput.ToString();
                Assert.DoesNotContain("Entry-level differences:", output);
                Assert.DoesNotContain("Entries added:", output);
            }
            finally
            {
                Console.SetOut(originalOut);
            }
        }

        [Fact]
        public void PrintComparison_HandlesNullEntryDiffsList()
        {
            // Arrange
            var result = new ComparisonResult
            {
                IsGood = true,
                Message = "Test - null list",
                BaselineStats = new LdifStats { TotalEntries = 10, TotalAttributes = 50 },
                NewStats = new LdifStats { TotalEntries = 10, TotalAttributes = 50 },
                EntryDiffs = null! // Null list
            };

            // Act & Assert - should throw NullReferenceException
            var originalOut = Console.Out;
            try
            {
                using var consoleOutput = new StringWriter();
                Console.SetOut(consoleOutput);
                
                // In the current implementation, this will throw NullReferenceException
                // This test documents the current behavior
                Assert.Throws<NullReferenceException>(() => LdifComparer.PrintComparison(result));
            }
            finally
            {
                Console.SetOut(originalOut);
            }
        }

        #endregion
    }
}
