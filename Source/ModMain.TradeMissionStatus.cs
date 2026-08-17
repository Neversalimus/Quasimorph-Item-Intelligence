using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace ItemIntelligence
{
    public static partial class ModMain
    {
        // v1.7.39-test11: exact vanilla Trade mission gate.
        // Vanilla SpaceStationsWindow selects Missions.Get(station.Id, false) before the trade path;
        // Missions.Get(..., false) searches Missions.Values by Mission.StationId.
        // MissionSystem.Update removes/resolves entries when ExpireTime <= SpaceTime.Time.
        // This module mirrors that read-only contract and never mutates mission state.
        private sealed class TradeMissionSnapshot
        {
            public readonly DateTime? ExpireTime;

            public TradeMissionSnapshot(DateTime? expireTime)
            {
                ExpireTime = expireTime;
            }
        }

        private static object _tradeMissionsState;
        private static Type _tradeMissionsType;
        private static bool _tradeMissionsTypeChecked;
        private static object _tradeSpaceTimeState;
        private static Type _tradeSpaceTimeType;
        private static bool _tradeSpaceTimeTypeChecked;
        private static int _tradeMissionResolveNextFrame;
        private static int _tradeMissionSnapshotFrame = -1000;
        private static bool _tradeMissionSchemaLogged;
        private static DateTime? _tradeMissionSnapshotTime;
        private static System.Reflection.MethodInfo _tradeMissionFormatMethod;
        private static bool _tradeMissionFormatChecked;
        private static readonly Dictionary<string, TradeMissionSnapshot> TradeMissionsByStationId =
            new Dictionary<string, TradeMissionSnapshot>(StringComparer.OrdinalIgnoreCase);

        private static void ResetTradeMissionSession()
        {
            _tradeMissionsState = null;
            _tradeMissionsType = null;
            _tradeMissionsTypeChecked = false;
            _tradeSpaceTimeState = null;
            _tradeSpaceTimeType = null;
            _tradeSpaceTimeTypeChecked = false;
            _tradeMissionResolveNextFrame = 0;
            _tradeMissionSnapshotFrame = -1000;
            _tradeMissionSchemaLogged = false;
            _tradeMissionSnapshotTime = null;
            _tradeMissionFormatMethod = null;
            _tradeMissionFormatChecked = false;
            TradeMissionsByStationId.Clear();
            ResetTradeMissionCountdownUiRefresh();
        }

        private static void RefreshTradeMissionStatusSnapshot()
        {
            int frame = Time.frameCount;
            if (frame - _tradeMissionSnapshotFrame < 30) return;
            _tradeMissionSnapshotFrame = frame;
            _tradeMissionSnapshotTime = null;
            TradeMissionsByStationId.Clear();

            object missions = ResolveTradeMissionsState();
            if (missions == null) return;

            int records = 0;
            int linked = 0;
            try
            {
                // Exact current-build contract: Missions.Get(stationId, false) scans Values.
                object values = GetMember(missions, "Values");
                if (values == null) return;

                List<DataEntry> entries = EnumerateData(values);
                records = entries.Count;

                object spaceTime = ResolveTradeSpaceTimeState();
                _tradeMissionSnapshotTime = GetTradeDateTimeMember(spaceTime, "Time");

                for (int i = 0; i < entries.Count; i++)
                {
                    object mission = entries[i] == null ? null : entries[i].Value;
                    if (mission == null) continue;

                    string stationId = GetStringMember(mission, "StationId");
                    if (string.IsNullOrEmpty(stationId)) continue;

                    // Missions.Get returns the first matching Values entry, so preserve first-win order.
                    if (TradeMissionsByStationId.ContainsKey(stationId)) continue;

                    DateTime? expireTime = GetTradeDateTimeMember(mission, "ExpireTime");
                    TradeMissionsByStationId.Add(stationId, new TradeMissionSnapshot(expireTime));
                    linked++;
                }
            }
            catch
            {
                TradeMissionsByStationId.Clear();
                _tradeMissionSnapshotTime = null;
            }

            if (!_tradeMissionSchemaLogged)
            {
                _tradeMissionSchemaLogged = true;
                Debug.Log("[ItemIntelligence][TradeMission] exactGate=Missions.Values/StationId, Missions=" + records +
                    ", stationLinks=" + linked +
                    ", SpaceTime=" + (_tradeMissionSnapshotTime.HasValue ? "OK" : "unavailable") + ".");
            }
        }

        private static object ResolveTradeMissionsState()
        {
            if (_tradeMissionsState != null) return _tradeMissionsState;
            if (Time.frameCount < _tradeMissionResolveNextFrame) return null;
            try
            {
                if (!_tradeMissionsTypeChecked)
                {
                    _tradeMissionsTypeChecked = true;
                    _tradeMissionsType = AccessTools.TypeByName("MGSC.Missions");
                }
                if (_tradeMissionsType != null) _tradeMissionsState = ResolveStateModule(_tradeMissionsType);
            }
            catch { _tradeMissionsState = null; }
            if (_tradeMissionsState == null) _tradeMissionResolveNextFrame = Time.frameCount + 60;
            return _tradeMissionsState;
        }

        private static object ResolveTradeSpaceTimeState()
        {
            if (_tradeSpaceTimeState != null) return _tradeSpaceTimeState;
            try
            {
                if (!_tradeSpaceTimeTypeChecked)
                {
                    _tradeSpaceTimeTypeChecked = true;
                    _tradeSpaceTimeType = AccessTools.TypeByName("MGSC.SpaceTime");
                }
                if (_tradeSpaceTimeType != null) _tradeSpaceTimeState = ResolveStateModule(_tradeSpaceTimeType);
            }
            catch { _tradeSpaceTimeState = null; }
            return _tradeSpaceTimeState;
        }

        private static DateTime? GetTradeDateTimeMember(object target, string memberName)
        {
            if (target == null || string.IsNullOrEmpty(memberName)) return null;
            try
            {
                object raw = GetMember(target, memberName);
                if (raw is DateTime) return (DateTime)raw;
            }
            catch { }
            return null;
        }

        private static void ApplyTradeMissionState(LiveMarketEntry entry)
        {
            if (entry == null) return;

            entry.HasMission = false;
            entry.MissionRemainingHours = null;
            entry.MissionArrivalState = 0;

            TradeMissionSnapshot mission;
            if (string.IsNullOrEmpty(entry.StationId) ||
                !TradeMissionsByStationId.TryGetValue(entry.StationId, out mission) || mission == null)
                return;

            entry.HasMission = true;

            if (_tradeMissionSnapshotTime.HasValue && mission.ExpireTime.HasValue)
            {
                double remaining = (mission.ExpireTime.Value - _tradeMissionSnapshotTime.Value).TotalHours;
                entry.MissionRemainingHours = Math.Max(0d, remaining);
            }

            RefreshTradeMissionArrivalState(entry);
        }

        private static void RefreshTradeMissionArrivalState(LiveMarketEntry entry)
        {
            if (entry == null || !entry.HasMission)
            {
                if (entry != null) entry.MissionArrivalState = 0;
                return;
            }

            // 1 = mission exists but arrival comparison is unavailable.
            // 2 = mission expires by arrival; 3 = mission still blocks the station on arrival.
            entry.MissionArrivalState = 1;
            if (!entry.MissionRemainingHours.HasValue || !entry.TravelHours.HasValue) return;
            entry.MissionArrivalState = entry.MissionRemainingHours.Value > entry.TravelHours.Value ? 3 : 2;
        }

        private static string GetTradeMissionDisplay(LiveMarketEntry entry)
        {
            if (entry == null || !entry.HasMission) return "—";
            if (!entry.MissionRemainingHours.HasValue) return Ui("ui.yes");

            double hours = Math.Max(0d, entry.MissionRemainingHours.Value);
            // Story missions use vanilla's +100-year expiry. Keep the compact table truthful
            // without letting a five-digit day count overflow the fixed 80 px mission column.
            if (hours >= 24000d) return IsRussian() ? ">999д" : ">999d";

            string vanilla = FormatTradeMissionRemainingVanilla(hours);
            if (!string.IsNullOrWhiteSpace(vanilla)) return vanilla;

            // Fail-soft formatting only if the exact vanilla formatter is unavailable.
            int totalHours = (int)Math.Floor(hours);
            if (totalHours >= 24)
            {
                int days = totalHours / 24;
                int remainderHours = totalHours % 24;
                return IsRussian()
                    ? days.ToString() + "д " + remainderHours.ToString() + "ч"
                    : days.ToString() + "d " + remainderHours.ToString() + "h";
            }

            if (totalHours >= 1)
                return IsRussian() ? totalHours.ToString() + "ч" : totalHours.ToString() + "h";

            int minutes = (int)Math.Floor(hours * 60d);
            if (minutes < 1) minutes = 1;
            return IsRussian() ? minutes.ToString() + "м" : minutes.ToString() + "m";
        }

        private static string FormatTradeMissionRemainingVanilla(double hours)
        {
            try
            {
                if (!_tradeMissionFormatChecked)
                {
                    _tradeMissionFormatChecked = true;
                    Type formatHelper = AccessTools.TypeByName("MGSC.FormatHelper");
                    if (formatHelper != null)
                    {
                        _tradeMissionFormatMethod = formatHelper.GetMethod(
                            "ToLocalizedDaysAndHours",
                            StaticFlags,
                            null,
                            new Type[] { typeof(TimeSpan) },
                            null);
                    }
                }

                if (_tradeMissionFormatMethod == null) return string.Empty;
                object formatted = _tradeMissionFormatMethod.Invoke(null, new object[] { TimeSpan.FromHours(hours) });
                return formatted as string ?? string.Empty;
            }
            catch { return string.Empty; }
        }
    }
}
