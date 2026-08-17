using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using MGSC;
using UnityEngine;

namespace ItemIntelligence
{
    /// <summary>
    /// Owns the physical-container to ContainerItemDrop profile mapping and its
    /// coverage audit. This is separate from the weighted reverse index because a
    /// physical obstacle can expose more than one drop-profile member.
    /// </summary>
    public static partial class ModMain
    {
        private static int _lootContainerProfileCount;
        private static int _lootContainerMappedProfileCount;
        private static int _lootContainerFallbackProfileCount;
        private static int _lootContainerPhysicalRecordCount;
        private static int _lootContainerDescriptorLinkCount;
        private static int _lootContainerAdditionalDropMemberLinks;
        private static int _lootContainerIndexedProfileCount;
        private static int _lootContainerEmptyProfileCount;
        private static int _lootContainerItemLinkCount;
        private static readonly HashSet<string> LootMultiProfilePhysicalContainerIds =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private static readonly List<string> LootFallbackContainerProfileIds =
            new List<string>();

        private static void ResetLootContainerProfileAuditState()
        {
            _lootContainerProfileCount = 0;
            _lootContainerMappedProfileCount = 0;
            _lootContainerFallbackProfileCount = 0;
            _lootContainerPhysicalRecordCount = 0;
            _lootContainerDescriptorLinkCount = 0;
            _lootContainerAdditionalDropMemberLinks = 0;
            _lootContainerIndexedProfileCount = 0;
            _lootContainerEmptyProfileCount = 0;
            _lootContainerItemLinkCount = 0;
            LootMultiProfilePhysicalContainerIds.Clear();
            LootFallbackContainerProfileIds.Clear();
        }

        private static void BuildLootContainerDescriptors()
        {
            HashSet<string> validDropIds = new HashSet<string>(
                LootWarmupContainerDropIds,
                StringComparer.OrdinalIgnoreCase);
            _lootContainerProfileCount = validDropIds.Count;

            List<DataEntry> entries = EnumerateData(
                GetStaticMember(typeof(Data), "ObstacleContainers"));
            _lootContainerPhysicalRecordCount = entries.Count;

            for (int i = 0; i < entries.Count; i++)
            {
                object record = entries[i].Value;
                if (record == null) continue;

                string containerId = FirstNonEmpty(
                    GetStringMember(record, "Id"),
                    entries[i].Key);
                Dictionary<string, string> references =
                    CollectLootContainerDropReferences(record, validDropIds);

                foreach (KeyValuePair<string, string> pair in references)
                {
                    string dropId = pair.Key;
                    string resolvedContainerId = FirstNonEmpty(containerId, dropId);
                    int min;
                    int max;
                    bool rollRangeResolved = ResolveLootContainerRollRange(
                        record, pair.Value, dropId, out min, out max);
                    if (AddLootContainerDescriptor(
                        resolvedContainerId, dropId, min, max, rollRangeResolved))
                    {
                        _lootContainerDescriptorLinkCount++;
                        if (!string.Equals(
                            NormalizeMemberLookupName(pair.Value),
                            "ManualDropId",
                            StringComparison.OrdinalIgnoreCase))
                            _lootContainerAdditionalDropMemberLinks++;
                    }
                }
            }

            HashSet<string> mapped = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (KeyValuePair<string, List<LootContainerDescriptor>> pair in
                LootContainerDescriptorsByDropId)
            {
                if (pair.Value != null && pair.Value.Count > 0) mapped.Add(pair.Key);
            }
            _lootContainerMappedProfileCount = mapped.Count;
            BuildLootMultiProfilePhysicalContainerSet();

            // A profile with no physical ObstacleContainers alias is still a real
            // ContainerItemDrop source. Preserve it under its exact profile id so no
            // weighted relation can disappear merely because presentation metadata is
            // absent or moved in a future game build.
            for (int i = 0; i < LootWarmupContainerDropIds.Count; i++)
            {
                string dropId = LootWarmupContainerDropIds[i];
                if (string.IsNullOrEmpty(dropId) || mapped.Contains(dropId)) continue;
                if (AddLootContainerDescriptor(dropId, dropId, 0, 0, false))
                {
                    _lootContainerFallbackProfileCount++;
                    _lootContainerDescriptorLinkCount++;
                    LootFallbackContainerProfileIds.Add(dropId);
                }
            }
            LootFallbackContainerProfileIds.Sort(StringComparer.OrdinalIgnoreCase);

            Debug.Log(
                "[ItemIntelligence] Loot container profile audit: profiles=" +
                _lootContainerProfileCount.ToString(CultureInfo.InvariantCulture) +
                ", mapped=" + _lootContainerMappedProfileCount.ToString(CultureInfo.InvariantCulture) +
                ", fallback=" + _lootContainerFallbackProfileCount.ToString(CultureInfo.InvariantCulture) +
                ", physicalRecords=" + _lootContainerPhysicalRecordCount.ToString(CultureInfo.InvariantCulture) +
                ", descriptorLinks=" + _lootContainerDescriptorLinkCount.ToString(CultureInfo.InvariantCulture) +
                ", additionalDropMembers=" +
                _lootContainerAdditionalDropMemberLinks.ToString(CultureInfo.InvariantCulture) +
                ", multiProfilePhysical=" +
                LootMultiProfilePhysicalContainerIds.Count.ToString(CultureInfo.InvariantCulture) +
                "; relationCoverage=" +
                (_lootContainerMappedProfileCount + _lootContainerFallbackProfileCount)
                    .ToString(CultureInfo.InvariantCulture) + "/" +
                _lootContainerProfileCount.ToString(CultureInfo.InvariantCulture) +
                "; fallbackIds=" + (LootFallbackContainerProfileIds.Count == 0
                    ? "<none>"
                    : string.Join(",", LootFallbackContainerProfileIds.ToArray())) + ".");
        }

        private static Dictionary<string, string> CollectLootContainerDropReferences(
            object record,
            HashSet<string> validDropIds)
        {
            Dictionary<string, string> result =
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            AddLootContainerDropReference(
                result, "ManualDropId", GetMember(record, "ManualDropId"), validDropIds);

            List<MemberInfo> members = GetReadableMembers(record.GetType());
            for (int i = 0; i < members.Count; i++)
            {
                MemberInfo member = members[i];
                if (member == null ||
                    NormalizeMemberLookupName(member.Name).IndexOf(
                        "Drop", StringComparison.OrdinalIgnoreCase) < 0)
                    continue;
                AddLootContainerDropReference(
                    result, member.Name, GetMemberValue(record, member), validDropIds);
            }
            return result;
        }

        private static void AddLootContainerDropReference(
            Dictionary<string, string> result,
            string memberName,
            object raw,
            HashSet<string> validDropIds)
        {
            if (result == null || raw == null) return;
            string direct = raw as string;
            if (direct != null)
            {
                if (!string.IsNullOrEmpty(direct) &&
                    (validDropIds == null || validDropIds.Count == 0 || validDropIds.Contains(direct)) &&
                    !result.ContainsKey(direct))
                    result[direct] = memberName ?? string.Empty;
                return;
            }

            IEnumerable values = raw as IEnumerable;
            if (values == null) return;
            int scanned = 0;
            foreach (object value in values)
            {
                if (++scanned > 64) break;
                string id = value as string;
                if (string.IsNullOrEmpty(id) ||
                    (validDropIds != null && validDropIds.Count > 0 && !validDropIds.Contains(id)) ||
                    result.ContainsKey(id))
                    continue;
                result[id] = memberName ?? string.Empty;
            }
        }

        private static bool ResolveLootContainerRollRange(
            object record,
            string dropMemberName,
            string dropId,
            out int min,
            out int max)
        {
            min = 0;
            max = 0;
            string normalized = NormalizeMemberLookupName(dropMemberName);
            object range = null;
            if (string.Equals(normalized, "ManualDropId", StringComparison.OrdinalIgnoreCase))
                range = GetMember(record, "ManualDropItemCount");
            else
            {
                string stem = normalized.EndsWith("Id", StringComparison.OrdinalIgnoreCase)
                    ? normalized.Substring(0, normalized.Length - 2)
                    : normalized;
                range = FirstNonNull(
                    GetMember(record, stem + "ItemCount"),
                    GetMember(record, stem + "Count"));
                if (range == null && string.Equals(
                    GetStringMember(record, "ManualDropId"), dropId,
                    StringComparison.OrdinalIgnoreCase))
                    range = GetMember(record, "ManualDropItemCount");
            }

            int fixedCount;
            if (TryToInt(range, out fixedCount))
            {
                min = Math.Max(0, fixedCount);
                max = min;
                return true;
            }
            if (range == null) return false;
            bool minResolved = TryToInt(GetMember(range, "Min"), out min);
            bool maxResolved = TryToInt(GetMember(range, "Max"), out max);
            if (!minResolved || !maxResolved) return false;
            min = Math.Max(0, min);
            max = Math.Max(min, max);
            return true;
        }

        private static bool AddLootContainerDescriptor(
            string containerId,
            string dropId,
            int min,
            int max,
            bool rollRangeResolved)
        {
            if (string.IsNullOrEmpty(dropId)) return false;
            List<LootContainerDescriptor> list;
            if (!LootContainerDescriptorsByDropId.TryGetValue(dropId, out list))
            {
                list = new List<LootContainerDescriptor>();
                LootContainerDescriptorsByDropId[dropId] = list;
            }
            for (int i = 0; i < list.Count; i++)
            {
                LootContainerDescriptor existing = list[i];
                if (existing != null &&
                    string.Equals(existing.ContainerId, containerId, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(existing.DropId, dropId, StringComparison.OrdinalIgnoreCase))
                    return false;
            }
            list.Add(new LootContainerDescriptor(
                containerId, dropId, Math.Max(0, min), Math.Max(0, max), rollRangeResolved));
            return true;
        }

        private static void RecordLootContainerProfileIndexResult(bool hasWeightedData)
        {
            _lootContainerIndexedProfileCount++;
            if (!hasWeightedData) _lootContainerEmptyProfileCount++;
        }

        private static void BuildLootMultiProfilePhysicalContainerSet()
        {
            Dictionary<string, HashSet<string>> profiles =
                new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
            foreach (KeyValuePair<string, List<LootContainerDescriptor>> pair in
                LootContainerDescriptorsByDropId)
            {
                if (pair.Value == null) continue;
                for (int i = 0; i < pair.Value.Count; i++)
                {
                    LootContainerDescriptor descriptor = pair.Value[i];
                    if (descriptor == null || string.IsNullOrEmpty(descriptor.ContainerId)) continue;
                    HashSet<string> ids;
                    if (!profiles.TryGetValue(descriptor.ContainerId, out ids))
                    {
                        ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                        profiles[descriptor.ContainerId] = ids;
                    }
                    ids.Add(descriptor.DropId);
                }
            }
            foreach (KeyValuePair<string, HashSet<string>> pair in profiles)
                if (pair.Value != null && pair.Value.Count > 1)
                    LootMultiProfilePhysicalContainerIds.Add(pair.Key);
        }
    }
}
