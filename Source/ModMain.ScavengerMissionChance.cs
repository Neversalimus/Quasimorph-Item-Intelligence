using System;
using System.Collections.Generic;
using System.Globalization;
using MGSC;

namespace ItemIntelligence
{
    public static partial class ModMain
    {
        private static void BuildBrowserScavengerMissionRewards(string itemId)
        {
            CompositeItemRecord inspectedComposite;
            ItemRecord inspectedPrimary;
            ScavengerRewardClass inspectedClasses;
            if (!TryResolveScavengerRewardClass(itemId, out inspectedComposite, out inspectedPrimary, out inspectedClasses) ||
                inspectedClasses == ScavengerRewardClass.None || string.IsNullOrEmpty(inspectedPrimary.Id))
                return;

            // Exact probability semantics use a feature-owned audited assembly/API contract.
            // Unknown builds still fail closed without disabling unrelated QII features.
            if (!IsScavengerChanceContractVerified())
                return;

            TryResolveMagnumProgressionLightweight();
            MagnumProgression progression = _magnumProgression as MagnumProgression;
            if (progression == null || !progression.HasPurgeBrigadeDepartment)
                return;

            // Vanilla skips the Purge Brigade branch in Bramfatura. Missing travel state must
            // therefore hide the percentage rather than silently treating it as normal space.
            if (!TryEnsureTradeTravelState() || _tradeTravelMetadata == null) return;
            bool? inBramfatura = GetBoolMember(_tradeTravelMetadata, "IsInBramfatura");
            if (!inBramfatura.HasValue || inBramfatura.Value) return;

            object missionsState = ResolveTradeMissionsState();
            if (missionsState == null) return;

            Stations stations = _stationsState as Stations;
            if (stations == null) stations = ResolveStateModule(typeof(Stations)) as Stations;
            Factions factions = _factionsState as Factions;
            if (factions == null) factions = ResolveStateModule(typeof(Factions)) as Factions;
            if (stations == null || factions == null || Data.Items == null || Data.StationTypes == null)
                return;

            object values = GetMember(missionsState, "Values");
            if (values == null) return;
            List<DataEntry> entries = EnumerateData(values);
            if (entries == null || entries.Count == 0) return;

            List<ScavengerMissionChanceRow> rows = new List<ScavengerMissionChanceRow>();
            Dictionary<string, ScavengerPoolStats> poolStatsCache =
                new Dictionary<string, ScavengerPoolStats>(StringComparer.Ordinal);
            ResetScavengerMissionTimingSnapshot();
            if (!_scavengerMissionTimingNow.HasValue) return;

            int skippedExpired = 0;
            int skippedProxyUnknown = 0;
            int skippedWhitelistUnresolved = 0;
            for (int i = 0; i < entries.Count; i++)
            {
                Mission mission = entries[i] == null ? null : entries[i].Value as Mission;
                if (mission == null || mission.IsStoryMission || mission.IsBlocked) continue;
                if (IsScavengerMissionExpiredAtSnapshot(mission)) { skippedExpired++; continue; }
                if (string.IsNullOrEmpty(mission.StationId) || string.IsNullOrEmpty(mission.VictimFactionId)) continue;

                // ProcMissionType 12 may call ProxyCorpDepartment.CaptureFaction before
                // Scavengers rewards when the Proxy Company department is installed.
                // Its mutation contract is not part of the audited Scavengers proof, so
                // omit only that edge case rather than claiming an exact percentage.
                if ((int)mission.ProcMissionType == 12 && progression.HasProxyCompanyDepartment)
                {
                    skippedProxyUnknown++;
                    continue;
                }

                Station station = stations.Get(mission.StationId, true);
                Faction victim = factions.Get(mission.VictimFactionId, true);
                if (station == null || station.Record == null || victim == null || victim.Record == null) continue;

                string stationTypeId = station.Record.StationType;
                if (string.IsNullOrEmpty(stationTypeId)) continue;
                StationTypeRecord stationType = Data.StationTypes.GetRecord(stationTypeId, true);
                if (stationType == null) continue;

                HashSet<string> whitelist;
                if (!TryBuildExactScavengerWhitelist(stationType.ItemDropCategories, victim.Record.ItemDropCategories, out whitelist))
                {
                    skippedWhitelistUnresolved++;
                    continue;
                }

                int currentTech = victim.CurrentTechLevel;
                string contextKey = stationTypeId + "|" + victim.Id + "|" + currentTech.ToString(CultureInfo.InvariantCulture);
                double missAll = 1.0;
                int totalRolls = 0;

                AddExactScavengerClassChance(inspectedComposite, inspectedPrimary, ScavengerRewardClass.Resources,
                    Math.Max(0, (int)progression.PurgeBrigadeResourcesBonus), whitelist, currentTech, victim.Id, contextKey, poolStatsCache,
                    ref missAll, ref totalRolls);
                AddExactScavengerClassChance(inspectedComposite, inspectedPrimary, ScavengerRewardClass.ArmorWeapons,
                    Math.Max(0, (int)progression.PurgeBrigadeArmorWeaponBonus), whitelist, currentTech, victim.Id, contextKey, poolStatsCache,
                    ref missAll, ref totalRolls);
                AddExactScavengerClassChance(inspectedComposite, inspectedPrimary, ScavengerRewardClass.FoodMeds,
                    Math.Max(0, (int)progression.PurgeBrigadeFoodMedsBonus), whitelist, currentTech, victim.Id, contextKey, poolStatsCache,
                    ref missAll, ref totalRolls);
                AddExactScavengerClassChance(inspectedComposite, inspectedPrimary, ScavengerRewardClass.AmmoGrenades,
                    Math.Max(0, (int)progression.PurgeBrigadeAmmoGrenadesBonus), whitelist, currentTech, victim.Id, contextKey, poolStatsCache,
                    ref missAll, ref totalRolls);

                if (totalRolls <= 0 || missAll >= 1.0) continue;
                float chance = (float)((1.0 - missAll) * 100.0);
                ScavengerMissionChanceRow row = new ScavengerMissionChanceRow
                {
                    Station = FirstNonEmpty(GetStringMember(station, "Name"), LocalizeStation(mission.StationId), mission.StationId),
                    Opponent = ResolveFactionDisplayName(victim.Id),
                    ChancePercent = chance,
                    Rolls = totalRolls,
                    TechLevel = currentTech
                };
                PopulateScavengerMissionTiming(row, mission, station);
                rows.Add(row);
            }

            UnityEngine.Debug.Log("[ItemIntelligence][ScavengersExact] item=" + itemId +
                ", missionEntries=" + entries.Count.ToString(CultureInfo.InvariantCulture) +
                ", eligibleRows=" + rows.Count.ToString(CultureInfo.InvariantCulture) +
                ", expiredSkipped=" + skippedExpired.ToString(CultureInfo.InvariantCulture) +
                ", proxyUnknownSkipped=" + skippedProxyUnknown.ToString(CultureInfo.InvariantCulture) +
                ", whitelistUnresolvedSkipped=" + skippedWhitelistUnresolved.ToString(CultureInfo.InvariantCulture) + ".");
            AddBrowserScavengerMissionRows(rows);
        }

        private static void AddExactScavengerClassChance(
            CompositeItemRecord inspectedComposite,
            ItemRecord inspectedPrimary,
            ScavengerRewardClass rewardClass,
            int rolls,
            HashSet<string> whitelist,
            int techLimit,
            string factionTag,
            string contextKey,
            Dictionary<string, ScavengerPoolStats> poolStatsCache,
            ref double missAll,
            ref int totalRolls)
        {
            if (rolls <= 0 || !MatchesScavengerRewardClass(inspectedComposite, rewardClass)) return;
            if (!IsExactScavengerCandidate(inspectedPrimary, whitelist, techLimit, factionTag)) return;

            string cacheKey = contextKey + "|" + ((int)rewardClass).ToString(CultureInfo.InvariantCulture);
            ScavengerPoolStats stats;
            if (!poolStatsCache.TryGetValue(cacheKey, out stats))
            {
                stats = CountExactScavengerPool(rewardClass, whitelist, techLimit, factionTag, inspectedPrimary.Id);
                poolStatsCache[cacheKey] = stats;
            }
            if (stats.TotalCandidates <= 0 || stats.TargetCandidates <= 0) return;

            // Every eligible CompositeItemRecord has the same total weight (1 class +
            // max whitelist weight 1). If K eligible records resolve to this inspected
            // primary item among N eligible records, exact per-roll p is K/N, not 1/N.
            double perRollHit = (double)stats.TargetCandidates / stats.TotalCandidates;
            double perRollMiss = 1.0 - perRollHit;
            missAll *= Math.Pow(perRollMiss, rolls);
            totalRolls += rolls;
        }
    }
}
