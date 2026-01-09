using System;
using System.IO;
using System.Text.Json;
using LdifDiffDemo;

/// <summary>
/// Simple example showing how to read and display LDIF diffs from JSONL output
/// 
/// Compile and run:
///   csc /reference:LdifDiffDemo/bin/Debug/net8.0/LdifDiffDemo.dll SimpleDiffExample.cs
///   ./SimpleDiffExample.exe example-diff.jsonl
/// 
/// Or with dotnet:
///   dotnet-script SimpleDiffExample.cs example-diff.jsonl
/// </summary>
class SimpleDiffExample
{
    static void Main(string[] args)
    {
        if (args.Length < 1)
        {
            Console.WriteLine("Usage: SimpleDiffExample <diff.jsonl>");
            return;
        }

        var jsonlFile = args[0];
        
        Console.WriteLine($"Reading diffs from: {jsonlFile}");
        Console.WriteLine();
        Console.WriteLine("=" + new string('=', 70));
        Console.WriteLine();

        int lineNum = 0;
        foreach (var line in File.ReadLines(jsonlFile))
        {
            lineNum++;
            try
            {
                // Parse JSON line
                var diff = JsonSerializer.Deserialize<EntryDiff>(line, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (diff == null) continue;

                // Display the diff in a simple, readable format
                ShowDiff(diff, lineNum);
                Console.WriteLine();
            }
            catch (JsonException ex)
            {
                Console.Error.WriteLine($"Error parsing line {lineNum}: {ex.Message}");
            }
        }
    }

    static void ShowDiff(EntryDiff diff, int number)
    {
        // Show entry number and change type with color indicator
        var indicator = diff.ChangeType switch
        {
            "Added" => "[+]",
            "Removed" => "[-]",
            "Modified" => "[~]",
            _ => "[?]"
        };

        Console.WriteLine($"#{number} {indicator} {diff.ChangeType.ToUpper()}");
        Console.WriteLine($"DN: {diff.Dn}");
        Console.WriteLine();

        // Show baseline (before) information
        if (diff.BaselineOperations != null && diff.BaselineOperations.Count > 0)
        {
            Console.WriteLine($"  BEFORE:");
            Console.WriteLine($"    Operations: {string.Join(", ", diff.BaselineOperations)}");
            Console.WriteLine($"    Attributes: {diff.BaselineAttrCount ?? 0}");
        }

        // Show new (after) information
        if (diff.NewOperations != null && diff.NewOperations.Count > 0)
        {
            Console.WriteLine($"  AFTER:");
            Console.WriteLine($"    Operations: {string.Join(", ", diff.NewOperations)}");
            Console.WriteLine($"    Attributes: {diff.NewAttrCount ?? 0}");
        }

        // Show attribute-level changes
        if (diff.AttributeDifferences != null && diff.AttributeDifferences.Count > 0)
        {
            Console.WriteLine();
            Console.WriteLine("  ATTRIBUTE CHANGES:");
            foreach (var attrDiff in diff.AttributeDifferences)
            {
                Console.WriteLine($"    {attrDiff.Trim()}");
            }
        }
    }
}
