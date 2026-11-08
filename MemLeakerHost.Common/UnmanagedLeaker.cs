// Copyright (c) 2026 Sector 7G Consulting LLC
// Licensed under the MIT License (see LICENSE file for details)
// Designed by Sector 7G Consulting LLC with code generated using various LLMs.
//
// UnmanagedLeaker.cs
// Wrapper for native unmanaged memory leak functionality.
// Encapsulates P/Invoke to NativeLeaker.dll and provides a symmetrical interface with ManagedLeaker.

using System.Runtime.InteropServices;

namespace MemLeakerHost.Common
{
    public class UnmanagedLeaker
    {
        [DllImport("NativeLeaker.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "LeakNativeMemory")]
        public static extern void LeakNativeMemory(int megabytes);
    }
}