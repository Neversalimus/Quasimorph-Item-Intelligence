using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace ItemIntelligence
{
    public static partial class ModMain
    {
        // v1.7.37-test2: detailed mode stats are an immutable read-only projection of
        // the same runtime FireMode records already indexed by the Ammo feature.
        private static readonly Dictionary<string, WeaponModeStaticStats> WeaponModeStatsByRawId =
            new Dictionary<string, WeaponModeStaticStats>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, WeaponModeStaticStats> WeaponModeStatsByKey =
            new Dictionary<string, WeaponModeStaticStats>(StringComparer.Ordinal);

        private sealed class WeaponModeStaticStats
        {
            public readonly int AmmoPerShot;
            public readonly int WeaponCastsCount;
            public readonly bool? RequiredAllAmmoToShot;
            public readonly float? Accuracy;
            public readonly float? DamageMult;
            public readonly float? ScatterAngle;
            public readonly float? DelayInSecsBetweenShots;

            public WeaponModeStaticStats(
                int ammoPerShot, int weaponCastsCount, bool? requiredAllAmmoToShot,
                float? accuracy, float? damageMult, float? scatterAngle, float? delayInSecsBetweenShots)
            {
                AmmoPerShot = ammoPerShot;
                WeaponCastsCount = weaponCastsCount;
                RequiredAllAmmoToShot = requiredAllAmmoToShot;
                Accuracy = accuracy;
                DamageMult = damageMult;
                ScatterAngle = scatterAngle;
                DelayInSecsBetweenShots = delayInSecsBetweenShots;
            }

            public bool HasCombatNumbers
            {
                get
                {
                    return Accuracy.HasValue || DamageMult.HasValue || ScatterAngle.HasValue ||
                           DelayInSecsBetweenShots.HasValue || AmmoPerShot > 0 || WeaponCastsCount > 0 ||
                           RequiredAllAmmoToShot.HasValue;
                }
            }
        }

        private static void BuildWeaponModeStatsIndex()
        {
            WeaponModeStatsByRawId.Clear();
            int accuracyCount = 0;
            int damageCount = 0;
            int scatterCount = 0;
            int delayCount = 0;
            int castsCount = 0;
            int ammoCount = 0;
            int requiredAllAmmoCount = 0;

            foreach (KeyValuePair<string, object> pair in WeaponModeRecordsById)
            {
                string rawId = pair.Key ?? string.Empty;
                object record = pair.Value;
                if (string.IsNullOrEmpty(rawId) || record == null) continue;

                WeaponModeStaticStats stats = ProjectWeaponModeStats(record);
                if (stats == null) continue;
                int ammoPerShot = stats.AmmoPerShot;
                int weaponCasts = stats.WeaponCastsCount;
                bool? requiredAllAmmo = stats.RequiredAllAmmoToShot;
                float? accuracy = stats.Accuracy;
                float? damageMult = stats.DamageMult;
                float? scatterAngle = stats.ScatterAngle;
                float? delay = stats.DelayInSecsBetweenShots;
                if (!stats.HasCombatNumbers) continue;
                WeaponModeStatsByRawId[rawId] = stats;

                if (ammoPerShot > 0) ammoCount++;
                if (weaponCasts > 0) castsCount++;
                if (requiredAllAmmo.HasValue) requiredAllAmmoCount++;
                if (accuracy.HasValue) accuracyCount++;
                if (damageMult.HasValue) damageCount++;
                if (scatterAngle.HasValue) scatterCount++;
                if (delay.HasValue) delayCount++;
            }

            string schema = WeaponModeStatsByRawId.Count == 0
                ? "NONE"
                : (WeaponModeStatsByRawId.Count == WeaponModeRecordsById.Count &&
                   accuracyCount == WeaponModeStatsByRawId.Count &&
                   damageCount == WeaponModeStatsByRawId.Count &&
                   scatterCount == WeaponModeStatsByRawId.Count &&
                   delayCount == WeaponModeStatsByRawId.Count
                    ? "FULL" : "PARTIAL");

            Debug.Log("[ItemIntelligence][WeaponModeStats] records=" + WeaponModeRecordsById.Count.ToString(CultureInfo.InvariantCulture) +
                ", indexed=" + WeaponModeStatsByRawId.Count.ToString(CultureInfo.InvariantCulture) +
                ", ammo=" + ammoCount.ToString(CultureInfo.InvariantCulture) +
                ", casts=" + castsCount.ToString(CultureInfo.InvariantCulture) +
                ", requiredAllAmmo=" + requiredAllAmmoCount.ToString(CultureInfo.InvariantCulture) +
                ", accuracy=" + accuracyCount.ToString(CultureInfo.InvariantCulture) +
                ", damage=" + damageCount.ToString(CultureInfo.InvariantCulture) +
                ", scatter=" + scatterCount.ToString(CultureInfo.InvariantCulture) +
                ", delay=" + delayCount.ToString(CultureInfo.InvariantCulture) +
                ", schema=" + schema + ".");
        }


        private static WeaponModeStaticStats ProjectWeaponModeStats(object record)
        {
            if (record == null) return null;
            WeaponModeStaticStats stats = new WeaponModeStaticStats(
                GetIntMember(record, "AmmoPerShot", -1),
                GetIntMember(record, "WeaponCastsCount", -1),
                GetBoolMember(record, "RequiredAllAmmoToShot"),
                GetWeaponModeFloatMember(record, "Accuracy"),
                GetWeaponModeFloatMember(record, "DamageMult"),
                GetWeaponModeFloatMember(record, "ScatterAngle"),
                GetWeaponModeFloatMember(record, "DelayInSecsBetweenShots"));
            return stats.HasCombatNumbers ? stats : null;
        }

        private static float? GetWeaponModeFloatMember(object target, string memberName)
        {
            object raw = GetMember(target, memberName);
            if (raw == null) return null;
            try
            {
                if (raw is float) return (float)raw;
                if (raw is double) return (float)(double)raw;
                if (raw is decimal) return (float)(decimal)raw;
                return Convert.ToSingle(raw, CultureInfo.InvariantCulture);
            }
            catch { return null; }
        }
    }
}
