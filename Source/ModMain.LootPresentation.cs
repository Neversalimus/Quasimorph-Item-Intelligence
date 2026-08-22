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
        // Test12 Loot presentation owner: progress UI, table rows, labels, wrapping and display formatting.

        // Owner state: progress view bookkeeping and localized display-name cache.
        private static GameObject _lootProgressRoot;
        private static RectTransform _lootProgressFillRect;
        private static TMP_Text _lootProgressText;
        private static int _lootProgressLastPercent = -1;
        private static bool _lootProgressLastVisible;
        private static int _lootLastBrowserRefreshFrame = -1000;
        private static readonly Dictionary<string, string> LootDisplayNameCache =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private static void CreateLootProgressUi()
        {
            if (_inspectorRoot == null || _lootProgressRoot != null) return;

            _lootProgressRoot = new GameObject("LootIndexProgress");
            _lootProgressRoot.transform.SetParent(_inspectorRoot.transform, false);
            RectTransform rootRt = _lootProgressRoot.AddComponent<RectTransform>();
            rootRt.anchorMin = new Vector2(0f, 1f);
            rootRt.anchorMax = new Vector2(0f, 1f);
            rootRt.pivot = new Vector2(0f, 1f);
            rootRt.anchoredPosition = new Vector2(18f, -207f);
            rootRt.sizeDelta = new Vector2(700f, 12f);

            Image back = _lootProgressRoot.AddComponent<Image>();
            back.color = new Color(0.015f, 0.050f, 0.041f, 0.95f);
            back.raycastTarget = false;

            GameObject fillGo = new GameObject("Fill");
            fillGo.transform.SetParent(_lootProgressRoot.transform, false);
            _lootProgressFillRect = fillGo.AddComponent<RectTransform>();
            _lootProgressFillRect.anchorMin = new Vector2(0f, 0f);
            _lootProgressFillRect.anchorMax = new Vector2(0f, 1f);
            _lootProgressFillRect.pivot = new Vector2(0f, 0.5f);
            _lootProgressFillRect.offsetMin = Vector2.zero;
            _lootProgressFillRect.offsetMax = Vector2.zero;
            Image fill = fillGo.AddComponent<Image>();
            fill.color = new Color(0.29f, 0.72f, 0.50f, 0.92f);
            fill.raycastTarget = false;

            GameObject textGo = CreateBrowserText("Percent", _lootProgressRoot.transform,
                new Vector2(0f, -1f), new Vector2(700f, 14f),
                11f, new Color(0.80f, 0.92f, 0.72f, 1f), FontStyles.Bold,
                TextAlignmentOptions.Center);
            _lootProgressText = textGo.GetComponent<TMP_Text>();
            _lootProgressRoot.SetActive(false);
        }

        private static void UpdateLootProgressUi()
        {
            if (_lootProgressRoot == null) return;
            bool show = _inspectorOpen && BrowserNavigation.Tab == (int)BrowserTabId.Loot && _lootWarmupActive;
            if (_lootProgressLastVisible != show)
            {
                _lootProgressLastVisible = show;
                _lootProgressRoot.SetActive(show);
            }
            if (!show)
            {
                _lootProgressLastPercent = -1;
                return;
            }

            int total = Math.Max(1, _lootWarmupTotal);
            float ratio = Mathf.Clamp01((float)_lootWarmupProcessed / (float)total);
            int percent = Mathf.Clamp(Mathf.RoundToInt(ratio * 100f), 0, 100);
            if (percent == _lootProgressLastPercent) return;
            _lootProgressLastPercent = percent;

            if (_lootProgressFillRect != null)
            {
                _lootProgressFillRect.anchorMax = new Vector2(ratio, 1f);
                _lootProgressFillRect.offsetMin = Vector2.zero;
                _lootProgressFillRect.offsetMax = Vector2.zero;
            }
            if (_lootProgressText != null)
            {
                _lootProgressText.text = NormalizeModUiText((Ui("ui.loot_index")) +
                    percent.ToString(CultureInfo.InvariantCulture) + "%");
            }
        }

        private static void ConfigureLootColumn(
            TMP_Text column,
            float x,
            float width,
            string value,
            float fontSize)
        {
            if (column == null) return;
            RectTransform rt = column.rectTransform;
            SetBrowserRectPositionIfChanged(rt, x, 0f);
            SetBrowserRectSizeIfChanged(rt, width, rt.sizeDelta.y);
            bool visible = width > 0f && !string.IsNullOrEmpty(value);
            SetBrowserActiveIfChanged(column.gameObject, visible);
            SetBrowserTextIfChanged(column, visible ? NormalizeModUiText(value) : string.Empty);
            SetBrowserFontSizeIfChanged(column, fontSize);
            SetBrowserAutoSizingIfChanged(column, false);
            SetBrowserWordWrappingIfChanged(column, false);
            SetBrowserOverflowIfChanged(column, TextOverflowModes.Ellipsis);
            if (visible) SetBrowserFontStyleIfChanged(column, FontStyles.Normal);
        }

        private static void SetLootColumnHeaderStyle(TMP_Text column, Color color)
        {
            if (column == null || !column.gameObject.activeSelf) return;
            column.color = color;
            column.fontStyle = FontStyles.Italic;
        }

        private static void BuildBrowserLootSources(string itemId)
        {
            bool ru = IsRussian();

            if (!ShowSources) return;

            if (!_compatLoot)
            {
                AddCompatibilityUnavailableLine("Loot");
                return;
            }

            EnsureLootWarmupStarted();

            LootModifierSnapshot lootModifiers = GetLootModifierSnapshot();
            AppendLootModifierControlLines(lootModifiers);
            int lootSectionsStartAt = BrowserLines.Count;
            bool any = false;

            // ContainerItemDrop.GetDrop exposes the weighted pool. The audited spawn path
            // rejects an entry when context Tech is below ItemRecord.TechLevel, so this is
            // a minimum threshold rather than a fixed tier.
            if (!LootItemMetaById.ContainsKey(itemId))
                IndexLootItemMeta(itemId);
            LootItemMeta inspectedLootMeta = null;
            LootItemMetaById.TryGetValue(itemId, out inspectedLootMeta);
            int containerItemTech = inspectedLootMeta != null ? Math.Max(0, inspectedLootMeta.TechLevel) : 0;

            List<LootContainerSource> rawContainers;
            LootContainerSourcesByItem.TryGetValue(itemId, out rawContainers);
            List<LootContainerSource> containers = FilterActiveLootContainerSources(
                rawContainers, lootModifiers.StorageExpected);
            if (containers != null && containers.Count > 0)
            {
                any = true;
                bool buildContainers = AddLootSectionHeaderAndShouldBuild(
                    Ui("ui.containers"), containers.Count);
                if (buildContainers)
                {
                    LootContainerSaveEstimateSnapshot containerEstimate =
                        GetLootContainerSaveEstimateSnapshot();
                    containers.Sort(delegate(LootContainerSource a, LootContainerSource b)
                    {
                        int name = string.Compare(a.ContainerId, b.ContainerId, StringComparison.OrdinalIgnoreCase);
                        if (name != 0) return name;
                        int profile = string.Compare(a.DropId, b.DropId, StringComparison.OrdinalIgnoreCase);
                        if (profile != 0) return profile;
                        return string.Compare(a.BiomeId, b.BiomeId, StringComparison.OrdinalIgnoreCase);
                    });

                    BrowserLines.Add(BrowserLine.LootHeader(
                        Ui("loot.column.container_profile"),
                        Ui("ui.context"),
                        Ui("loot.column.save_estimate"),
                        Ui("ui.tech"),
                        Ui("ui.rolls")));

                    bool hasUnknownRollRange = false;
                    bool hasUnavailableEstimate = false;
                    for (int i = 0; i < containers.Count; i++)
                    {
                        LootContainerSource source = containers[i];
                        if (source == null) continue;
                        if (!source.RollRangeResolved) hasUnknownRollRange = true;
                        string saveEstimate = FormatLootContainerEffectiveChance(
                            itemId, source, containerEstimate, lootModifiers.StorageExpected);
                        if (string.Equals(saveEstimate, "—", StringComparison.Ordinal))
                            hasUnavailableEstimate = true;

                        string context = string.IsNullOrEmpty(source.BiomeId)
                            ? Ui("ui.any")
                            : ResolveLootSourceName(source.BiomeId, "StationType");
                        BrowserLines.Add(BrowserLine.LootContainerRow(
                            source.ContainerId,
                            ResolveLootContainerSourceLabel(source),
                            context,
                            saveEstimate,
                            containerItemTech > 0
                                ? "T" + containerItemTech.ToString(CultureInfo.InvariantCulture) + "+"
                                : Ui("ui.any"),
                            FormatLootContainerRolls(source, lootModifiers.StorageExpected)));
                    }

                    AddWrappedLootNote("loot.note.container_context_chance");
                    if (lootModifiers.StorageExpected < 0.0)
                        AddWrappedLootNote("loot.note.container_modifier_unavailable");
                    if (hasUnavailableEstimate)
                        AddWrappedLootNote("loot.note.container_save_unavailable");
                    if (hasUnknownRollRange) AddWrappedLootNote("loot.note.unknown_container_rolls");
                }
            }
            AppendLootGeneralSpawnContainerLines(
                itemId, rawContainers, containerItemTech, ref any);

            // Scripted Baron loot is not part of MobClass equipment/drop generation.
            // Keep it as its own audited source before normal enemy rows.
            AppendLootBaronSpecialLines(itemId, ref any);

            // Exact non-random acquisition paths proven by the current source-family audit.
            AppendLootSpecialSourceLines(itemId, ref any);

            List<LootEnemySource> rawEnemies;
            LootEnemySourcesByItem.TryGetValue(itemId, out rawEnemies);
            AppendLootEnemySections(rawEnemies, lootModifiers, ru, ref any);

            List<LootAmputationSource> amputations;
            if (LootAmputationSourcesByItem.TryGetValue(itemId, out amputations) &&
                amputations != null && amputations.Count > 0)
            {
                any = true;
                bool buildAmputations = AddLootSectionHeaderAndShouldBuild(
                    Ui("ui.amputation_drops"), amputations.Count);
                if (buildAmputations)
                {
                amputations.Sort(delegate(LootAmputationSource a, LootAmputationSource b)
                {
                    return string.Compare(a.WoundSlotId, b.WoundSlotId, StringComparison.OrdinalIgnoreCase);
                });
                BrowserLines.Add(
                    BrowserLine.LootHeader(
                        Ui("ui.wound_slot"),
                        Ui("ui.source"),
                        Ui("ui.chance"),
                        Ui("ui.qty"),
                        Ui("ui.result")));
                for (int i = 0; i < amputations.Count; i++)
                {
                    LootAmputationSource source = amputations[i];
                    if (source == null) continue;
                    BrowserLines.Add(
                        BrowserLine.LootRow(
                            ResolveLootWoundSlotName(source.WoundSlotId),
                            Ui("ui.amputation"),
                            FormatLootPercent(source.ConditionalPercent),
                            "x1",
                            Ui("ui.floor")));
                }
                AddWrappedLootNote("loot.note.amputation");
                }
            }

            List<LootMissionSource> bramfaturas;
            LootBramfaturaSourcesByItem.TryGetValue(itemId, out bramfaturas);
            List<LootMissionSource> stationTypes;
            LootStationTypeSourcesByItem.TryGetValue(itemId, out stationTypes);
            List<LootMissionSource> factions;
            LootFactionSourcesByItem.TryGetValue(itemId, out factions);

            int missionCount =
                (bramfaturas == null ? 0 : bramfaturas.Count) +
                (stationTypes == null ? 0 : stationTypes.Count) +
                (factions == null ? 0 : factions.Count);
            if (missionCount > 0)
            {
                any = true;
                bool buildMissionPools = AddLootSectionHeaderAndShouldBuild(
                    Ui("ui.mission_pools"), missionCount);
                if (buildMissionPools)
                {
                BrowserLines.Add(
                    BrowserLine.LootHeader(
                        Ui("ui.source"),
                        Ui("ui.type"),
                        Ui("ui.tech"),
                        string.Empty,
                        string.Empty));

                AddLootMissionTableLines(bramfaturas, "Bramfatura");
                AddLootMissionTableLines(stationTypes, "StationType");
                AddLootMissionTableLines(factions, "Faction");

                AddLootPlacementRules();
                AddWrappedLootNote("loot.note.mission_pools");
                }
            }

            ApplyLootCollapsibleSections(lootSectionsStartAt);

            if (!any && !_lootWarmupActive)
                BrowserLines.Add(
                    BrowserLine.FullNote(
                        Ui("ui.no_explicit_loot_sources_found_check_trade_recip")));
        }

        private static string FormatEnemyLootMinTech(LootEnemySource source)
        {
            if (source == null || source.MinTech <= 0) return "—";
            return "T" + source.MinTech.ToString(CultureInfo.InvariantCulture) + "+";
        }

        private static string GetEnemyLootKindLabel(string kind, bool ru)
        {
            if (string.Equals(kind, "Granted", StringComparison.Ordinal)) return Ui("ui.granted_item");
            if (string.Equals(kind, "Primary", StringComparison.Ordinal)) return Ui("ui.primary_weapon");
            if (string.Equals(kind, "Secondary", StringComparison.Ordinal)) return Ui("ui.secondary_weapon");
            if (string.Equals(kind, "Head", StringComparison.Ordinal)) return Ui("ui.head_gear");
            if (string.Equals(kind, "Armor", StringComparison.Ordinal)) return Ui("ui.armor");
            if (string.Equals(kind, "Leggings", StringComparison.Ordinal)) return Ui("ui.leggings");
            if (string.Equals(kind, "Boots", StringComparison.Ordinal)) return Ui("ui.boots");
            if (string.Equals(kind, "Additional", StringComparison.Ordinal)) return Ui("ui.extra_item");
            if (string.Equals(kind, "PrimaryAmmo", StringComparison.Ordinal)) return Ui("ui.primary_ammo");
            if (string.Equals(kind, "SecondaryAmmo", StringComparison.Ordinal)) return Ui("ui.secondary_ammo");
            if (string.Equals(kind, "ExtraWeaponAmmo", StringComparison.Ordinal)) return Ui("ui.extra_weapon_ammo");
            if (string.Equals(kind, "GrantedWeaponAmmo", StringComparison.Ordinal)) return Ui("ui.granted_weapon_ammo");
            if (string.Equals(kind, "CorpseBonus", StringComparison.Ordinal)) return Ui("ui.corpse_bonus");
            if (string.Equals(kind, "GrantedAugmentation", StringComparison.Ordinal)) return Ui("ui.granted_augmentation");
            if (string.Equals(kind, "RandomAugmentation", StringComparison.Ordinal)) return Ui("ui.random_augmentation");
            if (string.Equals(kind, "GrantedImplant", StringComparison.Ordinal)) return Ui("ui.granted_implant");
            if (string.Equals(kind, "RandomImplant", StringComparison.Ordinal)) return Ui("ui.random_implant");
            return HumanizeLootIdentifier(kind);
        }

        private static string FormatEnemyLootChance(
            LootEnemySource source,
            double corpseBonusExpectedRolls)
        {
            if (source == null) return "-";
            float min = source.MinPercent;
            float max = source.MaxPercent;

            if (string.Equals(source.Kind, "CorpseBonus", StringComparison.Ordinal))
            {
                if (corpseBonusExpectedRolls < 0.0)
                {
                    string perRoll = Math.Abs(max - min) < 0.05f
                        ? FormatLootPercent(max)
                        : FormatLootPercent(min) + "-" + FormatLootPercent(max);
                    return perRoll + (Ui("ui.roll"));
                }

                min = (float)(CorpseBonusAtLeastOnceChance(min / 100.0, corpseBonusExpectedRolls) * 100.0);
                max = (float)(CorpseBonusAtLeastOnceChance(max / 100.0, corpseBonusExpectedRolls) * 100.0);
            }

            return Math.Abs(max - min) < 0.05f
                ? FormatLootPercent(max)
                : FormatLootPercent(min) + "-" + FormatLootPercent(max);
        }

        private static string FormatEnemyLootQuantity(
            LootEnemySource source,
            double corpseBonusExpectedRolls,
            bool ru)
        {
            if (source == null) return "-";
            if (string.Equals(source.Kind, "CorpseBonus", StringComparison.Ordinal))
            {
                if (corpseBonusExpectedRolls < 0.0) return Ui("ui.bonus");
                if (corpseBonusExpectedRolls <= 0.0) return "0";
                int floorRolls = Math.Max(0, (int)Math.Floor(corpseBonusExpectedRolls));
                double fraction = corpseBonusExpectedRolls - floorRolls;
                if (fraction < 0.0001)
                    return "x" + floorRolls.ToString(CultureInfo.InvariantCulture);
                return floorRolls.ToString(CultureInfo.InvariantCulture) + "-" +
                    (floorRolls + 1).ToString(CultureInfo.InvariantCulture);
            }

            if (string.Equals(source.Kind, "RandomAugmentation", StringComparison.Ordinal) &&
                source.MaxCount > 0)
                return "0-" + source.MaxCount.ToString(CultureInfo.InvariantCulture) +
                    (Ui("ui.rolls_2"));
            if (string.Equals(source.Kind, "RandomImplant", StringComparison.Ordinal) &&
                source.MaxCount > 0)
                return "0-" + source.MaxCount.ToString(CultureInfo.InvariantCulture) +
                    (Ui("ui.rolls_2"));
            if (string.Equals(source.Kind, "Additional", StringComparison.Ordinal) &&
                source.MaxCount > 0)
                return "0-" + source.MaxCount.ToString(CultureInfo.InvariantCulture);

            if (source.MinCount == source.MaxCount && source.MaxCount > 0)
                return "x" + source.MaxCount.ToString(CultureInfo.InvariantCulture);
            if (source.MaxCount > 0)
                return Math.Max(0, source.MinCount).ToString(CultureInfo.InvariantCulture) + "-" +
                    source.MaxCount.ToString(CultureInfo.InvariantCulture);
            return "-";
        }

        private static string GetEnemyLootResultLabel(LootEnemySource source, bool ru)
        {
            if (source == null) return string.Empty;
            if (string.Equals(source.Kind, "GrantedAugmentation", StringComparison.Ordinal) ||
                string.Equals(source.Kind, "RandomAugmentation", StringComparison.Ordinal) ||
                string.Equals(source.Kind, "GrantedImplant", StringComparison.Ordinal) ||
                string.Equals(source.Kind, "RandomImplant", StringComparison.Ordinal))
                return Ui("ui.amputate");
            return Ui("ui.corpse");
        }

        private static string ResolveLootWoundSlotName(string id)
        {
            if (string.IsNullOrEmpty(id)) return Ui("ui.wound_slot_2");
            string localized = LocalizeCandidates(
                new string[]
                {
                    "woundslot." + id + ".name",
                    "wound." + id + ".name",
                    id
                }, id);
            if (!string.IsNullOrEmpty(localized) &&
                !string.Equals(localized, id, StringComparison.OrdinalIgnoreCase))
                return NormalizeGameText(localized);
            return NormalizeGameText(HumanizeLootIdentifier(id));
        }

        private static void AddLootMissionTableLines(
            List<LootMissionSource> sources,
            string kind)
        {
            if (sources == null || sources.Count == 0) return;
            sources.Sort(delegate(LootMissionSource a, LootMissionSource b)
            {
                return string.Compare(
                    ResolveLootSourceName(a.SourceId, kind),
                    ResolveLootSourceName(b.SourceId, kind),
                    StringComparison.CurrentCultureIgnoreCase);
            });

            string typeLabel;
            if (string.Equals(kind, "Bramfatura", StringComparison.OrdinalIgnoreCase))
                typeLabel = Ui("ui.location");
            else if (string.Equals(kind, "StationType", StringComparison.OrdinalIgnoreCase))
                typeLabel = Ui("ui.station_2");
            else
                typeLabel = Ui("ui.faction");

            const int maxShown = 64;
            int totalValid = 0;
            for (int i = 0; i < sources.Count; i++)
                if (sources[i] != null) totalValid++;

            int shown = 0;
            for (int i = 0; i < sources.Count && shown < maxShown; i++)
            {
                LootMissionSource source = sources[i];
                if (source == null) continue;
                shown++;

                string tech = source.ItemTech > 0
                    ? "T" + source.ItemTech.ToString(CultureInfo.InvariantCulture) + "+"
                    : (Ui("ui.any"));

                BrowserLines.Add(
                    BrowserLine.LootMissionRow(
                        ResolveLootSourceName(source.SourceId, kind),
                        typeLabel,
                        tech));
            }

            int remaining = totalValid - shown;
            if (remaining > 0)
                BrowserLines.Add(BrowserLine.FullNote(string.Format(
                    CultureInfo.InvariantCulture, Ui("ui.more_rows_format"), remaining, typeLabel)));
        }

        private static void AddWrappedLootNote(string key)
        {
            AddWrappedBrowserNote(key, 110, 120);
        }

        private static void AddLootPlacementRules()
        {
            bool ru = IsRussian();
            int containersPercent = -1;
            try
            {
                object global = Data.Global;
                TryToInt(
                    GetMember(global, "ItemDropContainersPercent"),
                    out containersPercent);
            }
            catch { containersPercent = -1; }

            if (containersPercent >= 0)
            {
                containersPercent = Math.Max(0, Math.Min(100, containersPercent));
                string placementText = Ui("loot.note.placement_percent_prefix") +
                    containersPercent.ToString(CultureInfo.InvariantCulture) +
                    Ui("loot.note.placement_percent_suffix");
                int placementLimit = IsRussian() ? 72 : 86;
                List<string> placementLines = WrapBrowserFullWidthText(placementText, placementLimit);
                for (int i = 0; i < placementLines.Count; i++)
                    BrowserLines.Add(BrowserLine.FullNote(placementLines[i]));
            }
            else
            {
                AddWrappedLootNote("loot.note.placement_generic");
            }
        }

        private static object FindLootDataRecord(string collectionName, string id)
        {
            if (string.IsNullOrEmpty(collectionName) || string.IsNullOrEmpty(id)) return null;
            object collection = GetStaticMember(typeof(Data), collectionName);
            List<DataEntry> entries = EnumerateData(collection);
            for (int i = 0; i < entries.Count; i++)
            {
                DataEntry entry = entries[i];
                if (entry == null || entry.Value == null) continue;
                string candidate = FirstNonEmpty(
                    GetStringMember(entry.Value, "Id"),
                    GetStringMember(entry.Value, "StationTypeId"),
                    GetStringMember(entry.Value, "FactionId"),
                    entry.Key);
                if (string.Equals(candidate, id, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(entry.Key, id, StringComparison.OrdinalIgnoreCase))
                    return entry.Value;
            }
            return null;
        }

        private static string ResolveLootRecordLocalizedName(string collectionName, string id)
        {
            object record = FindLootDataRecord(collectionName, id);
            if (record == null) return string.Empty;

            List<object> nodes = new List<object>();
            nodes.Add(record);
            object descriptor = GetMember(record, "ContentDescriptor");
            if (descriptor != null) nodes.Add(descriptor);
            descriptor = GetMember(record, "Descriptor");
            if (descriptor != null && !nodes.Contains(descriptor)) nodes.Add(descriptor);

            for (int i = 0; i < nodes.Count; i++)
            {
                object node = nodes[i];
                string raw = FirstNonEmpty(
                    GetStringMember(node, "LocalizationId"),
                    GetStringMember(node, "NameLocalizationId"),
                    GetStringMember(node, "NameLocId"),
                    GetStringMember(node, "DisplayNameId"),
                    GetStringMember(node, "NameId"),
                    GetStringMember(node, "TitleId"),
                    GetStringMember(node, "DisplayName"),
                    GetStringMember(node, "Name"),
                    GetStringMember(node, "Title"));
                if (string.IsNullOrEmpty(raw)) continue;

                string localized = LocalizeCandidates(new string[] { raw }, raw);
                if (!string.IsNullOrEmpty(localized) &&
                    !string.Equals(localized, raw, StringComparison.OrdinalIgnoreCase))
                    return NormalizeGameText(CleanLootDisplayName(localized));

                // Some records already carry localized text instead of a localization key.
                if (!IsRussian() || ContainsCyrillic(raw))
                    return NormalizeGameText(CleanLootDisplayName(raw));
            }
            return string.Empty;
        }

        private static string LootContainerFallback(string id, string humanized)
        {
            string key = (id ?? string.Empty).Trim().ToLowerInvariant();
            string uiKey = string.Empty;
            if (key == "common_box") uiKey = "loot.container.common_box";
            else if (key == "common_container") uiKey = "loot.container.common_container";
            else if (key == "common_rack") uiKey = "loot.container.common_rack";
            else if (key == "common_locker") uiKey = "loot.container.common_locker";
            else if (key == "cloth_locker") uiKey = "loot.container.cloth_locker";
            else if (key == "wooden_box") uiKey = "loot.container.wooden_box";
            else if (key == "industry_container_value") uiKey = "loot.container.industry";
            else if (key == "science_container_value") uiKey = "loot.container.science";
            else if (key == "medical_container") uiKey = "loot.container.medical";
            else if (key == "medical_case") uiKey = "loot.container.medical_case";
            else if (key == "medical_holder") uiKey = "loot.container.medical_holder";
            else if (key == "weapon_case_big") uiKey = "loot.container.weapon_case_big";
            else if (key == "weapon_case_small") uiKey = "loot.container.weapon_case_small";
            else if (key == "elite_weapon_case") uiKey = "loot.container.elite_weapon_case";
            else if (key == "weapon_stand") uiKey = "loot.container.weapon_stand";
            else if (key == "ammo_case") uiKey = "loot.container.ammo_case";
            else if (key == "data_container") uiKey = "loot.container.data";
            else if (key == "server_container") uiKey = "loot.container.server";
            else if (key == "tool_case") uiKey = "loot.container.tool_case";
            else if (key == "trash_can") uiKey = "loot.container.trash_can";
            else if (key == "armor_locker") uiKey = "loot.container.armor_locker";
            else if (key == "fastfood_container") uiKey = "loot.container.food";
            else if (key == "flowers_container") uiKey = "loot.container.plants";
            else if (key == "aztec_chest") uiKey = "loot.container.aztec_chest";
            else if (key == "matrix_box") uiKey = "loot.container.matrix_box";
            else if (key == "gas_barrel") uiKey = "loot.container.gas_barrel";
            else if (key == "toxic_barrel") uiKey = "loot.container.toxic_barrel";
            else if (key == "water_sink") uiKey = "loot.container.water_sink";
            else if (key == "blood_sink") uiKey = "loot.container.blood_sink";
            else if (key == "water_toilet") uiKey = "loot.container.toilet";
            else if (key == "water_tank") uiKey = "loot.container.water_tank";
            else if (key == "corpsepile") uiKey = "loot.container.corpse_pile";
            else if (key == "stationstash") uiKey = "loot.container.station_stash";
            else if (key == "airdropcapsule") uiKey = "loot.container.airdrop_capsule";
            else if (key == "autonomouscapsule") uiKey = "loot.container.autonomous_capsule";
            else if (key == "aztecaltar") uiKey = "loot.container.aztec_altar";
            else if (key == "snowman") uiKey = "loot.container.snowman";
            else if (key == "aed_case") uiKey = "loot.container.aed_case";
            else if (key == "extinguisher_holder") uiKey = "loot.container.extinguisher_holder";
            else if (key == "watermelon_growbox") uiKey = "loot.container.watermelon_growbox";
            else if (key == "cabbage_growbox") uiKey = "loot.container.cabbage_growbox";
            else if (key == "silence_casket") uiKey = "loot.container.silence_casket";
            return string.IsNullOrEmpty(uiKey) ? humanized : Ui(uiKey);
        }

        private static string LootStationTypeFallback(string id, string humanized)
        {
            string key = (id ?? string.Empty).Replace("_", string.Empty).Replace(" ", string.Empty).ToLowerInvariant();
            string uiKey = string.Empty;
            if (key == "industrial") uiKey = "loot.station_type.industrial";
            else if (key == "lab") uiKey = "loot.station_type.lab";
            else if (key == "mine") uiKey = "loot.station_type.mine";
            else if (key == "military") uiKey = "loot.station_type.military";
            else if (key == "prison") uiKey = "loot.station_type.prison";
            else if (key == "colonyfarm") uiKey = "loot.station_type.colony_farm";
            else if (key == "civilian") uiKey = "loot.station_type.civilian";
            else if (key == "quasimorphictemple") uiKey = "loot.station_type.quasimorphic_temple";
            else if (key == "spacestation") uiKey = "loot.station_type.space_station";
            else if (key == "bramfaturian") uiKey = "loot.station_type.bramfatura";
            return string.IsNullOrEmpty(uiKey) ? humanized : Ui(uiKey);
        }

        private static string ResolveLootContainerName(string id)
        {
            if (string.IsNullOrEmpty(id))
                return Ui("ui.container_2");

            string cacheKey = (_localizationCacheLanguage ?? string.Empty) + "|container|" + id;
            string cached;
            if (LootDisplayNameCache.TryGetValue(cacheKey, out cached)) return cached;

            string fromRecord = ResolveLootRecordLocalizedName("ObstacleContainers", id);
            if (!string.IsNullOrEmpty(fromRecord))
            {
                LootDisplayNameCache[cacheKey] = fromRecord;
                return fromRecord;
            }

            string localized = LocalizeCandidates(
                new string[]
                {
                    "container." + id + ".name",
                    "obstaclecontainer." + id + ".name",
                    "obstacle_container." + id + ".name",
                    "mapobstacle." + id + ".name",
                    "obstacle." + id + ".name",
                    "container." + id,
                    "obstacle." + id,
                    id
                },
                id);

            if (!string.IsNullOrEmpty(localized) &&
                !string.Equals(localized, id, StringComparison.OrdinalIgnoreCase))
            {
                string result = NormalizeGameText(CleanLootDisplayName(localized));
                LootDisplayNameCache[cacheKey] = result;
                return result;
            }

            if (LooksLikeGuid(id))
            {
                string result = (Ui("ui.container_3")) + ShortStableId(id);
                LootDisplayNameCache[cacheKey] = result;
                return result;
            }

            string humanized = NormalizeGameText(CleanLootDisplayName(HumanizeLootIdentifier(id)));
            string fallback = LootContainerFallback(id, humanized);
            LootDisplayNameCache[cacheKey] = fallback;
            return fallback;
        }

        private static string ResolveLootContainerSourceLabel(LootContainerSource source)
        {
            if (source == null) return Ui("ui.container_2");
            string name = ResolveLootContainerName(source.ContainerId);
            if (!string.IsNullOrEmpty(source.DropId) &&
                LootMultiProfilePhysicalContainerIds.Contains(source.ContainerId))
                return name + " [" + source.DropId + "]";
            return name;
        }

        private static string ResolveLootSourceName(string id, string kind)
        {
            if (string.IsNullOrEmpty(id)) return string.Empty;

            string cacheKey = (_localizationCacheLanguage ?? string.Empty) + "|source|" + (kind ?? string.Empty) + "|" + id;
            string cached;
            if (LootDisplayNameCache.TryGetValue(cacheKey, out cached)) return cached;

            if (IsRussian() && string.Equals(kind, "MobClass", StringComparison.OrdinalIgnoreCase))
            {
                string exactRussianMob = ResolveExactRussianMobName(id);
                if (!string.IsNullOrEmpty(exactRussianMob))
                {
                    LootDisplayNameCache[cacheKey] = exactRussianMob;
                    return exactRussianMob;
                }
            }

            string collectionName = string.Empty;
            if (string.Equals(kind, "MobClass", StringComparison.OrdinalIgnoreCase)) collectionName = "MobClasses";
            else if (string.Equals(kind, "Bramfatura", StringComparison.OrdinalIgnoreCase)) collectionName = "Bramfaturas";
            else if (string.Equals(kind, "StationType", StringComparison.OrdinalIgnoreCase)) collectionName = "StationTypes";

            if (!string.IsNullOrEmpty(collectionName))
            {
                string fromRecord = ResolveLootRecordLocalizedName(collectionName, id);
                if (!string.IsNullOrEmpty(fromRecord))
                {
                    LootDisplayNameCache[cacheKey] = fromRecord;
                    return fromRecord;
                }
            }

            string[] keys;
            if (string.Equals(kind, "MobClass", StringComparison.OrdinalIgnoreCase))
                keys = new string[]
                {
                    "mobclass." + id + ".name",
                    "monster." + id + ".name",
                    "creature." + id + ".name",
                    id
                };
            else if (string.Equals(kind, "Bramfatura", StringComparison.OrdinalIgnoreCase))
                keys = new string[]
                {
                    "bramfatura." + id + ".name",
                    "spaceobject." + id + ".name",
                    id
                };
            else if (string.Equals(kind, "Faction", StringComparison.OrdinalIgnoreCase))
            {
                string result = ResolveFactionDisplayName(id);
                LootDisplayNameCache[cacheKey] = result;
                return result;
            }
            else if (string.Equals(kind, "StationType", StringComparison.OrdinalIgnoreCase))
                keys = new string[]
                {
                    "stationtype." + id + ".name",
                    "station_type." + id + ".name",
                    "station." + id + ".name",
                    id
                };
            else
                keys = new string[] { id };

            string localized = LocalizeCandidates(keys, id);
            if (!string.IsNullOrEmpty(localized) &&
                !string.Equals(localized, id, StringComparison.OrdinalIgnoreCase))
            {
                string result = NormalizeGameText(CleanLootDisplayName(localized));
                LootDisplayNameCache[cacheKey] = result;
                return result;
            }

            if (LooksLikeGuid(id))
            {
                string result = (kind ?? "Source") + " " + ShortStableId(id);
                LootDisplayNameCache[cacheKey] = result;
                return result;
            }

            string humanized = NormalizeGameText(CleanLootDisplayName(HumanizeLootIdentifier(id)));
            if (string.Equals(kind, "StationType", StringComparison.OrdinalIgnoreCase))
                humanized = LootStationTypeFallback(id, humanized);
            LootDisplayNameCache[cacheKey] = humanized;
            return humanized;
        }

        private static string HumanizeLootIdentifier(string id)
        {
            if (string.IsNullOrEmpty(id)) return string.Empty;

            string raw = id.Replace("_", " ").Replace("-", " ").Trim();
            if (string.IsNullOrEmpty(raw)) return string.Empty;

            System.Text.StringBuilder builder = new System.Text.StringBuilder(raw.Length + 8);
            char previous = '\0';
            for (int i = 0; i < raw.Length; i++)
            {
                char current = raw[i];
                if (i > 0 && current != ' ' && previous != ' ' &&
                    char.IsUpper(current) &&
                    (char.IsLower(previous) || char.IsDigit(previous)))
                    builder.Append(' ');
                builder.Append(current);
                previous = current;
            }

            string value = builder.ToString().Trim();
            if (string.Equals(value, "Colonyfarm", StringComparison.OrdinalIgnoreCase))
                value = "Colony Farm";

            try
            {
                return CultureInfo.InvariantCulture.TextInfo.ToTitleCase(value.ToLowerInvariant());
            }
            catch
            {
                return value;
            }
        }

        private static string CleanLootDisplayName(string value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            string clean = value.Trim();
            if (clean.EndsWith(" Value", StringComparison.OrdinalIgnoreCase))
                clean = clean.Substring(0, clean.Length - 6).TrimEnd();
            if (clean.EndsWith(" Record", StringComparison.OrdinalIgnoreCase))
                clean = clean.Substring(0, clean.Length - 7).TrimEnd();
            return clean;
        }

        private static string FormatLootPercent(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value)) return "—";
            value = Mathf.Clamp(value, 0f, 100f);
            if (value >= 10f)
                return value.ToString("0.#", CultureInfo.InvariantCulture) + "%";
            if (value >= 1f)
                return value.ToString("0.##", CultureInfo.InvariantCulture) + "%";
            return value.ToString("0.###", CultureInfo.InvariantCulture) + "%";
        }
    }
}
