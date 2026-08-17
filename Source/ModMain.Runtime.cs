using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Runtime.InteropServices;
using MGSC;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ItemIntelligence
{
    /// <summary>
    /// Better Item Info - Uses & Sources for Quasimorph.
    /// Data-driven, vanilla-first tooltip extension. No dependency on Inventory Search.
    /// </summary>
    public static partial class ModMain
    {
        public const string Version = "1.7.39";
        // Architectural invariant: Item Intelligence is a read-only knowledge browser.
        // It may inspect runtime data and open vanilla screens, but it does not mutate
        // inventory, economy, story variables, faction progression, or save data.
        internal const bool ReadOnlyKnowledgePolicy = true;
        private const string HarmonyId = "Quasimorph.ItemIntelligence.V1";
        private const string ModifiedItemMarker = "_custom";
        private const int MaxQuickRows = 2;

        // v1.5.15: exact keyboard guard copied from the proven InventorySearch path.
        // Quasimorph maps gameplay/UI actions through InputController.IsKeyDown/IsKey/IsKeyUp.
        private const int VkBackspace = 0x08;
        private const float BrowserBackspaceInitialRepeatDelay = 0.36f;
        private const float BrowserBackspaceRepeatInterval = 0.055f;


        private static bool _harmonyPatched;
        private static bool _loggedTooltipTemplateFailure;
        private static IModContext _modContext;

        // v1.7.5 keeps the exact v1.7.4 enemy-loot model, but builds it lazily and in frame-safe slices.
        // GenerateEquipment / corpse-transfer / amputation contracts remain unchanged.

        private static bool _exactMobNameResolverMethodSearched;
        private static MethodInfo _exactMobNameResolverMethod;

        [DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(int vKey);

        [Hook(ModHookType.BeforeBootstrap)]
        public static void BeforeBootstrap(IModContext context)
        {
            if (context != null) _modContext = context;
            EnsureConfigLoaded();
        }

        [Hook(ModHookType.AfterConfigsLoaded)]
        public static void AfterConfigsLoaded(IModContext context)
        {
            if (context != null) _modContext = context;
            EnsureConfigLoaded();
            Debug.Log("[ItemIntelligence] ACTIVE VERSION " + Version + " (StableRelease1739).");
            RunCompatibilityShieldStatic();
            RefreshBuildFingerprint();
            if (ShouldWriteAutomaticDiagnostics()) WriteDiagnosticsReportSafe("AfterConfigsLoaded");

            // Final save-populated strategy tables are not ready yet here. Building the
            // complete graph now caused the old 1374-record pass and then another
            // 1733-record pass in SpaceStarted.
            EnsureHarmonyPatched();
        }

        [Hook(ModHookType.MainMenuStarted)]
        public static void MainMenuStarted(IModContext context)
        {
            _applicationQuitting = false;
            if (context != null) _modContext = context;
            EnsureConfigLoaded();
            CloseInspector();
            HideHoverHint();

            // BuildFix3 Memory Hygiene: entering the main menu is a real session
            // boundary. Release save/runtime references and the large reverse indexes
            // using the same ClearIndexes path already used before a normal rebuild.
            // Live vanilla tooltip pools are only deactivated; we never destroy or
            // mutate vanilla-owned tooltip objects here.
            ClearBrowserTooltipPreviewBindings();
            DeactivateQuickTooltipPools();
            PruneDeadQuickTooltipPools();
            ResetSessionRuntimeReferencesForMenu();
            ResetLootModifierSessionState();
            RunConservativeMemoryHygiene("MainMenuStarted");
            PriceByItem.Clear();
            _indexesBuilt = false;
            ClearIndexes();

            RunCompatibilityShieldStatic();
            RefreshBuildFingerprint();
            if (ShouldWriteAutomaticDiagnostics()) WriteDiagnosticsReportSafe("MainMenuStarted");
            TryRegisterMcm();
            EnsureHarmonyPatched();
            EnsureInspectorDriver();
        }

        [Hook(ModHookType.SpaceStarted)]
        public static void SpaceStarted(IModContext context)
        {
            _applicationQuitting = false;
            if (context != null) _modContext = context;

            // v1.7.36-test8: Runtime orchestrates the space-session boundary, but each
            // feature owns the exact mutable state it resets. Order intentionally mirrors
            // test7's inline assignments so this is an ownership refactor, not a behavior change.
            InitializeTradeSpaceSessionState();
            ResetRuntimeServiceResolverSessionState();
            _iconFailureSchemaLogged = false;
            ResetAmmoRuntimeSessionState();
            InitializeFactionSpaceSessionState();
            ResetStarmapRuntimeSessionState();
            ResetMagnumRuntimeSessionState();

            EnsureConfigLoaded();
            CloseInspector();
            ClearBrowserTooltipPreviewBindings();
            DeactivateQuickTooltipPools();
            PruneDeadQuickTooltipPools();
            InitializeBrowserSpaceSessionState();
            HideHoverHint();

            RunCompatibilityShieldStatic();
            RunCompatibilityShieldRuntime();

            // Build the core graph exactly once from the final save-populated tables.
            // Weapon/Ammo descriptors are warmed incrementally after this hook.
            _indexesBuilt = false;
            BuildIndexesSafe();
            RefreshBuildFingerprint();
            if (ShouldWriteAutomaticDiagnostics()) WriteDiagnosticsReportSafe("SpaceStarted");

            TryRegisterMcm();
            EnsureHarmonyPatched();
            EnsureInspectorDriver();
        }

        internal static void PrepareForApplicationQuitSafe()
        {
            // The persistent driver survives scene changes. Stop all QII frame work as
            // soon as Unity begins application teardown so no reflection/UI work races
            // destroyed game objects. This only changes QII-owned state.
            _applicationQuitting = true;
            StopFeatureFrameWorkForApplicationQuit();
        }




















































































        private static TextAsset FindRuntimeConfigTextAsset(string assetName)
        {
            if (string.IsNullOrEmpty(assetName)) return null;

            string[] paths = new string[]
            {
                assetName,
                "Config/" + assetName,
                "Configs/" + assetName,
                "Data/" + assetName
            };

            for (int i = 0; i < paths.Length; i++)
            {
                try
                {
                    TextAsset direct = Resources.Load<TextAsset>(paths[i]);
                    if (direct != null) return direct;
                }
                catch { }
            }

            try
            {
                string compact = assetName.Replace("config_", string.Empty);
                TextAsset[] loaded = Resources.FindObjectsOfTypeAll<TextAsset>();
                for (int i = 0; i < loaded.Length; i++)
                {
                    TextAsset candidate = loaded[i];
                    if (candidate == null) continue;
                    string name = candidate.name ?? string.Empty;
                    if (string.Equals(name, assetName, StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(name, compact, StringComparison.OrdinalIgnoreCase) ||
                        name.EndsWith(assetName, StringComparison.OrdinalIgnoreCase))
                        return candidate;
                }
            }
            catch { }

            return null;
        }





        private static void AddWeightedIdField(List<string> target, string raw)
        {
            if (target == null || string.IsNullOrWhiteSpace(raw)) return;
            string[] tokens = raw.Split(new char[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < tokens.Length; i += 2)
            {
                string id = tokens[i].Trim();
                if (string.IsNullOrEmpty(id) || string.Equals(id, "none", StringComparison.OrdinalIgnoreCase))
                    continue;
                if (!target.Contains(id)) target.Add(id);
            }
        }

        private static List<string> SplitStableIds(string raw)
        {
            List<string> result = new List<string>();
            if (string.IsNullOrWhiteSpace(raw)) return result;
            string[] tokens = raw.Split(new char[] { ' ', '\t', ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < tokens.Length; i++)
            {
                string id = tokens[i].Trim();
                if (!string.IsNullOrEmpty(id) && !result.Contains(id)) result.Add(id);
            }
            return result;
        }

        private static Dictionary<string, double> ExtractWeightedStringMap(object value)
        {
            Dictionary<string, double> result =
                new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
            if (value == null) return result;

            IDictionary dict = value as IDictionary;
            if (dict != null)
            {
                foreach (DictionaryEntry entry in dict)
                {
                    string key = ConvertToStableString(entry.Key);
                    double weight;
                    if (!string.IsNullOrEmpty(key) &&
                        TryToDoubleSafe(entry.Value, out weight) && weight > 0.0)
                        result[key] = weight;
                }
                return result;
            }

            IEnumerable enumerable = value as IEnumerable;
            if (enumerable != null && !(value is string))
            {
                foreach (object entry in enumerable)
                {
                    if (entry == null) continue;
                    string key = ConvertToStableString(GetMember(entry, "Key"));
                    double weight;
                    if (!string.IsNullOrEmpty(key) &&
                        TryToDoubleSafe(GetMember(entry, "Value"), out weight) && weight > 0.0)
                        result[key] = weight;
                }
            }
            return result;
        }

        private static void ReadIntRange(object value, out int min, out int max)
        {
            min = 0;
            max = 0;
            if (value == null) return;
            int parsed;
            if (TryToInt(GetMember(value, "Min"), out parsed)) min = parsed;
            if (TryToInt(GetMember(value, "Max"), out parsed)) max = parsed;
            if (max < min) max = min;
        }





        private static double GetEnemyCategoryWeight(
            LootItemMeta meta,
            Dictionary<string, double> whitelist,
            bool whitelistExists,
            string factionId,
            out bool eligible)
        {
            eligible = true;
            if (!whitelistExists) return 0.0;

            eligible = false;
            double best = 0.0;
            double factionWeight;
            if (!string.IsNullOrEmpty(factionId) &&
                whitelist.TryGetValue("Faction", out factionWeight) &&
                meta != null && meta.Categories != null && meta.Categories.Contains(factionId))
            {
                eligible = true;
                best = Math.Max(best, factionWeight);
            }

            if (meta != null && meta.Categories != null)
            {
                foreach (string category in meta.Categories)
                {
                    double value;
                    if (!string.IsNullOrEmpty(category) && whitelist.TryGetValue(category, out value))
                    {
                        eligible = true;
                        if (value > best) best = value;
                    }
                }
            }

            return best;
        }

        private static List<LootItemMeta> CollectEnemyCandidates(
            Dictionary<string, double> classWeights,
            Dictionary<string, List<LootItemMeta>> index)
        {
            List<LootItemMeta> result = new List<LootItemMeta>();
            HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (classWeights == null || index == null) return result;

            foreach (KeyValuePair<string, double> pair in classWeights)
            {
                if (string.Equals(pair.Key, "None", StringComparison.OrdinalIgnoreCase)) continue;
                List<LootItemMeta> list;
                if (!index.TryGetValue(pair.Key, out list) || list == null) continue;
                for (int i = 0; i < list.Count; i++)
                {
                    LootItemMeta meta = list[i];
                    if (meta == null || string.IsNullOrEmpty(meta.ItemId) ||
                        meta.ItemId.IndexOf(ModifiedItemMarker, StringComparison.OrdinalIgnoreCase) >= 0)
                        continue;
                    if (seen.Add(meta.ItemId)) result.Add(meta);
                }
            }
            return result;
        }

        private static string GetEnemyMetaClass(LootItemMeta meta, string mode)
        {
            if (meta == null) return string.Empty;
            if (string.Equals(mode, "weapon", StringComparison.Ordinal)) return meta.WeaponClass;
            if (string.Equals(mode, "armor", StringComparison.Ordinal)) return meta.ArmorClass;
            if (string.Equals(mode, "implant", StringComparison.Ordinal)) return meta.AugmentationClass;
            return meta.ItemClass;
        }

        private static double GetEnemySlotGate(Dictionary<string, double> weights)
        {
            if (weights == null || weights.Count == 0) return 0.0;
            double total = 0.0;
            double none = 0.0;
            foreach (KeyValuePair<string, double> pair in weights)
            {
                if (pair.Value <= 0.0) continue;
                total += pair.Value;
                if (string.Equals(pair.Key, "None", StringComparison.OrdinalIgnoreCase))
                    none += pair.Value;
            }
            if (total <= 0.0) return 0.0;
            return Math.Max(0.0, Math.Min(1.0, 1.0 - none / total));
        }

        private static void UpdateEnemyAccumulator(
            Dictionary<string, EnemyChanceAccumulator> map,
            string itemId,
            double percent,
            int contextIndex,
            int rawTech)
        {
            if (map == null || string.IsNullOrEmpty(itemId) || percent <= 0.0) return;
            EnemyChanceAccumulator acc;
            if (!map.TryGetValue(itemId, out acc))
            {
                acc = new EnemyChanceAccumulator();
                map[itemId] = acc;
            }
            acc.Update(
                (float)Math.Max(0.0, Math.Min(100.0, percent)),
                contextIndex,
                rawTech);
        }

        private static int GetEarliestEnemyContextTech(List<EnemyLootContext> contexts)
        {
            if (contexts == null || contexts.Count == 0) return 1;
            int min = int.MaxValue;
            for (int i = 0; i < contexts.Count; i++)
            {
                EnemyLootContext context = contexts[i];
                if (context != null && context.RawTech > 0 && context.RawTech < min)
                    min = context.RawTech;
            }
            return min == int.MaxValue ? 1 : min;
        }

        private static double ProbabilityAtLeastOnceUniformCount(double perRoll, int minRolls, int maxRolls)
        {
            perRoll = Math.Max(0.0, Math.Min(1.0, perRoll));
            if (maxRolls <= 0 || perRoll <= 0.0) return 0.0;
            minRolls = Math.Max(0, minRolls);
            maxRolls = Math.Max(minRolls, maxRolls);
            double sum = 0.0;
            int count = maxRolls - minRolls + 1;
            for (int n = minRolls; n <= maxRolls; n++)
                sum += 1.0 - Math.Pow(1.0 - perRoll, n);
            return count > 0 ? sum / count : 0.0;
        }

        private static void FinalizeEnemyAccumulatorSources(
            string mobClassId,
            string kind,
            string detail,
            int minCount,
            int maxCount,
            int contextCount,
            Dictionary<string, EnemyChanceAccumulator> accumulators)
        {
            if (accumulators == null) return;
            foreach (KeyValuePair<string, EnemyChanceAccumulator> pair in accumulators)
            {
                EnemyChanceAccumulator acc = pair.Value;
                if (acc == null || acc.MaxPercent <= 0f) continue;
                float min = acc.SeenContextCount < contextCount ? 0f : acc.MinPercent;
                AddLootEnemySource(
                    pair.Key,
                    new LootEnemySource(
                        mobClassId,
                        min,
                        acc.MaxPercent,
                        kind,
                        detail,
                        minCount,
                        maxCount,
                        acc.MinRawTech == int.MaxValue ? 0 : acc.MinRawTech));
            }
        }

        private static void IndexEnemyWeightedSlot(
            string mobClassId,
            string kind,
            string mode,
            object rawWeights,
            object rawWhitelist,
            List<EnemyLootContext> contexts,
            int ammoMin,
            int ammoMax)
        {
            Dictionary<string, double> classWeights = ExtractWeightedStringMap(rawWeights);
            if (classWeights.Count == 0 || contexts == null || contexts.Count == 0) return;

            Dictionary<string, List<LootItemMeta>> index = LootItemsByItemClass;
            if (string.Equals(mode, "weapon", StringComparison.Ordinal)) index = LootItemsByWeaponClass;
            else if (string.Equals(mode, "armor", StringComparison.Ordinal)) index = LootItemsByArmorClass;
            else if (string.Equals(mode, "implant", StringComparison.Ordinal)) index = LootImplantsByAugmentationClass;

            List<LootItemMeta> candidates = CollectEnemyCandidates(classWeights, index);
            if (candidates.Count == 0) return;

            Dictionary<string, double> whitelist = ExtractWeightedStringMap(rawWhitelist);
            bool whitelistExists = rawWhitelist != null;
            double gate = GetEnemySlotGate(classWeights);
            if (gate <= 0.0) return;

            Dictionary<string, EnemyChanceAccumulator> accumulators =
                new Dictionary<string, EnemyChanceAccumulator>(StringComparer.OrdinalIgnoreCase);
            Dictionary<string, EnemyChanceAccumulator> ammoAccumulators =
                new Dictionary<string, EnemyChanceAccumulator>(StringComparer.OrdinalIgnoreCase);

            double ammoPositiveChance = 0.0;
            if (ammoMax >= ammoMin && ammoMax >= 0)
            {
                int totalAmmoValues = ammoMax - ammoMin + 1;
                int positiveValues = ammoMax - Math.Max(1, ammoMin) + 1;
                if (positiveValues < 0) positiveValues = 0;
                if (totalAmmoValues > 0)
                    ammoPositiveChance = (double)positiveValues / totalAmmoValues;
            }

            for (int c = 0; c < contexts.Count; c++)
            {
                EnemyLootContext context = contexts[c];
                Dictionary<string, double> eligibleWeights =
                    new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
                double total = 0.0;

                for (int i = 0; i < candidates.Count; i++)
                {
                    LootItemMeta meta = candidates[i];
                    if (meta == null || meta.TechLevel > context.EffectiveTech) continue;
                    string classId = GetEnemyMetaClass(meta, mode);
                    double baseWeight;
                    if (string.IsNullOrEmpty(classId) || !classWeights.TryGetValue(classId, out baseWeight))
                        continue;
                    if (string.Equals(mode, "armor", StringComparison.Ordinal) &&
                        !IsEnemyArmorSlotCompatible(meta, kind))
                        continue;

                    bool categoryEligible;
                    double categoryWeight = GetEnemyCategoryWeight(
                        meta, whitelist, whitelistExists, context.FactionId, out categoryEligible);
                    if (!categoryEligible) continue;

                    double finalWeight = baseWeight + categoryWeight;
                    if (finalWeight <= 0.0) continue;
                    eligibleWeights[meta.ItemId] = finalWeight;
                    total += finalWeight;
                }

                if (total <= 0.0) continue;
                Dictionary<string, double> ammoThisContext =
                    new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);

                for (int i = 0; i < candidates.Count; i++)
                {
                    LootItemMeta meta = candidates[i];
                    if (meta == null) continue;
                    double weight;
                    if (!eligibleWeights.TryGetValue(meta.ItemId, out weight)) continue;
                    double percent = gate * weight / total * 100.0;
                    UpdateEnemyAccumulator(accumulators, meta.ItemId, percent, c, context.RawTech);

                    if (string.Equals(mode, "weapon", StringComparison.Ordinal) &&
                        ammoPositiveChance > 0.0 && !string.IsNullOrEmpty(meta.DefaultAmmoId) &&
                        KnownItemIds.Contains(meta.DefaultAmmoId))
                    {
                        double existing;
                        ammoThisContext.TryGetValue(meta.DefaultAmmoId, out existing);
                        ammoThisContext[meta.DefaultAmmoId] = existing + percent * ammoPositiveChance;
                    }
                }

                foreach (KeyValuePair<string, double> ammo in ammoThisContext)
                    UpdateEnemyAccumulator(ammoAccumulators, ammo.Key, ammo.Value, c, context.RawTech);
            }

            FinalizeEnemyAccumulatorSources(
                mobClassId, kind, string.Empty, 1, 1,
                contexts.Count, accumulators);

            if (ammoAccumulators.Count > 0)
            {
                string ammoKind = string.Equals(kind, "Primary", StringComparison.Ordinal)
                    ? "PrimaryAmmo" : "SecondaryAmmo";
                FinalizeEnemyAccumulatorSources(
                    mobClassId, ammoKind,
                    ammoMin.ToString(CultureInfo.InvariantCulture) + "-" +
                    ammoMax.ToString(CultureInfo.InvariantCulture),
                    Math.Max(0, ammoMin), Math.Max(0, ammoMax),
                    contexts.Count, ammoAccumulators);
            }
        }

        private static void IndexEnemyAdditionalItems(
            string mobClassId,
            object mobRecord,
            object rawWhitelist,
            List<EnemyLootContext> contexts,
            int ammoMin,
            int ammoMax)
        {
            object rawClasses = GetMember(mobRecord, "AdditItemClasses");
            Dictionary<string, double> classWeights = ExtractWeightedStringMap(rawClasses);
            if (classWeights.Count == 0 || contexts == null || contexts.Count == 0) return;

            int minRolls, maxRolls;
            ReadIntRange(GetMember(mobRecord, "AdditItemCount"), out minRolls, out maxRolls);
            List<LootItemMeta> candidates = CollectEnemyCandidates(classWeights, LootItemsByItemClass);
            if (candidates.Count == 0) return;

            Dictionary<string, double> whitelist = ExtractWeightedStringMap(rawWhitelist);
            bool whitelistExists = rawWhitelist != null;
            Dictionary<string, EnemyChanceAccumulator> inventoryAcc =
                new Dictionary<string, EnemyChanceAccumulator>(StringComparer.OrdinalIgnoreCase);
            Dictionary<string, EnemyChanceAccumulator> corpseBonusPerRollAcc =
                new Dictionary<string, EnemyChanceAccumulator>(StringComparer.OrdinalIgnoreCase);
            Dictionary<string, EnemyChanceAccumulator> conditionalAmmoAcc =
                new Dictionary<string, EnemyChanceAccumulator>(StringComparer.OrdinalIgnoreCase);

            double ammoPositiveChance = 0.0;
            if (ammoMax >= ammoMin && ammoMax >= 0)
            {
                int totalAmmoValues = ammoMax - ammoMin + 1;
                int positiveAmmoValues = ammoMax - Math.Max(1, ammoMin) + 1;
                if (positiveAmmoValues < 0) positiveAmmoValues = 0;
                if (totalAmmoValues > 0)
                    ammoPositiveChance = (double)positiveAmmoValues / totalAmmoValues;
            }

            for (int c = 0; c < contexts.Count; c++)
            {
                EnemyLootContext context = contexts[c];
                Dictionary<string, double> eligible =
                    new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
                Dictionary<string, double> corpseEligible =
                    new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
                double total = 0.0;
                double corpseTotal = 0.0;

                for (int i = 0; i < candidates.Count; i++)
                {
                    LootItemMeta meta = candidates[i];
                    if (meta == null) continue;
                    double baseWeight;
                    if (string.IsNullOrEmpty(meta.ItemClass) ||
                        !classWeights.TryGetValue(meta.ItemClass, out baseWeight))
                        continue;
                    double finalWeight = baseWeight;
                    if (finalWeight <= 0.0) continue;

                    // GenerateEquipment applies EquipmentTechLevelBonus to the spawn
                    // Tech limit. CloneInventoryForCorpse's FLootCorpseItem rolls use
                    // the faction/current Tech directly, so these are intentionally two
                    // different eligible pools.
                    if (meta.TechLevel <= context.EffectiveTech)
                    {
                        eligible[meta.ItemId] = finalWeight;
                        total += finalWeight;
                    }
                    if (meta.TechLevel <= context.RawTech)
                    {
                        corpseEligible[meta.ItemId] = finalWeight;
                        corpseTotal += finalWeight;
                    }
                }

                if (total > 0.0)
                {
                    foreach (KeyValuePair<string, double> pair in eligible)
                    {
                        double perRoll = pair.Value / total;
                        double overall = ProbabilityAtLeastOnceUniformCount(perRoll, minRolls, maxRolls) * 100.0;
                        UpdateEnemyAccumulator(inventoryAcc, pair.Key, overall, c, context.RawTech);

                        LootItemMeta selectedMeta;
                        if (ammoPositiveChance > 0.0 &&
                            LootItemMetaById.TryGetValue(pair.Key, out selectedMeta) && selectedMeta != null &&
                            !string.IsNullOrEmpty(selectedMeta.DefaultAmmoId) &&
                            KnownItemIds.Contains(selectedMeta.DefaultAmmoId))
                        {
                            // Extra weapons are generated before SpawnAdditionalAmmo. They
                            // receive default ammo only if TakeOrEquip leaves them in one of
                            // the weapon slots, which depends on earlier primary/secondary
                            // rolls and inventory order. 0..upper-bound is therefore exact.
                            UpdateEnemyAccumulator(
                                conditionalAmmoAcc,
                                selectedMeta.DefaultAmmoId,
                                overall * ammoPositiveChance,
                                c,
                                context.RawTech);
                        }
                    }
                }

                if (corpseTotal > 0.0)
                {
                    foreach (KeyValuePair<string, double> pair in corpseEligible)
                        UpdateEnemyAccumulator(
                            corpseBonusPerRollAcc,
                            pair.Key,
                            pair.Value / corpseTotal * 100.0,
                            c,
                            context.RawTech);
                }
            }

            if (maxRolls > 0)
            {
                FinalizeEnemyAccumulatorSources(
                    mobClassId, "Additional", string.Empty,
                    Math.Max(0, minRolls), Math.Max(0, maxRolls),
                    contexts.Count, inventoryAcc);
            }

            if (conditionalAmmoAcc.Count > 0)
            {
                foreach (KeyValuePair<string, EnemyChanceAccumulator> pair in conditionalAmmoAcc)
                {
                    EnemyChanceAccumulator acc = pair.Value;
                    if (acc == null || acc.MaxPercent <= 0f) continue;
                    AddLootEnemySource(
                        pair.Key,
                        new LootEnemySource(
                            mobClassId, 0f, acc.MaxPercent,
                            "ExtraWeaponAmmo", "slot-dependent",
                            Math.Max(0, ammoMin), Math.Max(0, ammoMax),
                            acc.MinRawTech == int.MaxValue ? 0 : acc.MinRawTech));
                }
            }

            // CloneInventoryForCorpse performs additional independent AdditItemClasses
            // rolls when the player's FLootCorpseItem bonus resolves above zero. Store
            // the exact per-roll probability. Do not invoke GetAdditionalCorpseDropBonus
            // from UI rendering because that gameplay helper can perform a random roll.
            FinalizeEnemyAccumulatorSources(
                mobClassId, "CorpseBonus", "per-roll",
                0, 0, contexts.Count, corpseBonusPerRollAcc);
        }

        // v1.7.11 Quasi-source audit: mirrors vanilla Randomize category/faction eligibility.
        private static void IndexEnemyImplantAttempts(
            string mobClassId,
            object mobRecord,
            object rawWhitelist,
            List<EnemyLootContext> contexts)
        {
            List<string> granted = ExtractStringIds(GetMember(mobRecord, "GrantedImplants"));
            for (int i = 0; i < granted.Count; i++)
            {
                string itemId = granted[i];
                if (!string.IsNullOrEmpty(itemId) && KnownItemIds.Contains(itemId))
                    AddLootEnemySource(itemId, new LootEnemySource(
                        mobClassId, 100f, 100f, "GrantedImplant", "install attempt", 1, 1,
                        GetEarliestEnemyContextTech(contexts)));
            }

            Dictionary<string, double> classWeights =
                ExtractWeightedStringMap(GetMember(mobRecord, "ImplantClasses"));
            if (classWeights.Count == 0 || contexts == null || contexts.Count == 0) return;

            int minRolls, maxRolls;
            ReadIntRange(GetMember(mobRecord, "ImplantCount"), out minRolls, out maxRolls);
            if (maxRolls <= 0) return;

            List<LootItemMeta> candidates = CollectEnemyCandidates(
                classWeights, LootImplantsByAugmentationClass);
            if (candidates.Count == 0) return;

            Dictionary<string, double> whitelist = ExtractWeightedStringMap(rawWhitelist);
            bool whitelistExists = rawWhitelist != null;
            double gate = GetEnemySlotGate(classWeights);
            Dictionary<string, EnemyChanceAccumulator> acc =
                new Dictionary<string, EnemyChanceAccumulator>(StringComparer.OrdinalIgnoreCase);

            for (int c = 0; c < contexts.Count; c++)
            {
                EnemyLootContext context = contexts[c];
                Dictionary<string, double> eligible =
                    new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
                double total = 0.0;
                for (int i = 0; i < candidates.Count; i++)
                {
                    LootItemMeta meta = candidates[i];
                    if (meta == null || meta.TechLevel > context.EffectiveTech) continue;
                    double baseWeight;
                    if (string.IsNullOrEmpty(meta.AugmentationClass) ||
                        !classWeights.TryGetValue(meta.AugmentationClass, out baseWeight)) continue;

                    // Match CreatureSystem.GenerateAugmentations -> ItemDropSystem.Randomize.
                    // Quasi* class weights alone are not enough: with a MobClass
                    // ItemCategoriesWhitelist the candidate must also match a whitelist
                    // category or the current spawn faction through the Faction entry.
                    bool categoryEligible;
                    double categoryWeight = GetEnemyCategoryWeight(
                        meta, whitelist, whitelistExists, context.FactionId, out categoryEligible);
                    if (!categoryEligible) continue;

                    double finalWeight = baseWeight + categoryWeight;
                    if (finalWeight <= 0.0) continue;
                    eligible[meta.ItemId] = finalWeight;
                    total += finalWeight;
                }
                if (total <= 0.0) continue;
                foreach (KeyValuePair<string, double> pair in eligible)
                {
                    double perAttempt = gate * pair.Value / total;
                    double overall = ProbabilityAtLeastOnceUniformCount(perAttempt, minRolls, maxRolls) * 100.0;
                    UpdateEnemyAccumulator(acc, pair.Key, overall, c, context.RawTech);
                }
            }

            FinalizeEnemyAccumulatorSources(
                mobClassId, "RandomImplant", "install attempt",
                Math.Max(0, minRolls), Math.Max(0, maxRolls),
                contexts.Count, acc);
        }



        // v1.7.11 Quasi-source audit: random augmentations use vanilla category/faction eligibility.
        private static void IndexEnemyAugmentationAttempts(
            string mobClassId,
            object mobRecord,
            object rawWhitelist,
            List<EnemyLootContext> contexts)
        {
            List<string> granted = ExtractStringIds(GetMember(mobRecord, "GrantedAugmentations"));
            for (int i = 0; i < granted.Count; i++)
            {
                List<string> itemIds = ResolveLootExternalItemIds(granted[i]);
                for (int j = 0; j < itemIds.Count; j++)
                {
                    AddLootEnemySource(itemIds[j], new LootEnemySource(
                        mobClassId, 100f, 100f, "GrantedAugmentation", "installed", 1, 1,
                        GetEarliestEnemyContextTech(contexts)));
                }
            }

            Dictionary<string, double> classWeights =
                ExtractWeightedStringMap(GetMember(mobRecord, "AugmentationClasses"));
            if (classWeights.Count == 0 || contexts == null || contexts.Count == 0) return;

            int minRolls, maxRolls;
            ReadIntRange(GetMember(mobRecord, "AugCount"), out minRolls, out maxRolls);
            if (maxRolls <= 0) return;

            List<LootItemMeta> candidates = CollectEnemyCandidates(
                classWeights, LootAugmentationsByAugmentationClass);
            if (candidates.Count == 0) return;

            Dictionary<string, double> whitelist = ExtractWeightedStringMap(rawWhitelist);
            bool whitelistExists = rawWhitelist != null;
            double gate = GetEnemySlotGate(classWeights);
            Dictionary<string, EnemyChanceAccumulator> acc =
                new Dictionary<string, EnemyChanceAccumulator>(StringComparer.OrdinalIgnoreCase);

            for (int c = 0; c < contexts.Count; c++)
            {
                EnemyLootContext context = contexts[c];
                Dictionary<string, double> eligible =
                    new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
                double total = 0.0;
                for (int i = 0; i < candidates.Count; i++)
                {
                    LootItemMeta meta = candidates[i];
                    if (meta == null || meta.TechLevel > context.EffectiveTech) continue;
                    double baseWeight;
                    if (string.IsNullOrEmpty(meta.AugmentationClass) ||
                        !classWeights.TryGetValue(meta.AugmentationClass, out baseWeight)) continue;

                    // Match CreatureSystem.GenerateAugmentations -> ItemDropSystem.Randomize.
                    // Quasi* class weights alone are not enough: with a MobClass
                    // ItemCategoriesWhitelist the candidate must also match a whitelist
                    // category or the current spawn faction through the Faction entry.
                    bool categoryEligible;
                    double categoryWeight = GetEnemyCategoryWeight(
                        meta, whitelist, whitelistExists, context.FactionId, out categoryEligible);
                    if (!categoryEligible) continue;

                    double finalWeight = baseWeight + categoryWeight;
                    if (finalWeight <= 0.0) continue;
                    eligible[meta.ItemId] = finalWeight;
                    total += finalWeight;
                }
                if (total <= 0.0) continue;
                foreach (KeyValuePair<string, double> pair in eligible)
                {
                    double perAttempt = gate * pair.Value / total;
                    double overall = ProbabilityAtLeastOnceUniformCount(perAttempt, minRolls, maxRolls) * 100.0;
                    UpdateEnemyAccumulator(acc, pair.Key, overall, c, context.RawTech);
                }
            }

            FinalizeEnemyAccumulatorSources(
                mobClassId, "RandomAugmentation", "install attempt",
                Math.Max(0, minRolls), Math.Max(0, maxRolls),
                contexts.Count, acc);
        }





        private static double CorpseBonusAtLeastOnceChance(double perRoll, double expectedRolls)
        {
            perRoll = Math.Max(0.0, Math.Min(1.0, perRoll));
            expectedRolls = Math.Max(0.0, expectedRolls);
            if (perRoll <= 0.0 || expectedRolls <= 0.0) return 0.0;

            // CreatureData.RollExpectedCount(expected) resolves floor(expected) rolls
            // plus one extra roll with probability equal to the fractional remainder.
            // Integrating both outcomes gives the exact chance without consuming the
            // gameplay RNG from the information UI.
            int floorRolls = Math.Max(0, (int)Math.Floor(expectedRolls));
            double fraction = expectedRolls - floorRolls;
            double pFloor = 1.0 - Math.Pow(1.0 - perRoll, floorRolls);
            double pCeil = 1.0 - Math.Pow(1.0 - perRoll, floorRolls + 1);
            return (1.0 - fraction) * pFloor + fraction * pCeil;
        }









        private static string ResolveEquipmentSlotKindFromNode(
            object node, string typeName, string itemId, HashSet<string> categories, string itemClass)
        {
            // Equipment records can share the same ArmorClass across several physical slots.
            // Prefer explicit slot metadata and item identity before generic ArmorRecord names.
            List<string> probes = new List<string>();
            if (node != null)
            {
                string[] memberNames = new string[]
                {
                    "EquipmentSlot", "EquipmentSlotType", "Slot", "SlotType",
                    "ArmorSlot", "BodySlot", "WearSlot", "EquipmentType"
                };
                for (int i = 0; i < memberNames.Length; i++)
                {
                    string value = ConvertToStableString(GetMember(node, memberNames[i]));
                    if (!string.IsNullOrEmpty(value)) probes.Add(value);
                }
            }
            probes.Add(itemId ?? string.Empty);
            if (categories != null)
                foreach (string category in categories) probes.Add(category ?? string.Empty);
            probes.Add(typeName ?? string.Empty);
            probes.Add(itemClass ?? string.Empty);

            string joined = string.Join("|", probes.ToArray()).ToLowerInvariant();
            if (joined.IndexOf("boots", StringComparison.Ordinal) >= 0 ||
                joined.IndexOf("boot", StringComparison.Ordinal) >= 0 ||
                joined.IndexOf("shoe", StringComparison.Ordinal) >= 0 ||
                joined.IndexOf("footwear", StringComparison.Ordinal) >= 0 ||
                joined.IndexOf("feet", StringComparison.Ordinal) >= 0)
                return "Boots";
            if (joined.IndexOf("helmet", StringComparison.Ordinal) >= 0 ||
                joined.IndexOf("headgear", StringComparison.Ordinal) >= 0 ||
                joined.IndexOf("head", StringComparison.Ordinal) >= 0)
                return "Head";
            if (joined.IndexOf("legging", StringComparison.Ordinal) >= 0 ||
                joined.IndexOf("pants", StringComparison.Ordinal) >= 0 ||
                joined.IndexOf("greave", StringComparison.Ordinal) >= 0 ||
                joined.IndexOf("legs", StringComparison.Ordinal) >= 0)
                return "Leggings";
            if (joined.IndexOf("armor", StringComparison.Ordinal) >= 0 ||
                joined.IndexOf("armour", StringComparison.Ordinal) >= 0 ||
                joined.IndexOf("vest", StringComparison.Ordinal) >= 0 ||
                joined.IndexOf("torso", StringComparison.Ordinal) >= 0 ||
                joined.IndexOf("bodyarmor", StringComparison.Ordinal) >= 0)
                return "Armor";
            return string.Empty;
        }

        private static bool IsEnemyArmorSlotCompatible(LootItemMeta meta, string slotKind)
        {
            if (meta == null) return false;
            string slot = meta.EquipmentSlotKind ?? string.Empty;
            if (string.IsNullOrEmpty(slot)) return true;
            if (string.Equals(slotKind, "Armor", StringComparison.OrdinalIgnoreCase))
                return string.Equals(slot, "Armor", StringComparison.OrdinalIgnoreCase);
            return string.Equals(slot, slotKind ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        }



        private static HashSet<string> ExtractAugmentationRecordAliases(
            string itemId,
            List<object> graph)
        {
            HashSet<string> result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (graph == null) return result;
            for (int i = 0; i < graph.Count; i++)
            {
                object node = graph[i];
                if (node == null) continue;
                string nodeAugClass = ConvertToStableString(GetMember(node, "AugmentationClass"));
                if (string.IsNullOrEmpty(nodeAugClass))
                {
                    string typeName = node.GetType().Name ?? string.Empty;
                    if (typeName.IndexOf("Augment", StringComparison.OrdinalIgnoreCase) < 0)
                        continue;
                }

                string[] ids = new string[]
                {
                    ConvertToStableString(GetMember(node, "AugmentationId")),
                    ConvertToStableString(GetMember(node, "AugmentationRecordId")),
                    ConvertToStableString(GetMember(node, "Id"))
                };
                for (int j = 0; j < ids.Length; j++)
                {
                    string id = ids[j] ?? string.Empty;
                    if (string.IsNullOrEmpty(id)) continue;
                    if (string.Equals(id, itemId, StringComparison.OrdinalIgnoreCase)) continue;
                    if (KnownItemIds.Contains(id)) continue;
                    result.Add(id);
                }
            }
            return result;
        }









        private static HashSet<string> ExtractStableStringSet(object value)
        {
            HashSet<string> result =
                new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (value == null) return result;

            string direct = value as string;
            if (direct != null)
            {
                if (!string.IsNullOrEmpty(direct)) result.Add(direct);
                return result;
            }

            IEnumerable enumerable = value as IEnumerable;
            if (enumerable != null)
            {
                int count = 0;
                foreach (object item in enumerable)
                {
                    if (++count > 4096) break;
                    string textValue = ConvertToStableString(item);
                    if (!string.IsNullOrEmpty(textValue)) result.Add(textValue);
                }
                return result;
            }

            string single = ConvertToStableString(value);
            if (!string.IsNullOrEmpty(single)) result.Add(single);
            return result;
        }

        private static bool StringSetsOverlap(
            HashSet<string> left,
            HashSet<string> right)
        {
            if (left == null || right == null ||
                left.Count == 0 || right.Count == 0)
                return false;

            HashSet<string> small = left.Count <= right.Count ? left : right;
            HashSet<string> large = object.ReferenceEquals(small, left) ? right : left;
            foreach (string value in small)
                if (large.Contains(value)) return true;
            return false;
        }







        private static string FormatPercentValue(float value)
        {
            if (value < 0f) return "n/a";
            return value.ToString(value < 10f ? "0.#" : "0", CultureInfo.InvariantCulture) + "%";
        }







        private static object FindCanonicalItemRecord(object root)
        {
            if (root == null) return null;

            Queue<GraphScanNode> queue = new Queue<GraphScanNode>();
            HashSet<object> seen = new HashSet<object>(ReferenceComparer.Instance);
            queue.Enqueue(new GraphScanNode(root, 0));

            int inspected = 0;
            while (queue.Count > 0 && inspected < 64)
            {
                GraphScanNode current = queue.Dequeue();
                object value = current.Value;
                if (value == null || value is string) continue;

                Type type = value.GetType();
                if (IsSimple(type)) continue;
                if (seen.Contains(value)) continue;
                seen.Add(value);
                inspected++;

                // This is the exact field consumed by ItemInteractionSystem.Disassemble.
                MemberInfo disassemblyMember = FindCachedMember(type, "Disassembly", false);
                if (disassemblyMember != null)
                {
                    object disassembly = GetMemberValue(value, disassemblyMember);
                    if (disassembly is IEnumerable && !(disassembly is string))
                        return value;
                }

                if (current.Depth >= 3) continue;

                // CompositeItemRecord stores the real records in Records.
                object records = GetMember(value, "Records");
                IEnumerable enumerableRecords = records as IEnumerable;
                if (enumerableRecords != null && !(records is string))
                {
                    int n = 0;
                    foreach (object child in enumerableRecords)
                    {
                        if (++n > 64) break;
                        if (child != null)
                            queue.Enqueue(new GraphScanNode(child, current.Depth + 1));
                    }
                }

                string[] directMembers = new string[]
                {
                    "Record", "ItemRecord", "PrimaryRecord", "ContentRecord",
                    "Descriptor", "ContentDescriptor"
                };

                for (int i = 0; i < directMembers.Length; i++)
                {
                    object child = GetMember(value, directMembers[i]);
                    if (child != null && !object.ReferenceEquals(child, value))
                        queue.Enqueue(new GraphScanNode(child, current.Depth + 1));
                }
            }

            return null;
        }







        private static bool IsRandomDropMember(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;
            return name.IndexOf("Drop", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   name.IndexOf("Pool", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   name.IndexOf("Random", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   name.IndexOf("Chance", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   name.IndexOf("Loot", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool IsContainerLikeItem(string itemId)
        {
            if (string.IsNullOrEmpty(itemId)) return false;
            return itemId.IndexOf("container", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   itemId.IndexOf("crate", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   itemId.IndexOf("case", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   itemId.IndexOf("box", StringComparison.OrdinalIgnoreCase) >= 0;
        }







        private static bool HasRandomWeight(object value)
        {
            if (value == null) return false;
            return GetMember(value, "Weight") != null ||
                   GetMember(value, "DropWeight") != null ||
                   GetMember(value, "SelectionWeight") != null;
        }

























        private static void AnalyzeDatadiskGraph(string datadiskItemId, List<object> graph)
        {
            if (string.IsNullOrEmpty(datadiskItemId) || graph == null) return;

            for (int i = 0; i < graph.Count; i++)
            {
                object node = graph[i];
                if (node == null) continue;

                Type type = node.GetType();
                string typeName = type.Name ?? string.Empty;
                bool datadiskLike =
                    typeName.IndexOf("Datadisk", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    HasMemberNamed(type, "UnlockId") ||
                    HasMemberNamed(type, "UnlockIds");

                if (!datadiskLike) continue;

                ProductionDatadiskItemIds.Add(datadiskItemId);

                string unlockType = ConvertToStableString(GetMember(node, "UnlockType"));
                List<string> unlockIds = new List<string>();

                string direct = GetStringMember(node, "UnlockId");
                if (!string.IsNullOrEmpty(direct)) unlockIds.Add(direct);

                object unlockIdsValue = GetMember(node, "UnlockIds");
                List<string> rawMany = ExtractRawStringIds(unlockIdsValue);
                if (rawMany.Count > 0)
                {
                    // Exact MGSC.DatadiskRecord.UnlockIds is authoritative. If an unusual
                    // object graph exposes only fallback datadisk-like wrappers, accept a
                    // fallback pool only while every observation agrees exactly. Never use
                    // the old "longest list wins" heuristic for probability data.
                    bool exactDatadiskRecord = string.Equals(
                        type.FullName,
                        "MGSC.DatadiskRecord",
                        StringComparison.Ordinal);
                    ObserveDatadiskUnlockPool(datadiskItemId, rawMany, exactDatadiskRecord);
                }

                for (int n = 0; n < rawMany.Count; n++)
                {
                    string rawId = rawMany[n];
                    if (!string.IsNullOrEmpty(rawId) && !ContainsIgnoreCase(unlockIds, rawId))
                        unlockIds.Add(rawId);
                }

                if (unlockIds.Count == 0) continue;

                bool explicitlyItem =
                    unlockType.IndexOf("Item", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    unlockType.IndexOf("Production", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    unlockType.IndexOf("Receipt", StringComparison.OrdinalIgnoreCase) >= 0;

                for (int n = 0; n < unlockIds.Count; n++)
                {
                    string unlockId = unlockIds[n];
                    if (string.IsNullOrEmpty(unlockId)) continue;

                    string outputItemId = ResolveDatadiskUnlockToItemId(unlockId);
                    if (!explicitlyItem && !KnownItemIds.Contains(outputItemId))
                        continue;

                    if (!KnownItemIds.Contains(outputItemId))
                        continue;

                    AddUniqueString(DatadisksByUnlockedItem, outputItemId, datadiskItemId);
                    AddUniqueString(ItemsUnlockedByDatadisk, datadiskItemId, outputItemId);
                }
            }
        }

        private static void ObserveDatadiskUnlockPool(
            string datadiskItemId,
            List<string> rawPool,
            bool canonical)
        {
            if (string.IsNullOrEmpty(datadiskItemId) || rawPool == null || rawPool.Count == 0) return;

            string fingerprint = BuildUnlockPoolFingerprint(rawPool);

            if (canonical)
            {
                string canonicalPrevious;
                if (CanonicalUnlockPoolFingerprintByDatadisk.TryGetValue(datadiskItemId, out canonicalPrevious) &&
                    !string.Equals(canonicalPrevious, fingerprint, StringComparison.Ordinal))
                {
                    RawUnlockPoolByDatadisk.Remove(datadiskItemId);
                    UnlockHitCountsByDatadisk.Remove(datadiskItemId);
                    UnlockPoolSizeByDatadisk.Remove(datadiskItemId);
                    AmbiguousUnlockPoolDatadisks.Add(datadiskItemId);
                    LogRuntimeBoundaryWarningOnce(
                        "chip.pool.canonical-conflict." + datadiskItemId,
                        "Conflicting canonical DatadiskRecord.UnlockIds pools were observed for " + datadiskItemId +
                        "; chip chance is hidden rather than choosing one.",
                        null);
                    return;
                }

                CanonicalUnlockPoolDatadisks.Add(datadiskItemId);
                CanonicalUnlockPoolFingerprintByDatadisk[datadiskItemId] = fingerprint;
                AmbiguousUnlockPoolDatadisks.Remove(datadiskItemId);
                FallbackUnlockPoolFingerprintByDatadisk.Remove(datadiskItemId);
                SetRawDatadiskUnlockPool(datadiskItemId, rawPool);
                return;
            }

            // Once the concrete vanilla record was observed, wrapper/graph aliases cannot
            // override it. This removes the old dependence on traversal order or list size.
            if (CanonicalUnlockPoolDatadisks.Contains(datadiskItemId)) return;

            string previous;
            if (!FallbackUnlockPoolFingerprintByDatadisk.TryGetValue(datadiskItemId, out previous))
            {
                FallbackUnlockPoolFingerprintByDatadisk[datadiskItemId] = fingerprint;
                SetRawDatadiskUnlockPool(datadiskItemId, rawPool);
                return;
            }

            if (string.Equals(previous, fingerprint, StringComparison.Ordinal)) return;

            // Conflicting graph-only candidates are not trustworthy enough for a percentage.
            // Keep the unlock relations themselves, but remove chance caches until/unless an
            // exact DatadiskRecord is later encountered in the same bounded graph.
            RawUnlockPoolByDatadisk.Remove(datadiskItemId);
            UnlockHitCountsByDatadisk.Remove(datadiskItemId);
            UnlockPoolSizeByDatadisk.Remove(datadiskItemId);
            AmbiguousUnlockPoolDatadisks.Add(datadiskItemId);
            LogRuntimeBoundaryWarningOnce(
                "chip.pool.ambiguous." + datadiskItemId,
                "Conflicting graph-only UnlockIds pools were observed for " + datadiskItemId +
                "; chip chance is hidden unless the canonical DatadiskRecord is found.",
                null);
        }

        private static string BuildUnlockPoolFingerprint(List<string> rawPool)
        {
            if (rawPool == null || rawPool.Count == 0) return string.Empty;
            StringBuilder sb = new StringBuilder(rawPool.Count * 16);
            for (int i = 0; i < rawPool.Count; i++)
            {
                if (i > 0) sb.Append('\u001f');
                sb.Append(rawPool[i] ?? string.Empty);
            }
            return sb.ToString();
        }

        private static void SetRawDatadiskUnlockPool(string datadiskItemId, List<string> rawPool)
        {
            if (string.IsNullOrEmpty(datadiskItemId) || rawPool == null || rawPool.Count == 0) return;

            RawUnlockPoolByDatadisk[datadiskItemId] = new List<string>(rawPool);
            Dictionary<string, int> counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < rawPool.Count; i++)
            {
                string resolved = ResolveDatadiskUnlockToItemId(rawPool[i]);
                if (string.IsNullOrEmpty(resolved)) continue;
                int current;
                counts.TryGetValue(resolved, out current);
                counts[resolved] = current + 1;
            }
            UnlockHitCountsByDatadisk[datadiskItemId] = counts;
            UnlockPoolSizeByDatadisk[datadiskItemId] = rawPool.Count;
        }

        private static string ResolveDatadiskUnlockToItemId(string unlockId)
        {
            if (string.IsNullOrEmpty(unlockId)) return string.Empty;
            RecipeDef recipe;
            if (RecipesById.TryGetValue(unlockId, out recipe) && recipe != null && !string.IsNullOrEmpty(recipe.OutputItemId))
                return recipe.OutputItemId;
            return unlockId;
        }

        private static bool ContainsIgnoreCase(List<string> values, string value)
        {
            if (values == null || string.IsNullOrEmpty(value)) return false;
            for (int i = 0; i < values.Count; i++)
                if (string.Equals(values[i], value, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
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
            if (!_chipUnlockChanceContractVerified || AmbiguousUnlockPoolDatadisks.Contains(datadiskItemId)) return false;

            Dictionary<string, int> counts;
            int cachedTotal;
            if (UnlockHitCountsByDatadisk.TryGetValue(datadiskItemId, out counts) && counts != null &&
                UnlockPoolSizeByDatadisk.TryGetValue(datadiskItemId, out cachedTotal) && cachedTotal > 0)
            {
                totalEntries = cachedTotal;
                counts.TryGetValue(outputItemId, out matchingEntries);
                if (matchingEntries <= 0) return false;
                chancePercent = (100f * matchingEntries) / totalEntries;
                return true;
            }

            // Fallback for an unusual runtime graph that bypassed the normal warmup cache.
            List<string> rawPool;
            if (!RawUnlockPoolByDatadisk.TryGetValue(datadiskItemId, out rawPool) || rawPool == null || rawPool.Count == 0)
                return false;
            totalEntries = rawPool.Count;
            for (int i = 0; i < rawPool.Count; i++)
            {
                string resolved = ResolveDatadiskUnlockToItemId(rawPool[i]);
                if (string.Equals(resolved, outputItemId, StringComparison.OrdinalIgnoreCase)) matchingEntries++;
            }
            if (matchingEntries <= 0) return false;
            chancePercent = (100f * matchingEntries) / totalEntries;
            return true;
        }

        private static string FormatChipUnlockChance(float percent)
        {
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

        private static List<object> BuildRelevantItemGraph(object root, int maxDepth, int maxObjects)
        {
            List<object> result = new List<object>();
            if (root == null || maxObjects <= 0) return result;

            Queue<GraphScanNode> queue = new Queue<GraphScanNode>();
            HashSet<object> seen = new HashSet<object>(ReferenceComparer.Instance);
            queue.Enqueue(new GraphScanNode(root, 0));

            while (queue.Count > 0 && result.Count < maxObjects)
            {
                GraphScanNode current = queue.Dequeue();
                object value = current.Value;
                if (value == null) continue;

                Type type = value.GetType();
                if (IsSimple(type)) continue;
                if (seen.Contains(value)) continue;
                seen.Add(value);
                result.Add(value);

                if (current.Depth >= maxDepth) continue;

                IDictionary dict = value as IDictionary;
                if (dict != null)
                {
                    int n = 0;
                    foreach (DictionaryEntry entry in dict)
                    {
                        if (++n > 24) break;
                        if (entry.Key != null) queue.Enqueue(new GraphScanNode(entry.Key, current.Depth + 1));
                        if (entry.Value != null) queue.Enqueue(new GraphScanNode(entry.Value, current.Depth + 1));
                    }
                    continue;
                }

                IEnumerable enumerable = value as IEnumerable;
                if (enumerable != null && !(value is string))
                {
                    int n = 0;
                    foreach (object entry in enumerable)
                    {
                        if (++n > 24) break;
                        if (entry != null) queue.Enqueue(new GraphScanNode(entry, current.Depth + 1));
                    }
                    continue;
                }

                List<MemberInfo> members = GetReadableMembers(type);
                for (int i = 0; i < members.Count; i++)
                {
                    MemberInfo member = members[i];
                    string name = member.Name ?? string.Empty;
                    Type memberType = GetMemberDeclaredType(member);
                    string memberTypeName = memberType == null ? string.Empty : (memberType.Name ?? string.Empty);

                    bool relevant =
                        name.IndexOf("Descriptor", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        name.IndexOf("Record", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        name.IndexOf("Content", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        name.IndexOf("Component", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        name.IndexOf("Weapon", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        name.IndexOf("Ammo", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        name.IndexOf("Caliber", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        name.IndexOf("Calibre", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        string.Equals(name, "Value", StringComparison.OrdinalIgnoreCase) ||
                        memberTypeName.IndexOf("Descriptor", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        memberTypeName.IndexOf("Record", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        memberTypeName.IndexOf("Component", StringComparison.OrdinalIgnoreCase) >= 0;

                    if (!relevant) continue;

                    object nested = GetMemberValue(value, member);
                    if (nested == null || object.ReferenceEquals(nested, value)) continue;
                    queue.Enqueue(new GraphScanNode(nested, current.Depth + 1));
                }
            }

            return result;
        }

        private static void LogItemGraphDiagnostic(string itemId)
        {
            object record;
            if (string.IsNullOrEmpty(itemId) ||
                !ItemRecordsById.TryGetValue(itemId, out record) ||
                record == null)
                return;

            try
            {
                List<object> graph = BuildRelevantItemGraph(record, 4, 48);
                List<string> types = new List<string>();
                for (int i = 0; i < graph.Count && i < 24; i++)
                {
                    string name = graph[i].GetType().FullName;
                    if (!types.Contains(name)) types.Add(name);
                }
                Debug.LogWarning("[ItemIntelligence] Ammo graph diagnostic " + itemId +
                    ": root=" + record.GetType().FullName +
                    ", graphTypes=" + string.Join(" -> ", types.ToArray()) + ".");
            }
            catch { }
        }

        private static bool HasMemberNamed(Type type, string name)
        {
            if (type == null || string.IsNullOrEmpty(name)) return false;
            // Member metadata is immutable for the AppDomain. Reuse the shared positive
            // and negative lookup caches instead of issuing GetField/GetProperty on every
            // ammo-graph probe.
            return FindCachedMember(type, name, false) != null ||
                   FindCachedMember(type, name, true) != null;
        }









        private static void EnsureHarmonyPatched()
        {
            if (_harmonyPatched) return;
            try
            {
                Harmony harmony = new Harmony(HarmonyId);
                int patched = 0;

                // SAFETY CONTRACT:
                // Never patch vanilla Alt/details construction.
                // v1.5.6 hover isolation:
                // BuildItemTooltip and RestoreItemTooltip are generic factory paths also
                // used by station/production UI. Do not patch them at all.
                patched += PatchNamedMethods(harmony, "MGSC.TooltipFactory", "AddPriceBlock", "PriceBlockPrefix", null);
                patched += PatchNamedMethods(harmony, "MGSC.PropertiesTooltip", "SetPriceBlock", null, "SetPriceBlockPostfix");

                int tooltipPointerPatches = 0;
                if (_compatTooltip)
                {
                    tooltipPointerPatches += PatchNamedMethods(
                        harmony,
                        "MGSC.ItemTooltipHandler",
                        "OnPointerEnter",
                        "ItemPointerEnterPrefix",
                        "ItemPointerEnterPostfix");

                    tooltipPointerPatches += PatchNamedMethods(
                        harmony,
                        "MGSC.ItemTooltipHandler",
                        "OnPointerExit",
                        "ItemPointerExitPrefix",
                        null);

                    patched += tooltipPointerPatches;

                    if (tooltipPointerPatches < 2)
                        TripCompatibilityFeature(
                            "Tooltip",
                            "Required ItemTooltipHandler pointer Harmony patches were not installed.");
                }

                // Quasimorph's inventory grid does not reliably call ItemTooltipHandler's
                // pointer interface directly. The visible vanilla tooltip is driven through
                // ItemSlot.OnPointerEnter, so bridge that exact path with O(1) direct field
                // reads. This is intentionally separate from generic station tooltip code.
                patched += PatchNamedMethods(harmony, "MGSC.ItemSlot", "OnPointerEnter", "ItemSlotPointerEnterPrefix", "ItemSlotPointerEnterPostfix");
                patched += PatchNamedMethods(harmony, "MGSC.ItemSlot", "OnPointerExit", "ItemSlotPointerExitPrefix", null);

                // v1.5.15: use the exact action-query layer already proven by InventorySearch.
                // InventorySearch patches the three bool InputController methods
                // IsKeyDown / IsKey / IsKeyUp. Those are the action queries Quasimorph
                // actually uses for Back, movement and UI/gameplay hotkeys.
                //
                // Item Intelligence is a modal Item Intelligence browser, so suppress ALL game actions
                // while the browser is open, not only while the search field is focused.
                // Our browser still receives F2/Esc/tab/search text through Unity Input/TMP.
                int inputActionPatches = 0;
                if (_compatInputGuard)
                {
                    inputActionPatches =
                        PatchInputControllerActionQueries(harmony);
                    patched += inputActionPatches;

                    if (inputActionPatches < 3)
                        TripCompatibilityFeature(
                            "InputGuard",
                            "InputController action-query patches are incomplete.");
                }

                if (_compatTooltip ||
                    _compatInputGuard)
                    patched += PatchInventoryInputMethods(
                        harmony,
                        "MGSC.ItemTooltipHandler");
                patched += PatchInventoryInputMethods(harmony, "MGSC.ItemSlot");
                patched += PatchInventoryInputMethods(harmony, "MGSC.ItemGrid");

                // DragController is the actual inventory mutation gate in Quasimorph 1.0.1.
                // Pointer-only patches stopped tooltips but still allowed held-item movement,
                // so while Item Intelligence is modal we suppress every declared DragController operation.
                if (_compatInputGuard)
                    patched += PatchAllDeclaredMethodsWhileInspectorOpen(
                        harmony,
                        "MGSC.DragController");

                if (_compatInputGuard)
                    patched += PatchInventoryInputMethods(
                        harmony,
                        "MGSC.ItemsStorageView");

                // v1.7.36-test7: always observe vanilla spaceship travel and install
                // a QII-scoped departure backstop. This is safety infrastructure, not
                // diagnostic audit code, so it must be present in normal builds.
                int starmapTravelSafetyPatches = InstallStarmapTravelSafetyPatches(harmony);
                patched += starmapTravelSafetyPatches;
                if (starmapTravelSafetyPatches < 3)
                {
                    Debug.LogError("[ItemIntelligence][StarmapTravelSafety] Required safety hooks are incomplete; " +
                        "QII Starmap navigation will fail closed.");
                }

                WriteCompatibilityReport();

                if (patched == 0)
                    throw new MissingMethodException(
                        "No supported vanilla tooltip methods were found.");
                _harmonyPatched = true;
                Debug.Log("[ItemIntelligence] Vanilla hover isolation ready. Generic station/production tooltip builders are not patched. Vanilla Alt details are not patched. Patched methods: " + patched);
            }
            catch (Exception ex)
            {
                Debug.LogError("[ItemIntelligence] Harmony integration failed. Vanilla tooltips remain untouched: " + ex);
            }
        }



        private static int PatchInventoryInputMethods(Harmony harmony, string typeName)
        {
            Type type = AccessTools.TypeByName(typeName);
            if (type == null) return 0;

            MethodInfo prefix = typeof(ModMain).GetMethod("BlockVanillaInventoryPointerPrefix", StaticFlags);
            if (prefix == null) return 0;

            int patched = 0;
            try
            {
                MethodInfo[] methods = type.GetMethods(InstanceFlags | StaticFlags);
                for (int i = 0; i < methods.Length; i++)
                {
                    MethodInfo method = methods[i];
                    if (method == null || method.IsAbstract || method.ContainsGenericParameters) continue;
                    string name = method.Name ?? string.Empty;

                    bool pointer =
                        name.IndexOf("OnPointer", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        name.IndexOf("OnBeginDrag", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        name.IndexOf("OnDrag", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        name.IndexOf("OnEndDrag", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        name.IndexOf("StartDrag", StringComparison.OrdinalIgnoreCase) >= 0;
                    if (!pointer) continue;

                    try
                    {
                        harmony.Patch(method, prefix: new HarmonyMethod(prefix));
                        patched++;
                    }
                    catch (Exception ex)
                    {
                        LogRuntimeBoundaryWarningOnce(
                            "harmony.inventory." + typeName + "." + method.Name,
                            "Could not install an inventory modal guard patch on " + typeName + "." + method.Name + ".",
                            ex);
                    }
                }
            }
            catch (Exception ex)
            {
                LogRuntimeBoundaryWarningOnce(
                    "harmony.inventory.scan." + typeName,
                    "Inventory modal guard scan failed for " + typeName + ".",
                    ex);
            }

            return patched;
        }

        private static int PatchInputControllerActionQueries(Harmony harmony)
        {
            MethodInfo prefix = typeof(ModMain).GetMethod(
                "InputControllerModalActionPrefix",
                BindingFlags.Static | BindingFlags.NonPublic);

            if (prefix == null)
                return 0;

            int patched = 0;

            try
            {
                MethodInfo[] methods = typeof(InputController).GetMethods(
                    BindingFlags.Static | BindingFlags.Instance |
                    BindingFlags.Public | BindingFlags.NonPublic);

                for (int i = 0; i < methods.Length; i++)
                {
                    MethodInfo method = methods[i];

                    if (method == null || method.ReturnType != typeof(bool))
                        continue;

                    if (!string.Equals(method.Name, "IsKeyDown", StringComparison.Ordinal) &&
                        !string.Equals(method.Name, "IsKey", StringComparison.Ordinal) &&
                        !string.Equals(method.Name, "IsKeyUp", StringComparison.Ordinal))
                        continue;

                    try
                    {
                        harmony.Patch(method, prefix: new HarmonyMethod(prefix));
                        patched++;
                    }
                    catch (Exception ex)
                    {
                        LogRuntimeBoundaryWarningOnce(
                            "harmony.input." + method.Name,
                            "Could not install the modal input guard on InputController." + method.Name + ".",
                            ex);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[ItemIntelligence] InputController modal guard scan failed: " + ex.Message);
            }

            Debug.Log("[ItemIntelligence] InputController modal action guard patched methods: " + patched);
            return patched;
        }

        private static bool InputControllerModalActionPrefix(ref bool __result)
        {
            if (!_inspectorOpen)
            {
                // If a configurable hotkey such as X opens QII over a valid item, swallow
                // Quasimorph action queries on that same frame. Otherwise X could both
                // open Item Intelligence and trigger its vanilla gameplay/UI binding.
                try
                {
                    if (ShouldCaptureInspectorHotkeyOpeningFrame())
                    {
                        __result = false;
                        return false;
                    }
                }
                catch (Exception ex)
                {
                    LogRuntimeBoundaryWarningOnce(
                        "input.hotkey.capture",
                        "Inspector opening-frame input capture failed; vanilla input was allowed for this query.",
                        ex);
                }
                return true;
            }

            // The Item Intelligence browser is modal. Quasimorph must see no gameplay/UI
            // action while it is open. Raw keyboard text still reaches TMP_InputField.
            __result = false;
            return false;
        }

        private static bool BlockVanillaInventoryPointerPrefix(object __instance)
        {
            if (!_inspectorOpen)
                return true;

            // The Item Intelligence modal blocks vanilla inventory pointer interaction behind it,
            // but every QII-owned item icon is allowed to run the native
            // ItemTooltipHandler. Station/faction/status icons are never registered as
            // item-tooltip targets, so the Trade tab remains non-interactive here.
            if (IsBrowserOwnedItemTooltipHandler(__instance))
                return true;

            return false;
        }

        private static int PatchNamedMethods(Harmony harmony, string typeName, string methodName, string prefixName, string postfixName)
        {
            Type type = AccessTools.TypeByName(typeName);
            if (type == null)
            {
                Debug.LogWarning("[ItemIntelligence] Type not found: " + typeName);
                return 0;
            }
            MethodInfo prefix = string.IsNullOrEmpty(prefixName) ? null : typeof(ModMain).GetMethod(prefixName, StaticFlags);
            MethodInfo postfix = string.IsNullOrEmpty(postfixName) ? null : typeof(ModMain).GetMethod(postfixName, StaticFlags);
            int count = 0;
            MethodInfo[] methods = type.GetMethods(InstanceFlags | StaticFlags);
            for (int i = 0; i < methods.Length; i++)
            {
                MethodInfo method = methods[i];
                if (!string.Equals(method.Name, methodName, StringComparison.Ordinal)) continue;
                try
                {
                    harmony.Patch(method,
                        prefix == null ? null : new HarmonyMethod(prefix),
                        postfix == null ? null : new HarmonyMethod(postfix));
                    count++;
                }
                catch (Exception ex)
                {
                    Debug.LogWarning("[ItemIntelligence] Could not patch " + typeName + "." + methodName + ": " + ex.Message);
                }
            }
            return count;
        }


        private static void PriceBlockPrefix(object __instance, object[] __args)
        {
            // Station and conveyor generic tooltips can call AddPriceBlock too. Outside the
            // synchronous ItemTooltipHandler scope this hook is intentionally O(1).
            if (!_itemPointerScope || _itemPointerScopeFrame != Time.frameCount) return;
            if (string.IsNullOrEmpty(_itemPointerScopeItemId) || !IsKnownItemId(_itemPointerScopeItemId)) return;

            try
            {
                if (__instance != null) _activeTooltipFactory = __instance;
                _priceBlockItemId = _itemPointerScopeItemId;
                _priceBlockFrame = Time.frameCount;
                _lastHoveredItemId = _itemPointerScopeItemId;
            }
            catch { }
        }


        private static void SetPriceBlockPostfix(object __instance, object[] __args)
        {
            if (!_itemPointerScope || _itemPointerScopeFrame != Time.frameCount) return;
            if (_priceBlockFrame != Time.frameCount || string.IsNullOrEmpty(_priceBlockItemId)) return;

            try
            {
                Component direct = __instance as Component;
                if (direct != null) _activeTooltip = direct;
                _lastHoveredItemId = _priceBlockItemId;
                if (__args == null || __args.Length < 3) return;
                int owned;
                int required;
                if (!TryToInt(__args[1], out owned) || !TryToInt(__args[2], out required)) return;
                PriceByItem[_priceBlockItemId] = new PriceSnapshot(owned, required);
            }
            catch { }
        }

        private static void BuildItemTooltipPostfix(object __instance, object[] __args)
        {
            // Not installed in v1.5.6. Guard only for unusual hot-reload cases.
            if (!_itemPointerScope || _itemPointerScopeFrame != Time.frameCount) return;
            if (!EnableItemIntelligence) return;
            System.Diagnostics.Stopwatch timer = System.Diagnostics.Stopwatch.StartNew();
            string itemId = string.Empty;
            try
            {
                if (__instance != null) _activeTooltipFactory = __instance;
                itemId = FindItemIdInArgs(__args);
                if (string.IsNullOrEmpty(itemId) && _priceBlockFrame == Time.frameCount) itemId = _priceBlockItemId;

                // Never reuse a previous hover ID here. BuildItemTooltip overloads are also
                // reached by station/production UI; stale fallback was making QII process
                // non-item tooltips as the last inventory item.
                if (string.IsNullOrEmpty(itemId) || !IsKnownItemId(itemId)) return;

                _lastHoveredItemId = itemId;
                Component tooltip = FindPropertiesTooltip(__instance);
                if (tooltip == null) tooltip = _activeTooltip;
                if (QuickIntelligence) AppendQuick(tooltip, itemId);
                RefreshInspectorForHoveredItem(itemId);
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[ItemIntelligence] BuildItemTooltip integration skipped: " + ex.Message);
            }
            finally
            {
                timer.Stop();
                if (timer.ElapsedMilliseconds >= 25 && _slowTooltipWarnings < 8)
                {
                    _slowTooltipWarnings++;
                    Debug.LogWarning("[ItemIntelligence] Slow item tooltip hot path: " +
                        timer.ElapsedMilliseconds.ToString(CultureInfo.InvariantCulture) + " ms, item=" +
                        (string.IsNullOrEmpty(itemId) ? "<none>" : itemId) + ".");
                }
            }
        }

        private static void ItemTooltipBuildPostfix(object __instance, object[] __args)
        {
            BuildItemTooltipPostfix(__instance, __args);
        }


        private static object ResolveItemTooltipHandlerFromSlot(object slot)
        {
            if (slot == null) return null;

            string[] names = new string[]
            {
                "_itemTooltipHandler",
                "ItemTooltipHandler",
                "_tooltipHandler",
                "TooltipHandler"
            };

            for (int i = 0; i < names.Length; i++)
            {
                object handler = GetMember(slot, names[i]);
                if (handler != null &&
                    string.Equals(handler.GetType().Name, "ItemTooltipHandler", StringComparison.Ordinal))
                    return handler;
            }

            return null;
        }

        private static string ResolveItemSlotItemId(object slot)
        {
            if (slot == null) return string.Empty;

            // First use exact members that exist across Quasimorph item-slot variants.
            string[] idNames = new string[]
            {
                "_itemId",
                "ItemId",
                "ItemID"
            };

            for (int i = 0; i < idNames.Length; i++)
            {
                string directId = ConvertToStableString(GetMember(slot, idNames[i]));
                if (!string.IsNullOrEmpty(directId) && IsKnownItemId(directId))
                    return directId;
            }

            string[] valueNames = new string[]
            {
                "_itemRecord",
                "ItemRecord",
                "_record",
                "Record",
                "_item",
                "Item",
                "_itemData",
                "ItemData"
            };

            for (int i = 0; i < valueNames.Length; i++)
            {
                object value = GetMember(slot, valueNames[i]);
                if (value == null) continue;

                string id = GetItemIdDeep(value, 0);
                if (string.IsNullOrEmpty(id))
                    id = FirstNonEmpty(
                        GetStringMember(value, "Id"),
                        GetStringMember(value, "ItemId"),
                        GetStringMember(value, "ItemID"));

                if (!string.IsNullOrEmpty(id) && IsKnownItemId(id))
                    return id;
            }

            // ItemSlot commonly owns an ItemTooltipHandler. If so, use its known exact fields.
            object handler = ResolveItemTooltipHandlerFromSlot(slot);
            if (handler != null)
                return ResolveItemTooltipHandlerItemId(handler);

            return string.Empty;
        }

        private static Component ResolveCreatedItemTooltipFromSlot(object slot)
        {
            object handler = ResolveItemTooltipHandlerFromSlot(slot);
            if (handler != null)
            {
                _lastItemPointerHandler = handler;
                Component tooltip = ResolveCreatedItemTooltip(handler);
                if (tooltip != null) return tooltip;
            }

            // A few slot variants expose the created tooltip directly.
            string[] names = new string[]
            {
                "_createdTooltip",
                "CreatedTooltip",
                "_tooltip",
                "Tooltip"
            };

            for (int i = 0; i < names.Length; i++)
            {
                object raw = GetMember(slot, names[i]);

                Component component = raw as Component;
                if (component != null) return component;

                GameObject go = raw as GameObject;
                if (go != null) return go.transform;
            }

            return null;
        }

        private static void ItemSlotPointerEnterPrefix(object __instance)
        {
            _lastItemSlot = __instance;

            try
            {
                string itemId = ResolveItemSlotItemId(__instance);
                if (!string.IsNullOrEmpty(itemId))
                    _lastHoveredItemId = itemId;
            }
            catch { }
        }

        private static void ItemSlotPointerEnterPostfix(object __instance)
        {
            _lastItemSlot = __instance;

            try
            {
                string itemId = ResolveItemSlotItemId(__instance);
                if (string.IsNullOrEmpty(itemId) || !IsKnownItemId(itemId))
                {
                    // Empty inventory cells legitimately receive pointer callbacks.
                    // Absence of a bound item is not a compatibility warning; actual
                    // resolver exceptions remain bounded and logged below.
                    return;
                }

                _lastHoveredItemId = itemId;

                CaptureVanillaItemSlotIcon(itemId, __instance);

                Component tooltip = ResolveCreatedItemTooltipFromSlot(__instance);
                if (tooltip != null)
                    _activeTooltip = tooltip;

                if (EnableItemIntelligence)
                    ShowHoverHint(itemId);
            }
            catch (Exception ex)
            {
                if (_itemSlotHoverResolveWarnings < 4)
                {
                    _itemSlotHoverResolveWarnings++;
                    Debug.LogWarning("[ItemIntelligence] ItemSlot hover bridge skipped: " + ex.Message);
                }
            }
        }

        private static void ItemSlotPointerExitPrefix(object __instance)
        {
            // Do not clear _lastHoveredItemId. F2 is intentionally bound to the last
            // explicit item slot until another item slot is hovered.
            HideHoverHint();
        }

        private static string ResolveItemTooltipHandlerItemId(object handler)
        {
            if (handler == null) return string.Empty;

            try
            {
                object record = GetMember(handler, "_itemRecord");
                string id = GetItemIdDeep(record, 0);
                if (string.IsNullOrEmpty(id))
                    id = FirstNonEmpty(
                        GetStringMember(record, "Id"),
                        GetStringMember(record, "ItemId"),
                        GetStringMember(record, "ItemID"));

                if (!string.IsNullOrEmpty(id) && IsKnownItemId(id))
                    return id;
            }
            catch { }

            try
            {
                object item = GetMember(handler, "_item");
                string id = GetItemIdDeep(item, 0);
                if (string.IsNullOrEmpty(id))
                    id = FirstNonEmpty(
                        GetStringMember(item, "Id"),
                        GetStringMember(item, "ItemId"),
                        GetStringMember(item, "ItemID"));

                if (!string.IsNullOrEmpty(id) && IsKnownItemId(id))
                    return id;
            }
            catch { }

            return string.Empty;
        }

        private static Component ResolveCreatedItemTooltip(object handler)
        {
            if (handler == null) return null;

            try
            {
                // Quasimorph 1.0.1.566s audit: ItemTooltipHandler._createdTooltip is a
                // bool flag, not a tooltip reference. The created item tooltip itself is
                // TooltipFactory._tooltip (PropertiesTooltip). Keep this lookup O(1) by
                // using only the factory already captured during the synchronous build.
                object created = GetMember(handler, "_createdTooltip");
                if (created is bool && !(bool)created) return null;

                object factory = _activeTooltipFactory;
                if (factory == null) return null;
                Component tooltip = GetMember(factory, "_tooltip") as Component;
                if (tooltip != null && tooltip.gameObject != null && tooltip.gameObject.activeInHierarchy)
                    return tooltip;
            }
            catch { }

            return null;
        }











        private static void ItemPointerEnterPrefix(object __instance)
        {
            _itemPointerScope = true;
            _itemPointerScopeFrame = Time.frameCount;
            _itemPointerScopeItemId = string.Empty;
            _lastItemPointerHandler = __instance;

            try
            {
                BrowserItemTooltipBinding binding = ResolveBrowserItemTooltipBinding(__instance);
                ItemTooltipHandler browserHandler = __instance as ItemTooltipHandler;
                if (binding != null && browserHandler != null &&
                    !string.IsNullOrEmpty(binding.ItemId))
                {
                    PrepareBrowserBoundTooltipHandler(browserHandler, binding);
                    _itemPointerScopeItemId = binding.ItemId;
                    _lastHoveredItemId = binding.ItemId;
                    return;
                }

                string id = ResolveItemTooltipHandlerItemId(__instance);
                if (!string.IsNullOrEmpty(id))
                {
                    _itemPointerScopeItemId = id;
                    _lastHoveredItemId = id;
                }
            }
            catch { }
        }

        private static void ItemPointerEnterPostfix(object __instance)
        {
            try
            {
                _lastItemPointerHandler = __instance;

                string itemId = _itemPointerScopeItemId;
                if (string.IsNullOrEmpty(itemId) || !IsKnownItemId(itemId))
                    itemId = ResolveItemTooltipHandlerItemId(__instance);

                if ((string.IsNullOrEmpty(itemId) || !IsKnownItemId(itemId)) &&
                    _priceBlockFrame == Time.frameCount &&
                    !string.IsNullOrEmpty(_priceBlockItemId) &&
                    IsKnownItemId(_priceBlockItemId))
                {
                    itemId = _priceBlockItemId;
                }

                if (!string.IsNullOrEmpty(itemId) && IsKnownItemId(itemId))
                {
                    _itemPointerScopeItemId = itemId;
                    _lastHoveredItemId = itemId;

                    Component createdTooltip = ResolveCreatedItemTooltip(__instance);
                    if (createdTooltip != null)
                        _activeTooltip = createdTooltip;

                    if (EnableItemIntelligence)
                        ShowHoverHint(itemId);
                    else
                        HideHoverHint();
                }
                else
                {
                    HideHoverHint();

                    object record = GetMember(__instance, "_itemRecord");
                    object item = GetMember(__instance, "_item");
                    // Vanilla also keeps unbound ItemTooltipHandler components on
                    // empty/hidden controls. Only warn when a real payload existed but
                    // could not be mapped to a known item.
                    if ((record != null || item != null) && _itemHoverResolveWarnings < 4)
                    {
                        _itemHoverResolveWarnings++;
                        Debug.LogWarning(
                            "[ItemIntelligence] Direct ItemTooltipHandler resolve failed: _itemRecord=" +
                            (record == null ? "null" : record.GetType().FullName) +
                            ", _item=" +
                            (item == null ? "null" : item.GetType().FullName) + ".");
                    }
                }
            }
            catch (Exception ex)
            {
                HideHoverHint();

                if (_itemHoverResolveWarnings < 4)
                {
                    _itemHoverResolveWarnings++;
                    Debug.LogWarning("[ItemIntelligence] Direct item hover resolver skipped: " + ex.Message);
                }
            }
            finally
            {
                _itemPointerScope = false;
            }
        }

        private static void ItemPointerExitPrefix(object __instance)
        {
            if (IsBrowserOwnedItemTooltipHandler(__instance))
                RestoreBrowserTooltipLayer();

            _itemPointerScope = false;
            _itemPointerScopeFrame = -1000;
            _itemPointerScopeItemId = string.Empty;
            HideHoverHint();

            // Keep the last explicit item target for a deliberate F2 press.
            // No vanilla tooltip/layout mutation occurs here.
        }

        private static void RestoreTooltipPostfix(object __instance, object[] __args)
        {
            // Not installed in v1.5.6. Guard only for unusual hot-reload cases.
            if (!_itemPointerScope || _itemPointerScopeFrame != Time.frameCount) return;
            if (!EnableItemIntelligence || !QuickIntelligence) return;
            try
            {
                string itemId = FindItemIdInArgs(__args);
                if (string.IsNullOrEmpty(itemId)) itemId = _priceBlockItemId;
                if (string.IsNullOrEmpty(itemId)) itemId = _lastHoveredItemId;
                if (string.IsNullOrEmpty(itemId)) return;
                _lastHoveredItemId = itemId;
                if (__instance != null) _activeTooltipFactory = __instance;
                Component tooltip = FindPropertiesTooltip(__instance);
                if (tooltip == null) tooltip = _activeTooltip;
                AppendQuick(tooltip, itemId);
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[ItemIntelligence] Restored quick tooltip skipped: " + ex.Message);
            }
        }

        private static void ShowHoverHint(string itemId)
        {
            if (!InspectorEnabled || _inspectorOpen || !ShowInspectorHint || string.IsNullOrEmpty(itemId))
            {
                HideHoverHint();
                return;
            }

            try
            {
                EnsureHoverHintOverlay();
                if (_hoverHintCanvas == null || _hoverHintRect == null || _hoverHintText == null) return;

                bool ru = IsRussian();
                _hoverHintText.text = HotkeyUi("ui.f2_item_analysis");

                // v1.5.18: the hint is intentionally NOT attached to the active tooltip.
                // Different vanilla item tooltip layouts have different bounds/pivots and
                // caused the badge to jump around the screen. Keep one stable HUD location.
                _hoverHintRect.sizeDelta = new Vector2(238f, 28f);
                _hoverHintRect.anchoredPosition = new Vector2(-14f, 14f);
                _hoverHintCanvas.SetActive(true);
            }
            catch { }
        }

        private static bool TryGetActiveTooltipScreenRect(out float minX, out float minY, out float maxX, out float maxY)
        {
            minX = 0f;
            minY = 0f;
            maxX = 0f;
            maxY = 0f;

            try
            {
                if ((_activeTooltip == null || _activeTooltip.gameObject == null ||
                     !_activeTooltip.gameObject.activeInHierarchy) &&
                    _lastItemPointerHandler != null)
                {
                    Component createdTooltip = ResolveCreatedItemTooltip(_lastItemPointerHandler);
                    if (createdTooltip != null)
                        _activeTooltip = createdTooltip;
                }

                if ((_activeTooltip == null || _activeTooltip.gameObject == null ||
                     !_activeTooltip.gameObject.activeInHierarchy) &&
                    _lastItemSlot != null)
                {
                    Component createdTooltip = ResolveCreatedItemTooltipFromSlot(_lastItemSlot);
                    if (createdTooltip != null)
                        _activeTooltip = createdTooltip;
                }

                if (_activeTooltip == null || _activeTooltip.gameObject == null ||
                    !_activeTooltip.gameObject.activeInHierarchy)
                    return false;

                RectTransform rect = _activeTooltip.transform as RectTransform;
                if (rect == null) return false;

                // PropertiesTooltip is normally on the tooltip root. If a game update moves
                // it to a small child, climb only a few parents and choose the first sensible
                // tooltip-sized rect. This is constant-time and never scans children/layout.
                RectTransform candidate = rect;
                RectTransform current = rect;
                for (int depth = 0; depth < 3 && current != null; depth++)
                {
                    Rect localRect = current.rect;
                    if (localRect.width >= 180f && localRect.height >= 80f)
                    {
                        candidate = current;
                        break;
                    }

                    Transform parent = current.parent;
                    current = parent as RectTransform;
                }

                Canvas tooltipCanvas = candidate.GetComponentInParent<Canvas>();
                Camera camera = null;
                if (tooltipCanvas != null && tooltipCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
                    camera = tooltipCanvas.worldCamera;

                Vector3[] corners = new Vector3[4];
                candidate.GetWorldCorners(corners);

                Vector2 p0 = RectTransformUtility.WorldToScreenPoint(camera, corners[0]);
                minX = p0.x;
                maxX = p0.x;
                minY = p0.y;
                maxY = p0.y;

                for (int i = 1; i < 4; i++)
                {
                    Vector2 p = RectTransformUtility.WorldToScreenPoint(camera, corners[i]);
                    if (p.x < minX) minX = p.x;
                    if (p.x > maxX) maxX = p.x;
                    if (p.y < minY) minY = p.y;
                    if (p.y > maxY) maxY = p.y;
                }

                return maxX > minX + 20f && maxY > minY + 20f;
            }
            catch
            {
                return false;
            }
        }

        private static void EnsureHoverHintOverlay()
        {
            if (_hoverHintCanvas != null && _hoverHintRect != null && _hoverHintText != null) return;

            try
            {
                _hoverHintCanvas = new GameObject("QII_HoverHintCanvas");
                UnityEngine.Object.DontDestroyOnLoad(_hoverHintCanvas);

                Canvas canvas = _hoverHintCanvas.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.overrideSorting = true;
                canvas.sortingOrder = 32760;

                CanvasGroup group = _hoverHintCanvas.AddComponent<CanvasGroup>();
                group.interactable = false;
                group.blocksRaycasts = false;
                group.ignoreParentGroups = true;

                GameObject box = new GameObject("QII_HoverHint");
                box.transform.SetParent(_hoverHintCanvas.transform, false);
                _hoverHintRect = box.AddComponent<RectTransform>();
                _hoverHintRect.anchorMin = new Vector2(1f, 0f);
                _hoverHintRect.anchorMax = new Vector2(1f, 0f);
                _hoverHintRect.pivot = new Vector2(1f, 0f);
                _hoverHintRect.anchoredPosition = new Vector2(-14f, 14f);
                _hoverHintRect.sizeDelta = new Vector2(238f, 28f);

                Image bg = box.AddComponent<Image>();
                bg.color = new Color(0.008f, 0.030f, 0.024f, 0.97f);
                bg.raycastTarget = false;

                Outline outline = box.AddComponent<Outline>();
                outline.effectColor = new Color(0.30f, 0.68f, 0.49f, 0.90f);
                outline.effectDistance = new Vector2(1f, -1f);
                outline.useGraphicAlpha = true;

                GameObject accentGo = new GameObject("Accent");
                accentGo.transform.SetParent(box.transform, false);
                RectTransform accentRect = accentGo.AddComponent<RectTransform>();
                accentRect.anchorMin = new Vector2(0f, 0f);
                accentRect.anchorMax = new Vector2(0f, 1f);
                accentRect.pivot = new Vector2(0f, 0.5f);
                accentRect.anchoredPosition = Vector2.zero;
                accentRect.sizeDelta = new Vector2(4f, 0f);
                Image accent = accentGo.AddComponent<Image>();
                accent.color = new Color(0.53f, 0.90f, 0.61f, 1f);
                accent.raycastTarget = false;

                GameObject textGo = new GameObject("Text");
                textGo.transform.SetParent(box.transform, false);
                RectTransform textRect = textGo.AddComponent<RectTransform>();
                textRect.anchorMin = Vector2.zero;
                textRect.anchorMax = Vector2.one;
                textRect.offsetMin = new Vector2(12f, 2f);
                textRect.offsetMax = new Vector2(-8f, -2f);

                TextMeshProUGUI tmp = textGo.AddComponent<TextMeshProUGUI>();
                _hoverHintText = tmp;
                if (_inspectorFont != null) tmp.font = _inspectorFont;
                tmp.fontSize = 15f;
                tmp.fontStyle = FontStyles.Normal;
                tmp.color = new Color(0.66f, 0.90f, 0.72f, 1f);
                tmp.alignment = TextAlignmentOptions.MidlineLeft;
                tmp.enableWordWrapping = false;
                tmp.raycastTarget = false;
                tmp.overflowMode = TextOverflowModes.Truncate;

                _hoverHintCanvas.SetActive(false);
            }
            catch
            {
                _hoverHintCanvas = null;
                _hoverHintRect = null;
                _hoverHintText = null;
            }
        }

        private static void HideHoverHint()
        {
            try
            {
                if (_hoverHintCanvas != null)
                    _hoverHintCanvas.SetActive(false);
            }
            catch { }
        }

        private static void AppendQuick(Component tooltip, string itemId)
        {
            if (tooltip == null || string.IsNullOrEmpty(itemId)) return;
            List<DisplayRow> rows = BuildQuickRows(itemId);
            if (rows.Count == 0)
            {
                RemoveInjectedRows(tooltip);
                return;
            }
            InjectQuickRowsPooled(tooltip, rows);
        }

        private static List<DisplayRow> BuildQuickRows(string itemId)
        {
            List<DisplayRow> rows = new List<DisplayRow>();
            bool ru = IsRussian();

            if (ShowMagnumUses)
            {
                int magnumRequired = 0;
                MagnumSnapshot magnum = GetMagnumSnapshot(itemId);
                if (magnum != null && magnum.TotalRemaining > 0)
                    magnumRequired = magnum.TotalRemaining;

                PriceSnapshot price;
                if (magnumRequired <= 0 && ShowFutureMagnumUses &&
                    PriceByItem.TryGetValue(itemId, out price) && price.Required > 0)
                    magnumRequired = price.Required;

                if (magnumRequired > 0)
                    rows.Add(new DisplayRow((Ui("ui.magnum_required")) +
                        magnumRequired.ToString(CultureInfo.InvariantCulture), string.Empty, false));
            }

            // v1.5.18: the F2 reminder is a single fixed HUD badge. Do not inject a
            // second reminder into vanilla item tooltips; it adds visual noise and can move
            // with different tooltip layouts.
            return rows;
        }





        private static bool? CallBool(object instance, string methodName, string id)
        {
            if (instance == null || string.IsNullOrEmpty(id)) return null;
            try
            {
                Type type = instance.GetType();
                string cacheKey = type.AssemblyQualifiedName + "|" + methodName;
                MethodInfo method;
                if (!BoolMethodCache.TryGetValue(cacheKey, out method))
                {
                    MethodInfo[] methods = type.GetMethods(InstanceFlags);
                    for (int i = 0; i < methods.Length; i++)
                    {
                        MethodInfo candidate = methods[i];
                        if (!string.Equals(candidate.Name, methodName, StringComparison.Ordinal) || candidate.ReturnType != typeof(bool)) continue;
                        ParameterInfo[] p = candidate.GetParameters();
                        if (p.Length != 1) continue;
                        method = candidate;
                        break;
                    }
                    BoolMethodCache[cacheKey] = method;
                }

                if (method == null) return null;
                ParameterInfo[] parameters = method.GetParameters();
                object arg = ConvertArgument(id, parameters[0].ParameterType);
                if (arg == null && parameters[0].ParameterType.IsValueType) return null;
                return (bool)method.Invoke(instance, new object[] { arg });
            }
            catch { }
            return null;
        }

        private static object ConvertArgument(string value, Type targetType)
        {
            if (targetType == typeof(string) || targetType == typeof(object)) return value;
            try
            {
                if (targetType.IsEnum) return Enum.Parse(targetType, value, true);
                ConstructorInfo ctor = targetType.GetConstructor(new Type[] { typeof(string) });
                if (ctor != null) return ctor.Invoke(new object[] { value });
                return Convert.ChangeType(value, targetType, CultureInfo.InvariantCulture);
            }
            catch { return null; }
        }
















        private static void DeactivateQuickTooltipPools()
        {
            // Conservative ownership rule: never Destroy pooled rows from here because
            // their parent hierarchy belongs to vanilla. Deactivation releases active
            // layout state without risking a MissingReference regression.
            try
            {
                foreach (KeyValuePair<int, QuickTooltipPool> pair in QuickTooltipPools)
                {
                    QuickTooltipPool pool = pair.Value;
                    if (pool == null) continue;
                    pool.DeactivateFrom(0);
                    pool.Signature = string.Empty;
                }
            }
            catch { }
        }

        private static void PruneDeadQuickTooltipPools()
        {
            if (QuickTooltipPools.Count == 0) return;
            List<int> deadKeys = null;
            try
            {
                foreach (KeyValuePair<int, QuickTooltipPool> pair in QuickTooltipPools)
                {
                    QuickTooltipPool pool = pair.Value;
                    if (pool != null && pool.Tooltip != null) continue;
                    if (deadKeys == null) deadKeys = new List<int>();
                    deadKeys.Add(pair.Key);
                }
                if (deadKeys == null) return;
                for (int i = 0; i < deadKeys.Count; i++)
                    QuickTooltipPools.Remove(deadKeys[i]);
            }
            catch { }
        }

        private static void ResetSessionRuntimeReferencesForMenu()
        {
            // v1.7.36-test8: menu/session cleanup is now orchestration only. Feature
            // internals are released by their owners, while the shared service resolver
            // clears only its own discovery state.
            ResetTradeMenuSessionState();
            ResetRuntimeServiceResolverSessionState();
            ResetAmmoRuntimeSessionState();
            ResetFactionMenuSessionState();
            ResetMagnumRuntimeSessionState();
            ResetStarmapRuntimeSessionState();
            ResetBrowserMenuSessionState();
        }

        private static void SuppressVanillaGraphicRaycasters()
        {
            RestoreVanillaGraphicRaycasters();
            try
            {
                GraphicRaycaster[] raycasters = Resources.FindObjectsOfTypeAll<GraphicRaycaster>();
                if (raycasters == null) return;

                for (int i = 0; i < raycasters.Length; i++)
                {
                    GraphicRaycaster raycaster = raycasters[i];
                    if (raycaster == null || !raycaster.enabled) continue;
                    if (_inspectorGraphicRaycaster != null && object.ReferenceEquals(raycaster, _inspectorGraphicRaycaster))
                        continue;

                    Canvas canvas = raycaster.GetComponent<Canvas>();
                    if (_inspectorCanvas != null && canvas == _inspectorCanvas)
                        continue;

                    raycaster.enabled = false;
                    SuppressedRaycasters.Add(raycaster);
                }
            }
            catch { }
        }

        private static void RestoreVanillaGraphicRaycasters()
        {
            for (int i = 0; i < SuppressedRaycasters.Count; i++)
            {
                try
                {
                    GraphicRaycaster raycaster = SuppressedRaycasters[i];
                    if (raycaster != null) raycaster.enabled = true;
                }
                catch { }
            }
            SuppressedRaycasters.Clear();
        }





























        private static void HideSourceVanillaTooltip()
        {
            try
            {
                ItemTooltipHandler source = _lastItemPointerHandler as ItemTooltipHandler;
                if (source != null && !object.ReferenceEquals(source, _browserPreviewTooltipHandler))
                    source.OnPointerExit(null);
            }
            catch { }

            // Defensive cleanup for a tooltip created by a different vanilla slot handler.
            // Invoke the audited TooltipFactory.HideTooltip() only when an active instance
            // is already known; this is event-driven, never a per-frame scan.
            try
            {
                object factory = _activeTooltipFactory;
                if (factory != null)
                {
                    MethodInfo hide = factory.GetType().GetMethod(
                        "HideTooltip", InstanceFlags, null, Type.EmptyTypes, null);
                    if (hide != null) hide.Invoke(factory, null);
                }
            }
            catch { }
        }









        private static Component ResolveActiveVanillaItemTooltip()
        {
            object factory = _activeTooltipFactory;
            Type factoryType = AccessTools.TypeByName("MGSC.TooltipFactory");
            Component factoryComponent = factory as Component;

            // _activeTooltipFactory may point to a Component from an older UI scene.
            // Use Unity's Component null/liveness checks before trusting the cached object.
            if (factoryType == null || factoryComponent == null ||
                factoryComponent.gameObject == null || !factoryComponent.gameObject.activeInHierarchy ||
                !factoryType.IsInstanceOfType(factory))
            {
                factory = null;
                if (factoryType == null) return null;
                try
                {
                    UnityEngine.Object[] factories = Resources.FindObjectsOfTypeAll(factoryType);
                    for (int i = 0; i < factories.Length; i++)
                    {
                        object candidate = factories[i];
                        Component component = candidate as Component;
                        if (component == null || component.gameObject == null || !component.gameObject.activeInHierarchy)
                            continue;
                        factory = candidate;
                        _activeTooltipFactory = candidate;
                        break;
                    }
                }
                catch { return null; }
            }

            if (factory == null) return null;
            try
            {
                Component tooltip = GetMember(factory, "_tooltip") as Component;
                if (tooltip != null && tooltip.gameObject != null && tooltip.gameObject.activeInHierarchy)
                    return tooltip;
            }
            catch { }
            return null;
        }






























































        private static void EnsureQiiMarkerSprites()
        {
            if (_qiiUnlockedMarkerSprite == null)
                _qiiUnlockedMarkerSprite = CreateQiiMarkerSprite(1);
            if (_qiiLockedMarkerSprite == null)
                _qiiLockedMarkerSprite = CreateQiiMarkerSprite(-1);
            if (_qiiNoDatadiskSprite == null)
                _qiiNoDatadiskSprite = CreateQiiMarkerSprite(0);
        }

        private static Sprite CreateQiiMarkerSprite(int kind)
        {
            try
            {
                const int size = 16;
                Texture2D texture = new Texture2D(size, size, TextureFormat.ARGB32, false);
                texture.name = kind > 0 ? "QII_Check" : (kind < 0 ? "QII_Cross" : "QII_NoDatadisk");
                texture.filterMode = FilterMode.Point;
                texture.wrapMode = TextureWrapMode.Clamp;
                texture.hideFlags = HideFlags.HideAndDontSave;

                Color clear = new Color(0f, 0f, 0f, 0f);
                Color white = Color.white;
                Color[] pixels = new Color[size * size];
                for (int i = 0; i < pixels.Length; i++) pixels[i] = clear;
                texture.SetPixels(pixels);

                if (kind > 0)
                {
                    // Compact pixel check mark.
                    SetMarkerPixel(texture, 3, 8, white); SetMarkerPixel(texture, 4, 7, white);
                    SetMarkerPixel(texture, 5, 6, white); SetMarkerPixel(texture, 6, 5, white);
                    SetMarkerPixel(texture, 7, 6, white); SetMarkerPixel(texture, 8, 7, white);
                    SetMarkerPixel(texture, 9, 8, white); SetMarkerPixel(texture, 10, 9, white);
                    SetMarkerPixel(texture, 11, 10, white); SetMarkerPixel(texture, 12, 11, white);
                    SetMarkerPixel(texture, 4, 8, white); SetMarkerPixel(texture, 5, 7, white);
                    SetMarkerPixel(texture, 6, 6, white); SetMarkerPixel(texture, 10, 10, white);
                    SetMarkerPixel(texture, 11, 11, white);
                }
                else if (kind < 0)
                {
                    for (int p = 3; p <= 12; p++)
                    {
                        SetMarkerPixel(texture, p, p, white);
                        SetMarkerPixel(texture, p, 15 - p, white);
                        if (p < 12)
                        {
                            SetMarkerPixel(texture, p + 1, p, white);
                            SetMarkerPixel(texture, p + 1, 15 - p, white);
                        }
                    }
                }
                else
                {
                    // Universal "no chip" mark: a ring with a diagonal slash. The old
                    // microchip-with-dash was too easy to read as an unknown/disabled chip.
                    int cx = 7;
                    int cy = 7;
                    int r2Min = 20;
                    int r2Max = 34;
                    for (int y = 1; y < size - 1; y++)
                    {
                        for (int x = 1; x < size - 1; x++)
                        {
                            int dx = x - cx;
                            int dy = y - cy;
                            int r2 = dx * dx + dy * dy;
                            if (r2 >= r2Min && r2 <= r2Max)
                                SetMarkerPixel(texture, x, y, white);
                            if (Math.Abs((x + y) - 14) <= 1 && x >= 3 && x <= 12 && y >= 3 && y <= 12)
                                SetMarkerPixel(texture, x, y, white);
                        }
                    }
                }

                texture.Apply(false, false);
                Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 16f);
                sprite.name = texture.name;
                sprite.hideFlags = HideFlags.HideAndDontSave;
                return sprite;
            }
            catch
            {
                return null;
            }
        }

        private static void SetMarkerPixel(Texture2D texture, int x, int y, Color color)
        {
            if (texture == null || x < 0 || y < 0 || x >= texture.width || y >= texture.height) return;
            texture.SetPixel(x, y, color);
        }

















        private static TMP_Text _inspectorTitle;
        private static bool _inspectorPinnedTooltipOnRight = true;
        private static GameObject _inspectorDriverObject;
        private static bool _applicationQuitting;



        private static void ApplyBackgroundStyle(Image image)
        {
            if (image == null) return;
            image.color = new Color(0.012f, 0.030f, 0.026f, 0.975f);
        }

























        private static string BuildMagnumResearchRoute(MagnumUse use, bool ru)
        {
            if (use == null) return string.Empty;
            string moduleId = use.ModuleId;
            string departmentId = use.DepartmentId;
            string research = LocalizeMagnumPerk(use.PerkId);
            string module = NormalizeMagnumRouteNode(LocalizeMagnumNode(moduleId, true), moduleId, ru);
            string department = NormalizeMagnumRouteNode(LocalizeMagnumNode(departmentId, false), departmentId, ru);

            List<string> parts = new List<string>();
            if (!string.IsNullOrEmpty(module) && !string.Equals(module, moduleId, StringComparison.OrdinalIgnoreCase)) parts.Add(module);
            else if (!string.IsNullOrEmpty(moduleId)) parts.Add(LocalizeMagnumNode(moduleId, true));
            if (!string.IsNullOrEmpty(department) && !string.Equals(department, module, StringComparison.OrdinalIgnoreCase)) parts.Add(department);
            if (!string.IsNullOrEmpty(research)) parts.Add(research);
            return parts.Count > 0 ? string.Join("  ›  ", parts.ToArray()) : use.PerkId;
        }

        private static string NormalizeMagnumRouteNode(string value, string id, bool ru)
        {
            string raw = (id ?? string.Empty).Trim().ToLowerInvariant();
            string display = NormalizeGameText(value);
            string displayLower = display.ToLowerInvariant();

            // The structural Cloning department is named simply "Cloning" in the
            // vanilla Magnum UI. Generic UI localization can resolve its state label
            // as "Cloning in progress", which is not a department name.
            if (raw.IndexOf("cloning", StringComparison.Ordinal) >= 0 ||
                displayLower.IndexOf("cloning in progress", StringComparison.Ordinal) >= 0 ||
                displayLower.IndexOf("клонирован", StringComparison.Ordinal) >= 0)
                return Ui("ui.cloning");

            return display;
        }

        private static string LocalizeMagnumNode(string id, bool module)
        {
            if (string.IsNullOrEmpty(id)) return string.Empty;

            string prefix = module ? "mgmodule." : "mgdepartment.";
            string alt = module ? "magnummodule." : "magnumdepartment.";
            string value = LocalizeCandidates(new string[]
            {
                prefix + id + ".name",
                alt + id + ".name",
                "mgperk." + id + ".name",
                "magnumperk." + id + ".name",
                id
            }, id);

            value = NormalizeGameText(value);

            if (!string.IsNullOrEmpty(value) &&
                !string.Equals(value, id, StringComparison.OrdinalIgnoreCase))
                return value;

            // Some Magnum structural nodes are stored as raw IDs, but vanilla exposes
            // their player-facing name under generic UI keys rather than mgmodule.*.
            string uiValue = LocalizeCandidates(new string[]
            {
                "ui.label." + id,
                "ui.magnum." + id,
                "ui.magnum.module." + id,
                "ui.magnum.department." + id
            }, id);

            uiValue = NormalizeGameText(uiValue);

            if (!string.IsNullOrEmpty(uiValue) &&
                !string.Equals(uiValue, id, StringComparison.OrdinalIgnoreCase))
                return uiValue;

            return LocalizeMagnumNodeFallback(id, module, IsRussian());
        }

        private static string LocalizeMagnumNodeFallback(string id, bool module, bool ru)
        {
            if (string.IsNullOrEmpty(id)) return string.Empty;
            string key = id.Trim().ToLowerInvariant();
            string uiKey = string.Empty;
            if (module)
            {
                if (key == "navigation") uiKey = "magnum.module.navigation";
                else if (key == "research") uiKey = "magnum.module.research";
                else if (key == "hangar") uiKey = "magnum.module.hangar";
                else if (key == "supply" || key == "supplies") uiKey = "magnum.module.supply";
                else if (key == "cloning" || key == "clone") uiKey = "magnum.module.cloning";
                else if (key == "science") uiKey = "magnum.module.science";
                else if (key == "medical" || key == "medicine") uiKey = "magnum.module.medical";
                else if (key == "engineering" || key == "engineer") uiKey = "magnum.module.engineering";
                else if (key == "industry" || key == "production") uiKey = "magnum.module.industry";
                else if (key == "security") uiKey = "magnum.module.security";
                else if (key == "logistics") uiKey = "magnum.module.logistics";
                else if (key == "communications" || key == "communication") uiKey = "magnum.module.communications";
                else if (key == "cargo" || key == "storage") uiKey = "magnum.module.storage";
                else if (key == "command") uiKey = "magnum.module.command";
                else if (key == "crew") uiKey = "magnum.module.crew";
                else if (key == "armory" || key == "arsenal") uiKey = "magnum.module.armory";
                else if (key == "workshop") uiKey = "magnum.module.workshop";
                else if (key == "training") uiKey = "magnum.module.training";
                else if (key == "habitation" || key == "living") uiKey = "magnum.module.habitation";
            }
            else
            {
                if (key == "scanner") uiKey = "magnum.node.scanner";
                else if (key == "travel" || key == "travels") uiKey = "magnum.node.travel";
                else if (key == "shuttle") uiKey = "magnum.node.shuttle";
                else if (key == "capsule") uiKey = "magnum.node.capsule";
                else if (key == "trade") uiKey = "magnum.node.trade";
            }
            return string.IsNullOrEmpty(uiKey) ? HumanizeIdentifier(id) : Ui(uiKey);
        }

        private static string HumanizeIdentifier(string id)
        {
            if (string.IsNullOrEmpty(id)) return string.Empty;

            string value = id.Replace("_", " ").Replace("-", " ").Trim();
            if (string.IsNullOrEmpty(value)) return string.Empty;

            try
            {
                return CultureInfo.InvariantCulture.TextInfo.ToTitleCase(value.ToLowerInvariant());
            }
            catch
            {
                return value;
            }
        }

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
                        QueueTest3RowsRefresh(); // QII1739T3_MAGNUM_REFRESH_PRODUCTION
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









































        private static void AddUniqueTextValue(List<string> values, string value)
        {
            if (values == null || string.IsNullOrWhiteSpace(value)) return;
            string normalized = NormalizeGameText(value).Trim();
            if (string.IsNullOrEmpty(normalized)) return;
            for (int i = 0; i < values.Count; i++)
            {
                if (string.Equals(values[i], normalized, StringComparison.OrdinalIgnoreCase))
                    return;
            }
            values.Add(normalized);
        }

        private static void AddExactRussianLocalizationValue(List<string> values, string key)
        {
            if (values == null || string.IsNullOrEmpty(key)) return;
            string localized = InvokeLocalizationRaw(key);
            if (!string.IsNullOrEmpty(localized) && ContainsCyrillic(localized))
                AddUniqueTextValue(values, localized);
        }

        private static string JoinExactRussianMobNames(List<string> values)
        {
            if (values == null || values.Count == 0) return string.Empty;
            if (values.Count == 1) return values[0];
            return string.Join(" / ", values.ToArray());
        }

        private static MethodInfo GetExactMobNameResolverMethod()
        {
            if (_exactMobNameResolverMethodSearched) return _exactMobNameResolverMethod;
            _exactMobNameResolverMethodSearched = true;
            try
            {
                Type owner = AccessTools.TypeByName("MGSC.CreatureSystem");
                if (owner == null) return null;
                MethodInfo[] methods = owner.GetMethods(StaticFlags);
                for (int i = 0; i < methods.Length; i++)
                {
                    MethodInfo method = methods[i];
                    if (method == null || !method.IsStatic ||
                        !string.Equals(method.Name, "GetValidMobNameLocTag", StringComparison.Ordinal))
                        continue;
                    ParameterInfo[] ps = method.GetParameters();
                    if (ps == null || ps.Length != 2 ||
                        ps[0].ParameterType != typeof(string) ||
                        ps[1].ParameterType != typeof(string) ||
                        method.ReturnType != typeof(string))
                        continue;
                    _exactMobNameResolverMethod = method;
                    break;
                }
            }
            catch { _exactMobNameResolverMethod = null; }
            return _exactMobNameResolverMethod;
        }



        private static string ResolveRussianMobNameViaVanillaResolver(string mobClassId, object mobRecord)
        {
            MethodInfo resolver = GetExactMobNameResolverMethod();
            if (resolver == null || string.IsNullOrEmpty(mobClassId)) return string.Empty;

            List<string> factionIds = GetRelevantMobFactionIds(mobClassId, mobRecord);
            if (factionIds.Count == 0) return string.Empty;

            List<string> values = new List<string>();
            for (int i = 0; i < factionIds.Count; i++)
            {
                string factionId = factionIds[i];
                if (string.IsNullOrEmpty(factionId)) continue;
                try
                {
                    string locTag = resolver.Invoke(null, new object[] { mobClassId, factionId }) as string;
                    if (string.IsNullOrEmpty(locTag)) continue;
                    string localized = InvokeLocalizationRaw(locTag);
                    if (string.IsNullOrEmpty(localized) && ContainsCyrillic(locTag))
                        localized = locTag;
                    if (!string.IsNullOrEmpty(localized) && ContainsCyrillic(localized))
                        AddUniqueTextValue(values, localized);
                }
                catch { }
            }
            return JoinExactRussianMobNames(values);
        }



        private static string GetMobGenderFromActor(string actorId, string bodyTypeId)
        {
            string joined = (actorId ?? string.Empty) + " " + (bodyTypeId ?? string.Empty);
            if (joined.IndexOf("female", StringComparison.OrdinalIgnoreCase) >= 0 ||
                joined.IndexOf("woman", StringComparison.OrdinalIgnoreCase) >= 0)
                return "female";
            if (joined.IndexOf("male", StringComparison.OrdinalIgnoreCase) >= 0 ||
                joined.IndexOf("man", StringComparison.OrdinalIgnoreCase) >= 0)
                return "male";
            return string.Empty;
        }

        private static string ResolveExactRussianMobName(string mobClassId)
        {
            if (!IsRussian() || string.IsNullOrEmpty(mobClassId)) return string.Empty;

            string cacheKey = (_localizationCacheLanguage ?? string.Empty) + "|exact-russian-mob|" + mobClassId;
            string cached;
            if (LootDisplayNameCache.TryGetValue(cacheKey, out cached)) return cached;

            object mobRecord = FindLootDataRecord("MobClasses", mobClassId);
            List<string> bodyTypes = mobRecord == null
                ? new List<string>()
                : ExtractStringIds(GetMember(mobRecord, "BodyTypes"));
            if (bodyTypes.Count == 0) bodyTypes.Add(string.Empty);

            // Highest-confidence path: exact localization keys for this MobClass.
            // Quasimorph uses gender-specific localization keys
            // for many human enemies (monster.<mob>_male/female.name).
            List<string> exactMobValues = new List<string>();
            AddExactRussianLocalizationValue(exactMobValues, "monster." + mobClassId + ".name");
            for (int i = 0; i < bodyTypes.Count; i++)
            {
                string bodyTypeId = bodyTypes[i] ?? string.Empty;
                object bodyRecord = string.IsNullOrEmpty(bodyTypeId)
                    ? null
                    : FindLootDataRecord("BodyTypes", bodyTypeId);
                string actorId = bodyRecord == null ? string.Empty : FirstNonEmpty(
                    GetStringMember(bodyRecord, "ActorId"),
                    GetStringMember(bodyRecord, "ActorID"));
                string gender = GetMobGenderFromActor(actorId, bodyTypeId);
                if (!string.IsNullOrEmpty(gender))
                {
                    AddExactRussianLocalizationValue(exactMobValues,
                        "monster." + mobClassId + "_" + gender + ".name");
                    AddExactRussianLocalizationValue(exactMobValues,
                        "monster." + mobClassId + "." + gender + ".name");
                }
            }
            if (exactMobValues.Count > 0)
            {
                string exact = JoinExactRussianMobNames(exactMobValues);
                LootDisplayNameCache[cacheKey] = exact;
                return exact;
            }

            // Some classes do not own a direct localization key. Quasimorph itself
            // resolves those through CreatureSystem.GetValidMobNameLocTag(mob, faction).
            // Use only factions in which this MobClass is actually present in the unit
            // tables/default faction, preventing unrelated faction-name variants.
            string resolved = ResolveRussianMobNameViaVanillaResolver(mobClassId, mobRecord);
            if (!string.IsNullOrEmpty(resolved))
            {
                LootDisplayNameCache[cacheKey] = resolved;
                return resolved;
            }

            // Final exact fallback: a localized BodyType/Actor name. This is useful for
            // classes whose visible vanilla name is inherited from the spawned body.
            List<string> bodyValues = new List<string>();
            for (int i = 0; i < bodyTypes.Count; i++)
            {
                string bodyTypeId = bodyTypes[i] ?? string.Empty;
                object bodyRecord = string.IsNullOrEmpty(bodyTypeId)
                    ? null
                    : FindLootDataRecord("BodyTypes", bodyTypeId);
                string actorId = bodyRecord == null ? string.Empty : FirstNonEmpty(
                    GetStringMember(bodyRecord, "ActorId"),
                    GetStringMember(bodyRecord, "ActorID"));
                string gender = GetMobGenderFromActor(actorId, bodyTypeId);
                if (!string.IsNullOrEmpty(bodyTypeId))
                {
                    AddExactRussianLocalizationValue(bodyValues, "monster." + bodyTypeId + ".name");
                    if (!string.IsNullOrEmpty(gender))
                        AddExactRussianLocalizationValue(bodyValues,
                            "monster." + bodyTypeId + "_" + gender + ".name");
                }
                if (!string.IsNullOrEmpty(actorId))
                {
                    AddExactRussianLocalizationValue(bodyValues, "monster." + actorId + ".name");
                    if (!string.IsNullOrEmpty(gender))
                        AddExactRussianLocalizationValue(bodyValues,
                            "monster." + actorId + "_" + gender + ".name");
                }
            }

            string fallback = JoinExactRussianMobNames(bodyValues);
            LootDisplayNameCache[cacheKey] = fallback;
            return fallback;
        }









        private static bool LooksLikeGuid(string value)
        {
            if (string.IsNullOrEmpty(value) || value.Length < 32) return false;
            Guid parsed;
            return Guid.TryParse(value, out parsed);
        }

        private static string ShortStableId(string value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            string compact = value.Replace("-", string.Empty);
            return compact.Length <= 8 ? compact : compact.Substring(0, 8);
        }



















        private static int GetSafeMagnumRequired(string itemId)
        {
            PriceSnapshot price;
            if (PriceByItem.TryGetValue(itemId, out price) && price.Required > 0)
                return price.Required;

            List<MagnumUse> uses;
            if (!MagnumUses.TryGetValue(itemId, out uses) || uses == null) return 0;
            int total = 0;
            for (int i = 0; i < uses.Count; i++)
                if (uses[i] != null) total += Math.Max(0, uses[i].Quantity);
            return total;
        }

        private static int GetUniqueRecipeOutputCount(string itemId)
        {
            string relationId = ResolveStaticRelationItemId(itemId);
            List<RecipeUse> uses;
            if (!UsedInRecipes.TryGetValue(relationId, out uses) || uses == null) return 0;
            HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < uses.Count; i++)
            {
                RecipeUse use = uses[i];
                if (use != null && !string.IsNullOrEmpty(use.OutputItemId))
                    seen.Add(use.OutputItemId);
            }
            return seen.Count;
        }



        private static void TryResolveMagnumProgressionLightweight()
        {
            if (_magnumProgression != null)
            {
                BuildRuntimeMagnumIndexFromProgression();
                return;
            }
            if (_magnumLightLookupAttempted) return;
            _magnumLightLookupAttempted = true;
            try
            {
                Type target = AccessTools.TypeByName("MGSC.MagnumProgression");
                if (target != null)
                    _magnumProgression = ResolveStateModule(target);

                if (_magnumProgression == null)
                {
                    Type developmentType = AccessTools.TypeByName("MGSC.MagnumDevelopmentSystem");
                    object development = developmentType == null ? null : ResolveStateModule(developmentType);
                    if (development != null && target != null)
                        _magnumProgression = FindTargetInModuleContainer(development, target, 3, new HashSet<object>(ReferenceComparer.Instance));
                }

                if (_magnumProgression == null)
                {
                    object[] roots = new object[] { _modContext == null ? null : (object)_modContext.State, _activeTooltipFactory, _activeTooltip };
                    for (int i = 0; i < roots.Length && _magnumProgression == null; i++)
                    {
                        object root = roots[i];
                        if (root == null) continue;
                        if (target != null)
                            _magnumProgression = FindNestedRuntimeObject(root, target, 4, new HashSet<object>(ReferenceComparer.Instance));
                        if (_magnumProgression == null)
                            _magnumProgression = FindNestedObjectByTypeName(root, "MagnumProgression", 4, new HashSet<object>(ReferenceComparer.Instance));
                    }
                }

                BuildRuntimeMagnumIndexFromProgression();
            }
            catch { }
        }

        private static void BuildRuntimeMagnumIndexFromProgression()
        {
            if (_runtimeMagnumIndexBuilt || _magnumProgression == null) return;
            _runtimeMagnumIndexBuilt = true;
            try
            {
                HashSet<object> visited = new HashSet<object>(ReferenceComparer.Instance);
                int budget = 1800;
                int skippedNodes = 0;
                ScanRuntimeMagnumNodeSafe(
                    _magnumProgression, visited, 7, ref budget, ref skippedNodes);
                Debug.Log("[ItemIntelligence] Runtime Magnum relation pass complete. Indexed items=" +
                    MagnumUses.Count + ", skippedNodes=" + skippedNodes + ".");
                    QueueTest3RowsRefresh(); // QII1739T3_MAGNUM_REFRESH_RELATIONS
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[ItemIntelligence] Runtime Magnum relation pass failed: " +
                    ex.GetType().Name + ": " +
                    (string.IsNullOrEmpty(ex.Message) ? "<no message>" : ex.Message));
            }
        }

        private static void ScanRuntimeMagnumNodeSafe(
            object value, HashSet<object> visited, int depth,
            ref int budget, ref int skippedNodes)
        {
            try
            {
                ScanRuntimeMagnumNode(
                    value, visited, depth, ref budget, ref skippedNodes);
            }
            catch (Exception)
            {
                // Runtime state collections can invalidate their enumerators while a
                // save is loading. Skip only that volatile branch; the static Magnum
                // index and every other readable branch remain available.
                skippedNodes++;
            }
        }

        private static void ScanRuntimeMagnumNode(
            object value, HashSet<object> visited, int depth,
            ref int budget, ref int skippedNodes)
        {
            if (value == null || depth < 0 || budget <= 0 || IsSimple(value.GetType())) return;
            if (visited.Contains(value)) return;
            visited.Add(value);
            budget--;

            IDictionary dict = value as IDictionary;
            if (dict != null)
            {
                foreach (DictionaryEntry entry in dict)
                {
                    ScanRuntimeMagnumNodeSafe(
                        entry.Key, visited, depth - 1, ref budget, ref skippedNodes);
                    ScanRuntimeMagnumNodeSafe(
                        entry.Value, visited, depth - 1, ref budget, ref skippedNodes);
                    if (budget <= 0) return;
                }
                return;
            }

            IEnumerable enumerable = value as IEnumerable;
            if (enumerable != null && !(value is string))
            {
                int count = 0;
                foreach (object entry in enumerable)
                {
                    if (++count > 512 || budget <= 0) break;
                    ScanRuntimeMagnumNodeSafe(
                        entry, visited, depth - 1, ref budget, ref skippedNodes);
                }
                return;
            }

            Type type = value.GetType();
            string ns = type.Namespace ?? string.Empty;
            if (!ns.StartsWith("MGSC", StringComparison.Ordinal)) return;

            string projectId = FirstNonEmpty(
                GetStringMember(value, "PerkId"),
                GetStringMember(value, "ProjectId"),
                GetStringMember(value, "Id"));

            List<MemberInfo> members = GetReadableMembers(type);
            if (!string.IsNullOrEmpty(projectId))
            {
                for (int i = 0; i < members.Count; i++)
                {
                    MemberInfo member = members[i];
                    string name = member.Name ?? string.Empty;
                    bool costMember = name.IndexOf("Price", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                      name.IndexOf("Cost", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                      name.IndexOf("Required", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                      name.IndexOf("Resource", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                      name.IndexOf("Upgrade", StringComparison.OrdinalIgnoreCase) >= 0;
                    if (!costMember) continue;
                    Dictionary<string, int> quantities;
                    try
                    {
                        quantities = ExtractItemQuantities(GetMemberValue(value, member));
                    }
                    catch (Exception)
                    {
                        skippedNodes++;
                        continue;
                    }
                    foreach (KeyValuePair<string, int> pair in quantities)
                        AddMagnumUseUnique(pair.Key, new MagnumUse(projectId, pair.Value, value));
                }
            }

            if (depth == 0) return;
            for (int i = 0; i < members.Count && budget > 0; i++)
            {
                object child = GetMemberValue(value, members[i]);
                if (child == null || object.ReferenceEquals(child, value)) continue;
                ScanRuntimeMagnumNodeSafe(
                    child, visited, depth - 1, ref budget, ref skippedNodes);
            }
        }





        private static string GetRecipeAvailabilityLabel(RecipeDef recipe, bool ru)
        {
            if (recipe == null || recipe.RequiredPerks == null || recipe.RequiredPerks.Count == 0)
                return string.Empty;
            if (_magnumProgression == null)
                return string.Empty;

            bool unknown = false;
            for (int i = 0; i < recipe.RequiredPerks.Count; i++)
            {
                bool? purchased = CallBool(_magnumProgression, "IsPerkPurchased", recipe.RequiredPerks[i]);
                if (!purchased.HasValue) unknown = true;
                else if (!purchased.Value) return Ui("ui.locked_2");
            }
            if (unknown) return string.Empty;
            return Ui("ui.available_3");
        }

























        private static List<object> GetRuntimeStationsLightweight()
        {
            List<object> result = new List<object>();
            Type stationType = AccessTools.TypeByName("MGSC.Station");
            Type stationRecordType = AccessTools.TypeByName("MGSC.StationRecord");
            if (stationType == null && stationRecordType == null) return result;

            object[] roots = new object[] { _stationsState, _stationSystem };
            HashSet<object> seen = new HashSet<object>(ReferenceComparer.Instance);
            for (int r = 0; r < roots.Length; r++)
            {
                object root = roots[r];
                if (root == null) continue;
                CollectStationObjects(root, stationType, stationRecordType, result, seen, 6);
            }

            if (!_stationSchemaLogged && result.Count > 0)
            {
                _stationSchemaLogged = true;
                Debug.Log("[ItemIntelligence] Runtime stations resolved: count=" + result.Count +
                    ", firstType=" + result[0].GetType().FullName + ".");
            }
            return result;
        }

        private static void CollectStationObjects(object value, Type stationType, Type stationRecordType, List<object> result, HashSet<object> seen, int depth)
        {
            if (value == null || result == null || depth < 0) return;
            Type valueType = value.GetType();
            bool isStation = stationType != null && stationType.IsInstanceOfType(value);
            bool isStationRecord = stationRecordType != null && stationRecordType.IsInstanceOfType(value);
            string typeName = valueType.Name ?? string.Empty;
            if (!isStation && !isStationRecord && typeName.EndsWith("StationRecord", StringComparison.OrdinalIgnoreCase)) isStationRecord = true;
            if (isStation || isStationRecord)
            {
                if (!seen.Contains(value)) { seen.Add(value); result.Add(value); }
                return;
            }
            if (seen.Contains(value)) return;
            seen.Add(value);
            if (seen.Count > 4096) return;

            IDictionary dict = value as IDictionary;
            if (dict != null)
            {
                foreach (DictionaryEntry entry in dict)
                {
                    CollectStationObjects(entry.Value, stationType, stationRecordType, result, seen, depth - 1);
                    if (result.Count > 512) return;
                }
                return;
            }
            IEnumerable enumerable = value as IEnumerable;
            if (enumerable != null && !(value is string))
            {
                int count = 0;
                foreach (object entry in enumerable)
                {
                    if (++count > 1024) break;
                    CollectStationObjects(entry, stationType, stationRecordType, result, seen, depth - 1);
                    if (result.Count > 512) return;
                }
                return;
            }
            if (depth == 0 || IsSimple(valueType)) return;
            string ns = valueType.Namespace ?? string.Empty;
            if (!ns.StartsWith("MGSC", StringComparison.Ordinal)) return;

            List<MemberInfo> members = GetReadableMembers(valueType);
            for (int i = 0; i < members.Count; i++)
            {
                object child = GetMemberValue(value, members[i]);
                if (child == null || object.ReferenceEquals(child, value)) continue;
                CollectStationObjects(child, stationType, stationRecordType, result, seen, depth - 1);
                if (result.Count > 512) return;
            }
        }



        private static List<object> GetRuntimeStations()
        {
            return GetRuntimeStationsLightweight();
        }

        private static object ResolveRuntimeObjectByTypeName(string fullTypeName)
        {
            Type target = AccessTools.TypeByName(fullTypeName);
            if (target == null) return null;

            object nested = FindNestedObject(_activeTooltipFactory, target, 5, new HashSet<object>(ReferenceComparer.Instance));
            if (nested != null) return nested;

            Type[] types;
            try { types = typeof(Data).Assembly.GetTypes(); }
            catch (ReflectionTypeLoadException ex) { types = ex.Types; }
            if (types == null) return null;

            for (int i = 0; i < types.Length; i++)
            {
                Type owner = types[i];
                if (owner == null) continue;

                FieldInfo[] fields;
                try { fields = owner.GetFields(StaticFlags); } catch { fields = new FieldInfo[0]; }
                for (int f = 0; f < fields.Length; f++)
                {
                    if (!target.IsAssignableFrom(fields[f].FieldType)) continue;
                    try
                    {
                        object value = fields[f].GetValue(null);
                        if (value != null) return value;
                    }
                    catch { }
                }

                PropertyInfo[] props;
                try { props = owner.GetProperties(StaticFlags); } catch { props = new PropertyInfo[0]; }
                for (int q = 0; q < props.Length; q++)
                {
                    PropertyInfo prop = props[q];
                    if (!prop.CanRead || prop.GetIndexParameters().Length != 0 || !target.IsAssignableFrom(prop.PropertyType)) continue;
                    try
                    {
                        object value = prop.GetValue(null, null);
                        if (value != null) return value;
                    }
                    catch { }
                }
            }
            return null;
        }

        private static int GetContainerItemCount(object container, string itemId)
        {
            if (container == null || string.IsNullOrEmpty(itemId)) return 0;
            try
            {
                Type itemStorageType = AccessTools.TypeByName("MGSC.ItemStorage");
                if (itemStorageType == null || !itemStorageType.IsInstanceOfType(container)) return 0;
                MethodInfo countItems = itemStorageType.GetMethod(
                    "CountItems", InstanceFlags, null, new Type[] { typeof(string) }, null);
                if (countItems == null) return 0;
                object raw = countItems.Invoke(container, new object[] { itemId });
                int count;
                return TryToInt(raw, out count) && count > 0 ? count : 0;
            }
            catch (Exception ex)
            {
                LogRuntimeBoundaryWarningOnce(
                    "trade.internalstorage.countitems",
                    "Exact ItemStorage.CountItems(string) could not be read; BUY AT STATIONS fails closed.",
                    ex);
                return 0;
            }
        }

        private static bool TryGetExactStationPrice(object station, string itemId, bool stationBuys, out int price)
        {
            price = 0;
            EnsureTradeStateDependencies();

            // Quasimorph exposes the authoritative economy calculation through
            // TradeSystem.GetItemBuyPrice/GetItemSellPrice. Calling it avoids guessing a
            // formula and automatically respects faction reputation and Magnum bonuses.
            try
            {
                Type tradeType = AccessTools.TypeByName("MGSC.TradeSystem");
                Type stationType = AccessTools.TypeByName("MGSC.Station");
                Type factionType = AccessTools.TypeByName("MGSC.Faction");
                Type pricesType = AccessTools.TypeByName("MGSC.ItemsPrices");
                Type progressionType = AccessTools.TypeByName("MGSC.MagnumProgression");
                if (tradeType != null && station != null && stationType != null && stationType.IsInstanceOfType(station) &&
                    _itemsPrices != null && pricesType != null && pricesType.IsInstanceOfType(_itemsPrices))
                {
                    object faction = ResolveStationFaction(station);
                    if (faction != null && factionType != null && factionType.IsInstanceOfType(faction))
                    {
                        string methodName = stationBuys ? "GetItemSellPrice" : "GetItemBuyPrice";
                        MethodInfo[] methods = tradeType.GetMethods(StaticFlags);
                        for (int i = 0; i < methods.Length; i++)
                        {
                            MethodInfo method = methods[i];
                            if (!string.Equals(method.Name, methodName, StringComparison.Ordinal)) continue;
                            ParameterInfo[] p = method.GetParameters();
                            if (p.Length != 6) continue;
                            if (progressionType != null && p[0].ParameterType != progressionType) continue;
                            if (p[1].ParameterType != factionType || p[2].ParameterType != stationType || p[3].ParameterType != pricesType || p[4].ParameterType != typeof(string) || p[5].ParameterType != typeof(bool)) continue;
                            object raw = method.Invoke(null, new object[] { _magnumProgression, faction, station, _itemsPrices, itemId, false });
                            int parsed;
                            if (TryExtractPriceValue(raw, out parsed) && parsed >= 0)
                            {
                                price = parsed;
                                return true;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[ItemIntelligence] Exact TradeSystem price failed for " + itemId + ": " + ex.Message);
            }

            // Exact current-game contract was unavailable: do not infer a price from
            // similarly named APIs or generic price records. Unknown stays unknown.
            return false;
        }













        private static bool TryToDoubleSafe(object value, out double result)
        {
            result = 0.0;
            if (value == null) return false;

            try
            {
                if (value is double) { result = (double)value; return true; }
                if (value is float) { result = (double)(float)value; return true; }
                if (value is decimal) { result = (double)(decimal)value; return true; }
                if (value is int) { result = (double)(int)value; return true; }
                if (value is long) { result = (double)(long)value; return true; }
                if (value is short) { result = (double)(short)value; return true; }

                return double.TryParse(
                    ConvertToStableString(value),
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out result);
            }
            catch
            {
                return false;
            }
        }









        private static bool TryExtractPriceValue(object raw, out int price)
        {
            price = 0;
            return raw != null && TryToInt(raw, out price);
        }

        private static object GetItemRecord(string itemId)
        {
            if (string.IsNullOrEmpty(itemId)) return null;
            object record;
            if (ItemRecordsById.TryGetValue(itemId, out record) && record != null) return record;
            return null;
        }

        private static string BuildStationLocationLabel(string spaceObjectId)
        {
            if (string.IsNullOrEmpty(spaceObjectId)) return string.Empty;
            object record;
            if (!SpaceObjectRecordsById.TryGetValue(spaceObjectId, out record) || record == null)
            {
                BuildSpaceObjectIndex();
                SpaceObjectRecordsById.TryGetValue(spaceObjectId, out record);
            }
            if (record == null) return LocalizeSpaceObject(spaceObjectId);

            string own = LocalizeSpaceObject(spaceObjectId);
            string parentId = GetStringMember(record, "ParentId");
            string type = ConvertToStableString(GetMember(record, "SpaceObjectType"));
            bool satellite = type.IndexOf("Satel", StringComparison.OrdinalIgnoreCase) >= 0 ||
                             type.IndexOf("Moon", StringComparison.OrdinalIgnoreCase) >= 0;
            if (satellite && !string.IsNullOrEmpty(parentId))
            {
                string parent = LocalizeSpaceObject(parentId);
                if (!string.IsNullOrEmpty(parent) && !string.Equals(parent, own, StringComparison.OrdinalIgnoreCase))
                    return parent + " / " + own;
            }
            return own;
        }

        private static string LocalizeSpaceObject(string id)
        {
            if (string.IsNullOrEmpty(id)) return string.Empty;
            return LocalizeCandidates(new string[]
            {
                "spaceobject." + id + ".name",
                "mapobject." + id + ".name",
                "planet." + id + ".name",
                id
            }, id);
        }


        private static string LocalizeStation(string stationId)
        {
            if (string.IsNullOrEmpty(stationId)) return string.Empty;
            string[] keys = new string[] { "station." + stationId + ".name", "mapobject." + stationId + ".name", stationId };
            return LocalizeCandidates(keys, stationId);
        }

        private static string LocalizeGenericId(string id)
        {
            if (string.IsNullOrEmpty(id)) return string.Empty;
            string localized = LocalizeCandidates(new string[]
            {
                "item." + id + ".name",
                "workbench." + id + ".name",
                "mgperk." + id + ".name",
                id
            }, id);
            return localized;
        }

        private static bool HasNonMagnumUse(string itemId)
        {
            return GetStaticRelationListCount(UsedInRecipes, itemId) > 0 ||
                   GetListCount(BarterConsumers, itemId) > 0;
        }



















        private static Component FindPropertiesTooltip(object root)
        {
            Type target = AccessTools.TypeByName("MGSC.PropertiesTooltip");
            if (target == null) return null;

            Component component = root as Component;
            if (component != null)
            {
                if (target.IsInstanceOfType(component))
                {
                    _activeTooltip = component;
                    return component;
                }
                try
                {
                    Component parent = component.GetComponentInParent(target) as Component;
                    if (parent != null)
                    {
                        _activeTooltip = parent;
                        return parent;
                    }
                }
                catch { }
            }

            // SetPriceBlock normally gives us the exact active PropertiesTooltip. Prefer
            // that O(1) reference over a recursive factory walk on every hover.
            if (_activeTooltip != null && _activeTooltip.gameObject != null && _activeTooltip.gameObject.activeInHierarchy)
                return _activeTooltip;

            object found = FindNestedObject(root, target, 2, new HashSet<object>(ReferenceComparer.Instance));
            if (found is Component)
            {
                _activeTooltip = (Component)found;
                return _activeTooltip;
            }

            // Last-resort compatibility path. It should run only once until an active
            // tooltip reference is captured, never for every station/conveyor hover.
            UnityEngine.Object[] all = Resources.FindObjectsOfTypeAll(target);
            Component best = null;
            int bestScore = int.MinValue;
            for (int i = 0; i < all.Length; i++)
            {
                Component c = all[i] as Component;
                if (c == null || c.gameObject == null || !c.gameObject.activeInHierarchy) continue;
                int score = c.transform.GetSiblingIndex();
                string n = c.gameObject.name ?? string.Empty;
                if (n.IndexOf("item", StringComparison.OrdinalIgnoreCase) >= 0) score += 1000;
                if (n.IndexOf("tooltip", StringComparison.OrdinalIgnoreCase) >= 0) score += 500;
                if (score > bestScore) { best = c; bestScore = score; }
            }
            if (best != null) _activeTooltip = best;
            return best;
        }

        private static object FindNestedObject(object root, Type target, int depth, HashSet<object> visited)
        {
            if (root == null || target == null) return null;
            if (target.IsInstanceOfType(root)) return root;
            if (depth <= 0 || IsSimple(root.GetType()) || visited.Contains(root)) return null;
            visited.Add(root);
            FieldInfo[] fields;
            try { fields = root.GetType().GetFields(InstanceFlags); } catch { return null; }
            for (int i = 0; i < fields.Length; i++)
            {
                object value;
                try { value = fields[i].GetValue(root); } catch { continue; }
                if (value == null) continue;
                if (target.IsInstanceOfType(value)) return value;
                Type t = value.GetType();
                if (ShouldTraverse(t))
                {
                    object nested = FindNestedObject(value, target, depth - 1, visited);
                    if (nested != null) return nested;
                }
            }
            return null;
        }


        private static void EnsureMagnumProgressionResolved(object root)
        {
            if (_magnumProgression != null) return;
            try
            {
                _magnumProgression = FindNestedObjectByTypeName(root, "MagnumProgression", 4, new HashSet<object>(ReferenceComparer.Instance));
                if (_magnumProgression != null) return;

                Type target = AccessTools.TypeByName("MGSC.MagnumProgression");
                if (target == null) return;
                Type[] types;
                try { types = typeof(Data).Assembly.GetTypes(); }
                catch (ReflectionTypeLoadException ex) { types = ex.Types; }
                if (types == null) return;

                for (int i = 0; i < types.Length; i++)
                {
                    Type t = types[i];
                    if (t == null) continue;
                    FieldInfo[] fields;
                    try { fields = t.GetFields(StaticFlags); } catch { fields = new FieldInfo[0]; }
                    for (int f = 0; f < fields.Length; f++)
                    {
                        if (!target.IsAssignableFrom(fields[f].FieldType)) continue;
                        try
                        {
                            object value = fields[f].GetValue(null);
                            if (value != null) { _magnumProgression = value; return; }
                        }
                        catch { }
                    }
                    PropertyInfo[] props;
                    try { props = t.GetProperties(StaticFlags); } catch { props = new PropertyInfo[0]; }
                    for (int q = 0; q < props.Length; q++)
                    {
                        if (props[q].GetIndexParameters().Length != 0 || !target.IsAssignableFrom(props[q].PropertyType)) continue;
                        try
                        {
                            object value = props[q].GetValue(null, null);
                            if (value != null) { _magnumProgression = value; return; }
                        }
                        catch { }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[ItemIntelligence] MagnumProgression lookup failed; vanilla total requirement will be used when available: " + ex.Message);
            }
        }

        private static object FindNestedObjectByTypeName(object root, string typeNamePart, int depth, HashSet<object> visited)
        {
            if (root == null || depth < 0 || visited.Contains(root)) return null;
            Type rt = root.GetType();
            if (rt.Name.IndexOf(typeNamePart, StringComparison.OrdinalIgnoreCase) >= 0) return root;
            if (depth == 0 || IsSimple(rt)) return null;
            visited.Add(root);
            FieldInfo[] fields;
            try { fields = rt.GetFields(InstanceFlags); } catch { return null; }
            for (int i = 0; i < fields.Length; i++)
            {
                object value;
                try { value = fields[i].GetValue(root); } catch { continue; }
                if (value == null) continue;
                Type vt = value.GetType();
                if (vt.Name.IndexOf(typeNamePart, StringComparison.OrdinalIgnoreCase) >= 0) return value;
                if (ShouldTraverse(vt))
                {
                    object nested = FindNestedObjectByTypeName(value, typeNamePart, depth - 1, visited);
                    if (nested != null) return nested;
                }
            }
            return null;
        }

        private static bool ShouldTraverse(Type t)
        {
            if (t == null || IsSimple(t) || typeof(Delegate).IsAssignableFrom(t)) return false;
            string ns = t.Namespace ?? string.Empty;
            string n = t.Name ?? string.Empty;
            return ns.StartsWith("MGSC", StringComparison.Ordinal) || n.IndexOf("Tooltip", StringComparison.OrdinalIgnoreCase) >= 0 || n.IndexOf("Factory", StringComparison.OrdinalIgnoreCase) >= 0 || n.IndexOf("Handler", StringComparison.OrdinalIgnoreCase) >= 0 || n.IndexOf("Progression", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool IsSimple(Type t)
        {
            return t.IsPrimitive || t.IsEnum || t == typeof(string) || t == typeof(decimal) || t == typeof(DateTime) || t == typeof(Type);
        }

private static void InjectQuickRowsPooled(Component tooltip, List<DisplayRow> rows)
        {
            if (tooltip == null || rows == null) return;
            Type rowType = AccessTools.TypeByName("MGSC.TooltipProperty");
            if (rowType == null) return;

            int key = tooltip.GetInstanceID();
            QuickTooltipPool pool;
            if (!QuickTooltipPools.TryGetValue(key, out pool) || pool == null || pool.Tooltip == null || pool.Tooltip != tooltip)
            {
                pool = new QuickTooltipPool(tooltip);
                QuickTooltipPools[key] = pool;
            }

            string signature = BuildQuickSignature(rows);
            if (string.Equals(pool.Signature, signature, StringComparison.Ordinal) && pool.AreRowsActive(rows.Count))
                return;

            Component template = pool.Template;
            if (template == null)
            {
                template = FindBestTooltipPropertyTemplate(tooltip, rowType);
                if (template == null)
                {
                    if (!_loggedTooltipTemplateFailure)
                    {
                        _loggedTooltipTemplateFailure = true;
                        Debug.LogWarning("[ItemIntelligence] Vanilla TooltipProperty template was not found; intelligence block skipped.");
                    }
                    return;
                }
                pool.Template = template;
                pool.Parent = template.transform.parent;
            }

            Transform parent = pool.Parent;
            if (parent == null) parent = template.transform.parent;
            if (parent == null) return;

            for (int i = 0; i < rows.Count; i++)
            {
                GameObject clone = pool.GetRow(i);
                if (clone == null)
                {
                    clone = UnityEngine.Object.Instantiate(template.gameObject, parent);
                    clone.name = "QII_QuickPool_" + i.ToString(CultureInfo.InvariantCulture);
                    pool.SetRow(i, clone);
                }
                else if (clone.transform.parent != parent)
                {
                    clone.transform.SetParent(parent, false);
                }

                clone.SetActive(true);
                clone.transform.SetAsLastSibling();
                Component row = clone.GetComponent(rowType);
                ApplyTooltipRow(row, clone, rows[i]);
            }

            pool.DeactivateFrom(rows.Count);
            pool.Signature = signature;
            MarkTooltipLayout(tooltip);
        }

        private static string BuildQuickSignature(List<DisplayRow> rows)
        {
            if (rows == null || rows.Count == 0) return string.Empty;
            System.Text.StringBuilder sb = new System.Text.StringBuilder(64);
            for (int i = 0; i < rows.Count; i++)
            {
                if (i > 0) sb.Append('|');
                sb.Append(rows[i].Name ?? string.Empty);
                sb.Append('=');
                sb.Append(rows[i].Value ?? string.Empty);
            }
            return sb.ToString();
        }

        private static void MarkTooltipLayout(Component tooltip)
        {
            if (tooltip == null) return;
            try
            {
                RectTransform rt = tooltip.transform as RectTransform;
                if (rt != null) LayoutRebuilder.MarkLayoutForRebuild(rt);
                Transform parent = tooltip.transform.parent;
                RectTransform parentRt = parent as RectTransform;
                if (parentRt != null) LayoutRebuilder.MarkLayoutForRebuild(parentRt);
            }
            catch { }
        }

        private static Component FindBestTooltipPropertyTemplate(Component tooltip, Type rowType)
        {
            Component[] rows;
            try { rows = tooltip.GetComponentsInChildren(rowType, true); }
            catch { return null; }
            Component best = null;
            int bestScore = int.MinValue;
            for (int i = 0; i < rows.Length; i++)
            {
                Component row = rows[i];
                if (row == null || row.gameObject == null || row.gameObject.name.StartsWith("QII_", StringComparison.Ordinal)) continue;
                int score = row.gameObject.activeInHierarchy ? 100 : 0;
                Image[] images = row.gameObject.GetComponentsInChildren<Image>(true);
                int activeImages = 0;
                for (int x = 0; x < images.Length; x++) if (images[x] != null && images[x].gameObject.activeSelf) activeImages++;
                score -= activeImages * 5;
                TMP_Text[] texts = row.gameObject.GetComponentsInChildren<TMP_Text>(true);
                if (texts.Length >= 2) score += 20;
                if (score > bestScore) { best = row; bestScore = score; }
            }
            return best;
        }

        private static void ApplyTooltipRow(Component row, GameObject clone, DisplayRow display)
        {
            TMP_Text[] texts = clone.GetComponentsInChildren<TMP_Text>(true);
            for (int i = 0; i < texts.Length; i++)
                if (texts[i] != null) texts[i].text = string.Empty;

            Image[] images = clone.GetComponentsInChildren<Image>(true);
            for (int i = 0; i < images.Length; i++)
            {
                if (images[i] == null) continue;
                if (images[i].transform != clone.transform)
                    images[i].gameObject.SetActive(false);
            }

            bool named = InvokeStringSetter(row, "SetName", display.Name);
            bool valued = InvokeStringSetter(row, "SetValue", display.Value);
            texts = clone.GetComponentsInChildren<TMP_Text>(true);

            if (!named && texts.Length > 0) texts[0].text = display.Name;
            if (!valued)
            {
                if (texts.Length > 1) texts[texts.Length - 1].text = display.Value;
                else if (texts.Length == 1 && !string.IsNullOrEmpty(display.Value)) texts[0].text = display.Name + "   " + display.Value;
            }

            if (string.IsNullOrEmpty(display.Value) && texts.Length > 1)
                texts[texts.Length - 1].text = string.Empty;

            if (display.Header)
            {
                for (int i = 0; i < texts.Length; i++)
                {
                    texts[i].fontStyle = texts[i].fontStyle | FontStyles.Bold;
                    if (i > 0) texts[i].text = string.Empty;
                }
            }
        }

        private static bool InvokeStringSetter(object instance, string methodName, string value)
        {
            if (instance == null) return false;
            try
            {
                MethodInfo[] methods = instance.GetType().GetMethods(InstanceFlags);
                for (int i = 0; i < methods.Length; i++)
                {
                    MethodInfo method = methods[i];
                    if (!string.Equals(method.Name, methodName, StringComparison.Ordinal)) continue;
                    ParameterInfo[] p = method.GetParameters();
                    if (p.Length == 1 && p[0].ParameterType == typeof(string))
                    {
                        method.Invoke(instance, new object[] { value ?? string.Empty });
                        return true;
                    }
                }
            }
            catch { }
            return false;
        }

        private static void RemoveInjectedRows(Component tooltip)
        {
            if (tooltip == null) return;
            QuickTooltipPool pool;
            if (QuickTooltipPools.TryGetValue(tooltip.GetInstanceID(), out pool) && pool != null)
            {
                pool.DeactivateFrom(0);
                pool.Signature = string.Empty;
                MarkTooltipLayout(tooltip);
            }

        }

        private static bool IsModifiedItemId(string itemId)
        {
            return !string.IsNullOrEmpty(itemId) &&
                   itemId.IndexOf(ModifiedItemMarker, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string ResolveStaticRelationItemId(string itemId)
        {
            if (string.IsNullOrEmpty(itemId) || !IsModifiedItemId(itemId))
                return itemId ?? string.Empty;

            // Quasimorph's BasePickupItem.IsModifiedItem uses "_custom".
            // PickupItem.RenderId removes the same marker before resolving the base item.
            // Recipe and weapon/ammo indexes are keyed by that base ID.
            string baseId = itemId.Replace(ModifiedItemMarker, string.Empty);
            return string.IsNullOrEmpty(baseId) ? itemId : baseId;
        }

        private static bool UsesInheritedStaticRelations(string itemId)
        {
            string relationId = ResolveStaticRelationItemId(itemId);
            return !string.IsNullOrEmpty(relationId) &&
                   !string.Equals(relationId, itemId, StringComparison.OrdinalIgnoreCase);
        }



        private static int GetStaticRelationListCount<T>(Dictionary<string, List<T>> map, string itemId)
        {
            return GetListCount(map, ResolveStaticRelationItemId(itemId));
        }

        private static bool IsKnownItemId(string itemId)
        {
            if (string.IsNullOrEmpty(itemId)) return false;
            return KnownItemIds.Contains(itemId) || ItemRecordsById.ContainsKey(itemId) ||
                   PriceByItem.ContainsKey(itemId) || BarterItemIds.Contains(itemId);
        }

        private static string FindItemIdInArgs(object[] args)
        {
            if (args == null) return string.Empty;

            // First pass is intentionally O(arguments): station names, localization keys and
            // other arbitrary strings are NOT item IDs unless present in the loaded item table.
            for (int i = 0; i < args.Length; i++)
            {
                string textValue = args[i] as string;
                if (!string.IsNullOrEmpty(textValue) && IsKnownItemId(textValue)) return textValue;
                BasePickupItem pickup = args[i] as BasePickupItem;
                if (pickup != null && IsKnownItemId(pickup.Id)) return pickup.Id;
            }

            // Only then inspect small item wrappers. Do not recursively walk arbitrary station
            // or production controller graphs from a tooltip callback.
            for (int i = 0; i < args.Length; i++)
            {
                string id = FindItemIdFromObject(args[i], 2, new HashSet<object>(ReferenceComparer.Instance));
                if (!string.IsNullOrEmpty(id)) return id;
            }
            return string.Empty;
        }

        private static string FindItemIdFromObject(object obj, int depth, HashSet<object> visited)
        {
            if (obj == null || obj is string) return string.Empty;
            BasePickupItem pickup = obj as BasePickupItem;
            if (pickup != null && IsKnownItemId(pickup.Id)) return pickup.Id ?? string.Empty;

            Type t = obj.GetType();
            string typeName = t.Name ?? string.Empty;
            bool itemLike = typeName.IndexOf("Item", StringComparison.OrdinalIgnoreCase) >= 0 ||
                            typeName.IndexOf("Pickup", StringComparison.OrdinalIgnoreCase) >= 0 ||
                            typeName.IndexOf("Tooltip", StringComparison.OrdinalIgnoreCase) >= 0 ||
                            typeName.IndexOf("Slot", StringComparison.OrdinalIgnoreCase) >= 0;

            if (itemLike)
            {
                string direct = GetStringMember(obj, "ItemId");
                if (IsKnownItemId(direct)) return direct;
                direct = GetStringMember(obj, "Id");
                if (IsKnownItemId(direct)) return direct;
            }

            if (depth <= 0 || visited == null || visited.Contains(obj) || IsSimple(t)) return string.Empty;
            visited.Add(obj);

            FieldInfo[] fields;
            try { fields = t.GetFields(InstanceFlags); }
            catch { return string.Empty; }

            for (int i = 0; i < fields.Length; i++)
            {
                FieldInfo field = fields[i];
                if (field == null) continue;
                string fieldName = field.Name ?? string.Empty;
                if (fieldName.IndexOf("item", StringComparison.OrdinalIgnoreCase) < 0 &&
                    fieldName.IndexOf("pickup", StringComparison.OrdinalIgnoreCase) < 0 &&
                    fieldName.IndexOf("record", StringComparison.OrdinalIgnoreCase) < 0 &&
                    fieldName.IndexOf("slot", StringComparison.OrdinalIgnoreCase) < 0)
                    continue;

                object value;
                try { value = field.GetValue(obj); }
                catch { continue; }
                if (value == null) continue;
                string id = FindItemIdFromObject(value, depth - 1, visited);
                if (!string.IsNullOrEmpty(id)) return id;
            }
            return string.Empty;
        }












        private static string NormalizeGameText(string value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            value = value.Replace("Ё", "Е").Replace("ё", "е");
            return value;
        }





        private static bool ContainsCyrillic(string value)
        {
            if (string.IsNullOrEmpty(value)) return false;

            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];

                if ((c >= '\u0400' && c <= '\u04FF') ||
                    (c >= '\u0500' && c <= '\u052F'))
                    return true;
            }

            return false;
        }













        private static bool IsRussian()
        {
            string language = GetLanguageSignature();
            if (string.IsNullOrEmpty(language)) return false;

            return ExternalLanguageMatches(language, "Russian;ru;Русский;рус");
        }

























        private static string ConvertToStableString(object value)
        {
            if (value == null) return string.Empty;
            return Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
        }

        private static bool TryToInt(object value, out int result)
        {
            result = 0;
            if (value == null) return false;
            if (value is int) { result = (int)value; return true; }
            if (value is uint) { result = unchecked((int)(uint)value); return true; }
            if (value is short) { result = (short)value; return true; }
            if (value is long) { result = (int)(long)value; return true; }
            if (value is float) { result = Mathf.RoundToInt((float)value); return true; }
            if (value is double) { result = (int)Math.Round((double)value); return true; }
            if (value is decimal) { result = (int)Math.Round((decimal)value); return true; }
            return int.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), NumberStyles.Integer, CultureInfo.InvariantCulture, out result);
        }

        private static string FirstNonEmpty(string a, string b)
        {
            return !string.IsNullOrEmpty(a) ? a : (b ?? string.Empty);
        }

        private static string FirstNonEmpty(string a, string b, string c)
        {
            if (!string.IsNullOrEmpty(a)) return a;
            if (!string.IsNullOrEmpty(b)) return b;
            return c ?? string.Empty;
        }

        private static string FirstNonEmpty(string a, string b, string c, string d)
        {
            if (!string.IsNullOrEmpty(a)) return a;
            if (!string.IsNullOrEmpty(b)) return b;
            if (!string.IsNullOrEmpty(c)) return c;
            return d ?? string.Empty;
        }

        private static string FirstNonEmpty(string a, string b, string c, string d, string e)
        {
            if (!string.IsNullOrEmpty(a)) return a;
            if (!string.IsNullOrEmpty(b)) return b;
            if (!string.IsNullOrEmpty(c)) return c;
            if (!string.IsNullOrEmpty(d)) return d;
            return e ?? string.Empty;
        }

        private static string FirstNonEmpty(params string[] values)
        {
            if (values == null) return string.Empty;
            for (int i = 0; i < values.Length; i++) if (!string.IsNullOrEmpty(values[i])) return values[i];
            return string.Empty;
        }

        private static int GetListCount<T>(Dictionary<string, List<T>> map, string key)
        {
            List<T> list;
            return map.TryGetValue(key, out list) && list != null ? list.Count : 0;
        }

        private static void AddToList<T>(Dictionary<string, List<T>> map, string key, T value)
        {
            if (string.IsNullOrEmpty(key)) return;
            List<T> list;
            if (!map.TryGetValue(key, out list))
            {
                list = new List<T>();
                map[key] = list;
            }
            list.Add(value);
        }

        private sealed class FactionRewardPoolSnapshot
        {
            public readonly string FactionId;
            public readonly int CurrentTech;
            public readonly int TechLimit;
            public readonly int EffectiveTech;
            public float TotalWeight;
            public readonly Dictionary<string, float> ItemWeights =
                new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);

            public FactionRewardPoolSnapshot(string factionId, int currentTech, int techLimit, int effectiveTech)
            {
                FactionId = factionId ?? string.Empty;
                CurrentTech = currentTech;
                TechLimit = techLimit;
                EffectiveTech = effectiveTech;
            }
        }

        private sealed class FactionRewardView
        {
            public readonly string FactionId;
            public readonly int UnlockTech;
            public readonly int CurrentTech;
            public readonly int TechLimit;
            public readonly float RewardPercent;
            // 0 = available, 1 = tech locked, 2 = reputation locked, 3 = unknown
            public readonly int State;

            public FactionRewardView(
                string factionId, int unlockTech, int currentTech,
                int techLimit, float rewardPercent, int state)
            {
                FactionId = factionId ?? string.Empty;
                UnlockTech = unlockTech;
                CurrentTech = currentTech;
                TechLimit = techLimit;
                RewardPercent = rewardPercent;
                State = state;
            }
        }

        private sealed class FactionTechUnlock
        {
            public readonly string FactionId;
            public int TechLevel;
            public FactionTechUnlock(string factionId, int techLevel)
            {
                FactionId = factionId ?? string.Empty;
                TechLevel = techLevel;
            }
        }

        private sealed class DisplayRow
        {
            public readonly string Name;
            public readonly string Value;
            public readonly bool Header;
            public DisplayRow(string name, string value, bool header) { Name = name ?? string.Empty; Value = value ?? string.Empty; Header = header; }
        }

        private sealed class PriceSnapshot
        {
            public readonly int Owned;
            public readonly int Required;
            public PriceSnapshot(int owned, int required) { Owned = owned; Required = required; }
        }

        private sealed class MagnumUse
        {
            public readonly string PerkId;
            public readonly int Quantity;
            public readonly object Record;
            public readonly string ModuleId;
            public readonly string DepartmentId;
            public MagnumUse(string perkId, int quantity, object record)
            {
                PerkId = perkId ?? string.Empty;
                Quantity = quantity;
                Record = record;
                ModuleId = ModMain.GetStringMember(record, "ModuleId");
                DepartmentId = ModMain.GetStringMember(record, "DepartmentId");
            }
        }

        private sealed class MagnumSnapshot
        {
            public readonly List<MagnumUse> Current = new List<MagnumUse>();
            public readonly List<MagnumUse> Future = new List<MagnumUse>();
            public int CurrentRequired;
            public int FutureRequired;
            public int UnknownRequired;
            public int TotalRemaining;
        }

        private sealed class RecipeUseGroup
        {
            public readonly string OutputItemId;
            public readonly string Kind;
            public int Variants;
            public int MinQuantity;
            public int MaxQuantity;
            public readonly HashSet<string> Statuses = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            public readonly List<string> RecipeIds = new List<string>();
            public readonly List<string> OutputItemIds = new List<string>();

            public RecipeUseGroup(string outputItemId, string kind)
            {
                OutputItemId = outputItemId ?? string.Empty;
                Kind = kind ?? string.Empty;
                if (!string.IsNullOrEmpty(OutputItemId)) OutputItemIds.Add(OutputItemId);
            }
        }

        private sealed class RecipeUse
        {
            public readonly string RecipeId;
            public readonly string OutputItemId;
            public readonly int Quantity;
            public readonly string Kind;
            public RecipeUse(string recipeId, string outputItemId, int quantity, string kind) { RecipeId = recipeId; OutputItemId = outputItemId; Quantity = quantity; Kind = kind; }
        }

        private sealed class RecipeDef
        {
            public readonly string RecipeId;
            public readonly string OutputItemId;
            public readonly string Kind;
            public readonly Dictionary<string, int> Ingredients;
            public readonly List<string> RequiredPerks;
            public readonly List<string> AllowedWorkbenches;
            public RecipeDef(string recipeId, string outputItemId, string kind, Dictionary<string, int> ingredients, List<string> requiredPerks, List<string> allowedWorkbenches)
            {
                RecipeId = recipeId;
                OutputItemId = outputItemId;
                Kind = kind;
                Ingredients = ingredients ?? new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                RequiredPerks = requiredPerks ?? new List<string>();
                AllowedWorkbenches = allowedWorkbenches ?? new List<string>();
            }
        }

        private sealed class DisassemblyOutput
        {
            public readonly string ItemId;
            public int MinCount;
            public int MaxCount;
            public int RollCount;
            public float ChancePercent;
            public bool Possible;

            public DisassemblyOutput(string itemId, int minCount, int maxCount, float chancePercent, bool possible)
            {
                ItemId = itemId ?? string.Empty;
                MinCount = Math.Max(0, minCount);
                MaxCount = Math.Max(MinCount, maxCount);
                RollCount = Math.Max(1, maxCount);
                ChancePercent = chancePercent;
                Possible = possible;
            }
        }

        private sealed class LiveMarketEntry
        {
            public readonly string StationId;
            public readonly string SpaceObjectId;
            public readonly string Label;
            public readonly bool StationBuys;
            public readonly bool StationSells;
            public readonly int? StationBuyPrice;
            public readonly int? StationSellPrice;
            public readonly int? Stock;
            public readonly string OwnerFactionId;
            public readonly int OwnerRelation;
            public string TravelTime;
            public double? TravelHours;
            public bool HasMission;
            public double? MissionRemainingHours;
            public int MissionArrivalState;

            public LiveMarketEntry(
                string stationId, string spaceObjectId, string label,
                bool stationBuys, bool stationSells,
                int? stationBuyPrice, int? stationSellPrice, int? stock,
                string ownerFactionId, int ownerRelation)
            {
                StationId = stationId ?? string.Empty;
                SpaceObjectId = spaceObjectId ?? string.Empty;
                Label = label ?? string.Empty;
                StationBuys = stationBuys;
                StationSells = stationSells;
                StationBuyPrice = stationBuyPrice;
                StationSellPrice = stationSellPrice;
                Stock = stock;
                OwnerFactionId = ownerFactionId ?? string.Empty;
                OwnerRelation = ownerRelation;
                TravelTime = "—";
                TravelHours = null;
                HasMission = false;
                MissionRemainingHours = null;
                MissionArrivalState = 0;
            }
        }

        private sealed class WeaponInfo
        {
            public readonly string ItemId;
            public readonly HashSet<string> RequiredAmmoKeys;
            public readonly HashSet<string> DirectAmmoIds;
            public readonly Dictionary<string, int> OverrideAmmo;
            public readonly List<string> CompatibleAmmo = new List<string>();
            public readonly List<WeaponModeDescriptor> Modes = new List<WeaponModeDescriptor>();
            public WeaponInfo(string itemId, HashSet<string> requiredAmmoKeys, HashSet<string> directAmmoIds, Dictionary<string, int> overrideAmmo)
            {
                ItemId = itemId;
                RequiredAmmoKeys = requiredAmmoKeys ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                DirectAmmoIds = directAmmoIds ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                OverrideAmmo = overrideAmmo ?? new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            }
        }

        private sealed class WeaponModeDescriptor
        {
            public readonly string Key;
            public readonly string RawId;
            public readonly string Label;
            public readonly string Context;
            public readonly WeaponModeStaticStats Stats;

            public WeaponModeDescriptor(string key, string rawId, string label, string context, WeaponModeStaticStats stats)
            {
                Key = key ?? string.Empty;
                RawId = rawId ?? string.Empty;
                Label = label ?? string.Empty;
                Context = context ?? string.Empty;
                Stats = stats;
            }
        }

        private static List<WeaponModeDescriptor> GetWeaponModesForItem(string itemId)
        {
            List<WeaponModeDescriptor> result = new List<WeaponModeDescriptor>();
            string resolved = ResolveStaticRelationItemId(itemId);
            if (string.IsNullOrEmpty(resolved)) resolved = itemId;
            WeaponInfo info;
            if (!WeaponsByItem.TryGetValue(resolved, out info) || info == null || info.Modes == null || info.Modes.Count == 0)
                return result;
            for (int i = 0; i < info.Modes.Count; i++)
            {
                WeaponModeDescriptor mode = info.Modes[i];
                if (mode == null || (string.IsNullOrEmpty(mode.Label) && string.IsNullOrEmpty(mode.RawId))) continue;
                result.Add(mode);
            }
            return result;
        }

        private static Sprite TryResolveWeaponModeSmallIcon(string modeKey)
        {
            if (string.IsNullOrEmpty(modeKey)) return null;

            string rawId;
            if (!WeaponModeRawIdByKey.TryGetValue(modeKey, out rawId) || string.IsNullOrEmpty(rawId)) return null;

            Sprite sprite;
            if (WeaponModeIconsByRawId.TryGetValue(rawId, out sprite) && sprite != null) return sprite;
            if (WeaponModeIconMisses.Contains(rawId)) return null;

            object record;
            if (!WeaponModeRecordsById.TryGetValue(rawId, out record) || record == null)
            {
                WeaponModeIconMisses.Add(rawId);
                return null;
            }

            sprite = ResolveWeaponModeIconOnDemand(rawId, record);
            if (sprite != null)
            {
                WeaponModeIconsByRawId[rawId] = sprite;
                return sprite;
            }

            WeaponModeIconMisses.Add(rawId);
            return null;
        }

        private sealed class QuickTooltipPool
        {
            public readonly Component Tooltip;
            public Component Template;
            public Transform Parent;
            public string Signature = string.Empty;
            private readonly List<GameObject> _rows = new List<GameObject>();

            public QuickTooltipPool(Component tooltip) { Tooltip = tooltip; }

            public GameObject GetRow(int index)
            {
                if (index < 0 || index >= _rows.Count) return null;
                GameObject row = _rows[index];
                return row == null ? null : row;
            }

            public void SetRow(int index, GameObject row)
            {
                while (_rows.Count <= index) _rows.Add(null);
                _rows[index] = row;
            }

            public bool AreRowsActive(int count)
            {
                if (count < 0 || _rows.Count < count) return false;
                for (int i = 0; i < count; i++)
                {
                    GameObject row = _rows[i];
                    if (row == null || !row.activeSelf) return false;
                }
                for (int i = count; i < _rows.Count; i++)
                {
                    GameObject row = _rows[i];
                    if (row != null && row.activeSelf) return false;
                }
                return true;
            }

            public void DeactivateFrom(int first)
            {
                if (first < 0) first = 0;
                for (int i = first; i < _rows.Count; i++)
                {
                    GameObject row = _rows[i];
                    if (row != null && row.activeSelf) row.SetActive(false);
                }
            }
        }

    }

    public sealed class BrowserItemTooltipBinding : MonoBehaviour
    {
        public string ItemId = string.Empty;
        public string PreparedItemId = string.Empty;
        public BasePickupItem PreviewItem;
    }

    public sealed class BrowserModalTooltipLayerGuard : MonoBehaviour,
        UnityEngine.EventSystems.IPointerEnterHandler
    {
        private Coroutine _raiseRoutine;

        public void OnPointerEnter(UnityEngine.EventSystems.PointerEventData eventData)
        {
            if (_raiseRoutine != null) StopCoroutine(_raiseRoutine);
            _raiseRoutine = StartCoroutine(RaiseAfterNativeHandler());
        }

        private IEnumerator RaiseAfterNativeHandler()
        {
            // Exact LoadoutPresets R18 timing: ItemTooltipHandler can create or finish
            // its PropertiesTooltip later in the same event/frame. Two deferred passes
            // raise the native tooltip without scanning from Update.
            yield return null;
            ModMain.RequestBrowserTooltipLayerRaise();
            yield return null;
            ModMain.RequestBrowserTooltipLayerRaise();
            _raiseRoutine = null;
        }

        private void OnDisable()
        {
            if (_raiseRoutine != null) StopCoroutine(_raiseRoutine);
            _raiseRoutine = null;
        }
    }

    public sealed class ItemIntelligenceInspectorDriver : MonoBehaviour
    {
        private void Update()
        {
            ModMain.InspectorTick();
        }

        private void OnApplicationQuit()
        {
            ModMain.PrepareForApplicationQuitSafe();
        }
    }
}
