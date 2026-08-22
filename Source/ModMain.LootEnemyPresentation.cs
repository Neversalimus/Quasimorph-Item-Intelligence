using System;
using System.Collections.Generic;
using System.Globalization;

namespace ItemIntelligence
{
    public static partial class ModMain
    {
        // v1.7.38-test7: enemy/corpse presentation is intentionally split from
        // the base Loot renderer. Vanilla transfers ordinary enemy inventory to
        // the corpse, while FLootCorpseItem creates a separate AdditItemClasses
        // bonus-roll pipeline. Keeping the two tables separate prevents the
        // modifier UI from implying that Marauder buffs every enemy drop chance.
        // Reusable buffers avoid allocating/sorting fresh lists for every toggle.
        private static readonly List<LootEnemySource> LootEnemyRegularPresentationBuffer =
            new List<LootEnemySource>(256);
        private static readonly List<LootEnemySource> LootEnemyCorpseBonusPresentationBuffer =
            new List<LootEnemySource>(256);

        private static void AppendLootEnemySections(
            List<LootEnemySource> sources,
            LootModifierSnapshot modifiers,
            bool ru,
            ref bool any)
        {
            LootEnemyRegularPresentationBuffer.Clear();
            LootEnemyCorpseBonusPresentationBuffer.Clear();
            if (sources == null || sources.Count == 0) return;

            double corpseExpected = modifiers == null ? -1.0 : modifiers.CorpseExpected;
            bool showCorpseBonus = corpseExpected > 0.0;

            for (int i = 0; i < sources.Count; i++)
            {
                LootEnemySource source = sources[i];
                if (source == null) continue;
                if (string.Equals(source.Kind, "CorpseBonus", StringComparison.Ordinal))
                {
                    if (showCorpseBonus)
                        LootEnemyCorpseBonusPresentationBuffer.Add(source);
                }
                else
                {
                    LootEnemyRegularPresentationBuffer.Add(source);
                }
            }

            if (LootEnemyRegularPresentationBuffer.Count > 0)
            {
                any = true;
                bool buildRegular = AddLootSectionHeaderAndShouldBuild(
                    Ui("ui.enemy_corpse_loot"), LootEnemyRegularPresentationBuffer.Count);
                if (buildRegular)
                {
                LootEnemyRegularPresentationBuffer.Sort(CompareLootEnemySourcesForPresentation);

                bool hasImplantRows = false;
                bool hasAugmentationRows = false;
                for (int i = 0; i < LootEnemyRegularPresentationBuffer.Count; i++)
                {
                    LootEnemySource source = LootEnemyRegularPresentationBuffer[i];
                    if (!hasImplantRows &&
                        (string.Equals(source.Kind, "GrantedImplant", StringComparison.Ordinal) ||
                         string.Equals(source.Kind, "RandomImplant", StringComparison.Ordinal)))
                        hasImplantRows = true;
                    if (!hasAugmentationRows &&
                        (string.Equals(source.Kind, "GrantedAugmentation", StringComparison.Ordinal) ||
                         string.Equals(source.Kind, "RandomAugmentation", StringComparison.Ordinal)))
                        hasAugmentationRows = true;
                }

                AddWrappedBrowserNoteGroup(118, 128,
                    "loot.note.tech",
                    "loot.note.enemy_chance",
                    "loot.note.corpse_transfer",
                    "loot.note.enemy_bonus_separate");
                if (hasImplantRows) AddWrappedLootNote("loot.note.implants");
                if (hasAugmentationRows)
                {
                    AddWrappedLootNote("loot.note.augmentations");
                    AddWrappedLootNote("loot.note.augmentation_woundslot");
                }

                BrowserLines.Add(
                    BrowserLine.LootHeader6(
                        Ui("ui.enemy"),
                        Ui("ui.source"),
                        Ui("ui.chance"),
                        Ui("ui.tech"),
                        Ui("ui.qty_rolls"),
                        Ui("ui.result")));
                for (int i = 0; i < LootEnemyRegularPresentationBuffer.Count; i++)
                {
                    LootEnemySource source = LootEnemyRegularPresentationBuffer[i];
                    BrowserLines.Add(
                        BrowserLine.LootRow6(
                            ResolveLootSourceName(source.MobClassId, "MobClass"),
                            GetEnemyLootKindLabel(source.Kind, ru),
                            FormatEnemyLootChance(source, -1.0),
                            FormatEnemyLootMinTech(source),
                            FormatEnemyLootQuantity(source, -1.0, ru),
                            GetEnemyLootResultLabelWithModifiers(source, modifiers, ru),
                            GetRepresentativeMobFactionId(source.MobClassId)));
                }
                }
            }

            if (LootEnemyCorpseBonusPresentationBuffer.Count > 0)
            {
                any = true;
                bool buildBonus = AddLootSectionHeaderAndShouldBuild(
                    Ui("ui.bonus_corpse_loot"), LootEnemyCorpseBonusPresentationBuffer.Count);
                if (buildBonus)
                {
                LootEnemyCorpseBonusPresentationBuffer.Sort(CompareLootEnemySourcesForPresentation);
                AddWrappedLootNote("loot.note.corpse_bonus_rolls");
                BrowserLines.Add(
                    BrowserLine.FullNote(
                        Ui("ui.bonus_rolls") + ": " +
                        FormatCorpseBonusRollDistribution(corpseExpected)));
                BrowserLines.Add(
                    BrowserLine.LootHeader6(
                        Ui("ui.enemy"),
                        Ui("ui.per_roll"),
                        Ui("ui.final_chance"),
                        Ui("ui.tech"),
                        Ui("ui.rolls"),
                        Ui("ui.result")));

                string bonusRollRange = FormatCorpseBonusRollRange(corpseExpected);
                for (int i = 0; i < LootEnemyCorpseBonusPresentationBuffer.Count; i++)
                {
                    LootEnemySource source = LootEnemyCorpseBonusPresentationBuffer[i];
                    BrowserLines.Add(
                        BrowserLine.LootRow6(
                            ResolveLootSourceName(source.MobClassId, "MobClass"),
                            FormatEnemyLootPerRollChance(source),
                            FormatEnemyLootChance(source, corpseExpected),
                            FormatEnemyLootMinTech(source),
                            bonusRollRange,
                            Ui("ui.corpse"),
                            GetRepresentativeMobFactionId(source.MobClassId)));
                }
                }
            }
        }

        private static int CompareLootEnemySourcesForPresentation(
            LootEnemySource a,
            LootEnemySource b)
        {
            if (ReferenceEquals(a, b)) return 0;
            if (a == null) return 1;
            if (b == null) return -1;
            int name = string.Compare(
                ResolveLootSourceName(a.MobClassId, "MobClass"),
                ResolveLootSourceName(b.MobClassId, "MobClass"),
                StringComparison.CurrentCultureIgnoreCase);
            if (name != 0) return name;
            return string.Compare(a.Kind, b.Kind, StringComparison.OrdinalIgnoreCase);
        }

        private static string FormatEnemyLootPerRollChance(LootEnemySource source)
        {
            if (source == null) return "-";
            return Math.Abs(source.MaxPercent - source.MinPercent) < 0.05f
                ? FormatLootPercent(source.MaxPercent)
                : FormatLootPercent(source.MinPercent) + "-" + FormatLootPercent(source.MaxPercent);
        }

        private static string FormatCorpseBonusRollRange(double expectedRolls)
        {
            if (expectedRolls < 0.0) return "?";
            expectedRolls = Math.Max(0.0, expectedRolls);
            int guaranteed = Math.Max(0, (int)Math.Floor(expectedRolls));
            double fraction = expectedRolls - guaranteed;
            return fraction < 0.0001
                ? "x" + guaranteed.ToString(CultureInfo.InvariantCulture)
                : guaranteed.ToString(CultureInfo.InvariantCulture) + "-" +
                  (guaranteed + 1).ToString(CultureInfo.InvariantCulture);
        }

        private static string FormatCorpseBonusRollDistribution(double expectedRolls)
        {
            if (expectedRolls < 0.0) return "?";
            expectedRolls = Math.Max(0.0, expectedRolls);
            int guaranteed = Math.Max(0, (int)Math.Floor(expectedRolls));
            double fraction = expectedRolls - guaranteed;
            if (fraction < 0.0001)
                return "x" + guaranteed.ToString(CultureInfo.InvariantCulture);

            int extraPercent = (int)Math.Round(fraction * 100.0);
            return guaranteed.ToString(CultureInfo.InvariantCulture) + "-" +
                (guaranteed + 1).ToString(CultureInfo.InvariantCulture) + "  " +
                extraPercent.ToString(CultureInfo.InvariantCulture) + "%";
        }
    }
}
