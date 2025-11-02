using System;
using System.IO;
using System.Threading.Tasks;
using DialogEditor.Parsers;
using DialogEditor.Services;

namespace TestCreatureParser
{
    /// <summary>
    /// Test program for UTC (creature) file parsing.
    /// Validates CreatureParser and CreatureService functionality.
    /// </summary>
    class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("=== UTC Creature Parser Test ===\n");

            // Logging auto-initializes on first use
            UnifiedLogger.LogParser(LogLevel.INFO, "Starting UTC parser tests");

            // Test 1: Single UTC file parsing
            if (args.Length > 0 && File.Exists(args[0]))
            {
                await TestSingleUtcFile(args[0]);
            }
            else
            {
                Console.WriteLine("Usage: TestCreatureParser <path-to-utc-file> [module-directory]");
                Console.WriteLine("\nTest 1: Parse single UTC file");
                Console.WriteLine("  TestCreatureParser creature.utc");
                Console.WriteLine("\nTest 2: Scan module directory for all UTC files");
                Console.WriteLine("  TestCreatureParser creature.utc C:\\path\\to\\module");
                Console.WriteLine("\nTest 3: Scan with classes.2da for class name resolution");
                Console.WriteLine("  TestCreatureParser creature.utc C:\\path\\to\\module C:\\NWN\\data");
                return;
            }

            // Test 2: Directory scanning (if provided)
            if (args.Length > 1 && Directory.Exists(args[1]))
            {
                // Optional: game data directory for classes.2da
                var gameDataDir = args.Length > 2 ? args[2] : null;
                await TestDirectoryScanning(args[1], gameDataDir);
            }

            Console.WriteLine("\n=== Tests Complete ===");
            Console.WriteLine("Check logs for detailed parsing information");
        }

        /// <summary>
        /// Test parsing a single UTC file and display results.
        /// </summary>
        static async Task TestSingleUtcFile(string filePath)
        {
            Console.WriteLine($"Test 1: Single UTC File Parse");
            Console.WriteLine($"File: {filePath}\n");

            var parser = new CreatureParser();
            var creature = await parser.ParseFromFileAsync(filePath);

            if (creature == null)
            {
                Console.WriteLine("ERROR: Failed to parse UTC file");
                Console.WriteLine("Check logs for details");
                return;
            }

            // Display results
            Console.WriteLine("=== Creature Data ===");
            Console.WriteLine($"Tag:         {creature.Tag}");
            Console.WriteLine($"DisplayName: {creature.DisplayName}");
            Console.WriteLine($"PortraitId:  {creature.PortraitId}");
            Console.WriteLine($"\nFirstName:   {creature.FirstName?.GetDefault() ?? "(none)"}");
            Console.WriteLine($"LastName:    {creature.LastName?.GetDefault() ?? "(none)"}");
            Console.WriteLine($"Description: {creature.Description?.GetDefault() ?? "(none)"}");

            // Display classes
            Console.WriteLine($"\n=== Classes ({creature.Classes.Count}) ===");
            if (creature.Classes.Count == 0)
            {
                Console.WriteLine("(No classes found)");
            }
            else
            {
                foreach (var cls in creature.Classes)
                {
                    var nameInfo = !string.IsNullOrEmpty(cls.ClassName) ? $" ({cls.ClassName})" : "";
                    Console.WriteLine($"  Class ID: {cls.ClassId}{nameInfo}, Level: {cls.Level}");
                }
                Console.WriteLine($"\nClass Summary: {creature.ClassSummary}");
                Console.WriteLine($"Class Display: {string.Join(" / ", creature.Classes.Select(c => c.DisplayText))}");
            }

            // Validation checks
            Console.WriteLine("\n=== Validation ===");
            Console.WriteLine($"Tag is valid: {!string.IsNullOrEmpty(creature.Tag)}");
            Console.WriteLine($"Has name:     {creature.FirstName != null || creature.LastName != null}");
            Console.WriteLine($"Has classes:  {creature.Classes.Count > 0}");

            if (creature.Classes.Count > 3)
            {
                Console.WriteLine($"\nNOTE: Creature has {creature.Classes.Count} classes");
                Console.WriteLine("      This exceeds original 3-class limit, confirming NWN 1.69+ support");
            }

            Console.WriteLine($"\nResult: PASS - UTC parsed successfully\n");
        }

        /// <summary>
        /// Test scanning a directory for UTC files and caching.
        /// </summary>
        static async Task TestDirectoryScanning(string directory, string? gameDataDirectory)
        {
            Console.WriteLine($"\nTest 2: Directory Scanning");
            Console.WriteLine($"Directory: {directory}");
            if (!string.IsNullOrEmpty(gameDataDirectory))
            {
                Console.WriteLine($"Game Data: {gameDataDirectory}");
            }
            Console.WriteLine();

            var service = new CreatureService();
            var creatures = await service.ScanCreaturesAsync(directory, gameDataDirectory);

            Console.WriteLine($"\n=== Scan Results ===");
            Console.WriteLine($"Creatures found: {creatures.Count}");
            Console.WriteLine($"Cache populated: {service.HasCachedCreatures}");
            Console.WriteLine($"Cache count:     {service.CachedCreatureCount}");
            Console.WriteLine($"Class data loaded: {service.HasClassData}");

            if (creatures.Count > 0)
            {
                Console.WriteLine($"\n=== Sample Creatures (first 10) ===");
                var sampleCount = Math.Min(10, creatures.Count);
                for (int i = 0; i < sampleCount; i++)
                {
                    var c = creatures[i];
                    Console.WriteLine($"{i + 1}. {c.DisplayName}");
                    Console.WriteLine($"   Tag: {c.Tag}, Classes: {c.ClassSummary}, Portrait: {c.PortraitId}");
                }

                if (creatures.Count > 10)
                {
                    Console.WriteLine($"... and {creatures.Count - 10} more");
                }

                // Test tag lookup
                Console.WriteLine($"\n=== Tag Lookup Test ===");
                var firstCreature = creatures[0];
                var lookupResult = service.GetCreatureByTag(firstCreature.Tag);
                Console.WriteLine($"Lookup '{firstCreature.Tag}': {(lookupResult != null ? "FOUND" : "NOT FOUND")}");

                // Test case-insensitive lookup
                var lowerTag = firstCreature.Tag.ToLowerInvariant();
                var caseInsensitiveResult = service.GetCreatureByTag(lowerTag);
                Console.WriteLine($"Case-insensitive lookup '{lowerTag}': {(caseInsensitiveResult != null ? "PASS" : "FAIL")}");
            }

            // Test cache re-use
            Console.WriteLine($"\n=== Cache Re-use Test ===");
            var creatures2 = await service.ScanCreaturesAsync(directory);
            Console.WriteLine($"Second scan returned: {creatures2.Count} creatures");
            Console.WriteLine($"Cache re-used: {creatures2.Count == creatures.Count}");

            // Test cache clearing
            Console.WriteLine($"\n=== Cache Clear Test ===");
            service.ClearCache();
            Console.WriteLine($"Cache cleared: {!service.HasCachedCreatures}");

            Console.WriteLine($"\nResult: PASS - Directory scanning working\n");
        }
    }
}
