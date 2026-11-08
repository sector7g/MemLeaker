// Copyright (c) 2026 Sector 7G Consulting LLC
// Licensed under the MIT License (see LICENSE file for details)
// Designed by Sector 7G Consulting LLC with code generated using various LLMs.
//
// SystemMemoryInfo.cs
// .NET Framework 4.7.2 System memory metrics wrapper.
// Queries system commit charge, commit limit, and physical memory via Windows API (kernel32.dll).

using System;
using System.Runtime.InteropServices;

class SystemMemoryInfo
{
    [DllImport("kernel32.dll", SetLastError = true)]
    static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX lpBuffer);

    [StructLayout(LayoutKind.Sequential)]
    struct MEMORYSTATUSEX
    {
        public uint dwLength;
        public uint dwMemoryLoad;
        public ulong ullTotalPhys;
        public ulong ullAvailPhys;
        public ulong ullTotalPageFile;
        public ulong ullAvailPageFile;
        public ulong ullTotalVirtual;
        public ulong ullAvailVirtual;
        public ulong ullExtendedVirtual;
    }

    public long CommitChargeMB { get; private set; }
    public long CommitLimitMB { get; private set; }
    public double CommitPercentage { get; private set; }
    public long PhysicalUsedMB { get; private set; }
    public long PhysicalTotalMB { get; private set; }
    public double PhysicalPercentage { get; private set; }

    public SystemMemoryInfo()
    {
        Refresh();
    }

    public void Refresh()
    {
        MEMORYSTATUSEX memStatus = new MEMORYSTATUSEX();
        memStatus.dwLength = (uint)Marshal.SizeOf<MEMORYSTATUSEX>();

        CommitChargeMB = 0;
        CommitLimitMB = 0;
        CommitPercentage = 0;
        PhysicalUsedMB = 0;
        PhysicalTotalMB = 0;
        PhysicalPercentage = 0;

        if (GlobalMemoryStatusEx(ref memStatus))
        {
            // Commit
            CommitChargeMB = (long)(memStatus.ullTotalPageFile - memStatus.ullAvailPageFile) / (1024 * 1024);
            CommitLimitMB = (long)memStatus.ullTotalPageFile / (1024 * 1024);
            if (CommitLimitMB > 0)
                CommitPercentage = (CommitChargeMB * 100.0) / CommitLimitMB;

            // Physical RAM
            PhysicalTotalMB = (long)memStatus.ullTotalPhys / (1024 * 1024);
            long physicalAvailableMB = (long)memStatus.ullAvailPhys / (1024 * 1024);
            PhysicalUsedMB = PhysicalTotalMB - physicalAvailableMB;
            if (PhysicalTotalMB > 0)
                PhysicalPercentage = (PhysicalUsedMB * 100.0) / PhysicalTotalMB;
        }
    }
}
