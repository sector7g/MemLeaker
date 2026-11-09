// Copyright (c) 2026 Sector 7G Consulting LLC
// Licensed under the MIT License (see LICENSE file for details)
// Designed by Sector 7G Consulting LLC with code generated using various LLMs.
//
// NativeLeaker.cpp
// Native C++ memory leak DLL.
// Exports LeakNativeMemory(int megabytes) which allocates and intentionally leaks heap memory.

#include <windows.h>
#include <cstdlib>
#include <cstring>
#include <iostream>
#include <cstdio>

// Optional: log to console if a console is attached
static void Log(const char* msg) {
    if (GetConsoleWindow() != nullptr) {
        std::cout << msg << std::endl;
    }
}

extern "C" __declspec(dllexport)
void LeakNativeMemory(int megabytes) {
    if (megabytes < 1 || megabytes > 5000) {
        Log("LeakNativeMemory: megabytes must be between 1 and 5000.");
        return;
    }

    // Select chunk size based on allocation size
    // Large allocations: 100 MB chunks, medium: 10 MB chunks, small: 1 MB chunks
    std::size_t chunkSize;
    if (megabytes >= 100) {
        chunkSize = 100ull * 1024 * 1024; // 100 MB chunks
    } else if (megabytes >= 10) {
        chunkSize = 10ull * 1024 * 1024;  // 10 MB chunks
    } else {
        chunkSize = 1ull * 1024 * 1024;   // 1 MB chunks
    }

    std::size_t remaining = static_cast<std::size_t>(megabytes) * 1024 * 1024;
    int iteration = 0;

    while (remaining > 0) {
        std::size_t thisSize = (remaining > chunkSize) ? chunkSize : remaining;
        void* ptr = std::malloc(thisSize);
        if (ptr == nullptr) {
            char buf[128];
            std::snprintf(buf, sizeof(buf),
                          "LeakNativeMemory: malloc failed on iteration %d", iteration + 1);
            Log(buf);
            return;
        }

		// Write "!" in the entire allocation so all reserved memory is committed and with something conspicuous
        std::memset(ptr, 0x21, thisSize);

        // Intentionally do NOT free(ptr)
        remaining -= thisSize;
        ++iteration;
    }

    char summary[128];
    std::snprintf(summary, sizeof(summary),
                  "LeakNativeMemory: done. %d MB leaked in %d chunk(s).",
                  megabytes, iteration);
    Log(summary);

    // Pause and wait for user input to allow time for debugger/dump capture with LeakNativeMemory on the call stack
    if (GetConsoleWindow() != nullptr) {
        std::cout << "Press ENTER to continue..." << std::endl;
        std::cin.get();
    }
}

BOOL APIENTRY DllMain(HMODULE hModule, DWORD ul_reason_for_call, LPVOID lpReserved) {
    switch (ul_reason_for_call) {
    case DLL_PROCESS_ATTACH:
    case DLL_THREAD_ATTACH:
    case DLL_THREAD_DETACH:
    case DLL_PROCESS_DETACH:
        break;
    }
    return TRUE;
}
