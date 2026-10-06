namespace ItemIntelligence
{
    public static partial class ModMain
    {
        private static void RunWeaponModeDamageTypeCases()
        {
            string[] modes = new string[] { "slash_2", "stab_2" };

            Check(ResolveWeaponModeAmmoIdFromSlots(
                modes,
                new string[] { "implicted_sword", "implicted_knife" },
                "fallback_ammo",
                "slash_2") == "implicted_sword",
                "first firemode resolves its paired override ammo");

            Check(ResolveWeaponModeAmmoIdFromSlots(
                modes,
                new string[] { "implicted_sword", "implicted_knife" },
                "fallback_ammo",
                "stab_2") == "implicted_knife",
                "second firemode resolves its paired override ammo");

            Check(ResolveWeaponModeAmmoIdFromSlots(
                modes,
                new string[] { "", "implicted_knife" },
                "fallback_ammo",
                "slash_2") == "fallback_ammo",
                "empty override slot falls back to default ammo without shifting slots");

            Check(ResolveWeaponModeAmmoIdFromSlots(
                modes,
                new string[] { "", "implicted_knife" },
                "fallback_ammo",
                "stab_2") == "implicted_knife",
                "second override remains bound to the second mode after an empty first slot");

            Check(ResolveWeaponModeAmmoIdFromSlots(
                modes,
                new string[] { "a", "b" },
                "fallback_ammo",
                "unknown_mode") == string.Empty,
                "explicit firemode mapping fails closed for an unknown mode");

            Check(ResolveWeaponModeAmmoIdFromSlots(
                new string[0],
                new string[0],
                "fallback_ammo",
                "rifle_1") == "fallback_ammo",
                "record without positional mode data may use its default ammo");

            Check(ResolveWeaponModeAmmoIdFromSlots(
                modes,
                null,
                "fallback_ammo",
                "slash_2") == "fallback_ammo",
                "missing override array uses default ammo");

            Check(ResolveWeaponModeAmmoIdFromSlots(
                modes,
                new string[] { "a", "b" },
                "fallback_ammo",
                "") == string.Empty,
                "missing mode identity fails closed");
        }
    }
}
