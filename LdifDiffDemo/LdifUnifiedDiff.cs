using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using LdifHelper;

namespace LdifDiffDemo
{
    /// <summary>
    /// Generates unified diff output (like diff -u) for LDIF files
    /// </summary>
    public static class LdifUnifiedDiff
    {
        public static void GenerateUnifiedDiff(string baselinePath, string newPath, string outputPath)
        {
            // Parse both files and organize by DN
            var baselineEntries = ParseLdifToDictionary(baselinePath);
            var newEntries = ParseLdifToDictionary(newPath);

            using var writer = new StreamWriter(outputPath, false, new UTF8Encoding(false));

            // Write diff header
            writer.WriteLine($"--- {baselinePath}");
            writer.WriteLine($"+++ {newPath}");
            writer.WriteLine();

            // Get all DNs from both files
            var allDns = new HashSet<string>(baselineEntries.Keys);
            allDns.UnionWith(newEntries.Keys);

            foreach (var dn in allDns.OrderBy(d => d))
            {
                var hasBaseline = baselineEntries.TryGetValue(dn, out var baselineLines);
                var hasNew = newEntries.TryGetValue(dn, out var newLines);

                if (!hasBaseline && hasNew)
                {
                    // Entry added
                    WriteAddedEntry(writer, dn, newLines!);
                }
                else if (hasBaseline && !hasNew)
                {
                    // Entry removed
                    WriteRemovedEntry(writer, dn, baselineLines!);
                }
                else if (hasBaseline && hasNew)
                {
                    // Entry exists in both - check if changed
                    if (!AreEntriesEqual(baselineLines!, newLines!))
                    {
                        WriteModifiedEntry(writer, dn, baselineLines!, newLines!);
                    }
                }
            }
        }

        private static Dictionary<string, List<string>> ParseLdifToDictionary(string path)
        {
            var entries = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            var currentDn = string.Empty;
            var currentLines = new List<string>();

            using var reader = new StreamReader(path, Encoding.GetEncoding(20127), false);

            string? line;
            while ((line = reader.ReadLine()) != null)
            {
                // Skip version line
                if (line.StartsWith("version:", StringComparison.OrdinalIgnoreCase))
                    continue;

                // Empty line indicates end of entry
                if (string.IsNullOrWhiteSpace(line))
                {
                    if (!string.IsNullOrEmpty(currentDn) && currentLines.Count > 0)
                    {
                        entries[currentDn] = new List<string>(currentLines);
                        currentLines.Clear();
                    }
                    currentDn = string.Empty;
                    continue;
                }

                // Check for DN line
                if (line.StartsWith("dn:", StringComparison.OrdinalIgnoreCase))
                {
                    currentDn = line.Substring(3).Trim();
                    currentLines.Add(line);
                }
                else if (!string.IsNullOrEmpty(currentDn))
                {
                    currentLines.Add(line);
                }
            }

            // Handle last entry if file doesn't end with blank line
            if (!string.IsNullOrEmpty(currentDn) && currentLines.Count > 0)
            {
                entries[currentDn] = currentLines;
            }

            return entries;
        }

        private static bool AreEntriesEqual(List<string> baseline, List<string> newEntry)
        {
            if (baseline.Count != newEntry.Count)
                return false;

            for (int i = 0; i < baseline.Count; i++)
            {
                if (!baseline[i].Equals(newEntry[i], StringComparison.Ordinal))
                    return false;
            }

            return true;
        }

        private static void WriteAddedEntry(StreamWriter writer, string dn, List<string> lines)
        {
            writer.WriteLine($"@@ Entry Added: {dn} @@");
            foreach (var line in lines)
            {
                writer.WriteLine($"+{line}");
            }
            writer.WriteLine("+");
            writer.WriteLine();
        }

        private static void WriteRemovedEntry(StreamWriter writer, string dn, List<string> lines)
        {
            writer.WriteLine($"@@ Entry Removed: {dn} @@");
            foreach (var line in lines)
            {
                writer.WriteLine($"-{line}");
            }
            writer.WriteLine("-");
            writer.WriteLine();
        }

        private static void WriteModifiedEntry(StreamWriter writer, string dn, List<string> baseline, List<string> newEntry)
        {
            writer.WriteLine($"@@ Entry Modified: {dn} @@");

            // Perform line-by-line diff
            var diff = ComputeLineDiff(baseline, newEntry);

            foreach (var diffLine in diff)
            {
                writer.WriteLine(diffLine);
            }

            writer.WriteLine();
        }

        private static List<string> ComputeLineDiff(List<string> baseline, List<string> newEntry)
        {
            var result = new List<string>();

            // Simple line-by-line comparison (not optimal but straightforward)
            // For better diff, would use Myers diff algorithm

            var baselineSet = new HashSet<string>(baseline);
            var newSet = new HashSet<string>(newEntry);

            // Lines only in baseline (removed)
            var removed = baseline.Where(l => !newSet.Contains(l)).ToList();

            // Lines only in new (added)
            var added = newEntry.Where(l => !baselineSet.Contains(l)).ToList();

            // Lines in both (unchanged)
            var unchanged = baseline.Where(l => newSet.Contains(l)).ToList();

            // Build unified diff output
            // Show context + changes
            int baselineIdx = 0;
            int newIdx = 0;

            var baselineUsed = new HashSet<int>();
            var newUsed = new HashSet<int>();

            // First pass: identify matching lines
            for (int i = 0; i < baseline.Count; i++)
            {
                for (int j = 0; j < newEntry.Count; j++)
                {
                    if (!newUsed.Contains(j) && baseline[i] == newEntry[j])
                    {
                        baselineUsed.Add(i);
                        newUsed.Add(j);
                        break;
                    }
                }
            }

            // Second pass: generate diff
            baselineIdx = 0;
            newIdx = 0;

            while (baselineIdx < baseline.Count || newIdx < newEntry.Count)
            {
                if (baselineIdx < baseline.Count && !baselineUsed.Contains(baselineIdx))
                {
                    // Line was removed
                    result.Add($"-{baseline[baselineIdx]}");
                    baselineIdx++;
                }
                else if (newIdx < newEntry.Count && !newUsed.Contains(newIdx))
                {
                    // Line was added
                    result.Add($"+{newEntry[newIdx]}");
                    newIdx++;
                }
                else if (baselineIdx < baseline.Count && newIdx < newEntry.Count)
                {
                    // Both lines match
                    result.Add($" {baseline[baselineIdx]}");
                    baselineIdx++;
                    newIdx++;
                }
                else if (baselineIdx < baseline.Count)
                {
                    result.Add($" {baseline[baselineIdx]}");
                    baselineIdx++;
                }
                else if (newIdx < newEntry.Count)
                {
                    result.Add($" {newEntry[newIdx]}");
                    newIdx++;
                }
            }

            return result;
        }
    }
}
