// Copyright (c) 2026 Sector 7G Consulting LLC
// Licensed under the MIT License (see LICENSE file for details)
// Designed by Sector 7G Consulting LLC with code generated using various LLMs.
//
// Program.cs
// .NET Framework 4.7.2 Interactive memory leak host.
// Thin wrapper around MemLeakerHostBase that provides framework-specific leak invocations.

using System;
using MemLeakerHost.Common;

class Program : MemLeakerHostBase
{
    private Program(LeakOptions options) : base(options)
    {
    }

    static void Main(string[] args)
    {
        if (!ArgsParser.TryParse(args, out var options))
            return;

        var host = new Program(options);
        host.Run();
    }

    /// <summary>
    /// Implement native leak invocation for .NET Framework host.
    /// </summary>
    protected override void LeakNative(int megabytes)
    {
        UnmanagedLeaker.LeakNativeMemory(megabytes);
    }

    /// <summary>
    /// Implement managed leak invocation for .NET Framework host.
    /// </summary>
    protected override void LeakManaged(int count)
    {
        ManagedLeaker.LeakManagedMemory(count);
    }
}
