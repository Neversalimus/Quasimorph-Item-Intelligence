using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using HarmonyLib;
using MGSC;
using UnityEngine;

namespace ItemIntelligence
{
    public static partial class ModMain
    {
        // Core-index ownership boundary. Runtime owns lifecycle hooks; this module owns
        // the shared index state, rebuild gate, stage order, and cross-feature reset fan-out.
        private static bool _indexesBuilt;
        private static float _lastCoreIndexBuildMs;
        private static int _lastCoreIndexBuildFrame = -1000;

        private static readonly Dictionary<string, PriceSnapshot> PriceByItem =
            new Dictionary<string, PriceSnapshot>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, List<RecipeUse>> UsedInRecipes =
            new Dictionary<string, List<RecipeUse>>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, List<RecipeDef>> CraftedFromRecipes =
            new Dictionary<string, List<RecipeDef>>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, RecipeDef> RecipesById =
            new Dictionary<string, RecipeDef>(StringComparer.OrdinalIgnoreCase);

        private static readonly HashSet<string> KnownItemIds =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private static readonly HashSet<string> BarterItemIds =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, object> ItemRecordsById =
            new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, object> SpaceObjectRecordsById =
            new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        private static readonly List<string> ItemDataSourceNames = new List<string>();

        private static void BuildIndexesSafe()
        {
            if (_indexesBuilt) return;
            _indexesBuilt = true;

            System.Diagnostics.Stopwatch totalTimer = System.Diagnostics.Stopwatch.StartNew();
            ClearIndexes();

            int failures = 0;

            if (_compatCore)
                RunCompatibilityIndexStage(
                    "Items",
                    "Core",
                    delegate { BuildItemCoverageIndex(); },
                    ref failures);

            if (_compatMagnum)
            {
                RunCompatibilityIndexStage(
                    "Magnum",
                    "Magnum",
                    delegate { BuildMagnumIndex(); },
                    ref failures);

            }

            if (_compatRecipes)
            {
                // Keep production/workbench independent. One changed collection must
                // not hide recipes from the other.
                RunIndexStage(
                    "ProduceReceipts",
                    delegate
                    {
                        BuildRecipeIndex(
                            "ProduceReceipts",
                            "Production");
                    },
                    ref failures);

                RunIndexStage(
                    "WorkbenchReceipts",
                    delegate
                    {
                        BuildRecipeIndex(
                            "WorkbenchReceipts",
                            "Workbench");
                    },
                    ref failures);
            }

            if (_compatTrade)
            {
                RunCompatibilityIndexStage(
                    "BarterReceipts",
                    "Trade",
                    delegate { BuildBarterIndex(); },
                    ref failures);

                RunCompatibilityIndexStage(
                    "SpaceObjects",
                    "Trade",
                    delegate { BuildSpaceObjectIndex(); },
                    ref failures);
            }

            StartFeatureWarmupsAfterCoreIndexes();

            totalTimer.Stop();
            _lastCoreIndexBuildMs = (float)totalTimer.Elapsed.TotalMilliseconds;
            _lastCoreIndexBuildFrame = Time.frameCount;
            Debug.Log("[ItemIntelligence] Core indexes ready in " + totalTimer.ElapsedMilliseconds +
                      " ms: KnownItems=" + KnownItemIds.Count +
                      ", BarterItems=" + BarterItemIds.Count +
                      ", MagnumItems=" + MagnumUses.Count +
                      ", UsedIn=" + UsedInRecipes.Count +
                      ", CraftedFrom=" + CraftedFromRecipes.Count +
                      ", BarterConsumers=" + BarterConsumers.Count +
                      ", BarterSources=" + BarterSources.Count +
                      ", " + DescribeFeatureWarmupStates() +
                      ", partialFailures=" + failures + ".");

            WriteCompatibilityReport();
        }

        private static void RunIndexStage(string name, Action action, ref int failures)
        {
            System.Diagnostics.Stopwatch timer = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                if (action != null) action();
            }
            catch (Exception ex)
            {
                failures++;
                Debug.LogWarning("[ItemIntelligence] Index stage " + name + " disabled: " + ex);
            }
            finally
            {
                timer.Stop();
                Debug.Log("[ItemIntelligence] Index stage " + name + ": " + timer.ElapsedMilliseconds + " ms.");
            }
        }

        private static void EnsureRuntimeIndexesReady()
        {
            try
            {
                // Healthy fast path: F2 can be opened dozens of times in one session.
                // Do not materialize Data.Items/Data.MagnumPerks merely to recount tables
                // whose already-built index generation is still current.
                if (_indexesBuilt && KnownItemIds.Count > 0)
                {
                    if (_compatTrade && SpaceObjectRecordsById.Count == 0) BuildSpaceObjectIndex();
                    return;
                }

                object items = GetStaticMember(typeof(Data), "Items");
                int liveItems = items == null ? 0 : EnumerateData(items).Count;
                object magnum = GetStaticMember(typeof(Data), "MagnumPerks");
                int liveMagnum = magnum == null ? 0 : EnumerateData(magnum).Count;

                if ((KnownItemIds.Count == 0 && liveItems > 0) ||
                    (MagnumUses.Count == 0 && liveMagnum > 0))
                {
                    Debug.Log("[ItemIntelligence] Strategy data became available after bootstrap; rebuilding indexes. Items=" +
                        liveItems.ToString(CultureInfo.InvariantCulture) + ", MagnumPerks=" +
                        liveMagnum.ToString(CultureInfo.InvariantCulture) + ".");
                    _indexesBuilt = false;
                    BuildIndexesSafe();
                }

                if (_compatTrade && SpaceObjectRecordsById.Count == 0) BuildSpaceObjectIndex();
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[ItemIntelligence] Runtime index readiness check failed: " + ex.Message);
            }
        }

        private static void BuildSpaceObjectIndex()
        {
            SpaceObjectRecordsById.Clear();
            object collection = GetStaticMember(typeof(Data), "SpaceObjects");
            if (collection == null) return;
            List<DataEntry> records = EnumerateData(collection);
            for (int i = 0; i < records.Count; i++)
            {
                object record = records[i].Value;
                if (record == null) continue;
                string id = FirstNonEmpty(GetStringMember(record, "Id"), records[i].Key);
                if (!string.IsNullOrEmpty(id)) SpaceObjectRecordsById[id] = record;
            }
        }

        private static void ClearIndexes()
        {
            // Core owns the shared reset order. Feature modules retain ownership of
            // their mutable state and expose one narrow reset entry point each.
            KnownItemIds.Clear();
            BarterItemIds.Clear();
            ItemRecordsById.Clear();
            ResetItemMetadataResolverState();
            ItemSmallIcons.Clear();
            ItemSmallIconMisses.Clear();
            VanillaObservedItemIcons.Clear();
            _iconMissingAuditCount = 0;

            ResetAmmoKnowledgeIndexState();

            ResetMagnumIndexState();
            UsedInRecipes.Clear();
            CraftedFromRecipes.Clear();
            RecipesById.Clear();

            ResetDisassemblyIndexState();
            ResetTradeIndexState();
            ResetAmmoWeaponIndexState();

            SpaceObjectRecordsById.Clear();

            ResetFactionIndexState();
            ResetLootIndexState();
            ResetBrowserIndexState();
        }

        private static void BuildItemCoverageIndex()
        {
            ItemDataSourceNames.Clear();
            Type baseItemRecordType = AccessTools.TypeByName("MGSC.BasePickupItemRecord");
            Type itemRecordType = AccessTools.TypeByName("MGSC.ItemRecord");

            int sourceCount = 0;
            object canonicalItems = GetStaticMember(typeof(Data), "Items");
            if (canonicalItems != null)
            {
                int canonicalMatched = IndexItemRecords(EnumerateData(canonicalItems), baseItemRecordType, itemRecordType);
                if (canonicalMatched > 0)
                {
                    sourceCount++;
                    ItemDataSourceNames.Add("Items=" + canonicalMatched.ToString(CultureInfo.InvariantCulture));
                    Debug.Log("[ItemIntelligence] Item data resolver: items=" + KnownItemIds.Count +
                        ", sources=" + sourceCount +
                        ", tables=" + string.Join(", ", ItemDataSourceNames.ToArray()) + ".");
                    return;
                }
            }

            List<MemberInfo> members = GetStaticDataMembers();
            for (int m = 0; m < members.Count; m++)
            {
                MemberInfo member = members[m];
                Type declaredType = GetMemberDeclaredType(member);
                string memberName = member.Name ?? string.Empty;
                bool typeLikely = TypeContainsRecordType(declaredType, baseItemRecordType) || TypeContainsRecordType(declaredType, itemRecordType);
                bool nameLikely = memberName.IndexOf("Item", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                  memberName.IndexOf("Pickup", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                  memberName.IndexOf("Inventory", StringComparison.OrdinalIgnoreCase) >= 0;
                if (!typeLikely && !nameLikely) continue;

                object collection = GetMemberValue(null, member);
                if (collection == null || collection is string) continue;
                List<DataEntry> records = EnumerateData(collection);
                int matched = IndexItemRecords(records, baseItemRecordType, itemRecordType);
                if (matched > 0)
                {
                    sourceCount++;
                    ItemDataSourceNames.Add(memberName + "=" + matched.ToString(CultureInfo.InvariantCulture));
                }
            }

            // Some game versions expose the item table through a neutrally-named static member.
            // If the first pass did not find it, inspect all static Data collections once.
            if (KnownItemIds.Count == 0)
            {
                for (int m = 0; m < members.Count; m++)
                {
                    MemberInfo member = members[m];
                    object collection = GetMemberValue(null, member);
                    if (collection == null || collection is string) continue;
                    if (!(collection is IEnumerable)) continue;
                    List<DataEntry> records = EnumerateData(collection);
                    int matched = IndexItemRecords(records, baseItemRecordType, itemRecordType);
                    if (matched > 0)
                    {
                        sourceCount++;
                        ItemDataSourceNames.Add((member.Name ?? string.Empty) + "=" + matched.ToString(CultureInfo.InvariantCulture));
                    }
                }
            }

            Debug.Log("[ItemIntelligence] Item data resolver: items=" + KnownItemIds.Count +
                ", sources=" + sourceCount +
                (ItemDataSourceNames.Count > 0 ? ", tables=" + string.Join(", ", ItemDataSourceNames.ToArray()) : string.Empty) + ".");
        }

        private static int IndexItemRecords(List<DataEntry> records, Type baseItemRecordType, Type itemRecordType)
        {
            if (records == null || records.Count == 0) return 0;
            int matched = 0;
            for (int i = 0; i < records.Count; i++)
            {
                object record = records[i].Value;
                if (record == null) continue;
                Type recordType = record.GetType();
                string typeName = recordType.Name ?? string.Empty;
                bool isItemRecord = baseItemRecordType != null && baseItemRecordType.IsAssignableFrom(recordType);
                if (!isItemRecord && itemRecordType != null) isItemRecord = itemRecordType.IsAssignableFrom(recordType);
                if (!isItemRecord && typeName.EndsWith("ItemRecord", StringComparison.OrdinalIgnoreCase)) isItemRecord = true;
                if (!isItemRecord && (typeName.IndexOf("WeaponRecord", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                      typeName.IndexOf("AmmoRecord", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                      typeName.IndexOf("ArmorRecord", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                      typeName.IndexOf("ConsumableRecord", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                      typeName.IndexOf("TrashRecord", StringComparison.OrdinalIgnoreCase) >= 0)) isItemRecord = true;
                if (!isItemRecord) continue;

                string id = FirstNonEmpty(GetStringMember(record, "Id"), records[i].Key);
                if (string.IsNullOrEmpty(id)) continue;
                bool wasNew = KnownItemIds.Add(id);
                ItemRecordsById[id] = record;
                if (wasNew) matched++;

                bool isBarter = false;
                bool? canBeTraded = GetBoolMember(record, "CanBeTraded");
                if (canBeTraded.HasValue && canBeTraded.Value) isBarter = true;
                object barterValue = GetMember(record, "BarterValue");
                if (!isBarter && barterValue != null)
                {
                    try { if (Convert.ToDouble(barterValue, CultureInfo.InvariantCulture) > 0.0) isBarter = true; }
                    catch { }
                }
                string itemClass = ConvertToStableString(GetMember(record, "ItemClass"));
                string category = ConvertToStableString(GetMember(record, "Category"));
                if (!isBarter && (typeName.IndexOf("Barter", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                  itemClass.IndexOf("Barter", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                  category.IndexOf("Barter", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                  itemClass.IndexOf("Trade", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                  category.IndexOf("Trade", StringComparison.OrdinalIgnoreCase) >= 0)) isBarter = true;
                if (isBarter) BarterItemIds.Add(id);
            }
            return matched;
        }

        private static void BuildMagnumIndex()
        {
            object collection = GetStaticMember(typeof(Data), "MagnumPerks");
            if (collection == null) throw new MissingMemberException("Data.MagnumPerks not found.");

            List<DataEntry> records = EnumerateData(collection);
            for (int i = 0; i < records.Count; i++)
            {
                object record = records[i].Value;
                if (record == null || !string.Equals(
                    record.GetType().FullName,
                    "MGSC.MagnumPerkRecord",
                    StringComparison.Ordinal))
                    continue;

                bool? enabled = GetBoolMember(record, "Enabled");
                if (enabled.HasValue && !enabled.Value) continue;
                string perkId = FirstNonEmpty(GetStringMember(record, "Id"), records[i].Key);
                if (string.IsNullOrEmpty(perkId)) continue;

                // Current-build schema proof: MagnumPerkRecord.UpgradePrice is List<string>.
                // Count only known item ids from that exact field; no generic cost-like scan.
                Dictionary<string, int> price = ExtractKnownItemQuantitiesDeep(
                    GetMember(record, "UpgradePrice"), 3);
                foreach (KeyValuePair<string, int> pair in price)
                    AddMagnumUseUnique(pair.Key, new MagnumUse(perkId, pair.Value, record));
            }
        }


        private static void AddMagnumUseUnique(string itemId, MagnumUse use)
        {
            if (string.IsNullOrEmpty(itemId) || use == null || string.IsNullOrEmpty(use.PerkId)) return;
            List<MagnumUse> list;
            if (!MagnumUses.TryGetValue(itemId, out list))
            {
                list = new List<MagnumUse>();
                MagnumUses[itemId] = list;
            }
            for (int i = 0; i < list.Count; i++)
            {
                MagnumUse existing = list[i];
                if (existing == null) continue;
                if (string.Equals(existing.PerkId, use.PerkId, StringComparison.OrdinalIgnoreCase) && existing.Quantity == use.Quantity)
                    return;
            }
            list.Add(use);
        }

        private static void BuildRecipeIndex(string dataMember, string kind)
        {
            object collection = GetStaticMember(typeof(Data), dataMember);
            if (collection == null) return;
            List<DataEntry> records = EnumerateData(collection);
            for (int i = 0; i < records.Count; i++)
            {
                object record = records[i].Value;
                if (record == null) continue;
                string outputId = GetItemId(GetMember(record, "OutputItem"));
                if (string.IsNullOrEmpty(outputId))
                    outputId = GetStringMember(record, "OutputItemId");
                if (string.IsNullOrEmpty(outputId)) continue;

                Dictionary<string, int> ingredients = ExtractItemQuantities(GetMember(record, "RequiredItems"));
                if (ingredients.Count == 0) continue;
                string recipeId = FirstNonEmpty(GetStringMember(record, "Id"), records[i].Key, outputId);
                List<string> requiredPerks = ExtractStringIds(GetMember(record, "RequiredPerks"));
                List<string> allowedWorkbenches = ExtractStringIds(GetMember(record, "AllowedWorkbenches"));
                RecipeDef def = new RecipeDef(recipeId, outputId, kind, ingredients, requiredPerks, allowedWorkbenches);
                RecipesById[recipeId] = def;
                AddToList(CraftedFromRecipes, outputId, def);
                foreach (KeyValuePair<string, int> ingredient in ingredients)
                    AddToList(UsedInRecipes, ingredient.Key, new RecipeUse(recipeId, outputId, ingredient.Value, kind));
            }
        }

        private static void BuildBarterIndex()
        {
            object collection = GetStaticMember(typeof(Data), "BarterReceipts");
            if (collection == null) return;
            List<DataEntry> records = EnumerateData(collection);
            for (int i = 0; i < records.Count; i++)
            {
                object record = records[i].Value;
                if (record == null) continue;
                string id = FirstNonEmpty(
                    GetStringMember(record, "Id"),
                    records[i].Key,
                    "barter_" + i.ToString(CultureInfo.InvariantCulture));
                Dictionary<string, int> inputs = ExtractItemQuantities(GetMember(record, "InputItems"));
                Dictionary<string, int> outputs = ExtractItemQuantities(GetMember(record, "OutputItems"));
                // BarterReceipt is used by the station economy/production system.
                // Only the exact InputItems/OutputItems contract is indexed; broad
                // name-based directional reflection is intentionally not used.
                // Keep only stable item ids in the long-lived core index. Localized
                // names belong to presentation and must follow an in-session language
                // switch instead of being frozen when this index was built.
                if (inputs.Count == 0 || outputs.Count == 0) continue;

                foreach (KeyValuePair<string, int> input in inputs)
                    AddToList(BarterConsumers, input.Key,
                        new TradeRelation(id, input.Value, outputs));
                foreach (KeyValuePair<string, int> output in outputs)
                    AddToList(BarterSources, output.Key,
                        new TradeRelation(id, output.Value, inputs));
            }
        }
    }
}
