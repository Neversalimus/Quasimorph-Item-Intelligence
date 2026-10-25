using System;
using System.Collections.Generic;

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
            RefreshUpgradeUses(true);
            if (!HasUpgradeUse(itemId)) return;

            BrowserLines.Add(BrowserLine.Section(Ui("ui.upgrade_uses")));
            List<UpgradeUse> uses = UpgradeMaterials[itemId];
            var categories = new HashSet<string>(StringComparer.Ordinal);
            var targets = new List<string>();
            foreach (UpgradeUse use in uses)
            {
                if (categories.Add(use.Kind))
                    BrowserLines.Add(BrowserLine.FullNote(UpgradeUseLabel(use.Kind)));
                if (!string.IsNullOrEmpty(use.TargetItemId) && !targets.Contains(use.TargetItemId))
                    targets.Add(use.TargetItemId);
            }
            BrowserLines.Add(BrowserLine.FullNote(Ui("ui.upgrade_uses_note")));
            if (targets.Count > 0)
            {
                targets.Sort(StringComparer.OrdinalIgnoreCase);
                BrowserLines.Add(BrowserLine.Section(Ui("ui.upgrade_equipment")));
                foreach (string target in targets) BrowserLines.Add(BrowserLine.Item(target, string.Empty));
            }
            LogUpgradeUse(itemId);
        }

        private static string UpgradeUseLabel(string kind)
        {
            switch (kind)
            {
                case "RangeWeapon": return Ui("ui.upgrade_ranged");
                case "MeleeWeapon": return Ui("ui.upgrade_melee");
                case "Armor": return Ui("ui.upgrade_armor");
                case "Mercenary": return Ui("ui.upgrade_genomes");
                case "MercenaryClass": return Ui("ui.upgrade_classes");
                case "PactRitual": return Ui("ui.upgrade_pacts");
                case "Implant": return Ui("ui.upgrade_implants");
                case "Augmentation": return Ui("ui.upgrade_augmentations");
                default: return string.Empty;
            }
        }

        private static void AppendOverviewUpgradeUses(string itemId)
        {
            if (!HasUpgradeUse(itemId)) return;
            var categories = new HashSet<string>(StringComparer.Ordinal);
            BrowserLines.Add(BrowserLine.Section(Ui("ui.upgrade_uses")));
            foreach (UpgradeUse use in UpgradeMaterials[itemId])
                if (categories.Add(use.Kind))
                    BrowserLines.Add(BrowserLine.InternalAction(UpgradeUseLabel(use.Kind),
                        BrowserAction.SwitchTab(BrowserTabId.Magnum)));
        }
    }
}
