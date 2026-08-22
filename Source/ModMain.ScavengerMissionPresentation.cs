using System;
using System.Collections.Generic;
using System.Globalization;

namespace ItemIntelligence
{
    public static partial class ModMain
    {
        private sealed class ScavengerMissionChanceRow
        {
            public string Station;
            public string SpaceObjectId;
            public string Opponent;
            public float ChancePercent;
            public int Rolls;
            public int TechLevel;
            public string TravelText;
            public string RemainingText;
            public double? TravelHours;
            public double? RemainingHours;
            // 0 unknown, 1 reachable by/before expiry, 2 mission expires before arrival.
            public int ArrivalState;
        }

        private static void AddBrowserScavengerMissionRows(List<ScavengerMissionChanceRow> rows)
        {
            if (rows == null || rows.Count == 0) return;
            rows.Sort(delegate(ScavengerMissionChanceRow a, ScavengerMissionChanceRow b)
            {
                int chance = b.ChancePercent.CompareTo(a.ChancePercent);
                if (chance != 0) return chance;
                if (a.TravelHours.HasValue && b.TravelHours.HasValue)
                {
                    int travel = a.TravelHours.Value.CompareTo(b.TravelHours.Value);
                    if (travel != 0) return travel;
                }
                else if (a.TravelHours.HasValue) return -1;
                else if (b.TravelHours.HasValue) return 1;
                int station = string.Compare(a.Station, b.Station, StringComparison.CurrentCultureIgnoreCase);
                if (station != 0) return station;
                return string.Compare(a.Opponent, b.Opponent, StringComparison.CurrentCultureIgnoreCase);
            });

            int minRolls = int.MaxValue;
            int maxRolls = 0;
            for (int i = 0; i < rows.Count; i++)
            {
                minRolls = Math.Min(minRolls, rows[i].Rolls);
                maxRolls = Math.Max(maxRolls, rows[i].Rolls);
            }
            string rolls = minRolls == maxRolls
                ? maxRolls.ToString(CultureInfo.InvariantCulture)
                : minRolls.ToString(CultureInfo.InvariantCulture) + "-" + maxRolls.ToString(CultureInfo.InvariantCulture);

            BrowserLines.Add(BrowserLine.FullSection(Ui("ui.scavenger_mission_rewards")));
            BrowserLines.Add(BrowserLine.FullNote(
                Ui("ui.scavenger_current_missions") + rows.Count.ToString(CultureInfo.InvariantCulture) +
                "   •   " + Ui("ui.scavenger_best_chance") + FormatScavengerPercent(rows[0].ChancePercent) +
                "   •   " + Ui("ui.rolls") + ": " + rolls));
            BrowserLines.Add(BrowserLine.ScavengerMissionHeader(
                Ui("ui.station"), Ui("ui.opponent"), Ui("ui.chance"), Ui("ui.travel"), Ui("ui.scavenger_time_left")));

            for (int i = 0; i < rows.Count; i++)
            {
                ScavengerMissionChanceRow row = rows[i];
                BrowserLines.Add(BrowserLine.ScavengerMissionRow(
                    row.Station,
                    row.Opponent,
                    FormatScavengerPercent(row.ChancePercent),
                    FirstNonEmpty(row.TravelText, "—"),
                    FirstNonEmpty(row.RemainingText, "—"),
                    row.SpaceObjectId,
                    row.ArrivalState));
            }
        }

        private static string FormatScavengerPercent(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value)) return "—";
            value = Math.Max(0f, Math.Min(100f, value));
            return value >= 1f
                ? value.ToString("0.##", CultureInfo.InvariantCulture) + "%"
                : value.ToString("0.###", CultureInfo.InvariantCulture) + "%";
        }
    }
}
