# MemLeaker

A Windows memory leak generator and diagnostic tool for testing memory management and monitoring under intentional leak conditions.

## Table of Contents

- [Overview](#overview)
- [Architecture](#architecture)
- [Building](#building)
- [Usage](#usage)
- [gflags.exe and User-Mode Stack Traces](#gflagsexe-and-user-mode-stack-traces)
- [Memory Leak Mechanisms](#memory-leak-mechanisms)
- [NativeLeaker's Console Pause](#nativeleakers-console-pause)
- [OOM Safety](#oom-safety)
- [License](#license)
- [Disclaimer](#disclaimer)

## Overview

MemLeaker deliberately allocates and leaks memory via native heap, managed heap, and undisposed resources to simulate memory leak scenarios. It's useful for:

- Testing memory monitoring tools (debuggers, profilers, ETW)
- Understanding memory leak patterns in both native and managed code
- Demonstrating memory leak mechanisms in educational contexts

## Architecture

The solution consists of four core components:

### MemLeakerHost.NetFx (.NET Framework 4.8 Executable)

Interactive memory leak host for .NET Framework. Derives from `MemLeakerHostBase` and implements framework-specific leak invocations.

**Key Methods:**
- `LeakNative(int megabytes)` → `UnmanagedLeaker.LeakNativeMemory(megabytes)`
- `LeakManaged(int count)` → `ManagedLeaker.LeakManagedMemory(count)`

### MemLeakerHost.NetCore (.NET 8 Executable)

Interactive memory leak host for modern .NET. SDK-style project targeting `net8.0`. Same architecture as MemLeakerHost.NetFx with identical leak simulation capabilities.

**Key Methods:**
- `LeakNative(int megabytes)` → `UnmanagedLeaker.LeakNativeMemory(megabytes)`
- `LeakManaged(int count)` → `ManagedLeaker.LeakManagedMemory(count)`

### MemLeakerHost.Common (.NET Standard 2.0 Library)

Reusable shared infrastructure for leak hosts, memory monitoring, and diagnostics.

**Core Components:**

**MemLeakerHostBase** - Abstract orchestration class
- `Run()` - Main interactive loop
- `LeakOnce()` - Single iteration with memory monitoring
- `PromptUserForOOMRisk()` - Safety checks before allocation
- `GetManagedLeakStats()` - Retrieves managed leak statistics
- Concrete methods: `LeakNative(int)`, `LeakManaged(int)` (delegate to UnmanagedLeaker and ManagedLeaker)

**ManagedLeaker** - Intentional managed leak patterns
- `LeakViaStaticList(int count)` - Stores byte arrays in static list
- `LeakViaEventHandlers(int count)` - Accumulates event subscribers
- `LeakViaUndisposedResources(int count)` - Creates undisposed MemoryStreams
- `LeakManagedMemory(int count)` - Delegates to leak patterns
- `GetLeakStats()` - Returns leak counts as tuple

**UnmanagedLeaker** - P/Invoke wrapper
- `LeakNativeMemory(int megabytes)` - DLL import to NativeLeaker.dll

**Configuration & Diagnostics**
- `ArgsParser` - Command-line argument parsing
- `LeakOptions` - Configuration container
- `ProcessMemoryInfo` - Process memory metrics
- `MemorySnapshot` - Memory state capture and formatting
- `SystemMemoryInfo` - System commit charge and physical memory

### NativeLeaker (C++ x64 DLL)

Native unmanaged memory leak implementation. Built independently and copied to host executable output directory via post-build event.

**Export:**
- `LeakNativeMemory(int megabytes)` - Allocates and retains unmanaged heap memory

## Building

1. Open `MemLeaker.sln` in Visual Studio
2. Select Debug or Release configuration
3. **Build** > **Build Solution**
   - Builds MemLeakerHost.Common, MemLeakerHost.NetFx, MemLeakerHost.NetCore, and NativeLeaker
   - Post-build events copy NativeLeaker.dll and gflags.exe to host output directories
   - If DLL/gflags missing after build, copy manually from NativeLeaker\bin\<Config> and tools\ directories

## Usage

Both host executables support identical command-line syntax:

```
MemLeakerHost.[NetFx|NetCore].exe nativeMB <1-5000> [managedCount <1-5000>] [maxCommitPct <1-99|none>]
```

or

```
MemLeakerHost.[NetFx|NetCore].exe managedCount <1-5000> [maxCommitPct <1-99|none>]
```

**Parameters:**
- `nativeMB <1-5000>` - Native memory to leak per allocation (1-5000 MB)
- `managedCount <1-5000>` - Managed objects to leak per allocation (1-5000)
- `maxCommitPct <1-99|none>` - OOM safety threshold
  - `1-99` - Warn if predicted commit charge exceeds this percentage (default: 80)
  - `none` - Disable OOM warnings

**Examples:**

Simple managed leak (.NET Framework):
```
MemLeakerHost.NetFx.exe managedCount 1000
```

Native leak with custom threshold (.NET 8):
```
MemLeakerHost.NetCore.exe nativeMB 500 maxCommitPct 75
```

Combined native and managed, no warnings:
```
MemLeakerHost.NetFx.exe nativeMB 200 managedCount 50 maxCommitPct none
```

## gflags.exe and User-Mode Stack Traces

`gflags.exe` (Global Flags) is a Windows utility that enables advanced debugging features for processes, including **user-mode stack traces (+ust)**, which record the call stack at each heap allocation.

### Enable User-Mode Stack Traces

Enable stack traces for a specific process by executable name:

```
gflags.exe /i MemLeakerHost.NetFx.exe +ust
```

or for .NET Core:

```
gflags.exe /i MemLeakerHost.NetCore.exe +ust
```

This modifies the `NtGlobalFlag` registry value for the process and applies immediately to new instances.

### Disable User-Mode Stack Traces

Disable stack traces for the same process:

```
gflags.exe /i MemLeakerHost.NetFx.exe -ust
```

or:

```
gflags.exe /i MemLeakerHost.NetCore.exe -ust
```

### Why User-Mode Stack Traces Matter

When `+ust` is enabled:
- Every heap allocation records the call stack (up to a configurable depth)
- WinDbg's `!heap -p -a <address>` command will display the **full call stack** for that allocation
- You can trace allocations back to their origin (e.g., `NativeLeaker!LeakNativeMemory` or managed code calling into native code)
- Essential for attributed leak forensics when analyzing memory dumps

Without `+ust`, heap dumps show allocation details but no stack context. With `+ust`, you get the complete picture of *why* memory was allocated at that moment.

## Memory Leak Mechanisms

### Managed Leaks

**Static List** - Byte arrays (~1 MB each) stored in static list; never cleared

**Event Handlers** - Static event with subscribers that never unsubscribe; publisher holds references

**Undisposed Resources** - MemoryStreams stored statically without calling Dispose()

All prevent garbage collection and cause heap growth.

### Native Leak

- Allocates virtual address space via `malloc()` in dynamically-sized chunks (100 MB for allocations ≥100 MB, smaller for smaller allocations)
- Touches all allocated bytes (memset with `0x21`) to ensure physical commitment
- Never calls `free()` or `VirtualFree()`
- Results in committed virtual memory that cannot be reclaimed
- **Pauses on console input** after allocation completes, allowing the host process to remain live while you capture memory dumps, heap snapshots, or debugging sessions. Press ENTER to continue the leak loop or exit.

## NativeLeaker's Console Pause

The NativeLeaker pauses in the console before its function that allocates & sets memory returns.

- This allows dumping the process while the function is on the call stack (and thus should appear using the 'k' WinDbg command).
- Dumping the file at this point is optional and, in any case, pressing ENTER resumes from pause.

## OOM Safety

Configurable commit charge thresholds prevent system destabilization:

- Default threshold: 80% system commit charge
- Configurable range: 1-99%
- Disable with `maxCommitPct none` for isolated testing environments
- Warnings appear before each allocation that would exceed threshold

## License

MIT License © 2026 Sector 7G Consulting LLC

Designed by Sector 7G Consulting LLC with code generated using various LLMs.

## Disclaimer

**MemLeaker intentionally leaks memory.** Use only on dedicated test systems or virtual machines. Never use on production systems.

Misuse can render a system unstable or unusable. Always configure `maxCommitPct` appropriately.
