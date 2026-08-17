using System;
using System.Collections.Generic;
using System.Globalization;
using MGSC;
using UnityEngine;

namespace ItemIntelligence
{
    public static partial class ModMain
    {
        // v1.7.39: read-only nominal weapon damage per one action point.
        //
        // The current vanilla build has no weapon/fire-mode AP-cost field. A successful
        // firearm shot or melee attack is one player action / one AP; all
        // FireMode.WeaponCastsCount hits are processed inside that action.
        //
        // Damage semantics intentionally exclude character perks/effects, crits,
        // accuracy, armor/resistance, status damage and reload amortization. Ranged
        // projection uses WeaponRecord.DefaultAmmoId only. Melee projection is the
        // neutral weapon contribution: WeaponRecord.Damage * FireMode.DamageMult.
        // Character CreatureData.MeleeDamage is deliberately excluded. Vanilla weapons
        // with GetMeleeDamageFromCreature=true are omitted because their damage is
        // character-derived rather than a static item value.
        private sealed class WeaponModeDamagePerAp
        {
            public readonly int MinDamage;
            public readonly int MaxDamage;
            public readonly string DefaultAmmoId;
            public readonly float AmmoDamageMult;
            public readonly float ModeDamageMult;
            public readonly int BulletCastsPerShot;
            public readonly int WeaponCastsCount;

            public WeaponModeDamagePerAp(
                int minDamage, int maxDamage, string defaultAmmoId,
                float ammoDamageMult, float modeDamageMult,
                int bulletCastsPerShot, int weaponCastsCount)
            {
                MinDamage = minDamage;
                MaxDamage = maxDamage;
                DefaultAmmoId = defaultAmmoId ?? string.Empty;
                AmmoDamageMult = ammoDamageMult;
                ModeDamageMult = modeDamageMult;
                BulletCastsPerShot = bulletCastsPerShot;
                WeaponCastsCount = weaponCastsCount;
            }
        }

        private static readonly Dictionary<string, WeaponModeDamagePerAp> WeaponModeDamagePerApByKey =
            new Dictionary<string, WeaponModeDamagePerAp>(StringComparer.Ordinal);
        private static readonly HashSet<string> WeaponModeDamagePerApMisses =
            new HashSet<string>(StringComparer.Ordinal);
        private static readonly HashSet<string> WeaponModeDamagePerApLoggedKeys =
            new HashSet<string>(StringComparer.Ordinal);
        private static bool _weaponModeDamagePerApFormulaLogged;
        private static bool _weaponModeMeleeDamagePerApFormulaLogged;

        private static void ResetWeaponModeDamagePerApCache()
        {
            WeaponModeDamagePerApByKey.Clear();
            WeaponModeDamagePerApMisses.Clear();
            WeaponModeDamagePerApLoggedKeys.Clear();
            _weaponModeDamagePerApFormulaLogged = false;
            _weaponModeMeleeDamagePerApFormulaLogged = false;
        }

        private static bool TryCalculateWeaponModeDamagePerAp(
            string modeKey, WeaponModeStaticStats stats, out int minDamage, out int maxDamage)
        {
            minDamage = 0;
            maxDamage = 0;
            if (string.IsNullOrEmpty(modeKey) || stats == null) return false;

            WeaponModeDamagePerAp cached;
            if (WeaponModeDamagePerApByKey.TryGetValue(modeKey, out cached) && cached != null)
            {
                minDamage = cached.MinDamage;
                maxDamage = cached.MaxDamage;
                return true;
            }
            if (WeaponModeDamagePerApMisses.Contains(modeKey)) return false;

            WeaponRecord weapon = ResolveWeaponModeWeaponRecord(modeKey);
            if (weapon == null)
            {
                WeaponModeDamagePerApMisses.Add(modeKey);
                return false;
            }

            try
            {
                object damageBox = weapon.Damage;
                int baseMin = GetIntMember(damageBox, "minDmg", -1);
                int baseMax = GetIntMember(damageBox, "maxDmg", -1);
                if (baseMin < 0 || baseMax < 0)
                {
                    WeaponModeDamagePerApMisses.Add(modeKey);
                    return false;
                }
                if (baseMax < baseMin)
                {
                    int swap = baseMin;
                    baseMin = baseMax;
                    baseMax = swap;
                }

                float modeMult = stats.DamageMult.HasValue ? stats.DamageMult.Value : 1f;
                int casts = stats.WeaponCastsCount;
                if (casts <= 0)
                {
                    WeaponModeDamagePerApMisses.Add(modeKey);
                    return false;
                }

                if (weapon.IsMelee)
                {
                    // Vanilla melee combat adds CreatureData.MeleeDamage and other
                    // character state before applying FireMode.DamageMult. This browser
                    // metric is intentionally neutral/static, so only the weapon's own
                    // damage contribution is shown. For creature-derived melee weapons
                    // vanilla replaces the weapon damage with CreatureData.MeleeDamage;
                    // there is no truthful character-independent value to display.
                    if (weapon.GetMeleeDamageFromCreature)
                    {
                        WeaponModeDamagePerApMisses.Add(modeKey);
                        return false;
                    }

                    int meleeTotalMin = Mathf.RoundToInt(baseMin * modeMult) * casts;
                    int meleeTotalMax = Mathf.RoundToInt(baseMax * modeMult) * casts;
                    if (meleeTotalMax < meleeTotalMin)
                    {
                        int swap = meleeTotalMin;
                        meleeTotalMin = meleeTotalMax;
                        meleeTotalMax = swap;
                    }

                    WeaponModeDamagePerAp meleeResult = new WeaponModeDamagePerAp(
                        meleeTotalMin, meleeTotalMax, string.Empty, 1f, modeMult, 1, casts);
                    WeaponModeDamagePerApByKey[modeKey] = meleeResult;
                    minDamage = meleeTotalMin;
                    maxDamage = meleeTotalMax;

                    if (!_weaponModeMeleeDamagePerApFormulaLogged)
                    {
                        _weaponModeMeleeDamagePerApFormulaLogged = true;
                        Debug.Log("[ItemIntelligence][WeaponModeDamageAP] meleeFormula=Round(WeaponRecord.Damage*FireMode.DamageMult)*WeaponCastsCount; AP=1 per melee action; CreatureData.MeleeDamage/perks/effects excluded; GetMeleeDamageFromCreature omitted.");
                    }
                    if (ModderMode && WeaponModeDamagePerApLoggedKeys.Count < 12 && WeaponModeDamagePerApLoggedKeys.Add(modeKey))
                    {
                        string itemId;
                        WeaponModeItemIdByKey.TryGetValue(modeKey, out itemId);
                        string rawId;
                        WeaponModeRawIdByKey.TryGetValue(modeKey, out rawId);
                        Debug.Log("[ItemIntelligence][WeaponModeDamageAPValue] item=" + (itemId ?? string.Empty) +
                            ", mode=" + (rawId ?? string.Empty) +
                            ", melee=true" +
                            ", base=" + baseMin.ToString(CultureInfo.InvariantCulture) + "-" + baseMax.ToString(CultureInfo.InvariantCulture) +
                            ", modeMult=" + modeMult.ToString("0.###", CultureInfo.InvariantCulture) +
                            ", casts=" + casts.ToString(CultureInfo.InvariantCulture) +
                            ", damagePerAP=" + meleeTotalMin.ToString(CultureInfo.InvariantCulture) + "-" + meleeTotalMax.ToString(CultureInfo.InvariantCulture) + ".");
                    }
                    return true;
                }

                string ammoId = weapon.DefaultAmmoId ?? string.Empty;
                if (string.IsNullOrEmpty(ammoId) || Data.Items == null)
                {
                    WeaponModeDamagePerApMisses.Add(modeKey);
                    return false;
                }

                AmmoRecord ammo = Data.Items.GetSimpleRecord<AmmoRecord>(ammoId, true);
                if (ammo == null)
                {
                    WeaponModeDamagePerApMisses.Add(modeKey);
                    return false;
                }

                float ammoMult = ammo.DamageMult;
                int fragments = ((int)weapon.WeaponClass == 31) ? 1 : Mathf.Max(1, ammo.BulletCastsPerShot);

                // ProcessShooting uses one fragment for vanilla WeaponClass 31; otherwise it
                // divides the ranged multiplier by BulletCastsPerShot and executes that many fragments. Mirror that ordering so shotguns and
                // other multi-projectile ammo are not incorrectly multiplied by pellet count.
                float perFragmentMult = modeMult * ammoMult / fragments;
                int minPerFragment = Mathf.RoundToInt(baseMin * perFragmentMult);
                int maxPerFragment = Mathf.RoundToInt(baseMax * perFragmentMult);
                int totalMin = minPerFragment * fragments * casts;
                int totalMax = maxPerFragment * fragments * casts;
                if (totalMax < totalMin)
                {
                    int swap = totalMin;
                    totalMin = totalMax;
                    totalMax = swap;
                }

                WeaponModeDamagePerAp result = new WeaponModeDamagePerAp(
                    totalMin, totalMax, ammoId, ammoMult, modeMult, fragments, casts);
                WeaponModeDamagePerApByKey[modeKey] = result;
                minDamage = totalMin;
                maxDamage = totalMax;

                if (!_weaponModeDamagePerApFormulaLogged)
                {
                    _weaponModeDamagePerApFormulaLogged = true;
                    Debug.Log("[ItemIntelligence][WeaponModeDamageAP] formula=Round(baseDamage*FireMode.DamageMult*DefaultAmmo.DamageMult/BulletCastsPerShot)*BulletCastsPerShot*WeaponCastsCount; AP=1 per firearm action; neutral character modifiers.");
                }
                if (ModderMode && WeaponModeDamagePerApLoggedKeys.Count < 12 && WeaponModeDamagePerApLoggedKeys.Add(modeKey))
                {
                    string itemId;
                    WeaponModeItemIdByKey.TryGetValue(modeKey, out itemId);
                    string rawId;
                    WeaponModeRawIdByKey.TryGetValue(modeKey, out rawId);
                    Debug.Log("[ItemIntelligence][WeaponModeDamageAPValue] item=" + (itemId ?? string.Empty) +
                        ", mode=" + (rawId ?? string.Empty) +
                        ", ammo=" + ammoId +
                        ", base=" + baseMin.ToString(CultureInfo.InvariantCulture) + "-" + baseMax.ToString(CultureInfo.InvariantCulture) +
                        ", ammoMult=" + ammoMult.ToString("0.###", CultureInfo.InvariantCulture) +
                        ", modeMult=" + modeMult.ToString("0.###", CultureInfo.InvariantCulture) +
                        ", fragments=" + fragments.ToString(CultureInfo.InvariantCulture) +
                        ", casts=" + casts.ToString(CultureInfo.InvariantCulture) +
                        ", damagePerAP=" + totalMin.ToString(CultureInfo.InvariantCulture) + "-" + totalMax.ToString(CultureInfo.InvariantCulture) + ".");
                }
                return true;
            }
            catch
            {
                WeaponModeDamagePerApMisses.Add(modeKey);
                return false;
            }
        }

        private static string FormatWeaponModeDamagePerAp(int minDamage, int maxDamage)
        {
            if (maxDamage <= minDamage)
                return minDamage.ToString(CultureInfo.InvariantCulture);
            return minDamage.ToString(CultureInfo.InvariantCulture) + "-" + maxDamage.ToString(CultureInfo.InvariantCulture);
        }
    }
}
