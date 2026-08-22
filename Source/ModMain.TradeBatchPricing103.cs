using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using MGSC;

namespace ItemIntelligence
{
    public static partial class ModMain
    {
        private static int GetTradeBatchSampleQuantity(bool stationBuys, int? stock)
        {
            if (stationBuys) return 5;
            if (!stock.HasValue || stock.Value <= 0) return 0;
            return Math.Min(stock.Value, 5);
        }



        private static bool TryGetExactStationBatchPrice103(
            object station, string itemId, bool stationBuys, int quantity,
            out int totalPrice, out int lastUnitPrice)
        {
            totalPrice = 0;
            lastUnitPrice = 0;
            if (station == null || string.IsNullOrEmpty(itemId) || quantity <= 0) return false;

            try
            {
                Type tradeType = AccessTools.TypeByName("MGSC.TradeSystem");
                Type stationType = AccessTools.TypeByName("MGSC.Station");
                Type factionType = AccessTools.TypeByName("MGSC.Faction");
                Type factionsType = AccessTools.TypeByName("MGSC.Factions");
                Type pricesType = AccessTools.TypeByName("MGSC.ItemsPrices");
                Type progressionType = AccessTools.TypeByName("MGSC.MagnumProgression");
                Type difficultyType = AccessTools.TypeByName("MGSC.Difficulty");
                Type basePickupItemType = AccessTools.TypeByName("MGSC.BasePickupItem");
                if (tradeType == null || stationType == null || !stationType.IsInstanceOfType(station) ||
                    pricesType == null || _itemsPrices == null || !pricesType.IsInstanceOfType(_itemsPrices) ||
                    progressionType == null)
                    return false;

                if (!stationBuys)
                {
                    object factions = _factionsState;
                    if (factions == null && factionsType != null)
                        factions = ResolveStateModule(factionsType);
                    if (factions == null || factionsType == null || !factionsType.IsInstanceOfType(factions))
                        return false;
                    _factionsState = factions;

                    MethodInfo buyPrice = null;
                    MethodInfo[] buyMethods = tradeType.GetMethods(StaticFlags);
                    Type quantityMapType = typeof(Dictionary<string, int>);
                    for (int i = 0; i < buyMethods.Length; i++)
                    {
                        MethodInfo method = buyMethods[i];
                        if (!string.Equals(method.Name, "GetBuyPrice", StringComparison.Ordinal)) continue;
                        ParameterInfo[] p = method.GetParameters();
                        if (p.Length != 5 || p[0].ParameterType != progressionType ||
                            p[1].ParameterType != factionsType || p[2].ParameterType != pricesType ||
                            p[3].ParameterType != stationType || !p[4].ParameterType.IsAssignableFrom(quantityMapType))
                            continue;
                        buyPrice = method;
                        break;
                    }
                    if (buyPrice == null) return false;

                    Dictionary<string, int> quantityMap = new Dictionary<string, int>(StringComparer.Ordinal)
                    {
                        { itemId, quantity }
                    };
                    object raw = buyPrice.Invoke(
                        null, new object[] { _magnumProgression, factions, _itemsPrices, station, quantityMap });
                    int parsed;
                    if (!TryExtractPriceValue(raw, out parsed) || parsed < 0) return false;
                    totalPrice = parsed;
                    if (quantity == 1)
                    {
                        lastUnitPrice = parsed;
                        return true;
                    }

                    quantityMap[itemId] = quantity - 1;
                    object rawBeforeLast = buyPrice.Invoke(
                        null, new object[] { _magnumProgression, factions, _itemsPrices, station, quantityMap });
                    int beforeLast;
                    if (!TryExtractPriceValue(rawBeforeLast, out beforeLast) || beforeLast < 0 || beforeLast > parsed) return false;
                    lastUnitPrice = parsed - beforeLast;
                    return true;
                }

                object faction = ResolveStationFaction(station);
                if (faction == null || factionType == null || !factionType.IsInstanceOfType(faction) ||
                    basePickupItemType == null || difficultyType == null)
                    return false;

                if (_difficultyState == null)
                    _difficultyState = ResolveStateModule(difficultyType);
                if (_difficultyState == null || !difficultyType.IsInstanceOfType(_difficultyState))
                    return false;

                MethodInfo sellTradePoints = null;
                MethodInfo[] methods = tradeType.GetMethods(StaticFlags);
                Type soldMapType = typeof(Dictionary<string, int>);
                for (int i = 0; i < methods.Length; i++)
                {
                    MethodInfo method = methods[i];
                    if (!string.Equals(method.Name, "GetItemSellTradePoints", StringComparison.Ordinal)) continue;
                    ParameterInfo[] p = method.GetParameters();
                    if (p.Length != 8 || p[0].ParameterType != progressionType ||
                        p[1].ParameterType != factionType || p[2].ParameterType != stationType ||
                        p[3].ParameterType != pricesType || p[4].ParameterType != difficultyType ||
                        !basePickupItemType.IsAssignableFrom(p[5].ParameterType) ||
                        !p[6].ParameterType.IsAssignableFrom(soldMapType) ||
                        p[7].ParameterType != typeof(bool))
                        continue;
                    sellTradePoints = method;
                    break;
                }
                if (sellTradePoints == null) return false;

                BasePickupItem previewItem = CreateBrowserTooltipPreviewItem(itemId);
                if (previewItem == null) return false;
                previewItem.StackCount = 1;

                Dictionary<string, int> soldItemsCount = new Dictionary<string, int>(StringComparer.Ordinal);
                int total = 0;
                for (int i = 0; i < quantity; i++)
                {
                    object raw = sellTradePoints.Invoke(
                        null,
                        new object[]
                        {
                            _magnumProgression,
                            faction,
                            station,
                            _itemsPrices,
                            _difficultyState,
                            previewItem,
                            soldItemsCount,
                            false
                        });
                    int parsed;
                    if (!TryExtractPriceValue(raw, out parsed) || parsed < 0) return false;
                    checked { total += parsed; }
                    lastUnitPrice = parsed;
                }

                totalPrice = total;
                return true;
            }
            catch (Exception ex)
            {
                LogRuntimeBoundaryWarningOnce(
                    "trade.price.batch103",
                    "Exact 1.0.3 batch Trade price could not be reconstructed; batch totals fail closed.",
                    ex);
                return false;
            }
        }


    }
}
