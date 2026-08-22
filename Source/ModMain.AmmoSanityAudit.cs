using System;
using System.Collections.Generic;
using MGSC;
using UnityEngine;

namespace ItemIntelligence
{
    public static partial class ModMain
    {
        private static bool _ammoSanityAuditLogged;

        private static void AuditAmmoRelationsAfterWarmup()
        {
            if (_ammoSanityAuditLogged) return;
            _ammoSanityAuditLogged = true;

            int staticMelee = 0;
            int staticMeleeFalseLinks = 0;
            int energyMelee = 0;
            int energyMeleeWithAmmo = 0;

            foreach (KeyValuePair<string, WeaponInfo> pair in WeaponsByItem)
            {
                WeaponInfo weapon = pair.Value;
                if (weapon == null || weapon.Modes == null || weapon.Modes.Count == 0) continue;

                bool staticMeleeWeapon = ShouldSuppressDescriptorAmmoForMelee(weapon);
                bool sawEnergyMelee = false;
                for (int i = 0; i < weapon.Modes.Count; i++)
                {
                    WeaponModeDescriptor mode = weapon.Modes[i];
                    if (mode == null) continue;
                    WeaponRecord record = ResolveWeaponModeWeaponRecord(mode.Key);
                    if (record != null && record.IsMelee && mode.Stats != null && mode.Stats.AmmoPerShot > 0)
                        sawEnergyMelee = true;
                }

                if (staticMeleeWeapon)
                {
                    staticMelee++;
                    staticMeleeFalseLinks += weapon.CompatibleAmmo == null ? 0 : weapon.CompatibleAmmo.Count;
                }
                else if (sawEnergyMelee)
                {
                    energyMelee++;
                    if (weapon.CompatibleAmmo != null && weapon.CompatibleAmmo.Count > 0) energyMeleeWithAmmo++;
                }
            }

            string message = "[ItemIntelligence][AmmoSanity] staticMelee=" + staticMelee +
                ", falseAmmoLinks=" + staticMeleeFalseLinks +
                ", energyMelee=" + energyMelee +
                ", energyMeleeWithAmmo=" + energyMeleeWithAmmo + ".";
            if (staticMeleeFalseLinks == 0) Debug.Log(message);
            else Debug.LogWarning(message + " Static non-energy melee must have zero player-facing ammo relations.");
        }
    }
}
