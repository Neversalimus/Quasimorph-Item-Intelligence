using System;
using System.Collections.Generic;

namespace ItemIntelligence
{
    internal sealed class ItemDecayRelation
    {
        public readonly string SourceItemId;
        public readonly string ResultItemId;
        public readonly float ShelfLifeHours;

        public ItemDecayRelation(string sourceItemId, string resultItemId, float shelfLifeHours)
        {
            SourceItemId = sourceItemId;
            ResultItemId = resultItemId ?? string.Empty;
            ShelfLifeHours = shelfLifeHours;
        }
    }

    // Direct transformations only. Keeping both directions together prevents a
    // spoilage chain from being mistaken for an immediate source of its last item.
    internal sealed class ItemDecayIndex
    {
        private readonly Dictionary<string, ItemDecayRelation> results =
            new Dictionary<string, ItemDecayRelation>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, List<ItemDecayRelation>> sources =
            new Dictionary<string, List<ItemDecayRelation>>(StringComparer.OrdinalIgnoreCase);

        public int Count { get { return results.Count; } }

        public void Clear()
        {
            results.Clear();
            sources.Clear();
        }

        public bool Add(string sourceItemId, string resultItemId, float shelfLifeHours)
        {
            if (string.IsNullOrWhiteSpace(sourceItemId) ||
                float.IsNaN(shelfLifeHours) || float.IsInfinity(shelfLifeHours) || shelfLifeHours < 0f ||
                string.Equals(sourceItemId, resultItemId, StringComparison.OrdinalIgnoreCase) ||
                results.ContainsKey(sourceItemId)) return false;

            ItemDecayRelation relation = new ItemDecayRelation(sourceItemId, resultItemId, shelfLifeHours);
            results.Add(sourceItemId, relation);
            if (!string.IsNullOrEmpty(relation.ResultItemId))
            {
                List<ItemDecayRelation> linked;
                if (!sources.TryGetValue(relation.ResultItemId, out linked))
                {
                    linked = new List<ItemDecayRelation>();
                    sources.Add(relation.ResultItemId, linked);
                }
                linked.Add(relation);
            }
            return true;
        }

        public bool TryGetResult(string itemId, out ItemDecayRelation relation)
        {
            relation = null;
            return !string.IsNullOrEmpty(itemId) && results.TryGetValue(itemId, out relation);
        }

        public ItemDecayRelation[] GetSources(string itemId)
        {
            List<ItemDecayRelation> linked;
            return !string.IsNullOrEmpty(itemId) && sources.TryGetValue(itemId, out linked)
                ? linked.ToArray() : new ItemDecayRelation[0];
        }

        public int GetSourceCount(string itemId)
        {
            List<ItemDecayRelation> linked;
            return !string.IsNullOrEmpty(itemId) && sources.TryGetValue(itemId, out linked) ? linked.Count : 0;
        }
    }
}
