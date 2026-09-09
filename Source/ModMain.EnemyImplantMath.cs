using System;
using System.Collections.Generic;

namespace ItemIntelligence
{
    public static partial class ModMain
    {
        // Read-only model of body eligibility. No CreatureData, factories or RNG.
        private sealed class EnemyBodySlot
        {
            public string Id;
            public string Type;
            public string Nature;
            public int MinSockets;
            public int MaxSockets;
        }

        private sealed class EnemyImplantRule
        {
            public string Id;
            public string Type;
            public HashSet<string> Natures;
        }

        private sealed class EnemyAugmentationRule
        {
            public string Id;
            public List<EnemyBodySlot> Slots;
        }

        private sealed class EnemyBodyModel
        {
            public readonly Dictionary<string, List<EnemyBodySlot>> Slots =
                new Dictionary<string, List<EnemyBodySlot>>(StringComparer.Ordinal);
            public readonly HashSet<string> AugmentedTypes = new HashSet<string>(StringComparer.Ordinal);
            public readonly HashSet<string> GrantedAugs = new HashSet<string>(StringComparer.Ordinal);
            public bool Unknown;
            public bool Conditional;

            public EnemyBodyModel Copy()
            {
                EnemyBodyModel copy = new EnemyBodyModel();
                foreach (KeyValuePair<string, List<EnemyBodySlot>> pair in Slots)
                    copy.Slots.Add(pair.Key, new List<EnemyBodySlot>(pair.Value));
                copy.AugmentedTypes.UnionWith(AugmentedTypes);
                copy.GrantedAugs.UnionWith(GrantedAugs);
                copy.Unknown = Unknown;
                copy.Conditional = Conditional;
                return copy;
            }
        }

        private static bool HasGuaranteedAugmentationConflict(EnemyBodyModel body, EnemyAugmentationRule aug)
        {
            if (aug == null || aug.Slots == null) return false;
            for (int i = 0; i < aug.Slots.Count; i++)
                if (body.AugmentedTypes.Contains(aug.Slots[i].Type)) return true;
            return false;
        }

        private static void ApplyGuaranteedEnemyAugmentation(EnemyBodyModel body, EnemyAugmentationRule aug)
        {
            if (aug == null) return; // A missing simple record fails installation in the game.
            if (aug.Slots == null) { body.Unknown = true; return; }
            if (aug.Slots.Count == 0) return;
            if (HasGuaranteedAugmentationConflict(body, aug)) return;
            // Validate the entire operation before changing the private snapshot.
            HashSet<string> types = new HashSet<string>(StringComparer.Ordinal);
            foreach (EnemyBodySlot slot in aug.Slots)
                if (slot == null || string.IsNullOrEmpty(slot.Type) || !types.Add(slot.Type))
                { body.Unknown = true; return; }
            foreach (EnemyBodySlot slot in aug.Slots)
            {
                body.Slots[slot.Type] = new List<EnemyBodySlot> { slot };
                body.AugmentedTypes.Add(slot.Type);
            }
            body.GrantedAugs.Add(aug.Id);
        }

        private static void IncludePossibleEnemyAugmentation(EnemyBodyModel body, EnemyAugmentationRule aug)
        {
            if (aug == null) return;
            if (aug.Slots == null) { body.Unknown = true; return; }
            if (HasGuaranteedAugmentationConflict(body, aug)) return;
            if (aug.Slots.Count == 0) return;
            // A conservative union, NOT a probability distribution: random augmentations
            // also depend on previous rolls, conflicts and the health threshold. Retain
            // original slots, never claim that every possible augmentation was installed.
            body.Conditional = true;
            foreach (EnemyBodySlot slot in aug.Slots)
            {
                List<EnemyBodySlot> options;
                if (!body.Slots.TryGetValue(slot.Type, out options))
                {
                    options = new List<EnemyBodySlot>();
                    body.Slots.Add(slot.Type, options);
                }
                if (!options.Contains(slot)) options.Add(slot);
            }
        }

        private static bool ImplantFitsNature(EnemyBodySlot slot, EnemyImplantRule rule)
        {
            return slot != null && rule != null && rule.Type == slot.Type &&
                rule.Natures != null && rule.Natures.Contains(slot.Nature);
        }

        private static int CountCompatibleGrantedImplants(EnemyBodySlot slot,
            List<EnemyImplantRule> granted, string stopAtId)
        {
            int count = 0;
            for (int i = 0; i < granted.Count; i++)
            {
                EnemyImplantRule rule = granted[i];
                if (!ImplantFitsNature(slot, rule)) continue;
                count++;
                if (stopAtId != null && rule.Id == stopAtId) return count;
            }
            return stopAtId == null ? count : -1;
        }

        private static bool MayInstallEnemyImplant(EnemyBodyModel body, EnemyImplantRule rule,
            List<EnemyImplantRule> granted, bool grantedSource)
        {
            if (body.Unknown || rule == null || rule.Natures == null) return true;
            List<EnemyBodySlot> options;
            if (!body.Slots.TryGetValue(rule.Type, out options)) return false;
            foreach (EnemyBodySlot slot in options)
            {
                if (!ImplantFitsNature(slot, rule)) continue;
                int consumed = CountCompatibleGrantedImplants(slot, granted, grantedSource ? rule.Id : null);
                if (grantedSource ? consumed > 0 && slot.MaxSockets >= consumed : slot.MaxSockets > consumed)
                    return true;
            }
            return false;
        }

        // All implant selections happen before installation. Incompatible selections
        // remain in the denominator and consume a roll, but do not occupy a socket.
        // States track earlier compatible competitors in this slot, before the first
        // target implant. Once capacity is exhausted the target can no longer install.
        private static bool TryEnemyImplantInstallProbabilityWithSockets(double target, double compatible,
            int minSockets, int maxSockets, int granted, int minRolls, int maxRolls, out double probability)
        {
            probability = 0.0;
            if (double.IsNaN(target) || double.IsInfinity(target) ||
                double.IsNaN(compatible) || double.IsInfinity(compatible) ||
                target < 0.0 || compatible < target || compatible > 1.000000000001 ||
                minRolls < 0 || maxRolls < minRolls || maxRolls > 64 ||
                minSockets < 0 || maxSockets < minSockets || granted < 0) return false;
            if (maxSockets <= granted || target <= 0.0 || maxRolls == 0) return true;
            compatible = Math.Min(1.0, compatible);
            int capacity = Math.Min(maxSockets - granted, maxRolls);
            double[] waiting = new double[capacity];
            double[] next = new double[capacity];
            double[] enoughSockets = new double[capacity];
            // Integrate a uniform socket range once. Do not repeat the entire dynamic
            // program for each capacity, even for extreme modded socket ranges.
            for (int used = 0; used < capacity; used++)
                enoughSockets[used] = Math.Max(0.0, (double)maxSockets -
                    Math.Max((double)minSockets, (double)granted + used + 1.0) + 1.0) /
                    ((double)maxSockets - minSockets + 1.0);
            waiting[0] = 1.0;
            double installed = 0.0;
            double sum = 0.0;
            for (int roll = 1; roll <= maxRolls; roll++)
            {
                Array.Clear(next, 0, next.Length);
                for (int used = 0; used < capacity; used++)
                {
                    installed += waiting[used] * target * enoughSockets[used];
                    next[used] += waiting[used] * (1.0 - compatible);
                    if (used + 1 < capacity)
                        next[used + 1] += waiting[used] * (compatible - target);
                }
                double[] swap = waiting; waiting = next; next = swap;
                if (roll >= minRolls) sum += installed;
            }
            probability = Math.Max(0.0, Math.Min(1.0, sum / (maxRolls - minRolls + 1)));
            return true;
        }

        private static double ProjectGrantedEnemyImplant(EnemyBodyModel body, EnemyImplantRule rule,
            List<EnemyImplantRule> granted)
        {
            if (!MayInstallEnemyImplant(body, rule, granted, true)) return 0.0;
            if (body.Unknown || body.Conditional || rule == null || rule.Natures == null) return double.NaN;
            EnemyBodySlot slot = body.Slots[rule.Type][0];
            if (slot.MinSockets < 0 || slot.MaxSockets < slot.MinSockets)
                return double.NaN;
            int rank = CountCompatibleGrantedImplants(slot, granted, rule.Id);
            return Math.Max(0.0, (double)slot.MaxSockets - Math.Max(slot.MinSockets, rank) + 1.0) /
                ((double)slot.MaxSockets - slot.MinSockets + 1.0);
        }

        private static Dictionary<string, double> ProjectRandomEnemyImplants(EnemyBodyModel body,
            Dictionary<string, EnemyImplantRule> rules, Dictionary<string, double> weights,
            List<EnemyImplantRule> granted, double gate, int minRolls, int maxRolls)
        {
            Dictionary<string, double> result = new Dictionary<string, double>(StringComparer.Ordinal);
            if (maxRolls <= 0 || gate == 0.0) return result;
            Dictionary<string, double> pool = new Dictionary<string, double>(StringComparer.Ordinal);
            double total = 0.0;
            bool unknown = body.Unknown || body.Conditional || double.IsNaN(gate);
            foreach (KeyValuePair<string, double> pair in weights)
            {
                EnemyImplantRule rule;
                rules.TryGetValue(pair.Key, out rule);
                // Slot existence is a PRE-selection predicate. Nature/capacity are not.
                if (!body.Unknown && rule != null && !body.Slots.ContainsKey(rule.Type)) continue;
                pool[pair.Key] = pair.Value;
                if (rule == null || rule.Natures == null || pair.Value <= 0.0 ||
                    double.IsNaN(pair.Value) || double.IsInfinity(pair.Value)) unknown = true;
                total += pair.Value;
            }
            if (total <= 0.0 || double.IsNaN(total) || double.IsInfinity(total)) unknown = true;
            Dictionary<string, double> compatibleBySlot = new Dictionary<string, double>(StringComparer.Ordinal);
            if (!unknown) foreach (KeyValuePair<string, double> pair in pool)
            {
                EnemyImplantRule candidate = rules[pair.Key];
                EnemyBodySlot slot = body.Slots[candidate.Type][0];
                if (!ImplantFitsNature(slot, candidate)) continue;
                double previous;
                compatibleBySlot.TryGetValue(slot.Type, out previous);
                compatibleBySlot[slot.Type] = previous + pair.Value;
            }
            foreach (KeyValuePair<string, double> pair in pool)
            {
                EnemyImplantRule rule;
                rules.TryGetValue(pair.Key, out rule);
                if (!MayInstallEnemyImplant(body, rule, granted, false)) continue;
                if (unknown) { result[pair.Key] = double.NaN; continue; }
                EnemyBodySlot slot = body.Slots[rule.Type][0];
                if (slot.MinSockets < 0 || slot.MaxSockets < slot.MinSockets)
                { result[pair.Key] = double.NaN; continue; }
                double compatible = compatibleBySlot[slot.Type];
                double targetChance = gate * pair.Value / total;
                double compatibleChance = Math.Max(targetChance, gate * compatible / total);
                int consumed = CountCompatibleGrantedImplants(slot, granted, null);
                double average;
                if (!TryEnemyImplantInstallProbabilityWithSockets(targetChance, compatibleChance,
                    slot.MinSockets, slot.MaxSockets, consumed, minRolls, maxRolls, out average))
                    average = double.NaN;
                if (double.IsNaN(average) || average > 0.0) result[pair.Key] = average;
            }
            return result;
        }
    }
}
