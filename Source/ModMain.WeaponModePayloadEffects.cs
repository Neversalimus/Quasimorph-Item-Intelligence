using System;
using System.Collections.Generic;
using MGSC;

namespace ItemIntelligence
{
    public static partial class ModMain
    {
        // Traits/status are properties of the ammo payload paired with a specific
        // Firemodes slot. WeaponRecord.Traits are NOT substituted, because those
        // apply to the whole weapon and can misstate differences between modes.
        private static void AppendWeaponModePayloadEffectRows(
            string modeKey, List<KeyValuePair<string, string>> rows)
        {
            if (rows == null || Data.Items == null) return;
            WeaponRecord weapon = ResolveWeaponModeWeaponRecord(modeKey);
            if (weapon == null) return;
            string ammoId = ResolveWeaponModeAmmoId(modeKey, weapon);
            if (string.IsNullOrEmpty(ammoId)) return;

            AmmoRecord ammo = null;
            try { ammo = Data.Items.GetSimpleRecord<AmmoRecord>(ammoId, true); }
            catch { }
            if (ammo == null) return;

            HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            int added = 0;
            if (ammo.Traits != null)
            {
                foreach (string traitId in ammo.Traits)
                {
                    if (string.IsNullOrWhiteSpace(traitId) || !seen.Add(traitId.Trim())) continue;
                    string value = LocalizeWeaponModePayloadTrait(traitId.Trim());
                    if (string.IsNullOrEmpty(value)) continue;
                    rows.Add(new KeyValuePair<string, string>(Ui("ui.mode_effect"), value));
                    if (++added >= 3) break; // bound the presentation, never invent traits
                }
            }

            string statusId = (ammo.StatusEffectId ?? string.Empty).Trim();
            if (!string.IsNullOrEmpty(statusId) &&
                !string.Equals(statusId, "none", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(statusId, "null", StringComparison.OrdinalIgnoreCase))
            {
                string name = LocalizeCandidates(new string[]
                {
                    "effect." + statusId + ".name",
                    "status." + statusId + ".name",
                    "statuseffect." + statusId + ".name",
                    "status_effect." + statusId + ".name",
                    statusId
                }, statusId);
                if (!string.IsNullOrEmpty(name))
                    rows.Add(new KeyValuePair<string, string>(Ui("ui.mode_status_effect"),
                        NormalizeGameText(name)));
            }
        }

        private static string LocalizeWeaponModePayloadTrait(string rawId)
        {
            if (string.IsNullOrWhiteSpace(rawId)) return string.Empty;
            string id = rawId.Trim();
            string localized = LocalizeCandidates(new string[]
            {
                "trait." + id + ".name",
                "trait." + id,
                "weapon.trait." + id + ".name",
                "ammo.trait." + id + ".name",
                "item.trait." + id + ".name",
                id
            }, id);
            if (!string.IsNullOrEmpty(localized) &&
                !string.Equals(localized, id, StringComparison.OrdinalIgnoreCase))
                return NormalizeGameText(localized);

            // The exact vanilla Wounding id has a localized fail-closed QII label.
            if (string.Equals(id, "Wounding", StringComparison.OrdinalIgnoreCase))
                return Ui("ui.mode_trait_wounding");
            // Other unknown modded trait ids stay visibly raw; no name-based effect inference.
            return id;
        }
    }
}
