using System;
using System.Collections.Generic;

namespace ItemIntelligence
{
    /// <summary>
    /// Space-session cache for the static Station -> SpaceObject part of Baron habitat resolution.
    /// Mission records remain live-scanned because their set can change during play. This removes
    /// repeated reflection over every station without making mission habitat stale.
    /// </summary>
    public static partial class ModMain
    {
        private static readonly Dictionary<string, HashSet<string>> BaronStationBodiesByBramfatura =
            new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, int> BaronStationMatchesByBramfatura =
            new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, string> BaronStationBodyById =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private static bool _baronHabitatStationIndexBuilt;

        private static void ResetBaronHabitatRuntimeStationIndex()
        {
            BaronStationBodiesByBramfatura.Clear();
            BaronStationMatchesByBramfatura.Clear();
            BaronStationBodyById.Clear();
            _baronHabitatStationIndexBuilt = false;
        }

        private static void EnsureBaronHabitatRuntimeStationIndex()
        {
            if (_baronHabitatStationIndexBuilt) return;
            _baronHabitatStationIndexBuilt = true;
            List<object> stations = GetRuntimeStationsLightweight();
            if (stations == null) return;

            for (int i = 0; i < stations.Count; i++)
            {
                object station = stations[i];
                if (station == null) continue;
                string stationId = GetStringMember(station, "Id");
                string bramfaturaId = GetStringMember(station, "BramfaturaId");
                string bodyId = GetStringMember(station, "SpaceObjectId");

                if (!string.IsNullOrEmpty(stationId) && !string.IsNullOrEmpty(bodyId) &&
                    SpaceObjectRecordsById.ContainsKey(bodyId) && !BaronStationBodyById.ContainsKey(stationId))
                    BaronStationBodyById.Add(stationId, bodyId);

                if (string.IsNullOrEmpty(bramfaturaId)) continue;
                int matches;
                BaronStationMatchesByBramfatura.TryGetValue(bramfaturaId, out matches);
                BaronStationMatchesByBramfatura[bramfaturaId] = matches + 1;
                if (string.IsNullOrEmpty(bodyId) || !SpaceObjectRecordsById.ContainsKey(bodyId)) continue;

                HashSet<string> bodies;
                if (!BaronStationBodiesByBramfatura.TryGetValue(bramfaturaId, out bodies))
                {
                    bodies = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    BaronStationBodiesByBramfatura.Add(bramfaturaId, bodies);
                }
                bodies.Add(bodyId);
            }
        }
    }
}
