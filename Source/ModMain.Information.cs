using System;
using System.Collections.Generic;
using System.Globalization;

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
                 GetListCount(CraftedFromRecipes, relationId) > 0)) return true;
            if (GetDisassemblyOutputCount(itemId) > 0) return true;
            if (ShowSources && GetListCount(BarterSources, itemId) > 0) return true;
            if (ShowTradeInformation && GetListCount(BarterConsumers, itemId) > 0) return true;
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

        private static void UpdateBrowserStats(string itemId)
        {
            if (_browserStatsText == null) return;
            _browserStatsText.color = new UnityEngine.Color(0.43f, 0.69f, 0.59f, 1f);

            List<string> parts = new List<string>();
            if (ShowMagnumUses)
            {
                int magnum = GetVisibleMagnumRequired(itemId);
                string magnumText = magnum > 0
                    ? magnum.ToString(CultureInfo.InvariantCulture)
                    : (HasVisibleMagnumUses(itemId) ? Ui("ui.done") : "0");
                parts.Add(Ui("stats.magnum") + " " + magnumText);
            }

            if (ShowRecipes)
            {
                int used = GetUniqueRecipeOutputCount(itemId);
                int crafted = GetStaticRelationListCount(CraftedFromRecipes, itemId);
                parts.Add(Ui("stats.recipes") + " " + used + "/" + crafted);
            }

            if (ShowSources || ShowTradeInformation)
            {
                int sources = ShowSources ? GetUniqueRelationCount(itemId, true) : 0;
                int consumers = ShowTradeInformation ? GetUniqueRelationCount(itemId, false) : 0;
                if (string.Equals(_marketItemId, itemId, StringComparison.OrdinalIgnoreCase))
                {
                    for (int i = 0; i < MarketEntries.Count; i++)
                    {
                        LiveMarketEntry entry = MarketEntries[i];
                        if (entry == null) continue;
                        if (ShowSources && entry.StationSells) sources++;
                        if (ShowTradeInformation && entry.StationBuys) consumers++;
                    }
                }
                parts.Add(Ui("stats.trade") + " " + FormatVisibleTradeCounts(sources, consumers));
            }

            if (ShowAmmoRelations)
                parts.Add(Ui("stats.ammo") + " " + GetAmmoRelationCount(itemId));

            _browserStatsText.text = NormalizeModUiText(string.Join("   |   ", parts.ToArray()));
        }

        private static string FormatVisibleTradeCounts(int sources, int consumers)
        {
            if (ShowSources && ShowTradeInformation)
                return sources.ToString(CultureInfo.InvariantCulture) + "/" +
                    consumers.ToString(CultureInfo.InvariantCulture);
            if (ShowSources)
                return sources.ToString(CultureInfo.InvariantCulture);

            return consumers.ToString(CultureInfo.InvariantCulture);
        }

        private static int GetUniqueRelationCount(string itemId, bool sources)
        {
            HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            List<TradeRelation> relations;
            Dictionary<string, List<TradeRelation>> index = sources ? BarterSources : BarterConsumers;
            if (!index.TryGetValue(itemId, out relations) || relations == null) return 0;

            for (int i = 0; i < relations.Count; i++)
            {
                TradeRelation relation = relations[i];
                if (relation != null && !string.IsNullOrEmpty(relation.Id)) seen.Add(relation.Id);
            }
            return seen.Count;
        }
    }
}
