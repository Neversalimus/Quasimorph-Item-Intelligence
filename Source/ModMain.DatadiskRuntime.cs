using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;

namespace ItemIntelligence
{
    /// <summary>Production datadisk pool parsing and exact unlock probability ownership.</summary>
    public static partial class ModMain
    {
        private static void AnalyzeDatadiskGraph(string datadiskItemId, List<object> graph)
        {
            if (string.IsNullOrEmpty(datadiskItemId) || graph == null) return;

            for (int i = 0; i < graph.Count; i++)
            {
                object node = graph[i];
                if (node == null || !string.Equals(
                    node.GetType().FullName,
                    "MGSC.DatadiskRecord",
                    StringComparison.Ordinal))
                    continue;

                // Current-build IL proves DatadiskUnlockType ordinal 0 is the production-item path:
                // UnlockDatadisk writes DatadiskComponent.UnlockId directly to MagnumCargo.UnlockedProductionItems.
                object rawUnlockType = GetMember(node, "UnlockType");
                if (rawUnlockType == null) continue;
                int unlockType;
                try { unlockType = Convert.ToInt32(rawUnlockType, CultureInfo.InvariantCulture); }
                catch { continue; }
                if (unlockType != 0) continue;

                List<string> rawPool = ExtractRawStringIds(GetMember(node, "UnlockIds"));
                if (rawPool.Count == 0) continue;

                ProductionDatadiskItemIds.Add(datadiskItemId);
                SetCanonicalDatadiskUnlockPool(datadiskItemId, rawPool);

                for (int n = 0; n < rawPool.Count; n++)
                {
                    string outputItemId = rawPool[n];
                    if (string.IsNullOrEmpty(outputItemId) || !KnownItemIds.Contains(outputItemId)) continue;
                    AddUniqueString(DatadisksByUnlockedItem, outputItemId, datadiskItemId);
                    AddUniqueString(ItemsUnlockedByDatadisk, datadiskItemId, outputItemId);
                }
            }
        }

        private static void SetCanonicalDatadiskUnlockPool(string datadiskItemId, List<string> rawPool)
        {
            if (string.IsNullOrEmpty(datadiskItemId) || rawPool == null || rawPool.Count == 0) return;

            Dictionary<string, int> counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < rawPool.Count; i++)
            {
                string unlockId = rawPool[i];
                if (string.IsNullOrEmpty(unlockId)) continue;
                int current;
                counts.TryGetValue(unlockId, out current);
                counts[unlockId] = current + 1;
            }
            UnlockHitCountsByDatadisk[datadiskItemId] = counts;
            UnlockPoolSizeByDatadisk[datadiskItemId] = rawPool.Count;
        }

        private static List<string> ExtractRawStringIds(object value)
        {
            List<string> result = new List<string>();
            ExtractRawStringIdsInto(value, result);
            return result;
        }

        private static void ExtractRawStringIdsInto(object value, List<string> result)
        {
            if (value == null || result == null) return;
            string direct = value as string;
            if (direct != null)
            {
                if (!string.IsNullOrEmpty(direct)) result.Add(direct);
                return;
            }

            IEnumerable enumerable = value as IEnumerable;
            if (enumerable != null && !(value is IDictionary))
            {
                foreach (object item in enumerable)
                    ExtractRawStringIdsInto(item, result);
                return;
            }

            if (value is IDictionary)
            {
                IDictionary dict = (IDictionary)value;
                foreach (DictionaryEntry entry in dict)
                {
                    string id = GetItemId(entry.Key);
                    if (string.IsNullOrEmpty(id)) id = ConvertToStableString(entry.Key);
                    if (!string.IsNullOrEmpty(id)) result.Add(id);
                }
                return;
            }

            string candidate = FirstNonEmpty(
                GetStringMember(value, "Id"),
                GetStringMember(value, "ItemId"),
                GetStringMember(value, "Key"));
            if (!string.IsNullOrEmpty(candidate)) result.Add(candidate);
        }

        private static void AddUniqueString(Dictionary<string, List<string>> map, string key, string value)
        {
            if (map == null || string.IsNullOrEmpty(key) || string.IsNullOrEmpty(value)) return;
            List<string> list;
            if (!map.TryGetValue(key, out list))
            {
                list = new List<string>();
                map[key] = list;
            }

            for (int i = 0; i < list.Count; i++)
                if (string.Equals(list[i], value, StringComparison.OrdinalIgnoreCase))
                    return;
            list.Add(value);
        }

        private static bool TryGetDatadiskUnlockChance(string datadiskItemId, string outputItemId,
            out int matchingEntries, out int totalEntries, out float chancePercent)
        {
            matchingEntries = 0;
            totalEntries = 0;
            chancePercent = 0f;
            if (string.IsNullOrEmpty(datadiskItemId) || string.IsNullOrEmpty(outputItemId)) return false;
            if (!_chipUnlockChanceContractVerified) return false;

            Dictionary<string, int> counts;
            int cachedTotal;
            if (!UnlockHitCountsByDatadisk.TryGetValue(datadiskItemId, out counts) || counts == null ||
                !UnlockPoolSizeByDatadisk.TryGetValue(datadiskItemId, out cachedTotal) || cachedTotal <= 0)
                return false;

            totalEntries = cachedTotal;
            counts.TryGetValue(outputItemId, out matchingEntries);
            if (matchingEntries <= 0) return false;
            chancePercent = (100f * matchingEntries) / totalEntries;
            return true;
        }

        private static string FormatChipUnlockChance(float percent)
        {
            if (float.IsNaN(percent) || float.IsInfinity(percent)) return "—";
            if (percent >= 99.995f) return "100%";
            if (percent >= 10f) return percent.ToString("0.#", CultureInfo.InvariantCulture) + "%";
            return percent.ToString("0.##", CultureInfo.InvariantCulture) + "%";
        }

        private static List<string> GetDatadiskUnlockedItemsSorted(string datadiskItemId)
        {
            List<string> raw;
            if (string.IsNullOrEmpty(datadiskItemId) ||
                !ItemsUnlockedByDatadisk.TryGetValue(datadiskItemId, out raw) ||
                raw == null || raw.Count == 0)
                return new List<string>();

            List<string> result = new List<string>(raw);
            result.Sort(delegate(string a, string b)
            {
                int ta = GetExactItemTechLevel(a);
                int tb = GetExactItemTechLevel(b);
                int byTech = ta.CompareTo(tb);
                if (byTech != 0) return byTech;
                int byName = string.Compare(LocalizeItem(a), LocalizeItem(b), StringComparison.OrdinalIgnoreCase);
                if (byName != 0) return byName;
                return string.Compare(a, b, StringComparison.OrdinalIgnoreCase);
            });
            return result;
        }

        private sealed class GraphScanNode
        {
            public readonly object Value;
            public readonly int Depth;

            public GraphScanNode(object value, int depth)
            {
                Value = value;
                Depth = depth;
            }
        }

    }
}
