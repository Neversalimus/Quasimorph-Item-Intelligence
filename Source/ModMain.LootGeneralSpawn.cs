using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using MGSC;
using UnityEngine;

namespace ItemIntelligence
{
    /// <summary>
    /// v1.7.38 owner for vanilla's second physical-container route.
    /// ItemDropSystem.GenerateItems determines whether an item can exist in the
    /// mission pool; ExordiumDungeonGenerator then places it into a
    /// UseForSpawnItems container whose AllowedItemClasses contains ItemClass.
    /// This index deliberately stores placement eligibility only and never invents
    /// a per-container percentage.
    /// </summary>
    public static partial class ModMain
    {
        private static readonly Dictionary<string, List<string>> LootGeneralSpawnContainersByItem =
            new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        private static int _lootGeneralSpawnContainerCount;
        private static int _lootGeneralSpawnClassPairCount;
        private static int _lootGeneralSpawnPairCount;
        private static readonly HashSet<string> LootGeneralSpawnManualContainerBuffer =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private static readonly List<string> LootGeneralSpawnAdditionalContainerBuffer =
            new List<string>(32);
        private static readonly Dictionary<string, List<string>> LootGeneralSpawnContainersByClassWork =
            new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        private static readonly List<DataEntry> LootGeneralSpawnContainerWork = new List<DataEntry>();
        private static readonly List<string> LootGeneralSpawnClassWork = new List<string>();
        private static readonly List<string> LootGeneralSpawnSortItemWork = new List<string>();
        private static int _lootGeneralSpawnBuildStage;
        private static int _lootGeneralSpawnContainerWorkIndex;
        private static int _lootGeneralSpawnClassWorkIndex;
        private static int _lootGeneralSpawnClassItemIndex;
        private static int _lootGeneralSpawnSortIndex;

        private static void ResetLootGeneralSpawnIndex()
        {
            LootGeneralSpawnContainersByItem.Clear();
            LootGeneralSpawnManualContainerBuffer.Clear();
            LootGeneralSpawnAdditionalContainerBuffer.Clear();
            LootGeneralSpawnContainersByClassWork.Clear();
            LootGeneralSpawnContainerWork.Clear();
            LootGeneralSpawnClassWork.Clear();
            LootGeneralSpawnSortItemWork.Clear();
            _lootGeneralSpawnBuildStage = 0;
            _lootGeneralSpawnContainerWorkIndex = 0;
            _lootGeneralSpawnClassWorkIndex = 0;
            _lootGeneralSpawnClassItemIndex = 0;
            _lootGeneralSpawnSortIndex = 0;
            _lootGeneralSpawnContainerCount = 0;
            _lootGeneralSpawnClassPairCount = 0;
            _lootGeneralSpawnPairCount = 0;
        }

        private static bool TickLootGeneralSpawnIndexSlice()
        {
            if (_lootGeneralSpawnBuildStage == 0)
            {
                // This final Loot phase is now a real state machine. Initialization is
                // deliberately separate from the item x container relation pass so the
                // existing 1 ms outer budget remains authoritative.
                LootGeneralSpawnContainersByItem.Clear();
                LootGeneralSpawnContainersByClassWork.Clear();
                LootGeneralSpawnContainerWork.Clear();
                LootGeneralSpawnClassWork.Clear();
                LootGeneralSpawnSortItemWork.Clear();
                _lootGeneralSpawnContainerCount = 0;
                _lootGeneralSpawnClassPairCount = 0;
                _lootGeneralSpawnPairCount = 0;
                _lootGeneralSpawnContainerWorkIndex = 0;
                _lootGeneralSpawnClassWorkIndex = 0;
                _lootGeneralSpawnClassItemIndex = 0;
                _lootGeneralSpawnSortIndex = 0;
                LootGeneralSpawnContainerWork.AddRange(EnumerateData(
                    GetStaticMember(typeof(Data), "ObstacleContainers")));
                _lootGeneralSpawnBuildStage = 1;
                return false;
            }

            if (_lootGeneralSpawnBuildStage == 1)
            {
                if (_lootGeneralSpawnContainerWorkIndex < LootGeneralSpawnContainerWork.Count)
                {
                    DataEntry entry = LootGeneralSpawnContainerWork[_lootGeneralSpawnContainerWorkIndex++];
                    object record = entry == null ? null : entry.Value;
                    if (record == null || GetBoolMember(record, "UseForSpawnItems") != true) return false;

                    string containerId = FirstNonEmpty(GetStringMember(record, "Id"), entry.Key);
                    if (string.IsNullOrEmpty(containerId)) return false;
                    HashSet<string> allowedClasses =
                        ExtractLootGeneralSpawnEnumValues(GetMember(record, "AllowedItemClasses"));
                    if (allowedClasses.Count == 0) return false;

                    _lootGeneralSpawnContainerCount++;
                    foreach (string itemClass in allowedClasses)
                    {
                        if (string.IsNullOrEmpty(itemClass)) continue;
                        List<string> list;
                        if (!LootGeneralSpawnContainersByClassWork.TryGetValue(itemClass, out list))
                        {
                            list = new List<string>();
                            LootGeneralSpawnContainersByClassWork[itemClass] = list;
                        }
                        if (!list.Contains(containerId))
                        {
                            list.Add(containerId);
                            _lootGeneralSpawnClassPairCount++;
                        }
                    }
                    return false;
                }

                foreach (string itemClass in LootGeneralSpawnContainersByClassWork.Keys)
                    LootGeneralSpawnClassWork.Add(itemClass);
                LootGeneralSpawnClassWork.Sort(StringComparer.OrdinalIgnoreCase);
                _lootGeneralSpawnBuildStage = 2;
                return false;
            }

            if (_lootGeneralSpawnBuildStage == 2)
            {
                if (_lootGeneralSpawnClassWorkIndex >= LootGeneralSpawnClassWork.Count)
                {
                    foreach (string itemId in LootGeneralSpawnContainersByItem.Keys)
                        LootGeneralSpawnSortItemWork.Add(itemId);
                    _lootGeneralSpawnBuildStage = 3;
                    return false;
                }

                string itemClass = LootGeneralSpawnClassWork[_lootGeneralSpawnClassWorkIndex];
                List<LootItemMeta> items;
                if (!LootItemsByItemClass.TryGetValue(itemClass, out items) || items == null ||
                    _lootGeneralSpawnClassItemIndex >= items.Count)
                {
                    _lootGeneralSpawnClassWorkIndex++;
                    _lootGeneralSpawnClassItemIndex = 0;
                    return false;
                }

                LootItemMeta meta = items[_lootGeneralSpawnClassItemIndex++];
                if (meta == null || string.IsNullOrEmpty(meta.ItemId) ||
                    !HasNormalLootGenerationSource(meta.ItemId)) return false;

                List<string> target;
                if (!LootGeneralSpawnContainersByItem.TryGetValue(meta.ItemId, out target))
                {
                    target = new List<string>();
                    LootGeneralSpawnContainersByItem[meta.ItemId] = target;
                }
                List<string> containers = LootGeneralSpawnContainersByClassWork[itemClass];
                for (int c = 0; c < containers.Count; c++)
                {
                    string containerId = containers[c];
                    if (string.IsNullOrEmpty(containerId) || target.Contains(containerId)) continue;
                    target.Add(containerId);
                    _lootGeneralSpawnPairCount++;
                }
                return false;
            }

            if (_lootGeneralSpawnBuildStage == 3)
            {
                if (_lootGeneralSpawnSortIndex < LootGeneralSpawnSortItemWork.Count)
                {
                    string itemId = LootGeneralSpawnSortItemWork[_lootGeneralSpawnSortIndex++];
                    List<string> list;
                    if (LootGeneralSpawnContainersByItem.TryGetValue(itemId, out list) && list != null)
                        list.Sort(StringComparer.OrdinalIgnoreCase);
                    return false;
                }

                // On-demand resolution can populate the same dictionary while the
                // incremental global phase is still running. Recount from the final
                // authoritative dictionary so diagnostics describe the actual index,
                // not only pairs inserted by this state-machine pass.
                _lootGeneralSpawnPairCount = CountLootGeneralSpawnPairs();

                Debug.Log(
                    "[ItemIntelligence] Loot general-spawn index: containers=" +
                    _lootGeneralSpawnContainerCount.ToString(CultureInfo.InvariantCulture) +
                    ", classPairs=" +
                    _lootGeneralSpawnClassPairCount.ToString(CultureInfo.InvariantCulture) +
                    ", items=" +
                    LootGeneralSpawnContainersByItem.Count.ToString(CultureInfo.InvariantCulture) +
                    ", pairs=" +
                    _lootGeneralSpawnPairCount.ToString(CultureInfo.InvariantCulture) +
                    "; semantics=placement-eligibility-only.");
                LootGeneralSpawnContainersByClassWork.Clear();
                LootGeneralSpawnContainerWork.Clear();
                LootGeneralSpawnClassWork.Clear();
                LootGeneralSpawnSortItemWork.Clear();
                _lootGeneralSpawnBuildStage = 4;
                return true;
            }

            return true;
        }

        private static int CountLootGeneralSpawnPairs()
        {
            int count = 0;
            foreach (KeyValuePair<string, List<string>> pair in LootGeneralSpawnContainersByItem)
            {
                if (pair.Value != null) count += pair.Value.Count;
            }
            return count;
        }

        private static bool HasNormalLootGenerationSource(string itemId)
        {
            if (string.IsNullOrEmpty(itemId)) return false;
            List<LootMissionSource> list;
            if (LootStationTypeSourcesByItem.TryGetValue(itemId, out list) && list != null && list.Count > 0)
                return true;
            if (LootFactionSourcesByItem.TryGetValue(itemId, out list) && list != null && list.Count > 0)
                return true;
            if (LootBramfaturaSourcesByItem.TryGetValue(itemId, out list) && list != null && list.Count > 0)
                return true;
            return false;
        }

        // BuildFix1: the global general-spawn index is intentionally the final Loot
        // warmup phase, while BrowserLoot can already contain enemy rows from earlier
        // phases. That made a valid item such as biomonitor appear to have no container
        // sources until the very end of the asynchronous pass (and could leave a stale
        // page if no later render happened). Resolve the currently inspected item
        // directly from the same vanilla contracts. The scan is tiny (42 physical
        // container records + the three static generation-source collections), cached,
        // read-only, and does not consume RNG.
        private static List<string> ResolveLootGeneralSpawnContainersForItem(string itemId)
        {
            if (string.IsNullOrEmpty(itemId)) return null;

            List<string> cached;
            // An empty list is an authoritative negative result too. Reusing it avoids
            // rescanning every physical container whenever the same normal-spawn item
            // has no compatible UseForSpawnItems container.
            if (LootGeneralSpawnContainersByItem.TryGetValue(itemId, out cached) &&
                cached != null)
                return cached;

            if (!LootItemMetaById.ContainsKey(itemId))
                IndexLootItemMeta(itemId);

            LootItemMeta meta;
            if (!LootItemMetaById.TryGetValue(itemId, out meta) || meta == null ||
                string.IsNullOrEmpty(meta.ItemClass))
                return null;

            bool hasNormalSource = HasNormalLootGenerationSource(itemId);
            if (!hasNormalSource)
                hasNormalSource = HasDirectNormalLootGenerationSource(meta);
            if (!hasNormalSource) return null;

            List<string> result = new List<string>();
            List<DataEntry> containerEntries = EnumerateData(
                GetStaticMember(typeof(Data), "ObstacleContainers"));
            // Do not negative-cache a temporarily unavailable vanilla table.
            if (containerEntries == null || containerEntries.Count == 0) return null;
            for (int i = 0; i < containerEntries.Count; i++)
            {
                object record = containerEntries[i].Value;
                if (record == null || GetBoolMember(record, "UseForSpawnItems") != true)
                    continue;

                HashSet<string> allowedClasses =
                    ExtractLootGeneralSpawnEnumValues(GetMember(record, "AllowedItemClasses"));
                if (!allowedClasses.Contains(meta.ItemClass)) continue;

                string containerId = FirstNonEmpty(
                    GetStringMember(record, "Id"),
                    containerEntries[i].Key);
                if (!string.IsNullOrEmpty(containerId) && !result.Contains(containerId))
                    result.Add(containerId);
            }

            result.Sort(StringComparer.OrdinalIgnoreCase);
            LootGeneralSpawnContainersByItem[itemId] = result;
            Debug.Log(
                "[ItemIntelligence][LootGeneralSpawn][OnDemand] item=" + itemId +
                ", itemClass=" + meta.ItemClass +
                ", containers=" + result.Count.ToString(CultureInfo.InvariantCulture) +
                ", normalSource=true.");
            return result;
        }

        private static bool HasDirectNormalLootGenerationSource(LootItemMeta meta)
        {
            if (meta == null || meta.Categories == null || meta.Categories.Count == 0)
                return false;

            if (CollectionHasDirectNormalLootGenerationSource("StationTypes", meta)) return true;
            if (CollectionHasDirectNormalLootGenerationSource("Factions", meta)) return true;
            if (CollectionHasDirectNormalLootGenerationSource("Bramfaturas", meta)) return true;
            return false;
        }

        private static bool CollectionHasDirectNormalLootGenerationSource(
            string dataMember,
            LootItemMeta meta)
        {
            List<DataEntry> entries = EnumerateData(GetStaticMember(typeof(Data), dataMember));
            for (int i = 0; i < entries.Count; i++)
            {
                object record = entries[i].Value;
                if (record == null) continue;

                HashSet<string> sourceCategories =
                    ExtractStableStringSet(GetMember(record, "ItemDropCategories"));
                if (sourceCategories.Count == 0) continue;

                HashSet<string> forbiddenClasses =
                    ExtractStableStringSet(GetMember(record, "ForbiddenItemClasses"));
                if (!string.IsNullOrEmpty(meta.ItemClass) &&
                    forbiddenClasses.Contains(meta.ItemClass))
                    continue;

                foreach (string category in meta.Categories)
                {
                    if (!string.IsNullOrEmpty(category) && sourceCategories.Contains(category))
                        return true;
                }
            }
            return false;
        }

        private static HashSet<string> ExtractLootGeneralSpawnEnumValues(object raw)
        {
            HashSet<string> result =
                new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (raw == null) return result;

            IEnumerable values = raw as IEnumerable;
            if (values == null || raw is string)
            {
                AddLootGeneralSpawnEnumValue(result, raw);
                return result;
            }

            int scanned = 0;
            foreach (object value in values)
            {
                if (++scanned > 256) break;
                AddLootGeneralSpawnEnumValue(result, value);
            }
            return result;
        }

        private static void AddLootGeneralSpawnEnumValue(HashSet<string> result, object value)
        {
            if (result == null || value == null) return;
            string id = ConvertToStableString(value);
            if (string.IsNullOrEmpty(id))
            {
                try { id = value.ToString(); }
                catch { id = string.Empty; }
            }
            if (!string.IsNullOrEmpty(id)) result.Add(id);
        }
        private static void AppendLootGeneralSpawnContainerLines(
            string itemId,
            List<LootContainerSource> manualContainers,
            int itemTech,
            ref bool any)
        {
            if (string.IsNullOrEmpty(itemId)) return;

            // The current item must not wait for the last asynchronous warmup phase.
            // This also guarantees a fresh render can recover the general-spawn section
            // even if the page was opened while enemy indexing was still in progress.
            List<string> generalContainers =
                ResolveLootGeneralSpawnContainersForItem(itemId);
            if (generalContainers == null || generalContainers.Count == 0)
                return;

            LootGeneralSpawnManualContainerBuffer.Clear();
            LootGeneralSpawnAdditionalContainerBuffer.Clear();
            if (manualContainers != null)
            {
                for (int i = 0; i < manualContainers.Count; i++)
                {
                    LootContainerSource manual = manualContainers[i];
                    if (manual != null && !string.IsNullOrEmpty(manual.ContainerId))
                        LootGeneralSpawnManualContainerBuffer.Add(manual.ContainerId);
                }
            }

            // generalContainers is already stored in deterministic sorted order.
            // Filtering it into a reusable buffer preserves that order and avoids a
            // HashSet/List allocation plus a second sort on every modifier toggle.
            for (int i = 0; i < generalContainers.Count; i++)
            {
                string containerId = generalContainers[i];
                if (!string.IsNullOrEmpty(containerId) &&
                    !LootGeneralSpawnManualContainerBuffer.Contains(containerId))
                    LootGeneralSpawnAdditionalContainerBuffer.Add(containerId);
            }
            if (LootGeneralSpawnAdditionalContainerBuffer.Count == 0) return;

            any = true;
            BrowserLines.Add(
                BrowserLine.Section(
                    Ui("ui.other_containers") +
                    "  •  " + LootGeneralSpawnAdditionalContainerBuffer.Count.ToString(CultureInfo.InvariantCulture)));
            BrowserLines.Add(
                BrowserLine.LootHeader(
                    Ui("loot.column.container_profile"),
                    Ui("ui.source"),
                    Ui("ui.chance"),
                    Ui("ui.tech"),
                    Ui("ui.rolls")));

            for (int i = 0; i < LootGeneralSpawnAdditionalContainerBuffer.Count; i++)
            {
                string containerId = LootGeneralSpawnAdditionalContainerBuffer[i];
                BrowserLines.Add(
                    BrowserLine.LootContainerRow(
                        containerId,
                        ResolveLootContainerName(containerId),
                        Ui("ui.mission_generation"),
                        "—",
                        itemTech > 0
                            ? "T" + itemTech.ToString(CultureInfo.InvariantCulture) + "+"
                            : Ui("ui.any"),
                        "—"));
            }

            AddWrappedLootNote("loot.note.general_spawn");
        }

    }
}
