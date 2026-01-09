using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using LdifDiffDemo;

namespace LdifDiffAnalyzer
{
    internal class Program
    {
        static int Main(string[] args)
        {
            if (args.Length < 1)
            {
                PrintUsage();
                return 1;
            }

            var jsonlFile = args[0];
            
            if (!File.Exists(jsonlFile))
            {
                Console.Error.WriteLine($"Error: File '{jsonlFile}' not found");
                return 1;
            }

            try
            {
                var diffs = LoadJsonl(jsonlFile);
                Console.WriteLine($"Loaded {diffs.Count} entries from {jsonlFile}");
                Console.WriteLine();

                // Determine which reports to show
                var showAll = args.Contains("--all");
                var showSummary = showAll || args.Contains("--summary") || args.Length == 1;
                var showAdded = showAll || args.Contains("--added");
                var showRemoved = showAll || args.Contains("--removed");
                var showModified = showAll || args.Contains("--modified");

                if (showSummary)
                {
                    ShowSummary(diffs);
                }

                if (showAdded)
                {
                    ShowAddedEntries(diffs);
                }

                if (showRemoved)
                {
                    ShowRemovedEntries(diffs);
                }

                if (showModified)
                {
                    ShowModifiedEntries(diffs);
                }

                // Handle --find
                var findIndex = Array.IndexOf(args, "--find");
                if (findIndex >= 0 && findIndex + 1 < args.Length)
                {
                    var pattern = args[findIndex + 1];
                    FindEntriesByPattern(diffs, pattern);
                }

                // Handle --csv
                var csvIndex = Array.IndexOf(args, "--csv");
                if (csvIndex >= 0 && csvIndex + 1 < args.Length)
                {
                    var csvFile = args[csvIndex + 1];
                    ExportToCsv(diffs, csvFile);
                }

                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error: {ex.Message}");
                return 1;
            }
        }

        static void PrintUsage()
        {
            Console.Error.WriteLine("Usage: dotnet run --project LdifDiffAnalyzer/LdifDiffAnalyzer.csproj -- <diff.jsonl> [options]");
            Console.Error.WriteLine();
            Console.Error.WriteLine("Options:");
            Console.Error.WriteLine("  --summary              Show summary statistics (default)");
            Console.Error.WriteLine("  --added                Show added entries");
            Console.Error.WriteLine("  --removed              Show removed entries");
            Console.Error.WriteLine("  --modified             Show modified entries");
            Console.Error.WriteLine("  --find <pattern>       Find entries matching pattern");
            Console.Error.WriteLine("  --csv <output.csv>     Export to CSV");
            Console.Error.WriteLine("  --all                  Show all reports");
        }

        static List<EntryDiff> LoadJsonl(string filepath)
        {
            var diffs = new List<EntryDiff>();
            var lineNum = 0;

            foreach (var line in File.ReadLines(filepath))
            {
                lineNum++;
                try
                {
                    var diff = JsonSerializer.Deserialize<EntryDiff>(line, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });
                    
                    if (diff != null)
                    {
                        diffs.Add(diff);
                    }
                }
                catch (JsonException ex)
                {
                    Console.Error.WriteLine($"Warning: Error parsing line {lineNum}: {ex.Message}");
                }
            }

            return diffs;
        }

        static void ShowSummary(List<EntryDiff> diffs)
        {
            Console.WriteLine("=== Summary Report ===");
            Console.WriteLine();

            var changeTypes = diffs.GroupBy(d => d.ChangeType)
                                  .OrderBy(g => g.Key)
                                  .Select(g => new { Type = g.Key, Count = g.Count() });

            Console.WriteLine($"Total differences: {diffs.Count}");
            foreach (var ct in changeTypes)
            {
                Console.WriteLine($"  {ct.Type}: {ct.Count}");
            }
            Console.WriteLine();
        }

        static void ShowAddedEntries(List<EntryDiff> diffs)
        {
            Console.WriteLine("=== Added Entries ===");
            Console.WriteLine();

            var added = diffs.Where(d => d.ChangeType == "Added").ToList();
            Console.WriteLine($"Total added: {added.Count}");

            if (added.Any())
            {
                Console.WriteLine();
                Console.WriteLine("Sample entries (first 10):");
                foreach (var diff in added.Take(10))
                {
                    Console.WriteLine($"  - {diff.Dn}");
                    if (diff.NewOperations != null && diff.NewOperations.Count > 0)
                    {
                        Console.WriteLine($"    Operations: {string.Join(", ", diff.NewOperations)}");
                    }
                    Console.WriteLine($"    Attributes: {diff.NewAttrCount ?? 0}");
                }
            }
            Console.WriteLine();
        }

        static void ShowRemovedEntries(List<EntryDiff> diffs)
        {
            Console.WriteLine("=== Removed Entries ===");
            Console.WriteLine();

            var removed = diffs.Where(d => d.ChangeType == "Removed").ToList();
            Console.WriteLine($"Total removed: {removed.Count}");

            if (removed.Any())
            {
                Console.WriteLine();
                Console.WriteLine("Sample entries (first 10):");
                foreach (var diff in removed.Take(10))
                {
                    Console.WriteLine($"  - {diff.Dn}");
                    if (diff.BaselineOperations != null && diff.BaselineOperations.Count > 0)
                    {
                        Console.WriteLine($"    Operations: {string.Join(", ", diff.BaselineOperations)}");
                    }
                    Console.WriteLine($"    Attributes: {diff.BaselineAttrCount ?? 0}");
                }
            }
            Console.WriteLine();
        }

        static void ShowModifiedEntries(List<EntryDiff> diffs)
        {
            Console.WriteLine("=== Modified Entries ===");
            Console.WriteLine();

            var modified = diffs.Where(d => d.ChangeType == "Modified").ToList();
            Console.WriteLine($"Total modified: {modified.Count}");

            if (modified.Any())
            {
                // Count attribute changes
                int attrAddCount = 0, attrRemoveCount = 0, attrChangeCount = 0;

                foreach (var diff in modified)
                {
                    if (diff.AttributeDifferences == null) continue;
                    
                    foreach (var attrDiff in diff.AttributeDifferences)
                    {
                        if (attrDiff.Contains("  + "))
                            attrAddCount++;
                        else if (attrDiff.Contains("  - "))
                            attrRemoveCount++;
                        else if (attrDiff.Contains("  ~ "))
                            attrChangeCount++;
                    }
                }

                Console.WriteLine();
                Console.WriteLine("Attribute changes:");
                Console.WriteLine($"  Added attributes: {attrAddCount}");
                Console.WriteLine($"  Removed attributes: {attrRemoveCount}");
                Console.WriteLine($"  Modified attributes: {attrChangeCount}");

                Console.WriteLine();
                Console.WriteLine("Sample modified entries (first 5):");
                foreach (var diff in modified.Take(5))
                {
                    Console.WriteLine($"  - {diff.Dn}");
                    if (diff.AttributeDifferences != null && diff.AttributeDifferences.Count > 0)
                    {
                        foreach (var attrDiff in diff.AttributeDifferences.Take(3))
                        {
                            Console.WriteLine($"    {attrDiff.Trim()}");
                        }
                        if (diff.AttributeDifferences.Count > 3)
                        {
                            Console.WriteLine($"    ... and {diff.AttributeDifferences.Count - 3} more");
                        }
                    }
                }
            }
            Console.WriteLine();
        }

        static void FindEntriesByPattern(List<EntryDiff> diffs, string pattern)
        {
            Console.WriteLine($"=== Entries matching '{pattern}' ===");
            Console.WriteLine();

            var matches = diffs.Where(d => d.Dn.Contains(pattern, StringComparison.OrdinalIgnoreCase))
                              .ToList();
            Console.WriteLine($"Found {matches.Count} matching entries");

            if (matches.Any())
            {
                Console.WriteLine();
                foreach (var diff in matches.Take(20))
                {
                    Console.WriteLine($"  [{diff.ChangeType}] {diff.Dn}");
                    var attrCount = diff.AttributeDifferences?.Count ?? 0;
                    if (attrCount > 0)
                    {
                        Console.WriteLine($"    {attrCount} attribute changes");
                    }
                }

                if (matches.Count > 20)
                {
                    Console.WriteLine($"  ... and {matches.Count - 20} more");
                }
            }
            Console.WriteLine();
        }

        static void ExportToCsv(List<EntryDiff> diffs, string outputFile)
        {
            using var writer = new StreamWriter(outputFile);

            // Write header
            writer.WriteLine("DN,Change Type,Baseline Operations,New Operations,Baseline Attr Count,New Attr Count,Attr Changes");

            // Write data
            foreach (var diff in diffs)
            {
                var baselineOps = diff.BaselineOperations != null 
                    ? string.Join(";", diff.BaselineOperations) 
                    : "";
                var newOps = diff.NewOperations != null 
                    ? string.Join(";", diff.NewOperations) 
                    : "";
                var attrChanges = diff.AttributeDifferences?.Count ?? 0;

                writer.WriteLine($"\"{EscapeCsv(diff.Dn)}\",\"{diff.ChangeType}\",\"{baselineOps}\",\"{newOps}\",{diff.BaselineAttrCount ?? 0},{diff.NewAttrCount ?? 0},{attrChanges}");
            }

            Console.WriteLine($"Exported {diffs.Count} entries to {outputFile}");
        }

        static string EscapeCsv(string value)
        {
            if (value == null) return "";
            return value.Replace("\"", "\"\"");
        }
    }
}
