using System;
using System.Collections.Generic;
using System.Globalization;
using MGSC;

namespace ItemIntelligence
{
    public static partial class ModMain
    {
        // Manual Ctrl+Shift+F10 only. Captures record inputs for the selected implant
        // so an unrepeatable drop report can be investigated without spawning enemies.
        private static void AppendSelectedEnemyImplantDiagnostics(List<string> lines)
        {
            if (string.IsNullOrEmpty(_inspectorItemId)) return;
            try
            {
                ImplantRecord selected = Data.Items.GetSimpleRecord<ImplantRecord>(_inspectorItemId, true);
                if (selected == null) return;
                lines.Add("");
                lines.Add("[EnemyImplantEvidence]");
                lines.Add("Item=" + _inspectorItemId + "; SlotType=" + selected.SlotType +
                    "; Natures=" + EnemyBodyDiagnosticIds(selected.NatureTypes));
                lines.Add("CandidateCacheReady=" + _enemyBodyDataReady + "; LootComplete=" + _lootWarmupComplete);
                lines.Add("BodyScope=ordinary MobClass.BodyTypes; explicit spawn overrides are not modeled.");
                lines.Add("RandomAugmentationScope=possible sources only; health/sequence probabilities are not claimed.");
                lines.Add("MobAugSpawnMinHpPercent=" + Convert.ToString(
                    GetMember(GetStaticMember(typeof(Data), "Global"), "MobAugSpawnMinHpPercent"), CultureInfo.InvariantCulture));
                lines.Add("EnemyHealthMultiplier=" + Convert.ToString(
                    GetMember(GetMember(ResolveStateModule(typeof(Difficulty)), "Preset"), "EnemyHealth"), CultureInfo.InvariantCulture));
                HashSet<string> bodyIds = new HashSet<string>(StringComparer.Ordinal);
                foreach (DataEntry entry in LootWarmupMobClasses)
                {
                    MobClassRecord mob = entry.Value as MobClassRecord;
                    if (mob == null) continue;
                    bool granted = mob.GrantedImplants != null && mob.GrantedImplants.Contains(_inspectorItemId);
                    bool candidate = mob.ImplantCount.Max > 0 && mob.ImplantClasses != null &&
                        mob.ImplantClasses.ContainsKey(selected.AugmentationClass);
                    if (!granted && !candidate) continue;
                    string id = FirstNonEmpty(GetStringMember(mob, "Id"), entry.Key);
                    lines.Add("Mob=" + id + "; Bodies=" + EnemyBodyDiagnosticIds(mob.BodyTypes) +
                        "; HealthMod=" + mob.HealthMod + "; DefaultFaction=" + mob.DefaultItemFactionTag);
                    lines.Add("  Implants=" + mob.ImplantCount.Min + ":" + mob.ImplantCount.Max +
                        "; Classes=" + FormatBaronClassWeights(ReadEnemyBodyWeights(mob.ImplantClasses)) +
                        "; Granted=" + EnemyBodyDiagnosticIds(mob.GrantedImplants));
                    lines.Add("  Augmentations=" + mob.AugCount.Min + ":" + mob.AugCount.Max +
                        "; Classes=" + FormatBaronClassWeights(ReadEnemyBodyWeights(mob.AugmentationClasses)) +
                        "; Granted=" + EnemyBodyDiagnosticIds(mob.GrantedAugmentations));
                    lines.Add("  Whitelist=" + (mob.ItemCategoriesWhitelist == null ? "<null>" :
                        FormatBaronClassWeights(ReadEnemyBodyWeights(mob.ItemCategoriesWhitelist))));
                    foreach (EnemyLootContext context in BuildEnemyLootContexts(id, mob))
                        lines.Add("  Context=" + context.FactionId + "; RawTech=" + context.RawTech +
                            "; EffectiveTech=" + context.EffectiveTech);
                    if (mob.BodyTypes != null) bodyIds.UnionWith(mob.BodyTypes);
                }
                foreach (string id in bodyIds)
                {
                    BodyTypeRecord body = Data.BodyTypes.GetRecord(id, true);
                    if (body == null) { lines.Add("MissingBody=" + id); continue; }
                    lines.Add("Body=" + id + "; Health=" + body.Health + "; Slots=" + EnemyBodyDiagnosticIds(body.WoundSlots));
                    if (body.WoundSlots != null) foreach (string slot in body.WoundSlots)
                        AppendEnemyBodySlotEvidence(lines, slot);
                }
                // Include all selector candidates, including candidates not shown as
                // sources. Failed installation outcomes still belong in a denominator.
                foreach (EnemyBodyCandidate candidate in EnemyBodyCandidates)
                {
                    lines.Add("Candidate=" + candidate.Id + "; Class=" + candidate.Class +
                        "; Tech=" + candidate.Tech + "; Categories=" + EnemyBodyDiagnosticIds(candidate.Categories));
                    if (candidate.Implant != null)
                        lines.Add("  ImplantSlot=" + candidate.Implant.Type +
                            "; Natures=" + EnemyBodyDiagnosticIds(candidate.Implant.Natures));
                }
                foreach (EnemyAugmentationRule aug in EnemyAugmentationRules.Values)
                {
                    if (aug == null) continue;
                    lines.Add("Augmentation=" + aug.Id + "; DefinitionKnown=" + (aug.Slots != null));
                    if (aug.Slots != null) foreach (EnemyBodySlot slot in aug.Slots)
                        AppendEnemyBodySlotEvidence(lines, slot.Id);
                }
                foreach (EnemyImplantRule implant in EnemyImplantRules.Values)
                    if (implant != null)
                        lines.Add("ImplantDefinition=" + implant.Id + "; Slot=" + implant.Type +
                            "; Natures=" + EnemyBodyDiagnosticIds(implant.Natures));
                List<LootEnemySource> sources;
                if (LootEnemySourcesByItem.TryGetValue(_inspectorItemId, out sources))
                    foreach (LootEnemySource source in sources)
                        lines.Add("ShownSource=" + source.MobClassId + "; Kind=" + source.Kind +
                            "; Chance=" + (float.IsNaN(source.MaxPercent) ? "conditional" :
                            source.MinPercent.ToString("R", CultureInfo.InvariantCulture) + ":" +
                            source.MaxPercent.ToString("R", CultureInfo.InvariantCulture)));
            }
            catch (Exception ex) { lines.Add("EnemyImplantEvidenceIncomplete=" + ex.GetType().Name + ": " + ex.Message); }
        }

        private static string EnemyBodyDiagnosticIds(IEnumerable<string> values)
        {
            if (values == null) return "<null>";
            return string.Join(",", new List<string>(values).ToArray());
        }

        private static void AppendEnemyBodySlotEvidence(List<string> lines, string id)
        {
            WoundSlotRecord slot = Data.WoundSlots.GetRecord(id, true);
            if (slot == null) { lines.Add("  MissingSlot=" + id); return; }
            float penalty = 0f, bonus = 0f;
            if (slot.ImplicitPenaltyEffects != null) slot.ImplicitPenaltyEffects.TryGetValue("max_health", out penalty);
            if (slot.ImplicitBonusEffects != null) slot.ImplicitBonusEffects.TryGetValue("max_health", out bonus);
            lines.Add("  Slot=" + id + "; Type=" + slot.SlotType + "; Nature=" + slot.NatureType +
                "; DefaultSockets=" + slot.ImplantSocketsDefault + "; AugSockets=" + slot.ImplantSocketsMin + ":" +
                slot.ImplantSocketsMax + "; HealthPenalty=" + penalty.ToString("R", CultureInfo.InvariantCulture) +
                "; HealthBonus=" + bonus.ToString("R", CultureInfo.InvariantCulture));
        }
    }
}
