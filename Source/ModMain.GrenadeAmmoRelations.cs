using System;
using System.Collections.Generic;
using MGSC;

namespace ItemIntelligence
{
    public static partial class ModMain
    {
        // Grenade launchers have their own vanilla item allowlist. A generic ammo
        // caliber join can produce false positives (e.g. 7.97 rifle rounds).
        // Run after normal ammo warmup, then repair both forward and reverse views.
        private static void CorrectExplicitGrenadeLauncherRelations()
        {
            if (Data.Items == null) return;
            int corrected = 0;
            int unresolved = 0;
            foreach (KeyValuePair<string, WeaponInfo> pair in WeaponsByItem)
            {
                WeaponInfo weapon = pair.Value;
                if (weapon == null || weapon.CompatibleAmmo == null) continue;
                WeaponRecord record = null;
                try { record = Data.Items.GetSimpleRecord<WeaponRecord>(pair.Key, true); }
                catch { }
                if (record == null) continue;

                HashSet<string> explicitGrenades = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                bool hasExplicitLauncherContract = ResolveExplicitLauncherGrenades(
                    record.DefaultGrenadeId, record.AllowedGrenadeIds, KnownItemIds, explicitGrenades);
                if (!hasExplicitLauncherContract) continue;

                for (int i = 0; i < weapon.CompatibleAmmo.Count; i++)
                {
                    string oldId = weapon.CompatibleAmmo[i];
                    List<string> reverse;
                    if (CompatibleWeaponsByAmmo.TryGetValue(oldId, out reverse) && reverse != null)
                    {
                        reverse.Remove(weapon.ItemId);
                        if (reverse.Count == 0) CompatibleWeaponsByAmmo.Remove(oldId);
                    }
                }
                weapon.CompatibleAmmo.Clear();

                foreach (string grenadeId in explicitGrenades)
                {
                    weapon.CompatibleAmmo.Add(grenadeId);
                    AddToList(CompatibleWeaponsByAmmo, grenadeId, weapon.ItemId);
                }
                corrected++;
                if (explicitGrenades.Count == 0) unresolved++;
                VerboseLog("[ItemIntelligence][GrenadeAmmo] item=" + pair.Key +
                    ", grenades=" + explicitGrenades.Count +
                    ", genericAmmoSuppressed=True.");
            }
            if (corrected > 0)
                VerboseLog("[ItemIntelligence][GrenadeAmmo] corrected=" + corrected +
                    ", unresolved=" + unresolved + ".");
        }

        // The values are verified game item ids, never a caliber or a name guess.
        private static bool ResolveExplicitLauncherGrenades(
            string defaultGrenadeId, IEnumerable<string> allowedGrenadeIds,
            HashSet<string> knownItemIds, HashSet<string> output)
        {
            bool hasContract = !string.IsNullOrWhiteSpace(defaultGrenadeId);
            if (!string.IsNullOrWhiteSpace(defaultGrenadeId) && knownItemIds != null &&
                knownItemIds.Contains(defaultGrenadeId.Trim()) && output != null)
                output.Add(defaultGrenadeId.Trim());

            if (allowedGrenadeIds != null)
            {
                foreach (string raw in allowedGrenadeIds)
                {
                    if (string.IsNullOrWhiteSpace(raw)) continue;
                    hasContract = true;
                    string id = raw.Trim();
                    if (knownItemIds != null && knownItemIds.Contains(id) && output != null)
                        output.Add(id);
                }
            }
            return hasContract;
        }
    }
}
