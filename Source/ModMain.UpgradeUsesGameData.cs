using System.Collections.Generic;
using MGSC;

namespace ItemIntelligence
{
    public static partial class ModMain
    {
        // Same record precedence as MagnumDevelopmentSystem.GetItemProjectType;
        // GetSimpleRecord also handles composite items without constructing one.
        private static MagnumProjectType ResolveUpgradeEquipmentKind(string itemId)
        {
            WeaponRecord weapon = Data.Items.GetSimpleRecord<WeaponRecord>(itemId, true);
            if (weapon != null) return weapon.IsMelee ? MagnumProjectType.MeleeWeapon : MagnumProjectType.RangeWeapon;
            if (Data.Items.GetSimpleRecord<ArmorRecord>(itemId, true) != null) return MagnumProjectType.Armor;
            if (Data.Items.GetSimpleRecord<HelmetRecord>(itemId, true) != null) return MagnumProjectType.Helmet;
            if (Data.Items.GetSimpleRecord<LeggingsRecord>(itemId, true) != null) return MagnumProjectType.Leggings;
            if (Data.Items.GetSimpleRecord<BootsRecord>(itemId, true) != null) return MagnumProjectType.Boots;
            return MagnumProjectType.None;
        }

        private static void AddCloneAndPactUpgradeUses(Dictionary<string, List<UpgradeUse>> materials)
        {
            // These operations consume the item itself. They are not project-price
            // rows: AugmentationSystem and SkullRitualSystem own their semantics.
            foreach (string itemId in KnownItemIds)
            {
                if (Data.Items.GetSimpleRecord<PactComponentRecord>(itemId, true) != null)
                    UpgradeUseReader.AddUse(materials, itemId, "PactRitual", string.Empty);
                if (Data.Items.GetSimpleRecord<ImplantRecord>(itemId, true) != null)
                    UpgradeUseReader.AddUse(materials, itemId, "Implant", string.Empty);
                if (Data.Items.GetSimpleRecord<AugmentationRecord>(itemId, true) != null)
                    UpgradeUseReader.AddUse(materials, itemId, "Augmentation", string.Empty);
            }
        }
    }
}
