using System;
using System.Collections;
using System.Collections.Generic;

namespace ItemIntelligence
{
    /// <summary>
    /// Shared, read-only item metadata resolver. Data.Items can expose wrapper records
    /// whose actual ItemRecord lives under Records/Record/ItemRecord. Keep TechLevel
    /// resolution independent from Loot warmup so Catalog, Advanced Search and Modder
    /// Mode can all use the same exact source.
    /// </summary>
    public static partial class ModMain
    {
        private static readonly Dictionary<string, object> CanonicalItemMetadataRecordsById =
            new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, int> ExactItemTechLevelsById =
            new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        private static readonly HashSet<string> UnresolvedItemMetadataIds =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        private sealed class ItemMetadataScanNode
        {
            public readonly object Value;
            public readonly int Depth;

            public ItemMetadataScanNode(object value, int depth)
            {
                Value = value;
                Depth = depth;
            }
        }

        private static void ResetItemMetadataResolverState()
        {
            CanonicalItemMetadataRecordsById.Clear();
            ExactItemTechLevelsById.Clear();
            UnresolvedItemMetadataIds.Clear();
        }

        private static bool IsCanonicalItemMetadataRecord(object value)
        {
            if (value == null || value is string) return false;
            Type type = value.GetType();
            return FindCachedMember(type, "Categories", false) != null &&
                   FindCachedMember(type, "TechLevel", false) != null;
        }

        private static bool IsItemMetadataSimple(Type type)
        {
            return type == null || type.IsPrimitive || type.IsEnum ||
                   type == typeof(string) || type == typeof(decimal) ||
                   type == typeof(DateTime) || type == typeof(Type);
        }

        // Called by existing graph consumers (Catalog classification) so the normal
        // time-sliced browser warmup primes TechLevel without a second graph walk.
        private static void ObserveCanonicalItemMetadataNode(string itemId, object value)
        {
            if (string.IsNullOrEmpty(itemId) || value == null ||
                CanonicalItemMetadataRecordsById.ContainsKey(itemId) ||
                !IsCanonicalItemMetadataRecord(value))
                return;

            CanonicalItemMetadataRecordsById[itemId] = value;
            UnresolvedItemMetadataIds.Remove(itemId);

            int tech;
            if (TryToInt(GetMember(value, "TechLevel"), out tech))
                ExactItemTechLevelsById[itemId] = Math.Max(0, tech);
        }

        private static object ResolveCanonicalItemMetadataRecord(string itemId)
        {
            if (string.IsNullOrEmpty(itemId)) return null;

            object cached;
            if (CanonicalItemMetadataRecordsById.TryGetValue(itemId, out cached) && cached != null)
                return cached;
            if (UnresolvedItemMetadataIds.Contains(itemId)) return null;

            object root;
            if (!ItemRecordsById.TryGetValue(itemId, out root) || root == null)
                return null;

            Queue<ItemMetadataScanNode> queue = new Queue<ItemMetadataScanNode>();
            HashSet<object> seen = new HashSet<object>(ReferenceComparer.Instance);
            queue.Enqueue(new ItemMetadataScanNode(root, 0));

            int inspected = 0;
            while (queue.Count > 0 && inspected < 64)
            {
                ItemMetadataScanNode current = queue.Dequeue();
                object value = current.Value;
                if (value == null || value is string) continue;

                Type type = value.GetType();
                if (IsItemMetadataSimple(type) || seen.Contains(value)) continue;
                seen.Add(value);
                inspected++;

                if (IsCanonicalItemMetadataRecord(value))
                {
                    CanonicalItemMetadataRecordsById[itemId] = value;
                    UnresolvedItemMetadataIds.Remove(itemId);

                    int tech;
                    if (TryToInt(GetMember(value, "TechLevel"), out tech))
                        ExactItemTechLevelsById[itemId] = Math.Max(0, tech);
                    return value;
                }

                if (current.Depth >= 3) continue;

                object records = GetMember(value, "Records");
                IEnumerable children = records as IEnumerable;
                if (children != null && !(records is string))
                {
                    int count = 0;
                    foreach (object child in children)
                    {
                        if (++count > 64) break;
                        if (child != null) queue.Enqueue(new ItemMetadataScanNode(child, current.Depth + 1));
                    }
                }

                string[] direct = { "Record", "ItemRecord", "PrimaryRecord", "ContentRecord" };
                for (int i = 0; i < direct.Length; i++)
                {
                    object child = GetMember(value, direct[i]);
                    if (child != null && !object.ReferenceEquals(child, value))
                        queue.Enqueue(new ItemMetadataScanNode(child, current.Depth + 1));
                }
            }

            UnresolvedItemMetadataIds.Add(itemId);
            return null;
        }

        private static bool TryGetExactItemTechLevel(string itemId, out int techLevel)
        {
            techLevel = 0;
            if (string.IsNullOrEmpty(itemId)) return false;
            if (ExactItemTechLevelsById.TryGetValue(itemId, out techLevel)) return true;

            object record = ResolveCanonicalItemMetadataRecord(itemId);
            if (record == null) return false;

            int parsed;
            if (!TryToInt(GetMember(record, "TechLevel"), out parsed)) return false;
            techLevel = Math.Max(0, parsed);
            ExactItemTechLevelsById[itemId] = techLevel;
            return true;
        }

        private static int GetExactItemTechLevel(string itemId)
        {
            int techLevel;
            return TryGetExactItemTechLevel(itemId, out techLevel) ? techLevel : 0;
        }
    }
}
