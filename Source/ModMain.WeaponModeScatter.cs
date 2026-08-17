using System;
using System.Collections.Generic;
using System.Globalization;
using MGSC;
using UnityEngine;

namespace ItemIntelligence
{
    public static partial class ModMain
    {
        // Vanilla TooltipFactory.BuildFiremodeTooltip uses the exact HUD formula:
        // (FireModeRecord.ScatterAngle + WeaponRecord.BonusScatterAngle)
        //     * Mercenary.CreatureData.GetScatterAngleMult(WeaponRecord)
        //
        // Resolve only the hovered weapon on demand. No catalog scan, no graph walk,
        // no per-frame reflection and no gameplay mutation.
        private static readonly Dictionary<string, WeaponRecord> WeaponModeWeaponRecordsByItem =
            new Dictionary<string, WeaponRecord>(StringComparer.OrdinalIgnoreCase);
        private static bool _weaponModeScatterFormulaLogged;
        private static object _weaponModeCreatures;
        private static readonly HashSet<string> WeaponModeScatterLoggedKeys =
            new HashSet<string>(StringComparer.Ordinal);

        private static void ResetWeaponModeScatterCache()
        {
            WeaponModeWeaponRecordsByItem.Clear();
            _weaponModeScatterFormulaLogged = false;
            _weaponModeCreatures = null;
            WeaponModeScatterLoggedKeys.Clear();
            ResetWeaponModeDamagePerApCache();
        }

        private static bool IsWeaponModeMelee(string modeKey, WeaponModeStaticStats stats)
        {
            WeaponRecord record = ResolveWeaponModeWeaponRecord(modeKey);
            if (record != null)
            {
                try { return record.IsMelee; } catch { }
            }
            return stats != null && stats.AmmoPerShot <= 0 && stats.WeaponCastsCount <= 0;
        }

        private static bool TryCalculateVanillaFiremodeScatter(
            string modeKey, WeaponModeStaticStats stats, out float scatter)
        {
            scatter = 0f;
            if (stats == null || !stats.ScatterAngle.HasValue) return false;

            WeaponRecord record = ResolveWeaponModeWeaponRecord(modeKey);
            if (record == null) return false;

            try
            {
                if (record.IsMelee) return false;

                float multiplier = 1f;
                Player player = ResolveWeaponModePlayer();
                bool liveMultiplier = false;
                if (player != null)
                {
                    Mercenary mercenary = null;
                    try { mercenary = player.Mercenary; } catch { }
                    if (mercenary != null && mercenary.CreatureData != null)
                    {
                        multiplier = mercenary.CreatureData.GetScatterAngleMult(record);
                        liveMultiplier = true;
                    }
                }

                float weaponBonus = record.BonusScatterAngle;
                scatter = (stats.ScatterAngle.Value + weaponBonus) * multiplier;
                if (ModderMode)
                {
                    if (!_weaponModeScatterFormulaLogged)
                    {
                        _weaponModeScatterFormulaLogged = true;
                        Debug.Log("[ItemIntelligence][WeaponModeScatter] formula=(FireMode.ScatterAngle+WeaponRecord.BonusScatterAngle)*CreatureData.GetScatterAngleMult, resolver=GetSimpleRecord<WeaponRecord>, multiplier=" +
                            (liveMultiplier ? "LIVE_MERCENARY" : "NEUTRAL_1") + ".");
                    }
                    if (WeaponModeScatterLoggedKeys.Count < 8 && WeaponModeScatterLoggedKeys.Add(modeKey))
                    {
                        string itemId;
                        WeaponModeItemIdByKey.TryGetValue(modeKey, out itemId);
                        string rawId;
                        WeaponModeRawIdByKey.TryGetValue(modeKey, out rawId);
                        Debug.Log("[ItemIntelligence][WeaponModeScatterValue] item=" + (itemId ?? string.Empty) +
                            ", mode=" + (rawId ?? string.Empty) +
                            ", fireMode=" + stats.ScatterAngle.Value.ToString("0.###", CultureInfo.InvariantCulture) +
                            ", weaponBonus=" + weaponBonus.ToString("0.###", CultureInfo.InvariantCulture) +
                            ", multiplier=" + multiplier.ToString("0.###", CultureInfo.InvariantCulture) +
                            ", final=" + scatter.ToString("0.###", CultureInfo.InvariantCulture) +
                            ", source=" + (liveMultiplier ? "LIVE_MERCENARY" : "NEUTRAL_1") + ".");
                    }
                }
                return Mathf.Abs(scatter) > Mathf.Epsilon;
            }
            catch
            {
                return false;
            }
        }

        private static WeaponRecord ResolveWeaponModeWeaponRecord(string modeKey)
        {
            if (string.IsNullOrEmpty(modeKey)) return null;

            string itemId;
            if (!WeaponModeItemIdByKey.TryGetValue(modeKey, out itemId) || string.IsNullOrEmpty(itemId))
                return null;

            WeaponRecord cached;
            if (WeaponModeWeaponRecordsByItem.TryGetValue(itemId, out cached) && cached != null)
                return cached;

            // Prefer the detached browser preview already created for vanilla item hover.
            // Record<T>() follows vanilla composite-item resolution.
            try
            {
                if (_browserPreviewLiveItem != null &&
                    string.Equals(_browserPreviewLiveItem.Id, itemId, StringComparison.OrdinalIgnoreCase))
                {
                    WeaponRecord previewRecord = _browserPreviewLiveItem.Record<WeaponRecord>();
                    if (previewRecord != null)
                    {
                        WeaponModeWeaponRecordsByItem[itemId] = previewRecord;
                        return previewRecord;
                    }
                }
            }
            catch { }

            // Crucial Test8 fix for CompositeItemRecord weapons: ask ItemsCollection for
            // the concrete WeaponRecord instead of walking wrapper/descriptors.
            try
            {
                if (Data.Items != null)
                {
                    WeaponRecord record = Data.Items.GetSimpleRecord<WeaponRecord>(itemId, true);
                    if (record != null)
                    {
                        WeaponModeWeaponRecordsByItem[itemId] = record;
                        return record;
                    }

                    string relationId = ResolveStaticRelationItemId(itemId);
                    if (!string.IsNullOrEmpty(relationId) &&
                        !string.Equals(relationId, itemId, StringComparison.OrdinalIgnoreCase))
                    {
                        record = Data.Items.GetSimpleRecord<WeaponRecord>(relationId, true);
                        if (record != null)
                        {
                            WeaponModeWeaponRecordsByItem[itemId] = record;
                            return record;
                        }
                    }
                }
            }
            catch { }

            return null;
        }

        private static Player ResolveWeaponModePlayer()
        {
            try
            {
                if (_weaponModeCreatures == null)
                    _weaponModeCreatures = ResolveStateModule(typeof(Creatures));
                return GetMember(_weaponModeCreatures, "Player") as Player;
            }
            catch { return null; }
        }

        private static string FormatWeaponModeScatter(float value)
        {
            CultureInfo culture = IsRussian()
                ? CultureInfo.GetCultureInfo("ru-RU")
                : CultureInfo.InvariantCulture;
            return value.ToString("0.0", culture) + "°";
        }
    }
}
