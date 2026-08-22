using System;
using System.Collections.Generic;

namespace ItemIntelligence
{
    /// <summary>
    /// Cheap, data-driven Overview strength evaluation. It reads already-owned indexes only
    /// and never starts Loot/market/faction work just to choose a landing tab.
    /// </summary>
    public static partial class ModMain
    {
        private sealed class OverviewSignalSnapshot
        {
            public int CombatModes;
            public int ChipUnlocks;
            public bool BaronPact;
            public int AmmoRelations;
            public bool AmmoItem;
            public int RecipeRelations;
            public int DisassemblyRelations;
            public int MagnumRelations;

            public int MeaningfulGroups
            {
                get
                {
                    int n = 0;
                    if (CombatModes > 0) n++;
                    if (ChipUnlocks > 0) n++;
                    if (BaronPact) n++;
                    if (AmmoRelations > 0) n++;
                    if (RecipeRelations > 0) n++;
                    if (DisassemblyRelations > 0) n++;
                    if (MagnumRelations > 0) n++;
                    return n;
                }
            }

            public bool StrongOverview
            {
                get { return CombatModes > 0 || ChipUnlocks > 0 || BaronPact || AmmoItem || AmmoRelations >= 2; }
            }
        }

        private static OverviewSignalSnapshot EvaluateOverviewSignals(string itemId)
        {
            OverviewSignalSnapshot s = new OverviewSignalSnapshot();
            if (string.IsNullOrEmpty(itemId)) return s;

            string relationId = ResolveStaticRelationItemId(itemId);
            if (string.IsNullOrEmpty(relationId)) relationId = itemId;

            if (ShowAmmoRelations)
            {
                List<WeaponModeDescriptor> modes = GetWeaponModesForItem(itemId);
                s.CombatModes = modes == null ? 0 : modes.Count;

                WeaponInfo weapon;
                List<string> weapons;
                if (WeaponsByItem.TryGetValue(relationId, out weapon) && weapon != null)
                    s.AmmoRelations = weapon.CompatibleAmmo == null ? 0 : weapon.CompatibleAmmo.Count;
                else if (CompatibleWeaponsByAmmo.TryGetValue(relationId, out weapons) && weapons != null)
                {
                    s.AmmoRelations = weapons.Count;
                    s.AmmoItem = weapons.Count > 0;
                }
            }

            List<string> unlockedByChip;
            if (ItemsUnlockedByDatadisk.TryGetValue(itemId, out unlockedByChip) && unlockedByChip != null)
                s.ChipUnlocks = unlockedByChip.Count;

            s.BaronPact = IsBaronPactItem(itemId);
            if (ShowRecipes)
                s.RecipeRelations = GetOverviewRecipeRelationCount(itemId);
            if (_compatDisassembly && _disassemblyWarmupComplete)
                s.DisassemblyRelations = GetDisassemblyOutputCount(itemId) + GetDisassemblySourceCount(itemId);
            if (ShowMagnumUses)
                s.MagnumRelations = GetVisibleMagnumRequired(itemId);
            return s;
        }
    }
}
