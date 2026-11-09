// Copyright (c) 2026 Sector 7G Consulting LLC
// Licensed under the MIT License (see LICENSE file for details)
// Designed by Sector 7G Consulting LLC with code generated using various LLMs.
//
// Program.cs
// .NET 8 entry point for interactive memory leak host.

using MemLeakerHost.Common;

namespace MemLeakerHost.NetCore
{
    internal class Program : MemLeakerHostBase
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
    }
}
