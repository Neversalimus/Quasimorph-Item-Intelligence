using System.Collections.Generic;

namespace ItemIntelligence
{
    public static partial class ModMain
    {
        // A Baron is presented once even when vanilla has multiple Qmorphos phase
        // records pointing at the same Baron creature (for example Duggur reload).
        private static void AppendOverviewBaronSpecial(string itemId)
        {
            if (!IsBaronPactItem(itemId)) return;
            EnsureLootBaronSpecialIndex(itemId);

            List<LootBaronSpecialSource> sources;
            if (!LootBaronSpecialSourcesByItem.TryGetValue(itemId, out sources) ||
                sources == null || sources.Count == 0)
                return;

            List<LootBaronPresentationGroup> groups = BuildLootBaronPresentationGroups(sources);
            if (groups.Count == 0) return;

            BrowserLines.Add(BrowserLine.FullSection(Ui("loot.baron.section")));
            for (int i = 0; i < groups.Count; i++)
            {
                LootBaronPresentationGroup group = groups[i];
                string baron = ResolveLootSourceName(group.BaronCreatureId, "MobClass");
                BrowserLines.Add(BrowserLine.Header(baron, string.Empty));

                AppendOverviewBaronHabitat(group.Sources);

                BrowserLines.Add(BrowserLine.Normal(
                    Ui("ui.baron_guaranteed"), Ui("ui.baron_one_pact")));
                BrowserLines.Add(BrowserLine.Normal(
                    Ui("ui.baron_this_pact"),
                    FormatBaronChance(group.ItemMinPercent, group.ItemMaxPercent, group.ChanceResolved)));
                BrowserLines.Add(BrowserLine.FullNote(Ui("ui.baron_depends_on_mission_tech")));
            }
        }
    }
}
