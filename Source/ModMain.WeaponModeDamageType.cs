using System;
using System.Collections;
using System.Collections.Generic;
using MGSC;

namespace ItemIntelligence
{
    public static partial class ModMain
    {
        private static readonly Dictionary<string, string> WeaponModeDamageTypeByKey =
            new Dictionary<string, string>(StringComparer.Ordinal);
        private static readonly HashSet<string> WeaponModeDamageTypeMisses =
            new HashSet<string>(StringComparer.Ordinal);
        private static readonly HashSet<string> WeaponModeDamageTypeLoggedKeys =
            new HashSet<string>(StringComparer.Ordinal);
        private static readonly HashSet<string> VanillaDamageTypeIds =
            new HashSet<string>(new string[]
            {
                "blunt", "pierce", "lacer", "fire", "cold", "poison", "shock",
                "beam", "explosion", "plasma", "chaos", "proton", "cryo"
            }, StringComparer.OrdinalIgnoreCase);

        private static void ResetWeaponModeDamageTypeCache()
        {
            WeaponModeDamageTypeByKey.Clear();
            WeaponModeDamageTypeMisses.Clear();
            WeaponModeDamageTypeLoggedKeys.Clear();
        }

        // WeaponRecord stores Firemodes and OverrideAmmo as positionally paired arrays.
        // Preserve empty override slots: compacting the array would silently bind the
        // second mode to the first mode's ammo and report a false damage type.
        private static string[] ReadWeaponModeStringSlots(
            object owner, string collectionMember, string firstLegacyMember, string secondLegacyMember)
        {
            List<string> result = new List<string>();
            if (owner == null) return result.ToArray();

            object raw = GetMember(owner, collectionMember);
            IEnumerable sequence = raw as IEnumerable;
            if (sequence != null && !(raw is string))
            {
                foreach (object entry in sequence)
                    result.Add(entry == null ? string.Empty : Convert.ToString(entry) ?? string.Empty);
                if (result.Count > 0) return result.ToArray();
            }

            // Narrow compatibility fallback for older/custom records that expose the
            // same two positional slots as separate members.
            string first = GetStringMember(owner, firstLegacyMember);
            string second = GetStringMember(owner, secondLegacyMember);
            if (!string.IsNullOrEmpty(first) || !string.IsNullOrEmpty(second))
            {
                result.Add(first ?? string.Empty);
                result.Add(second ?? string.Empty);
            }
            return result.ToArray();
        }

        private static string ResolveWeaponModeAmmoIdFromSlots(
            string[] firemodes, string[] overrideAmmo, string defaultAmmoId, string rawModeId)
        {
            string fallback = (defaultAmmoId ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(rawModeId)) return string.Empty;

            if (firemodes == null || firemodes.Length == 0)
                return fallback;

            int modeIndex = -1;
            for (int i = 0; i < firemodes.Length; i++)
            {
                if (string.Equals(
                    (firemodes[i] ?? string.Empty).Trim(),
                    rawModeId.Trim(),
                    StringComparison.OrdinalIgnoreCase))
                {
                    modeIndex = i;
                    break;
                }
            }

            // A weapon with an explicit Firemodes list owns the positional mapping.
            // If the current mode cannot be found in it, do not guess from another slot.
            if (modeIndex < 0) return string.Empty;

            if (overrideAmmo != null && modeIndex < overrideAmmo.Length)
            {
                string overridden = (overrideAmmo[modeIndex] ?? string.Empty).Trim();
                if (!string.IsNullOrEmpty(overridden)) return overridden;
            }

            return fallback;
        }

        private static string ResolveWeaponModeAmmoId(string modeKey, WeaponRecord weapon)
        {
            if (weapon == null || string.IsNullOrEmpty(modeKey)) return string.Empty;

            string rawModeId;
            if (!WeaponModeRawIdByKey.TryGetValue(modeKey, out rawModeId) ||
                string.IsNullOrEmpty(rawModeId))
                return string.Empty;

            string[] firemodes = ReadWeaponModeStringSlots(
                weapon, "Firemodes", "Firemode1", "Firemode2");
            string[] overrideAmmo = ReadWeaponModeStringSlots(
                weapon, "OverrideAmmo", "OverrideAmmo1", "OverrideAmmo2");
            string defaultAmmoId = FirstNonEmpty(
                GetStringMember(weapon, "DefaultAmmoId"),
                GetItemIdDeep(GetMember(weapon, "DefaultAmmo"), 0));

            return ResolveWeaponModeAmmoIdFromSlots(
                firemodes, overrideAmmo, defaultAmmoId, rawModeId);
        }

        private static string ResolveWeaponModeDamageTypeDisplay(string rawDamageType)
        {
            string raw = (rawDamageType ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(raw)) return string.Empty;

            string localized = LocalizeCandidates(new string[]
            {
                "damage_type." + raw + ".name",
                "damage_type." + raw,
                "damagetype." + raw + ".name",
                "damagetype." + raw,
                "dmgtype." + raw,
                "damage." + raw + ".name",
                "damage." + raw,
                raw
            }, raw);

            if (!string.IsNullOrEmpty(localized) &&
                !string.Equals(localized, raw, StringComparison.OrdinalIgnoreCase))
                return NormalizeGameText(localized);

            // Vanilla's raw IDs are stable gameplay data; provide QII-localized names
            // only for that audited set. Unknown/modded IDs remain exact raw tokens.
            if (VanillaDamageTypeIds.Contains(raw))
                return Ui("ui.damage_type." + raw.ToLowerInvariant());

            return raw;
        }

        private static bool TryResolveWeaponModeDamageType(
            string modeKey, out string label, out string value)
        {
            label = string.Empty;
            value = string.Empty;
            if (string.IsNullOrEmpty(modeKey)) return false;

            string cached;
            if (WeaponModeDamageTypeByKey.TryGetValue(modeKey, out cached))
            {
                WeaponRecord cachedWeapon = ResolveWeaponModeWeaponRecord(modeKey);
                if (cachedWeapon == null) return false;
                label = Ui(cachedWeapon.IsMelee
                    ? "ui.mode_damage_type"
                    : "ui.mode_damage_type_default");
                value = ResolveWeaponModeDamageTypeDisplay(cached);
                return !string.IsNullOrEmpty(value);
            }
            if (WeaponModeDamageTypeMisses.Contains(modeKey)) return false;

            WeaponRecord weapon = ResolveWeaponModeWeaponRecord(modeKey);
            if (weapon == null)
            {
                WeaponModeDamageTypeMisses.Add(modeKey);
                return false;
            }

            string ammoId = ResolveWeaponModeAmmoId(modeKey, weapon);
            if (string.IsNullOrEmpty(ammoId) || Data.Items == null)
            {
                WeaponModeDamageTypeMisses.Add(modeKey);
                return false;
            }

            AmmoRecord ammo = null;
            try { ammo = Data.Items.GetSimpleRecord<AmmoRecord>(ammoId, true); }
            catch { }
            if (ammo == null)
            {
                WeaponModeDamageTypeMisses.Add(modeKey);
                return false;
            }

            string rawDamageType = GetStringMember(ammo, "DmgType").Trim();
            if (string.IsNullOrEmpty(rawDamageType))
            {
                WeaponModeDamageTypeMisses.Add(modeKey);
                return false;
            }

            WeaponModeDamageTypeByKey[modeKey] = rawDamageType;
            label = Ui(weapon.IsMelee
                ? "ui.mode_damage_type"
                : "ui.mode_damage_type_default");
            value = ResolveWeaponModeDamageTypeDisplay(rawDamageType);

            if (WeaponModeDamageTypeLoggedKeys.Count < 24 &&
                WeaponModeDamageTypeLoggedKeys.Add(modeKey))
            {
                string itemId;
                WeaponModeItemIdByKey.TryGetValue(modeKey, out itemId);
                string rawModeId;
                WeaponModeRawIdByKey.TryGetValue(modeKey, out rawModeId);
                VerboseLog("[ItemIntelligence][WeaponModeDamageType] item=" +
                    (itemId ?? string.Empty) +
                    ", mode=" + (rawModeId ?? string.Empty) +
                    ", ammo=" + ammoId +
                    ", type=" + rawDamageType +
                    ", melee=" + weapon.IsMelee + ".");
            }

            return !string.IsNullOrEmpty(value);
        }
    }
}
