using System;
using UnityEngine;

namespace ItemIntelligence
{
    public static partial class ModMain
    {
        // v1.7.39-test12: Trade freshness policy.
        // PRICE/STOCK/owner relations remain an intentional time-sliced snapshot and are
        // refreshed only on an explicit Trade-tab entry/re-click. Mission countdowns are
        // much cheaper: while Trade is visible, re-check Missions.Values + SpaceTime at a
        // low cadence and redraw only when a displayed mission changed or game time moved.
        private const int TradeMissionUiCheckFrames = 300;
        private const double TradeMissionUiRefreshMinutes = 5d;
        private static int _tradeMissionUiCheckNextFrame;
        private static DateTime? _tradeMissionUiRenderedAt;

        private static void ResetTradeMissionCountdownUiRefresh()
        {
            _tradeMissionUiCheckNextFrame = 0;
            _tradeMissionUiRenderedAt = null;
        }

        private static void MarkTradeMissionCountdownUiRendered()
        {
            if (_tradeMissionSnapshotTime.HasValue)
                _tradeMissionUiRenderedAt = _tradeMissionSnapshotTime.Value;
        }

        private static void TickTradeMissionCountdownUiRefresh()
        {
            if (_marketScanActive || !_marketScanComplete || MarketEntries.Count == 0) return;
            int frame = Time.frameCount;
            if (frame < _tradeMissionUiCheckNextFrame) return;
            _tradeMissionUiCheckNextFrame = frame + TradeMissionUiCheckFrames;

            // Rebuild only the tiny mission snapshot, never the 175-station market here.
            _tradeMissionSnapshotFrame = -1000;
            RefreshTradeMissionStatusSnapshot();

            bool missionChanged = false;
            bool displayedMission = false;
            for (int i = 0; i < MarketEntries.Count; i++)
            {
                LiveMarketEntry entry = MarketEntries[i];
                if (entry == null || string.IsNullOrEmpty(entry.StationId)) continue;
                bool hasMissionNow = TradeMissionsByStationId.ContainsKey(entry.StationId);
                if (hasMissionNow) displayedMission = true;
                if (hasMissionNow != entry.HasMission) missionChanged = true;
            }

            bool timeChanged = false;
            if (displayedMission && _tradeMissionSnapshotTime.HasValue && _tradeMissionUiRenderedAt.HasValue)
            {
                double elapsedMinutes = (_tradeMissionSnapshotTime.Value - _tradeMissionUiRenderedAt.Value).TotalMinutes;
                timeChanged = elapsedMinutes < 0d || elapsedMinutes >= TradeMissionUiRefreshMinutes;
            }
            else if (displayedMission && _tradeMissionSnapshotTime.HasValue)
            {
                timeChanged = true;
            }

            if (missionChanged || timeChanged)
                RenderBrowser(_inspectorItemId);
        }
    }
}
