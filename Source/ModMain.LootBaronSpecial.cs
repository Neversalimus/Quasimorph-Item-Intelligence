using System;
using System.Collections.Generic;
using System.Globalization;

namespace ItemIntelligence
{
    public static partial class ModMain
    {
        // v1.7.40-test16: compact player-facing projection of the Baron pact
        // inventory/death pipeline. The data/math owner lives in LootBaronUltimateData.
        private sealed class LootBaronSpecialSource
        {
            public readonly string QmorphosRecordId;
            public readonly string BramfaturaId;
            public readonly string BaronCreatureId;
            public readonly string AiPresetId;
            public readonly int PhaseMinLevel;
            public readonly int PhaseMaxLevel;
            public readonly float ItemMinPercent;
            public readonly float ItemMaxPercent;
            public readonly float AnyPactMinPercent;
            public readonly float AnyPactMaxPercent;
            public readonly float DeathRestorePercent;
            public readonly bool ChanceResolved;
            public readonly bool DeathRestoreResolved;
            public readonly int UniformPoolCount;
            public readonly int AdditMinRolls;
            public readonly int AdditMaxRolls;
            public readonly bool LegacyPoolUsed;

            public LootBaronSpecialSource(
                string qmorphosRecordId,
                string bramfaturaId,
                string baronCreatureId,
                string aiPresetId,
                int phaseMinLevel,
                int phaseMaxLevel,
                float itemMinPercent,
                float itemMaxPercent,
                float anyPactMinPercent,
                float anyPactMaxPercent,
                float deathRestorePercent,
                bool chanceResolved,
                bool deathRestoreResolved,
                int uniformPoolCount,
                int additMinRolls,
                int additMaxRolls,
                bool legacyPoolUsed)
            {
                QmorphosRecordId = qmorphosRecordId ?? string.Empty;
                BramfaturaId = bramfaturaId ?? string.Empty;
                BaronCreatureId = baronCreatureId ?? string.Empty;
                AiPresetId = aiPresetId ?? string.Empty;
                PhaseMinLevel = phaseMinLevel;
                PhaseMaxLevel = phaseMaxLevel;
                ItemMinPercent = ClampPercent(itemMinPercent);
                ItemMaxPercent = ClampPercent(itemMaxPercent);
                AnyPactMinPercent = ClampPercent(anyPactMinPercent);
                AnyPactMaxPercent = ClampPercent(anyPactMaxPercent);
                DeathRestorePercent = ClampPercent(deathRestorePercent);
                ChanceResolved = chanceResolved;
                DeathRestoreResolved = deathRestoreResolved;
                UniformPoolCount = Math.Max(0, uniformPoolCount);
                AdditMinRolls = Math.Max(0, additMinRolls);
                AdditMaxRolls = Math.Max(AdditMinRolls, additMaxRolls);
                LegacyPoolUsed = legacyPoolUsed;
            }
        }

        private sealed class LootBaronPresentationGroup
        {
            public readonly string BaronCreatureId;
            public readonly List<LootBaronSpecialSource> Sources = new List<LootBaronSpecialSource>();
            public float ItemMinPercent = 100f;
            public float ItemMaxPercent;
            public float AnyPactMinPercent = 100f;
            public float AnyPactMaxPercent;
            public bool ChanceResolved = true;

            public LootBaronPresentationGroup(string baronCreatureId)
            {
                BaronCreatureId = baronCreatureId ?? string.Empty;
            }

            public void Add(LootBaronSpecialSource source)
            {
                if (source == null) return;
                Sources.Add(source);
                if (!source.ChanceResolved) ChanceResolved = false;
                ItemMinPercent = Math.Min(ItemMinPercent, source.ItemMinPercent);
                ItemMaxPercent = Math.Max(ItemMaxPercent, source.ItemMaxPercent);
                AnyPactMinPercent = Math.Min(AnyPactMinPercent, source.AnyPactMinPercent);
                AnyPactMaxPercent = Math.Max(AnyPactMaxPercent, source.AnyPactMaxPercent);
            }
        }

        private static List<LootBaronPresentationGroup> BuildLootBaronPresentationGroups(
            List<LootBaronSpecialSource> sources)
        {
            List<LootBaronPresentationGroup> result = new List<LootBaronPresentationGroup>();
            if (sources == null || sources.Count == 0) return result;
            Dictionary<string, LootBaronPresentationGroup> byBaron =
                new Dictionary<string, LootBaronPresentationGroup>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < sources.Count; i++)
            {
                LootBaronSpecialSource source = sources[i];
                if (source == null || string.IsNullOrEmpty(source.BaronCreatureId)) continue;
                LootBaronPresentationGroup group;
                if (!byBaron.TryGetValue(source.BaronCreatureId, out group))
                {
                    group = new LootBaronPresentationGroup(source.BaronCreatureId);
                    byBaron[source.BaronCreatureId] = group;
                    result.Add(group);
                }
                group.Add(source);
            }
            result.Sort(delegate(LootBaronPresentationGroup a, LootBaronPresentationGroup b)
            {
                return string.Compare(a.BaronCreatureId, b.BaronCreatureId, StringComparison.OrdinalIgnoreCase);
            });
            return result;
        }

        private static readonly Dictionary<string, List<LootBaronSpecialSource>> LootBaronSpecialSourcesByItem =
            new Dictionary<string, List<LootBaronSpecialSource>>(StringComparer.OrdinalIgnoreCase);
        private static bool _lootBaronSpecialIndexBuilt;

        private static float ClampPercent(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value)) return 0f;
            return Math.Max(0f, Math.Min(100f, value));
        }

        private static void ResetLootBaronSpecialIndex()
        {
            LootBaronSpecialSourcesByItem.Clear();
            ResetBaronUltimateDataState();
            ResetBaronHabitatRuntimeStationIndex();
            _lootBaronSpecialIndexBuilt = false;
        }

        private static void EnsureLootBaronSpecialIndex(string itemId)
        {
            if (_lootBaronSpecialIndexBuilt) return;
            if (!IsBaronPactItem(itemId)) return;
            BuildLootBaronSpecialIndex();
        }

        private static void AddLootBaronSpecialSource(string itemId, LootBaronSpecialSource source)
        {
            if (string.IsNullOrEmpty(itemId) || source == null) return;
            List<LootBaronSpecialSource> list;
            if (!LootBaronSpecialSourcesByItem.TryGetValue(itemId, out list))
            {
                list = new List<LootBaronSpecialSource>();
                LootBaronSpecialSourcesByItem[itemId] = list;
            }
            list.Add(source);
        }

        private static string FormatBaronChance(float minPercent, float maxPercent, bool resolved)
        {
            if (!resolved || float.IsNaN(minPercent) || float.IsInfinity(minPercent) ||
                float.IsNaN(maxPercent) || float.IsInfinity(maxPercent)) return "?";
            float min = ClampPercent(minPercent);
            float max = ClampPercent(maxPercent);
            if (max < min) { float tmp = min; min = max; max = tmp; }
            return Math.Abs(max - min) < 0.005f
                ? FormatLootPercent(max)
                : FormatLootPercent(min) + "-" + FormatLootPercent(max);
        }

        private static void AppendLootBaronSpecialLines(string itemId, ref bool any)
        {
            EnsureLootBaronSpecialIndex(itemId);
            List<LootBaronSpecialSource> sources;
            if (!LootBaronSpecialSourcesByItem.TryGetValue(itemId, out sources) ||
                sources == null || sources.Count == 0)
                return;

            List<LootBaronPresentationGroup> groups = BuildLootBaronPresentationGroups(sources);
            if (groups.Count == 0) return;

            any = true;
            if (!AddLootSectionHeaderAndShouldBuild(Ui("loot.baron.section"), groups.Count)) return;
            BrowserLines.Add(BrowserLine.BaronLootHeader(
                Ui("loot.baron.column.baron"),
                Ui("loot.baron.column.item_chance"),
                Ui("loot.baron.column.any_pact")));

            for (int i = 0; i < groups.Count; i++)
            {
                LootBaronPresentationGroup group = groups[i];
                BrowserLines.Add(BrowserLine.BaronLootRow(
                    ResolveLootSourceName(group.BaronCreatureId, "MobClass"),
                    FormatBaronChance(group.ItemMinPercent, group.ItemMaxPercent, group.ChanceResolved),
                    FormatBaronChance(group.AnyPactMinPercent, group.AnyPactMaxPercent, group.ChanceResolved)));
            }

            AddWrappedLootNote("loot.note.baron_ultimate_death");
        }
    }
}
