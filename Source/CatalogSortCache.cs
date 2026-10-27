using System;
using System.Collections.Generic;

namespace ItemIntelligence
{
    // Retains only item IDs and the previous ordering. Filtering still runs against
    // current data before this cache is consulted; live Tech sorting bypasses it.
    internal sealed class CatalogSortCache
    {
        private readonly List<string> sourceOrder = new List<string>();
        private readonly List<string> sortedOrder = new List<string>();
        private bool valid;
        private int revision;
        private int mode;
        private bool descending;
        private string culture;

        internal bool Sort(List<string> items, Comparison<string> compare, int nextRevision,
            int nextMode, bool nextDescending, string nextCulture, bool allowReuse)
        {
            if (items == null) throw new ArgumentNullException("items");
            if (compare == null) throw new ArgumentNullException("compare");
            bool reuse = allowReuse && valid && revision == nextRevision && mode == nextMode &&
                descending == nextDescending && string.Equals(culture, nextCulture, StringComparison.Ordinal) &&
                sourceOrder.Count == items.Count;
            if (reuse)
            {
                for (int i = 0; i < items.Count; i++)
                {
                    if (!string.Equals(sourceOrder[i], items[i], StringComparison.Ordinal))
                    {
                        reuse = false;
                        break;
                    }
                }
            }
            if (reuse)
            {
                items.Clear();
                items.AddRange(sortedOrder);
                return true;
            }

            Reset();
            if (allowReuse) sourceOrder.AddRange(items);
            items.Sort(compare);
            if (allowReuse)
            {
                sortedOrder.AddRange(items);
                revision = nextRevision;
                mode = nextMode;
                descending = nextDescending;
                culture = nextCulture;
                valid = true;
            }
            return false;
        }

        internal void Reset()
        {
            valid = false;
            culture = null;
            sourceOrder.Clear();
            sortedOrder.Clear();
        }
    }
}
