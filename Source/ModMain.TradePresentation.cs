using System;
using System.Collections.Generic;
using System.Globalization;

namespace ItemIntelligence
{
    public static partial class ModMain
    {
        private static void BuildBrowserTrade(string itemId)
        {
            if (!ShowSources && !ShowTradeInformation) return;
            if (!_compatTrade) { AddCompatibilityUnavailableLine("Trade"); return; }

            EnsureTradeStateDependencies();
            if (SpaceObjectRecordsById.Count == 0) BuildSpaceObjectIndex();
            if (!string.Equals(_marketItemId, itemId, StringComparison.OrdinalIgnoreCase)) StartMarketScan(itemId);

            PrepareTradePresentationEntries();
            MarkTradeMissionCountdownUiRendered();
            int sellToPlayerCount = TradeSellEntries.Count;
            int buyFromPlayerCount = TradeBuyEntries.Count;
            bool current103 = IsCurrent103FeatureAssembly();
            bool cards103 = current103 && !UsePreviousTradeLayout;
            bool table103 = current103 && UsePreviousTradeLayout;

            if (current103 && ((ShowSources && sellToPlayerCount > 0) || (ShowTradeInformation && buyFromPlayerCount > 0)))
                BrowserLines.Add(BrowserLine.FullNote(Ui(table103 ? "ui.trade_previous_note" : "ui.trade_repricing_note")));

            if (ShowSources && sellToPlayerCount > 0)
            {
                BrowserLines.Add(BrowserLine.FullSection(Ui("ui.buy_at_stations") + "  •  " + sellToPlayerCount.ToString(CultureInfo.InvariantCulture)));
                if (table103)
                    BrowserLines.Add(BrowserLine.TradeHeader(Ui("ui.station"), Ui("ui.next"), Ui("ui.batch"), Ui("ui.stock"), Ui("ui.mission"), Ui("ui.travel")));
                else if (!cards103)
                    BrowserLines.Add(BrowserLine.TradeHeader(Ui("ui.station"), Ui("ui.price"), Ui("ui.stock"), Ui("ui.mission"), Ui("ui.travel")));

                for (int i = 0; i < TradeSellEntries.Count; i++)
                {
                    LiveMarketEntry entry = TradeSellEntries[i];
                    if (cards103) AddTradeStationCard103(entry, false);
                    else if (table103) AddTradeStationTable103(entry, false);
                    else AddLegacyTradeStationRow(entry, false);
                }
            }

            if (ShowTradeInformation && buyFromPlayerCount > 0)
            {
                BrowserLines.Add(BrowserLine.FullSection(Ui("ui.sell_to_stations") + "  •  " + buyFromPlayerCount.ToString(CultureInfo.InvariantCulture)));
                if (table103)
                    BrowserLines.Add(BrowserLine.TradeHeader(Ui("ui.station"), Ui("ui.next"), Ui("ui.batch"), string.Empty, Ui("ui.mission"), Ui("ui.travel")));
                else if (!cards103)
                    BrowserLines.Add(BrowserLine.TradeHeader(Ui("ui.station"), Ui("ui.price"), string.Empty, Ui("ui.mission"), Ui("ui.travel")));

                for (int i = 0; i < TradeBuyEntries.Count; i++)
                {
                    LiveMarketEntry entry = TradeBuyEntries[i];
                    if (cards103) AddTradeStationCard103(entry, true);
                    else if (table103) AddTradeStationTable103(entry, true);
                    else AddLegacyTradeStationRow(entry, true);
                }
            }

            if ((ShowSources && sellToPlayerCount > 0) || (ShowTradeInformation && buyFromPlayerCount > 0))
                BrowserLines.Add(BrowserLine.Note(Ui("ui.click_a_station_to_open_its_location_on_the_star")));

            if (_runtimeFallbackResolveActive) BrowserLines.Add(BrowserLine.Note(Ui("ui.connecting_to_market_data")));
            if (_marketScanActive)
                BrowserLines.Add(BrowserLine.Note(Ui("ui.market_processed") + _marketStationIndex.ToString(CultureInfo.InvariantCulture) + "/" + MarketStations.Count.ToString(CultureInfo.InvariantCulture)));
            else if (_marketScanComplete && MarketStations.Count == 0)
                BrowserLines.Add(BrowserLine.Note(Ui("ui.station_list_is_unavailable_from_the_current_gam")));

            List<TradeRelation> sources = new List<TradeRelation>();
            List<TradeRelation> consumers = new List<TradeRelation>();
            List<TradeRelation> list;
            if (ShowSources && BarterSources.TryGetValue(itemId, out list) && list != null) sources.AddRange(list);
            if (ShowTradeInformation && BarterConsumers.TryGetValue(itemId, out list) && list != null) consumers.AddRange(list);

            if (sources.Count > 0) { BrowserLines.Add(BrowserLine.Section(Ui("ui.station_economy_recipe_output"))); AddBrowserBarterRelations(sources, true); }
            if (consumers.Count > 0) { BrowserLines.Add(BrowserLine.Section(Ui("ui.station_economy_recipe_input"))); AddBrowserBarterRelations(consumers, false); }
            if (BrowserLines.Count == 0 && (ShowSources || ShowTradeInformation)) BrowserLines.Add(BrowserLine.Note(Ui("ui.no_trade_relationships_found_yet")));
        }

        private static void AddTradeStationCard103(LiveMarketEntry entry, bool stationBuys)
        {
            int? first = stationBuys ? entry.StationBuyPrice : entry.StationSellPrice;
            int? last = stationBuys ? entry.StationBuyLastBatchPrice : entry.StationSellLastBatchPrice;
            int? total = stationBuys ? entry.StationBuyBatchPrice : entry.StationSellBatchPrice;
            int quantity = stationBuys ? entry.StationBuyBatchQuantity : entry.StationSellBatchQuantity;
            string priceLabel = stationBuys ? Ui("ui.trade_payout") : Ui("ui.price");
            string priceLine = priceLabel + " " + FormatTradePriceRange(first, last);
            string middleLine = stationBuys ? FormatTradeSellBatchCard(total, quantity) : FormatTradeBuyBatchCard(total, quantity, entry.Stock);
            string travelMissionLine = Ui("ui.travel") + ": " + SafeTradeText(entry.TravelTime) + "\n" + Ui("ui.mission") + ": " + SafeTradeText(GetTradeMissionDisplay(entry));

            BrowserLines.Add(BrowserLine.TradeStationCard103(
                entry.Label, priceLine, middleLine, travelMissionLine, entry.SpaceObjectId,
                entry.OwnerFactionId, entry.OwnerRelation, entry.MissionArrivalState));
        }

        private static void AddTradeStationTable103(LiveMarketEntry entry, bool stationBuys)
        {
            int? next = stationBuys ? entry.StationBuyPrice : entry.StationSellPrice;
            int? total = stationBuys ? entry.StationBuyBatchPrice : entry.StationSellBatchPrice;
            int quantity = stationBuys ? entry.StationBuyBatchQuantity : entry.StationSellBatchQuantity;
            string stock = !stationBuys && entry.Stock.HasValue ? entry.Stock.Value.ToString(CultureInfo.InvariantCulture) : string.Empty;
            BrowserLines.Add(BrowserLine.TradeStation(
                entry.Label, next.HasValue ? next.Value.ToString(CultureInfo.InvariantCulture) : "?", FormatTradeTableBatch(total, quantity), stock,
                GetTradeMissionDisplay(entry), entry.SpaceObjectId, entry.OwnerFactionId, entry.OwnerRelation, entry.MissionArrivalState, entry.TravelTime));
        }

        private static string FormatTradeTableBatch(int? total, int quantity)
        {
            return total.HasValue && quantity > 0
                ? total.Value.ToString(CultureInfo.InvariantCulture) + " (" + quantity.ToString(CultureInfo.InvariantCulture) + ")"
                : "—";
        }

        private static void AddLegacyTradeStationRow(LiveMarketEntry entry, bool stationBuys)
        {
            int? price = stationBuys ? entry.StationBuyPrice : entry.StationSellPrice;
            string stock = !stationBuys && entry.Stock.HasValue ? entry.Stock.Value.ToString(CultureInfo.InvariantCulture) : string.Empty;
            BrowserLines.Add(BrowserLine.TradeStation(
                entry.Label, price.HasValue ? price.Value.ToString(CultureInfo.InvariantCulture) : "?", stock,
                GetTradeMissionDisplay(entry), entry.TravelTime, entry.SpaceObjectId,
                entry.OwnerFactionId, entry.OwnerRelation, entry.MissionArrivalState));
        }

        private static string FormatTradePriceRange(int? first, int? last)
        {
            if (!first.HasValue) return "?";
            if (!last.HasValue || last.Value == first.Value) return first.Value.ToString(CultureInfo.InvariantCulture);
            return first.Value.ToString(CultureInfo.InvariantCulture) + " -> " + last.Value.ToString(CultureInfo.InvariantCulture);
        }

        private static string FormatTradeBuyBatchCard(int? total, int quantity, int? stock)
        {
            string stockLine = Ui("ui.trade_stock_short") + " " + (stock.HasValue ? stock.Value.ToString(CultureInfo.InvariantCulture) : "—");
            if (!total.HasValue || quantity <= 0) return stockLine + "\n—";
            string batchLabel = stock.HasValue && stock.Value < 5 && quantity == stock.Value
                ? Ui("ui.trade_all") + " " + quantity.ToString(CultureInfo.InvariantCulture)
                : quantity.ToString(CultureInfo.InvariantCulture) + " " + Ui("ui.trade_pcs");
            return stockLine + "\n" + batchLabel + " = " + total.Value.ToString(CultureInfo.InvariantCulture);
        }

        private static string FormatTradeSellBatchCard(int? total, int quantity)
        {
            if (!total.HasValue || quantity <= 0) return "—\n—";
            return quantity.ToString(CultureInfo.InvariantCulture) + " " + Ui("ui.trade_pcs") + "\n" + Ui("ui.trade_total") + " " + total.Value.ToString(CultureInfo.InvariantCulture);
        }

        private static string SafeTradeText(string value) { return string.IsNullOrEmpty(value) ? "—" : value; }
    }
}
