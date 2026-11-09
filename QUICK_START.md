# MemLeaker Quick Start Guide

## Memory Counter Mapping

| MemLeaker Metric | Task Manager (Win 11) | Description |
|---|---|---|
| **WS** | Working set (memory) | How much memory has been allocated & written |
| **Private WS** | Memory (private working set) | How much memory has been written, excluding shared memory with other processes |
| **Commit** | Commit size | Total amount of allocated memory regardless of whether it's been written (it's all been written in this case) |

## Build Output Structure

After building, output binaries are located in `\bin\Release` or `\bin\Debug`:

### .NET Framework 4.8
- `MemLeakerHost.NetFx.*`

### .NET (Core) 8.0
- `MemLeakerHost.NetCore.*`

### Both (Shared)
- `MemLeakerHost.Common.*` (.NET Standard 2.0)
- `NativeLeaker.*` (x64)
- `gflags.exe` (x64/Win SDK) — copied automatically on build

## Running MemLeaker

### Usage

At least one of `nativeMB` or `managedCount` is required.

```
MemLeakerHost.[NetFx|NetCore].exe nativeMB <1-5000> [managedCount <1-5000>] [maxCommitPct <1-99|none>]
MemLeakerHost.[NetFx|NetCore].exe managedCount <1-5000> [maxCommitPct <1-99|none>]
```

### Parameters

- **nativeMB** `<1-5000>` — Native memory to leak per allocation (1-5000 MB)
- **managedCount** `<1-5000>` — Managed objects to leak per allocation (1-5000)
- **maxCommitPct** `<1-99|none>` — Safety threshold for OOM warning
  - `1-99`: warn if predicted commit ≥ this percentage
  - `none`: disable OOM warnings
  - Default: `80`

### Examples

```
MemLeakerHost.NetFx.exe nativeMB 1000 managedCount 100
MemLeakerHost.NetFx.exe nativeMB 500
MemLeakerHost.NetFx.exe nativeMB 1000 maxCommitPct 90
MemLeakerHost.NetFx.exe managedCount 50 maxCommitPct none
```

---

## Scenario 1: Basics

**Objective:**
- Leak 300 MB native memory and 100 managed objects
- Use .NET Framework version
- Dump process while idle and perform basic WinDbg analysis

### 1. Run MemLeaker

```
.\MemLeakerHost.NetFx.exe nativeMB 300 managedCount 100
```

Expected output:
```
Interactive leak mode.
  Native: 300 MB per leak
  Managed: 100 count per leak
=== Leak 1 ===
Current commit: 10781 MB, Predicted after leak: ~11181 MB (39.8%)
Calling native LeakNativeMemory(300 MB)...
LeakNativeMemory: done. 300 MB leaked in 3 chunk(s).
Press ENTER to continue...

Native total so far: 300 MB
Calling managed LeakManagedMemory(100)...
Managed stats: ListCount=100, ApproxListBytes=104857600, EventSubscribers=100, UndisposedStreams=100
Managed total so far: ~100 MB

System:
  Prediction accuracy:  99.9%
  Commit:  11,188 / 28,122 MB ( 39.8%)
  Physical:  9,853 / 24,538 MB ( 40.2%)

This PID: 12732
Time            WS (MB)  Private WS (MB)  Commit (MB)
------------------------------------------------------
05:32:00           428             411         425
```

At this point we have:
- **300 MB** total leaked native memory across 3 × 100 MB segments (chunks)
- **~100 MB** total leaked managed memory across 100 lists, events, streams, and 100 MB byte array

### 2. Full dump the process

Use Task Manager or WinDbg to capture a full user-mode dump.

### 3. Load the dump file into WinDbg

### 4. Enable logging the WinDbg session

Tees the console output to a file:

```
0:000> .logopen C:\data\windbg.log
```

It will remain open until WinDbg closes or:

```
0:000> .logclose
```

### 5. Verify dump file target info

```
0:000> vertarget
```

### 6. Show machine type

```
0:000> .effmach
```

Cross-reference the version with the Windows version table at the end of this guide.

### 7. Enable verbose symbol output and reload symbols

```
0:000> !sym noisy
0:000> .reload /f
0:000> !sym quiet
```

WinDbg will attempt to fetch symbols from local cache and Microsoft's symbol server.

### 8. Get overall memory summary

```
0:000> !address -summary
```

Output similar to:
```
Heap                                     16        0`12e4a000 ( 302.289 MB)   6.08%    0.00%
[...]
MEM_COMMIT                              490        0`20035000 ( 512.207 MB)  10.30%    0.00%
```

We're mainly concerned with:
- **Heap**: Native heap allocations
- **MEM_COMMIT**: Total memory written (committed against commit charge)

### 9. Filter on MEM_COMMIT & Heap

```
0:000> !address -f:MEM_COMMIT,Heap
```

Output:
```
BaseAddress      EndAddress+1        RegionSize     Type       State     Protect             Usage
----------------------------------------------------------
188`255b0000      188`255b2000        0`00002000 MEM_PRIVATE MEM_COMMIT  PAGE_READWRITE      Heap       [ID: 0; Handle: 0000018825710000; Type: Front End]
188`25710000      188`257f8000        0`000e8000 MEM_PRIVATE MEM_COMMIT  PAGE_READWRITE      Heap       [ID: 0; Handle: 0000018825710000; Type: Segment]
188`2717f000      188`27280000        0`00101000 MEM_PRIVATE MEM_COMMIT  PAGE_READWRITE      Heap       [ID: 0; Handle: 0000018825710000; Type: Large Block]
188`3fb5d000      188`45f5e000        0`06401000 MEM_PRIVATE MEM_COMMIT  PAGE_READWRITE      Heap       [ID: 0; Handle: 0000018825710000; Type: Large Block]
188`45f6a000      188`4c36b000        0`06401000 MEM_PRIVATE MEM_COMMIT  PAGE_READWRITE      Heap       [ID: 0; Handle: 0000018825710000; Type: Large Block]
188`4c373000      188`52774000        0`06401000 MEM_PRIVATE MEM_COMMIT  PAGE_READWRITE      Heap       [ID: 0; Handle: 0000018825710000; Type: Large Block]
```

**Note:** RegionSize `0x06401000` = 104,861,696 bytes ≈ 100 MB. Three such blocks = 300 MB total native leak.

### 10. Get managed heap (.NET/CLR) summary

```
0:000> !eeheap -gc
```

Output includes:
```
GC Heap Size:            Size: 0x6a82238 (111682104) bytes.
```

111,682,104 bytes ≈ 100 MB, consistent with ~100 MB total leak of managed memory.

### 11. Get managed heap details

```
0:000> !dumpheap -stat
```

Output:
```
MT                              Count    TotalSize Class Name
00007fff8a264cd8                    1           24 System.Security.Permissions.SecurityPermission
[...]
00007fff2c21c700                  100         2400 MemLeakerHost.Common.ManagedLeaker+EventSubscriber
00007fff8a251278                   84         6232 System.String[]
00007fff8a25ac60                  101         6464 System.EventHandler
00007fff8a25b870                  100         8000 System.IO.MemoryStream
00007fff8a254bc8                   67        16310 System.Char[]
00007fff8a253db8                  750        35488 System.String
00007fff8a254268                   15        37360 System.Object[]
0000018825733660                  441       118208 Free
00007fff8a259018                  213    111424125 System.Byte[]
```

From here it's straightforward:
- Sum of "TotalSize" equals `!eeheap -gc` output
- Note counts of **100** representing leaked objects
- Note totalsize of **~100 MB** representing leaked byte array

### 12. List native heap summaries

```
0:000> !heap -s
```

Output:
```
NT HEAP STATS BELOW
LFH Key                   : 0x131af75568d121e1
Termination on corruption : ENABLED
				   Heap     Flags   Reserv  Commit  Virt   Free  List   UCR  Virt  Lock  Fast 
									 (k)     (k)    (k)     (k) length      blocks cont. heap 
-------------------------------------------------------------------------------------
0000018825710000 08000002    1124    936   1020     22    51     1    4      5   LFH
0000018823c60000 08008000      64      4     64      2     1     1    0      0      
0000018825700000 08001002     164     32     60      4     3     1    0      0   LFH
0000018825c0000 08001002     164     32     60      3     2     1    0      0   LFH
00000188259b0000 08001002      60      8     60      5     1     1    0      0      
00000188272a0000 08001002      60      8     60      2     1     1    0      0      
0000018825930000 08041002      60      8     60      5     1     1    0      0      
```

**Note:** If `gflags.exe +ust` (user backtracing) was enabled, the header would include:
```
NtGlobalFlag enables following debugging aids for new heaps:
	stack back traces
```

If not enabled, those lines would be absent.

### 13. Output native heap detail & stats

```
0:000> !heap -stat -h 18825710000
```

Output:
```
Warning: !ext.heap -stat is deprecated.
	For the latest support, use !exts.heap -s -h <heap address>
heap @ 0000018825710000
group-by: TOTSIZE max-display: 20
	size     #blocks     total     ( %) (percent of total busy bytes)
	6400000 3 - 12c00000  (99.45)
	100000 1 - 100000  (0.33)
	24000 1 - 24000  (0.05)
	[...]
```

**Bingo:** 3 blocks × 0x6400000 (100 MB) = 0x12c00000 (300 MB) accounting for 99% of heap at 0x0000018825710000

### 14. Output details for segments of a specific size

```
0:000> !heap -flt s 6400000
```

Output:
```
_HEAP @ 18825710000
			  HEAP_ENTRY Size Prev Flags            UserPtr UserSize - state
000001883fb5d030 640100 0000  [00]   000001883fb5d060    6400000 - (busy VirtualAlloc)
0000018845f6a030 640100 0100  [00]   0000018845f6a060    6400000 - (busy VirtualAlloc)
000001884c373030 640100 0100  [00]   000001884c373060    6400000 - (busy VirtualAlloc)
```

### 15. Inspect an address

#### Without gflags +ust

```
0:000> !heap -p -a 1883fb5d060
```

Output:
```
address 000001883fb5d060 found in
_HEAP @ 18825710000
				  HEAP_ENTRY Size Prev Flags            UserPtr UserSize - state
		000001883fb5d030 640100 0000  [00]   000001883fb5d060    6400000 - (busy VirtualAlloc)

[end]
```

#### With gflags +ust enabled

```
0:000> !heap -p -a 1883fb5d060
```

Output:
```
address 000001883fb5d060 found in
_HEAP @ 18825710000
				  HEAP_ENTRY Size Prev Flags            UserPtr UserSize - state
		000001883fb5d030 640100 0000  [00]   000001883fb5d060    6400000 - (busy VirtualAlloc)
		7fffef030b7c ntdll!RtlpCallInterceptRoutine+0x00000000000002ac
		7fffef03257d ntdll!RtlpAllocateNTHeapInternal+0x000000000000051d
		7fffef03200b ntdll!RtlAllocateHeap+0x0000000000000abb
		7fffeca550d9 ucrtbase!malloc_base+0x0000000000000039
		7fffabab10f3 NativeLeaker!LeakNativeMemory+0x0000000000000083
		7fff2c3222b6 MemLeakerHost_Common!DomainBoundILStubClass.IL_STUB_PInvoke(Int32)+0x0000000000000076
```

**This demonstrates the value of `gflags.exe +ust`** — the full call stack shows the leak origin.

---

## Windows Version Reference

| Version | Release |
|---|---|
| 10240 | Windows 10 1507, Windows 10 IoT Enterprise 2015 LTSB |
| 10586 | Windows 10 1511 |
| 14393 | Windows 10 1607, Windows Server 2016, Windows 10 IoT Enterprise 2016 LTSB |
| 15063 | Windows 10 1703 |
| 16299 | Windows 10 1709 |
| 17134 | Windows 10 1803 |
| 17763 | Windows 10 1809, Windows Server 2019, Windows 10 IoT Enterprise LTSC 2019 |
| 18362 | Windows 10 1903 |
| 18363 | Windows 10 1909 |
| 19041 | Windows 10 2004 |
| 19042 | Windows 10 20H2 |
| 19043 | Windows 10 21H1 |
| 19044 | Windows 10 21H2, Windows 10 IoT Enterprise LTSC 2021 |
| 19045 | Windows 10 22H2 |
| 20348 | Windows Server 2022 |
| 22000 | Windows 11 21H2 |
| 22621 | Windows 11 22H2 |
| 22631 | Windows 11 23H2 |
| 25398 | Windows Server 2023H2 |
| 26100 | Windows 11 24H2, Windows Server 2025, Windows 11 IoT Enterprise LTSC 2024 |
| 26200 | Windows 11 25H2 |
| 26300 | Windows 11 26H2 |
| 28000 | Windows 11 26H1 |
