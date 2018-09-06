namespace Microsoft.LocalForwarder.Library.Utils
{
    using System;
    using System.Collections.Concurrent;
    using System.Linq;
    using Microsoft.LocalForwarder.Common;

    /// <summary>
    /// Cache for opencensus clients info shared between sessions.
    /// </summary>
    /// <typeparam name="TKey">Client identifier.</typeparam>
    /// <typeparam name="TValue">Arbitrary client info.</typeparam>
    internal class OpenCensusClientCache<TKey, TValue>
    {
        private readonly int maxItems;
        private readonly TimeSpan timeToLiveAfterUpdateIfOverflow;
        private readonly ConcurrentDictionary<TKey, Item> items;
        private readonly object lck = new object();
        private readonly TimeSpan updateCheckInterval;

        /// <summary>
        /// OpenCensusClientCache constructor.
        /// </summary>
        /// <param name="maxItems">Maximum number of clients to store. </param>
        /// <param name="timeToLiveAfterUpdateIfOverflow">If maxItems is exceeded,
        /// all items that were updated longer than timeToLiveAfterUpdateIfOverflow ago would be removed.</param>
        public OpenCensusClientCache(int maxItems, TimeSpan timeToLiveAfterUpdateIfOverflow)
        {
            this.maxItems = maxItems;
            this.timeToLiveAfterUpdateIfOverflow = timeToLiveAfterUpdateIfOverflow;
            this.items = new ConcurrentDictionary<TKey, Item>();
            this.updateCheckInterval = TimeSpan.FromSeconds(this.timeToLiveAfterUpdateIfOverflow.TotalSeconds / 100);
        }

        /// <summary>
        /// Creates OpenCensusClient cache with 1000 max items and one day expiration.
        /// </summary>
        public OpenCensusClientCache() : this(1000, TimeSpan.FromDays(1))
        {
            this.items = new ConcurrentDictionary<TKey, Item>();
            this.updateCheckInterval = TimeSpan.FromSeconds(this.timeToLiveAfterUpdateIfOverflow.TotalSeconds / 10);
        }

        /// <summary>
        /// Tries to get item value from the cache.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <param name="value">Value in cache or null.</param>
        /// <returns>True if item was found.</returns>
        public bool TryGet(TKey key, out TValue value)
        {
            value = default(TValue);

            if (items.TryGetValue(key, out var item))
            {
                value = item.Value;
                var now = DateTime.UtcNow;

                // TryGet are extremely frequent and concurrent, 
                // so update last ping time only when expiry is approaching 
                // for optimization purposes.
                if (now - item.Updated >= updateCheckInterval)
                {
                    var newItem = new Item(value, now);

                    // if something updates the same value, it will also update the time.
                    // so ignore the result of update
                    items.TryUpdate(key, newItem, item);
                }

                return true;
            }

            return false;
        }

        /// <summary>
        /// Adds or updates client info in the cache.
        /// </summary>
        /// <param name="key">The key to add.</param>
        /// <param name="value">Value to add.</param>
        public TValue AddOrUpdate(TKey key, TValue value)
        {
            if (items.Count >= maxItems)
            {
                RemoveExpiredItems();
            }

            var newItem = new Item(value, DateTime.UtcNow);

            return items.AddOrUpdate(key, newItem, (k,v) => newItem).Value;
        }

        private void RemoveExpiredItems()
        {
            // Removals are extremely rare: happen only when
            // number of monitored app instances exceeds 1000
            lock (lck)
            {
                if (items.Count < maxItems)
                {
                    // perhaps someone already cleaned everything up
                    return;
                }

                var expiryThreshold = DateTime.UtcNow - this.timeToLiveAfterUpdateIfOverflow;

                foreach (var key in items.
                    Where(kvp => kvp.Value.Updated < expiryThreshold).
                    Select(kvp => kvp.Key).
                    ToArray())
                {
                    // remove all items that were last updated longer than 1d (or custom time) ago
                    if (items.TryRemove(key, out var item))
                    {
                        Diagnostics.LogWarn(FormattableString.Invariant($"Removing expired '{key}' from the client cache, last updated: {item.Updated.ToString("o")}"));
                    }
                }

                // ok, we have removed all expired items, but still got more than max items left.
                // this indicates some error case
                var removeCount = items.Count - maxItems;
                if (removeCount > 0)
                {
                    foreach (var key in items
                        .OrderBy(i => i.Value.Updated)
                        .Select(i => i.Key)
                        .Take(removeCount)
                        .ToArray())
                    {
                        // take oldest items and remove them
                        if (items.TryRemove(key, out var item))
                        {
                            Diagnostics.LogError(FormattableString.Invariant($"Removing '{key}' from the client cache, last updated: {item.Updated.ToString("o")}"));
                        }
                    }
                }
            }
        }

        private class Item
        {
            public readonly TValue Value;
            public readonly DateTime Updated;

            public Item(TValue value, DateTime updated)
            {
                this.Value = value;
                this.Updated = updated;
            }
        }
    }
}