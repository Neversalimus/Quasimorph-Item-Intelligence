using System;
using System.Collections.Generic;
using MGSC;

namespace ItemIntelligence
{
    public static partial class ModMain
    {
        private static readonly Dictionary<string, List<string>> ArmWeaponsByAugmentation =
            new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, List<string>> AugmentationsByArmWeapon =
            new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        private static void ResetArmWeaponLinks()
        {
            ArmWeaponsByAugmentation.Clear();
            AugmentationsByArmWeapon.Clear();
        }

        // AugmentationSystem installs WoundSlotIds; the resulting body slot owns
        // BareHandWeapon. Item names do not describe this many-to-many relation.
        // Called inside the existing item warmup, never from a hover or frame loop.
        private static void IndexArmWeaponLinks(string augmentationId)
        {
            if (Data.Items == null || Data.WoundSlots == null ||
                string.IsNullOrEmpty(augmentationId)) return;
            AugmentationRecord augmentation = Data.Items.GetSimpleRecord<AugmentationRecord>(augmentationId, true);
            if (augmentation == null || augmentation.WoundSlotIds == null) return;
            foreach (string slotId in augmentation.WoundSlotIds)
            {
                if (string.IsNullOrEmpty(slotId)) continue;
                WoundSlotRecord slot = Data.WoundSlots.GetRecord(slotId, true);
                string weaponId = slot == null ? string.Empty : slot.BareHandWeapon;
                if (string.IsNullOrEmpty(weaponId) || !KnownItemIds.Contains(weaponId) ||
                    Data.Items.GetSimpleRecord<WeaponRecord>(weaponId, true) == null) continue;
                List<string> existing;
                if (ArmWeaponsByAugmentation.TryGetValue(augmentationId, out existing) &&
                    existing.Contains(weaponId)) continue;
                AddToList(ArmWeaponsByAugmentation, augmentationId, weaponId);
                AddToList(AugmentationsByArmWeapon, weaponId, augmentationId);
            }
        }

        private static void AppendOverviewArmWeaponLinks(string itemId)
        {
            if (!ShowAmmoRelations || !_compatAmmo || !_ammoWarmupComplete ||
                Data.Items == null || string.IsNullOrEmpty(itemId)) return;
            string id = ResolveStaticRelationItemId(itemId);
            if (string.IsNullOrEmpty(id)) id = itemId;
            List<string> linked;
            if (ArmWeaponsByAugmentation.TryGetValue(id, out linked) && linked.Count > 0)
            {
                BrowserLines.Add(BrowserLine.Section(Ui("ui.arm_builtin_weapon")));
                foreach (string weaponId in linked)
                    BrowserLines.Add(ItemWithProductionChip(weaponId, string.Empty));
            }
            if (AugmentationsByArmWeapon.TryGetValue(id, out linked) && linked.Count > 0)
            {
                BrowserLines.Add(BrowserLine.Section(Ui("ui.arm_source_augmentation")));
                foreach (string augmentationId in linked)
                    BrowserLines.Add(ItemWithProductionChip(augmentationId, string.Empty));
            }
        }
    }
}
