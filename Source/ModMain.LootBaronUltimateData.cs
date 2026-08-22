using System;
using System.Collections.Generic;
using System.Globalization;
using MGSC;
using UnityEngine;

namespace ItemIntelligence
{
    public static partial class ModMain
    {
        // Exact audit basis for current Assembly-CSharp SHA256
        // EFF608C5118735359CD07FEAD8A8219E1CFB557E3A5A57517DB4428F04834B8B:
        // * Baron spawn supplies an empty factionId; GenerateEquipment replaces an empty
        //   faction tag with MobClass.DefaultItemFactionTag;
        // * additional items are selected through ItemDropSystem.Randomize using ItemClass,
        //   TechLevel, ItemCategoriesWhitelist/Faction matching and additive weights;
        // * a Baron can consume SkullRecord items only when CanBaronUse is true;
        // * death restore recreates the consumed Ultimate ID with AiPreset.DropUltimateItemChance.
        // QII never invokes vanilla RNG/mutation APIs; it mirrors only audited static inputs.
        private sealed class BaronItemMeta
        {
            public string ItemId;
            public string ItemClass;
            public int TechLevel;
            public bool TechResolved;
            public bool CanBaronUse;
            public HashSet<string> Categories;
        }

        private sealed class BaronPhaseProjection
        {
            public string Id;
            public string BramfaturaId;
            public string BaronCreatureId;
            public int MaxLevel;
            public bool BaronPhase;
            public List<Tuple<float, string>> LegacyDropPool;
        }

        private sealed class BaronChanceAccumulator
        {
            public float MinPercent = float.MaxValue;
            public float MaxPercent;
            public int SeenContexts;
            public void Update(double probability)
            {
                float percent = ClampPercent((float)(Math.Max(0.0, Math.Min(1.0, probability)) * 100.0));
                if (percent < MinPercent) MinPercent = percent;
                if (percent > MaxPercent) MaxPercent = percent;
                SeenContexts++;
            }
        }

        private static readonly Dictionary<string, BaronItemMeta> BaronItemMetaById =
            new Dictionary<string, BaronItemMeta>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, List<BaronItemMeta>> BaronItemsByClass =
            new Dictionary<string, List<BaronItemMeta>>(StringComparer.OrdinalIgnoreCase);
        private static readonly HashSet<string> BaronUnresolvedTechClasses =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private static bool _baronItemMetaReady;

        private static void ResetBaronUltimateDataState()
        {
            BaronItemMetaById.Clear();
            BaronItemsByClass.Clear();
            BaronUnresolvedTechClasses.Clear();
            _baronItemMetaReady = false;
        }

        private static void EnsureBaronItemMetadata()
        {
            if (_baronItemMetaReady) return;
            long started = System.Diagnostics.Stopwatch.GetTimestamp();
            int techResolved = 0, usable = 0, quasiPacts = 0, categorizedPacts = 0;

            foreach (string itemId in KnownItemIds)
            {
                if (string.IsNullOrEmpty(itemId) ||
                    itemId.IndexOf(ModifiedItemMarker, StringComparison.OrdinalIgnoreCase) >= 0)
                    continue; // ItemDropSystem.Randomize excludes *_custom records before selection.
                object record = ResolveCanonicalItemMetadataRecord(itemId);
                if (record == null) continue;

                string itemClass = ConvertToStableString(GetMember(record, "ItemClass"));
                if (string.IsNullOrEmpty(itemClass)) continue;

                int tech;
                bool hasTech = TryGetExactItemTechLevel(itemId, out tech);
                // Canonical metadata is already resolved through the wrapper graph.
                // Do not run another 48-node graph walk for every one of ~1700 items:
                // inherited SkullRecord.CanBaronUse is visible through GetMember here.
                bool canUse = GetBoolMember(record, "CanBaronUse") == true;
                HashSet<string> categories = ExtractStableStringSet(GetMember(record, "Categories"));
                if (string.Equals(itemClass, "QuasiPact", StringComparison.OrdinalIgnoreCase))
                {
                    quasiPacts++;
                    if (categories.Count > 0) categorizedPacts++;
                }

                BaronItemMeta meta = new BaronItemMeta
                {
                    ItemId = itemId,
                    ItemClass = itemClass,
                    TechLevel = hasTech ? Math.Max(0, tech) : 0,
                    TechResolved = hasTech,
                    CanBaronUse = canUse,
                    Categories = categories
                };
                BaronItemMetaById[itemId] = meta;
                List<BaronItemMeta> byClass;
                if (!BaronItemsByClass.TryGetValue(itemClass, out byClass))
                {
                    byClass = new List<BaronItemMeta>();
                    BaronItemsByClass[itemClass] = byClass;
                }
                byClass.Add(meta);
                if (hasTech) techResolved++; else BaronUnresolvedTechClasses.Add(itemClass);
                if (canUse) usable++;
            }

            _baronItemMetaReady = true;
            double ms = (System.Diagnostics.Stopwatch.GetTimestamp() - started) * 1000.0 /
                System.Diagnostics.Stopwatch.Frequency;
            Debug.Log("[ItemIntelligence][BaronUltimateLoot] itemMeta=" + BaronItemMetaById.Count.ToString(CultureInfo.InvariantCulture) +
                ", techResolved=" + techResolved.ToString(CultureInfo.InvariantCulture) +
                ", usableSkulls=" + usable.ToString(CultureInfo.InvariantCulture) +
                ", quasiPacts=" + quasiPacts.ToString(CultureInfo.InvariantCulture) +
                ", categorizedPacts=" + categorizedPacts.ToString(CultureInfo.InvariantCulture) +
                ", selectorMeta=ItemClass+Tech+Categories" +
                ", buildMs=" + ms.ToString("0.0", CultureInfo.InvariantCulture) + ".");
        }

        private static Dictionary<string, object> BuildBaronRecordLookup(object collection)
        {
            Dictionary<string, object> result = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            List<DataEntry> entries = EnumerateData(collection);
            for (int i = 0; i < entries.Count; i++)
            {
                object record = entries[i].Value;
                if (record == null) continue;
                string id = FirstNonEmpty(GetStringMember(record, "Id"), entries[i].Key);
                if (!string.IsNullOrEmpty(id)) result[id] = record;
            }
            return result;
        }

        private static Dictionary<string, double> BuildLegacyBaronPoolProbabilities(
            List<Tuple<float, string>> entries, out bool exact)
        {
            Dictionary<string, double> result = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
            exact = true;
            if (entries == null || entries.Count == 0) return result;
            double total = 0.0;
            string lastValid = string.Empty;
            for (int i = 0; i < entries.Count; i++)
            {
                Tuple<float, string> entry = entries[i];
                if (entry == null || string.IsNullOrEmpty(entry.Item2) ||
                    float.IsNaN(entry.Item1) || float.IsInfinity(entry.Item1) || entry.Item1 < 0f)
                {
                    exact = false;
                    continue;
                }
                lastValid = entry.Item2;
                double current;
                result.TryGetValue(entry.Item2, out current);
                result[entry.Item2] = current + entry.Item1;
                total += entry.Item1;
            }
            if (!exact)
            {
                result.Clear();
                return result;
            }
            if (total > 0.0)
            {
                List<string> keys = new List<string>(result.Keys);
                for (int i = 0; i < keys.Count; i++) result[keys[i]] /= total;
            }
            else if (!string.IsNullOrEmpty(lastValid))
            {
                result.Clear();
                result[lastValid] = 1.0; // DropManager's all-zero non-empty fallback.
            }
            return result;
        }

        private static double CombineIndependentProbabilities(double a, double b, double c)
        {
            a = Math.Max(0.0, Math.Min(1.0, a));
            b = Math.Max(0.0, Math.Min(1.0, b));
            c = Math.Max(0.0, Math.Min(1.0, c));
            return 1.0 - (1.0 - a) * (1.0 - b) * (1.0 - c);
        }

        private static int ResolveBaronPhaseMinLevel(List<BaronPhaseProjection> phases, BaronPhaseProjection target)
        {
            if (target == null || target.MaxLevel <= 0) return 0;
            int previousMax = -1;
            for (int i = 0; i < phases.Count; i++)
            {
                BaronPhaseProjection candidate = phases[i];
                if (candidate == null || ReferenceEquals(candidate, target)) continue;
                if (!string.Equals(candidate.BramfaturaId, target.BramfaturaId, StringComparison.OrdinalIgnoreCase)) continue;
                if (candidate.MaxLevel >= 0 && candidate.MaxLevel < target.MaxLevel && candidate.MaxLevel > previousMax)
                    previousMax = candidate.MaxLevel;
            }
            return previousMax >= 0 ? previousMax + 1 : 0;
        }

        private static void BuildLootBaronSpecialIndex()
        {
            LootBaronSpecialSourcesByItem.Clear();
            EnsureBaronItemMetadata();
            List<QmorphosRecord> rawRecords = CollectQmorphosRecordsForBaronIndex();
            List<BaronPhaseProjection> phases = new List<BaronPhaseProjection>();
            for (int i = 0; i < rawRecords.Count; i++)
            {
                QmorphosRecord record = rawRecords[i];
                if (record == null) continue;
                phases.Add(new BaronPhaseProjection
                {
                    Id = GetStringMember(record, "Id"),
                    BramfaturaId = record.BramfaturaId ?? string.Empty,
                    BaronCreatureId = record.BaronCreatureId ?? string.Empty,
                    MaxLevel = record.MaxLevel,
                    BaronPhase = record.BaronPhase,
                    LegacyDropPool = record.BaronSkullsDrop
                });
            }

            Dictionary<string, object> mobs = BuildBaronRecordLookup(GetStaticMember(typeof(Data), "MobClasses"));
            Dictionary<string, object> aiPresets = BuildBaronRecordLookup(GetStaticMember(typeof(Data), "AiPresets"));
            int maxTech = 10;
            try { if (Data.Global != null) maxTech = Math.Max(1, Data.Global.MaxTechLevel); } catch { }

            int baronFlags = 0, mobResolved = 0, aiResolvedCount = 0, links = 0, unresolvedPools = 0, legacyPools = 0;
            for (int i = 0; i < phases.Count; i++)
            {
                BaronPhaseProjection phase = phases[i];
                if (phase == null || !phase.BaronPhase) continue;
                baronFlags++;
                if (string.IsNullOrEmpty(phase.BaronCreatureId)) continue;

                object mob;
                if (!mobs.TryGetValue(phase.BaronCreatureId, out mob) || mob == null)
                {
                    Debug.Log("[ItemIntelligence][BaronUltimateLoot] phase=" + phase.Id + ", baron=" + phase.BaronCreatureId + ", mobResolved=false.");
                    continue;
                }
                mobResolved++;

                string aiPresetId = GetStringMember(mob, "AiPresetId");
                object ai;
                double deathRestore = 0.0;
                bool deathResolved = !string.IsNullOrEmpty(aiPresetId) && aiPresets.TryGetValue(aiPresetId, out ai) && ai != null &&
                    TryToDoubleSafe(GetMember(ai, "DropUltimateItemChance"), out deathRestore);
                if (deathResolved)
                {
                    deathRestore = Math.Max(0.0, Math.Min(1.0, deathRestore));
                    aiResolvedCount++;
                }

                int bonus = 0;
                TryToInt(GetMember(mob, "EquipmentTechLevelBonus"), out bonus);
                bool classWeightsExact;
                Dictionary<string, double> classWeights = ExtractItemDropWeightMap(
                    GetMember(mob, "AdditItemClasses"), out classWeightsExact);
                object rawWhitelist = GetMember(mob, "ItemCategoriesWhitelist");
                bool whitelistExact;
                Dictionary<string, double> whitelist = ExtractItemDropWeightMap(
                    rawWhitelist, out whitelistExact);
                bool whitelistExists = rawWhitelist != null;
                // QmorphosController.SpawnBaron passes factionId="" to
                // SpawnMonsterFromMobClass. GenerateEquipment therefore receives the
                // MobClass DefaultItemFactionTag via its audited empty-tag fallback.
                string defaultFactionTag = GetStringMember(mob, "DefaultItemFactionTag") ?? string.Empty;
                object additCountRange = GetMember(mob, "AdditItemCount");
                int minRolls, maxRolls;
                ReadIntRange(additCountRange, out minRolls, out maxRolls);
                int exactMinRolls, exactMaxRolls;
                bool rollRangeResolved = additCountRange != null &&
                    TryToInt(GetMember(additCountRange, "Min"), out exactMinRolls) &&
                    TryToInt(GetMember(additCountRange, "Max"), out exactMaxRolls);
                List<string> granted = ExtractStringIds(GetMember(mob, "GrantedItems"));
                HashSet<string> grantedSet = new HashSet<string>(granted, StringComparer.OrdinalIgnoreCase);
                bool grantedPact = false;
                foreach (string grantedId in grantedSet)
                {
                    BaronItemMeta grantedMeta;
                    if (BaronItemMetaById.TryGetValue(grantedId, out grantedMeta) &&
                        grantedMeta != null && string.Equals(grantedMeta.ItemClass, "QuasiPact", StringComparison.OrdinalIgnoreCase))
                    {
                        grantedPact = true;
                        break;
                    }
                }

                bool legacyExact;
                Dictionary<string, double> legacy = BuildLegacyBaronPoolProbabilities(phase.LegacyDropPool, out legacyExact);
                if (legacy.Count > 0) legacyPools++;

                bool additionalExact = classWeightsExact && whitelistExact;
                foreach (string itemClass in classWeights.Keys)
                    if (BaronUnresolvedTechClasses.Contains(itemClass)) { additionalExact = false; break; }
                bool phaseExact = additionalExact && legacyExact &&
                    (classWeights.Count == 0 || rollRangeResolved);
                Dictionary<string, BaronChanceAccumulator> itemAcc =
                    new Dictionary<string, BaronChanceAccumulator>(StringComparer.OrdinalIgnoreCase);
                BaronChanceAccumulator anyPactAcc = new BaronChanceAccumulator();
                int uniformPoolCount = -1;
                bool uniformPool = minRolls == 1 && maxRolls == 1 && classWeights.Count == 1 && legacy.Count == 0 && !grantedPact;
                int contextCount = 0;

                for (int rawTech = 1; rawTech <= maxTech; rawTech++)
                {
                    contextCount++;
                    int effectiveTech = Math.Max(1, Math.Min(maxTech, rawTech + bonus));
                    Dictionary<string, double> addPerItem = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
                    double totalWeight = 0.0, pactWeight = 0.0;
                    int eligibleCount = 0;

                    bool contextUniform = true;
                    double firstEligibleWeight = double.NaN;
                    if (phaseExact && maxRolls > 0 && classWeights.Count > 0)
                    {
                        foreach (KeyValuePair<string, double> classPair in classWeights)
                        {
                            List<BaronItemMeta> classItems;
                            if (!BaronItemsByClass.TryGetValue(classPair.Key, out classItems) || classItems == null) continue;
                            for (int m = 0; m < classItems.Count; m++)
                            {
                                BaronItemMeta meta = classItems[m];
                                if (meta == null || !meta.TechResolved || meta.TechLevel > effectiveTech) continue;

                                bool categoryEligible;
                                double categoryWeight = GetItemDropCategoryWeight(
                                    meta.Categories, whitelist, whitelistExists,
                                    defaultFactionTag, out categoryEligible);
                                if (!categoryEligible) continue;

                                // Exact ItemDropSystem.Randomize weight: the ItemClass
                                // delegate weight plus the maximum matched category/faction
                                // whitelist weight. No PactRecord.BramfaturaId filter exists
                                // in the audited vanilla selector.
                                double finalWeight = classPair.Value + categoryWeight;
                                addPerItem[meta.ItemId] = finalWeight;
                                eligibleCount++;
                                if (double.IsNaN(firstEligibleWeight)) firstEligibleWeight = finalWeight;
                                else if (Math.Abs(firstEligibleWeight - finalWeight) > 0.0000001) contextUniform = false;
                                if (string.Equals(meta.ItemClass, "QuasiPact", StringComparison.OrdinalIgnoreCase))
                                    pactWeight += finalWeight;
                            }
                        }

                        if (addPerItem.Count > 0 && !TryResolveStrictlyPositiveItemDropTotal(
                            addPerItem, "baron." + phase.Id, out totalWeight))
                        {
                            phaseExact = false;
                            totalWeight = 0.0;
                            pactWeight = 0.0;
                            uniformPool = false;
                        }
                    }

                    if (uniformPool)
                    {
                        if (eligibleCount <= 0 || !contextUniform) uniformPool = false;
                        else if (uniformPoolCount < 0) uniformPoolCount = eligibleCount;
                        else if (uniformPoolCount != eligibleCount) uniformPool = false;
                    }

                    HashSet<string> ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    foreach (string id in addPerItem.Keys) ids.Add(id);
                    foreach (string id in grantedSet) if (KnownItemIds.Contains(id)) ids.Add(id);
                    foreach (string id in legacy.Keys) if (KnownItemIds.Contains(id)) ids.Add(id);

                    foreach (string id in ids)
                    {
                        double addChance = 0.0;
                        double weight;
                        if (totalWeight > 0.0 && addPerItem.TryGetValue(id, out weight))
                            addChance = ProbabilityAtLeastOnceUniformCount(weight / totalWeight, minRolls, maxRolls);
                        double grantedChance = grantedSet.Contains(id) ? 1.0 : 0.0;
                        double legacyChance;
                        if (!legacy.TryGetValue(id, out legacyChance)) legacyChance = 0.0;
                        double inventoryChance = CombineIndependentProbabilities(addChance, grantedChance, legacyChance);
                        if (inventoryChance <= 0.0) continue;

                        double finalMin = deathResolved ? inventoryChance * deathRestore : 0.0;
                        double finalMax = inventoryChance;
                        // With a 100% death restore, consumption cannot remove the pact from
                        // the corpse. Otherwise consumption timing is behavior-dependent, so
                        // [inventory*restore, inventory] is the exact safe bound.
                        BaronChanceAccumulator acc;
                        if (!itemAcc.TryGetValue(id, out acc))
                        {
                            acc = new BaronChanceAccumulator();
                            itemAcc[id] = acc;
                        }
                        acc.Update(finalMax);
                        // Store the lower bound separately by folding it into MinPercent when
                        // restore < 100%; this keeps the public source model a simple range.
                        if (deathResolved && deathRestore < 0.999999)
                        {
                            float lower = ClampPercent((float)(finalMin * 100.0));
                            if (lower < acc.MinPercent) acc.MinPercent = lower;
                        }
                    }

                    // "Any pact" means any generated QuasiPact. It does not depend on
                    // whether the Baron consumed that item as an Ultimate.
                    double addAny = totalWeight > 0.0
                        ? ProbabilityAtLeastOnceUniformCount(pactWeight / totalWeight, minRolls, maxRolls)
                        : 0.0;
                    double grantedAny = 0.0;
                    foreach (string id in grantedSet)
                    {
                        BaronItemMeta meta;
                        if (BaronItemMetaById.TryGetValue(id, out meta) && meta != null && string.Equals(meta.ItemClass, "QuasiPact", StringComparison.OrdinalIgnoreCase))
                        { grantedAny = 1.0; break; }
                    }
                    double legacyAny = 0.0;
                    foreach (KeyValuePair<string, double> pair in legacy)
                    {
                        BaronItemMeta meta;
                        if (BaronItemMetaById.TryGetValue(pair.Key, out meta) && meta != null && string.Equals(meta.ItemClass, "QuasiPact", StringComparison.OrdinalIgnoreCase))
                            legacyAny += pair.Value;
                    }
                    legacyAny = Math.Max(0.0, Math.Min(1.0, legacyAny));
                    double inventoryAny = CombineIndependentProbabilities(addAny, grantedAny, legacyAny);
                    double anyUpper = inventoryAny;
                    double anyLower = deathResolved ? inventoryAny * deathRestore : 0.0;
                    anyPactAcc.Update(anyUpper);
                    if (deathResolved && deathRestore < 0.999999)
                    {
                        float lower = ClampPercent((float)(anyLower * 100.0));
                        if (lower < anyPactAcc.MinPercent) anyPactAcc.MinPercent = lower;
                    }
                }

                if (!phaseExact) unresolvedPools++;

                int phaseMin = ResolveBaronPhaseMinLevel(phases, phase);
                int phaseLinks = 0;
                foreach (KeyValuePair<string, BaronChanceAccumulator> pair in itemAcc)
                {
                    BaronItemMeta meta;
                    if (!BaronItemMetaById.TryGetValue(pair.Key, out meta) || meta == null ||
                        !string.Equals(meta.ItemClass, "QuasiPact", StringComparison.OrdinalIgnoreCase)) continue;
                    BaronChanceAccumulator acc = pair.Value;
                    if (acc == null || acc.MaxPercent <= 0f) continue;
                    float minPercent = acc.SeenContexts < contextCount ? 0f : acc.MinPercent;
                    float maxPercent = acc.MaxPercent;
                    float anyMin = anyPactAcc.SeenContexts < contextCount ? 0f : anyPactAcc.MinPercent;
                    float anyMax = anyPactAcc.MaxPercent;
                    AddLootBaronSpecialSource(pair.Key, new LootBaronSpecialSource(
                        phase.Id, phase.BramfaturaId, phase.BaronCreatureId, aiPresetId,
                        phaseMin, phase.MaxLevel,
                        minPercent, maxPercent, anyMin, anyMax,
                        deathResolved ? (float)(deathRestore * 100.0) : 0f,
                        phaseExact && deathResolved,
                        deathResolved,
                        uniformPool && uniformPoolCount > 0 ? uniformPoolCount : 0,
                        minRolls, maxRolls, legacy.Count > 0));
                    phaseLinks++; links++;
                }

                string classes = FormatBaronClassWeights(classWeights);
                Debug.Log("[ItemIntelligence][BaronUltimateLoot] phase=" + phase.Id +
                    ", bramfatura=" + phase.BramfaturaId +
                    ", baron=" + phase.BaronCreatureId +
                    ", qmorph=" + phaseMin.ToString(CultureInfo.InvariantCulture) +
                    ", aiPreset=" + aiPresetId +
                    ", deathRestore=" + (deathResolved ? (deathRestore * 100.0).ToString("0.###", CultureInfo.InvariantCulture) + "%" : "?") +
                    ", granted=" + grantedSet.Count.ToString(CultureInfo.InvariantCulture) +
                    ", additClasses=" + classes +
                    ", itemDropWhitelist=" + FormatBaronClassWeights(whitelist) +
                    ", factionTag=" + (string.IsNullOrEmpty(defaultFactionTag) ? "<empty>" : defaultFactionTag) +
                    ", additCount=" + minRolls.ToString(CultureInfo.InvariantCulture) + "-" + maxRolls.ToString(CultureInfo.InvariantCulture) +
                    ", legacyPool=" + legacy.Count.ToString(CultureInfo.InvariantCulture) +
                    ", uniformPactPool=" + (uniformPool && uniformPoolCount > 0 ? uniformPoolCount.ToString(CultureInfo.InvariantCulture) : "-") +
                    ", itemLinks=" + phaseLinks.ToString(CultureInfo.InvariantCulture) +
                    ", exact=" + (phaseExact && deathResolved).ToString() + ".");
            }

            _lootBaronSpecialIndexBuilt = true;
            Debug.Log("[ItemIntelligence][BaronUltimateLoot] records=" + rawRecords.Count.ToString(CultureInfo.InvariantCulture) +
                ", baronFlags=" + baronFlags.ToString(CultureInfo.InvariantCulture) +
                ", baronMobResolved=" + mobResolved.ToString(CultureInfo.InvariantCulture) +
                ", aiResolved=" + aiResolvedCount.ToString(CultureInfo.InvariantCulture) +
                ", legacyPools=" + legacyPools.ToString(CultureInfo.InvariantCulture) +
                ", itemLinks=" + links.ToString(CultureInfo.InvariantCulture) +
                ", unresolvedPools=" + unresolvedPools.ToString(CultureInfo.InvariantCulture) +
                ", source=BaronMobInventory+ItemDropSystemRandomizeExact+UltimateDeathRestore, rngInvoked=false.");
        }

    }
}
