using System;
using MGSC;

namespace ItemIntelligence
{
    public static partial class ModMain
    {
        // Vanilla MissionSystem.MissionFinishedByPlayer has four independent Purge Brigade
        // reward loops. Keep this resolver deliberately narrow and exact: the predicates
        // below mirror the game's compiler-generated lambdas from Assembly-CSharp
        // EFF608C5118735359CD07FEAD8A8219E1CFB557E3A5A57517DB4428F04834B8B.
        [Flags]
        private enum ScavengerRewardClass
        {
            None = 0,
            Resources = 1,
            ArmorWeapons = 2,
            FoodMeds = 4,
            AmmoGrenades = 8
        }

        private static bool TryResolveScavengerRewardClass(
            string itemId,
            out CompositeItemRecord composite,
            out ItemRecord primary,
            out ScavengerRewardClass classes)
        {
            composite = null;
            primary = null;
            classes = ScavengerRewardClass.None;
            if (string.IsNullOrEmpty(itemId) || itemId.IndexOf("_custom", StringComparison.Ordinal) >= 0)
                return false;

            object root;
            if (!ItemRecordsById.TryGetValue(itemId, out root) || root == null)
                return false;

            composite = root as CompositeItemRecord;
            if (composite == null) return false;

            primary = composite.PrimaryRecord as ItemRecord;
            if (primary == null || primary.Categories == null || primary.Categories.Count == 0)
                return false;

            if (MatchesScavengerRewardClass(composite, ScavengerRewardClass.Resources))
                classes |= ScavengerRewardClass.Resources;
            if (MatchesScavengerRewardClass(composite, ScavengerRewardClass.ArmorWeapons))
                classes |= ScavengerRewardClass.ArmorWeapons;
            if (MatchesScavengerRewardClass(composite, ScavengerRewardClass.FoodMeds))
                classes |= ScavengerRewardClass.FoodMeds;
            if (MatchesScavengerRewardClass(composite, ScavengerRewardClass.AmmoGrenades))
                classes |= ScavengerRewardClass.AmmoGrenades;

            return classes != ScavengerRewardClass.None;
        }

        private static bool MatchesScavengerRewardClass(CompositeItemRecord composite, ScavengerRewardClass rewardClass)
        {
            if (composite == null) return false;
            if (rewardClass == ScavengerRewardClass.Resources)
                return composite.GetRecord<TrashRecord>() != null;
            if (rewardClass == ScavengerRewardClass.ArmorWeapons)
                return composite.GetRecord<WeaponRecord>() != null ||
                    composite.GetRecord<HelmetRecord>() != null ||
                    composite.GetRecord<ArmorRecord>() != null ||
                    composite.GetRecord<LeggingsRecord>() != null ||
                    composite.GetRecord<BootsRecord>() != null;
            if (rewardClass == ScavengerRewardClass.FoodMeds)
                return composite.GetRecord<ConsumableRecord>() != null ||
                    composite.GetRecord<FixationMedicineRecord>() != null;
            if (rewardClass == ScavengerRewardClass.AmmoGrenades)
                return composite.GetRecord<AmmoRecord>() != null ||
                    composite.GetRecord<GrenadeRecord>() != null;
            return false;
        }
    }
}
