using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using MGSC;
using UnityEngine;

namespace ItemIntelligence
{
    public static partial class ModMain
    {
        private static readonly string[] BaronHabitatMemberNames = new string[]
        {
            "SpaceObjectId", "SpaceObjectIds", "SpaceObject", "SpaceObjects",
            "PlanetId", "PlanetIds", "SatelliteId", "SatelliteIds",
            "MoonId", "MoonIds", "HabitatId", "HabitatIds",
            "HabitatSpaceObjectId", "HabitatSpaceObjectIds",
            "LocationSpaceObjectId", "LocationSpaceObjectIds"
        };
        private static readonly HashSet<string> BaronHabitatAuditLogged =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        private sealed class BaronHabitatNode
        {
            public readonly string Id;
            public readonly string Label;
            public bool IsHabitat;
            public readonly List<BaronHabitatNode> Children = new List<BaronHabitatNode>();

            public BaronHabitatNode(string id, string label)
            {
                Id = id ?? string.Empty;
                Label = label ?? string.Empty;
            }
        }

        // Presentation grouping is by the actual Baron creature, not by Qmorphos phase.
        // A single Baron can have multiple phase records (Duggur/Duggur_Reload). Exact
        // station/mission habitat evidence wins for the entire group; compatibility
        // fallback is used only when no grouped source has exact raid evidence.
        private static List<BaronHabitatNode> ResolveBaronHabitatTree(
            IList<LootBaronSpecialSource> sources)
        {
            List<BaronHabitatNode> roots = new List<BaronHabitatNode>();
            if (sources == null || sources.Count == 0) return roots;
            if (SpaceObjectRecordsById.Count == 0) BuildSpaceObjectIndex();
            if (SpaceObjectRecordsById.Count == 0) return roots;

            HashSet<string> ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            List<string> evidence = new List<string>();
            int stationMatches = 0;
            int missionMatches = 0;
            string baronId = string.Empty;

            for (int i = 0; i < sources.Count; i++)
            {
                LootBaronSpecialSource source = sources[i];
                if (source == null) continue;
                if (string.IsNullOrEmpty(baronId)) baronId = source.BaronCreatureId;
                stationMatches += CollectBaronHabitatFromRuntimeStations(source.BramfaturaId, ids, evidence);
                missionMatches += CollectBaronHabitatFromRuntimeMissions(source.BramfaturaId, ids, evidence);
            }

            if (stationMatches == 0 && missionMatches == 0)
            {
                for (int i = 0; i < sources.Count; i++)
                {
                    LootBaronSpecialSource source = sources[i];
                    if (source == null) continue;
                    CollectBaronHabitatFromQmorphos(source, ids, evidence);
                    CollectBaronHabitatFromBramfatura(source.BramfaturaId, ids, evidence);
                    CollectBaronHabitatReverseLinks(source, ids, evidence);
                    CollectBaronHabitatFromCreatureId(source.BaronCreatureId, ids, evidence);
                }
            }

            Dictionary<string, BaronHabitatNode> nodesById =
                new Dictionary<string, BaronHabitatNode>(StringComparer.OrdinalIgnoreCase);
            HashSet<string> ancestry = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string id in ids)
            {
                BaronHabitatNode node = EnsureBaronHabitatNode(roots, nodesById, ancestry, id);
                if (node != null) node.IsHabitat = true;
            }
            SortBaronHabitatNodes(roots);

            string auditKey = "group|" + baronId + "|" + sources.Count.ToString();
            if (BaronHabitatAuditLogged.Add(auditKey))
            {
                Debug.Log("[ItemIntelligence][BaronHabitatGroup] baron=" + baronId +
                    ", rawSources=" + sources.Count.ToString() +
                    ", locations=" + (ids.Count == 0 ? "0" : string.Join(",", new List<string>(ids).ToArray())) +
                    ", roots=" + roots.Count.ToString() +
                    ", uniqueNodes=" + nodesById.Count.ToString() +
                    ", stationMatches=" + stationMatches.ToString() +
                    ", missionMatches=" + missionMatches.ToString() +
                    ", exactRaidSource=" + ((stationMatches > 0 || missionMatches > 0) ? "true" : "false") +
                    ", evidence=" + (evidence.Count == 0 ? "none" : string.Join(",", evidence.ToArray())) + ".");
            }
            return roots;
        }



        private static BaronHabitatNode EnsureBaronHabitatNode(
            List<BaronHabitatNode> roots,
            Dictionary<string, BaronHabitatNode> nodesById,
            HashSet<string> ancestry,
            string id)
        {
            if (roots == null || nodesById == null || ancestry == null || string.IsNullOrEmpty(id)) return null;
            BaronHabitatNode existing;
            if (nodesById.TryGetValue(id, out existing)) return existing;
            if (!ancestry.Add(id)) return null;

            object record;
            if (!SpaceObjectRecordsById.TryGetValue(id, out record) || record == null)
            {
                ancestry.Remove(id);
                return null;
            }

            BaronHabitatNode node = new BaronHabitatNode(id, LocalizeSpaceObject(id));
            string parentId = GetStringMember(record, "ParentId");
            bool satellite = IsBaronSatelliteRecord(record);
            object parentRecord;
            bool exactParent = satellite && !string.IsNullOrEmpty(parentId) &&
                !string.Equals(parentId, id, StringComparison.OrdinalIgnoreCase) &&
                SpaceObjectRecordsById.TryGetValue(parentId, out parentRecord) && parentRecord != null;

            BaronHabitatNode parent = exactParent
                ? EnsureBaronHabitatNode(roots, nodesById, ancestry, parentId)
                : null;
            if (parent != null)
                parent.Children.Add(node);
            else
                roots.Add(node);

            nodesById[id] = node;
            ancestry.Remove(id);
            return node;
        }

        private static bool IsBaronSatelliteRecord(object record)
        {
            if (record == null) return false;
            string type = ConvertToStableString(GetMember(record, "SpaceObjectType"));
            return type.IndexOf("Satel", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   type.IndexOf("Moon", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static void SortBaronHabitatNodes(List<BaronHabitatNode> nodes)
        {
            if (nodes == null) return;
            nodes.Sort(delegate(BaronHabitatNode a, BaronHabitatNode b)
            {
                return string.Compare(a.Label, b.Label, StringComparison.CurrentCultureIgnoreCase);
            });
            for (int i = 0; i < nodes.Count; i++) SortBaronHabitatNodes(nodes[i].Children);
        }

        private static int CollectBaronHabitatFromRuntimeStations(
            string bramfaturaId, HashSet<string> ids, List<string> evidence)
        {
            if (string.IsNullOrEmpty(bramfaturaId) || ids == null) return 0;
            EnsureBaronHabitatRuntimeStationIndex();

            int stationMatches;
            if (!BaronStationMatchesByBramfatura.TryGetValue(bramfaturaId, out stationMatches))
                stationMatches = 0;
            int bodiesBefore = ids.Count;
            HashSet<string> bodies;
            if (BaronStationBodiesByBramfatura.TryGetValue(bramfaturaId, out bodies) && bodies != null)
                foreach (string bodyId in bodies) ids.Add(bodyId);

            if (stationMatches > 0)
            {
                evidence.Add("station-bramfatura");
                evidence.Add("station-bodies:" + Math.Max(0, ids.Count - bodiesBefore).ToString());
            }
            return stationMatches;
        }

        private static int CollectBaronHabitatFromRuntimeMissions(
            string bramfaturaId, HashSet<string> ids, List<string> evidence)
        {
            if (string.IsNullOrEmpty(bramfaturaId) || ids == null) return 0;
            object missions = ResolveTradeMissionsState();
            if (missions == null) return 0;
            object values = GetMember(missions, "Values");
            if (values == null) return 0;

            List<DataEntry> missionEntries = EnumerateData(values);
            if (missionEntries.Count == 0) return 0;
            EnsureBaronHabitatRuntimeStationIndex();
            if (BaronStationBodyById.Count == 0) return 0;

            string defenseBramfatura = string.Empty;
            object global = GetStaticMember(typeof(Data), "Global");
            if (global != null) defenseBramfatura = GetStringMember(global, "DefenseMissionsBramfaturaId");

            int missionMatches = 0;
            int bodiesBefore = ids.Count;
            for (int i = 0; i < missionEntries.Count; i++)
            {
                object mission = missionEntries[i] == null ? null : missionEntries[i].Value;
                if (mission == null) continue;

                string effectiveBramfatura = GetStringMember(mission, "BramfaturaId");
                // Exact current-build StartMission branch: ProcMissionType 4 substitutes
                // Data.Global.DefenseMissionsBramfaturaId before writing RaidMetadata.
                if (GetIntMember(mission, "ProcMissionType", -1) == 4 &&
                    !string.IsNullOrEmpty(defenseBramfatura))
                    effectiveBramfatura = defenseBramfatura;

                if (!string.Equals(effectiveBramfatura, bramfaturaId, StringComparison.OrdinalIgnoreCase))
                    continue;

                string stationId = GetStringMember(mission, "StationId");
                string bodyId;
                if (string.IsNullOrEmpty(stationId) || !BaronStationBodyById.TryGetValue(stationId, out bodyId))
                    continue;
                missionMatches++;
                ids.Add(bodyId);
            }

            if (missionMatches > 0)
            {
                evidence.Add("mission-bramfatura");
                evidence.Add("mission-bodies:" + Math.Max(0, ids.Count - bodiesBefore).ToString());
            }
            return missionMatches;
        }

        private static void CollectBaronHabitatFromCreatureId(string baronCreatureId, HashSet<string> ids, List<string> evidence)
        {
            if (string.IsNullOrEmpty(baronCreatureId)) return;
            const string suffix = "_baron";
            if (!baronCreatureId.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)) return;
            string candidate = baronCreatureId.Substring(0, baronCreatureId.Length - suffix.Length);
            if (SpaceObjectRecordsById.ContainsKey(candidate) && ids.Add(candidate))
                evidence.Add("baron-id");
        }

        private static void CollectBaronHabitatFromQmorphos(LootBaronSpecialSource source, HashSet<string> ids, List<string> evidence)
        {
            List<QmorphosRecord> records = CollectQmorphosRecordsForBaronIndex();
            for (int i = 0; i < records.Count; i++)
            {
                QmorphosRecord record = records[i];
                if (record == null) continue;
                string id = GetStringMember(record, "Id");
                if (!string.Equals(id, source.QmorphosRecordId, StringComparison.OrdinalIgnoreCase)) continue;
                int before = ids.Count;
                CollectNamedSpaceObjectMembers(record, ids);
                if (ids.Count > before) evidence.Add("qmorph-record");
                break;
            }
        }

        private static void CollectBaronHabitatFromBramfatura(string bramfaturaId, HashSet<string> ids, List<string> evidence)
        {
            if (string.IsNullOrEmpty(bramfaturaId)) return;
            List<DataEntry> entries = EnumerateData(GetStaticMember(typeof(Data), "Bramfaturas"));
            for (int i = 0; i < entries.Count; i++)
            {
                DataEntry entry = entries[i];
                if (entry == null || entry.Value == null) continue;
                string id = FirstNonEmpty(GetStringMember(entry.Value, "Id"), entry.Key);
                if (!string.Equals(id, bramfaturaId, StringComparison.OrdinalIgnoreCase)) continue;
                int before = ids.Count;
                CollectNamedSpaceObjectMembers(entry.Value, ids);
                if (ids.Count > before) evidence.Add("bramfatura-record");
                break;
            }
        }

        private static void CollectNamedSpaceObjectMembers(object record, HashSet<string> ids)
        {
            if (record == null) return;
            for (int i = 0; i < BaronHabitatMemberNames.Length; i++)
            {
                object value = GetMember(record, BaronHabitatMemberNames[i]);
                CollectExistingSpaceObjectIds(value, ids, 0);
            }
        }

        private static void CollectExistingSpaceObjectIds(object value, HashSet<string> ids, int depth)
        {
            if (value == null || depth > 2) return;
            string text = value as string;
            if (text != null)
            {
                if (SpaceObjectRecordsById.ContainsKey(text)) ids.Add(text);
                return;
            }

            IDictionary dict = value as IDictionary;
            if (dict != null)
            {
                foreach (DictionaryEntry pair in dict)
                {
                    CollectExistingSpaceObjectIds(pair.Key, ids, depth + 1);
                    CollectExistingSpaceObjectIds(pair.Value, ids, depth + 1);
                }
                return;
            }

            IEnumerable enumerable = value as IEnumerable;
            if (enumerable != null)
            {
                foreach (object child in enumerable) CollectExistingSpaceObjectIds(child, ids, depth + 1);
                return;
            }

            string id = GetStringMember(value, "Id");
            if (!string.IsNullOrEmpty(id) && SpaceObjectRecordsById.ContainsKey(id)) ids.Add(id);
        }

        private static void CollectBaronHabitatReverseLinks(LootBaronSpecialSource source, HashSet<string> ids, List<string> evidence)
        {
            foreach (KeyValuePair<string, object> pair in SpaceObjectRecordsById)
            {
                object record = pair.Value;
                if (record == null) continue;
                bool matched = false;
                Type type = record.GetType();
                try
                {
                    FieldInfo[] fields = type.GetFields(InstanceFlags);
                    for (int i = 0; i < fields.Length && !matched; i++)
                    {
                        string name = fields[i].Name ?? string.Empty;
                        if (!IsBaronHabitatRelationMember(name)) continue;
                        object value;
                        if (!TryReadBaronHabitatField(record, fields[i], out value)) continue;
                        matched = BaronRelationValueMatches(value, source, 0);
                    }
                    PropertyInfo[] properties = type.GetProperties(InstanceFlags);
                    for (int i = 0; i < properties.Length && !matched; i++)
                    {
                        PropertyInfo property = properties[i];
                        string name = property.Name ?? string.Empty;
                        if (!property.CanRead || property.GetIndexParameters().Length != 0 || !IsBaronHabitatRelationMember(name)) continue;
                        object value;
                        if (!TryReadBaronHabitatProperty(record, property, out value)) continue;
                        matched = BaronRelationValueMatches(value, source, 0);
                    }
                }
                catch (Exception ex)
                {
                    LogRuntimeBoundaryWarningOnce(
                        "baron.habitat.reverse.type." + type.FullName,
                        "Baron habitat reverse-link reflection failed for " + type.FullName + "; this source is omitted.",
                        ex);
                }

                if (matched && ids.Add(pair.Key)) evidence.Add("spaceobject-reverse");
            }
        }

        private static bool TryReadBaronHabitatField(object record, FieldInfo field, out object value)
        {
            value = null;
            if (record == null || field == null) return false;
            try
            {
                value = field.GetValue(record);
                return true;
            }
            catch (Exception ex)
            {
                LogRuntimeBoundaryWarningOnce(
                    "baron.habitat.reverse.field." + field.DeclaringType.FullName + "." + field.Name,
                    "Baron habitat field could not be read; this reverse-link candidate is omitted.",
                    ex);
                return false;
            }
        }

        private static bool TryReadBaronHabitatProperty(object record, PropertyInfo property, out object value)
        {
            value = null;
            if (record == null || property == null) return false;
            try
            {
                value = property.GetValue(record, null);
                return true;
            }
            catch (Exception ex)
            {
                LogRuntimeBoundaryWarningOnce(
                    "baron.habitat.reverse.property." + property.DeclaringType.FullName + "." + property.Name,
                    "Baron habitat property could not be read; this reverse-link candidate is omitted.",
                    ex);
                return false;
            }
        }

        private static bool IsBaronHabitatRelationMember(string name)
        {
            return !string.IsNullOrEmpty(name) &&
                (name.IndexOf("Bramfatura", StringComparison.OrdinalIgnoreCase) >= 0 ||
                 name.IndexOf("Qmorph", StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private static bool BaronRelationValueMatches(object value, LootBaronSpecialSource source, int depth)
        {
            if (value == null || source == null || depth > 2) return false;
            string text = value as string;
            if (text != null)
                return string.Equals(text, source.BramfaturaId, StringComparison.OrdinalIgnoreCase) ||
                       string.Equals(text, source.QmorphosRecordId, StringComparison.OrdinalIgnoreCase);

            IDictionary dict = value as IDictionary;
            if (dict != null)
            {
                foreach (DictionaryEntry pair in dict)
                    if (BaronRelationValueMatches(pair.Key, source, depth + 1) ||
                        BaronRelationValueMatches(pair.Value, source, depth + 1)) return true;
                return false;
            }

            IEnumerable enumerable = value as IEnumerable;
            if (enumerable != null)
            {
                foreach (object child in enumerable)
                    if (BaronRelationValueMatches(child, source, depth + 1)) return true;
            }
            return false;
        }
    }
}
