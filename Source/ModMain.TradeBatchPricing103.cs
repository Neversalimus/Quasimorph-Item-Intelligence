using System;
using System.Collections.Generic;
using System.Reflection;
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
                Type stationType = typeof(Station);
                Type factionType = typeof(Faction);
                Type factionsType = typeof(Factions);
                Type pricesType = typeof(ItemsPrices);
                Type progressionType = typeof(MagnumProgression);
                Type difficultyType = typeof(Difficulty);
                Type basePickupItemType = typeof(BasePickupItem);
                if (!stationType.IsInstanceOfType(station) ||
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

                    MethodInfo buyPrice = TradePriceApi103.BuyPrice;
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

                MethodInfo sellTradePoints = TradePriceApi103.SellTradePoints;
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
