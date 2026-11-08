// Copyright (c) 2026 Sector 7G Consulting LLC
// Licensed under the MIT License (see LICENSE file for details)
// Designed by Sector 7G Consulting LLC with code generated using various LLMs.
//
// MemLeakerHostBase.cs
// Abstract base class for interactive memory leak hosts.
// Provides framework-agnostic orchestration of native/managed leak allocation,
// memory monitoring, and OOM safety thresholds.
// Derived classes implement LeakNative() and LeakManaged() for their specific leak invocations.

using System;
using System.Diagnostics;

namespace MemLeakerHost.Common
{
    public abstract class MemLeakerHostBase
    {
        protected LeakOptions Options { get; private set; }
        protected ProcessMemoryInfo ProcessMemory { get; private set; }
        protected MemorySnapshot Snapshot { get; private set; }

        private int leakCount = 0;
        private long totalNativeMB = 0;
        private long totalManagedApproxMB = 0;
        private double maxCommitPctThreshold = 80.0;

        protected MemLeakerHostBase(LeakOptions options)
        {
            Options = options ?? throw new ArgumentNullException(nameof(options));

            var currentProcess = Process.GetCurrentProcess();
            ProcessMemory = new ProcessMemoryInfo(currentProcess);
            Snapshot = new MemorySnapshot(ProcessMemory);

            // Parse safety threshold
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
        }

        /// <summary>
        /// Execute the full interactive memory leak host orchestration.
        /// </summary>
        protected void Run()
        {
            Console.WriteLine("Interactive leak mode.");
            if (Options.NativeMB.HasValue)
                Console.WriteLine($"  Native: {Options.NativeMB.Value} MB per leak");
            if (Options.ManagedCount.HasValue)
                Console.WriteLine($"  Managed: {Options.ManagedCount.Value} count per leak");

            try
            {
                // Allocate once immediately on startup
                var currentMemory = new SystemMemoryInfo();
                long predictedNativeIncreaseMB = Options.NativeMB ?? 0;
                long predictedManagedIncreaseMB = Options.ManagedCount ?? 0;
                long predictedCommitMB = currentMemory.CommitChargeMB + predictedNativeIncreaseMB + predictedManagedIncreaseMB;

                // Check if initial prediction exceeds safety threshold
                double predictedPercentage = (predictedCommitMB * 100.0) / currentMemory.CommitLimitMB;
                if (maxCommitPctThreshold > 0 && predictedPercentage >= maxCommitPctThreshold)
                {
                    if (!PromptUserForOOMRisk(predictedPercentage, predictedCommitMB, currentMemory.CommitLimitMB))
                    {
                        Console.WriteLine("Initial allocation cancelled. Exiting.");
                        return;
                    }
                }

                LeakOnce();
                ProcessMemory.Refresh();
                Snapshot.PredictedCommitMB = predictedCommitMB;
                Snapshot.Print();

                // Interactive command loop
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
                        ProcessMemory.Refresh();
                        Snapshot.Print();
                    }
                    else if (input == "l" || input == "leak" || string.IsNullOrEmpty(input))
                    {
                        // Calculate prediction before leak
                        var preleakMemory = new SystemMemoryInfo();
                        long predictNativeInc = Options.NativeMB ?? 0;
                        long predictManagedInc = Options.ManagedCount ?? 0;
                        long predictCommit = preleakMemory.CommitChargeMB + predictNativeInc + predictManagedInc;

                        // Check if prediction exceeds safety threshold
                        double loopPredictedPercentage = (predictCommit * 100.0) / preleakMemory.CommitLimitMB;
                        if (maxCommitPctThreshold > 0 && loopPredictedPercentage >= maxCommitPctThreshold)
                        {
                            if (!PromptUserForOOMRisk(loopPredictedPercentage, predictCommit, preleakMemory.CommitLimitMB))
                            {
                                Console.WriteLine("Allocation cancelled.");
                                continue;
                            }
                        }

                        LeakOnce();
                        ProcessMemory.Refresh();
                        Snapshot.PredictedCommitMB = predictCommit;
                        Snapshot.Print();
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

        /// <summary>
        /// Perform a single leak operation (one iteration).
        /// </summary>
        private void LeakOnce()
        {
            leakCount++;
            Console.WriteLine($"=== Leak {leakCount} ===");

            var currentMemory = new SystemMemoryInfo();
            long predictedNativeIncreaseMB = Options.NativeMB ?? 0;
            long predictedManagedIncreaseMB = Options.ManagedCount ?? 0;
            long predictedCommitMB = currentMemory.CommitChargeMB + predictedNativeIncreaseMB + predictedManagedIncreaseMB;

            double predictedPercentageAtStart = (predictedCommitMB * 100.0) / currentMemory.CommitLimitMB;
            Console.WriteLine($"Current commit: {currentMemory.CommitChargeMB} MB, Predicted after leak: ~{predictedCommitMB} MB ({predictedPercentageAtStart:F1}%)");

            if (Options.NativeMB.HasValue)
            {
                Console.WriteLine($"Calling native LeakNativeMemory({Options.NativeMB.Value} MB)...");
                LeakNative(Options.NativeMB.Value);
                totalNativeMB += Options.NativeMB.Value;
                Console.WriteLine($"Native total so far: {totalNativeMB} MB");
            }

            if (Options.ManagedCount.HasValue)
            {
                Console.WriteLine($"Calling managed LeakManagedMemory({Options.ManagedCount.Value})...");
                LeakManaged(Options.ManagedCount.Value);
                totalManagedApproxMB += Options.ManagedCount.Value;

                var stats = GetManagedLeakStats();
                Console.WriteLine($"Managed stats: ListCount={stats.ListCount}, ApproxListBytes={stats.ApproxListBytes}, " +
                                  $"EventSubscribers={stats.EventSubscriberCount}, UndisposedStreams={stats.UndisposedStreamCount}");
                Console.WriteLine($"Managed total so far: ~{totalManagedApproxMB} MB");
            }
        }

        /// <summary>
        /// Prompt the user for confirmation of OOM risk.
        /// </summary>
        private bool PromptUserForOOMRisk(double percentageCharge, long predictedCommitMB, long commitLimitMB)
        {
            Console.WriteLine();
            Console.WriteLine($"WARNING: Allocation would result in {percentageCharge:F1}% commit charge ({predictedCommitMB} MB / {commitLimitMB} MB)");
            Console.WriteLine("This risks a machine OOM condition.");
            Console.Write("Continue anyway? (Y/N): ");
            string confirm = Console.ReadLine()?.Trim().ToLowerInvariant();
            return confirm == "y" || confirm == "yes";
        }

        /// <summary>
        /// Abstract method for derived classes to implement native leak invocation.
        /// </summary>
        protected abstract void LeakNative(int megabytes);

        /// <summary>
        /// Abstract method for derived classes to implement managed leak invocation.
        /// </summary>
        protected abstract void LeakManaged(int count);

        /// <summary>
        /// Get managed leak statistics from local ManagedLeaker library.
        /// This is not abstract because all implementations use the same ManagedLeaker.GetLeakStats().
        /// </summary>
        protected (int ListCount, long ApproxListBytes, int EventSubscriberCount, int UndisposedStreamCount) GetManagedLeakStats()
        {
            return ManagedLeaker.GetLeakStats();
        }
    }
}
