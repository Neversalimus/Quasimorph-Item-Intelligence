namespace ItemIntelligence
{
    public static partial class ModMain
    {
        private static void BuildBrowserMagnum(string itemId)
        {
            if (!ShowMagnumUses) return;
            // Preserve the existing research builder and every numeric total. Its
            // early returns must not hide a resource's separate upgrade use.
            BuildBrowserMagnumResearch(itemId);
            RefreshGenomeUpgradeUses(true);
            if (!HasGenomeUpgradeUse(itemId)) return;

            BrowserLines.Add(BrowserLine.Section(Ui("ui.upgrade_uses")));
            BrowserLines.Add(BrowserLine.FullNote(Ui("ui.upgrade_genomes")));
            BrowserLines.Add(BrowserLine.FullNote(Ui("ui.upgrade_uses_note")));
            LogGenomeUpgradeUse(itemId);
        }
    }
}
