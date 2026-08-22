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
        // Canonical recycler/disassembly relationships. Both directions are derived from
        // ItemRecord.Disassembly during the same incremental warmup; no Drop/Loot/Container
        // scan is needed and the reverse index adds no additional game-data traversal.
        private static readonly Dictionary<string, List<DisassemblyOutput>> DisassemblyOutputsByItem =
            new Dictionary<string, List<DisassemblyOutput>>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, List<DisassemblySource>> DisassemblySourcesByOutputItem =
            new Dictionary<string, List<DisassemblySource>>(StringComparer.OrdinalIgnoreCase);
        private static readonly List<KeyValuePair<string, object>> DisassemblyWarmupItems =
            new List<KeyValuePair<string, object>>();
        private static int _disassemblyWarmupIndex;
        private static bool _disassemblyWarmupActive;
        private static bool _disassemblyWarmupComplete;
        private static float _disassemblyRollChancePercent = -1f;

        private sealed class DisassemblySource
        {
            public readonly string ItemId;
            public readonly int RollCount;
            public readonly float ChancePercent;
            public readonly bool Possible;

            public DisassemblySource(string itemId, int rollCount, float chancePercent, bool possible)
            {
                ItemId = itemId ?? string.Empty;
                RollCount = Math.Max(1, rollCount);
                ChancePercent = chancePercent;
                Possible = possible;
            }
        }

        // Disassembly owns its complete reset contract.
        private static void ResetDisassemblyIndexState()
        {
            DisassemblyOutputsByItem.Clear();
            DisassemblySourcesByOutputItem.Clear();
            DisassemblyWarmupItems.Clear();
            _disassemblyWarmupIndex = 0;
            _disassemblyWarmupActive = false;
            _disassemblyWarmupComplete = false;
            _disassemblyRollChancePercent = -1f;
            ResetDisassemblySpecialChanceContract();
        }

        private static void StartDisassemblyFeatureWarmup()
        {
            if (!_compatDisassembly) return;
            try { StartDisassemblyWarmup(); }
            catch (Exception ex)
            {
                StopDisassemblyFeatureFrameWork();
                TripCompatibilityFeatureRuntime("Disassembly", ex);
            }
        }

        private static void TickDisassemblyFeatureFrameWork()
        {
            if (!_compatDisassembly) return;
            try { TickDisassemblyWarmup(); }
            catch (Exception ex)
            {
                StopDisassemblyFeatureFrameWork();
                TripCompatibilityFeatureRuntime("Disassembly", ex);
            }
        }

        private static void StopDisassemblyFeatureFrameWork()
        {
            _disassemblyWarmupActive = false;
            DisassemblyWarmupItems.Clear();
            _disassemblyWarmupIndex = 0;
        }

        private static string GetDisassemblyWarmupStatus()
        {
            return !_compatDisassembly
                ? "disabled"
                : (_disassemblyWarmupActive ? "pending" : "complete");
        }

        private static void StartDisassemblyWarmup()
        {
            DisassemblyOutputsByItem.Clear();
            DisassemblySourcesByOutputItem.Clear();
            DisassemblyWarmupItems.Clear();
            _disassemblyWarmupIndex = 0;
            _disassemblyWarmupComplete = false;
            _disassemblyWarmupActive = false;

            ResolveDisassemblyRollChance();

            foreach (KeyValuePair<string, object> pair in ItemRecordsById)
            {
                if (!string.IsNullOrEmpty(pair.Key) && pair.Value != null)
                    DisassemblyWarmupItems.Add(pair);
            }

            if (DisassemblyWarmupItems.Count == 0)
            {
                _disassemblyWarmupComplete = true;
                return;
            }

            // The game's real dismantling path reads ItemRecord.Disassembly only.
            _disassemblyWarmupActive = true;
            Debug.Log("[ItemIntelligence] Canonical disassembly warmup queued: " +
                DisassemblyWarmupItems.Count.ToString(CultureInfo.InvariantCulture) +
                " item records, rollChance=" + FormatPercentValue(_disassemblyRollChancePercent) + ".");
        }

        private static void TickDisassemblyWarmup()
        {
            if (!_disassemblyWarmupActive) return;

            const int recordsPerFrame = 18;
            const double frameBudgetMs = 1.00;
            long started = System.Diagnostics.Stopwatch.GetTimestamp();
            int processed = 0;

            while (_disassemblyWarmupIndex < DisassemblyWarmupItems.Count &&
                   processed < recordsPerFrame &&
                   !PerformanceBudgetExceeded(started, frameBudgetMs))
            {
                KeyValuePair<string, object> pair = DisassemblyWarmupItems[_disassemblyWarmupIndex++];
                try { IndexDisassemblyForItem(pair.Key, pair.Value); }
                catch { }
                processed++;
            }

            if (_disassemblyWarmupIndex < DisassemblyWarmupItems.Count)
                return;

            _disassemblyWarmupActive = false;
            _disassemblyWarmupComplete = true;
            DisassemblyWarmupItems.Clear();

            int totalOutputs = 0;
            foreach (KeyValuePair<string, List<DisassemblyOutput>> pair in DisassemblyOutputsByItem)
                if (pair.Value != null) totalOutputs += pair.Value.Count;

            int reverseLinks;
            string symmetryError;
            if (!ValidateDisassemblyIndexSymmetry(out reverseLinks, out symmetryError))
                throw new InvalidOperationException("Disassembly forward/reverse index mismatch: " + symmetryError);

            Debug.Log("[ItemIntelligence] Canonical disassembly warmup complete: items=" +
                DisassemblyOutputsByItem.Count.ToString(CultureInfo.InvariantCulture) +
                ", outputs=" + totalOutputs.ToString(CultureInfo.InvariantCulture) +
                ", reverseItems=" + DisassemblySourcesByOutputItem.Count.ToString(CultureInfo.InvariantCulture) +
                ", reverseLinks=" + reverseLinks.ToString(CultureInfo.InvariantCulture) +
                ", symmetry=OK, rollChance=" + FormatPercentValue(_disassemblyRollChancePercent) + ".");

            if (_inspectorOpen && (BrowserNavigation.Tab == (int)BrowserTabId.Recipes || BrowserNavigation.Tab == (int)BrowserTabId.Overview))
                RenderBrowser(_inspectorItemId);
        }

        private static void ResolveDisassemblyRollChance()
        {
            _disassemblyRollChancePercent = -1f;

            try
            {
                object global = GetStaticMember(typeof(Data), "Global");
                object raw = GetMember(global, "SpawnItemOnDisassembleChance");
                double parsed;

                if (raw != null && TryToDoubleSafe(raw, out parsed) && parsed >= 0.0)
                {
                    if (parsed <= 1.000001)
                        parsed *= 100.0;

                    if (parsed <= 100.0001)
                        _disassemblyRollChancePercent = Mathf.Clamp((float)parsed, 0f, 100f);
                }
            }
            catch { }

            ResolveDisassemblySpecialChanceContract();

            if (_disassemblyRollChancePercent >= 0f)
                Debug.Log("[ItemIntelligence] Disassembly roll chance resolved from Data.Global: " +
                    FormatPercentValue(_disassemblyRollChancePercent) + ".");
            else
                Debug.LogWarning("[ItemIntelligence] Could not resolve Data.Global.SpawnItemOnDisassembleChance.");
        }

        private static void IndexDisassemblyForItem(string itemId, object record)
        {
            if (string.IsNullOrEmpty(itemId) || record == null) return;

            object itemRecord = FindCanonicalItemRecord(record);
            if (itemRecord == null) return;

            object rawDisassembly = GetMember(itemRecord, "Disassembly");
            IEnumerable entries = rawDisassembly as IEnumerable;
            if (entries == null || rawDisassembly is string) return;

            Dictionary<string, DisassemblyOutput> outputs =
                new Dictionary<string, DisassemblyOutput>(StringComparer.OrdinalIgnoreCase);

            int scanned = 0;
            foreach (object entry in entries)
            {
                if (++scanned > 256) break;
                if (entry == null) continue;

                string outputId = FirstNonEmpty(
                    GetStringMember(entry, "ItemId"),
                    GetStringMember(entry, "Id"));

                if (string.IsNullOrEmpty(outputId) ||
                    string.Equals(outputId, itemId, StringComparison.OrdinalIgnoreCase) ||
                    !KnownItemIds.Contains(outputId))
                    continue;

                int count = 1;
                int parsedCount;
                if (TryToInt(GetMember(entry, "Count"), out parsedCount) && parsedCount > 0)
                    count = parsedCount;

                DisassemblyOutput output =
                    new DisassemblyOutput(outputId, 0, count, GetDirectDisassemblyChancePercent(itemId), true);
                output.RollCount = count;
                MergeCanonicalDisassemblyOutput(outputs, output);
            }

            if (outputs.Count == 0) return;

            List<DisassemblyOutput> canonicalOutputs = new List<DisassemblyOutput>(outputs.Values);
            DisassemblyOutputsByItem[itemId] = canonicalOutputs;
            IndexCanonicalDisassemblySources(itemId, canonicalOutputs);
        }

        private static void MergeCanonicalDisassemblyOutput(
            Dictionary<string, DisassemblyOutput> outputs,
            DisassemblyOutput candidate)
        {
            if (outputs == null || candidate == null || string.IsNullOrEmpty(candidate.ItemId))
                return;

            DisassemblyOutput existing;
            if (!outputs.TryGetValue(candidate.ItemId, out existing) || existing == null)
            {
                outputs[candidate.ItemId] = candidate;
                return;
            }

            // ItemQuantity.Count is a number of independent output rolls per dismantled
            // source item. If the same ItemId occurs more than once, the game performs
            // all of those rolls, so sum them rather than collapsing to max().
            existing.RollCount += candidate.RollCount;
            existing.MinCount = 0;
            existing.MaxCount += candidate.MaxCount;
            existing.ChancePercent = candidate.ChancePercent;
            existing.Possible = true;
        }

        private static void IndexCanonicalDisassemblySources(
            string sourceItemId, List<DisassemblyOutput> outputs)
        {
            if (string.IsNullOrEmpty(sourceItemId) || outputs == null) return;

            for (int i = 0; i < outputs.Count; i++)
            {
                DisassemblyOutput output = outputs[i];
                if (output == null || string.IsNullOrEmpty(output.ItemId)) continue;

                List<DisassemblySource> sources;
                if (!DisassemblySourcesByOutputItem.TryGetValue(output.ItemId, out sources) || sources == null)
                {
                    sources = new List<DisassemblySource>();
                    DisassemblySourcesByOutputItem[output.ItemId] = sources;
                }

                // One canonical source row per source item. The forward index already
                // merged duplicate ItemQuantity rows for this output before we get here.
                sources.Add(new DisassemblySource(
                    sourceItemId,
                    Math.Max(1, output.RollCount > 0 ? output.RollCount : output.MaxCount),
                    output.ChancePercent,
                    output.Possible));
            }
        }

        private static bool ValidateDisassemblyIndexSymmetry(out int reverseLinks, out string error)
        {
            reverseLinks = 0;
            error = string.Empty;

            foreach (KeyValuePair<string, List<DisassemblyOutput>> forwardPair in DisassemblyOutputsByItem)
            {
                string sourceItemId = forwardPair.Key;
                List<DisassemblyOutput> outputs = forwardPair.Value;
                if (outputs == null) continue;

                for (int i = 0; i < outputs.Count; i++)
                {
                    DisassemblyOutput output = outputs[i];
                    if (output == null || string.IsNullOrEmpty(output.ItemId)) continue;

                    List<DisassemblySource> sources;
                    int reverseMatches =
                        DisassemblySourcesByOutputItem.TryGetValue(output.ItemId, out sources) && sources != null
                            ? CountMatchingDisassemblySources(sources, sourceItemId, output)
                            : 0;
                    if (reverseMatches != 1)
                    {
                        error = sourceItemId + " -> " + output.ItemId +
                            " expected one reverse edge, found " + reverseMatches.ToString(CultureInfo.InvariantCulture);
                        return false;
                    }
                }
            }

            foreach (KeyValuePair<string, List<DisassemblySource>> reversePair in DisassemblySourcesByOutputItem)
            {
                string outputItemId = reversePair.Key;
                List<DisassemblySource> sources = reversePair.Value;
                if (sources == null) continue;

                for (int i = 0; i < sources.Count; i++)
                {
                    DisassemblySource source = sources[i];
                    if (source == null || string.IsNullOrEmpty(source.ItemId)) continue;
                    reverseLinks++;

                    List<DisassemblyOutput> outputs;
                    int forwardMatches =
                        DisassemblyOutputsByItem.TryGetValue(source.ItemId, out outputs) && outputs != null
                            ? CountMatchingDisassemblyOutputs(outputs, outputItemId, source)
                            : 0;
                    if (forwardMatches != 1)
                    {
                        error = outputItemId + " <- " + source.ItemId +
                            " expected one forward edge, found " + forwardMatches.ToString(CultureInfo.InvariantCulture);
                        return false;
                    }
                }
            }

            return true;
        }

        private static int CountMatchingDisassemblySources(
            List<DisassemblySource> sources, string sourceItemId, DisassemblyOutput output)
        {
            if (sources == null || output == null) return 0;
            int expectedRolls = Math.Max(1, output.RollCount > 0 ? output.RollCount : output.MaxCount);
            int matches = 0;

            for (int i = 0; i < sources.Count; i++)
            {
                DisassemblySource source = sources[i];
                if (source == null) continue;
                if (!string.Equals(source.ItemId, sourceItemId, StringComparison.OrdinalIgnoreCase)) continue;
                if (source.RollCount != expectedRolls) continue;
                if (!SameDisassemblyChance(source.ChancePercent, output.ChancePercent)) continue;
                if (source.Possible != output.Possible) continue;
                matches++;
            }

            return matches;
        }

        private static int CountMatchingDisassemblyOutputs(
            List<DisassemblyOutput> outputs, string outputItemId, DisassemblySource source)
        {
            if (outputs == null || source == null) return 0;
            int matches = 0;

            for (int i = 0; i < outputs.Count; i++)
            {
                DisassemblyOutput output = outputs[i];
                if (output == null) continue;
                if (!string.Equals(output.ItemId, outputItemId, StringComparison.OrdinalIgnoreCase)) continue;
                int rolls = Math.Max(1, output.RollCount > 0 ? output.RollCount : output.MaxCount);
                if (rolls != source.RollCount) continue;
                if (!SameDisassemblyChance(output.ChancePercent, source.ChancePercent)) continue;
                if (output.Possible != source.Possible) continue;
                matches++;
            }

            return matches;
        }

        private static bool SameDisassemblyChance(float a, float b)
        {
            if (a < 0f || b < 0f) return a < 0f && b < 0f;
            return Mathf.Abs(a - b) <= 0.0001f;
        }

        private static bool IsRandomDisassemblyPool(string itemId, List<DisassemblyOutput> outputs)
        {
            if (outputs == null || outputs.Count == 0) return false;

            // The normal cargo/inventory dismantle path performs one random roll for
            // every ItemQuantity unit using Data.Global.SpawnItemOnDisassembleChance.
            return IsRandomDirectDisassemblyItem(itemId);
        }

        private static int GetDisassemblyOutputCount(string itemId)
        {
            if (string.IsNullOrEmpty(itemId)) return 0;
            List<DisassemblyOutput> list;
            return DisassemblyOutputsByItem.TryGetValue(itemId, out list) && list != null ? list.Count : 0;
        }

        private static int GetDisassemblySourceCount(string itemId)
        {
            if (string.IsNullOrEmpty(itemId) || !_disassemblyWarmupComplete) return 0;
            List<DisassemblySource> list;
            return DisassemblySourcesByOutputItem.TryGetValue(itemId, out list) && list != null ? list.Count : 0;
        }

        private static string FormatDisassemblyOutput(DisassemblyOutput output, bool ru)
        {
            if (output == null) return string.Empty;
            int rolls = Math.Max(1, output.RollCount > 0 ? output.RollCount : output.MaxCount);
            return FormatDisassemblyRolls(rolls, output.ChancePercent, output.Possible);
        }

        private static string FormatDisassemblySource(DisassemblySource source, bool ru)
        {
            if (source == null) return string.Empty;
            return FormatDisassemblyRolls(source.RollCount, source.ChancePercent, source.Possible);
        }

        private static string FormatDisassemblyRolls(int rolls, float chancePercent, bool possible)
        {
            rolls = Math.Max(1, rolls);
            if (chancePercent < 0f && possible && _disassemblyRollChancePercent >= 0f)
                chancePercent = _disassemblyRollChancePercent;

            if (chancePercent >= 99.999f)
                return "x" + rolls.ToString(CultureInfo.InvariantCulture) + "  •  100%" + Ui("ui.roll");

            if (chancePercent >= 0f)
            {
                string chance = FormatPercentValue(chancePercent);
                if (rolls <= 1)
                    return "x1  •  " + chance + Ui("ui.roll");

                return "0-" + rolls.ToString(CultureInfo.InvariantCulture) + "  •  " + chance + Ui("ui.roll");
            }

            // Unknown means unknown: never show a bare chance label without a number.
            return "0-" + rolls.ToString(CultureInfo.InvariantCulture) + "  •  ?";
        }
    }
}
