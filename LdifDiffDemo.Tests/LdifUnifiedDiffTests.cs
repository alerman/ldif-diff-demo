using System;
using System.IO;
using Xunit;

namespace LdifDiffDemo.Tests
{
    public class LdifUnifiedDiffTests
    {
        private readonly string _testDataDir;

        public LdifUnifiedDiffTests()
        {
            // Get the project root directory
            var currentDir = Directory.GetCurrentDirectory();
            _testDataDir = Path.Combine(currentDir, "..", "..", "..", "..", "test-data");
            _testDataDir = Path.GetFullPath(_testDataDir);
        }

        [Fact]
        public void GenerateUnifiedDiff_IdenticalFiles_ProducesEmptyDiff()
        {
            // Arrange
            var baselinePath = Path.Combine(_testDataDir, "ordered_baseline.ldif");
            var newPath = Path.Combine(_testDataDir, "ordered_baseline.ldif"); // Same file
            var outputPath = Path.Combine(Path.GetTempPath(), $"test_identical_{Guid.NewGuid()}.diff");

            try
            {
                // Act
                LdifUnifiedDiff.GenerateUnifiedDiff(baselinePath, newPath, outputPath);

                // Assert
                var output = File.ReadAllText(outputPath);
                var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                
                // Should only have header lines (---, +++, and empty line)
                Assert.True(lines.Length <= 3, "Diff should be empty for identical files");
                Assert.Contains("---", output);
                Assert.Contains("+++", output);
            }
            finally
            {
                if (File.Exists(outputPath))
                    File.Delete(outputPath);
            }
        }

        [Fact]
        public void GenerateUnifiedDiff_ReorderedEntriesNoChanges_ProducesEmptyDiff()
        {
            // Arrange
            var baselinePath = Path.Combine(_testDataDir, "ordered_baseline.ldif");
            var newPath = Path.Combine(_testDataDir, "reordered_nochanges.ldif");
            var outputPath = Path.Combine(Path.GetTempPath(), $"test_reordered_nochanges_{Guid.NewGuid()}.diff");

            try
            {
                // Act
                LdifUnifiedDiff.GenerateUnifiedDiff(baselinePath, newPath, outputPath);

                // Assert
                var output = File.ReadAllText(outputPath);
                var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                
                // Should only have header lines for files with reordered but identical entries
                Assert.True(lines.Length <= 3, "Diff should be empty when entries are reordered but content is identical");
            }
            finally
            {
                if (File.Exists(outputPath))
                    File.Delete(outputPath);
            }
        }

        [Fact]
        public void GenerateUnifiedDiff_ReorderedEntriesWithChanges_ShowsOnlyChanges()
        {
            // Arrange
            var baselinePath = Path.Combine(_testDataDir, "ordered_baseline.ldif");
            var newPath = Path.Combine(_testDataDir, "reordered_withchanges.ldif");
            var outputPath = Path.Combine(Path.GetTempPath(), $"test_reordered_withchanges_{Guid.NewGuid()}.diff");

            try
            {
                // Act
                LdifUnifiedDiff.GenerateUnifiedDiff(baselinePath, newPath, outputPath);

                // Assert
                var output = File.ReadAllText(outputPath);
                
                // Should show changes to User03 and User04
                Assert.Contains("Entry Modified: cn=User03", output);
                Assert.Contains("Entry Modified: cn=User04", output);
                Assert.Contains("-mail: user03@example.com", output);
                Assert.Contains("+mail: user03.updated@example.com", output);
                Assert.Contains("+telephoneNumber: +1 555 000004", output);
                
                // Should NOT show User01 or User02 as they are unchanged
                Assert.DoesNotContain("Entry Modified: cn=User01", output);
                Assert.DoesNotContain("Entry Modified: cn=User02", output);
            }
            finally
            {
                if (File.Exists(outputPath))
                    File.Delete(outputPath);
            }
        }

        [Fact]
        public void GenerateUnifiedDiff_ModifiedEntries_ShowsLineByLineChanges()
        {
            // Arrange
            var baselinePath = Path.Combine(_testDataDir, "baseline_test.ldif");
            var newPath = Path.Combine(_testDataDir, "modified_test.ldif");
            var outputPath = Path.Combine(Path.GetTempPath(), $"test_modified_{Guid.NewGuid()}.diff");

            try
            {
                // Act
                LdifUnifiedDiff.GenerateUnifiedDiff(baselinePath, newPath, outputPath);

                // Assert
                var output = File.ReadAllText(outputPath);
                
                // Should have proper diff headers
                Assert.Contains("---", output);
                Assert.Contains("+++", output);
                
                // Should show modified entries
                Assert.Contains("@@ Entry Modified:", output);
                
                // Should have removed lines (-)
                Assert.Contains("-mail: user01@example.com", output);
                
                // Should have added lines (+)
                Assert.Contains("+mail: user01.updated@example.com", output);
                
                // Should have context lines (space prefix)
                Assert.Contains(" dn: cn=User01", output);
            }
            finally
            {
                if (File.Exists(outputPath))
                    File.Delete(outputPath);
            }
        }

        [Fact]
        public void GenerateUnifiedDiff_AddedAndRemovedEntries_ShowsCompleteEntries()
        {
            // Arrange
            var baselinePath = Path.Combine(_testDataDir, "few_changes.ldif");
            var newPath = Path.Combine(_testDataDir, "many_changes.ldif");
            var outputPath = Path.Combine(Path.GetTempPath(), $"test_added_removed_{Guid.NewGuid()}.diff");

            try
            {
                // Act
                LdifUnifiedDiff.GenerateUnifiedDiff(baselinePath, newPath, outputPath);

                // Assert
                var output = File.ReadAllText(outputPath);
                
                // Should show removed entries
                Assert.Contains("@@ Entry Removed:", output);
                Assert.Contains("-dn: cn=Alice Smith", output);
                
                // Should show added entries
                Assert.Contains("@@ Entry Added:", output);
                Assert.Contains("+dn: cn=User", output);
                
                // All lines in removed entries should be prefixed with -
                var removedSection = output.Substring(output.IndexOf("@@ Entry Removed:"));
                var firstAddedIdx = removedSection.IndexOf("@@ Entry Added:");
                if (firstAddedIdx > 0)
                {
                    removedSection = removedSection.Substring(0, firstAddedIdx);
                }
                
                // Check that removed entries have lines starting with -
                Assert.Contains("-changetype:", removedSection);
            }
            finally
            {
                if (File.Exists(outputPath))
                    File.Delete(outputPath);
            }
        }

        [Fact]
        public void GenerateUnifiedDiff_SortsByDn_EvenWhenInputOutOfOrder()
        {
            // Arrange
            var baselinePath = Path.Combine(_testDataDir, "ordered_baseline.ldif");
            var newPath = Path.Combine(_testDataDir, "reordered_withchanges.ldif");
            var outputPath = Path.Combine(Path.GetTempPath(), $"test_sorting_{Guid.NewGuid()}.diff");

            try
            {
                // Act
                LdifUnifiedDiff.GenerateUnifiedDiff(baselinePath, newPath, outputPath);

                // Assert
                var output = File.ReadAllText(outputPath);
                
                // Find positions of User entries in output
                var user01Pos = output.IndexOf("cn=User01");
                var user02Pos = output.IndexOf("cn=User02");
                var user03Pos = output.IndexOf("cn=User03");
                var user04Pos = output.IndexOf("cn=User04");
                
                // Even though input is reordered, output should be sorted alphabetically
                // Only changed entries (User03, User04) should appear, and in order
                if (user03Pos > 0 && user04Pos > 0)
                {
                    Assert.True(user03Pos < user04Pos, "Entries should be sorted by DN");
                }
            }
            finally
            {
                if (File.Exists(outputPath))
                    File.Delete(outputPath);
            }
        }

        [Fact]
        public void GenerateUnifiedDiff_EmptyFiles_HandlesGracefully()
        {
            // Arrange
            var baselinePath = Path.Combine(_testDataDir, "no_changes.ldif");
            var newPath = Path.Combine(_testDataDir, "no_changes.ldif");
            var outputPath = Path.Combine(Path.GetTempPath(), $"test_empty_{Guid.NewGuid()}.diff");

            try
            {
                // Act
                LdifUnifiedDiff.GenerateUnifiedDiff(baselinePath, newPath, outputPath);

                // Assert
                var output = File.ReadAllText(outputPath);
                
                // Should have headers but no entries
                Assert.Contains("---", output);
                Assert.Contains("+++", output);
                Assert.DoesNotContain("@@ Entry", output);
            }
            finally
            {
                if (File.Exists(outputPath))
                    File.Delete(outputPath);
            }
        }
    }
}
