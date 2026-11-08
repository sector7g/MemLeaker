// Copyright (c) 2026 Sector 7G Consulting LLC
// Licensed under the MIT License (see LICENSE file for details)
// Designed by Sector 7G Consulting LLC with code generated using various LLMs.
//
// Program.cs
// .NET Framework 4.7.2 Interactive memory leak host.
// Orchestrates native and managed leak allocation, monitors system/process memory,
// and applies configurable OOM safety thresholds.

using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using ManagedLeakerNetFx;

class Program
{
    [DllImport("NativeLeaker.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "LeakMemory")]
    static extern void NativeLeakMemory(int megabytes);

    static void Main(string[] args)
    {
        if (!ArgsParser.TryParse(args, out var options))
            return;

        Console.WriteLine("Interactive leak mode.");
        if (options.NativeMB.HasValue)
            Console.WriteLine($"  Native: {options.NativeMB.Value} MB per leak");
        if (options.ManagedCount.HasValue)
            Console.WriteLine($"  Managed: {options.ManagedCount.Value} count per leak");

        var currentProcess = Process.GetCurrentProcess();
        var processMemory = new ProcessMemoryInfo(currentProcess);
        var snapshot = new MemorySnapshot(processMemory);

        int leakCount = 0;
        long totalNativeMB = 0;
        long totalManagedApproxMB = 0;

        // Calculate safety threshold once
        double maxCommitPctThreshold = 80.0; // default
        if (options.MaxCommitPct.HasValue)
        {
            if (options.MaxCommitPct.Value == -1)
            {
                maxCommitPctThreshold = -1; // "none" - skip warning
            }
            else
            {
                maxCommitPctThreshold = options.MaxCommitPct.Value;
            }
        }

        try
        {
            // Allocate once immediately on startup
            var currentMemory = new SystemMemoryInfo();
            long predictedNativeIncreaseMB = (options.NativeMB ?? 0);
            long predictedManagedIncreaseMB = (options.ManagedCount ?? 0);
            long predictedCommitMB = currentMemory.CommitChargeMB + predictedNativeIncreaseMB + predictedManagedIncreaseMB;

            // Check if initial prediction exceeds safety threshold
            double predictedPercentage = (predictedCommitMB * 100.0) / currentMemory.CommitLimitMB;
            if (maxCommitPctThreshold > 0 && predictedPercentage >= maxCommitPctThreshold)
            {
                Console.WriteLine();
                Console.WriteLine($"WARNING: Initial allocation would result in {predictedPercentage:F1}% commit charge ({predictedCommitMB} MB / {currentMemory.CommitLimitMB} MB)");
                Console.WriteLine("This risks a machine OOM condition.");
                Console.Write("Continue anyway? (Y/N): ");
                string confirm = Console.ReadLine()?.Trim().ToLowerInvariant();
                if (confirm != "y" && confirm != "yes")
                {
                    Console.WriteLine("Initial allocation cancelled. Exiting.");
                    return;
                }
            }

            LeakMemory(options, ref leakCount, ref totalNativeMB, ref totalManagedApproxMB, processMemory);
            processMemory.Refresh();
            snapshot.PredictedCommitMB = predictedCommitMB;
            snapshot.Print();

            while (true)
            {
                Console.WriteLine();
                Console.Write("L=leak more  R=refresh stats  Q=quit: ");
                string input = Console.ReadLine()?.Trim().ToLowerInvariant();

                if (input == "q" || input == "quit")
                {
                    Console.WriteLine("Exiting.");
                    return;
                }
                else if (input == "r" || input == "refresh")
                {
                    processMemory.Refresh();
                    snapshot.Print();
                }
                 else if (input == "l" || input == "leak" || string.IsNullOrEmpty(input))
                {
                    // Calculate prediction before leak
                    var preleakMemory = new SystemMemoryInfo();
                    long predictNativeInc = options.NativeMB ?? 0;
                    long predictManagedInc = options.ManagedCount ?? 0;
                    long predictCommit = preleakMemory.CommitChargeMB + predictNativeInc + predictManagedInc;

                    // Check if prediction exceeds safety threshold
                    double loopPredictedPercentage = (predictCommit * 100.0) / preleakMemory.CommitLimitMB;
                    if (maxCommitPctThreshold > 0 && loopPredictedPercentage >= maxCommitPctThreshold)
                    {
                        Console.WriteLine();
                        Console.WriteLine($"WARNING: Allocation would result in {loopPredictedPercentage:F1}% commit charge ({predictCommit} MB / {preleakMemory.CommitLimitMB} MB)");
                        Console.WriteLine("This risks a machine OOM condition.");
                        Console.Write("Continue anyway? (Y/N): ");
                        string confirm = Console.ReadLine()?.Trim().ToLowerInvariant();
                        if (confirm != "y" && confirm != "yes")
                        {
                            Console.WriteLine("Allocation cancelled.");
                            continue;
                        }
                    }

                    LeakMemory(options, ref leakCount, ref totalNativeMB, ref totalManagedApproxMB, processMemory);
                    processMemory.Refresh();
                    snapshot.PredictedCommitMB = predictCommit;
                    snapshot.Print();
                }
                else
                {
                    Console.WriteLine("Unknown command. Use L, R, or Q.");
                }
            }
        }
        catch (DllNotFoundException)
        {
            Console.WriteLine("Error: NativeLeaker.dll not found. Place it next to this exe.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
        }
    }

    static void LeakMemory(LeakOptions options, ref int leakCount, ref long totalNativeMB, ref long totalManagedApproxMB, ProcessMemoryInfo processMemory)
    {
        leakCount++;
        Console.WriteLine($"=== Leak {leakCount} ===");

        var currentMemory = new SystemMemoryInfo();
        long predictedNativeIncreaseMB = options.NativeMB ?? 0;
        long predictedManagedIncreaseMB = options.ManagedCount ?? 0; // Rough approximation
        long predictedCommitMB = currentMemory.CommitChargeMB + predictedNativeIncreaseMB + predictedManagedIncreaseMB;

        double predictedPercentageAtStart = (predictedCommitMB * 100.0) / currentMemory.CommitLimitMB;
        Console.WriteLine($"Current commit: {currentMemory.CommitChargeMB} MB, Predicted after leak: ~{predictedCommitMB} MB ({predictedPercentageAtStart:F1}%)");

        if (options.NativeMB.HasValue)
        {
            Console.WriteLine($"Calling native LeakMemory({options.NativeMB.Value} MB)...");
            NativeLeakMemory(options.NativeMB.Value);
            totalNativeMB += options.NativeMB.Value;
            Console.WriteLine($"Native total so far: {totalNativeMB} MB");
        }

        if (options.ManagedCount.HasValue)
        {
            Console.WriteLine($"Calling managed LeakAll({options.ManagedCount.Value})...");
            Leaker.LeakAll(options.ManagedCount.Value);
            totalManagedApproxMB += options.ManagedCount.Value;

            var stats = Leaker.GetLeakStats();
            Console.WriteLine($"Managed stats: ListCount={stats.ListCount}, ApproxListBytes={stats.ApproxListBytes}, " +
                              $"EventSubscribers={stats.EventSubscriberCount}, UndisposedStreams={stats.UndisposedStreamCount}");
            Console.WriteLine($"Managed total so far: ~{totalManagedApproxMB} MB");
        }
    }
}
