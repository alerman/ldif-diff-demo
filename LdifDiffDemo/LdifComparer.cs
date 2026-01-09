using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using LdifHelper;

namespace LdifDiffDemo
{
    public class EntryInfo
    {
        public string Dn { get; set; } = string.Empty;
        public List<string> OperationTypes { get; set; } = new();
        public int TotalAttributeCount { get; set; }
        public Dictionary<string, int> Attributes { get; set; } = new();
    }

    public class LdifStats
    {
        public int TotalEntries { get; set; }
        public int TotalAddOperations { get; set; }
        public int TotalDeleteOperations { get; set; }
        public int TotalModifyOperations { get; set; }
        public int TotalModDnOperations { get; set; }
        
        public int TotalAttributes { get; set; }
        public Dictionary<string, int> AttributeCounts { get; set; } = new();
        public Dictionary<string, int> EntryAttributeCounts { get; set; } = new(); // DN -> attr count
        public Dictionary<string, EntryInfo> Entries { get; set; } = new(); // DN -> entry info
        
        public double AverageAttributesPerEntry => TotalEntries > 0 
            ? (double)TotalAttributes / TotalEntries 
            : 0;
    }

    public class EntryDiff
    {
        public string Dn { get; set; } = string.Empty;
        public string ChangeType { get; set; } = string.Empty; // "Added", "Removed", "Modified"
        public List<string>? BaselineOperations { get; set; }
        public List<string>? NewOperations { get; set; }
        public int? BaselineAttrCount { get; set; }
        public int? NewAttrCount { get; set; }
        public List<string> AttributeDifferences { get; set; } = new();
    }

    public class ComparisonResult
    {
        public bool IsGood { get; set; }
        public string Message { get; set; } = string.Empty;
        public List<string> Differences { get; set; } = new();
        public LdifStats BaselineStats { get; set; } = new();
        public LdifStats NewStats { get; set; } = new();
        public List<EntryDiff> EntryDiffs { get; set; } = new();
    }

    public static class LdifComparer
    {
        public static ComparisonResult Compare(
            string baselinePath, 
            string newPath,
            double entityThresholdPercent = 10.0,
            double attributeThresholdPercent = 10.0,
            double avgAttributesThresholdPercent = 15.0)
        {
            var baselineStats = AnalyzeLdif(baselinePath);
            var newStats = AnalyzeLdif(newPath);
            return CompareStats(baselineStats, newStats, entityThresholdPercent, attributeThresholdPercent, avgAttributesThresholdPercent);
        }

        public static ComparisonResult CompareStats(
            LdifStats baselineStats,
            LdifStats newStats,
            double entityThresholdPercent = 10.0,
            double attributeThresholdPercent = 10.0,
            double avgAttributesThresholdPercent = 15.0)
        {

            var result = new ComparisonResult
            {
                BaselineStats = baselineStats,
                NewStats = newStats,
                IsGood = true
            };

            // Compare total entities
            var entityDiffPercent = CalculatePercentageDiff(
                baselineStats.TotalEntries, 
                newStats.TotalEntries);
            
            if (Math.Abs(entityDiffPercent) > entityThresholdPercent)
            {
                result.IsGood = false;
                result.Differences.Add(
                    $"Entity count difference: {entityDiffPercent:+0.0;-0.0}% " +
                    $"(baseline: {baselineStats.TotalEntries}, new: {newStats.TotalEntries})");
            }

            // Compare total attributes
            var attributeDiffPercent = CalculatePercentageDiff(
                baselineStats.TotalAttributes, 
                newStats.TotalAttributes);
            
            if (Math.Abs(attributeDiffPercent) > attributeThresholdPercent)
            {
                result.IsGood = false;
                result.Differences.Add(
                    $"Total attribute count difference: {attributeDiffPercent:+0.0;-0.0}% " +
                    $"(baseline: {baselineStats.TotalAttributes}, new: {newStats.TotalAttributes})");
            }

            // Compare average attributes per entry
            var avgDiffPercent = CalculatePercentageDiff(
                baselineStats.AverageAttributesPerEntry, 
                newStats.AverageAttributesPerEntry);
            
            if (Math.Abs(avgDiffPercent) > avgAttributesThresholdPercent)
            {
                result.IsGood = false;
                result.Differences.Add(
                    $"Average attributes per entry difference: {avgDiffPercent:+0.0;-0.0}% " +
                    $"(baseline: {baselineStats.AverageAttributesPerEntry:F2}, " +
                    $"new: {newStats.AverageAttributesPerEntry:F2})");
            }

            // Compare attribute distribution
            var attributeDiffs = CompareAttributeDistribution(
                baselineStats.AttributeCounts, 
                newStats.AttributeCounts);
            
            if (attributeDiffs.Count > 0)
            {
                result.Differences.Add("Attribute distribution changes:");
                result.Differences.AddRange(attributeDiffs.Take(10)); // Limit to top 10
                if (attributeDiffs.Count > 10)
                {
                    result.Differences.Add($"  ... and {attributeDiffs.Count - 10} more");
                }
            }

            // Generate entry-level diffs
            result.EntryDiffs = GenerateEntryDiffs(baselineStats, newStats);

            result.Message = result.IsGood 
                ? "✓ New LDIF file appears good - within acceptable thresholds"
                : "✗ New LDIF file has significant differences from baseline";

            return result;
        }

        private static LdifStats AnalyzeLdif(string path)
        {
            using var reader = new StreamReader(
                path,
                Encoding.GetEncoding(20127),
                detectEncodingFromByteOrderMarks: false);
            return AnalyzeLdif(reader);
        }

        public static LdifStats AnalyzeLdif(TextReader reader)
        {
            var stats = new LdifStats();

            foreach (var record in LdifReader.Parse(reader))
            {
                stats.TotalEntries++;

                switch (record)
                {
                    case ChangeAdd add:
                        stats.TotalAddOperations++;
                        ProcessAddAttributes(add, stats);
                        break;
                    
                    case ChangeDelete del:
                        stats.TotalDeleteOperations++;
                        {
                            var dn = del.DistinguishedName;
                            if (!stats.Entries.TryGetValue(dn, out var entryInfo))
                            {
                                entryInfo = new EntryInfo { Dn = dn };
                                stats.Entries[dn] = entryInfo;
                            }
                            entryInfo.OperationTypes.Add("Delete");
                        }
                        break;
                    
                    case ChangeModify mod:
                        stats.TotalModifyOperations++;
                        ProcessModifyAttributes(mod, stats);
                        break;
                    
                    case ChangeModDn modDn:
                        stats.TotalModDnOperations++;
                        {
                            var dn = modDn.DistinguishedName;
                            if (!stats.Entries.TryGetValue(dn, out var entryInfo))
                            {
                                entryInfo = new EntryInfo { Dn = dn };
                                stats.Entries[dn] = entryInfo;
                            }
                            entryInfo.OperationTypes.Add("ModDn");
                        }
                        break;
                }
            }

            return stats;
        }

        public static void ProcessAddAttributes(ChangeAdd add, LdifStats stats)
        {
            var entryAttrCount = 0;
            var dn = add.DistinguishedName;
            
            if (!stats.Entries.TryGetValue(dn, out var entryInfo))
            {
                entryInfo = new EntryInfo { Dn = dn };
                stats.Entries[dn] = entryInfo;
            }
            
            entryInfo.OperationTypes.Add("Add");
            
            foreach (var attr in add)
            {
                var attrName = attr.AttributeType;
                var valueCount = attr.Count();
                
                stats.TotalAttributes += valueCount;
                entryAttrCount += valueCount;
                
                if (!stats.AttributeCounts.ContainsKey(attrName))
                {
                    stats.AttributeCounts[attrName] = 0;
                }
                stats.AttributeCounts[attrName] += valueCount;
                
                if (!entryInfo.Attributes.ContainsKey(attrName))
                {
                    entryInfo.Attributes[attrName] = 0;
                }
                entryInfo.Attributes[attrName] += valueCount;
            }
            
            entryInfo.TotalAttributeCount += entryAttrCount;
            
            if (!stats.EntryAttributeCounts.ContainsKey(dn))
            {
                stats.EntryAttributeCounts[dn] = 0;
            }
            stats.EntryAttributeCounts[dn] += entryAttrCount;
        }

        public static void ProcessModifyAttributes(ChangeModify mod, LdifStats stats)
        {
            var entryAttrCount = 0;
            var dn = mod.DistinguishedName;
            
            if (!stats.Entries.TryGetValue(dn, out var entryInfo))
            {
                entryInfo = new EntryInfo { Dn = dn };
                stats.Entries[dn] = entryInfo;
            }
            
            entryInfo.OperationTypes.Add("Modify");
            
            foreach (var spec in mod.ModSpecs)
            {
                var attrName = spec.AttributeType;
                var valueCount = spec.Count();
                
                stats.TotalAttributes += valueCount;
                entryAttrCount += valueCount;
                
                if (!stats.AttributeCounts.ContainsKey(attrName))
                {
                    stats.AttributeCounts[attrName] = 0;
                }
                stats.AttributeCounts[attrName] += valueCount;
                
                if (!entryInfo.Attributes.ContainsKey(attrName))
                {
                    entryInfo.Attributes[attrName] = 0;
                }
                entryInfo.Attributes[attrName] += valueCount;
            }
            
            entryInfo.TotalAttributeCount += entryAttrCount;
            
            if (!stats.EntryAttributeCounts.ContainsKey(dn))
            {
                stats.EntryAttributeCounts[dn] = 0;
            }
            stats.EntryAttributeCounts[dn] += entryAttrCount;
        }

        public static double CalculatePercentageDiff(double baseline, double newValue)
        {
            if (baseline == 0)
            {
                return newValue == 0 ? 0 : 100;
            }
            return ((newValue - baseline) / baseline) * 100.0;
        }

        public static List<string> CompareAttributeDistribution(
            Dictionary<string, int> baseline,
            Dictionary<string, int> newCounts)
        {
            var diffs = new List<string>();
            var allAttrs = new HashSet<string>(baseline.Keys);
            allAttrs.UnionWith(newCounts.Keys);

            foreach (var attr in allAttrs.OrderBy(a => a))
            {
                var baseCount = baseline.GetValueOrDefault(attr, 0);
                var newCount = newCounts.GetValueOrDefault(attr, 0);
                
                if (baseCount == 0 && newCount > 0)
                {
                    diffs.Add($"  + {attr}: new attribute ({newCount} values)");
                }
                else if (baseCount > 0 && newCount == 0)
                {
                    diffs.Add($"  - {attr}: removed ({baseCount} values)");
                }
                else if (baseCount != newCount)
                {
                    var diffPercent = CalculatePercentageDiff(baseCount, newCount);
                    if (Math.Abs(diffPercent) > 20) // Only show significant attribute changes
                    {
                        diffs.Add($"  ~ {attr}: {diffPercent:+0.0;-0.0}% ({baseCount} → {newCount})");
                    }
                }
            }

            return diffs;
        }

        public static List<EntryDiff> GenerateEntryDiffs(LdifStats baseline, LdifStats newStats)
        {
            var diffs = new List<EntryDiff>();
            var allDns = new HashSet<string>(baseline.Entries.Keys);
            allDns.UnionWith(newStats.Entries.Keys);

            foreach (var dn in allDns.OrderBy(d => d))
            {
                var hasBaseline = baseline.Entries.TryGetValue(dn, out var baseEntry);
                var hasNew = newStats.Entries.TryGetValue(dn, out var newEntry);

                if (!hasBaseline && hasNew)
                {
                    // Entry added in new file
                    diffs.Add(new EntryDiff
                    {
                        Dn = dn,
                        ChangeType = "Added",
                        NewOperations = newEntry!.OperationTypes,
                        NewAttrCount = newEntry.TotalAttributeCount,
                        AttributeDifferences = newEntry.Attributes
                            .Select(kvp => $"  + {kvp.Key}: {kvp.Value} value(s)")
                            .ToList()
                    });
                }
                else if (hasBaseline && !hasNew)
                {
                    // Entry removed in new file
                    diffs.Add(new EntryDiff
                    {
                        Dn = dn,
                        ChangeType = "Removed",
                        BaselineOperations = baseEntry!.OperationTypes,
                        BaselineAttrCount = baseEntry.TotalAttributeCount,
                        AttributeDifferences = baseEntry.Attributes
                            .Select(kvp => $"  - {kvp.Key}: {kvp.Value} value(s)")
                            .ToList()
                    });
                }
                else if (hasBaseline && hasNew && baseEntry != null && newEntry != null)
                {
                    // Entry exists in both - check for modifications
                    var attrDiffs = CompareEntryAttributes(baseEntry, newEntry);
                    var opsDiff = !baseEntry.OperationTypes.SequenceEqual(newEntry.OperationTypes);
                    if (attrDiffs.Count > 0 || baseEntry.TotalAttributeCount != newEntry.TotalAttributeCount || opsDiff)
                    {
                        diffs.Add(new EntryDiff
                        {
                            Dn = dn,
                            ChangeType = "Modified",
                            BaselineOperations = baseEntry.OperationTypes,
                            NewOperations = newEntry.OperationTypes,
                            BaselineAttrCount = baseEntry.TotalAttributeCount,
                            NewAttrCount = newEntry.TotalAttributeCount,
                            AttributeDifferences = attrDiffs
                        });
                    }
                }
            }

            return diffs;
        }

        public static List<string> CompareEntryAttributes(EntryInfo baseline, EntryInfo newEntry)
        {
            var diffs = new List<string>();
            var allAttrs = new HashSet<string>(baseline.Attributes.Keys);
            allAttrs.UnionWith(newEntry.Attributes.Keys);

            foreach (var attr in allAttrs.OrderBy(a => a))
            {
                var baseCount = baseline.Attributes.GetValueOrDefault(attr, 0);
                var newCount = newEntry.Attributes.GetValueOrDefault(attr, 0);

                if (baseCount == 0 && newCount > 0)
                {
                    diffs.Add($"  + {attr}: {newCount} value(s)");
                }
                else if (baseCount > 0 && newCount == 0)
                {
                    diffs.Add($"  - {attr}: {baseCount} value(s)");
                }
                else if (baseCount != newCount)
                {
                    diffs.Add($"  ~ {attr}: {baseCount} → {newCount} value(s)");
                }
            }

            return diffs;
        }

        public static void PrintComparison(ComparisonResult result, string? detailedDiffPath = null)
        {
            Console.WriteLine("=== LDIF Comparison Report ===");
            Console.WriteLine();
            Console.WriteLine(result.Message);
            Console.WriteLine();

            Console.WriteLine("Baseline Statistics:");
            PrintStats(result.BaselineStats, "  ");
            Console.WriteLine();

            Console.WriteLine("New File Statistics:");
            PrintStats(result.NewStats, "  ");
            Console.WriteLine();

            if (result.Differences.Count > 0)
            {
                Console.WriteLine("Detected Differences:");
                foreach (var diff in result.Differences)
                {
                    Console.WriteLine(diff);
                }
                Console.WriteLine();
            }

            if (result.EntryDiffs.Count > 0)
            {
                Console.WriteLine($"Entry-level differences: {result.EntryDiffs.Count} entries changed");
                
                if (!string.IsNullOrEmpty(detailedDiffPath))
                {
                    var isJsonl = detailedDiffPath.EndsWith(".jsonl", StringComparison.OrdinalIgnoreCase);
                    var format = isJsonl ? "JSONL" : "text";
                    WriteDetailedDiff(result.EntryDiffs, detailedDiffPath);
                    Console.WriteLine($"Detailed diff written to: {detailedDiffPath} ({format} format)");
                }
                else
                {
                    Console.WriteLine("(Use --output flag to save detailed differences to a file)");
                    Console.WriteLine("(Use .jsonl extension for machine-readable JSON Lines format)");
                }
                Console.WriteLine();
            }

            Console.WriteLine("=== End of Report ===");
        }

        private static void WriteDetailedDiff(List<EntryDiff> diffs, string outputPath)
        {
            // Determine output format based on file extension
            var isJsonl = outputPath.EndsWith(".jsonl", StringComparison.OrdinalIgnoreCase);
            
            if (isJsonl)
            {
                WriteDetailedDiffJsonl(diffs, outputPath);
            }
            else
            {
                WriteDetailedDiffText(diffs, outputPath);
            }
        }

        private static void WriteDetailedDiffJsonl(List<EntryDiff> diffs, string outputPath)
        {
            using var writer = new StreamWriter(outputPath, false, new UTF8Encoding(false));
            
            foreach (var diff in diffs)
            {
                var jsonBytes = JsonSerializer.SerializeToUtf8Bytes(diff, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    WriteIndented = false
                });
                writer.WriteLine(Encoding.UTF8.GetString(jsonBytes));
            }
        }

        private static void WriteDetailedDiffText(List<EntryDiff> diffs, string outputPath)
        {
            using var writer = new StreamWriter(outputPath, false, Encoding.UTF8);
            
            writer.WriteLine("=== Detailed Entry-Level Differences ===");
            writer.WriteLine();

            foreach (var diff in diffs)
            {
                writer.WriteLine($"[{diff.ChangeType.ToUpper()}] {diff.Dn}");
                
                if (diff.BaselineOperations != null && diff.BaselineOperations.Count > 0)
                {
                    writer.WriteLine($"  Baseline: {string.Join(", ", diff.BaselineOperations)} operation(s), {diff.BaselineAttrCount} attribute(s)");
                }
                
                if (diff.NewOperations != null && diff.NewOperations.Count > 0)
                {
                    writer.WriteLine($"  New:      {string.Join(", ", diff.NewOperations)} operation(s), {diff.NewAttrCount} attribute(s)");
                }
                
                if (diff.AttributeDifferences.Count > 0)
                {
                    writer.WriteLine("  Attributes:");
                    foreach (var attrDiff in diff.AttributeDifferences)
                    {
                        writer.WriteLine($"  {attrDiff}");
                    }
                }
                
                writer.WriteLine();
            }
            
            writer.WriteLine($"Total differences: {diffs.Count} entries");
        }

        private static void PrintStats(LdifStats stats, string indent = "")
        {
            Console.WriteLine($"{indent}Total entries: {stats.TotalEntries}");
            Console.WriteLine($"{indent}  - Add operations: {stats.TotalAddOperations}");
            Console.WriteLine($"{indent}  - Modify operations: {stats.TotalModifyOperations}");
            Console.WriteLine($"{indent}  - Delete operations: {stats.TotalDeleteOperations}");
            Console.WriteLine($"{indent}  - ModDn operations: {stats.TotalModDnOperations}");
            Console.WriteLine($"{indent}Total attributes: {stats.TotalAttributes}");
            Console.WriteLine($"{indent}Average attributes per entry: {stats.AverageAttributesPerEntry:F2}");
            Console.WriteLine($"{indent}Unique attribute types: {stats.AttributeCounts.Count}");
        }
    }
}
