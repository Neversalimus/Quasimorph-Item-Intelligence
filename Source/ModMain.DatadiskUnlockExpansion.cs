using System;
using System.Collections.Generic;
using MGSC;

namespace ItemIntelligence
{
    public static partial class ModMain
    {
        private static readonly Dictionary<string, List<string>> DatadiskOutputsByUnlockId =
            new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, List<string>> DatadiskArmorSetsByItem =
            new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        private static bool _datadiskArmorSetsReady;

        private static void ResetDatadiskUnlockExpansion()
        {
            DatadiskOutputsByUnlockId.Clear();
            DatadiskArmorSetsByItem.Clear();
            _datadiskArmorSetsReady = false;
        }

        private static WeaponRecord GetBaseWeaponRecord(string itemId)
        {
            object record;
            if (string.IsNullOrEmpty(itemId) || !ItemRecordsById.TryGetValue(itemId, out record)) return null;
            WeaponRecord weapon = record as WeaponRecord;
            CompositeItemRecord composite = record as CompositeItemRecord;
            return weapon ?? (composite == null ? null : composite.GetRecord<WeaponRecord>());
        }

        private static void EnsureDatadiskArmorSets()
        {
            if (_datadiskArmorSetsReady) return;
            _datadiskArmorSetsReady = true;
            if (Data.ArmorSets == null) return;
            // UnlockDatadisk uses the first set containing the selected armor item.
            // Do not merge overlapping sets or infer membership from item IDs/names.
            foreach (ArmorSetRecord set in Data.ArmorSets.Records)
            {
                if (set == null || set.Items == null) continue;
                foreach (string itemId in set.Items)
                    if (!string.IsNullOrEmpty(itemId) && !DatadiskArmorSetsByItem.ContainsKey(itemId))
                        DatadiskArmorSetsByItem[itemId] = new List<string>(set.Items);
            }
        }

        private static List<string> GetDatadiskUnlockOutputs(string unlockId)
        {
            List<string> result;
            if (DatadiskOutputsByUnlockId.TryGetValue(unlockId, out result)) return result;
            result = new List<string>();
            DatadiskOutputsByUnlockId[unlockId] = result;
            AddKnownDatadiskOutput(result, unlockId);

            WeaponRecord weapon = GetBaseWeaponRecord(unlockId);
            if (weapon != null)
            {
                // The production-item branch also unlocks exactly this default payload.
                if (weapon.WeaponClass == WeaponClass.GrenadeLauncher)
                    AddKnownDatadiskOutput(result, weapon.DefaultGrenadeId);
                else if (!string.IsNullOrEmpty(weapon.RequiredAmmo))
                    AddKnownDatadiskOutput(result, weapon.DefaultAmmoId);
                return result;
            }

            object record;
            ItemRecordsById.TryGetValue(unlockId, out record);
            CompositeItemRecord composite = record as CompositeItemRecord;
            bool armor = record is IArmorRecord || (composite != null &&
                (composite.GetRecord<ArmorRecord>() != null || composite.GetRecord<HelmetRecord>() != null ||
                 composite.GetRecord<LeggingsRecord>() != null || composite.GetRecord<BootsRecord>() != null));
            if (!armor) return result;
            EnsureDatadiskArmorSets();
            List<string> setItems;
            if (DatadiskArmorSetsByItem.TryGetValue(unlockId, out setItems))
                foreach (string itemId in setItems) AddKnownDatadiskOutput(result, itemId);
            return result;
        }

        private static void AddKnownDatadiskOutput(List<string> result, string itemId)
        {
            if (!string.IsNullOrEmpty(itemId) && KnownItemIds.Contains(itemId) && !result.Contains(itemId))
                result.Add(itemId);
        }
    }
}
