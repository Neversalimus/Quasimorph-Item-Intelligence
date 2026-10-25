namespace ItemIntelligence
{
    public static partial class ModMain
    {
        private static void RunWeaponReportCases()
        {
            HashSet<string> known = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "grenade_frag", "grenade_fire"
            };
            HashSet<string> result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            ResolveLauncherGrenades(
                new string[] { "grenade_fire", "grenade_frag" }, known, result);
            Check(result.Count == 2 && result.Contains("grenade_frag") &&
                result.Contains("grenade_fire"), "explicit grenade ids only, deduplicated");

            result.Clear();
            ResolveLauncherGrenades(
                new string[] { "missing_2" }, known, result);
            Check(result.Count == 0,
                "unknown launcher grenade items fail closed instead of admitting rifle caliber");

            result.Clear();
            ResolveLauncherGrenades(new string[0], known, result);
            Check(result.Count == 0, "empty allowlist permits no grenade");

            result.Clear();
            ResolveLauncherGrenades(null, known, result);
            Check(result.SetEquals(known), "null allowlist permits every grenade");

            result.Clear();
            ResolveLauncherGrenades(new[] { "grenade_fire", "rifle_basic_ammo" }, known, result);
            Check(result.SetEquals(new[] { "grenade_fire" }), "non-grenade ids are rejected");
        }
    }
}
