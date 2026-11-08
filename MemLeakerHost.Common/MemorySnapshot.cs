// Copyright (c) 2026 Sector 7G Consulting LLC
// Licensed under the MIT License (see LICENSE file for details)
// Designed by Sector 7G Consulting LLC with code generated using various LLMs.
//
// MemorySnapshot.cs
// Memory state formatter and reporter.
// Displays system and process memory metrics, and calculates prediction accuracy.

using System;

namespace MemLeakerHost.Common
{
    public class MemorySnapshot
    {
        public SystemMemoryInfo SystemMemory { get; set; }
        public ProcessMemoryInfo ProcessMemory { get; set; }
        public long? PredictedCommitMB { get; set; }

        public MemorySnapshot(ProcessMemoryInfo processMemory)
        {
            SystemMemory = new SystemMemoryInfo();
            ProcessMemory = processMemory;
            PredictedCommitMB = null;
        }

        public void Print()
        {
            // Refresh system memory info on each print
            SystemMemory.Refresh();

            if (!ProcessMemory.IsValid)
            {
                Console.WriteLine($"Error: Could not get process memory info");
                return;
            }

            Console.WriteLine();
            Console.WriteLine("System:");

            // Print prediction accuracy if prediction was made
            if (PredictedCommitMB.HasValue)
            {
                double accuracy = 100.0 - (Math.Abs(SystemMemory.CommitChargeMB - PredictedCommitMB.Value) * 100.0 / PredictedCommitMB.Value);
                if (accuracy < 0) accuracy = 0;
                Console.WriteLine($"  Prediction accuracy: {accuracy,5:F1}%");
                PredictedCommitMB = null; // Clear for next iteration
            }

            Console.WriteLine($"  Commit:  {SystemMemory.CommitChargeMB,6:N0} / {SystemMemory.CommitLimitMB,6:N0} MB ({SystemMemory.CommitPercentage,5:F1}%)");
            Console.WriteLine($"  Physical: {SystemMemory.PhysicalUsedMB,6:N0} / {SystemMemory.PhysicalTotalMB,6:N0} MB ({SystemMemory.PhysicalPercentage,5:F1}%)");
            Console.WriteLine();
            Console.WriteLine($"This PID: {ProcessMemory.ProcessId}");
            Console.WriteLine($"{"Time",-10} {"WS (MB)",12} {"Private WS (MB)",16} {"Commit (MB)",12}");
            Console.WriteLine(new string('-', 54));

            Console.WriteLine(
                $"{DateTime.Now,-10:HH:mm:ss}" +
                $"{ProcessMemory.WorkingSetMB,12:N0}" +
                $"{ProcessMemory.PrivateWorkingSetMB,16:N0}" +
                $"{ProcessMemory.CommitMB,12:N0}");
        }
    }
}
