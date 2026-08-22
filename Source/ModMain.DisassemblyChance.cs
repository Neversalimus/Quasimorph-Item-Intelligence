using System;
using MGSC;
using UnityEngine;

namespace ItemIntelligence
{
    public static partial class ModMain
    {
        // Vanilla ItemInteractionSystem.Disassemble starts from
        // Data.Global.SpawnItemOnDisassembleChance, but overrides it to 100% when
        // item.Id == Data.Global.DeathGiftId. Keep that special-case in one owner so
        // forward and reverse disassembly projections cannot disagree.
        private static string _disassemblyDeathGiftItemId = string.Empty;

        private static void ResetDisassemblySpecialChanceContract()
        {
            _disassemblyDeathGiftItemId = string.Empty;
        }

        private static void ResolveDisassemblySpecialChanceContract()
        {
            _disassemblyDeathGiftItemId = string.Empty;
            try
            {
                object global = GetStaticMember(typeof(Data), "Global");
                _disassemblyDeathGiftItemId = GetStringMember(global, "DeathGiftId") ?? string.Empty;
            }
            catch { }

            if (!string.IsNullOrEmpty(_disassemblyDeathGiftItemId))
                Debug.Log("[ItemIntelligence] Disassembly special chance resolved: DeathGiftId=" +
                    _disassemblyDeathGiftItemId + " => 100% direct dismantle.");
        }

        private static float GetDirectDisassemblyChancePercent(string itemId)
        {
            if (!string.IsNullOrEmpty(itemId) &&
                !string.IsNullOrEmpty(_disassemblyDeathGiftItemId) &&
                string.Equals(itemId, _disassemblyDeathGiftItemId, StringComparison.OrdinalIgnoreCase))
                return 100f;

            return _disassemblyRollChancePercent;
        }

        private static bool IsRandomDirectDisassemblyItem(string itemId)
        {
            float chance = GetDirectDisassemblyChancePercent(itemId);
            return chance >= 0f && chance < 99.999f;
        }
    }
}
