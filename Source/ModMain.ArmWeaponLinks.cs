using System;
using System.Collections.Generic;
using MGSC;

namespace ItemIntelligence
{
    public static partial class ModMain
    {
        private static string ValidatedLimbWeapon(string augmentationId)
        {
            if (Data.Items == null || string.IsNullOrEmpty(augmentationId) ||
                augmentationId.StartsWith("nature_", StringComparison.OrdinalIgnoreCase))
                return string.Empty;
            AugmentationRecord augmentation = null;
            try { augmentation = Data.Items.GetSimpleRecord<AugmentationRecord>(augmentationId, true); }
            catch { }
            if (augmentation == null) return string.Empty;
            string candidate = "nature_" + augmentationId;
            if (!KnownItemIds.Contains(candidate)) return string.Empty;
            try
            {
                return Data.Items.GetSimpleRecord<WeaponRecord>(candidate, true) == null
                    ? string.Empty : candidate;
            }
            catch { return string.Empty; }
        }

        private static void AppendOverviewArmWeaponLinks(string itemId)
        {
            if (!ShowAmmoRelations || !_compatAmmo || !_ammoWarmupComplete ||
                Data.Items == null || string.IsNullOrEmpty(itemId)) return;
            string id = ResolveStaticRelationItemId(itemId);
            if (string.IsNullOrEmpty(id)) id = itemId;
            string weapon = ValidatedLimbWeapon(id);
            if (!string.IsNullOrEmpty(weapon))
            {
                BrowserLines.Add(BrowserLine.Section(Ui("ui.arm_builtin_weapon")));
                BrowserLines.Add(ItemWithProductionChip(weapon, string.Empty));
                return;
            }
            if (!id.StartsWith("nature_", StringComparison.OrdinalIgnoreCase)) return;
            string parent = id.Substring("nature_".Length);
            if (!KnownItemIds.Contains(parent) ||
                !string.Equals(ValidatedLimbWeapon(parent), id,
                    StringComparison.OrdinalIgnoreCase)) return;
            BrowserLines.Add(BrowserLine.Section(Ui("ui.arm_source_augmentation")));
            BrowserLines.Add(ItemWithProductionChip(parent, string.Empty));
        }
    }
}
