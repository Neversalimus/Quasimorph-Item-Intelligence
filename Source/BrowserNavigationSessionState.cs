using System;
using System.Collections.Generic;

namespace ItemIntelligence
{
    internal sealed class BrowserNavigationSnapshot
    {
        internal readonly string ItemId;
        internal readonly int Tab, ScrollOffset;
        internal BrowserNavigationSnapshot(string itemId, int tab, int scrollOffset)
        {
            ItemId = itemId ?? string.Empty;
            Tab = tab;
            ScrollOffset = scrollOffset;
        }
    }

    // Owns navigation values only. Game lookups and window/render side effects stay
    // in the adapter; callers cannot mutate the history or per-tab scroll storage.
    internal sealed class BrowserNavigationSessionState
    {
        private readonly int[] scrollOffsets;
        private readonly int historyLimit;
        private readonly List<BrowserNavigationSnapshot> history = new List<BrowserNavigationSnapshot>();

        internal BrowserNavigationSessionState(int tabCount, int historyLimit)
        {
            if (tabCount <= 0) throw new ArgumentOutOfRangeException("tabCount");
            if (historyLimit <= 0) throw new ArgumentOutOfRangeException("historyLimit");
            scrollOffsets = new int[tabCount];
            this.historyLimit = historyLimit;
        }

        internal int Tab { get; private set; }
        internal int ScrollOffset { get; private set; }
        internal int HistoryCount { get { return history.Count; } }

        internal void Begin(int tab)
        {
            Array.Clear(scrollOffsets, 0, scrollOffsets.Length);
            history.Clear();
            Tab = ClampTab(tab);
            ScrollOffset = 0;
        }

        internal void Close()
        {
            ScrollOffset = 0;
            history.Clear();
        }

        internal void StartItem(int tab)
        {
            Tab = ClampTab(tab);
            SetScrollOffset(0);
        }

        internal bool SwitchTab(int tab)
        {
            int next = ClampTab(tab);
            if (Tab == next) return false;
            scrollOffsets[Tab] = ScrollOffset;
            Tab = next;
            ScrollOffset = scrollOffsets[Tab];
            return true;
        }

        internal void SetScrollOffset(int offset)
        {
            ScrollOffset = Math.Max(0, offset);
            scrollOffsets[Tab] = ScrollOffset;
        }

        internal void ClampScrollOffset(int rowCount, int visibleRows)
        {
            SetScrollOffset(Math.Min(ScrollOffset, Math.Max(0, rowCount - visibleRows)));
        }

        internal void Push(string itemId)
        {
            if (string.IsNullOrEmpty(itemId)) return;
            history.Add(new BrowserNavigationSnapshot(itemId, Tab, ScrollOffset));
            if (history.Count > historyLimit) history.RemoveAt(0);
        }

        internal bool TryBack(Func<string, bool> isKnownItem, out BrowserNavigationSnapshot snapshot)
        {
            if (isKnownItem == null) throw new ArgumentNullException("isKnownItem");
            snapshot = null;
            while (history.Count > 0)
            {
                int last = history.Count - 1;
                BrowserNavigationSnapshot entry = history[last];
                history.RemoveAt(last);
                if (!isKnownItem(entry.ItemId)) continue;
                snapshot = entry;
                return true;
            }
            return false;
        }

        internal void Restore(BrowserNavigationSnapshot snapshot)
        {
            if (snapshot == null) throw new ArgumentNullException("snapshot");
            // The adapter closes the previous item's overlays before applying this
            // snapshot, preserving the ordering of native UI callbacks.
            Tab = ClampTab(snapshot.Tab);
            SetScrollOffset(snapshot.ScrollOffset);
        }

        private int ClampTab(int tab)
        {
            return Math.Max(0, Math.Min(scrollOffsets.Length - 1, tab));
        }
    }
}
