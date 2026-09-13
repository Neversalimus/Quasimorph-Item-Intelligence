using System;
using System.Globalization;
using UnityEngine;

namespace ItemIntelligence
{
    public static partial class ModMain
    {
        // Bounded test diagnostics: one summary per five seconds while Trade is
        // visible, plus completion. Timings overlap (station includes prices/travel).
        // Frame time includes Unity and other mods; it is not QII's CPU time.
        private enum TradePerfStage { Refresh, Station, Prices, Travel, Render }
        private struct TradePerfTiming
        {
            public int Count;
            public double TotalMs;
            public double MaxMs;
            public void Add(double ms)
            {
                Count++;
                TotalMs += ms;
                if (ms > MaxMs) MaxMs = ms;
            }
            public string Summary()
            {
                return Count.ToString(CultureInfo.InvariantCulture) + ":" +
                    (Count == 0 ? 0d : TotalMs / Count).ToString("F2", CultureInfo.InvariantCulture) + "/" +
                    MaxMs.ToString("F2", CultureInfo.InvariantCulture);
            }
        }
        private static readonly TradePerfTiming[] TradePerfStages = new TradePerfTiming[5];
        private static TradePerfTiming _tradePerfTick;
        private static TradePerfTiming _tradePerfFrame;
        private static long _tradePerfWindowStart;

        private static long TradePerfTimestamp()
        {
            return System.Diagnostics.Stopwatch.GetTimestamp();
        }
        private static double TradePerfElapsedMs(long start)
        {
            return (TradePerfTimestamp() - start) * 1000d / System.Diagnostics.Stopwatch.Frequency;
        }
        private static void ResetTradePerformanceWindow()
        {
            Array.Clear(TradePerfStages, 0, TradePerfStages.Length);
            _tradePerfTick = new TradePerfTiming();
            _tradePerfFrame = new TradePerfTiming();
            _tradePerfWindowStart = TradePerfTimestamp();
        }
        private static void RecordTradePerformance(TradePerfStage stage, long start)
        {
            TradePerfStages[(int)stage].Add(TradePerfElapsedMs(start));
        }
        private static void FinishTradePerformanceTick(long start, bool completed)
        {
            _tradePerfTick.Add(TradePerfElapsedMs(start));
            _tradePerfFrame.Add(Time.unscaledDeltaTime * 1000d);
            if (!completed && TradePerfElapsedMs(_tradePerfWindowStart) < 5000d) return;

            Debug.Log("[ItemIntelligence][TradePerf] item=" + _marketItemId +
                ", phase=" + (_marketScanActive ? "scan" : "idle") +
                ", completed=" + completed +
                ", stations=" + _marketStationIndex + "/" + MarketStations.Count +
                ", entries=" + MarketEntries.Count +
                ", count:avgMs/maxMs frame=" + _tradePerfFrame.Summary() +
                ", tick=" + _tradePerfTick.Summary() +
                ", refresh=" + TradePerfStages[(int)TradePerfStage.Refresh].Summary() +
                ", station=" + TradePerfStages[(int)TradePerfStage.Station].Summary() +
                ", prices=" + TradePerfStages[(int)TradePerfStage.Prices].Summary() +
                ", travel=" + TradePerfStages[(int)TradePerfStage.Travel].Summary() +
                ", render=" + TradePerfStages[(int)TradePerfStage.Render].Summary() +
                ", layout=" + (UsePreviousTradeLayout ? "table" : "cards") +
                ", enhanced=" + EnhancedReadability + ".");
            ResetTradePerformanceWindow();
        }
    }
}
