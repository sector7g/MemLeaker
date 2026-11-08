# MemLeaker

A Windows/.NET Framework memory leak testing and demonstration tool with interactive leak simulation across native (C++) and managed (.NET) memory.

## Overview

MemLeaker is designed to deliberately allocate and leak memory via multiple mechanisms—native heap, managed heap, and undisposed resources—to simulate real-world memory leak scenarios. It's useful for:

- **Testing memory monitoring tools** (debuggers, profilers, ETW)
- **Understanding memory leak patterns** in both native and managed code
- **Validating Out-Of-Memory (OOM) safeguards** with configurable commit charge thresholds
- **Demonstrating memory leak mechanisms** in educational or training contexts

## Architecture

The solution consists of three main components:

### 1. **MemLeakerHostNetFx** (.NET Framework 4.7.2 Console App)
The interactive host that orchestrates leak scenarios and displays system/process memory metrics.

**Key responsibilities:**
- Interactive command loop (Leak, Refresh, Quit)
- System and process memory monitoring
- Commit charge prediction and OOM warnings
- Configurable safety thresholds for maximum commit percentage

**Key files:**
- `Program.cs` - Main entry point, interactive loop, and argument parsing
- `ProcessMemoryInfo.cs` - Windows API wrappers for process memory counters
- `SystemMemoryInfo.cs` - System commit charge and physical memory via GlobalMemoryStatusEx
- `MemorySnapshot.cs` - Formatted output of memory state and prediction accuracy
- `ArgsParser.cs` - Command-line argument parsing for leak parameters and safety thresholds

### 2. **ManagedLeakerNetFx** (.NET Framework 4.7.2 Class Library)
Implements three different managed memory leak patterns:

1. **Static List Leak** - Byte arrays stored in a static List that is never cleared
2. **Event Handler Leak** - Static event subscriptions that are never unsubscribed
3. **Undisposed Resources Leak** - MemoryStreams stored statically without disposal

**Key file:**
- `ManagedLeaker.cs` - Leaker static class with three leak methods

### 3. **NativeLeaker** (C++/CLI Project)
Native memory allocator that deliberately leaks heap-allocated memory.

**Characteristics:**
- Allocates memory in 100 MB chunks (configurable up to 5000 MB per call)
- Touches all allocated memory (memset) to ensure physical allocation
- Never frees allocated memory (intentional leak)
- Outputs summary (total MB leaked, chunk count)

**Key file:**
- `NativeLeaker.cpp` - LeakMemory export function

## System Architecture Diagram

```
┌─────────────────────────────────────────────────────┐
│     MemLeakerHostNetFx (Interactive Console)        │
│  ┌────────────────────────────────────────────┐    │
│  │ Program.cs (Main Loop)                     │    │
│  │ - Parse args (native/managed MB, threshold)│    │
│  │ - Predict commit charge before allocation  │    │
│  │ - Warn if prediction exceeds threshold     │    │
│  └────────────────────────────────────────────┘    │
│  ┌────────────────────────────────────────────┐    │
│  │ Memory Reporters                           │    │
│  │ - ProcessMemoryInfo (process counters)    │    │
│  │ - SystemMemoryInfo (system commit %)      │    │
│  │ - MemorySnapshot (formatted output)       │    │
│  └────────────────────────────────────────────┘    │
└──────────────┬──────────────────────────────────────┘
			   │
	   ┌───────┴───────┐
	   │               │
	┌──▼──┐      ┌─────▼─────┐
	│Managed Leak│  Native Leak│
	│(ManagedLeakerNetFx) │  (NativeLeaker.dll) │
	└───────┘      └──────────┘
```

## Prerequisites

- **Visual Studio 2019** or later (tested on VS 2026 Community)
- **.NET Framework 4.7.2** (or higher targeting the same framework)
- **C++ build tools** (for compiling NativeLeaker.dll)
  - Install from Visual Studio Installer: Desktop development with C++

## Building

### Option 1: Build in Visual Studio

1. Open `MemLeaker.sln` in Visual Studio
2. Select **Debug** or **Release** configuration
3. **Build** > **Build Solution**
   - The solution will build MemLeakerHostNetFx, ManagedLeakerNetFx, and NativeLeaker
   - NativeLeaker.dll will be automatically copied to MemLeakerHostNetFx output on successful build

### Option 2: Build from Command Line

```powershell
cd C:\path\to\MemLeaker
msbuild MemLeaker.sln /p:Configuration=Debug /p:Platform="Any CPU"
```

**Output:**
- `MemLeakerHostNetFx\bin\Debug\MemLeakerHostNetFx.exe` - Main executable
- `ManagedLeakerNetFx\bin\Debug\ManagedLeakerNetFx.dll` - Managed leak library
- `NativeLeaker\bin\Debug\NativeLeaker.dll` - Native leak library

## Usage

### Running the Interactive Host

```bash
MemLeakerHostNetFx.exe [--nativeMB <MB>] [--managedCount <count>] [--maxCommitPct <pct|none>]
```

### Arguments

| Argument           | Description                                          | Default |
|--------------------|------------------------------------------------------|---------|
| `--nativeMB <MB>`  | Native memory to leak per allocation (1-5000 MB)    | None    |
| `--managedCount <count>` | Managed objects to leak per allocation           | None    |
| `--maxCommitPct <pct\|none>` | Warn if commit charge exceeds % (1-99, or `none` to disable) | 80 |

### Example Invocations

**Simple managed leak (100 objects per allocation, default 80% threshold):**
```bash
MemLeakerHostNetFx.exe --managedCount 100
```

**Native leak (500 MB per allocation, warn at 75% commit):**
```bash
MemLeakerHostNetFx.exe --nativeMB 500 --maxCommitPct 75
```

**Combined native and managed, disable warnings:**
```bash
MemLeakerHostNetFx.exe --nativeMB 200 --managedCount 50 --maxCommitPct none
```

### Interactive Commands

Once running, the tool presents:
```
L=leak more  R=refresh stats  Q=quit:
```

| Command | Action                                               |
|---------|------------------------------------------------------|
| `L` (or `leak`, or just Enter) | Perform a leak allocation (native and/or managed) |
| `R` (or `refresh`) | Refresh and display current memory metrics |
| `Q` (or `quit`) | Exit the program |

### Memory Output Example

```
System memory:
  Commit: 8.5 GB / 12.0 GB (70.8%)
  Physical: 14.2 GB / 16.0 GB (88.8%)

Process memory (PID 1234):
  Working Set: 1.2 GB
  Private Working Set: 1.1 GB
  Commit Charge: 640 MB
```

If a prediction was made, accuracy is displayed:
```
Prediction accuracy: 98.5% (predicted 512 MB, actual 520 MB)
```

## Memory Leak Mechanisms

### Managed Leaks (ManagedLeakerNetFx)

1. **Static List** - Stores `~1 MB byte[]` objects in a static list; never cleared
2. **Event Handlers** - Static event with subscribers that never unsubscribe; publisher holds strong references
3. **Undisposed Resources** - MemoryStreams in static list without calling Dispose()

All three mechanisms prevent garbage collection, causing heap growth.

### Native Leak (NativeLeaker)

- Allocates virtual address space via `malloc()` in 100 MB chunks
- Touches all allocated bytes (memset) to ensure physical commitment
- Never calls `free()` or `VirtualFree()`
- Results in committed virtual memory that cannot be reclaimed during the process lifetime

## OOM Safety Features

The tool includes configurable commit charge thresholds to prevent devastating the system:

- **Default threshold: 80%** system commit charge
- **Configurable range: 1-99%** for custom thresholds
- **Disable with `--maxCommitPct none`** if testing dangerous scenarios in a protected VM
- **Warnings appear at initial allocation AND each subsequent leak** before committing

If a predicted commit charge would exceed the threshold, the tool prompts:
```
WARNING: Allocation would result in 85% commit charge (10.2 GB / 12.0 GB)
This risks a machine OOM condition.
Continue anyway? (Y/N):
```

## System Memory Concepts

### Commit Charge
The total amount of virtual address space **committed** (backed by physical RAM or page file). As VM is committed, this increases. The OS can satisfy commit up to the **commit limit** (usually RAM + pagefile size).

### Physical Memory
The actual RAM installed and in use. When commit charge exceeds RAM, the OS pages to disk.

### Why Predict?
MemLeaker calculates the predicted commit charge *before* allocation to warn if you're about to push the system into a dangerous OOM state.

## Contributing

Contributions welcome! Areas of interest:

- Additional leak pattern implementations (thread-local storage, pinned objects, etc.)
- Support for .NET 6+ (parallel projects targeting newer frameworks)
- MacOS/Linux support (requires non-Windows memory APIs)
- Enhanced memory metrics and reporting
- Unit tests for leak mechanisms

To contribute:
1. Fork the repository
2. Create a feature branch (`git checkout -b feature/my-feature`)
3. Commit and push changes
4. Submit a pull request

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for full details.

## Disclaimer

**MemLeaker is designed to INTENTIONALLY leak memory.** Use it only:
- On dedicated test systems or virtual machines
- Never on production systems
- With full understanding of its destructive nature

Misuse can render a system unstable or unusable. Always set `--maxCommitPct` appropriately or use `none` only in isolated environments.

## Future Enhancements

- [ ] Net 6+ target versions (MemLeakerHostNet6, ManagedLeakerNet6)
- [ ] ETW event tracing for leak diagnostics
- [ ] Real-time heap snapshots and diff analysis
- [ ] Support for thread-local static leaks
- [ ] Pinned object heap testing
- [ ] JSON-formatted memory reports
- [ ] Automated test scenarios (batch leak sequences)

## References

- [Virtual Memory on Windows](https://docs.microsoft.com/en-us/windows/win32/memory/virtual-memory)
- [Garbage Collection (.NET Framework)](https://docs.microsoft.com/en-us/dotnet/standard/garbage-collection/)
- [Memory Profiling in Visual Studio](https://docs.microsoft.com/en-us/visualstudio/profiling/memory-usage)
- [WinDbg Memory Debugging](https://docs.microsoft.com/en-us/windows-hardware/drivers/debugger/)
