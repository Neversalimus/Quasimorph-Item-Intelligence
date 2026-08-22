using System;

namespace ItemIntelligence
{
    /// <summary>
    /// Chooses the most useful first tab from real data already available in memory.
    /// Item classes are not used as the deciding policy: weak one-purpose items go straight
    /// to their detail page, while strong summaries (weapons/ammo/chips/pacts) keep Overview.
    /// </summary>
    public static partial class ModMain
    {
        private static BrowserTabId ResolveAdaptiveEntryTab(string itemId)
        {
            if (string.IsNullOrEmpty(itemId)) return BrowserTabId.Overview;

            OverviewSignalSnapshot s = EvaluateOverviewSignals(itemId);
            if (s.StrongOverview || s.MeaningfulGroups >= 2)
                return BrowserTabId.Overview;

            if (s.RecipeRelations > 0 || s.DisassemblyRelations > 0)
                return BrowserTabId.Recipes;
            if (s.MagnumRelations > 0)
                return BrowserTabId.Magnum;
            if (s.AmmoRelations > 0)
                return BrowserTabId.Ammo;
            return BrowserTabId.Overview;
        }
    }
}
