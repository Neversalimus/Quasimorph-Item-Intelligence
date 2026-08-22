using System;
using System.Globalization;
using UnityEngine;

namespace ItemIntelligence
{
    public static partial class ModMain
    {
        private const float BrowserColdOpenBudgetMs = 50f;
        private const float BrowserWarmOpenBudgetMs = 20f;
        private const float BrowserRenderBudgetMs = 20f;
        private static int _browserPerfBudgetViolations;

        private static void ReportBrowserPerformanceBudget(
            string itemId, bool cold, float totalMs, float renderMs)
        {
            float totalBudget = cold ? BrowserColdOpenBudgetMs : BrowserWarmOpenBudgetMs;
            if (totalMs <= totalBudget && renderMs <= BrowserRenderBudgetMs) return;
            _browserPerfBudgetViolations++;
            if (_browserPerfBudgetViolations > 12)
            {
                if (_browserPerfBudgetViolations == 13)
                    Debug.LogWarning("[ItemIntelligence][PerfBudget] further budget warnings suppressed for this session.");
                return;
            }
            Debug.LogWarning(
                "[ItemIntelligence][PerfBudget] item=" + (itemId ?? string.Empty) +
                ", mode=" + (cold ? "cold" : "warm") +
                ", total=" + totalMs.ToString("0.0", CultureInfo.InvariantCulture) +
                "ms/" + totalBudget.ToString("0", CultureInfo.InvariantCulture) +
                "ms, render=" + renderMs.ToString("0.0", CultureInfo.InvariantCulture) +
                "ms/" + BrowserRenderBudgetMs.ToString("0", CultureInfo.InvariantCulture) +
                "ms, violations=" + _browserPerfBudgetViolations.ToString(CultureInfo.InvariantCulture) + ".");
        }
    }
}
