using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace ItemIntelligence
{
    public static partial class ModMain
    {
        // v1.7.39-test8: AztecAltar is a real ContainerItemDrop profile but has no
        // ObstacleContainers record in the audited build. Resolve a visual only when
        // vanilla exposes an exact semantic renderer stem. No fuzzy family fallback is
        // allowed here: an unresolved altar is safer than displaying the wrong prop.
        private static Sprite TryResolveMissingRecordLootContainerIcon(string containerId)
        {
            if (!string.Equals(containerId, "AztecAltar", StringComparison.OrdinalIgnoreCase))
            {
                LootContainerIconMisses.Add(containerId);
                if (ModderMode)
                    Debug.Log("[ItemIntelligence][ContainerIconAudit] lazy id=" + containerId +
                        "; selected=<none>; reason=record-not-found.");
                return null;
            }

            EnsureLootContainerIconsResolved();
            string key = NormalizeLootContainerRendererStem("AztecAltar");
            List<LootContainerRendererSnapshot> candidates;
            if (string.IsNullOrEmpty(key) ||
                !LootContainerRenderersByStem.TryGetValue(key, out candidates) ||
                candidates == null || candidates.Count == 0)
            {
                LootContainerIconMisses.Add(containerId);
                if (ModderMode)
                    Debug.Log("[ItemIntelligence][ContainerIconAudit] lazy id=AztecAltar; " +
                        "selected=<none>; reason=exact-altar-renderer-not-loaded.");
                return null;
            }

            LootContainerRendererSnapshot best = null;
            int bestRank = int.MinValue;
            string bestSource = string.Empty;
            for (int i = 0; i < candidates.Count; i++)
            {
                LootContainerRendererSnapshot candidate = candidates[i];
                if (candidate == null || candidate.Sprite == null) continue;
                string candidateStem = NormalizeLootContainerRendererStem(candidate.ObjectName);
                string spriteStem = NormalizeLootContainerRendererStem(candidate.SpriteName);
                bool exact = string.Equals(candidateStem, key, StringComparison.Ordinal) ||
                    string.Equals(spriteStem, key, StringComparison.Ordinal);
                if (!exact) continue;

                int rank = ScoreContainerVisualStateHint(candidate.ObjectName) +
                    ScoreContainerVisualStateHint(candidate.SpriteName);
                if ((candidate.SpriteName ?? string.Empty).EndsWith("_0", StringComparison.OrdinalIgnoreCase))
                    rank += 20;
                string source = candidate.Source ?? string.Empty;
                if (best == null || rank > bestRank ||
                    (rank == bestRank && string.Compare(source, bestSource,
                        StringComparison.OrdinalIgnoreCase) < 0))
                {
                    best = candidate;
                    bestRank = rank;
                    bestSource = source;
                }
            }

            if (best == null || best.Sprite == null)
            {
                LootContainerIconMisses.Add(containerId);
                if (ModderMode)
                    Debug.Log("[ItemIntelligence][ContainerIconAudit] lazy id=AztecAltar; " +
                        "selected=<none>; reason=no-exact-altar-stem.");
                return null;
            }

            LootContainerIconsById[containerId] = best.Sprite;
            LootContainerIconSourcesById[containerId] = bestSource;
            if (ModderMode)
                Debug.Log("[ItemIntelligence][ContainerIconAudit] lazy id=AztecAltar; " +
                    "selected=" + (best.Sprite.name ?? "<unnamed>") +
                    "; source=" + bestSource + "; exactSemantic=true.");
            return best.Sprite;
        }
    }
}
