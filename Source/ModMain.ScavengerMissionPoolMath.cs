using System;
using System.Collections.Generic;
using MGSC;

namespace ItemIntelligence
{
    public static partial class ModMain
    {
        private struct ScavengerPoolStats
        {
            public int TotalCandidates;
            public int TargetCandidates;
        }

        private static ScavengerPoolStats CountExactScavengerPool(
            ScavengerRewardClass rewardClass,
            HashSet<string> whitelist,
            int techLimit,
            string factionTag,
            string targetPrimaryId)
        {
            ScavengerPoolStats stats = new ScavengerPoolStats();
            foreach (BasePickupItemRecord raw in Data.Items.Records)
            {
                CompositeItemRecord composite = raw as CompositeItemRecord;
                if (composite == null || composite.Id == null || composite.Id.Contains("_custom")) continue;
                ItemRecord primary = composite.PrimaryRecord as ItemRecord;
                if (primary == null) continue;
                if (!MatchesScavengerRewardClass(composite, rewardClass)) continue;
                if (!IsExactScavengerCandidate(primary, whitelist, techLimit, factionTag)) continue;

                stats.TotalCandidates++;
                if (!string.IsNullOrEmpty(targetPrimaryId) &&
                    string.Equals(primary.Id, targetPrimaryId, StringComparison.Ordinal))
                    stats.TargetCandidates++;
            }
            return stats;
        }

        private static bool IsExactScavengerCandidate(ItemRecord primary, HashSet<string> whitelist, int techLimit, string factionTag)
        {
            if (primary == null || primary.Categories == null) return false;
            if (techLimit != -1 && primary.TechLevel > techLimit) return false;

            if (whitelist.Contains("Faction") && !string.IsNullOrEmpty(factionTag) && primary.Categories.Contains(factionTag))
                return true;

            for (int i = 0; i < primary.Categories.Count; i++)
            {
                string category = primary.Categories[i];
                if (!string.IsNullOrEmpty(category) && whitelist.Contains(category)) return true;
            }
            return false;
        }

        private static bool TryBuildExactScavengerWhitelist(
            IList<string> stationCategories,
            IList<string> factionCategories,
            out HashSet<string> whitelist)
        {
            whitelist = new HashSet<string>(StringComparer.Ordinal);
            if (stationCategories == null || factionCategories == null) return false;

            // Vanilla MissionFinishedByPlayer builds Dictionary<string,float> with Add(),
            // not an overwrite/merge. A duplicate category would make that vanilla path
            // throw before Scavengers selection, so QII must not invent a merged pool.
            for (int i = 0; i < stationCategories.Count; i++)
            {
                string category = stationCategories[i];
                if (category == null || !whitelist.Add(category)) return false;
            }
            for (int i = 0; i < factionCategories.Count; i++)
            {
                string category = factionCategories[i];
                if (category == null || !whitelist.Add(category)) return false;
            }
            return whitelist.Count > 0;
        }
    }
}
