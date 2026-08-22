using System;
using System.Collections.Generic;
using System.Globalization;
using MGSC;

namespace ItemIntelligence
{
    public static partial class ModMain
    {
        // Read-only container probability projection. Base and Marauder paths are
        // independent vanilla draws; current-save Tech contexts are averaged only
        // after each context's nonlinear at-least-once chance has been resolved.
        private static bool TryGetExactContainerBonusEligibility(
            string itemId,
            out bool eligible)
        {
            eligible = false;
            object raw;
            if (string.IsNullOrEmpty(itemId) || !ItemRecordsById.TryGetValue(itemId, out raw))
                return false;
            CompositeItemRecord composite = raw as CompositeItemRecord;
            if (composite == null || (composite.PrimaryRecord as ItemRecord) == null)
                return false;

            TrashRecord trash = composite.GetRecord<TrashRecord>();
            if (trash == null)
            {
                eligible = true;
                return true;
            }

            object rawSubtype = GetMember(trash, "SubType");
            int subtype;
            if (rawSubtype != null && rawSubtype.GetType().IsEnum)
                subtype = Convert.ToInt32(rawSubtype, CultureInfo.InvariantCulture);
            else if (!TryToInt(rawSubtype, out subtype))
                return false;
            eligible = subtype != 2;
            return true;
        }

        private static string FormatLootContainerEffectiveChance(
            string itemId,
            LootContainerSource source,
            LootContainerSaveEstimateSnapshot snapshot,
            double storageExpected)
        {
            if (source == null || !source.RollRangeResolved ||
                source.MinRolls < 0 || source.MaxRolls < source.MinRolls ||
                snapshot == null || !snapshot.Available)
                return "—";

            LootContainerWeightedPool pool;
            if (!LootContainerWeightedPoolsByContext.TryGetValue(
                    BuildLootContainerPoolContextKey(source.DropId, source.BiomeId), out pool) ||
                pool == null || !pool.SchemaResolved || pool.Entries.Count == 0)
                return "—";

            List<int> techContexts = snapshot.GetTechContexts(source.BiomeId);
            if (techContexts == null || techContexts.Count == 0) return "—";

            bool modifierKnown = storageExpected >= 0.0;
            double bonusExpected = modifierKnown ? storageExpected : 0.0;
            double minimum;
            double maximum;
            if (!TryAverageContainerChance(
                    itemId, pool, techContexts, source.MinRolls, bonusExpected, out minimum) ||
                !TryAverageContainerChance(
                    itemId, pool, techContexts, source.MaxRolls, bonusExpected, out maximum))
                return "—";

            bool ru = IsRussian();
            string minText = FormatContainerEstimateNumber(minimum * 100.0, ru);
            string maxText = FormatContainerEstimateNumber(maximum * 100.0, ru);
            string percentSuffix = ru ? " %" : "%";
            string estimate = string.Equals(minText, maxText, StringComparison.Ordinal) ||
                Math.Abs(maximum - minimum) < 0.0000005
                ? "≈ " + maxText + percentSuffix
                : "≈ " + minText + "–" + maxText + percentSuffix;
            return modifierKnown ? estimate : estimate + "*";
        }

        private static bool TryAverageContainerChance(
            string itemId,
            LootContainerWeightedPool pool,
            List<int> techContexts,
            int baseRolls,
            double bonusExpected,
            out double average)
        {
            average = 0.0;
            int targetTech;
            if (string.IsNullOrEmpty(itemId) || pool == null || techContexts == null ||
                techContexts.Count == 0 || baseRolls < 0 || bonusExpected < 0.0 ||
                double.IsNaN(bonusExpected) || double.IsInfinity(bonusExpected) ||
                !TryGetExactContainerItemTechLevel(itemId, out targetTech))
                return false;

            for (int i = 0; i < techContexts.Count; i++)
            {
                double basePerRoll;
                if (!TryResolveContainerPerRollChance(
                        itemId, targetTech, pool.Entries, techContexts[i], false, out basePerRoll))
                    return false;
                double baseChance = baseRolls == 0
                    ? 0.0
                    : 1.0 - Math.Pow(1.0 - basePerRoll, baseRolls);

                double bonusChance = 0.0;
                if (bonusExpected > 0.0)
                {
                    double bonusPerRoll;
                    if (!TryResolveContainerPerRollChance(
                            itemId, targetTech, pool.Entries, techContexts[i], true, out bonusPerRoll))
                        return false;
                    // Same floor-plus-fraction integration as the audited corpse bonus;
                    // this computes the distribution without touching gameplay RNG.
                    bonusChance = CorpseBonusAtLeastOnceChance(bonusPerRoll, bonusExpected);
                }
                average += 1.0 - (1.0 - baseChance) * (1.0 - bonusChance);
            }
            average /= techContexts.Count;
            return !double.IsNaN(average) && !double.IsInfinity(average);
        }

        private static bool TryResolveContainerPerRollChance(
            string itemId,
            int targetTech,
            List<LootWeightedItem> entries,
            int contextTech,
            bool bonusPool,
            out double chance)
        {
            chance = 0.0;
            if (targetTech > contextTech) return true;
            if (entries == null || entries.Count == 0) return false;

            if (bonusPool)
            {
                bool targetSeen = false;
                bool targetEligible = false;
                for (int i = 0; i < entries.Count; i++)
                {
                    LootWeightedItem entry = entries[i];
                    if (entry == null || entry.TechLevel > contextTech ||
                        !string.Equals(entry.ItemId, itemId, StringComparison.OrdinalIgnoreCase))
                        continue;
                    targetSeen = true;
                    if (!entry.BonusEligibilityResolved) return false;
                    if (entry.BonusEligible) targetEligible = true;
                }
                if (!targetSeen) return false;
                if (!targetEligible) return true;
            }

            double totalWeight = 0.0;
            double targetWeight = 0.0;
            for (int i = 0; i < entries.Count; i++)
            {
                LootWeightedItem entry = entries[i];
                if (entry == null || !entry.TechResolved) return false;
                if (entry.TechLevel > contextTech) continue;
                if (bonusPool)
                {
                    if (!entry.BonusEligibilityResolved) return false;
                    if (!entry.BonusEligible) continue;
                }
                if (entry.Weight <= 0.0 || double.IsNaN(entry.Weight) ||
                    double.IsInfinity(entry.Weight))
                    return false;

                totalWeight += entry.Weight;
                if (string.Equals(entry.ItemId, itemId, StringComparison.OrdinalIgnoreCase))
                    targetWeight += entry.Weight;
            }

            if (totalWeight <= 0.0 || targetWeight <= 0.0 ||
                double.IsNaN(totalWeight) || double.IsInfinity(totalWeight))
                return false;
            chance = Math.Max(0.0, Math.Min(1.0, targetWeight / totalWeight));
            return true;
        }

        private static string FormatContainerEstimateNumber(double percent, bool russian)
        {
            if (double.IsNaN(percent) || double.IsInfinity(percent)) return "—";
            percent = Math.Max(0.0, Math.Min(100.0, percent));
            string text;
            if (percent > 0.0 && percent < 0.01) text = "<0.01";
            else if (percent >= 1.0) text = percent.ToString("0.#", CultureInfo.InvariantCulture);
            else text = percent.ToString("0.##", CultureInfo.InvariantCulture);
            return russian ? text.Replace('.', ',') : text;
        }
    }
}
