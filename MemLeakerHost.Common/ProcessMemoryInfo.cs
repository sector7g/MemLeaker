// Copyright (c) 2026 Sector 7G Consulting LLC
// Licensed under the MIT License (see LICENSE file for details)
// Designed by Sector 7G Consulting LLC with code generated using various LLMs.
//
// ProcessMemoryInfo.cs
// Process memory metrics wrapper.
// Queries process working set, private working set, and commit charge via Windows API (psapi.dll).

using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace MemLeakerHost.Common
{
    public class ProcessMemoryInfo
    {
        [StructLayout(LayoutKind.Sequential)]
        struct PROCESS_MEMORY_COUNTERS_EX2
        {
            public uint cb;
            public uint PageFaultCount;
            public UIntPtr PeakWorkingSetSize;
            public UIntPtr WorkingSetSize;
            public UIntPtr QuotaPeakPagedPoolUsage;
            public UIntPtr QuotaPagedPoolUsage;
            public UIntPtr QuotaPeakNonPagedPoolUsage;
            public UIntPtr QuotaNonPagedPoolUsage;
            public UIntPtr PagefileUsage;
            public UIntPtr PeakPagefileUsage;
            public UIntPtr PrivateUsage;
            public UIntPtr PrivateWorkingSetSize;
            public ulong SharedCommitUsage;
        }

        [DllImport("psapi.dll", SetLastError = true)]
        static extern bool GetProcessMemoryInfo(IntPtr hProcess, out PROCESS_MEMORY_COUNTERS_EX2 counters, uint cb);

        private Process process;

        public int ProcessId { get; private set; }
        public long WorkingSetMB { get; private set; }
        public long PrivateWorkingSetMB { get; private set; }
        public long CommitMB { get; private set; }
        public bool IsValid { get; private set; }

        public ProcessMemoryInfo(Process proc)
        {
            process = proc;
            ProcessId = proc.Id;
            Refresh();
        }

        public void Refresh()
        {
            PROCESS_MEMORY_COUNTERS_EX2 mem = new PROCESS_MEMORY_COUNTERS_EX2();
            mem.cb = (uint)Marshal.SizeOf<PROCESS_MEMORY_COUNTERS_EX2>();

            IsValid = GetProcessMemoryInfo(process.Handle, out mem, mem.cb);

            if (IsValid)
            {
                WorkingSetMB = (long)(mem.WorkingSetSize.ToUInt64() / (1024 * 1024));
                PrivateWorkingSetMB = (long)(mem.PrivateWorkingSetSize.ToUInt64() / (1024 * 1024));
                CommitMB = (long)(mem.PrivateUsage.ToUInt64() / (1024 * 1024));
            }
            else
            {
                WorkingSetMB = 0;
                PrivateWorkingSetMB = 0;
                CommitMB = 0;
            }
        }
    }
}
