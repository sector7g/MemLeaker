# WinDbg Full-Dump Memory Analysis Guide

**A practical command runbook for full user-mode dumps**

Below is a practical WinDbg runbook for a full user-mode dump, especially a 64-bit .NET Framework process. It covers total committed memory, virtual-address classifications, native and managed heaps, stacks, images, handles, GC roots, and a method for identifying the largest consumers.

Important distinction: no single WinDbg command produces a perfect VMMap or DebugDiag report. The closest overall view is !address -summary, supplemented by !heap, SOS commands, thread stacks, module listings, and handle analysis. !address classifies regions such as heap, stack, image, mapped memory, private memory, committed, reserved, and free.

## Contents

- [1. Initial setup and validation](#1-initial-setup-and-validation)
- [2. Automatic crash and exception triage](#2-automatic-crash-and-exception-triage)
- [3. Overall memory summary](#3-overall-memory-summary)
- [4. Export address-region data for sorting](#4-export-address-region-data-for-sorting)
- [5. Process Environment Block and process parameters](#5-process-environment-block-and-process-parameters)
- [6. Loaded images, DLLs, and modules](#6-loaded-images-dlls-and-modules)
- [7. Native thread and stack analysis](#7-native-thread-and-stack-analysis)
- [8. Managed thread and stack analysis](#8-managed-thread-and-stack-analysis)
- [9. Managed heap totals](#9-managed-heap-totals)
- [10. Managed heap object analysis](#10-managed-heap-object-analysis)
- [11. Determine why a managed object is alive](#11-determine-why-a-managed-object-is-alive)
- [12. Managed runtime native overhead](#12-managed-runtime-native-overhead)
- [13. Native Windows heap analysis](#13-native-windows-heap-analysis)
- [14. Segment heap considerations](#14-segment-heap-considerations)
- [15. VirtualAlloc and custom native allocators](#15-virtualalloc-and-custom-native-allocators)
- [16. Handles and kernel-backed resources](#16-handles-and-kernel-backed-resources)
- [17. Images and mapped-file memory](#17-images-and-mapped-file-memory)
- [18. JIT-generated code and managed/native boundaries](#18-jit-generated-code-and-managednative-boundaries)
- [19. COM and interop](#19-com-and-interop)
- [20. Locks, hangs, and memory retention](#20-locks-hangs-and-memory-retention)
- [21. Convert hexadecimal sizes to decimal](#21-convert-hexadecimal-sizes-to-decimal)
- [22. Recommended “where is the memory?” sequence](#22-recommended-where-is-the-memory-sequence)
- [23. How to reconcile the totals](#23-how-to-reconcile-the-totals)
- [24. Key limitations of a single full dump](#24-key-limitations-of-a-single-full-dump)
- [Compact command block](#compact-command-block)
- [References](#references)

## 1. Initial setup and validation

```text
.symfix C:\Symbols
.sympath+ C:\Path\To\Your\Product\PDBs
.reload /f
!sym noisy
.reload
!sym quiet
```

Confirm the dump, target architecture, OS, and debugging environment:

```text
vertarget
version
.effmach
.dumpdebug
|
~
lm
.chain
```

- vertarget identifies the target Windows build.
- .effmach confirms the effective processor architecture.
- .dumpdebug describes the dump streams and dump type.
- | displays process information from the dump.
- ~ lists threads.
- lm lists loaded image modules.
- .chain lists loaded debugger extensions.
Determine whether the target is x86 or x64:

```text
.effmach
r
```

Typical output:

```text
Effective machine: x64 (AMD64)
```

Save the entire session:

```text
.logopen /t C:\Dumps\MemoryAnalysis.txt
```

At the end:

```text
.logclose
```

## 2. Automatic crash and exception triage

Even for a memory investigation, run:

```text
!analyze -v
.lastevent
.ecxr
k
```

For a managed exception:

```text
.loadby sos clr
!pe
!clrstack -a
```

If .loadby sos clr fails for a .NET Framework dump, check that debugger architecture, CLR bitness, SOS bitness, and mscordacwks.dll match the dumped process.

For your 64-bit .NET Framework target, the expected starting command is:

```text
.loadby sos clr
```

Then verify:

```text
.chain
!soshelp
```

The .NET Framework SOS extension provides managed heap, CLR runtime, thread, object, exception, and root-analysis commands.

## 3. Overall memory summary

### Primary DebugDiag/VMMap-style starting point

```text
!address -summary
```

This is the most useful first command for answering:

- How much virtual memory is committed?
- How much is reserved?
- How much address space is free?
- How much is classified as heap?
- How much is stack?
- How much is images?
- How much is mapped files?
- How much is private or unclassified allocation?
!address supports filtering by usage, type, state, and protection, including Heap, Stack, Image, FileMap, MEM_PRIVATE, MEM_MAPPED, MEM_IMAGE, MEM_COMMIT, and MEM_RESERVE.

### Inspect all committed regions

```text
!address -f:MEM_COMMIT
```

### Private committed regions

```text
!address -f:MEM_PRIVATE,MEM_COMMIT
```

This is particularly important because large unexplained native allocations often appear as private committed VAR regions.

### Reserved but not committed

```text
!address -f:MEM_RESERVE
```

Reserved memory consumes address space but generally does not represent committed physical/pagefile backing.

### Images

```text
!address -f:Image
!address -f:MEM_IMAGE
```

### Thread stacks

```text
!address -f:Stack
```

### Windows heaps

```text
!address -f:Heap
```

### Memory-mapped regions

```text
!address -f:MEM_MAPPED
```

Some FileMap behavior is limited for dump analysis, so do not assume every mapped region will retain its original filename in an offline dump.

### Unclassified/private allocator regions

```text
!address -f:VAR
```

This category deserves special attention. It can contain:

- VirtualAlloc allocations
- custom memory pools
- allocator arenas
- JIT or runtime allocations
- native library caches
- memory that WinDbg cannot classify as a standard Windows heap
Inspect a suspicious region:

```text
!address <address>
```

Examples:

```text
!address 000001f4`80000000
db 000001f4`80000000 L100
dq 000001f4`80000000 L20
dc 000001f4`80000000 L40
```

## 4. Export address-region data for sorting

```text
!address -o:csv
```

Committed regions only:

```text
!address -f:MEM_COMMIT -o:csv
```

Private committed regions:

```text
!address -f:MEM_PRIVATE,MEM_COMMIT -o:csv
```

TSV output:

```text
!address -f:MEM_COMMIT -o:tsv
```

This is one of the best ways to identify the largest individual committed virtual-memory regions. Log it to a file, import into Excel or PowerShell, calculate EndAddress - BaseAddress, and sort descending.

## 5. Process Environment Block and process parameters

```text
!peb
```

This shows useful process-level information including:

- executable and command line
- current directory
- environment
- loader data
- process heap address
- process parameters
For the native process environment structures:

```text
dt ntdll!_PEB @$peb
dt ntdll!_TEB @$teb
```

## 6. Loaded images, DLLs, and modules

### Summary list

```text
lm
```

### Verbose list

```text
lmv
```

### User-mode modules

```text
lm u
```

### Module names and base addresses

```text
lma
```

### Specific module

```text
lmvm ModuleName
```

Example:

```text
lmvm clr
lmvm mscorlib
lmvm MyApplication
```

### Find which module owns an address

```text
ln <address>
```

For example:

```text
ln 00007ffb`12345678
```

### Loader information

```text
!peb
```

Then inspect loader lists if necessary:

```text
dt ntdll!_PEB_LDR_DATA <LdrAddress>
```

Remember that lm lists images recorded as loaded in the dumped process. It does not list PDBs loaded only into the debugger session.

## 7. Native thread and stack analysis

### List threads

```text
~
```

### All native stacks

```text
~* k
```

More detail:

```text
~* kb
~* kp
~* kv
```

Recommended:

```text
~* kv
```

kv includes parameters, frame information, and additional stack metadata where available.

### Switch to a thread

```text
~<thread-number>s
```

Example:

```text
~12s
kv
```

### Stack limits and usage for current thread

```text
!teb
```

Inspect:

- StackBase
- StackLimit
- DeallocationStack
The reserved stack size is approximately:

```text
StackBase - DeallocationStack
```

The currently committed stack range is generally related to StackBase - StackLimit, although guard pages and dump capture details must be considered.

### Search all stacks for a module or function

```text
~* k
```

Use WinDbg output filtering:

```text
.shell -ci "~* kv" findstr /i "MyModule MyFunction"
```

### Raw stack memory

On x64:

```text
dq @rsp L100
```

On x86:

```text
dd @esp L100
```

### Examine a stack pointer

```text
!address @rsp
```

## 8. Managed thread and stack analysis

Load SOS:

```text
.loadby sos clr
```

### CLR thread list

```text
!threads
```

### Current managed stack

```text
!clrstack
```

With arguments and locals:

```text
!clrstack -a
```

With parameters:

```text
!clrstack -p
```

With locals:

```text
!clrstack -l
```

### All managed stacks

For .NET Framework SOS, use:

```text
~*e !clrstack
```

With arguments and locals:

```text
~*e !clrstack -a
```

For mixed native and managed analysis:

```text
~*e !clrstack
~* kv
```

### Exception on current thread

```text
!pe
```

### All managed exceptions found on stacks

```text
~*e !pe
```

## 9. Managed heap totals

### GC heap layout and committed segments

```text
!eeheap -gc
```

This is the primary command for:

- GC heap count
- workstation versus server GC structure
- generation segments
- ephemeral segments
- large object heap segments
- segment start and end addresses
- allocated endpoints
- reserved versus committed GC memory
For CLR loader and runtime native structures:

```text
!eeheap -loader
```

### Managed object totals by type

```text
!dumpheap -stat
```

The bottom of the output normally gives total object count and total object bytes.

!dumpheap -stat answers:

- Which managed types have the most instances?
- Which types have the highest aggregate shallow size?
- What is the total size of objects currently represented on the managed heap?
It does not directly show retained size or the total transitive graph owned by each object.

### Per-generation managed statistics

```text
!dumpheap -stat -gen 0
!dumpheap -stat -gen 1
!dumpheap -stat -gen 2
!dumpheap -stat -gen 3
```

For .NET Framework SOS, generation 3 commonly represents the LOH when supported by that SOS version.

If generation filtering is unavailable, use the LOH segment ranges from !eeheap -gc, then:

```text
!dumpheap -stat -startAtLowerBound <LOHStart>
```

Support and exact syntax can vary with SOS version, so verify with:

```text
!soshelp dumpheap
```

## 10. Managed heap object analysis

### Find objects by type name

```text
!dumpheap -type System.String
!dumpheap -type Byte[]
!dumpheap -type MyNamespace.MyType
```

### Find objects by method table

```text
!dumpheap -mt <MethodTable>
```

### Include only objects at least a certain size

```text
!dumpheap -min 85000
```

This is useful for LOH-sized objects, since the traditional LOH threshold is approximately 85,000 bytes.

Examples:

```text
!dumpheap -min 85000 -stat
!dumpheap -type Byte[] -min 85000
!dumpheap -type System.String -min 85000
```

### Find exceptionally large objects

```text
!dumpheap -min 1000000
!dumpheap -min 10000000
!dumpheap -min 100000000
```

### Inspect an object

```text
!do <object-address>
```

Equivalent long name:

```text
!dumpobj <object-address>
```

### Inspect an array

```text
!da <array-address>
```

For details:

```text
!dumparray <array-address>
```

### Inspect a managed string

```text
!do <string-address>
```

Depending on SOS version:

```text
!dumpobj <string-address>
```

### Determine the size of an object graph

```text
!objsize <object-address>
```

This can help estimate the aggregate graph reachable from an object, but it is not equivalent to unique retained size. Shared objects may be counted as reachable from multiple roots.

## 11. Determine why a managed object is alive

This is the critical managed-leak workflow.

1. Find the high-consumption type: !dumpheap -stat
1. List instances: !dumpheap -type MyNamespace.SuspectType
1. Choose an old or representative object: !do <object-address>
1. Find its root chain: !gcroot <object-address>
1. Repeat for multiple instances with !gcroot on second and third object addresses.
```text
!dumpheap -stat
!dumpheap -type MyNamespace.SuspectType
!do <object-address>
!gcroot <object-address>
!gcroot <second-object-address>
!gcroot <third-object-address>
```

Interpret common roots:

- static field
- thread stack local
- GC handle
- finalizer queue
- event delegate
- timer callback
- singleton cache
- native interop handle
- COM wrapper
### Handle roots

```text
!gchandles
```

Look for unusually high counts of:

- strong handles
- pinned handles
- ref-counted handles
- dependent handles
### Finalizer queue

```text
!finalizequeue
```

Warning signs include:

- very large numbers of finalizable objects
- one recurring type dominating the queue
- a blocked finalizer thread
- objects waiting behind a slow or deadlocked finalizer
Find the finalizer thread with:

```text
!threads
```

Then switch to it:

```text
~<thread-number>s
!clrstack
kv
```

### OOM information

```text
!analyzeoom
```

Short alias, where supported:

```text
!ao
```

This reports the most recent managed GC allocation failure recorded by the CLR.

## 12. Managed runtime native overhead

Not all CLR memory is represented by !dumpheap -stat. Run:

```text
!eeheap -loader
```

This covers CLR loader allocator areas such as:

- class loader heaps
- high-frequency heaps
- low-frequency heaps
- stub heaps
- domain-related runtime allocations
Also examine:

```text
!dumpdomain
!assembly
!threads
!gchandles
!finalizequeue
```

Applications that dynamically load many assemblies, generate proxies, compile expressions, create dynamic methods, or repeatedly create unloadable domains can have significant CLR-native overhead even when managed object totals do not look extreme.

Useful commands:

```text
!dumpdomain
!dumpassembly <assembly-address>
!dumpmodule <module-address>
!ip2md <instruction-pointer>
!dumpmd <MethodDesc>
```

## 13. Native Windows heap analysis

The !heap extension supports NT heaps and segment heaps, with some options varying by heap type and Windows version.

### List all native heaps

```text
!heap
```

### Native heap summary

```text
!heap -s
```

This is the primary command for:

- heap count
- committed bytes
- reserved bytes
- allocated bytes
- free bytes
- fragmentation
- per-heap totals
### Detailed analysis of one heap

```text
!heap -a <HeapAddress>
```

Example:

```text
!heap -a 000001f4`12340000
```

Be careful: !heap -a can produce enormous output.

### Native allocation statistics

```text
!heap -stat
```

For a specific heap:

```text
!heap -stat -h <HeapAddress>
```

Show help for grouping options:

```text
!heap -stat -?
!heap -s -?
```

### Find the heap block containing an address

```text
!heap -x <address>
```

More verbose:

```text
!heap -x -v <address>
```

### Validate or triage heap corruption

```text
!heap -triage
```

Or a particular heap:

```text
!heap -triage <HeapAddress>
```

### Search native heaps for a value

```text
!heap -srch <Pattern>
```

### Inspect a suspected allocation

```text
!heap -x <AllocationAddress>
db <AllocationAddress> L100
dq <AllocationAddress> L40
```

#### Critical native-heap limitation

A postmortem dump usually contains current allocation addresses, sizes, heap structures, and current memory contents. It generally does not contain the original allocation call stack unless the process was configured before capture with User-mode stack trace database, GFlags +ust, full page heap, Application Verifier, or instrumentation from DebugDiag or another allocator tracer.

Without prior instrumentation, WinDbg can show what currently exists, but often cannot prove which code originally allocated each native block.

## 14. Segment heap considerations

Modern Windows applications may use the segment heap instead of only classic NT heaps. Start with:

```text
!heap
!heap -s
```

The output identifies heap types where supported. Some older !heap options work only with classic NT heaps, while summary and other newer options support both NT and segment heaps.

If a command produces incomplete information:

```text
!heap -?
!heap -s -?
!heap -stat -?
```

Do not assume older !heap -a parsing will provide complete segment-heap attribution.

## 15. VirtualAlloc and custom native allocators

When !address -f:VAR is large but !heap -s is comparatively small, the likely consumers are outside the standard Windows heaps.

Possibilities include:

- direct VirtualAlloc
- custom slab or arena allocator
- database engine cache
- graphics allocations
- compression buffers
- unmanaged interop buffers
- memory-mapped sections
- CLR runtime-native memory
- JIT code heaps
- thread stacks
- native DLL internal pools
Investigate each large region:

```text
!address <BaseAddress>
```

Examine beginning and end:

```text
db <BaseAddress> L100
db <EndAddress-100> L100
```

Search for recognizable values:

```text
s -a <Start> <End> "ASCII string"
s -u <Start> <End> "Unicode string"
```

Search for a pointer:

```text
s -q <Start> <End> <PointerValue>
```

On 32-bit targets:

```text
s -d <Start> <End> <PointerValue>
```

Map found pointers back to modules and regions:

```text
ln <address>
!address <address>
!heap -x <address>
```

## 16. Handles and kernel-backed resources

A process can appear to leak memory because it leaks objects backed by kernel resources or mapped sections.

### Handle summary

```text
!handle 0 0
```

### More detailed handle information

```text
!handle 0 1
```

### Maximum detail

```text
!handle 0 f
```

This can produce substantial output.

### Filter by type

```text
!handle 0 3 File
!handle 0 3 Section
!handle 0 3 Event
!handle 0 3 Thread
!handle 0 3 Mutant
```

Important handle categories:

- Section: mapped files or shared memory
- File: open files or devices
- Thread: leaked thread handles
- Process: leaked process handles
- Event, Semaphore, Mutant: synchronization objects
- registry keys
- ALPC ports
- tokens
Handle information does not directly equal committed bytes, but a very high handle count can identify the subsystem responsible for associated memory growth.

## 17. Images and mapped-file memory

List images:

```text
lm
lmv
```

Display image headers:

```text
!dh <ModuleBase>
```

Example:

```text
!dh 00007ff7`40000000
```

Map an address to an image:

```text
ln <address>
!address <address>
```

List image regions:

```text
!address -f:Image
!address -f:MEM_IMAGE
```

List mapped regions:

```text
!address -f:MEM_MAPPED
```

Be careful when adding module image sizes. Image virtual size is not automatically equivalent to private committed bytes, resident working set, physical memory uniquely charged to the process, or loaded pages actually present at dump time.

## 18. JIT-generated code and managed/native boundaries

### Map an instruction pointer to a managed method

```text
!ip2md <InstructionPointer>
```

### Inspect the MethodDesc

```text
!dumpmd <MethodDesc>
```

### Managed code heap information

```text
!eeheap -loader
```

### Disassemble method or address

```text
u <address>
uf <function>
```

For managed code:

```text
!u <MethodDesc>
```

Availability depends on the SOS version.

## 19. COM and interop

For applications using COM interop:

```text
!comstate
```

Inspect a Runtime Callable Wrapper:

```text
!dumprcw <address>
```

Inspect a COM Callable Wrapper:

```text
!dumpccw <address>
```

List COM callable wrappers where supported:

```text
!ccw
```

Look for:

- unexpectedly large RCW/CCW populations
- wrappers rooted by static objects
- reference-count cycles
- managed objects held by native COM references
- native objects held by managed wrappers
## 20. Locks, hangs, and memory retention

Memory can remain live because worker threads are blocked while retaining large object graphs.

### Native locks

```text
!locks
```

### Managed locks

```text
!syncblk
```

### Managed threads and stacks

```text
!threads
~*e !clrstack
```

### Native stacks

```text
~* kv
```

Look for:

- blocked finalizer thread
- lock owner holding a large cache
- threads waiting indefinitely
- work queues that keep accumulating objects
- stalled disposal paths
- deadlocked cleanup code
## 21. Convert hexadecimal sizes to decimal

Set the default number base:

```text
n 10
```

Return to hexadecimal:

```text
n 16
```

Display one value in multiple formats:

```text
.formats <value>
```

Example:

```text
.formats 0x12345678
```

Evaluate explicitly in decimal:

```text
? 0n12345678
```

Print an expression as unsigned decimal:

```text
.printf "%I64u\n", <expression>
```

Example:

```text
.printf "%I64u\n", 0x12345678
```

For MB:

```text
.printf "%.2f MB\n", <bytes> / 1024.0 / 1024.0
```

For GB:

```text
.printf "%.2f GB\n", <bytes> / 1024.0 / 1024.0 / 1024.0
```

## 22. Recommended “where is the memory?” sequence

Run these in this order.

### Phase A: Establish the total and categories

```text
!address -summary
!address -f:MEM_PRIVATE,MEM_COMMIT
!address -f:VAR
!address -f:Heap
!address -f:Stack
!address -f:Image
!address -f:MEM_MAPPED
```

### Phase B: Native heaps

```text
!heap
!heap -s
!heap -stat
```

For each unusually large heap:

```text
!heap -stat -h <HeapAddress>
!heap -a <HeapAddress>
```

### Phase C: Managed heap

```text
.loadby sos clr
!eeheap -gc
!dumpheap -stat
!dumpheap -min 85000 -stat
!gchandles
!finalizequeue
!analyzeoom
```

### Phase D: CLR-native overhead

```text
!eeheap -loader
!dumpdomain
!assembly
```

### Phase E: Threads and stacks

```text
~
~* kv
!threads
~*e !clrstack
```

### Phase F: Images and handles

```text
lmv
!handle 0 0
```

### Phase G: Investigate the dominant category

#### If GC Heap is dominant

```text
!dumpheap -stat
!dumpheap -type <SuspectType>
!gcroot <ObjectAddress>
!objsize <ObjectAddress>
```

#### If native heap is dominant

```text
!heap -s
!heap -stat -h <LargestHeap>
!heap -a <LargestHeap>
```

#### If VAR or private committed memory is dominant

```text
!address -f:VAR
!address -f:MEM_PRIVATE,MEM_COMMIT
!address <LargeRegionBase>
```

Then inspect contents and search for pointers or signatures.

#### If stacks are dominant

```text
~
!address -f:Stack
~* kv
```

A process with thousands of threads can consume substantial committed stack memory even if most threads are idle.

#### If images or mapped memory are dominant

```text
lmv
!address -f:MEM_IMAGE
!address -f:MEM_MAPPED
!handle 0 3 Section
```

## 23. How to reconcile the totals

Use this approximate accounting model:

```text
Total process committed virtual memory
    approximately =
      managed GC committed segments
    + native Windows heaps
    + thread stack commitment
    + images/private image pages
    + mapped sections
    + CLR native/runtime memory
    + JIT code heaps
    + direct VirtualAlloc/custom allocator regions
    + miscellaneous process structures
```

Do not simply add every number from every command. Categories can overlap:

- The managed GC heap is ultimately backed by virtual-memory regions.
- Native heaps appear within the process address map.
- Image and mapped regions appear in !address.
- Loader heaps are CLR-native memory, not managed object bytes.
- !dumpheap -stat counts object sizes, not necessarily all committed bytes in GC segments.
- !eeheap -gc includes segment commitment, free space, fragmentation, and allocation headroom.
The comparison you want is:

1. Take total committed from !address -summary.
1. Take GC committed from !eeheap -gc.
1. Take native heap totals from !heap -s.
1. Estimate stack commitment from !address -f:Stack.
1. Evaluate images and mapped sections.
1. Attribute the remainder to VAR, CLR-native, JIT, custom allocators, and miscellaneous private regions.
## 24. Key limitations of a single full dump

A full dump is a snapshot. It normally cannot tell you:

- which allocation type is growing over time
- the original call stack for every native allocation
- whether memory was temporary or permanently leaked
- exact live working-set residency for every page
- complete shared-versus-private physical-page attribution
- the historical high-water mark of individual allocation categories
- which native component allocated arbitrary VirtualAlloc regions
For a conclusive leak investigation, compare at least two full dumps taken:

1. after startup/warm-up
1. after reproducible growth
1. optionally after idle time or forced managed GC
Then compare:

```text
!address -summary
!heap -s
!heap -stat
!eeheap -gc
!dumpheap -stat
!gchandles
!finalizequeue
~
!handle 0 0
```

The category whose committed bytes, allocation count, object count, thread count, or handle count consistently increases is the likely source.

## Compact command block

This is the set I would run first and log:

```text
.logopen /t C:\Dumps\MemoryAnalysis.txt
.symfix C:\Symbols
.sympath+ C:\Path\To\Product\PDBs
.reload /f

vertarget
version
.effmach
.dumpdebug
!analyze -v
!peb

!address -summary
!address -f:MEM_PRIVATE,MEM_COMMIT
!address -f:VAR
!address -f:Heap
!address -f:Stack
!address -f:Image
!address -f:MEM_MAPPED

lmv
~
~* kv
!handle 0 0

!heap
!heap -s
!heap -stat

.loadby sos clr
!threads
~*e !clrstack
!eeheap -gc
!eeheap -loader
!dumpheap -stat
!dumpheap -min 85000 -stat
!gchandles
!finalizequeue
!analyzeoom
!syncblk
!dumpdomain

.logclose
```

That block provides the closest practical WinDbg equivalent to the high-level memory portion of an old DebugDiag report. The next investigative step is determined by the dominant category: GC Heap, native Heap, Stack, Image, MEM_MAPPED, or private/unclassified VAR.

## References

- Microsoft Learn: !address (WinDbg) - memory-region classifications and filters.
- Microsoft Learn: !heap (WinDbg) - NT heap and segment heap analysis.
- Microsoft Learn: SOS.dll (SOS Debugging Extension) for .NET Framework.
- Microsoft Learn: SOS debugging extension - managed stack and heap commands.
