using System;
using System.IO;
using Xunit;

namespace LdifDiffDemo.Tests
{
    public class IntegrationTests
    {
        [Fact]
        public void EndToEnd_IdenticalFiles_ReturnsGoodComparison()
        {
            var ldif = @"version: 1

dn: cn=Alice Smith,OU=Users,DC=example,DC=com
changetype: add
objectClass: inetOrgPerson
cn: Alice Smith
sn: Smith
givenName: Alice
mail: alice.smith@example.com

dn: cn=Bob Jones,OU=Users,DC=example,DC=com
changetype: modify
replace: mail
mail: bob.jones.new@example.com
-
add: telephoneNumber
telephoneNumber: +1 555 000 0001
-
";

            using var baseline = new StringReader(ldif);
            using var newFile = new StringReader(ldif);
            
            var baselineStats = LdifComparer.AnalyzeLdif(baseline);
            var newStats = LdifComparer.AnalyzeLdif(newFile);
            var result = LdifComparer.CompareStats(baselineStats, newStats);
            
            Assert.True(result.IsGood);
            Assert.Empty(result.Differences);
            Assert.Empty(result.EntryDiffs);
        }

        [Fact]
        public void EndToEnd_DifferentFiles_ReportsDifferences()
        {
            var baselineLdif = @"version: 1

dn: cn=Test User,dc=example,dc=com
changetype: add
objectClass: person
cn: Test User
sn: User
";

            var newLdif = @"version: 1

dn: cn=Test User,dc=example,dc=com
changetype: add
objectClass: person
cn: Test User
sn: User

dn: cn=Another User,dc=example,dc=com
changetype: add
objectClass: person
cn: Another User
sn: User2
";

            using var baseline = new StringReader(baselineLdif);
            using var newFile = new StringReader(newLdif);
            
            var baselineStats = LdifComparer.AnalyzeLdif(baseline);
            var newStats = LdifComparer.AnalyzeLdif(newFile);
            var result = LdifComparer.CompareStats(baselineStats, newStats);
            
            Assert.False(result.IsGood);
            Assert.NotEmpty(result.Differences);
            Assert.Single(result.EntryDiffs);
            Assert.Equal("Added", result.EntryDiffs[0].ChangeType);
        }

        [Fact]
        public void EndToEnd_AddModifyDelete_TracksAllOperations()
        {
            var ldif = @"version: 1

dn: cn=User1,dc=example,dc=com
changetype: add
objectClass: person
cn: User1

dn: cn=User1,dc=example,dc=com
changetype: modify
add: mail
mail: user1@example.com
-

dn: cn=User2,dc=example,dc=com
changetype: delete
";

            using var reader = new StringReader(ldif);
            var stats = LdifComparer.AnalyzeLdif(reader);
            
            Assert.Equal(3, stats.TotalEntries);
            Assert.Equal(1, stats.TotalAddOperations);
            Assert.Equal(1, stats.TotalModifyOperations);
            Assert.Equal(1, stats.TotalDeleteOperations);
            Assert.Equal(2, stats.Entries.Count);
            
            // User1 should have both Add and Modify operations
            var user1 = stats.Entries["cn=User1,dc=example,dc=com"];
            Assert.Equal(2, user1.OperationTypes.Count);
            Assert.Contains("Add", user1.OperationTypes);
            Assert.Contains("Modify", user1.OperationTypes);
            Assert.Equal(3, user1.TotalAttributeCount); // objectClass + cn from Add, mail from Modify
            
            // User2 should only have Delete operation
            var user2 = stats.Entries["cn=User2,dc=example,dc=com"];
            Assert.Single(user2.OperationTypes);
            Assert.Contains("Delete", user2.OperationTypes);
            Assert.Equal(0, user2.TotalAttributeCount);
        }

        [Fact]
        public void EndToEnd_LargePercentageDifferences_DetectedCorrectly()
        {
            var baselineLdif = @"version: 1

dn: cn=User1,dc=example,dc=com
changetype: add
objectClass: person
cn: User1
";

            var newLdif = @"version: 1

dn: cn=User1,dc=example,dc=com
changetype: add
objectClass: person
cn: User1

dn: cn=User2,dc=example,dc=com
changetype: add
objectClass: person
cn: User2

dn: cn=User3,dc=example,dc=com
changetype: add
objectClass: person
cn: User3

dn: cn=User4,dc=example,dc=com
changetype: add
objectClass: person
cn: User4

dn: cn=User5,dc=example,dc=com
changetype: add
objectClass: person
cn: User5
";

            using var baseline = new StringReader(baselineLdif);
            using var newFile = new StringReader(newLdif);
            
            var baselineStats = LdifComparer.AnalyzeLdif(baseline);
            var newStats = LdifComparer.AnalyzeLdif(newFile);
            var result = LdifComparer.CompareStats(baselineStats, newStats);
            
            Assert.False(result.IsGood);
            Assert.Contains(result.Differences, d => d.Contains("Entity count"));
            Assert.Contains(result.Differences, d => d.Contains("400")); // 400% increase
            Assert.Equal(4, result.EntryDiffs.Count); // 4 new users added
        }

        [Fact]
        public void EndToEnd_ModifiedEntryAttributes_DetectsChanges()
        {
            var baselineLdif = @"version: 1

dn: cn=Test User,dc=example,dc=com
changetype: modify
replace: mail
mail: old@example.com
-
";

            var newLdif = @"version: 1

dn: cn=Test User,dc=example,dc=com
changetype: modify
replace: mail
mail: old@example.com
-
add: telephoneNumber
telephoneNumber: +1-555-1234
-
";

            using var baseline = new StringReader(baselineLdif);
            using var newFile = new StringReader(newLdif);
            
            var baselineStats = LdifComparer.AnalyzeLdif(baseline);
            var newStats = LdifComparer.AnalyzeLdif(newFile);
            var result = LdifComparer.CompareStats(baselineStats, newStats);
            
            // Should detect the modified entry with additional attribute
            Assert.Single(result.EntryDiffs);
            Assert.Equal("Modified", result.EntryDiffs[0].ChangeType);
            Assert.NotEmpty(result.EntryDiffs[0].AttributeDifferences);
            Assert.Contains(result.EntryDiffs[0].AttributeDifferences, d => d.Contains("telephoneNumber"));
        }

        [Fact]
        public void EndToEnd_EmptyBaseline_AllEntriesMarkedAsAdded()
        {
            var baselineLdif = "version: 1\n";
            var newLdif = @"version: 1

dn: cn=User1,dc=example,dc=com
changetype: add
objectClass: person
cn: User1

dn: cn=User2,dc=example,dc=com
changetype: add
objectClass: person
cn: User2
";

            using var baseline = new StringReader(baselineLdif);
            using var newFile = new StringReader(newLdif);
            
            var baselineStats = LdifComparer.AnalyzeLdif(baseline);
            var newStats = LdifComparer.AnalyzeLdif(newFile);
            var result = LdifComparer.CompareStats(baselineStats, newStats);
            
            Assert.False(result.IsGood); // Should be bad because baseline is empty
            Assert.Equal(2, result.EntryDiffs.Count);
            Assert.All(result.EntryDiffs, diff => Assert.Equal("Added", diff.ChangeType));
        }

        [Fact]
        public void EndToEnd_ThresholdBoundaries_WorksCorrectly()
        {
            var baselineLdif = @"version: 1

dn: cn=User1,dc=example,dc=com
changetype: add
objectClass: person
cn: User1
";

            var newLdif = @"version: 1

dn: cn=User1,dc=example,dc=com
changetype: add
objectClass: person
cn: User1

dn: cn=User2,dc=example,dc=com
changetype: add
objectClass: person
cn: User2
";

            using var baseline = new StringReader(baselineLdif);
            using var newFile = new StringReader(newLdif);
            
            var baselineStats = LdifComparer.AnalyzeLdif(baseline);
            var newStats = LdifComparer.AnalyzeLdif(newFile);
            
            // 100% increase should fail with 10% threshold
            var result1 = LdifComparer.CompareStats(baselineStats, newStats, entityThresholdPercent: 10.0);
            Assert.False(result1.IsGood);
            
            // But should pass with 150% threshold
            var result2 = LdifComparer.CompareStats(baselineStats, newStats, 
                entityThresholdPercent: 150.0, 
                attributeThresholdPercent: 150.0,
                avgAttributesThresholdPercent: 150.0);
            Assert.True(result2.IsGood);
        }
    }
}
