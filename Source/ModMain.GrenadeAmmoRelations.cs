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
            HashSet<string> grenadeIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (string itemId in KnownItemIds)
                if (Data.Items.GetSimpleRecord<GrenadeRecord>(itemId, true) != null)
                    grenadeIds.Add(itemId);
            int corrected = 0;
            int unresolved = 0;
            foreach (KeyValuePair<string, WeaponInfo> pair in WeaponsByItem)
            {
                WeaponInfo weapon = pair.Value;
                if (weapon == null || weapon.CompatibleAmmo == null) continue;
                WeaponRecord record = Data.Items.GetSimpleRecord<WeaponRecord>(pair.Key, true);
                if (!IsGrenadeLauncherRecord(record)) continue;

                HashSet<string> explicitGrenades = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                ResolveLauncherGrenades(record.AllowedGrenadeIds, grenadeIds, explicitGrenades);

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

        private static bool IsGrenadeLauncherRecord(WeaponRecord record)
        {
            return record != null && record.WeaponClass == WeaponClass.GrenadeLauncher;
        }

        private static string ResolveDefaultGrenadeDamageType(WeaponRecord weapon)
        {
            if (!IsGrenadeLauncherRecord(weapon) || Data.Items == null || Data.Explosions == null ||
                string.IsNullOrEmpty(weapon.DefaultGrenadeId)) return string.Empty;
            GrenadeRecord grenade = Data.Items.GetSimpleRecord<GrenadeRecord>(weapon.DefaultGrenadeId, true);
            if (grenade == null || string.IsNullOrEmpty(grenade.Explosion)) return string.Empty;
            ExplosionRecord explosion = Data.Explosions.GetRecord(grenade.Explosion, true);
            if (explosion == null || !explosion.Parameters.GainDmgToCreature) return string.Empty;
            return explosion.Parameters.DamageType ?? string.Empty;
        }

        // ReloadLauncherSystem.CanReload / CanReloadWeaponFromStorage:
        // null allows every GrenadeRecord; an empty list allows none. DefaultGrenadeId
        // only fills a newly created weapon and does not override a reload restriction.
        private static void ResolveLauncherGrenades(
            IEnumerable<string> allowedGrenadeIds, HashSet<string> grenadeIds,
            HashSet<string> output)
        {
            if (grenadeIds == null || output == null) return;
            foreach (string id in allowedGrenadeIds ?? grenadeIds)
            {
                if (!string.IsNullOrEmpty(id) && grenadeIds.Contains(id)) output.Add(id);
            }
        }
    }
}
