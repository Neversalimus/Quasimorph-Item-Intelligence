using System;

namespace ItemIntelligence
{
    // Owns one inspection's stock snapshot and polling schedule. The caller supplies
    // time and a reader of the current save; this component retains no game/UI objects.
    internal sealed class ShipCargoMonitor
    {
        private const float RefreshInterval = 0.5f;
        private readonly Func<string, int?> readCount;
        private float nextCheck;

        internal ShipCargoMonitor(Func<string, int?> readCount)
        {
            if (readCount == null) throw new ArgumentNullException("readCount");
            this.readCount = readCount;
        }

        internal string ItemId { get; private set; }
        internal int? Count { get; private set; }

        internal int? Begin(string itemId, float now)
        {
            ItemId = itemId;
            Count = readCount(itemId);
            nextCheck = now + RefreshInterval;
            return Count;
        }

        internal bool IsDue(string itemId, float now)
        {
            return ItemId != null && string.Equals(ItemId, itemId, StringComparison.Ordinal) &&
                now >= nextCheck;
        }

        internal bool TryRefresh(string itemId, float now, out int? count)
        {
            count = Count;
            if (!IsDue(itemId, now)) return false;
            nextCheck = now + RefreshInterval;
            int? next = readCount(ItemId);
            if (next == Count) return false;
            count = Count = next;
            return true;
        }

        internal void Reset()
        {
            ItemId = null;
            Count = null;
            nextCheck = 0f;
        }
    }
}
