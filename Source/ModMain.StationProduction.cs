using System;
using System.Collections.Generic;
using System.Globalization;

namespace ItemIntelligence
{
    public static partial class ModMain
    {
        // Vanilla BarterReceipt is a station-production recipe despite its historical
        // class name. These indexes intentionally describe global recipe capability;
        // a specific station runs only receipt ids present in Station.CurrentReceipts.
        private static readonly Dictionary<string, List<StationProductionRelation>> StationProductionByInputItem =
            new Dictionary<string, List<StationProductionRelation>>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, List<StationProductionRelation>> StationProductionByOutputItem =
            new Dictionary<string, List<StationProductionRelation>>(StringComparer.OrdinalIgnoreCase);

        private sealed class StationProductionRelation
        {
            public readonly string ReceiptId;
            public readonly int SelectedQuantity;
            public readonly Dictionary<string, int> RelatedItems;

            public StationProductionRelation(string receiptId, int selectedQuantity, Dictionary<string, int> relatedItems)
            {
                ReceiptId = receiptId;
                SelectedQuantity = selectedQuantity;
                RelatedItems = relatedItems == null
                    ? new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
                    : new Dictionary<string, int>(relatedItems, StringComparer.OrdinalIgnoreCase);
            }
        }

        private static void ResetStationProductionIndexState()
        {
            StationProductionByInputItem.Clear();
            StationProductionByOutputItem.Clear();
        }

        private static void AddBrowserStationProductionRelations(
            List<StationProductionRelation> relations,
            bool selectedItemIsOutput)
        {
            if (relations == null || relations.Count == 0) return;

            List<StationProductionRelation> unique = new List<StationProductionRelation>();
            HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < relations.Count; i++)
            {
                StationProductionRelation relation = relations[i];
                if (relation == null || string.IsNullOrEmpty(relation.ReceiptId) || !seen.Add(relation.ReceiptId)) continue;
                unique.Add(relation);
            }
            unique.Sort(delegate(StationProductionRelation a, StationProductionRelation b)
            {
                return string.Compare(a.ReceiptId, b.ReceiptId, StringComparison.OrdinalIgnoreCase);
            });

            string selectedVerb = selectedItemIsOutput
                ? Ui("ui.station_production_output")
                : Ui("ui.station_production_input");
            string relatedVerb = selectedItemIsOutput
                ? Ui("ui.station_production_input")
                : Ui("ui.station_production_output");

            for (int i = 0; i < unique.Count; i++)
            {
                StationProductionRelation relation = unique[i];
                int selectedQuantity = Math.Max(1, relation.SelectedQuantity);
                if (unique.Count > 1)
                {
                    BrowserLines.Add(BrowserLine.Normal(
                        Ui("ui.station_production_recipe") + " " + (i + 1).ToString(CultureInfo.InvariantCulture),
                        selectedVerb + "  x" + selectedQuantity.ToString(CultureInfo.InvariantCulture)));
                }
                else if (!selectedItemIsOutput || selectedQuantity > 1)
                {
                    BrowserLines.Add(BrowserLine.Normal(
                        Ui("ui.this_item"),
                        selectedVerb + "  x" + selectedQuantity.ToString(CultureInfo.InvariantCulture)));
                }

                List<KeyValuePair<string, int>> related =
                    new List<KeyValuePair<string, int>>(relation.RelatedItems);
                related.Sort(delegate(KeyValuePair<string, int> a, KeyValuePair<string, int> b)
                {
                    int localized = string.Compare(
                        LocalizeItem(a.Key), LocalizeItem(b.Key), StringComparison.OrdinalIgnoreCase);
                    if (localized != 0) return localized;
                    return string.Compare(a.Key, b.Key, StringComparison.OrdinalIgnoreCase);
                });

                for (int r = 0; r < related.Count; r++)
                {
                    KeyValuePair<string, int> item = related[r];
                    if (string.IsNullOrEmpty(item.Key)) continue;
                    BrowserLines.Add(BrowserLine.Item(
                        item.Key,
                        relatedVerb + "  x" + Math.Max(1, item.Value).ToString(CultureInfo.InvariantCulture)));
                }
            }
        }
    }
}
