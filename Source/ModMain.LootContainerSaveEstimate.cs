using System;
using System.Collections.Generic;
using System.Globalization;
using MGSC;
using UnityEngine;

namespace ItemIntelligence
{
    public static partial class ModMain
    {
        // Save-aware manual-container context owner. This path is read-only and never
        // advances vanilla RNG; chance math and perk projection live in the paired owner.
        private sealed class LootContainerWeightedPool
        {
            public readonly List<LootWeightedItem> Entries;
            public readonly bool SchemaResolved;

            public LootContainerWeightedPool(List<LootWeightedItem> entries, bool schemaResolved)
            {
                Entries = entries == null
                    ? new List<LootWeightedItem>()
                    : new List<LootWeightedItem>(entries);
                SchemaResolved = schemaResolved;
            }
        }

        private sealed class LootContainerSaveEstimateSnapshot
        {
            public readonly Dictionary<string, List<int>> MissionTechByStationType =
                new Dictionary<string, List<int>>(StringComparer.OrdinalIgnoreCase);
            public readonly List<int> EnabledFactionTech = new List<int>();
            public int MissionContexts;
            public int FactionContexts;

            public bool Available
            {
                get { return MissionContexts > 0 || FactionContexts > 0; }
            }

            public List<int> GetTechContexts(string stationTypeId)
            {
                List<int> matching;
                if (!string.IsNullOrEmpty(stationTypeId) &&
                    MissionTechByStationType.TryGetValue(stationTypeId, out matching) &&
                    matching != null && matching.Count > 0)
                    return matching;
                return EnabledFactionTech;
            }
        }

        private static readonly Dictionary<string, LootContainerWeightedPool>
            LootContainerWeightedPoolsByContext =
                new Dictionary<string, LootContainerWeightedPool>(StringComparer.OrdinalIgnoreCase);
        private static LootContainerSaveEstimateSnapshot _lootContainerSaveEstimateSnapshot;
        private static int _lootContainerSaveEstimateFrame = -1000;
        private static bool _lootContainerSaveEstimateUnavailableLogged;
        private static bool _lootContainerSaveEstimateAvailableLogged;

        private static void ResetLootContainerSaveEstimateIndex()
        {
            LootContainerWeightedPoolsByContext.Clear();
            _lootContainerSaveEstimateSnapshot = null;
            _lootContainerSaveEstimateFrame = -1000;
            _lootContainerSaveEstimateUnavailableLogged = false;
            _lootContainerSaveEstimateAvailableLogged = false;
        }

        private static string BuildLootContainerPoolContextKey(string dropId, string stationTypeId)
        {
            return (dropId ?? string.Empty) + "\u001F" + (stationTypeId ?? string.Empty);
        }

        private static bool TryGetExactContainerItemTechLevel(string itemId, out int techLevel)
        {
            techLevel = 0;
            object raw;
            if (string.IsNullOrEmpty(itemId) || !ItemRecordsById.TryGetValue(itemId, out raw)) return false;
            CompositeItemRecord composite = raw as CompositeItemRecord;
            ItemRecord primary = composite == null ? null : composite.PrimaryRecord as ItemRecord;
            if (primary == null) return false;
            techLevel = Math.Max(0, primary.TechLevel);
            return true;
        }

        private static void RecordLootContainerWeightedPool(
            string dropId,
            string stationTypeId,
            List<LootWeightedItem> entries,
            bool schemaResolved)
        {
            if (string.IsNullOrEmpty(dropId) || string.IsNullOrEmpty(stationTypeId)) return;
            LootContainerWeightedPoolsByContext[
                BuildLootContainerPoolContextKey(dropId, stationTypeId)] =
                    new LootContainerWeightedPool(entries, schemaResolved);
        }

        private static LootContainerSaveEstimateSnapshot GetLootContainerSaveEstimateSnapshot()
        {
            int frame = Time.frameCount;
            if (_lootContainerSaveEstimateSnapshot != null &&
                frame >= _lootContainerSaveEstimateFrame &&
                frame - _lootContainerSaveEstimateFrame < 30)
                return _lootContainerSaveEstimateSnapshot;

            _lootContainerSaveEstimateFrame = frame;
            _lootContainerSaveEstimateSnapshot = BuildLootContainerSaveEstimateSnapshot();
            LootContainerSaveEstimateSnapshot snapshot = _lootContainerSaveEstimateSnapshot;
            if (snapshot.Available && !_lootContainerSaveEstimateAvailableLogged)
            {
                _lootContainerSaveEstimateAvailableLogged = true;
                Debug.Log("[ItemIntelligence][ContainerSaveEstimate] neutralTechContext=true, RNG=untouched, " +
                    "missionContexts=" + snapshot.MissionContexts.ToString(CultureInfo.InvariantCulture) +
                    ", enabledFactionContexts=" + snapshot.FactionContexts.ToString(CultureInfo.InvariantCulture) +
                    ", missionPointBudget=excluded.");
            }
            else if (!snapshot.Available && !_lootContainerSaveEstimateUnavailableLogged)
            {
                _lootContainerSaveEstimateUnavailableLogged = true;
                Debug.LogWarning("[ItemIntelligence][ContainerSaveEstimate] current-save Tech contexts unavailable; estimates fail closed.");
            }
            return snapshot;
        }

        private static LootContainerSaveEstimateSnapshot BuildLootContainerSaveEstimateSnapshot()
        {
            LootContainerSaveEstimateSnapshot snapshot = new LootContainerSaveEstimateSnapshot();
            if (!IsContainerSaveEstimateContractVerified())
                return snapshot;

            Factions factions = _factionsState as Factions;
            if (factions == null) factions = ResolveStateModule(typeof(Factions)) as Factions;
            if (factions == null) return snapshot;
            _factionsState = factions;

            CollectEnabledFactionTechContexts(snapshot);

            Stations stations = _stationsState as Stations;
            if (stations == null) stations = ResolveStateModule(typeof(Stations)) as Stations;
            object missionsState = ResolveTradeMissionsState();
            DateTime? now = GetTradeDateTimeMember(ResolveTradeSpaceTimeState(), "Time");
            if (stations == null || missionsState == null || !now.HasValue) return snapshot;
            _stationsState = stations;

            object values = GetMember(missionsState, "Values");
            if (values == null) return snapshot;
            List<DataEntry> entries = EnumerateData(values);
            for (int i = 0; i < entries.Count; i++)
            {
                try
                {
                    Mission mission = entries[i] == null ? null : entries[i].Value as Mission;
                    // Story missions can replace the equipment faction by current stage.
                    // That stage is not known from the space-side mission row, so exclude
                    // story rows; the snapshot falls back only when no procedural row matches.
                    if (mission == null || mission.IsStoryMission || mission.IsBlocked ||
                        mission.ExpireTime <= now.Value)
                        continue;
                    if (string.IsNullOrEmpty(mission.StationId) ||
                        string.IsNullOrEmpty(mission.VictimFactionId))
                        continue;

                    Station station = stations.Get(mission.StationId, true);
                    Faction victim = factions.Get(mission.VictimFactionId, true);
                    if (station == null || station.Record == null || victim == null) continue;
                    string stationTypeId = station.Record.StationType;
                    int tech = Math.Max(mission.MinTechLevel, victim.CurrentTechLevel);
                    if (string.IsNullOrEmpty(stationTypeId) || tech < 0) continue;

                    List<int> stationTech;
                    if (!snapshot.MissionTechByStationType.TryGetValue(stationTypeId, out stationTech))
                    {
                        stationTech = new List<int>();
                        snapshot.MissionTechByStationType[stationTypeId] = stationTech;
                    }
                    stationTech.Add(tech);
                    snapshot.MissionContexts++;
                }
                catch (Exception ex)
                {
                    LogRuntimeBoundaryWarningOnce(
                        "loot.container.save-context",
                        "One mission could not be included in the neutral container estimate.",
                        ex);
                }
            }
            return snapshot;
        }

        private static void CollectEnabledFactionTechContexts(
            LootContainerSaveEstimateSnapshot snapshot)
        {
            object factionRecords = GetStaticMember(typeof(Data), "Factions");
            if (snapshot == null || factionRecords == null) return;

            List<DataEntry> entries = EnumerateData(factionRecords);
            HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < entries.Count; i++)
            {
                DataEntry entry = entries[i];
                if (entry == null) continue;
                string factionId = FirstNonEmpty(
                    GetStringMember(entry.Value, "Id"),
                    GetStringMember(entry.Value, "FactionId"),
                    entry.Key);
                if (string.IsNullOrEmpty(factionId) || !seen.Add(factionId)) continue;
                if (ResolveFactionAvailabilityForCurrentSave(factionId) != 1) continue;

                Faction faction = ResolveFactionById(factionId) as Faction;
                if (faction == null || faction.CurrentTechLevel < 0) continue;
                snapshot.EnabledFactionTech.Add(faction.CurrentTechLevel);
                snapshot.FactionContexts++;
            }
        }

    }
}
