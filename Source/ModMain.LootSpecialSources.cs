using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using MGSC;
using UnityEngine;

namespace ItemIntelligence
{
    public static partial class ModMain
    {
        private sealed class LootSpecialSource
        {
            public readonly string SourceId;
            public readonly string Kind;
            public readonly string Detail;
            public readonly bool Deterministic;

            public LootSpecialSource(string sourceId, string kind, string detail, bool deterministic)
            {
                SourceId = sourceId ?? string.Empty;
                Kind = kind ?? string.Empty;
                Detail = detail ?? string.Empty;
                Deterministic = deterministic;
            }
        }

        private static readonly Dictionary<string, List<LootSpecialSource>> LootSpecialSourcesByItem =
            new Dictionary<string, List<LootSpecialSource>>(StringComparer.OrdinalIgnoreCase);
        private static bool _lootSpecialSourcesBuilt;
        private static int _lootSpecialSourceLinks;


        private static void ResetLootSpecialSourcesIndex()
        {
            LootSpecialSourcesByItem.Clear();
            _lootSpecialSourcesBuilt = false;
            _lootSpecialSourceLinks = 0;
            ResetStationProductionRewardIndex();
        }

        private static void EnsureLootSpecialSourcesIndex()
        {
            if (_lootSpecialSourcesBuilt) return;
            _lootSpecialSourcesBuilt = true;
            LootSpecialSourcesByItem.Clear();
            _lootSpecialSourceLinks = 0;

            BuildStoryMissionPrizeSources();
            BuildStartingLoadoutSources();
            BuildAnComExchangeRewardSources();
            BuildItemExpireConversionSources();
            BuildSkullRitualSources();
            BuildStoryVarsScriptedSources();
            BuildAuditedHardcodedStorySources();
            BuildAuditedRewardAndByproductSources();

            Debug.Log("[ItemIntelligence][LootSpecialSources] items=" +
                LootSpecialSourcesByItem.Count.ToString(CultureInfo.InvariantCulture) +
                ", links=" + _lootSpecialSourceLinks.ToString(CultureInfo.InvariantCulture) +
                ", hardcodedCurrentBuild=" + (IsAuditedSourceFamilyContractVerified() ? "enabled" : "disabled") + ".");
        }

        private static void AddLootSpecialSource(
            string itemId, string sourceId, string kind, string detail, bool deterministic)
        {
            if (string.IsNullOrEmpty(itemId) || !KnownItemIds.Contains(itemId)) return;
            List<LootSpecialSource> list;
            if (!LootSpecialSourcesByItem.TryGetValue(itemId, out list))
            {
                list = new List<LootSpecialSource>();
                LootSpecialSourcesByItem[itemId] = list;
            }

            // StoryScript route IDs are audited implementation details, but every one of them
            // resolves to the same player-facing source/kind/condition/result. Collapse those
            // equivalent routes here, before presentation, so a collapsed accordion section
            // stays allocation-free. All other source families preserve SourceId identity.
            bool collapsePlayerEquivalentStoryRoutes =
                string.Equals(kind, "StoryScript", StringComparison.Ordinal);
            for (int i = 0; i < list.Count; i++)
            {
                LootSpecialSource old = list[i];
                if (old != null &&
                    string.Equals(old.Kind, kind, StringComparison.Ordinal) &&
                    string.Equals(old.Detail, detail, StringComparison.Ordinal) &&
                    (collapsePlayerEquivalentStoryRoutes ||
                     string.Equals(old.SourceId, sourceId, StringComparison.OrdinalIgnoreCase)))
                    return;
            }

            list.Add(new LootSpecialSource(sourceId, kind, detail, deterministic));
            _lootSpecialSourceLinks++;
        }

        private static void BuildStoryMissionPrizeSources()
        {
            object collection = GetStaticMember(typeof(Data), "StoryMissions");
            List<DataEntry> entries = EnumerateData(collection);
            for (int i = 0; i < entries.Count; i++)
            {
                DataEntry entry = entries[i];
                if (entry == null || entry.Value == null) continue;
                string missionId = FirstNonEmpty(GetStringMember(entry.Value, "Id"), entry.Key);
                foreach (string itemId in ExtractStableStringSet(GetMember(entry.Value, "PrizeItems")))
                    AddLootSpecialSource(itemId, missionId, "StoryPrize", "", true);
            }
        }

        private static void BuildStartingLoadoutSources()
        {
            object collection = GetStaticMember(typeof(Data), "StartingItems");
            List<DataEntry> entries = EnumerateData(collection);
            for (int i = 0; i < entries.Count; i++)
            {
                DataEntry entry = entries[i];
                if (entry == null || entry.Value == null) continue;
                string presetId = FirstNonEmpty(GetStringMember(entry.Value, "Id"), entry.Key);
                foreach (string itemId in ExtractStableStringSet(GetMember(entry.Value, "Items")))
                    AddLootSpecialSource(itemId, presetId, "StartingLoadout", "", true);
            }
        }

        private static void BuildAnComExchangeRewardSources()
        {
            object collection = GetStaticMember(typeof(Data), "AnComDataRewards");
            List<DataEntry> entries = EnumerateData(collection);
            for (int i = 0; i < entries.Count; i++)
            {
                DataEntry entry = entries[i];
                if (entry == null || entry.Value == null) continue;
                string factionId = FirstNonEmpty(GetStringMember(entry.Value, "Id"), entry.Key);
                foreach (string itemId in ExtractStableStringSet(GetMember(entry.Value, "Items")))
                    AddLootSpecialSource(itemId, factionId, "AnComExchange", "", true);
            }

            object storyVars = GetStaticMember(typeof(Data), "StoryVars");
            if (storyVars == null) return;
            foreach (string itemId in ExtractStableStringSet(GetMember(storyVars, "TutorialAncomRewardItems")))
                AddLootSpecialSource(itemId, "AnCom", "AnComExchangeFallback", "", true);
        }

        private static void BuildItemExpireConversionSources()
        {
            object collection = GetStaticMember(typeof(Data), "ItemExpire");
            List<DataEntry> entries = EnumerateData(collection);
            for (int i = 0; i < entries.Count; i++)
            {
                DataEntry entry = entries[i];
                if (entry == null || entry.Value == null) continue;
                string sourceItemId = FirstNonEmpty(GetStringMember(entry.Value, "Id"), entry.Key);
                string convertedItemId = GetStringMember(entry.Value, "ConvertedItemId");
                AddLootSpecialSource(convertedItemId, sourceItemId, "ExpireConversion", "", true);
            }
        }

        private static void BuildSkullRitualSources()
        {
            foreach (string skullId in KnownItemIds)
            {
                object record = FindLootItemRecord(skullId);
                if (record == null || !string.Equals(
                    record.GetType().FullName, "MGSC.SkullRecord", StringComparison.Ordinal))
                    continue;

                AddLootSpecialSource(GetStringMember(record, "Upgrade"), skullId, "SkullRitual", "Upgrade", false);
                AddLootSpecialSource(GetStringMember(record, "Sidegrade"), skullId, "SkullRitual", "Sidegrade", false);
                AddLootSpecialSource(GetStringMember(record, "Downgrade"), skullId, "SkullRitual", "Downgrade", false);

                string essenceId = GetStringMember(record, "Essence");
                if (string.IsNullOrEmpty(essenceId)) continue;
                object essence = FindLootDataRecord("Essences", essenceId);
                if (essence == null) continue;
                AddLootSpecialSource(
                    GetStringMember(essence, "FailedRitualItemId"),
                    skullId,
                    "SkullRitual",
                    "Failed",
                    false);
            }
        }

        private static void BuildStoryVarsScriptedSources()
        {
            object storyVars = GetStaticMember(typeof(Data), "StoryVars");
            if (storyVars == null) return;

            string[] directMembers = new string[]
            {
                "SonnenAndreevDeviceId",
                "LightEipshwitzHeadId",
                "LightPhaseBombId",
                "ReloadAntiQmorphDeviceId",
                "TelegraphHelstromIndexId"
            };
            for (int i = 0; i < directMembers.Length; i++)
            {
                string itemId = GetStringMember(storyVars, directMembers[i]);
                AddLootSpecialSource(itemId, directMembers[i], "StoryScript", "", false);
            }

            string[] listMembers = new string[]
            {
                "TutorialItemsInLocker",
                "TutorialItemsOnCharacter"
            };
            for (int i = 0; i < listMembers.Length; i++)
            {
                foreach (string itemId in ExtractStableStringSet(GetMember(storyVars, listMembers[i])))
                    AddLootSpecialSource(itemId, listMembers[i], "StoryScript", "", false);
            }
        }

        private static void BuildAuditedHardcodedStorySources()
        {
            // These IDs are literal CreateForInventory/SpawnItem paths in the audited
            // 1.0.2 and 1.0.3 binaries. Unknown hashes fail closed instead of carrying
            // hardcoded story acquisition claims across an unaudited game update.
            if (!IsAuditedSourceFamilyContractVerified()) return;

            AddLootSpecialSource("quest_emp_turret", "rwa_1_covert", "StoryScript", "", false);
            AddLootSpecialSource("quest_bomb_briefcase", "rwa_story", "StoryScript", "", false);
            AddLootSpecialSource("quest_red_chip", "civ_2_spark_flame", "StoryScript", "", false);
            AddLootSpecialSource("quest_golden_skull", "tez_story", "StoryScript", "", false);
            AddLootSpecialSource("venus_spear_hmg_1", "xio_tez_story", "StoryScript", "", false);
            AddLootSpecialSource("quest_skull_xiomara", "ksiomara_story", "StoryScript", "", false);
            AddLootSpecialSource("quest_ancom_data", "tutorial", "StoryScript", "", false);
        }

        private static void AppendLootSpecialSourceLines(string itemId, ref bool any)
        {
            EnsureLootSpecialSourcesIndex();
            List<LootSpecialSource> all = new List<LootSpecialSource>();
            List<LootSpecialSource> indexed;
            if (LootSpecialSourcesByItem.TryGetValue(itemId, out indexed) &&
                indexed != null && indexed.Count > 0)
                all.AddRange(indexed);

            // Current station receipts are reverse-indexed and only rebuilt when the
            // station/receipt fingerprint changes; item switching never scans all stations.
            AppendCurrentStationProductionMissionSources(itemId, all);
            if (all.Count == 0) return;

            List<LootSpecialSource> other = new List<LootSpecialSource>();
            List<LootSpecialSource> rewards = new List<LootSpecialSource>();
            List<LootSpecialSource> starts = new List<LootSpecialSource>();
            int fixedStartLoadoutCount = 0;
            for (int i = 0; i < all.Count; i++)
            {
                LootSpecialSource source = all[i];
                if (source == null) continue;
                if (string.Equals(source.Kind, "StartingLoadout", StringComparison.Ordinal))
                {
                    // StartingItemsRecord.Id values such as High/Normal are internal data IDs,
                    // not proven player-facing localization keys. Preserve the exact number of
                    // matching fixed loadouts without exposing or inventing names for them.
                    fixedStartLoadoutCount++;
                }
                else if (string.Equals(source.Kind, "RandomStartingLoadout", StringComparison.Ordinal))
                    starts.Add(source);
                else if (string.Equals(source.Kind, "FactionMissionReward", StringComparison.Ordinal) ||
                    string.Equals(source.Kind, "FactionMissionOrbitReward", StringComparison.Ordinal) ||
                    string.Equals(source.Kind, "StationProductionMissionReward", StringComparison.Ordinal))
                    rewards.Add(source);
                else
                    other.Add(source);
            }
            if (fixedStartLoadoutCount > 0)
                starts.Insert(0, new LootSpecialSource(
                    fixedStartLoadoutCount.ToString(CultureInfo.InvariantCulture),
                    "StartingLoadoutGroup", string.Empty, true));

            AppendLootSpecialSourceGroup(Ui("loot.special.other_section"), other, ref any);
            AppendLootSpecialSourceGroup(Ui("loot.special.reward_section"), rewards, ref any);
            AppendLootSpecialSourceGroup(Ui("loot.special.start_section"), starts, ref any);
        }

        private static void AppendLootSpecialSourceGroup(
            string sectionLabel, List<LootSpecialSource> sources, ref bool any)
        {
            if (sources == null || sources.Count == 0) return;
            any = true;

            // StoryMissions can contain several internal prize routes that have no usable
            // player-facing mission title. Present those as one truthful grouped row instead
            // of repeating the same generic "Story mission" line. This count is computed
            // from raw source metadata before the lazy gate, without localization or row creation.
            int storyPrizeCount = 0;
            for (int i = 0; i < sources.Count; i++)
            {
                LootSpecialSource source = sources[i];
                if (source != null && string.Equals(source.Kind, "StoryPrize", StringComparison.Ordinal))
                    storyPrizeCount++;
            }
            int visibleRowCount = sources.Count - Math.Max(0, storyPrizeCount - 1);
            if (!AddLootSectionHeaderAndShouldBuild(sectionLabel, visibleRowCount)) return;

            // Sorting and display-string resolution stay below the accordion gate: closed
            // sections do not localize, allocate presentation rows, or sort hidden data.
            sources.Sort(delegate(LootSpecialSource a, LootSpecialSource b)
            {
                int kind = string.Compare(a == null ? string.Empty : a.Kind,
                    b == null ? string.Empty : b.Kind, StringComparison.Ordinal);
                if (kind != 0) return kind;
                return string.Compare(a == null ? string.Empty : a.SourceId,
                    b == null ? string.Empty : b.SourceId, StringComparison.CurrentCultureIgnoreCase);
            });

            string sourceHeader = Ui("ui.source");
            string kindHeader = Ui("loot.special.header_method");
            string conditionHeader = Ui("loot.special.condition");
            string resultHeader = Ui("loot.special.header_status");
            bool rewardGroup = string.Equals(
                sectionLabel, Ui("loot.special.reward_section"), StringComparison.CurrentCultureIgnoreCase);
            if (rewardGroup)
            {
                sourceHeader = Ui("loot.special.header_faction");
                kindHeader = Ui("loot.special.header_where");
                conditionHeader = Ui("loot.special.header_requirement");
            }
            else if (string.Equals(sectionLabel, Ui("loot.special.start_section"), StringComparison.CurrentCultureIgnoreCase))
            {
                sourceHeader = Ui("loot.special.header_loadout");
                kindHeader = Ui("loot.special.header_mode");
            }

            if (string.Equals(sectionLabel, Ui("loot.special.other_section"), StringComparison.CurrentCultureIgnoreCase))
                AddWrappedLootNote("loot.note.special_sources");
            else if (rewardGroup)
                AddWrappedLootNote("loot.note.faction_reward_sources");
            else if (string.Equals(sectionLabel, Ui("loot.special.start_section"), StringComparison.CurrentCultureIgnoreCase))
                AddWrappedLootNote("loot.note.starting_sources");

            BrowserLines.Add(BrowserLine.LootSpecialHeader(
                sourceHeader, kindHeader, conditionHeader, resultHeader,
                rewardGroup ? "reward_pool" : string.Empty));

            bool storyPrizeEmitted = false;
            for (int i = 0; i < sources.Count; i++)
            {
                LootSpecialSource source = sources[i];
                if (source == null) continue;

                if (string.Equals(source.Kind, "StoryPrize", StringComparison.Ordinal) && storyPrizeCount > 1)
                {
                    if (storyPrizeEmitted) continue;
                    storyPrizeEmitted = true;
                    BrowserLines.Add(BrowserLine.LootSpecialRow(
                        string.Format(CultureInfo.InvariantCulture,
                            Ui("loot.special.story_missions_count"), storyPrizeCount),
                        ResolveLootSpecialKind(source.Kind),
                        ResolveLootSpecialCondition(source),
                        ResolveLootSpecialResult(source),
                        rewardGroup ? "reward_pool" : string.Empty));
                    continue;
                }

                BrowserLines.Add(BrowserLine.LootSpecialRow(
                    ResolveLootSpecialSourceDisplay(source),
                    ResolveLootSpecialKind(source.Kind),
                    ResolveLootSpecialCondition(source),
                    ResolveLootSpecialResult(source),
                    rewardGroup ? "reward_pool" : string.Empty));
            }
        }

        private static string ResolveLootSpecialResult(LootSpecialSource source)
        {
            if (source == null) return string.Empty;
            if (string.Equals(source.Kind, "FactionMissionReward", StringComparison.Ordinal) ||
                string.Equals(source.Kind, "FactionMissionOrbitReward", StringComparison.Ordinal) ||
                string.Equals(source.Kind, "StationProductionMissionReward", StringComparison.Ordinal))
                return "eligible";
            if (string.Equals(source.Kind, "RandomStartingLoadout", StringComparison.Ordinal))
                return Ui("loot.special.result_in_pool");
            if (string.Equals(source.Kind, "SkullRitual", StringComparison.Ordinal))
                return Ui("loot.special.result_outcome");
            return source.Deterministic
                ? Ui("loot.special.result_guaranteed")
                : Ui("loot.special.result_conditional");
        }

        private static string ResolveLootSpecialSourceDisplay(LootSpecialSource source)
        {
            if (source == null) return string.Empty;
            if (string.Equals(source.Kind, "StoryScript", StringComparison.Ordinal))
                return Ui("loot.special.story_source");
            if (string.Equals(source.Kind, "AnComExchange", StringComparison.Ordinal) ||
                string.Equals(source.Kind, "AnComExchangeFallback", StringComparison.Ordinal))
                return ResolveFactionDisplayName(source.SourceId);
            if (string.Equals(source.Kind, "ExpireConversion", StringComparison.Ordinal) ||
                string.Equals(source.Kind, "SkullRitual", StringComparison.Ordinal) ||
                string.Equals(source.Kind, "UseByproduct", StringComparison.Ordinal))
                return ResolveLootSpecialItemSourceDisplay(source.SourceId);
            if (string.Equals(source.Kind, "FactionMissionReward", StringComparison.Ordinal) ||
                string.Equals(source.Kind, "FactionMissionOrbitReward", StringComparison.Ordinal) ||
                string.Equals(source.Kind, "StationProductionMissionReward", StringComparison.Ordinal))
                return ResolveFactionDisplayName(source.SourceId);
            if (string.Equals(source.Kind, "DeathGift", StringComparison.Ordinal))
                return Ui("loot.special.death_gift_source");
            if (string.Equals(source.Kind, "RandomStartingLoadout", StringComparison.Ordinal))
                return Ui("loot.special.random_start_source");
            if (string.Equals(source.Kind, "StartingLoadoutGroup", StringComparison.Ordinal))
                return Ui("loot.special.fixed_loadout_count") + ": " + source.SourceId;
            if (string.Equals(source.Kind, "StoryPrize", StringComparison.Ordinal))
            {
                string name = ResolveLootRecordLocalizedName("StoryMissions", source.SourceId);
                if (!string.IsNullOrEmpty(name)) return name;
                return Ui("loot.special.story_mission");
            }
            return HumanizeLootIdentifier(source.SourceId);
        }

        private static string ResolveLootSpecialItemSourceDisplay(string itemId)
        {
            if (string.IsNullOrEmpty(itemId)) return string.Empty;

            string localized = ResolveLootRecordLocalizedName("Items", itemId);
            if (!string.IsNullOrEmpty(localized)) return localized;

            string itemName = LocalizeItem(itemId);
            if (!string.IsNullOrEmpty(itemName) &&
                !string.Equals(itemName, itemId, StringComparison.OrdinalIgnoreCase))
                return NormalizeGameText(itemName);

            return HumanizeLootIdentifier(itemId);
        }

        private static string ResolveLootSpecialKind(string kind)
        {
            if (string.Equals(kind, "StoryPrize", StringComparison.Ordinal)) return Ui("loot.special.kind_story");
            if (string.Equals(kind, "StartingLoadout", StringComparison.Ordinal) ||
                string.Equals(kind, "StartingLoadoutGroup", StringComparison.Ordinal)) return Ui("loot.special.kind_fixed_start");
            if (string.Equals(kind, "AnComExchange", StringComparison.Ordinal) ||
                string.Equals(kind, "AnComExchangeFallback", StringComparison.Ordinal)) return Ui("loot.special.kind_secret_data");
            if (string.Equals(kind, "ExpireConversion", StringComparison.Ordinal)) return Ui("loot.special.expire_conversion");
            if (string.Equals(kind, "SkullRitual", StringComparison.Ordinal)) return Ui("loot.special.skull_ritual");
            if (string.Equals(kind, "StoryScript", StringComparison.Ordinal)) return Ui("loot.special.story_script");
            if (string.Equals(kind, "UseByproduct", StringComparison.Ordinal)) return Ui("loot.special.kind_use");
            if (string.Equals(kind, "DeathGift", StringComparison.Ordinal)) return Ui("loot.special.kind_death");
            if (string.Equals(kind, "FactionMissionReward", StringComparison.Ordinal)) return Ui("loot.special.kind_mission_only");
            if (string.Equals(kind, "FactionMissionOrbitReward", StringComparison.Ordinal)) return Ui("loot.special.kind_mission_orbit");
            if (string.Equals(kind, "RandomStartingLoadout", StringComparison.Ordinal)) return Ui("loot.special.kind_random_pool");
            if (string.Equals(kind, "StationProductionMissionReward", StringComparison.Ordinal)) return Ui("loot.special.kind_station_reward");
            return HumanizeLootIdentifier(kind);
        }

        private static string ResolveLootSpecialCondition(LootSpecialSource source)
        {
            if (source == null) return string.Empty;
            if (string.Equals(source.Kind, "StoryPrize", StringComparison.Ordinal)) return Ui("loot.special.cond_complete");
            if (string.Equals(source.Kind, "StartingLoadout", StringComparison.Ordinal) ||
                string.Equals(source.Kind, "StartingLoadoutGroup", StringComparison.Ordinal)) return Ui("loot.special.cond_fixed_start_enabled");
            if (string.Equals(source.Kind, "AnComExchange", StringComparison.Ordinal) ||
                string.Equals(source.Kind, "AnComExchangeFallback", StringComparison.Ordinal)) return Ui("loot.special.cond_exchange");
            if (string.Equals(source.Kind, "ExpireConversion", StringComparison.Ordinal)) return Ui("loot.special.cond_expire");
            if (string.Equals(source.Kind, "SkullRitual", StringComparison.Ordinal))
            {
                if (string.Equals(source.Detail, "Failed", StringComparison.Ordinal)) return Ui("loot.special.cond_ritual_failed");
                return ResolveLootRitualOutcomeLabel(source.Detail);
            }
            if (string.Equals(source.Kind, "StoryScript", StringComparison.Ordinal)) return Ui("loot.special.cond_story");
            if (string.Equals(source.Kind, "UseByproduct", StringComparison.Ordinal)) return Ui("loot.special.cond_use");
            if (string.Equals(source.Kind, "DeathGift", StringComparison.Ordinal)) return Ui("loot.special.cond_death");
            if (string.Equals(source.Kind, "FactionMissionReward", StringComparison.Ordinal) ||
                string.Equals(source.Kind, "FactionMissionOrbitReward", StringComparison.Ordinal))
            {
                int tech;
                if (int.TryParse(source.Detail, NumberStyles.Integer, CultureInfo.InvariantCulture, out tech) && tech > 0)
                    return "TECH T" + tech.ToString(CultureInfo.InvariantCulture) + "+";
                return Ui("loot.special.cond_no_extra_requirement");
            }
            if (string.Equals(source.Kind, "RandomStartingLoadout", StringComparison.Ordinal))
                return Ui("loot.special.cond_random_start_enabled");
            if (string.Equals(source.Kind, "StationProductionMissionReward", StringComparison.Ordinal))
                return Ui("loot.special.cond_station_current");
            return string.Empty;
        }

        private static string ResolveLootRitualOutcomeLabel(string detail)
        {
            if (string.Equals(detail, "Upgrade", StringComparison.Ordinal)) return Ui("loot.special.cond_ritual_upgrade");
            if (string.Equals(detail, "Sidegrade", StringComparison.Ordinal)) return Ui("loot.special.cond_ritual_sidegrade");
            if (string.Equals(detail, "Downgrade", StringComparison.Ordinal)) return Ui("loot.special.cond_ritual_downgrade");
            return Ui("loot.special.cond_ritual_outcome");
        }
    }
}
