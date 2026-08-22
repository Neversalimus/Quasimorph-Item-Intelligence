using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using MGSC;
using UnityEngine;

namespace ItemIntelligence
{
    public static partial class ModMain
    {
        private static readonly Dictionary<string, List<string>> StationProductionRewardFactionsByItem =
            new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        private static long _stationProductionRewardFingerprint = long.MinValue;
        private static float _stationProductionRewardNextRefreshTime;

        private static void ResetStationProductionRewardIndex()
        {
            StationProductionRewardFactionsByItem.Clear();
            _stationProductionRewardFingerprint = long.MinValue;
            _stationProductionRewardNextRefreshTime = 0f;
        }

        // Final current-build source-family closure from the V3 IL audit.
        // These paths are acquisition semantics, not generic "possible item" heuristics.
        private static void BuildAuditedRewardAndByproductSources()
        {
            BuildUseByproductSources();
            BuildDeathGiftSource();
            BuildFactionMissionRewardPoolSources();
            BuildRandomStartingRewardPoolSources();
        }

        private static void BuildUseByproductSources()
        {
            foreach (string sourceItemId in KnownItemIds)
            {
                object record = FindLootItemRecord(sourceItemId);
                if (record == null) continue;
                string garbageItemId = GetStringMember(record, "GarbageItemId");
                if (string.IsNullOrEmpty(garbageItemId)) continue;

                AddLootSpecialSource(
                    garbageItemId,
                    sourceItemId,
                    "UseByproduct",
                    string.Empty,
                    true);
            }
        }

        private static void BuildDeathGiftSource()
        {
            object global = null;
            try { global = Data.Global; }
            catch { global = null; }
            if (global == null) return;

            string itemId = GetStringMember(global, "DeathGiftId");
            AddLootSpecialSource(
                itemId,
                "DeathGift",
                "DeathGift",
                string.Empty,
                true);
        }

        private static IEnumerable InvokeFactionRewardTradeItems(
            object runtimeFaction,
            int techLevel,
            object categoryValue)
        {
            if (runtimeFaction == null || categoryValue == null ||
                _factionGetTradeItemsMethod == null || _factionDropCollection == null)
                return null;

            try
            {
                // MissionFactory / OrbitEventScenario call the 3-arg wrapper, which passes
                // Faction.CurrentTechLevel into this exact 4-arg overload. We intentionally
                // use the explicit-tech overload at maxTech to build potential T+ membership,
                // while preserving vanilla reward-mode=true semantics.
                object raw = _factionGetTradeItemsMethod.Invoke(
                    _factionDropCollection,
                    new object[] { runtimeFaction, techLevel, categoryValue, true });
                if (raw is string) return null;
                return raw as IEnumerable;
            }
            catch { return null; }
        }

        private static void BuildFactionMissionRewardPoolSources()
        {
            object factions = GetStaticMember(typeof(Data), "Factions");
            List<DataEntry> entries = EnumerateData(factions);
            if (entries == null || entries.Count == 0) return;

            int maxTech = 10;
            try
            {
                if (Data.Global != null) maxTech = Math.Max(0, Data.Global.MaxTechLevel);
            }
            catch { maxTech = 10; }

            Dictionary<string, int> minTechByRoute =
                new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            Dictionary<string, string> routeItem =
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            Dictionary<string, string> routeFaction =
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            Dictionary<string, string> routeKind =
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < entries.Count; i++)
            {
                DataEntry entry = entries[i];
                if (entry == null || entry.Value == null) continue;

                string factionId = FirstNonEmpty(
                    GetStringMember(entry.Value, "Id"),
                    GetStringMember(entry.Value, "FactionId"),
                    entry.Key);
                if (string.IsNullOrEmpty(factionId)) continue;

                object runtimeFaction = ResolveFactionById(factionId);
                if (runtimeFaction == null || !EnsureFactionRewardApi(runtimeFaction))
                    continue;

                bool useGeneralRewards = GetBoolMember(entry.Value, "UseGeneralRewards") == true;

                string[] categories = useGeneralRewards
                    ? new string[]
                    {
                        "Equipment", "Chips", "Consumables",
                        "GeneralEquipment", "GeneralConsumables"
                    }
                    : new string[] { "Equipment", "Chips", "Consumables" };

                for (int c = 0; c < categories.Length; c++)
                {
                    string categoryName = categories[c];
                    object categoryValue = ParseFactionTradeCategory(categoryName);
                    if (categoryValue == null) continue;

                    IEnumerable records = InvokeFactionRewardTradeItems(
                        runtimeFaction, maxTech, categoryValue);
                    if (records == null) continue;

                    string kind = string.Equals(
                        categoryName, "Chips", StringComparison.OrdinalIgnoreCase)
                        ? "FactionMissionReward"
                        : "FactionMissionOrbitReward";

                    int scanned = 0;
                    foreach (object record in records)
                    {
                        if (++scanned > 4096) break;
                        if (record == null) continue;

                        int tech = 0;
                        int parsedTech;
                        if (TryToInt(GetMember(record, "TechLevel"), out parsedTech))
                            tech = Math.Max(0, parsedTech);

                        foreach (string itemId in ExtractStableStringSet(
                            GetMember(record, "ContentIds")))
                        {
                            if (string.IsNullOrEmpty(itemId) || !KnownItemIds.Contains(itemId))
                                continue;

                            string routeKey =
                                itemId + "\u001f" + factionId + "\u001f" + kind;
                            int existing;
                            if (!minTechByRoute.TryGetValue(routeKey, out existing) ||
                                tech < existing)
                            {
                                minTechByRoute[routeKey] = tech;
                                routeItem[routeKey] = itemId;
                                routeFaction[routeKey] = factionId;
                                routeKind[routeKey] = kind;
                            }
                        }
                    }
                }
            }

            foreach (KeyValuePair<string, int> pair in minTechByRoute)
            {
                string routeKey = pair.Key;
                AddLootSpecialSource(
                    routeItem[routeKey],
                    routeFaction[routeKey],
                    routeKind[routeKey],
                    pair.Value.ToString(CultureInfo.InvariantCulture),
                    false);
            }
        }

        private static void BuildRandomStartingRewardPoolSources()
        {
            // V3 proves random starting equipment reads the two General_* reward pools
            // directly at tech bucket 10. Keep this literal current-build path hash-gated.
            if (!IsAuditedSourceFamilyContractVerified()) return;

            object drop = null;
            try { drop = Data.FactionDrop; }
            catch { drop = null; }
            if (drop == null) return;

            MethodInfo getRawData = null;
            try
            {
                getRawData = drop.GetType().GetMethod(
                    "GetRawData",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                    null,
                    new Type[] { typeof(string) },
                    null);
            }
            catch { getRawData = null; }
            if (getRawData == null) return;

            string[] keys = new string[]
            {
                "General_rewardEquipment",
                "General_rewardConsumables"
            };

            for (int k = 0; k < keys.Length; k++)
            {
                object raw = null;
                try { raw = getRawData.Invoke(drop, new object[] { keys[k] }); }
                catch { raw = null; }

                IDictionary dict = raw as IDictionary;
                if (dict == null || !dict.Contains(10)) continue;
                IEnumerable records = dict[10] as IEnumerable;
                if (records == null) continue;

                int scanned = 0;
                foreach (object record in records)
                {
                    if (++scanned > 4096) break;
                    foreach (string itemId in ExtractStableStringSet(
                        GetMember(record, "ContentIds")))
                    {
                        AddLootSpecialSource(
                            itemId,
                            "RandomStartingEquipment",
                            "RandomStartingLoadout",
                            string.Empty,
                            false);
                    }
                }
            }
        }

        private static void AppendCurrentStationProductionMissionSources(
            string itemId,
            List<LootSpecialSource> output)
        {
            if (string.IsNullOrEmpty(itemId) || output == null) return;
            EnsureStationProductionRewardIndexCurrent();

            List<string> factionIds;
            if (!StationProductionRewardFactionsByItem.TryGetValue(itemId, out factionIds) ||
                factionIds == null) return;
            for (int i = 0; i < factionIds.Count; i++)
            {
                string factionId = factionIds[i];
                if (!string.IsNullOrEmpty(factionId))
                    output.Add(new LootSpecialSource(
                        factionId, "StationProductionMissionReward", string.Empty, false));
            }
        }

        private static void EnsureStationProductionRewardIndexCurrent()
        {
            // CurrentReceipts can change during play, but rescanning every station on every
            // item switch is unnecessary. Keep UI freshness sub-second-ish while amortizing
            // the global station walk across rapid browser navigation.
            float now = Time.realtimeSinceStartup;
            if (_stationProductionRewardFingerprint != long.MinValue &&
                now < _stationProductionRewardNextRefreshTime)
                return;
            _stationProductionRewardNextRefreshTime = now + 1.0f;

            List<object> stations = GetRuntimeStationsLightweight();
            if (stations == null || stations.Count == 0)
            {
                ResetStationProductionRewardIndex();
                return;
            }

            List<KeyValuePair<string, string>> routes =
                new List<KeyValuePair<string, string>>(Math.Max(32, stations.Count * 2));
            unchecked
            {
                long hash = 1469598103934665603L;
                for (int s = 0; s < stations.Count; s++)
                {
                    object station = stations[s];
                    if (station == null) continue;
                    string factionId = GetStringMember(station, "OwnerFactionId") ?? string.Empty;
                    HashStationRewardFingerprint(ref hash, factionId);
                    foreach (string receiptId in ExtractStableStringSet(GetMember(station, "CurrentReceipts")))
                    {
                        if (string.IsNullOrEmpty(receiptId)) continue;
                        HashStationRewardFingerprint(ref hash, receiptId);
                        routes.Add(new KeyValuePair<string, string>(factionId, receiptId));
                    }
                }

                if (hash == _stationProductionRewardFingerprint) return;
                _stationProductionRewardFingerprint = hash;
            }

            StationProductionRewardFactionsByItem.Clear();
            for (int i = 0; i < routes.Count; i++)
            {
                string factionId = routes[i].Key;
                object receipt = FindLootDataRecord("BarterReceipts", routes[i].Value);
                if (receipt == null) continue;
                Dictionary<string, int> outputs = ExtractItemQuantities(GetMember(receipt, "OutputItems"));
                if (outputs == null) continue;
                foreach (string outputItemId in outputs.Keys)
                {
                    if (string.IsNullOrEmpty(outputItemId) || !KnownItemIds.Contains(outputItemId)) continue;
                    List<string> factions;
                    if (!StationProductionRewardFactionsByItem.TryGetValue(outputItemId, out factions))
                    {
                        factions = new List<string>();
                        StationProductionRewardFactionsByItem[outputItemId] = factions;
                    }
                    if (!string.IsNullOrEmpty(factionId) && !factions.Contains(factionId))
                        factions.Add(factionId);
                }
            }
        }

        private static void HashStationRewardFingerprint(ref long hash, string value)
        {
            unchecked
            {
                string text = value ?? string.Empty;
                for (int i = 0; i < text.Length; i++)
                {
                    char c = char.ToUpperInvariant(text[i]);
                    hash ^= c;
                    hash *= 1099511628211L;
                }
                hash ^= 0x1f;
                hash *= 1099511628211L;
            }
        }
    }
}
