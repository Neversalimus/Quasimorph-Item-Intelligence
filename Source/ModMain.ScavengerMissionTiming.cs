using System;
using System.Collections.Generic;
using MGSC;

namespace ItemIntelligence
{
    public static partial class ModMain
    {
        private sealed class ScavengerTravelSnapshot
        {
            public string Text;
            public double? Hours;
        }

        private static readonly Dictionary<string, ScavengerTravelSnapshot> ScavengerTravelBySpaceObject =
            new Dictionary<string, ScavengerTravelSnapshot>(StringComparer.OrdinalIgnoreCase);
        private static DateTime? _scavengerMissionTimingNow;
        private static bool _scavengerTravelTimeAvailable;

        private static void ResetScavengerMissionTimingSnapshot()
        {
            ScavengerTravelBySpaceObject.Clear();
            _scavengerMissionTimingNow = GetTradeDateTimeMember(ResolveTradeSpaceTimeState(), "Time");

            // Exact point-to-point travel is only meaningful while vanilla TravelMetadata
            // is Idle. During an active flight CurrentSpaceObject remains the old origin
            // (or empty) until arrival, so displaying a new point-to-point time would lie.
            _scavengerTravelTimeAvailable = false;
            object travelData = _tradeTravelMetadata;
            if (travelData == null) return;
            string state = GetStringMember(travelData, "State");
            _scavengerTravelTimeAvailable = string.Equals(state, "Idle", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsScavengerMissionExpiredAtSnapshot(Mission mission)
        {
            return mission == null || !_scavengerMissionTimingNow.HasValue ||
                mission.ExpireTime <= _scavengerMissionTimingNow.Value;
        }

        // Read-only timing projection. Every row in one render uses the same SpaceTime
        // snapshot, while travel is cached per destination body to avoid repeated vanilla calls.
        private static void PopulateScavengerMissionTiming(
            ScavengerMissionChanceRow row, Mission mission, Station station)
        {
            if (row == null || mission == null || station == null) return;

            row.TravelText = "—";
            row.RemainingText = "—";
            row.TravelHours = null;
            row.RemainingHours = null;
            row.ArrivalState = 0;
            row.SpaceObjectId = GetStringMember(station, "SpaceObjectId");

            if (_scavengerTravelTimeAvailable && !string.IsNullOrEmpty(row.SpaceObjectId))
            {
                ScavengerTravelSnapshot travel;
                if (ScavengerTravelBySpaceObject.TryGetValue(row.SpaceObjectId, out travel))
                {
                    row.TravelText = travel.Text;
                    row.TravelHours = travel.Hours;
                }
                else
                {
                    double? travelHours;
                    row.TravelText = GetTradeTravelTimeSafe(row.SpaceObjectId, out travelHours);
                    row.TravelHours = travelHours;
                    ScavengerTravelBySpaceObject[row.SpaceObjectId] = new ScavengerTravelSnapshot
                    { Text = row.TravelText, Hours = row.TravelHours };
                }
            }

            if (_scavengerMissionTimingNow.HasValue)
            {
                double remainingHours = (mission.ExpireTime - _scavengerMissionTimingNow.Value).TotalHours;
                if (remainingHours > 0d)
                {
                    row.RemainingHours = remainingHours;
                    row.RemainingText = FormatScavengerMissionRemaining(remainingHours);
                }
            }

            if (row.TravelHours.HasValue && row.RemainingHours.HasValue)
                row.ArrivalState = row.TravelHours.Value <= row.RemainingHours.Value ? 1 : 2;
        }

        private static string FormatScavengerMissionRemaining(double hours)
        {
            hours = Math.Max(0d, hours);
            string vanilla = FormatTradeMissionRemainingVanilla(hours);
            if (!string.IsNullOrWhiteSpace(vanilla)) return vanilla;

            int totalHours = (int)Math.Floor(hours);
            if (totalHours >= 24)
            {
                int days = totalHours / 24;
                int remainderHours = totalHours % 24;
                return IsRussian()
                    ? days.ToString() + "д " + remainderHours.ToString() + "ч"
                    : days.ToString() + "d " + remainderHours.ToString() + "h";
            }
            if (totalHours >= 1) return IsRussian() ? totalHours.ToString() + "ч" : totalHours.ToString() + "h";

            int minutes = Math.Max(1, (int)Math.Floor(hours * 60d));
            return IsRussian() ? minutes.ToString() + "м" : minutes.ToString() + "m";
        }
    }
}
