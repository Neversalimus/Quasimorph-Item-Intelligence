using System;
using System.Globalization;

namespace ItemIntelligence
{
    public static partial class ModMain
    {
        private static string _lastTradeLayoutDiagnosticSignature = string.Empty;

        private static void LogTradeLayoutDiagnostic(bool exact103Pricing, bool previousLayout)
        {
            string layout = previousLayout
                ? (exact103Pricing ? "PreviousTable" : "PreviousTableCompat")
                : (exact103Pricing ? "Card" : "CardCompat");
            string assemblySha = string.IsNullOrEmpty(_compatAssemblySha256) ? "<unknown>" : _compatAssemblySha256;
            string signature = layout + "|" + UsePreviousTradeLayout + "|" + exact103Pricing + "|" + assemblySha;
            if (string.Equals(_lastTradeLayoutDiagnosticSignature, signature, StringComparison.Ordinal)) return;
            _lastTradeLayoutDiagnosticSignature = signature;

            UnityEngine.Debug.Log("[ItemIntelligence][TradeLayout] layout=" + layout +
                ", PreviousTradeLayout=" + UsePreviousTradeLayout +
                ", Exact103Pricing=" + exact103Pricing +
                ", BuildStatus=" + (_compatBuildStatus ?? string.Empty) +
                ", AssemblySHA256=" + assemblySha + ".");
        }

        private static void AddTradeStationCardCompat(LiveMarketEntry entry, bool stationBuys)
        {
            int? price = stationBuys ? entry.StationBuyPrice : entry.StationSellPrice;
            string priceLabel = stationBuys ? Ui("ui.trade_payout") : Ui("ui.price");
            string priceLine = priceLabel + " " + FormatTradePriceRange(price, null);
            string middleLine = stationBuys
                ? string.Empty
                : Ui("ui.trade_stock_short") + " " + (entry.Stock.HasValue ? entry.Stock.Value.ToString(CultureInfo.InvariantCulture) : "—");
            string travelMissionLine = Ui("ui.travel") + ": " + SafeTradeText(entry.TravelTime) + "\n" + Ui("ui.mission") + ": " + SafeTradeText(GetTradeMissionDisplay(entry));

            BrowserLines.Add(BrowserLine.TradeStationCard103(
                entry.Label, priceLine, middleLine, travelMissionLine, entry.SpaceObjectId,
                entry.OwnerFactionId, entry.OwnerRelation, entry.MissionArrivalState));
        }
    }
}
