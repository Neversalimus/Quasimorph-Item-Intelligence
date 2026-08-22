using System;
using System.Collections.Generic;
using System.Globalization;
using MGSC;
using UnityEngine;

namespace ItemIntelligence
{
    public static partial class ModMain
    {
        // v1.7.40-test1: critical counterpart of the neutral/static Damage/AP metric.
        // Vanilla applies DmgInfo.critDmg to an already rounded damage instance and
        // adds the current creature's GetCritDamageBonus to that multiplier. This
        // browser value deliberately uses the weapon multiplier only (character bonus
        // = 0), matching the existing Damage/AP (default) scope.
        private sealed class WeaponModeCriticalDamagePerAp
        {
            public readonly int MinDamage;
            public readonly int MaxDamage;
            public readonly float CritDamageMult;

            public WeaponModeCriticalDamagePerAp(int minDamage, int maxDamage, float critDamageMult)
            {
                MinDamage = minDamage;
                MaxDamage = maxDamage;
                CritDamageMult = critDamageMult;
            }
        }

        private static readonly Dictionary<string, WeaponModeCriticalDamagePerAp> WeaponModeCriticalDamagePerApByKey =
            new Dictionary<string, WeaponModeCriticalDamagePerAp>(StringComparer.Ordinal);
        private static readonly HashSet<string> WeaponModeCriticalDamagePerApMisses =
            new HashSet<string>(StringComparer.Ordinal);
        private static readonly HashSet<string> WeaponModeCriticalDamagePerApLoggedKeys =
            new HashSet<string>(StringComparer.Ordinal);
        private static bool _weaponModeCriticalDamagePerApFormulaLogged;

        private static void ResetWeaponModeCriticalDamagePerApCache()
        {
            WeaponModeCriticalDamagePerApByKey.Clear();
            WeaponModeCriticalDamagePerApMisses.Clear();
            WeaponModeCriticalDamagePerApLoggedKeys.Clear();
            _weaponModeCriticalDamagePerApFormulaLogged = false;
        }

        private static bool TryCalculateWeaponModeCriticalDamagePerAp(
            string modeKey, WeaponModeStaticStats stats, out int minDamage, out int maxDamage)
        {
            minDamage = 0;
            maxDamage = 0;
            if (string.IsNullOrEmpty(modeKey) || stats == null) return false;

            WeaponModeCriticalDamagePerAp cachedCrit;
            if (WeaponModeCriticalDamagePerApByKey.TryGetValue(modeKey, out cachedCrit) && cachedCrit != null)
            {
                minDamage = cachedCrit.MinDamage;
                maxDamage = cachedCrit.MaxDamage;
                return true;
            }
            if (WeaponModeCriticalDamagePerApMisses.Contains(modeKey)) return false;

            int normalMin;
            int normalMax;
            if (!TryCalculateWeaponModeDamagePerAp(modeKey, stats, out normalMin, out normalMax))
            {
                WeaponModeCriticalDamagePerApMisses.Add(modeKey);
                return false;
            }

            WeaponModeDamagePerAp normalDetail;
            if (!WeaponModeDamagePerApByKey.TryGetValue(modeKey, out normalDetail) || normalDetail == null)
            {
                WeaponModeCriticalDamagePerApMisses.Add(modeKey);
                return false;
            }

            WeaponRecord weapon = ResolveWeaponModeWeaponRecord(modeKey);
            if (weapon == null)
            {
                WeaponModeCriticalDamagePerApMisses.Add(modeKey);
                return false;
            }

            float? critMultValue = GetWeaponModeFloatMember((object)weapon.Damage, "critDmg");
            if (!critMultValue.HasValue || float.IsNaN(critMultValue.Value) ||
                float.IsInfinity(critMultValue.Value) || critMultValue.Value <= 0f)
            {
                WeaponModeCriticalDamagePerApMisses.Add(modeKey);
                return false;
            }

            int fragments = Math.Max(1, normalDetail.BulletCastsPerShot);
            int casts = Math.Max(1, normalDetail.WeaponCastsCount);
            long groupWide = (long)fragments * casts;
            if (groupWide <= 0 || groupWide > int.MaxValue || normalMin < 0 || normalMax < 0)
            {
                WeaponModeCriticalDamagePerApMisses.Add(modeKey);
                return false;
            }
            int group = (int)groupWide;
            if (normalMin % group != 0 || normalMax % group != 0)
            {
                // The normal calculator builds totals from already-rounded damage
                // instances. If that invariant ever changes, fail closed instead of
                // applying the critical multiplier at the wrong rounding stage.
                WeaponModeCriticalDamagePerApMisses.Add(modeKey);
                return false;
            }

            int normalPerHitMin = normalMin / group;
            int normalPerHitMax = normalMax / group;
            float critMult = critMultValue.Value;

            // Exact vanilla ordering: rounded normal damage instance -> multiply by
            // (DmgInfo.critDmg + characterCritBonus) -> RoundToInt. Character bonus is
            // intentionally zero for this static/default browser metric.
            int totalMin, totalMax;
            if (!TryRoundAndScaleDamage(normalPerHitMin * critMult, fragments, casts, out totalMin) ||
                !TryRoundAndScaleDamage(normalPerHitMax * critMult, fragments, casts, out totalMax))
            {
                WeaponModeCriticalDamagePerApMisses.Add(modeKey);
                return false;
            }
            if (totalMax < totalMin)
            {
                int swap = totalMin;
                totalMin = totalMax;
                totalMax = swap;
            }

            WeaponModeCriticalDamagePerAp result =
                new WeaponModeCriticalDamagePerAp(totalMin, totalMax, critMult);
            WeaponModeCriticalDamagePerApByKey[modeKey] = result;
            minDamage = totalMin;
            maxDamage = totalMax;

            if (!_weaponModeCriticalDamagePerApFormulaLogged)
            {
                _weaponModeCriticalDamagePerApFormulaLogged = true;
                Debug.Log("[ItemIntelligence][WeaponModeCriticalDamageAP] formula=Round(NormalDamageInstance*Weapon.Damage.critDmg)*damageInstances; AP=1 per attack action; character GetCritDamageBonus/backstab/perks/effects excluded.");
            }
            if (ModderMode && WeaponModeCriticalDamagePerApLoggedKeys.Count < 12 &&
                WeaponModeCriticalDamagePerApLoggedKeys.Add(modeKey))
            {
                string itemId;
                WeaponModeItemIdByKey.TryGetValue(modeKey, out itemId);
                string rawId;
                WeaponModeRawIdByKey.TryGetValue(modeKey, out rawId);
                Debug.Log("[ItemIntelligence][WeaponModeCriticalDamageAPValue] item=" + (itemId ?? string.Empty) +
                    ", mode=" + (rawId ?? string.Empty) +
                    ", critMult=" + critMult.ToString("0.###", CultureInfo.InvariantCulture) +
                    ", normal=" + normalMin.ToString(CultureInfo.InvariantCulture) + "-" + normalMax.ToString(CultureInfo.InvariantCulture) +
                    ", criticalDamagePerAP=" + totalMin.ToString(CultureInfo.InvariantCulture) + "-" + totalMax.ToString(CultureInfo.InvariantCulture) + ".");
            }
            return true;
        }
    }
}
