using System;
using MGSC;
using UnityEngine;

namespace ItemIntelligence
{
    public static partial class ModMain
    {
        private static BrowserLine _overviewShipCargoLine;
        private static string _overviewShipCargoItemId;
        private static int? _overviewShipCargoCount;
        private static float _overviewShipCargoNextCheck;

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
            _overviewShipCargoItemId = itemId;
            _overviewShipCargoCount = ReadOverviewShipCargoCount(itemId);
            _overviewShipCargoLine = CreateOverviewShipCargoLine(_overviewShipCargoCount);
            _overviewShipCargoNextCheck = Time.realtimeSinceStartup + 0.5f;
            BrowserLines.Add(_overviewShipCargoLine);
        }

        private static void TickOverviewShipCargo()
        {
            if (!_inspectorOpen || _browserCatalogOpen || _inspectorRoot == null ||
                BrowserNavigation.Tab != (int)BrowserTabId.Overview || _overviewShipCargoLine == null ||
                !string.Equals(_overviewShipCargoItemId, _inspectorItemId, StringComparison.Ordinal) ||
                Time.realtimeSinceStartup < _overviewShipCargoNextCheck) return;

            _overviewShipCargoNextCheck = Time.realtimeSinceStartup + 0.5f;
            int? count = ReadOverviewShipCargoCount(_inspectorItemId);
            if (count == _overviewShipCargoCount) return;

            int index = BrowserLines.IndexOf(_overviewShipCargoLine);
            if (index < 0) return;
            _overviewShipCargoCount = count;
            _overviewShipCargoLine = CreateOverviewShipCargoLine(count);
            BrowserLines[index] = _overviewShipCargoLine;
            // Update only the value row; keep scroll position, search and other data.
            RenderBrowserRowsOnly();
        }

        private static void ResetOverviewShipCargo()
        {
            _overviewShipCargoLine = null;
            _overviewShipCargoItemId = null;
            _overviewShipCargoCount = null;
            _overviewShipCargoNextCheck = 0f;
        }
    }
}
