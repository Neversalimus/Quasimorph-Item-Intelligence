using System;
using System.Collections.Generic;

namespace ItemIntelligence
{
    public static partial class ModMain
    {
        private static void RunWeaponReportCases()
        {
            HashSet<string> known = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "grenade_frag", "grenade_fire", "rifle_basic_ammo"
            };
            HashSet<string> result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            bool active = ResolveExplicitLauncherGrenades("grenade_frag",
                new string[] { "grenade_fire", "grenade_frag" }, known, result);
            Check(active && result.Count == 2 && result.Contains("grenade_frag") &&
                result.Contains("grenade_fire"), "explicit grenade ids only, deduplicated");

            result.Clear();
            active = ResolveExplicitLauncherGrenades("missing_grenade",
                new string[] { "missing_2" }, known, result);
            Check(active && result.Count == 0,
                "unknown launcher grenade items fail closed instead of admitting rifle caliber");

            result.Clear();
            active = ResolveExplicitLauncherGrenades("",
                new string[0], known, result);
            Check(!active && result.Count == 0, "ordinary ammo must be unaffected");

            result.Clear();
            active = ResolveExplicitLauncherGrenades("grenade_frag",
                null, known, result);
            Check(active && result.Count == 1,
                "default grenade works without explicit additional choices");
        }
    }
}
