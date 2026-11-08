// Copyright (c) 2026 Sector 7G Consulting LLC
// Licensed under the MIT License (see LICENSE file for details)
// Designed by Sector 7G Consulting LLC with code generated using various LLMs.
//
// ArgsParser.cs
// Command-line argument parser.
// Parses --nativeMB, --managedCount, and --maxCommitPct options.

using System;

namespace MemLeakerHost.Common
{
    public static class ArgsParser
    {
        public static bool TryParse(string[] args, out LeakOptions options)
        {
            options = new LeakOptions();

            for (int i = 0; i < args.Length; i++)
            {
                string arg = args[i].ToLowerInvariant();
                if (arg == "nativemb" && i + 1 < args.Length)
                {
                    if (int.TryParse(args[i + 1], out int val) && val >= 1 && val <= 5000)
                    {
                        options.NativeMB = val;
                        i++;
                    }
                    else
                    {
                        Console.WriteLine("nativeMB must be followed by an integer between 1 and 5000.");
                        PrintUsage();
                        return false;
                    }
                }
                else if (arg == "managedcount" && i + 1 < args.Length)
                {
                    if (int.TryParse(args[i + 1], out int val) && val >= 1 && val <= 5000)
                    {
                        options.ManagedCount = val;
                        i++;
                    }
                    else
                    {
                        Console.WriteLine("managedCount must be followed by an integer between 1 and 5000.");
                        PrintUsage();
                        return false;
                    }
                }
                else if (arg == "maxcommitpct" && i + 1 < args.Length)
                {
                    string val = args[i + 1].ToLowerInvariant();
                    if (val == "none")
                    {
                        options.MaxCommitPct = -1; // Special value for "none"
                        i++;
                    }
                    else if (int.TryParse(val, out int pctVal) && pctVal >= 1 && pctVal <= 99)
                    {
                        options.MaxCommitPct = pctVal;
                        i++;
                    }
                    else
                    {
                        Console.WriteLine("maxCommitPct must be followed by either 'none' or an integer between 1 and 99.");
                        PrintUsage();
                        return false;
                    }
                }
            }

            if (options.NativeMB == null && options.ManagedCount == null)
            {
                Console.WriteLine("At least one of nativeMB or managedCount is required.");
                PrintUsage();
                return false;
            }

            return true;
        }

        static void PrintUsage()
        {
            Console.WriteLine("Usage (at least one of nativeMB or managedCount required):");
            Console.WriteLine("  MemLeakerHost.exe nativeMB <1-5000> [managedCount <1-5000>] [maxCommitPct <1-99|none>]");
            Console.WriteLine("  MemLeakerHost.exe managedCount <1-5000> [maxCommitPct <1-99|none>]");
            Console.WriteLine();
            Console.WriteLine("Parameters:");
            Console.WriteLine("  nativeMB       - Native memory to leak per allocation (1-5000 MB)");
            Console.WriteLine("  managedCount   - Managed objects to leak per allocation (1-5000)");
            Console.WriteLine("  maxCommitPct   - Safety threshold for OOM warning");
            Console.WriteLine("                   1-99: warn if predicted commit >= this percentage");
            Console.WriteLine("                   none: disable OOM warnings");
            Console.WriteLine("                   (default: 80)");
            Console.WriteLine();
            Console.WriteLine("Examples:");
            Console.WriteLine("  MemLeakerHost.exe nativeMB 1000 managedCount 100");
            Console.WriteLine("  MemLeakerHost.exe nativeMB 500");
            Console.WriteLine("  MemLeakerHost.exe nativeMB 1000 maxCommitPct 90");
            Console.WriteLine("  MemLeakerHost.exe managedCount 50 maxCommitPct none");
        }
    }

    public class LeakOptions
    {
        public int? NativeMB { get; set; }
        public int? ManagedCount { get; set; }
        public double? MaxCommitPct { get; set; } // null = use default (80), "none" = -1, 1-99 = use value
    }
}
