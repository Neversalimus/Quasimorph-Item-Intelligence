using System;
using System.Collections;
using System.Collections.Generic;
using MGSC;

namespace ItemIntelligence
{
    public static partial class ModMain
    {
        private sealed class EnemyBodyCandidate
        {
            public string Id;
            public string Class;
            public int Tech;
            public HashSet<string> Categories;
            public EnemyImplantRule Implant;
            public EnemyAugmentationRule Augmentation;
        }

        private sealed class EnemyBodyChanceSummary
        {
            public double Min = 1.0;
            public double Max;
            public bool Unknown;
            public int Seen;
            public int Tech = int.MaxValue;
        }

        private static readonly List<EnemyBodyCandidate> EnemyBodyCandidates = new List<EnemyBodyCandidate>();
        private static readonly Dictionary<string, EnemyImplantRule> EnemyImplantRules =
            new Dictionary<string, EnemyImplantRule>(StringComparer.Ordinal);
        private static readonly Dictionary<string, EnemyAugmentationRule> EnemyAugmentationRules =
            new Dictionary<string, EnemyAugmentationRule>(StringComparer.Ordinal);
        private static IEnumerator _enemyBodySourceWork;
        private static bool _enemyBodyDataReady;
        private static int _enemyBodyExcludedImplantCandidates;

        private static void ResetEnemyBodySources()
        {
            _enemyBodySourceWork = null;
            _enemyBodyDataReady = false;
            EnemyBodyCandidates.Clear();
            EnemyImplantRules.Clear();
            EnemyAugmentationRules.Clear();
            _enemyBodyExcludedImplantCandidates = 0;
        }

        private static bool IsAuditedEnemyBodyAssembly()
        {
            if (!_compatStaticChecked) RunCompatibilityShieldStatic();
            return string.Equals(_compatAssemblySha256,
                "BE78036434737521BB43DABBD934B579A08DC3E8370B834AEE36E40932F56FE0",
                StringComparison.OrdinalIgnoreCase);
        }

        private static EnemyBodySlot ReadEnemyBodySlot(string id, bool augmentation)
        {
            WoundSlotRecord record = Data.WoundSlots.GetRecord(id, true);
            if (record == null || string.IsNullOrEmpty(record.SlotType) || record.NatureType == null) return null;
            int min = augmentation ? record.ImplantSocketsMin : record.ImplantSocketsDefault;
            int max = augmentation ? record.ImplantSocketsMax : record.ImplantSocketsDefault;
            if (min < 0 || max < min) return null;
            return new EnemyBodySlot { Id = id, Type = record.SlotType, Nature = record.NatureType,
                MinSockets = min, MaxSockets = max };
        }

        private static EnemyImplantRule ReadEnemyImplantRule(string id)
        {
            EnemyImplantRule rule;
            if (EnemyImplantRules.TryGetValue(id, out rule)) return rule;
            ImplantRecord record = Data.Items.GetSimpleRecord<ImplantRecord>(id, true);
            if (record != null)
                rule = new EnemyImplantRule { Id = id, Type = record.SlotType ?? string.Empty,
                    Natures = record.NatureTypes == null ? null :
                        new HashSet<string>(record.NatureTypes, StringComparer.Ordinal) };
            EnemyImplantRules[id] = rule;
            return rule;
        }

        private static EnemyAugmentationRule ReadEnemyAugmentationRule(string id)
        {
            EnemyAugmentationRule rule;
            if (EnemyAugmentationRules.TryGetValue(id, out rule)) return rule;
            AugmentationRecord record = Data.Items.GetSimpleRecord<AugmentationRecord>(id, true);
            if (record != null)
            {
                rule = new EnemyAugmentationRule { Id = id, Slots = new List<EnemyBodySlot>() };
                if (record.WoundSlotIds == null) rule.Slots = null;
                else foreach (string slotId in record.WoundSlotIds)
                {
                    EnemyBodySlot slot = ReadEnemyBodySlot(slotId, true);
                    if (slot == null) { rule.Slots = null; break; }
                    rule.Slots.Add(slot);
                }
            }
            EnemyAugmentationRules[id] = rule;
            return rule;
        }

        private static IEnumerator PrepareEnemyBodyCandidates()
        {
            if (_enemyBodyDataReady) yield break;
            int processed = 0;
            // Use the actual CompositeItemRecord/PrimaryRecord collection, including
            // non-display items in the denominator, exactly as Randomize does.
            foreach (BasePickupItemRecord record in Data.Items.Records)
            {
                if (++processed % 32 == 0) yield return null;
                CompositeItemRecord composite = record as CompositeItemRecord;
                if (composite == null || record.Id.Contains("_custom")) continue;
                ItemRecord primary = composite.PrimaryRecord as ItemRecord;
                if (primary == null) continue;
                ImplantRecord implant = composite.GetRecord<ImplantRecord>();
                AugmentationRecord aug = composite.GetRecord<AugmentationRecord>();
                if (implant == null && aug == null) continue;
                EnemyBodyCandidates.Add(new EnemyBodyCandidate {
                    Id = primary.Id, Tech = primary.TechLevel,
                    Categories = primary.Categories == null ? null :
                        new HashSet<string>(primary.Categories, StringComparer.Ordinal),
                    Class = implant != null ? implant.AugmentationClass.ToString() : aug.AugmentationClass.ToString(),
                    Implant = implant == null ? null : ReadEnemyImplantRule(primary.Id),
                    Augmentation = aug == null ? null : ReadEnemyAugmentationRule(primary.Id)
                });
            }
            _enemyBodyDataReady = true;
        }

        private static List<EnemyBodyModel> ReadEnemyBodyModels(MobClassRecord mob)
        {
            List<EnemyBodyModel> result = new List<EnemyBodyModel>();
            bool audited = IsAuditedEnemyBodyAssembly();
            if (mob.BodyTypes != null) foreach (string id in mob.BodyTypes)
            {
                EnemyBodyModel body = new EnemyBodyModel();
                body.Unknown = !audited;
                BodyTypeRecord record = Data.BodyTypes.GetRecord(id, true);
                if (record == null || record.WoundSlots == null) body.Unknown = true;
                else foreach (string slotId in record.WoundSlots)
                {
                    EnemyBodySlot slot = ReadEnemyBodySlot(slotId, false);
                    if (slot == null) { body.Unknown = true; continue; }
                    if (body.Slots.ContainsKey(slot.Type))
                    {
                        // Vanilla selects the first matching dictionary entry. Do not
                        // guess enumeration order for ambiguous modded body definitions.
                        if (body.Slots[slot.Type][0].Id != slot.Id) body.Unknown = true;
                        continue;
                    }
                    body.Slots[slot.Type] = new List<EnemyBodySlot> { slot };
                }
                if (mob.GrantedAugmentations != null) foreach (string aug in mob.GrantedAugmentations)
                    ApplyGuaranteedEnemyAugmentation(body, ReadEnemyAugmentationRule(aug));
                result.Add(body);
            }
            if (result.Count == 0) result.Add(new EnemyBodyModel { Unknown = true });
            return result;
        }

        private static Dictionary<string, double> ReadEnemyBodyWeights(object value)
        {
            // Preserve the game's case-sensitive category matching.
            Dictionary<string, double> result = new Dictionary<string, double>(StringComparer.Ordinal);
            IDictionary map = value as IDictionary;
            if (map != null) foreach (DictionaryEntry entry in map)
            {
                double weight;
                string key = ConvertToStableString(entry.Key);
                result[key] = TryToDoubleSafe(entry.Value, out weight) ? weight : double.NaN;
            }
            return result;
        }

        private static double GetEnemyBodyRollGate(Dictionary<string, double> classes)
        {
            if (classes.Count == 0) return 0.0;
            double active = 0.0, total = 0.0;
            foreach (KeyValuePair<string, double> pair in classes)
            {
                if (pair.Value <= 0.0 || double.IsNaN(pair.Value) || double.IsInfinity(pair.Value))
                    return double.NaN;
                total += pair.Value;
                if (pair.Key != "None") active += pair.Value;
            }
            return active / total;
        }

        private static Dictionary<string, double> GetEnemyBodyCandidateWeights(
            Dictionary<string, double> classes, Dictionary<string, double> whitelist,
            bool whitelistExists, EnemyLootContext context, bool implants)
        {
            Dictionary<string, double> result = new Dictionary<string, double>(StringComparer.Ordinal);
            foreach (EnemyBodyCandidate candidate in EnemyBodyCandidates)
            {
                if (implants ? candidate.Implant == null : candidate.Augmentation == null) continue;
                double classWeight;
                if (candidate.Tech > context.EffectiveTech || !classes.TryGetValue(candidate.Class, out classWeight))
                    continue;
                bool eligible;
                double category = GetItemDropCategoryWeight(candidate.Categories, whitelist,
                    whitelistExists, context.FactionId, out eligible);
                if (eligible)
                {
                    double previous;
                    result.TryGetValue(candidate.Id, out previous);
                    result[candidate.Id] = previous + ((float)classWeight + (float)category);
                }
            }
            return result;
        }

        private static void AddEnemyBodyContextChance(Dictionary<string, EnemyBodyChanceSummary> summaries,
            string id, double probability, int rawTech)
        {
            if (!double.IsNaN(probability) && probability <= 0.0) return;
            EnemyBodyChanceSummary summary;
            if (!summaries.TryGetValue(id, out summary))
            { summary = new EnemyBodyChanceSummary(); summaries.Add(id, summary); }
            summary.Seen++;
            summary.Tech = Math.Min(summary.Tech, rawTech);
            if (double.IsNaN(probability)) summary.Unknown = true;
            else { summary.Min = Math.Min(summary.Min, probability); summary.Max = Math.Max(summary.Max, probability); }
        }

        private static void FinishEnemyBodySources(string mobId, string kind,
            Dictionary<string, EnemyBodyChanceSummary> summaries, int contexts, int minCount, int maxCount)
        {
            foreach (KeyValuePair<string, EnemyBodyChanceSummary> pair in summaries)
            {
                if (!KnownItemIds.Contains(pair.Key)) continue;
                EnemyBodyChanceSummary value = pair.Value;
                float min = value.Unknown ? float.NaN : (float)(100.0 * (value.Seen < contexts ? 0.0 : value.Min));
                float max = value.Unknown ? float.NaN : (float)(100.0 * value.Max);
                AddLootEnemySource(pair.Key, new LootEnemySource(mobId, min, max, kind,
                    "body-aware", minCount, maxCount, value.Tech == int.MaxValue ? 0 : value.Tech));
            }
        }

        private static bool TickEnemyBodySources(string mobId, object rawMob, List<EnemyLootContext> contexts)
        {
            if (_enemyBodySourceWork == null)
                _enemyBodySourceWork = BuildEnemyBodySources(mobId, rawMob as MobClassRecord, contexts);
            if (_enemyBodySourceWork.MoveNext()) return false;
            _enemyBodySourceWork = null;
            return true;
        }

        private static IEnumerator BuildEnemyBodySources(string mobId, MobClassRecord mob,
            List<EnemyLootContext> contexts)
        {
            if (mob == null || contexts == null || contexts.Count == 0) yield break;
            bool hasImplants = (mob.GrantedImplants != null && mob.GrantedImplants.Count > 0) ||
                (mob.ImplantCount.Max > 0 && mob.ImplantClasses != null && mob.ImplantClasses.Count > 0);
            bool hasAugmentations = (mob.GrantedAugmentations != null && mob.GrantedAugmentations.Count > 0) ||
                (mob.AugCount.Max > 0 && mob.AugmentationClasses != null && mob.AugmentationClasses.Count > 0);
            if (!hasImplants && !hasAugmentations) yield break;
            IEnumerator prepare = PrepareEnemyBodyCandidates();
            while (prepare.MoveNext()) yield return null;
            List<EnemyBodyModel> baseBodies = ReadEnemyBodyModels(mob);
            List<EnemyImplantRule> granted = new List<EnemyImplantRule>();
            bool unknownGrant = false;
            if (mob.GrantedImplants != null) foreach (string id in mob.GrantedImplants)
            {
                EnemyImplantRule rule = ReadEnemyImplantRule(id);
                if (rule == null) continue;
                granted.Add(rule);
                if (rule.Natures == null) unknownGrant = true;
            }
            Dictionary<string, double> augClasses = ReadEnemyBodyWeights(mob.AugmentationClasses);
            Dictionary<string, double> implantClasses = ReadEnemyBodyWeights(mob.ImplantClasses);
            Dictionary<string, double> whitelist = ReadEnemyBodyWeights(mob.ItemCategoriesWhitelist);
            bool hasWhitelist = mob.ItemCategoriesWhitelist != null;
            double augGate = GetEnemyBodyRollGate(augClasses);
            double implantGate = GetEnemyBodyRollGate(implantClasses);
            Dictionary<string, EnemyBodyChanceSummary> randomImplants = new Dictionary<string, EnemyBodyChanceSummary>();
            Dictionary<string, EnemyBodyChanceSummary> grantedImplants = new Dictionary<string, EnemyBodyChanceSummary>();
            Dictionary<string, EnemyBodyChanceSummary> randomAugs = new Dictionary<string, EnemyBodyChanceSummary>();
            Dictionary<string, EnemyBodyChanceSummary> grantedAugs = new Dictionary<string, EnemyBodyChanceSummary>();
            foreach (EnemyLootContext context in contexts)
            {
                Dictionary<string, double> implantWeights = GetEnemyBodyCandidateWeights(implantClasses,
                    whitelist, hasWhitelist, context, true);
                Dictionary<string, double> augWeights = GetEnemyBodyCandidateWeights(augClasses,
                    whitelist, hasWhitelist, context, false);
                Dictionary<string, double> randomSum = new Dictionary<string, double>(StringComparer.Ordinal);
                Dictionary<string, double> grantedSum = new Dictionary<string, double>(StringComparer.Ordinal);
                Dictionary<string, double> augGrantedSum = new Dictionary<string, double>(StringComparer.Ordinal);
                HashSet<string> possibleAugs = new HashSet<string>(StringComparer.Ordinal);
                foreach (EnemyBodyModel basis in baseBodies)
                {
                    EnemyBodyModel body = basis.Copy();
                    if (unknownGrant) body.Unknown = true;
                    if (mob.GrantedAugmentations != null) foreach (string id in new HashSet<string>(mob.GrantedAugmentations))
                    {
                        List<string> items = ResolveLootExternalItemIds(id);
                        double chance = body.Unknown ? double.NaN : (body.GrantedAugs.Contains(id) ? 1.0 : 0.0);
                        foreach (string item in items) AddEnemyBodyProbability(augGrantedSum, item, chance / baseBodies.Count);
                    }
                    if (mob.AugCount.Max > 0 && augGate != 0.0) foreach (string id in augWeights.Keys)
                    {
                        EnemyAugmentationRule aug = EnemyAugmentationRules[id];
                        if (!body.Unknown && HasGuaranteedAugmentationConflict(body, aug)) continue;
                        if (aug != null && (aug.Slots == null || aug.Slots.Count > 0)) possibleAugs.Add(id);
                        IncludePossibleEnemyAugmentation(body, aug);
                    }
                    HashSet<string> seenGrants = new HashSet<string>(StringComparer.Ordinal);
                    foreach (EnemyImplantRule rule in granted)
                    {
                        // Duplicate grants are attempts in sequence, not independent 100% sources.
                        if (!seenGrants.Add(rule.Id)) continue;
                        double chance = ProjectGrantedEnemyImplant(body, rule, granted);
                        AddEnemyBodyProbability(grantedSum, rule.Id, chance / baseBodies.Count);
                    }
                    Dictionary<string, double> projected = ProjectRandomEnemyImplants(body, EnemyImplantRules,
                        implantWeights, granted, implantGate, mob.ImplantCount.Min, mob.ImplantCount.Max);
                    _enemyBodyExcludedImplantCandidates += Math.Max(0, implantWeights.Count - projected.Count);
                    foreach (KeyValuePair<string, double> pair in projected)
                        AddEnemyBodyProbability(randomSum, pair.Key, pair.Value / baseBodies.Count);
                    yield return null; // One body/context per slice; never grow one long enemy-frame operation.
                }
                foreach (KeyValuePair<string, double> pair in randomSum)
                    AddEnemyBodyContextChance(randomImplants, pair.Key, pair.Value, context.RawTech);
                foreach (KeyValuePair<string, double> pair in grantedSum)
                    AddEnemyBodyContextChance(grantedImplants, pair.Key, pair.Value, context.RawTech);
                foreach (KeyValuePair<string, double> pair in augGrantedSum)
                    AddEnemyBodyContextChance(grantedAugs, pair.Key, pair.Value, context.RawTech);
                foreach (string id in possibleAugs)
                    AddEnemyBodyContextChance(randomAugs, id, double.NaN, context.RawTech);
            }
            FinishEnemyBodySources(mobId, "GrantedImplant", grantedImplants, contexts.Count, 1, 1);
            FinishEnemyBodySources(mobId, "RandomImplant", randomImplants, contexts.Count,
                Math.Max(0, mob.ImplantCount.Min), Math.Max(0, mob.ImplantCount.Max));
            FinishEnemyBodySources(mobId, "GrantedAugmentation", grantedAugs, contexts.Count, 1, 1);
            FinishEnemyBodySources(mobId, "RandomAugmentation", randomAugs, contexts.Count,
                Math.Max(0, mob.AugCount.Min), Math.Max(0, mob.AugCount.Max));
        }

        private static void AddEnemyBodyProbability(Dictionary<string, double> map, string id, double chance)
        {
            double previous;
            map.TryGetValue(id, out previous);
            map[id] = previous + chance; // NaN deliberately propagates through unknown body alternatives.
        }

        private static void LogEnemyBodySourceSummary()
        {
            int exact = 0, conditional = 0;
            foreach (List<LootEnemySource> rows in LootEnemySourcesByItem.Values)
                foreach (LootEnemySource row in rows)
                    if (row.Kind == "GrantedImplant" || row.Kind == "RandomImplant")
                    { if (float.IsNaN(row.MaxPercent)) conditional++; else exact++; }
            UnityEngine.Debug.Log("[ItemIntelligence][EnemyBodySources] installedChanceLinks=" + exact +
                ", conditionalImplantLinks=" + conditional +
                ", excludedImplantBodyCandidates=" + _enemyBodyExcludedImplantCandidates +
                "; random augmentation paths remain conditional; gameplay RNG untouched.");
        }
    }
}
