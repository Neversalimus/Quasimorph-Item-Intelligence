using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text;
using MGSC;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ItemIntelligence
{
    public static partial class ModMain
    {
        // Loot facade: shared source models, the single reset contract and
        // feature lifecycle only. Mutable state and behavior live in LootIndexes,
        // LootContainerIcons and LootPresentation.

        private sealed class LootWeightedItem
        {
            public readonly string ItemId;
            public readonly double Weight;
            public readonly int TechLevel;
            public readonly bool TechResolved;
            public readonly bool BonusEligible;
            public readonly bool BonusEligibilityResolved;
            public LootWeightedItem(
                string itemId,
                double weight,
                int techLevel,
                bool techResolved,
                bool bonusEligible,
                bool bonusEligibilityResolved)
            {
                ItemId = itemId ?? string.Empty;
                Weight = weight;
                TechLevel = techLevel;
                TechResolved = techResolved;
                BonusEligible = bonusEligible;
                BonusEligibilityResolved = bonusEligibilityResolved;
            }
        }

        private sealed class LootContainerDescriptor
        {
            public readonly string ContainerId;
            public readonly string DropId;
            public readonly int MinRolls;
            public readonly int MaxRolls;
            public readonly bool RollRangeResolved;
            public LootContainerDescriptor(
                string containerId,
                string dropId,
                int minRolls,
                int maxRolls,
                bool rollRangeResolved)
            {
                ContainerId = containerId ?? string.Empty;
                DropId = dropId ?? string.Empty;
                MinRolls = minRolls;
                MaxRolls = maxRolls;
                RollRangeResolved = rollRangeResolved;
            }
        }

        private sealed class LootContainerSource
        {
            public readonly string ContainerId;
            public readonly string DropId;
            public readonly string BiomeId;
            public readonly float BaseDrawPercent;
            public readonly int MinRolls;
            public readonly int MaxRolls;
            public readonly bool RollRangeResolved;
            public LootContainerSource(
                string containerId,
                string dropId,
                string biomeId,
                float baseDrawPercent,
                int minRolls,
                int maxRolls,
                bool rollRangeResolved)
            {
                ContainerId = containerId ?? string.Empty;
                DropId = dropId ?? string.Empty;
                BiomeId = biomeId ?? string.Empty;
                BaseDrawPercent = baseDrawPercent;
                MinRolls = minRolls;
                MaxRolls = maxRolls;
                RollRangeResolved = rollRangeResolved;
            }
        }

        private sealed class LootEnemySource
        {
            public readonly string MobClassId;
            public float MinPercent;
            public float MaxPercent;
            public readonly string Kind;
            public readonly string Detail;
            public int MinCount;
            public int MaxCount;
            public int MinTech;

            public LootEnemySource(
                string mobClassId,
                float minPercent,
                float maxPercent,
                string kind,
                string detail,
                int minCount,
                int maxCount,
                int minTech = 0)
            {
                MobClassId = mobClassId ?? string.Empty;
                MinPercent = minPercent;
                MaxPercent = maxPercent;
                Kind = kind ?? string.Empty;
                Detail = detail ?? string.Empty;
                MinCount = minCount;
                MaxCount = maxCount;
                MinTech = minTech;
            }
        }

        private sealed class LootAmputationSource
        {
            public readonly string WoundSlotId;
            public readonly float ConditionalPercent;
            public LootAmputationSource(string woundSlotId, float conditionalPercent)
            {
                WoundSlotId = woundSlotId ?? string.Empty;
                ConditionalPercent = conditionalPercent;
            }
        }

        private sealed class EnemyLootContext
        {
            public readonly string FactionId;
            public readonly int RawTech;
            public readonly int EffectiveTech;
            public EnemyLootContext(string factionId, int rawTech, int effectiveTech)
            {
                FactionId = factionId ?? string.Empty;
                RawTech = rawTech;
                EffectiveTech = effectiveTech;
            }
        }

        private sealed class EnemyChanceAccumulator
        {
            public float MinPercent = float.MaxValue;
            public float MaxPercent;
            public int SeenContextCount;
            public int MinRawTech = int.MaxValue;
            private int _lastContext = -1;

            public void Update(float percent, int contextIndex, int rawTech)
            {
                if (percent < MinPercent) MinPercent = percent;
                if (percent > MaxPercent) MaxPercent = percent;
                if (rawTech > 0 && rawTech < MinRawTech) MinRawTech = rawTech;
                if (_lastContext != contextIndex)
                {
                    _lastContext = contextIndex;
                    SeenContextCount++;
                }
            }
        }

        private sealed class LootItemMeta
        {
            public readonly string ItemId;
            public readonly string ItemClass;
            public readonly int TechLevel;
            public readonly HashSet<string> Categories;
            public readonly string WeaponClass;
            public readonly string ArmorClass;
            public readonly string AugmentationClass;
            public readonly string DefaultAmmoId;
            public readonly string EquipmentSlotKind;
            public readonly bool IsImplant;

            public LootItemMeta(
                string itemId,
                string itemClass,
                int techLevel,
                HashSet<string> categories,
                string weaponClass,
                string armorClass,
                string augmentationClass,
                string defaultAmmoId,
                string equipmentSlotKind,
                bool isImplant)
            {
                ItemId = itemId ?? string.Empty;
                ItemClass = itemClass ?? string.Empty;
                TechLevel = techLevel;
                Categories = categories ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                WeaponClass = weaponClass ?? string.Empty;
                ArmorClass = armorClass ?? string.Empty;
                AugmentationClass = augmentationClass ?? string.Empty;
                DefaultAmmoId = defaultAmmoId ?? string.Empty;
                EquipmentSlotKind = equipmentSlotKind ?? string.Empty;
                IsImplant = isImplant;
            }
        }

        private sealed class LootMissionSource
        {
            public readonly string SourceId;
            public readonly string Kind;
            public readonly int ItemTech;
            public LootMissionSource(
                string sourceId,
                string kind,
                int itemTech)
            {
                SourceId = sourceId ?? string.Empty;
                Kind = kind ?? string.Empty;
                ItemTech = itemTech;
            }
        }


        // v1.7.36-test5: Loot owns all of its index/warmup invalidation. This is
        // a mechanical extraction of the former Runtime.ClearIndexes() block.
        private static void ResetLootIndexState()
        {
            LootContainerSourcesByItem.Clear();
            ResetLootContainerSaveEstimateIndex();
            ResetLootBaronSpecialIndex();
            ResetLootSpecialSourcesIndex();
            ResetLootGeneralSpawnIndex();
            LootEnemySourcesByItem.Clear();
            LootAmputationSourcesByItem.Clear();
            ResetLootAmputationBuildState();
            LootItemsByItemClass.Clear();
            LootItemsByWeaponClass.Clear();
            LootItemsByArmorClass.Clear();
            LootImplantsByAugmentationClass.Clear();
            LootAugmentationsByAugmentationClass.Clear();
            LootAugmentationsByRecordId.Clear();
            LootEnemyMinSpawnTechByFaction.Clear();
            LootEnemyFactionIds.Clear();
            _lootEnemyContextIndexReady = false;
            ResetEnemyLootSpawnContextSlice();
            LootBramfaturaSourcesByItem.Clear();
            LootStationTypeSourcesByItem.Clear();
            LootFactionSourcesByItem.Clear();
            LootContainerDescriptorsByDropId.Clear();
            ResetLootContainerProfileAuditState();
            LootContainerIconsById.Clear();
            LootContainerIconSourcesById.Clear();
            LootContainerIconMisses.Clear();
            _lootContainerRendererCatalog = new LootContainerRendererSnapshot[0];
            LootContainerRenderersByStem.Clear();
            _lootContainerRendererCatalogReady = false;
            LootContainerRecordsById.Clear();
            _lootContainerRecordCacheReady = false;
            LootItemMetaById.Clear();
            LootItemsByCategory.Clear();
            LootWarmupItemIds.Clear();
            LootWarmupContainerDropIds.Clear();
            LootWarmupMobClasses.Clear();
            LootWarmupBramfaturas.Clear();
            LootWarmupStationTypes.Clear();
            LootWarmupFactions.Clear();
            _lootWarmupPhase = 0;
            _lootWarmupIndex = 0;
            _lootWarmupProcessed = 0;
            _lootWarmupTotal = 0;
            _lootWarmupActive = false;
            _lootWarmupComplete = false;
            _lootWarmupRequested = false;
            _lootWarmupNextFrame = 0;
            ResetLootMobWork();
            LootDisplayNameCache.Clear();
            _lootContainerDropCollection = null;
            _lootContainerGetDropMethod = null;
            _lootContainerGetDropBiomesMethod = null;
        }

        private static void StartLootSourcesWarmup()
        {
            // Keep one reset contract. The former duplicated block could silently drift
            // away from ResetLootIndexState when a new Loot cache was added.
            ResetLootIndexState();

            if (!_compatLoot || KnownItemIds.Count == 0)
                return;

            try
            {
                foreach (string knownItemId in KnownItemIds)
                {
                    if (!string.IsNullOrEmpty(knownItemId))
                        LootWarmupItemIds.Add(knownItemId);
                }

                _lootContainerDropCollection =
                    GetStaticMember(typeof(Data), "ContainerItemDrop");
                ResolveLootContainerMethods();

                object containerIds =
                    GetMember(_lootContainerDropCollection, "ContainerIds");
                IEnumerable ids = containerIds as IEnumerable;
                if (ids != null && !(containerIds is string))
                {
                    foreach (object raw in ids)
                    {
                        string id = ConvertToStableString(raw);
                        if (!string.IsNullOrEmpty(id) &&
                            !LootWarmupContainerDropIds.Contains(id))
                            LootWarmupContainerDropIds.Add(id);
                    }
                }

                // Resolve the physical-container aliases only after the authoritative
                // ContainerItemDrop profile set is known. This lets the profile owner
                // audit every drop-like member without accepting unrelated string data.
                BuildLootContainerDescriptors();

                // Baron pact/death coverage is intentionally not built for ordinary Loot.
                // It is tiny but item-metadata-wide, so it remains lazy until a Baron-usable
                // Skull/Pact is actually inspected. This preserves the normal Loot start budget.

                LootWarmupMobClasses.AddRange(
                    EnumerateData(GetStaticMember(typeof(Data), "MobClasses")));
                LootWarmupBramfaturas.AddRange(
                    EnumerateData(GetStaticMember(typeof(Data), "Bramfaturas")));
                LootWarmupStationTypes.AddRange(
                    EnumerateData(GetStaticMember(typeof(Data), "StationTypes")));
                LootWarmupFactions.AddRange(
                    EnumerateData(GetStaticMember(typeof(Data), "Factions")));

                // v1.7.5: the expensive enemy/corpse reverse index is deliberately
                // deferred until the player opens the Loot tab. The old behavior spent
                // several seconds after every save load evaluating all mob/Tech/faction
                // combinations even when Item Intelligence was never opened.
                _lootWarmupTotal =
                    LootWarmupItemIds.Count +
                    LootWarmupContainerDropIds.Count +
                    (LootWarmupMobClasses.Count * 10) + // init + 9 small enemy stages
                    LootWarmupBramfaturas.Count +
                    LootWarmupStationTypes.Count +
                    LootWarmupFactions.Count + 3; // spawn-context + amputation + general-spawn phases

                _lootWarmupActive = false;
                _lootWarmupComplete = _lootWarmupTotal <= 0;
                _lootWarmupRequested = false;

                Debug.Log(
                    "[ItemIntelligence] Loot Sources warmup deferred until Loot tab: items=" +
                    LootWarmupItemIds.Count +
                    ", containers=" + LootWarmupContainerDropIds.Count +
                    ", mobs=" + LootWarmupMobClasses.Count +
                    ", bramfaturas=" + LootWarmupBramfaturas.Count +
                    ", stationTypes=" + LootWarmupStationTypes.Count +
                    ", factions=" + LootWarmupFactions.Count + ".");
            }
            catch (Exception ex)
            {
                _lootWarmupComplete = false;
                StopLootFeatureFrameWork();
                TripCompatibilityFeatureRuntime("Loot", ex);
            }
        }

        private static void StartLootFeatureWarmup()
        {
            if (!_compatLoot) return;
            try { StartLootSourcesWarmup(); }
            catch (Exception ex)
            {
                StopLootFeatureFrameWork();
                TripCompatibilityFeatureRuntime("Loot", ex);
            }
        }

        private static void TickLootFeatureFrameWork()
        {
            if (!_compatLoot) return;

            try
            {
                TickLootSourcesWarmup();
                UpdateLootProgressUi();
            }
            catch (Exception ex)
            {
                StopLootFeatureFrameWork();
                try
                {
                    if (_lootProgressRoot != null) _lootProgressRoot.SetActive(false);
                    _lootProgressLastVisible = false;
                    _lootProgressLastPercent = -1;
                }
                catch { }
                TripCompatibilityFeatureRuntime("Loot", ex);
            }

            if (_lootWarmupActive && _inspectorOpen &&
                BrowserNavigation.Tab == (int)BrowserTabId.Loot &&
                Time.frameCount - _lootLastBrowserRefreshFrame >= 120 &&
                !string.IsNullOrEmpty(_inspectorItemId))
            {
                _lootLastBrowserRefreshFrame = Time.frameCount;
                try { RenderBrowser(_inspectorItemId); }
                catch (Exception ex)
                {
                    LogRuntimeBoundaryWarningOnce(
                        "browser.loot.refresh",
                        "Loot progress refresh failed; the current browser view was left unchanged.",
                        ex);
                }
            }
        }

        private static void StopLootFeatureFrameWork()
        {
            _lootWarmupActive = false;
            _lootWarmupRequested = false;
            _lootWarmupNextFrame = 0;
        }

        private static string GetLootWarmupStatus()
        {
            return !_compatLoot
                ? "disabled"
                : (_lootWarmupComplete
                ? "complete"
                : (_lootWarmupActive ? "pending" : "deferred"));
        }


    }
}
