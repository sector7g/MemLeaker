// Copyright (c) 2026 Sector 7G Consulting LLC
// Licensed under the MIT License (see LICENSE file for details)
// Designed by Sector 7G Consulting LLC with code generated using various LLMs.
//
// ManagedLeaker.cs
// .NET Standard 2.0 Managed memory leak library.
// Implements three leak patterns: static collection, event handler subscription, and undisposed resources.
// Usage: MemLeakerHost.Common.ManagedLeaker.LeakManagedMemory(count) applies all three types.

using System;
using System.Collections.Generic;
using System.IO;

namespace MemLeakerHost.Common
{
    public static class ManagedLeaker
    {
        // Leak #1: static collection that is never cleared
        private static readonly List<byte[]> _staticList = new List<byte[]>();

        // Leak #2: static event that subscribers never unsubscribe from
        private static event EventHandler _staticEvent;

        // Leak #3: static list of undisposed IDisposable resources
        private static readonly List<MemoryStream> _undisposedStreams = new List<MemoryStream>();

        /// <summary>
        /// Applies all three managed leak types using the given count.
        /// Count must be between 1 and 5000.
        /// </summary>
        public static void LeakManagedMemory(int count)
        {
            if (count < 1 || count > 5000) return;

            LeakViaStaticList(count);
            LeakViaEventHandlers(count);
            LeakViaUndisposedResources(count);
        }

        /// <summary>
        /// Allocates 'count' byte arrays of ~1 MB each and stores them in a static List.
        /// These objects can never be collected.
        /// </summary>
        public static void LeakViaStaticList(int count)
        {
            if (count <= 0) return;

            for (int i = 0; i < count; i++)
            {
                // ~1 MB array (large object heap for objects >= 85 KB)
                var block = new byte[1024 * 1024];
                // Touch it so it is fully committed
                for (int j = 0; j < block.Length; j += 4096)
                    block[j] = 0xAB;

                _staticList.Add(block);
            }
        }

        /// <summary>
        /// Creates 'count' objects that subscribe to a static event and never unsubscribe.
        /// The static event holds a strong reference to each subscriber forever.
        /// </summary>
        public static void LeakViaEventHandlers(int count)
        {
            if (count <= 0) return;

            for (int i = 0; i < count; i++)
            {
                var subscriber = new EventSubscriber();
                _staticEvent += subscriber.OnEvent;
                // subscriber goes out of scope, but the event still references it
            }
        }

        /// <summary>
        /// Creates 'count' MemoryStreams, writes data to them, and stores them without ever disposing.
        /// This leaks the managed MemoryStream objects (and their internal buffers).
        /// </summary>
        public static void LeakViaUndisposedResources(int count)
        {
            if (count <= 0) return;

            for (int i = 0; i < count; i++)
            {
                // Create a MemoryStream with a modest buffer
                var stream = new MemoryStream(1024 * 64); // 64 KB
                // Write some data so the stream is "used"
                byte[] data = new byte[1024];
                for (int b = 0; b < data.Length; b++)
                    data[b] = 0xAB;
                stream.Write(data, 0, data.Length);
                _undisposedStreams.Add(stream);
                // Intentionally never Dispose/Close
            }
        }

        /// <summary>
        /// Returns current leak statistics (for diagnostics).
        /// </summary>
        public static (int ListCount, long ApproxListBytes, int EventSubscriberCount, int UndisposedStreamCount) GetLeakStats()
        {
            int listCount = _staticList.Count;
            long approxBytes = (long)listCount * 1024 * 1024;
            int eventCount = _staticEvent?.GetInvocationList().Length ?? 0;
            int streamCount = _undisposedStreams.Count;
            return (listCount, approxBytes, eventCount, streamCount);
        }

        // Helper class used only for the event-handler leak
        private class EventSubscriber
        {
            public void OnEvent(object sender, EventArgs e) { /* no-op */ }
        }
    }
}
