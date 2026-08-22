using System;
using System.Collections.Generic;

namespace ItemIntelligence
{
    public static partial class ModMain
    {
        // One visibility policy for the MCM Information group. Renderers may build only
        // categories enabled here; this prevents hidden content from leaking through
        // Overview, the stats ribbon, detail tabs, or the quick-information path.
        private static bool HasInspectorData(string itemId)
        {
            if (string.IsNullOrEmpty(itemId)) return false;

            string relationId = ResolveStaticRelationItemId(itemId);
            if (ShowMagnumUses &&
                (GetVisibleMagnumRequired(itemId) > 0 || HasVisibleMagnumUses(itemId))) return true;
            if (ShowRecipes &&
                (GetListCount(UsedInRecipes, relationId) > 0 ||
                 GetListCount(CraftedFromRecipes, relationId) > 0 ||
                 GetListCount(StationProductionByOutputItem, relationId) > 0 ||
                 GetListCount(StationProductionByInputItem, relationId) > 0)) return true;
            if (GetDisassemblyOutputCount(itemId) > 0) return true;
            if (ShowAmmoRelations)
            {
                if (WeaponsByItem.ContainsKey(relationId)) return true;
                if (CompatibleWeaponsByAmmo.ContainsKey(relationId)) return true;
            }
            return false;
        }

        private static bool HasVisibleMagnumUses(string itemId)
        {
            if (!ShowMagnumUses) return false;
            List<MagnumUse> uses;
            if (!MagnumUses.TryGetValue(itemId, out uses) || uses == null || uses.Count == 0)
                return false;
            if (ShowFutureMagnumUses) return true;

            for (int i = 0; i < uses.Count; i++)
            {
                MagnumUse use = uses[i];
                if (use == null) continue;
                bool? purchased = CallBool(_magnumProgression, "IsPerkPurchased", use.PerkId);
                if (!purchased.HasValue || purchased.Value) return true;
                bool? available = CallBool(_magnumProgression, "IsAvailableToUpgrade", use.PerkId);
                if (available.HasValue && available.Value) return true;
            }
            return false;
        }

        private static int GetVisibleMagnumRequired(string itemId)
        {
            if (!ShowMagnumUses) return 0;
            if (ShowFutureMagnumUses) return GetSafeMagnumRequired(itemId);

            List<MagnumUse> uses;
            if (!MagnumUses.TryGetValue(itemId, out uses) || uses == null || uses.Count == 0)
                return 0;

            int total = 0;
            for (int i = 0; i < uses.Count; i++)
            {
                MagnumUse use = uses[i];
                if (use == null) continue;
                bool? purchased = CallBool(_magnumProgression, "IsPerkPurchased", use.PerkId);
                if (purchased.HasValue && purchased.Value) continue;
                bool? available = CallBool(_magnumProgression, "IsAvailableToUpgrade", use.PerkId);
                // With future branches hidden, only a definitely available branch may
                // contribute to a numeric total. Unknown rows remain visible but cannot
                // safely be counted without leaking a possibly future requirement.
                if (available.HasValue && available.Value)
                    total += Math.Max(0, use.Quantity);
            }
            return total;
        }

        private static MagnumSnapshot GetMagnumSnapshot(string itemId)
        {
            List<MagnumUse> uses;
            if (!MagnumUses.TryGetValue(itemId, out uses) || uses == null || uses.Count == 0)
                return null;

            MagnumSnapshot result = new MagnumSnapshot();
            for (int i = 0; i < uses.Count; i++)
            {
                MagnumUse use = uses[i];
                if (use == null) continue;
                bool? purchased = CallBool(_magnumProgression, "IsPerkPurchased", use.PerkId);
                if (purchased.HasValue && purchased.Value) continue;

                bool? available = CallBool(_magnumProgression, "IsAvailableToUpgrade", use.PerkId);
                if (!available.HasValue)
                {
                    result.UnknownRequired += Math.Max(0, use.Quantity);
                }
                else if (available.Value)
                {
                    result.Current.Add(use);
                    result.CurrentRequired += Math.Max(0, use.Quantity);
                }
                else
                {
                    result.Future.Add(use);
                    result.FutureRequired += Math.Max(0, use.Quantity);
                }
            }

            result.TotalRemaining = result.CurrentRequired +
                (ShowFutureMagnumUses ? result.UnknownRequired + result.FutureRequired : 0);

            PriceSnapshot vanilla;
            if (_magnumProgression == null && ShowFutureMagnumUses &&
                PriceByItem.TryGetValue(itemId, out vanilla))
                result.TotalRemaining = vanilla.Required;
            return result;
        }

    }
}
