using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace ItemIntelligence
{
    /// <summary>Production unlock state and recipe-chip presentation data ownership.</summary>
    public static partial class ModMain
    {
        private static List<RecipeUseGroup> ConsolidateRecipeUseFamilies(List<RecipeUseGroup> raw)
        {
            if (raw == null || raw.Count <= 1) return raw ?? new List<RecipeUseGroup>();

            Dictionary<string, List<RecipeUseGroup>> byDisplay =
                new Dictionary<string, List<RecipeUseGroup>>(StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < raw.Count; i++)
            {
                RecipeUseGroup group = raw[i];
                if (group == null) continue;
                string display = NormalizeGameText(LocalizeItem(group.OutputItemId));
                string key = display + "|" + (group.Kind ?? string.Empty);
                List<RecipeUseGroup> list;
                if (!byDisplay.TryGetValue(key, out list))
                {
                    list = new List<RecipeUseGroup>();
                    byDisplay[key] = list;
                }
                list.Add(group);
            }

            List<RecipeUseGroup> result = new List<RecipeUseGroup>();
            foreach (KeyValuePair<string, List<RecipeUseGroup>> pair in byDisplay)
            {
                List<RecipeUseGroup> family = pair.Value;
                if (family == null || family.Count == 0) continue;
                if (family.Count == 1)
                {
                    result.Add(family[0]);
                    continue;
                }

                HashSet<string> distinctChips = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                for (int i = 0; i < family.Count; i++)
                {
                    RecipeUseGroup group = family[i];
                    if (group == null) continue;
                    for (int n = 0; n < group.OutputItemIds.Count; n++)
                    {
                        List<string> disks;
                        if (!DatadisksByUnlockedItem.TryGetValue(group.OutputItemIds[n], out disks) || disks == null) continue;
                        for (int d = 0; d < disks.Count; d++)
                            if (!string.IsNullOrEmpty(disks[d])) distinctChips.Add(disks[d]);
                    }
                }

                // Same displayed family + zero/one distinct chip is a single vanilla unlock
                // family. If multiple different chips exist, keep rows separate: Quasimorph
                // has a few same-name cases that really are unlocked independently.
                if (distinctChips.Count <= 1)
                {
                    RecipeUseGroup merged = new RecipeUseGroup(family[0].OutputItemId, family[0].Kind);
                    merged.OutputItemIds.Clear();
                    merged.Variants = 0;
                    merged.MinQuantity = 0;
                    merged.MaxQuantity = 0;

                    for (int i = 0; i < family.Count; i++)
                    {
                        RecipeUseGroup group = family[i];
                        if (group == null) continue;
                        merged.Variants += Math.Max(1, group.Variants);
                        if (merged.MinQuantity <= 0 || (group.MinQuantity > 0 && group.MinQuantity < merged.MinQuantity))
                            merged.MinQuantity = group.MinQuantity;
                        if (group.MaxQuantity > merged.MaxQuantity) merged.MaxQuantity = group.MaxQuantity;

                        for (int n = 0; n < group.OutputItemIds.Count; n++)
                            if (!merged.OutputItemIds.Contains(group.OutputItemIds[n]))
                                merged.OutputItemIds.Add(group.OutputItemIds[n]);
                        for (int n = 0; n < group.RecipeIds.Count; n++)
                            if (!merged.RecipeIds.Contains(group.RecipeIds[n]))
                                merged.RecipeIds.Add(group.RecipeIds[n]);
                    }

                    result.Add(merged);
                }
                else
                {
                    result.AddRange(family);
                }
            }

            return result;
        }

        private static string GetFamilyPrimaryDatadisk(List<string> outputItemIds)
        {
            if (outputItemIds == null || outputItemIds.Count == 0) return string.Empty;
            HashSet<string> distinct = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < outputItemIds.Count; i++)
            {
                List<string> disks;
                if (!DatadisksByUnlockedItem.TryGetValue(outputItemIds[i], out disks) || disks == null) continue;
                for (int d = 0; d < disks.Count; d++)
                    if (!string.IsNullOrEmpty(disks[d])) distinct.Add(disks[d]);
            }

            if (distinct.Count != 1) return string.Empty;
            foreach (string value in distinct) return value;
            return string.Empty;
        }

        private static int GetFamilyDatadiskStatus(List<string> outputItemIds, string datadiskItemId)
        {
            if (string.IsNullOrEmpty(datadiskItemId)) return 0;
            if (outputItemIds == null || outputItemIds.Count == 0) return 2;

            bool sawKnown = false;
            for (int i = 0; i < outputItemIds.Count; i++)
            {
                bool? unlocked = IsProductionItemUnlocked(outputItemIds[i]);
                if (!unlocked.HasValue) continue;
                sawKnown = true;
                if (unlocked.Value) return 1;
            }

            return sawKnown ? -1 : 2;
        }

        private static BrowserLine ItemWithProductionChip(string itemId, string right)
        {
            string chipItemId = GetPrimaryDatadiskForItem(itemId);
            int chipStatus = GetDatadiskStatus(itemId, chipItemId);
            return BrowserLine.RecipeItem(itemId, right, chipItemId, chipStatus);
        }

        private static void EnsureUnlockedProductionItemsResolved()
        {
            if (_unlockedProductionResolveAttempted) return;
            _unlockedProductionResolveAttempted = true;

            try
            {
                object state = null;
                try { if (_modContext != null) state = _modContext.State; }
                catch { if (_modContext != null) state = GetMember(_modContext, "State"); }
                if (state == null) return;

                _unlockedProductionItems = FindNamedStateValue(
                    state,
                    "UnlockedProductionItems",
                    3,
                    new HashSet<object>(ReferenceComparer.Instance),
                    new int[] { 0 });

                if (_unlockedProductionItems != null)
                {
                    Debug.Log("[ItemIntelligence] Production unlock state resolved: " +
                        _unlockedProductionItems.GetType().FullName + ".");
                        QueueBrowserRowsRefresh(); // QII_MAGNUM_REFRESH_PRODUCTION
                }
                else
                {
                    Debug.LogWarning("[ItemIntelligence] Production unlock state member UnlockedProductionItems was not found.");
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[ItemIntelligence] Production unlock state resolver failed: " + ex.Message);
            }
        }

        private static object FindNamedStateValue(object value, string memberName, int depth, HashSet<object> visited, int[] inspected)
        {
            if (value == null || depth < 0 || visited == null || inspected == null) return null;
            if (inspected[0]++ > 512) return null;

            Type type = value.GetType();
            if (IsSimple(type)) return null;
            if (visited.Contains(value)) return null;
            visited.Add(value);

            object direct = GetMember(value, memberName);
            if (direct != null) return direct;

            if (depth == 0) return null;

            IDictionary dict = value as IDictionary;
            if (dict != null)
            {
                int count = 0;
                foreach (DictionaryEntry entry in dict)
                {
                    if (++count > 96) break;
                    object found = FindNamedStateValue(entry.Value, memberName, depth - 1, visited, inspected);
                    if (found != null) return found;
                }
                return null;
            }

            string ns = type.Namespace ?? string.Empty;
            if (!ns.StartsWith("MGSC", StringComparison.Ordinal))
                return null;

            List<MemberInfo> members = GetReadableMembers(type);
            for (int i = 0; i < members.Count; i++)
            {
                object child = GetMemberValue(value, members[i]);
                if (child == null || child is string) continue;

                Type childType = child.GetType();
                if (IsSimple(childType)) continue;

                object found = FindNamedStateValue(child, memberName, depth - 1, visited, inspected);
                if (found != null) return found;
            }
            return null;
        }

        private static bool? IsProductionItemUnlocked(string itemId)
        {
            if (string.IsNullOrEmpty(itemId)) return null;
            EnsureUnlockedProductionItemsResolved();
            if (_unlockedProductionItems == null) return null;

            try
            {
                MethodInfo contains = _unlockedProductionItems.GetType().GetMethod(
                    "Contains",
                    InstanceFlags,
                    null,
                    new Type[] { typeof(string) },
                    null);
                if (contains != null)
                {
                    object raw = contains.Invoke(_unlockedProductionItems, new object[] { itemId });
                    if (raw is bool) return (bool)raw;
                }
            }
            catch { }

            IDictionary dict = _unlockedProductionItems as IDictionary;
            if (dict != null)
            {
                try
                {
                    if (dict.Contains(itemId)) return true;
                    foreach (DictionaryEntry entry in dict)
                    {
                        string keyId = GetItemIdDeep(entry.Key, 0);
                        if (string.IsNullOrEmpty(keyId)) keyId = entry.Key as string;
                        if (string.Equals(keyId, itemId, StringComparison.OrdinalIgnoreCase)) return true;

                        string valueId = GetItemIdDeep(entry.Value, 0);
                        if (string.Equals(valueId, itemId, StringComparison.OrdinalIgnoreCase)) return true;
                    }
                    return false;
                }
                catch { }
            }

            IEnumerable enumerable = _unlockedProductionItems as IEnumerable;
            if (enumerable != null && !(_unlockedProductionItems is string))
            {
                try
                {
                    int count = 0;
                    foreach (object entry in enumerable)
                    {
                        if (++count > 4096) break;
                        if (entry == null) continue;

                        string id = entry as string;
                        if (string.IsNullOrEmpty(id)) id = GetItemIdDeep(entry, 0);
                        if (string.IsNullOrEmpty(id))
                            id = FirstNonEmpty(GetStringMember(entry, "Id"), GetStringMember(entry, "ItemId"));

                        if (string.Equals(id, itemId, StringComparison.OrdinalIgnoreCase))
                            return true;
                    }
                    return false;
                }
                catch { }
            }

            return null;
        }

        private static string GetPrimaryDatadiskForItem(string outputItemId)
        {
            List<string> disks;
            if (string.IsNullOrEmpty(outputItemId) ||
                !DatadisksByUnlockedItem.TryGetValue(outputItemId, out disks) ||
                disks == null ||
                disks.Count == 0)
                return string.Empty;

            return disks[0] ?? string.Empty;
        }

        private static int GetDatadiskStatus(string outputItemId, string datadiskItemId)
        {
            if (string.IsNullOrEmpty(datadiskItemId)) return 0;
            bool? unlocked = IsProductionItemUnlocked(outputItemId);
            if (!unlocked.HasValue) return 2; // unknown state: neutral marker
            return unlocked.Value ? 1 : -1;
        }









































    }
}
