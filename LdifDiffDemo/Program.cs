using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using LdifHelper;

namespace LdifDiffDemo
{
    public enum ChangeOperation
    {
        AddEntry,
        DeleteEntry,
        ModifyEntry,
        ModifyDn
    }

    public enum AttributeOperation
    {
        Add,
        Delete,
        DeleteAll,
        Replace
    }

    public class AttributeChangeDto
    {
        public string AttributeName { get; set; } = string.Empty;
        public AttributeOperation Operation { get; set; }
        public List<string> Values { get; set; } = new();
    }

    public class ChangeRecordDto
    {
        public string Dn { get; set; } = string.Empty;
        public ChangeOperation Operation { get; set; }
        public List<AttributeChangeDto> AttributeChanges { get; set; } = new();
        public bool IsUser { get; set; }
        public string? NewRdn { get; set; }
        public string? NewSuperior { get; set; }
    }

    internal class LdifSummary
    {
        public int TotalEntriesAdded { get; set; }
        public int TotalEntriesDeleted { get; set; }
        public int UsersAdded { get; set; }
        public int UsersDeleted { get; set; }

        public int ModifyOperations { get; set; }
        public int ModifyDnOperations { get; set; }
        public int UserModifyOperations { get; set; }
        public int UserDnModifications { get; set; }

        public int AttributeValuesAdded { get; set; }
        public int AttributeValuesDeleted { get; set; }
        public int AttributeValuesReplacedNewValues { get; set; }
        public int AttributesFullyCleared { get; set; }

        public HashSet<string> AttributesEverObserved { get; } =
            new(StringComparer.OrdinalIgnoreCase);
        public HashSet<string> AttributesTouched { get; } =
            new(StringComparer.OrdinalIgnoreCase);
        public HashSet<string> AttributesClearedEverywhereCandidates { get; } =
            new(StringComparer.OrdinalIgnoreCase);
        public HashSet<string> AttributesStillPresentAfterChanges { get; } =
            new(StringComparer.OrdinalIgnoreCase);
        public HashSet<string> AttributesLostFromAllModifiedEntries { get; set; } =
            new(StringComparer.OrdinalIgnoreCase);

        public List<string> EntriesDeletedDns { get; } = new();
        public List<string> OtherFailures { get; } = new();

        public double PercentageAttributesChanged { get; set; }
    }

    internal static class Program
    {
        static int CompareMode(string baselinePath, string newPath, string? detailedDiffPath = null)
        {
            if (!File.Exists(baselinePath))
            {
                Console.Error.WriteLine($"Baseline LDIF file not found: {baselinePath}");
                return 1;
            }

            if (!File.Exists(newPath))
            {
                Console.Error.WriteLine($"New LDIF file not found: {newPath}");
                return 1;
            }

            try
            {
                var result = LdifComparer.Compare(baselinePath, newPath);
                LdifComparer.PrintComparison(result, detailedDiffPath);
                return result.IsGood ? 0 : 1;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("Fatal error while comparing LDIF files:");
                Console.Error.WriteLine(ex);
                return 2;
            }
        }

        static int Main(string[] args)
        {
            if (args.Length < 1)
            {
                Console.Error.WriteLine("Usage:");
                Console.Error.WriteLine("  Analysis mode:   dotnet run -- <input.ldif> [output.jsonl]");
                Console.Error.WriteLine("  Comparison mode: dotnet run -- --compare <baseline.ldif> <new.ldif> [--output diff.txt]");
                Console.Error.WriteLine("  Diff mode:       dotnet run -- --diff <baseline.ldif> <new.ldif> <output.diff>");
                return 1;
            }

            // Check for unified diff mode
            if (args[0] == "--diff" || args[0] == "-d")
            {
                if (args.Length < 4)
                {
                    Console.Error.WriteLine("Diff mode requires two LDIF files and an output path");
                    Console.Error.WriteLine("Usage: dotnet run -- --diff <baseline.ldif> <new.ldif> <output.diff>");
                    return 1;
                }

                try
                {
                    LdifUnifiedDiff.GenerateUnifiedDiff(args[1], args[2], args[3]);
                    Console.WriteLine($"Unified diff written to: {args[3]}");
                    return 0;
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine("Error generating unified diff:");
                    Console.Error.WriteLine(ex);
                    return 2;
                }
            }

            // Check for comparison mode
            if (args[0] == "--compare" || args[0] == "-c")
            {
                if (args.Length < 3)
                {
                    Console.Error.WriteLine("Comparison mode requires two LDIF files");
                    Console.Error.WriteLine("Usage: dotnet run -- --compare <baseline.ldif> <new.ldif> [--output diff.txt]");
                    return 1;
                }
                
                string? diffOutputPath = null;
                if (args.Length >= 5 && (args[3] == "--output" || args[3] == "-o"))
                {
                    diffOutputPath = args[4];
                }
                
                return CompareMode(args[1], args[2], diffOutputPath);
            }

            // Original analysis mode
            var inputPath = args[0];
            var outputPath = args.Length > 1
                ? args[1]
                : Path.ChangeExtension(inputPath, ".jsonl");

            if (!File.Exists(inputPath))
            {
                Console.Error.WriteLine($"LDIF file not found: {inputPath}");
                return 1;
            }

            try
            {
                using var ldifReader = new StreamReader(
                    inputPath,
                    Encoding.GetEncoding(20127), // ASCII (see LdifHelper docs)
                    detectEncodingFromByteOrderMarks: false);

                using var jsonStream = File.Create(outputPath);

                var summary = new LdifSummary();

                foreach (var record in LdifReader.Parse(ldifReader))
                {
                    var dto = MapRecordToDto(record, summary);
                    var jsonBytes = JsonSerializer.SerializeToUtf8Bytes(dto);
                    jsonStream.Write(jsonBytes, 0, jsonBytes.Length);
                    jsonStream.WriteByte((byte)'\n');
                }

                PrintSummary(summary, outputPath);
                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("Fatal error while processing LDIF:");
                Console.Error.WriteLine(ex);
                return 2;
            }
        }

        private static ChangeRecordDto MapRecordToDto(IChangeRecord record, LdifSummary summary)
        {
            return record switch
            {
                ChangeAdd add     => MapAdd(add, summary),
                ChangeDelete del  => MapDelete(del, summary),
                ChangeModify mod  => MapModify(mod, summary),
                ChangeModDn modDn => MapModDn(modDn, summary),
                _                 => MapUnknown(record, summary)
            };
        }

        private static ChangeRecordDto MapAdd(ChangeAdd add, LdifSummary summary)
        {
            summary.TotalEntriesAdded++;
            var isUser = IsUserAdd(add);
            if (isUser) summary.UsersAdded++;

            var dto = new ChangeRecordDto
            {
                Dn        = add.DistinguishedName,
                Operation = ChangeOperation.AddEntry,
                IsUser    = isUser
            };

            foreach (var attr in add)
            {
                var name = attr.AttributeType;
                summary.AttributesEverObserved.Add(name);
                summary.AttributesTouched.Add(name);

                var values = attr.Select(v => v?.ToString() ?? string.Empty).ToList();
                if (values.Count > 0)
                {
                    summary.AttributeValuesAdded += values.Count;
                }

                dto.AttributeChanges.Add(new AttributeChangeDto
                {
                    AttributeName = name,
                    Operation     = AttributeOperation.Add,
                    Values        = values
                });
            }

            return dto;
        }

        private static ChangeRecordDto MapDelete(ChangeDelete del, LdifSummary summary)
        {
            summary.TotalEntriesDeleted++;
            var isUser = IsUserDn(del.DistinguishedName);
            if (isUser) summary.UsersDeleted++;

            summary.EntriesDeletedDns.Add(del.DistinguishedName);

            return new ChangeRecordDto
            {
                Dn        = del.DistinguishedName,
                Operation = ChangeOperation.DeleteEntry,
                IsUser    = isUser
            };
        }

        private static ChangeRecordDto MapModify(ChangeModify mod, LdifSummary summary)
        {
            summary.ModifyOperations++;
            var isUser = IsUserDn(mod.DistinguishedName);
            if (isUser) summary.UserModifyOperations++;

            var dto = new ChangeRecordDto
            {
                Dn        = mod.DistinguishedName,
                Operation = ChangeOperation.ModifyEntry,
                IsUser    = isUser
            };

            foreach (var spec in mod.ModSpecs)
            {
                var attrName = spec.AttributeType;
                summary.AttributesEverObserved.Add(attrName);
                summary.AttributesTouched.Add(attrName);

                var values = spec.Select(v => v?.ToString() ?? string.Empty).ToList();
                AttributeOperation attrOp;

                switch (spec.ModSpecType)
                {
                    case ModSpecType.Add:
                        attrOp = AttributeOperation.Add;
                        summary.AttributeValuesAdded += values.Count;
                        break;

                    case ModSpecType.Delete:
                        if (values.Count == 0)
                        {
                            attrOp = AttributeOperation.DeleteAll;
                            summary.AttributesFullyCleared++;
                            summary.AttributesClearedEverywhereCandidates.Add(attrName);
                        }
                        else
                        {
                            attrOp = AttributeOperation.Delete;
                            summary.AttributeValuesDeleted += values.Count;
                        }
                        break;

                    case ModSpecType.Replace:
                        attrOp = AttributeOperation.Replace;
                        summary.AttributeValuesReplacedNewValues += values.Count;
                        summary.AttributesStillPresentAfterChanges.Add(attrName);
                        break;

                    default:
                        attrOp = AttributeOperation.Replace;
                        summary.OtherFailures.Add(
                            $"Unknown ModSpecType '{spec.ModSpecType}' for DN '{mod.DistinguishedName}' attribute '{attrName}'.");
                        break;
                }

                if (spec.ModSpecType == ModSpecType.Add || spec.ModSpecType == ModSpecType.Replace)
                {
                    summary.AttributesStillPresentAfterChanges.Add(attrName);
                }

                dto.AttributeChanges.Add(new AttributeChangeDto
                {
                    AttributeName = attrName,
                    Operation     = attrOp,
                    Values        = values
                });
            }

            return dto;
        }

        private static ChangeRecordDto MapModDn(ChangeModDn modDn, LdifSummary summary)
        {
            summary.ModifyDnOperations++;
            var isUser = IsUserDn(modDn.DistinguishedName);
            if (isUser) summary.UserDnModifications++;

            return new ChangeRecordDto
            {
                Dn         = modDn.DistinguishedName,
                Operation  = ChangeOperation.ModifyDn,
                IsUser     = isUser,
                NewRdn     = modDn.NewRdn,
                NewSuperior = modDn.NewSuperior
            };
        }

        private static ChangeRecordDto MapUnknown(IChangeRecord record, LdifSummary summary)
        {
            summary.OtherFailures.Add($"Unknown record type: {record.GetType().FullName}");
            return new ChangeRecordDto
            {
                Dn        = string.Empty,
                Operation = ChangeOperation.ModifyEntry
            };
        }

        private static bool IsUserAdd(ChangeAdd changeAdd)
        {
            foreach (var attribute in changeAdd)
            {
                if (!attribute.AttributeType.Equals("objectClass", StringComparison.OrdinalIgnoreCase))
                    continue;

                foreach (var value in attribute)
                {
                    var s = value?.ToString();
                    if (s is null) continue;

                    if (s.Equals("user", StringComparison.OrdinalIgnoreCase) ||
                        s.Equals("inetOrgPerson", StringComparison.OrdinalIgnoreCase) ||
                        s.Equals("person", StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
            }

            return IsUserDn(changeAdd.DistinguishedName);
        }

        private static bool IsUserDn(string dn)
        {
            if (string.IsNullOrWhiteSpace(dn)) return false;

            return dn.IndexOf("OU=Users", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   dn.IndexOf("CN=Users", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static void PrintSummary(LdifSummary s, string outputPath)
        {
            if (s.AttributesEverObserved.Count > 0)
            {
                s.PercentageAttributesChanged =
                    (double)s.AttributesTouched.Count / s.AttributesEverObserved.Count * 100.0;
            }

            s.AttributesLostFromAllModifiedEntries =
                s.AttributesClearedEverywhereCandidates
                    .Except(s.AttributesStillPresentAfterChanges, StringComparer.OrdinalIgnoreCase)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);

            Console.WriteLine("=== LDIF Change Summary ===");
            Console.WriteLine($"Diff JSONL written to: {outputPath}");
            Console.WriteLine();
            Console.WriteLine($"Total entries added:   {s.TotalEntriesAdded}");
            Console.WriteLine($"Total entries deleted: {s.TotalEntriesDeleted}");
            Console.WriteLine($"Users added:           {s.UsersAdded}");
            Console.WriteLine($"Users deleted:         {s.UsersDeleted}");
            Console.WriteLine();
            Console.WriteLine($"Attribute values added:       {s.AttributeValuesAdded}");
            Console.WriteLine($"Attribute values deleted:     {s.AttributeValuesDeleted}");
            Console.WriteLine($"Attribute values in replaces: {s.AttributeValuesReplacedNewValues}");
            Console.WriteLine();
            Console.WriteLine($"Distinct attributes observed: {s.AttributesEverObserved.Count}");
            Console.WriteLine($"Distinct attributes touched:  {s.AttributesTouched.Count}");
            Console.WriteLine($"Percentage attributes changed: {s.PercentageAttributesChanged:F2}%");
            Console.WriteLine();

            if (s.AttributesLostFromAllModifiedEntries.Count > 0)
            {
                Console.WriteLine("Attributes that appear lost from all modified entries:");
                foreach (var attr in s.AttributesLostFromAllModifiedEntries.OrderBy(a => a))
                {
                    Console.WriteLine($"  - {attr}");
                }
            }

            if (s.OtherFailures.Count > 0)
            {
                Console.WriteLine();
                Console.WriteLine("Other failures / anomalies:");
                foreach (var f in s.OtherFailures)
                {
                    Console.WriteLine($"  - {f}");
                }
            }

            Console.WriteLine();
            Console.WriteLine("=== End of summary ===");
        }
    }
}
