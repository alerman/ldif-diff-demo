using System;
using System.IO;

namespace LdifTestGenerator
{
    internal class Program
    {
        private static readonly string[] Departments = { "Engineering", "Sales", "Marketing", "HR", "Finance", "Operations", "IT", "Legal" };
        private static readonly string[] Locations = { "New York", "San Francisco", "London", "Tokyo", "Berlin", "Sydney" };
        private static readonly string[] Domains = { "example.com", "test.org", "demo.net" };
        private static readonly string[] Titles = { "Engineer", "Manager", "Director", "Analyst", "Specialist" };
        private static readonly Random Rand = new Random(42); // Fixed seed for reproducibility

        static void Main(string[] args)
        {
            Console.WriteLine("Generating large LDIF test files...");
            Console.WriteLine();

            GenerateBaselineLdif(5000, "test-data/baseline_large.ldif");
            GenerateSimilarLdif(5000, "test-data/similar_large.ldif");
            GenerateDifferentLdif(5000, "test-data/different_large.ldif");
            GenerateComplexOperationsLdif(2000, "test-data/complex_operations.ldif");
            GenerateVeryLargeLdif(25000, "test-data/very_large.ldif");

            Console.WriteLine();
            Console.WriteLine("All test files generated successfully!");
            Console.WriteLine();
            Console.WriteLine("Test scenarios:");
            Console.WriteLine("  1. baseline_large.ldif vs similar_large.ldif - Should PASS (2% change)");
            Console.WriteLine("  2. baseline_large.ldif vs different_large.ldif - Should FAIL (30% increase)");
            Console.WriteLine("  3. complex_operations.ldif - Complex add/modify/delete operations");
            Console.WriteLine("  4. very_large.ldif - Stress test with 25,000 entries");
        }

        static void GenerateBaselineLdif(int numEntries, string filename)
        {
            using var writer = new StreamWriter(filename);
            writer.WriteLine("version: 1\n");

            for (int i = 0; i < numEntries; i++)
            {
                WriteUserAdd(writer, i);
            }

            Console.WriteLine($"Generated {filename} with {numEntries} entries");
        }

        static void GenerateSimilarLdif(int numEntries, string filename)
        {
            using var writer = new StreamWriter(filename);
            writer.WriteLine("version: 1\n");

            var numModified = (int)(numEntries * 0.02); // 2% modifications
            var modifiedIndices = new HashSet<int>();
            while (modifiedIndices.Count < numModified)
            {
                modifiedIndices.Add(Rand.Next(numEntries));
            }

            for (int i = 0; i < numEntries; i++)
            {
                WriteUserAdd(writer, i);

                if (modifiedIndices.Contains(i))
                {
                    WriteUserModify(writer, i);
                }
            }

            Console.WriteLine($"Generated {filename} with {numEntries} entries and {numModified} modifications (2% change)");
        }

        static void GenerateDifferentLdif(int numEntries, string filename)
        {
            var newNumEntries = (int)(numEntries * 1.3); // 30% more

            using var writer = new StreamWriter(filename);
            writer.WriteLine("version: 1\n");

            for (int i = 0; i < newNumEntries; i++)
            {
                WriteUserAdd(writer, i, i >= numEntries); // Extra attrs for new entries
            }

            Console.WriteLine($"Generated {filename} with {newNumEntries} entries (+30% increase)");
        }

        static void GenerateComplexOperationsLdif(int numEntries, string filename)
        {
            using var writer = new StreamWriter(filename);
            writer.WriteLine("version: 1\n");

            // Add users
            for (int i = 0; i < numEntries; i++)
            {
                WriteUserAdd(writer, i, false, 6); // Fewer attributes
            }

            // Modify 1/3 of users
            for (int i = 0; i < numEntries; i += 3)
            {
                WriteUserModify(writer, i);
            }

            // Delete 1/10 of users
            for (int i = 0; i < numEntries; i += 10)
            {
                WriteUserDelete(writer, i);
            }

            var totalOps = numEntries + (numEntries / 3) + (numEntries / 10);
            Console.WriteLine($"Generated {filename} with {totalOps} total operations (add/modify/delete)");
        }

        static void GenerateVeryLargeLdif(int numEntries, string filename)
        {
            using var writer = new StreamWriter(filename);
            writer.WriteLine("version: 1\n");

            for (int i = 0; i < numEntries; i++)
            {
                WriteUserAdd(writer, i, false, 17); // More attributes

                if ((i + 1) % 5000 == 0)
                {
                    Console.Error.WriteLine($"  Progress: {i + 1}/{numEntries} entries written...");
                }
            }

            Console.WriteLine($"Generated {filename} with {numEntries} entries");
        }

        static void WriteUserAdd(StreamWriter writer, int index, bool extraAttrs = false, int? attrCount = null)
        {
            var userId = $"user{index:D6}";
            var firstName = $"FirstName{index}";
            var lastName = $"LastName{index}";
            var dept = Departments[Rand.Next(Departments.Length)];
            var location = Locations[Rand.Next(Locations.Length)];
            var domain = Domains[Rand.Next(Domains.Length)];
            var domainParts = domain.Split('.');

            writer.WriteLine($"dn: cn={userId},ou=Users,dc={domainParts[0]},dc={domainParts[1]}");
            writer.WriteLine("changetype: add");
            writer.WriteLine("objectClass: inetOrgPerson");
            writer.WriteLine("objectClass: organizationalPerson");
            writer.WriteLine("objectClass: person");
            
            if (attrCount.HasValue && attrCount.Value >= 17)
            {
                writer.WriteLine("objectClass: top");
            }
            
            writer.WriteLine($"cn: {userId}");
            writer.WriteLine($"sn: {lastName}");
            writer.WriteLine($"givenName: {firstName}");
            writer.WriteLine($"displayName: {firstName} {lastName}");
            writer.WriteLine($"mail: {userId}@{domain}");
            writer.WriteLine($"telephoneNumber: +1-555-{index % 10000:D4}");
            writer.WriteLine($"department: {dept}");
            writer.WriteLine($"l: {location}");
            writer.WriteLine($"employeeNumber: EMP{index:D6}");
            writer.WriteLine($"title: {Titles[Rand.Next(Titles.Length)]}");

            if (attrCount.HasValue && attrCount.Value >= 17)
            {
                writer.WriteLine($"mobile: +1-555-{(index + 10000) % 10000:D4}");
                writer.WriteLine($"description: Employee record for {firstName} {lastName}");
                writer.WriteLine($"userPrincipalName: {userId}@{domain}");
            }

            if (extraAttrs)
            {
                writer.WriteLine($"mobile: +1-555-{index % 1000:D4}");
                writer.WriteLine("description: New user added in migration");
            }

            writer.WriteLine();
        }

        static void WriteUserModify(StreamWriter writer, int index)
        {
            var userId = $"user{index:D6}";
            var domain = Domains[Rand.Next(Domains.Length)];
            var domainParts = domain.Split('.');

            writer.WriteLine($"dn: cn={userId},ou=Users,dc={domainParts[0]},dc={domainParts[1]}");
            writer.WriteLine("changetype: modify");
            writer.WriteLine("replace: telephoneNumber");
            writer.WriteLine($"telephoneNumber: +1-555-{(index + 1000) % 10000:D4}");
            writer.WriteLine("-");
            writer.WriteLine();
        }

        static void WriteUserDelete(StreamWriter writer, int index)
        {
            var userId = $"user{index:D6}";
            var domain = Domains[Rand.Next(Domains.Length)];
            var domainParts = domain.Split('.');

            writer.WriteLine($"dn: cn={userId},ou=Users,dc={domainParts[0]},dc={domainParts[1]}");
            writer.WriteLine("changetype: delete");
            writer.WriteLine();
        }
    }
}
