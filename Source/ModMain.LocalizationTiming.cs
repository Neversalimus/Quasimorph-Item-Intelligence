using System;
using System.Globalization;

namespace ItemIntelligence
{
    public static partial class ModMain
    {
        // Match vanilla's compact day/hour rounding, substituting only QII units.
        // Travel calculations and mission-arrival comparisons keep their exact hours.
        private static string FormatUiDaysAndHours(double hours)
        {
            if (double.IsNaN(hours) || double.IsInfinity(hours) || hours < 0d) return "—";
            if (hours >= 24000d) return ">999" + Ui("ui.unit_day_short");
            int days = (int)Math.Floor((float)(hours / 24d));
            int remainder = (int)Math.Round((float)(hours % 24d), MidpointRounding.ToEven);
            if (remainder >= 24) { days++; remainder = 0; }
            if (days > 0)
                return days.ToString(CultureInfo.InvariantCulture) + Ui("ui.unit_day_short") + " " +
                    remainder.ToString(CultureInfo.InvariantCulture) + Ui("ui.unit_hour_short");
            return (remainder < 1 ? "<1" : remainder.ToString(CultureInfo.InvariantCulture)) + Ui("ui.unit_hour_short");
        }

        private static string GetTradeTravelDisplay(LiveMarketEntry entry)
        {
            if (entry == null) return "—";
            // Reproject cached hours at render time: manual language switching must
            // not leave travel cells in the language used to build the market cache.
            if (entry.TravelHours.HasValue)
            {
                if (string.Equals(entry.SpaceObjectId, _tradeTravelOriginSpaceObjectId,
                    StringComparison.OrdinalIgnoreCase)) return Ui("ui.here");
                if (UiLanguagePreference != "Auto (Game)")
                    return FormatUiDaysAndHours(entry.TravelHours.Value);
            }
            return entry.TravelTime;
        }
    }
}
