using System;
using MGSC;
using UnityEngine;

namespace ItemIntelligence
{
    public static partial class ModMain
    {
        private static BrowserLine _overviewShipCargoLine;
        private static readonly ShipCargoMonitor OverviewShipCargo =
            new ShipCargoMonitor(ReadOverviewShipCargoCount);

        private static int? ReadOverviewShipCargoCount(string itemId)
        {
            if (!_browserGameplayActive || _modContext == null) return null;
            try
            {
                // Resolve from the current context on each read. A cached cargo or
                // tooltip price can outlive a save switch or miss a mission resume.
                State state = _modContext.State;
                int count;
                return state != null && ShipCargoReader.TryGetCount(state.Get<MagnumCargo>(), itemId, out count)
                    ? (int?)count : null;
            }
            catch
            {
                return null;
            }
        }

        private static BrowserLine CreateOverviewShipCargoLine(int? count)
        {
            return BrowserLine.Normal(Ui("ui.ship_cargo"), count.HasValue
                ? count.Value.ToString("N0", GetUiNumberCulture()) : "?");
        }

        private static void AppendOverviewShipCargo(string itemId)
        {
            _overviewShipCargoLine = CreateOverviewShipCargoLine(
                OverviewShipCargo.Begin(itemId, Time.realtimeSinceStartup));
            BrowserLines.Add(_overviewShipCargoLine);
        }

        private static void TickOverviewShipCargo()
        {
            if (!_inspectorOpen || _browserCatalogOpen || _inspectorRoot == null ||
                BrowserNavigation.Tab != (int)BrowserTabId.Overview || _overviewShipCargoLine == null ||
                !OverviewShipCargo.IsDue(_inspectorItemId, Time.realtimeSinceStartup)) return;

            int index = BrowserLines.IndexOf(_overviewShipCargoLine);
            if (index < 0) return;
            int? count;
            if (!OverviewShipCargo.TryRefresh(_inspectorItemId, Time.realtimeSinceStartup, out count)) return;
            _overviewShipCargoLine = CreateOverviewShipCargoLine(count);
            BrowserLines[index] = _overviewShipCargoLine;
            // Update only the value row; keep scroll position, search and other data.
            RenderBrowserRowsOnly();
        }

        private static void ResetOverviewShipCargo()
        {
            _overviewShipCargoLine = null;
            OverviewShipCargo.Reset();
        }
    }
}
