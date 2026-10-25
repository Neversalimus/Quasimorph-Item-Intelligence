using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using MGSC;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ItemIntelligence
{
    /// <summary>
    /// Adapters for reflection reads, record enumeration and quantity extraction.
    /// </summary>
    public static partial class ModMain
    {
        private static readonly ReflectionDataReader ReflectionData = new ReflectionDataReader();
        private static readonly BindingFlags InstanceFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private static readonly BindingFlags StaticFlags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        private static readonly string[] ItemIdNestedMemberNames = new string[]
        {
            "Item", "ItemRecord", "Record", "OutputItem", "InputItem", "Value"
        };

        private static bool PerformanceBudgetExceeded(long startedTimestamp, double budgetMs)
        {
            if (budgetMs <= 0.0) return false;
            long elapsed = System.Diagnostics.Stopwatch.GetTimestamp() - startedTimestamp;
            if (elapsed <= 0) return false;
            return (elapsed * 1000.0 / System.Diagnostics.Stopwatch.Frequency) >= budgetMs;
        }

        private sealed class DataEntry
        {
            public readonly string Key;
            public readonly object Value;
            public DataEntry(string key, object value) { Key = key ?? string.Empty; Value = value; }
        }

        private sealed class ReferenceComparer : IEqualityComparer<object>
        {
            public static readonly ReferenceComparer Instance = new ReferenceComparer();
            public new bool Equals(object x, object y) { return ReferenceEquals(x, y); }
            public int GetHashCode(object obj)
            {
                return obj == null
                    ? 0
                    : System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
            }
        }

        private static object GetStaticMember(Type type, string name) { return ReflectionData.GetStaticMember(type, name); }

        private static object GetMember(object obj, string name) { return ReflectionData.GetMember(obj, name); }

        private static MemberInfo FindCachedMember(Type type, string name, bool isStatic) { return ReflectionData.FindCachedMember(type, name, isStatic); }

        private static string NormalizeMemberLookupName(string name) { return ReflectionDataReader.NormalizeMemberLookupName(name); }


        private static string GetStringMember(object obj, string name)
        {
            object value = GetMember(obj, name);
            return value == null ? string.Empty : Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
        }

        private static bool? GetBoolMember(object obj, string name)
        {
            object value = GetMember(obj, name);
            if (value is bool) return (bool)value;
            if (value != null)
            {
                bool parsed;
                if (bool.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), out parsed)) return parsed;
            }
            return null;
        }

        private static List<DataEntry> EnumerateData(object collection)
        {
            List<DataEntry> result = new List<DataEntry>();
            if (collection == null) return result;
            IEnumerable enumerable = collection as IEnumerable;
            if (enumerable == null)
            {
                // ConfigRecordCollection<T> (used by Data.Items and Data.MagnumPerks in
                // Quasimorph 1.0.1) is not itself IEnumerable. Its public Records property
                // exposes the actual records. Missing this was the root cause of
                // KnownItems=0, Weapons=0 and MagnumItems=0 in Player.log.
                object nested = GetMember(collection, "Records");
                if (nested == null) nested = GetMember(collection, "_records");
                if (nested == null) nested = GetMember(collection, "Values");
                if (nested != null && !object.ReferenceEquals(nested, collection))
                    return EnumerateData(nested);

                result.Add(new DataEntry(string.Empty, collection));
                return result;
            }
            foreach (object raw in enumerable)
            {
                if (raw == null) continue;
                object key = GetMember(raw, "Key");
                object value = GetMember(raw, "Value");
                if (value != null && raw.GetType().Name.IndexOf("KeyValuePair", StringComparison.OrdinalIgnoreCase) >= 0)
                    result.Add(new DataEntry(ConvertToStableString(key), value));
                else
                    result.Add(new DataEntry(string.Empty, raw));
            }
            return result;
        }

        private static List<string> ExtractStringIds(object value)
        {
            List<string> result = new List<string>();
            ExtractStringIdsInto(value, result);
            return result;
        }

        private static void ExtractStringIdsInto(object value, List<string> result)
        {
            if (value == null || result == null) return;
            string direct = value as string;
            if (direct != null)
            {
                if (!string.IsNullOrEmpty(direct) && !result.Contains(direct)) result.Add(direct);
                return;
            }

            IEnumerable enumerable = value as IEnumerable;
            if (enumerable != null && !(value is IDictionary))
            {
                foreach (object item in enumerable)
                    ExtractStringIdsInto(item, result);
                return;
            }

            if (value is IDictionary)
            {
                IDictionary dict = (IDictionary)value;
                foreach (DictionaryEntry entry in dict)
                {
                    string id = GetItemId(entry.Key);
                    if (string.IsNullOrEmpty(id)) id = ConvertToStableString(entry.Key);
                    if (!string.IsNullOrEmpty(id) && !result.Contains(id)) result.Add(id);
                }
                return;
            }

            string candidate = FirstNonEmpty(
                GetStringMember(value, "Id"),
                GetStringMember(value, "PerkId"),
                GetStringMember(value, "WorkbenchId"),
                GetStringMember(value, "ItemId"),
                GetStringMember(value, "Key"));
            if (!string.IsNullOrEmpty(candidate) && !result.Contains(candidate))
                result.Add(candidate);
        }

        private static List<MemberInfo> GetStaticDataMembers()
        {
            List<MemberInfo> result = new List<MemberInfo>();
            Type dataType = typeof(Data);
            PropertyInfo[] props;
            try { props = dataType.GetProperties(StaticFlags); } catch { props = new PropertyInfo[0]; }
            for (int i = 0; i < props.Length; i++)
                if (props[i].CanRead && props[i].GetIndexParameters().Length == 0) result.Add(props[i]);
            FieldInfo[] fields;
            try { fields = dataType.GetFields(StaticFlags); } catch { fields = new FieldInfo[0]; }
            for (int i = 0; i < fields.Length; i++) result.Add(fields[i]);
            return result;
        }

        private static Type GetMemberDeclaredType(MemberInfo member)
        {
            FieldInfo field = member as FieldInfo;
            if (field != null) return field.FieldType;
            PropertyInfo prop = member as PropertyInfo;
            if (prop != null) return prop.PropertyType;
            return null;
        }

        private static bool TypeContainsRecordType(Type containerType, Type target)
        {
            if (containerType == null || target == null) return false;
            if (target.IsAssignableFrom(containerType)) return true;
            if (containerType.IsArray) return TypeContainsRecordType(containerType.GetElementType(), target);
            if (containerType.IsGenericType)
            {
                Type[] args = containerType.GetGenericArguments();
                for (int i = 0; i < args.Length; i++)
                    if (TypeContainsRecordType(args[i], target)) return true;
            }
            return false;
        }

        private static bool IsCostLikeMemberName(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;
            return name.IndexOf("Price", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   name.IndexOf("Cost", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   name.IndexOf("Required", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   name.IndexOf("Resource", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   name.IndexOf("Upgrade", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   name.IndexOf("Item", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   name.IndexOf("Cargo", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   name.IndexOf("Component", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static Dictionary<string, int> ExtractKnownItemQuantitiesDeep(object value, int depth)
        {
            Dictionary<string, int> result = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            ExtractKnownItemQuantitiesDeepInto(value, result, depth, new HashSet<object>(ReferenceComparer.Instance));
            return result;
        }

        private static void ExtractKnownItemQuantitiesDeepInto(object value, Dictionary<string, int> result, int depth, HashSet<object> visited)
        {
            if (value == null || result == null || depth < 0) return;
            string directString = value as string;
            if (directString != null)
            {
                if (KnownItemIds.Contains(directString)) AddQuantity(result, directString, 1);
                return;
            }
            Type valueType = value.GetType();
            if (IsSimple(valueType)) return;
            if (visited.Contains(value)) return;
            visited.Add(value);

            string directId = GetItemIdDeep(value, 0);
            if (!string.IsNullOrEmpty(directId) && KnownItemIds.Contains(directId))
            {
                int count = 1;
                object rawCount = FirstNonNull(GetMember(value, "Count"), GetMember(value, "Quantity"), GetMember(value, "Amount"), GetMember(value, "ItemsCount"));
                int parsed;
                if (TryToInt(rawCount, out parsed) && parsed > 0) count = parsed;
                AddQuantity(result, directId, count);
                return;
            }

            IDictionary dict = value as IDictionary;
            if (dict != null)
            {
                foreach (DictionaryEntry entry in dict)
                {
                    string keyId = GetItemIdDeep(entry.Key, 0);
                    if (string.IsNullOrEmpty(keyId)) keyId = entry.Key as string;
                    int count;
                    if (!string.IsNullOrEmpty(keyId) && KnownItemIds.Contains(keyId) && TryToInt(entry.Value, out count))
                    {
                        AddQuantity(result, keyId, Math.Max(1, count));
                        continue;
                    }
                    ExtractKnownItemQuantitiesDeepInto(entry.Key, result, depth - 1, visited);
                    ExtractKnownItemQuantitiesDeepInto(entry.Value, result, depth - 1, visited);
                }
                return;
            }

            IEnumerable enumerable = value as IEnumerable;
            if (enumerable != null && !(value is string))
            {
                int count = 0;
                foreach (object entry in enumerable)
                {
                    if (++count > 2048) break;
                    ExtractKnownItemQuantitiesDeepInto(entry, result, depth - 1, visited);
                }
                return;
            }

            if (depth == 0) return;
            IList<MemberInfo> members = GetReadableMembers(valueType);
            for (int i = 0; i < members.Count; i++)
            {
                string name = members[i].Name ?? string.Empty;
                if (!IsCostLikeMemberName(name) &&
                    name.IndexOf("Input", StringComparison.OrdinalIgnoreCase) < 0 &&
                    name.IndexOf("Output", StringComparison.OrdinalIgnoreCase) < 0 &&
                    name.IndexOf("Produce", StringComparison.OrdinalIgnoreCase) < 0 &&
                    name.IndexOf("Consume", StringComparison.OrdinalIgnoreCase) < 0 &&
                    name.IndexOf("Value", StringComparison.OrdinalIgnoreCase) < 0)
                    continue;
                object child = GetMemberValue(value, members[i]);
                if (child == null || object.ReferenceEquals(child, value)) continue;
                ExtractKnownItemQuantitiesDeepInto(child, result, depth - 1, visited);
            }
        }

        private static object FirstNonNull(object a, object b)
        {
            return a ?? b;
        }

        private static object FirstNonNull(object a, object b, object c)
        {
            return a ?? b ?? c;
        }

        private static object FirstNonNull(object a, object b, object c, object d)
        {
            return a ?? b ?? c ?? d;
        }

        private static object FirstNonNull(params object[] values)
        {
            if (values == null) return null;
            for (int i = 0; i < values.Length; i++) if (values[i] != null) return values[i];
            return null;
        }

        private static Dictionary<string, int> ExtractItemQuantities(object value)
        {
            Dictionary<string, int> result = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            ExtractItemQuantitiesInto(value, result);
            return result;
        }

        private static void ExtractItemQuantitiesInto(object value, Dictionary<string, int> result)
        {
            if (value == null) return;
            string s = value as string;
            if (s != null)
            {
                AddQuantity(result, s, 1);
                return;
            }
            IEnumerable enumerable = value as IEnumerable;
            if (enumerable != null && !(value is IDictionary))
            {
                foreach (object item in enumerable) ExtractSingleItemQuantity(item, result);
                return;
            }
            if (value is IDictionary)
            {
                IDictionary dict = (IDictionary)value;
                foreach (DictionaryEntry e in dict)
                {
                    string id = GetItemId(e.Key);
                    int count;
                    if (!TryToInt(e.Value, out count)) count = 1;
                    if (!string.IsNullOrEmpty(id)) AddQuantity(result, id, Math.Max(1, count));
                }
                return;
            }
            ExtractSingleItemQuantity(value, result);
        }

        private static void ExtractSingleItemQuantity(object item, Dictionary<string, int> result)
        {
            if (item == null) return;
            if (item is string)
            {
                AddQuantity(result, (string)item, 1);
                return;
            }

            object pairKey = GetMember(item, "Key");
            object pairValue = GetMember(item, "Value");
            if (pairValue != null && item.GetType().Name.IndexOf("KeyValuePair", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                string pairId = GetItemId(pairKey);
                int pairCount;
                if (!TryToInt(pairValue, out pairCount)) pairCount = 1;
                if (!string.IsNullOrEmpty(pairId)) AddQuantity(result, pairId, Math.Max(1, pairCount));
                return;
            }

            string id = FirstNonEmpty(GetStringMember(item, "ItemId"), GetStringMember(item, "Id"));

            // Some Quasimorph records store the referenced item object itself in Item,
            // rather than duplicating its string ItemId. Converting that object with
            // ToString() loses the real ID and caused part of barter/Magnum coverage
            // to disappear. Resolve the nested object explicitly.
            if (string.IsNullOrEmpty(id))
            {
                object rawItem = GetMember(item, "Item");
                id = GetItemIdDeep(rawItem, 0);
            }

            if (string.IsNullOrEmpty(id))
            {
                object rawRecord = GetMember(item, "ItemRecord");
                id = GetItemIdDeep(rawRecord, 0);
            }

            if (string.IsNullOrEmpty(id))
            {
                object rawRecord = GetMember(item, "Record");
                id = GetItemIdDeep(rawRecord, 0);
            }

            if (string.IsNullOrEmpty(id)) return;

            int count = 1;
            object rawCount = GetMember(item, "Count");
            if (rawCount == null) rawCount = GetMember(item, "Quantity");
            if (rawCount == null) rawCount = GetMember(item, "Amount");
            int parsed;
            if (TryToInt(rawCount, out parsed)) count = Math.Max(1, parsed);

            AddQuantity(result, id, count);
        }

        private static void AddQuantity(Dictionary<string, int> map, string id, int count)
        {
            if (string.IsNullOrEmpty(id)) return;
            int existing;
            map.TryGetValue(id, out existing);
            map[id] = existing + Math.Max(1, count);
        }

        private static string GetItemId(object value)
        {
            return GetItemIdDeep(value, 0);
        }

        private static string GetItemIdDeep(object value, int depth)
        {
            if (value == null || depth > 3) return string.Empty;
            if (value is string) return (string)value;
            if (value is BasePickupItem) return ((BasePickupItem)value).Id ?? string.Empty;

            string direct = FirstNonEmpty(
                GetStringMember(value, "ItemId"),
                GetStringMember(value, "Id"));
            if (!string.IsNullOrEmpty(direct))
                return direct;

            for (int i = 0; i < ItemIdNestedMemberNames.Length; i++)
            {
                object nested = GetMember(value, ItemIdNestedMemberNames[i]);
                if (nested == null || object.ReferenceEquals(nested, value)) continue;
                string nestedId = GetItemIdDeep(nested, depth + 1);
                if (!string.IsNullOrEmpty(nestedId))
                    return nestedId;
            }

            return string.Empty;
        }

        private static IList<MemberInfo> GetReadableMembers(Type type) { return ReflectionData.GetReadableMembers(type); }

        private static object GetMemberValue(object obj, MemberInfo member) { return ReflectionDataReader.GetMemberValue(obj, member); }

    }
}
