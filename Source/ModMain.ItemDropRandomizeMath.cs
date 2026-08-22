using System;
using System.Collections;
using System.Collections.Generic;

namespace ItemIntelligence
{
    public static partial class ModMain
    {
        // Exact mirror of the non-RNG candidate math in ItemDropSystem.Randomize
        // (current audited Assembly-CSharp SHA256 EFF608C5118735359CD07FEAD8A8219E1CFB557E3A5A57517DB4428F04834B8B).
        // Keys are preserved even when their configured weight is zero/negative because
        // vanilla selector predicates use Dictionary.ContainsKey before reading the value.
        private static Dictionary<string, double> ExtractItemDropWeightMap(object value, out bool exact)
        {
            Dictionary<string, double> result =
                new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
            exact = true;
            if (value == null) return result;

            IDictionary dict = value as IDictionary;
            if (dict != null)
            {
                foreach (DictionaryEntry entry in dict)
                {
                    string key = ConvertToStableString(entry.Key);
                    double weight;
                    if (string.IsNullOrEmpty(key) || !TryToDoubleSafe(entry.Value, out weight) ||
                        double.IsNaN(weight) || double.IsInfinity(weight))
                    {
                        exact = false;
                        continue;
                    }
                    result[key] = weight;
                }
                return result;
            }

            IEnumerable enumerable = value as IEnumerable;
            if (enumerable != null && !(value is string))
            {
                foreach (object entry in enumerable)
                {
                    if (entry == null) { exact = false; continue; }
                    string key = ConvertToStableString(GetMember(entry, "Key"));
                    double weight;
                    if (string.IsNullOrEmpty(key) || !TryToDoubleSafe(GetMember(entry, "Value"), out weight) ||
                        double.IsNaN(weight) || double.IsInfinity(weight))
                    {
                        exact = false;
                        continue;
                    }
                    result[key] = weight;
                }
                return result;
            }

            exact = false;
            return result;
        }

        private static Dictionary<string, double> ExtractItemDropWeightMap(object value)
        {
            bool ignored;
            return ExtractItemDropWeightMap(value, out ignored);
        }

        // ItemDropSystem.Randomize category/faction gate:
        // * null whitelist => candidate stays eligible, category bonus 0;
        // * non-null whitelist => candidate must match either a configured category or
        //   the special "Faction" key against ItemRecord.Categories.Contains(factionTag);
        // * bonus is the maximum matched whitelist weight (starting from 0).
        private static double GetItemDropCategoryWeight(
            HashSet<string> categories,
            Dictionary<string, double> whitelist,
            bool whitelistExists,
            string factionTag,
            out bool eligible)
        {
            eligible = true;
            if (!whitelistExists) return 0.0;

            eligible = false;
            double best = 0.0;
            if (whitelist == null)
                return best;

            double factionWeight;
            if (whitelist.TryGetValue("Faction", out factionWeight) &&
                categories != null && categories.Contains(factionTag ?? string.Empty))
            {
                eligible = true;
                best = Math.Max(best, factionWeight);
            }

            if (categories != null)
            {
                foreach (string category in categories)
                {
                    double value;
                    if (!string.IsNullOrEmpty(category) && whitelist.TryGetValue(category, out value))
                    {
                        eligible = true;
                        best = Math.Max(best, value);
                    }
                }
            }

            return best;
        }


        // WeightedList/DropManager do not reject zero or negative weights. Their
        // fallback behavior then depends on candidate order, so the usual weight/total
        // probability formula is exact only when every eligible final weight is > 0.
        // Fail closed rather than silently dropping candidates and publishing a false %.
        private static bool TryResolveStrictlyPositiveItemDropTotal(
            Dictionary<string, double> weights,
            string contextKey,
            out double total)
        {
            total = 0.0;
            if (weights == null || weights.Count == 0) return false;

            int nonPositive = 0;
            foreach (KeyValuePair<string, double> pair in weights)
            {
                double weight = pair.Value;
                if (double.IsNaN(weight) || double.IsInfinity(weight) || weight <= 0.0)
                {
                    nonPositive++;
                    continue;
                }
                total += weight;
            }

            if (nonPositive > 0 || total <= 0.0 || double.IsNaN(total) || double.IsInfinity(total))
            {
                LogRuntimeBoundaryWarningOnce(
                    "itemdrop.nonpositive." + (contextKey ?? "unknown"),
                    "ItemDrop probability hidden because vanilla WeightedList received " +
                    nonPositive.ToString(System.Globalization.CultureInfo.InvariantCulture) +
                    " non-positive/invalid eligible weight(s); candidate-order fallback is not represented by QII.",
                    null);
                total = 0.0;
                return false;
            }
            return true;
        }

        // Diagnostic formatting for exact ItemDrop selector inputs belongs with the
        // shared ItemDrop math owner, not the Baron projection owner.
        private static string FormatBaronClassWeights(Dictionary<string, double> weights)
        {
            if (weights == null || weights.Count == 0) return "-";
            List<string> parts = new List<string>();
            foreach (KeyValuePair<string, double> pair in weights)
                parts.Add(pair.Key + ":" + pair.Value.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture));
            parts.Sort(StringComparer.OrdinalIgnoreCase);
            return string.Join(",", parts.ToArray());
        }

        private static double GetEnemyCategoryWeight(
            LootItemMeta meta,
            Dictionary<string, double> whitelist,
            bool whitelistExists,
            string factionId,
            out bool eligible)
        {
            return GetItemDropCategoryWeight(
                meta == null ? null : meta.Categories,
                whitelist,
                whitelistExists,
                factionId,
                out eligible);
        }
    }
}
