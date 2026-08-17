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
    /// Shared read-only reflection, record enumeration and quantity extraction owner.
    /// Extracted in v1.7.36-test10 without changing runtime behavior.
    /// </summary>
    public static partial class ModMain
    {
        private static readonly BindingFlags InstanceFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private static readonly BindingFlags StaticFlags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        private static readonly Dictionary<string, MethodInfo> BoolMethodCache =
            new Dictionary<string, MethodInfo>(StringComparer.Ordinal);
        private static readonly Dictionary<Type, MethodInfo[]> ContainerCountMethodsByType =
            new Dictionary<Type, MethodInfo[]>();
        private static readonly object[] ContainerCountInvokeArgs = new object[1];
        private static readonly HashSet<object> ContainerDeepSearchVisited =
            new HashSet<object>(ReferenceComparer.Instance);

        // Reflection metadata is immutable for the lifetime of the AppDomain.
        private static readonly Dictionary<Type, List<MemberInfo>> ReadableMemberCache =
            new Dictionary<Type, List<MemberInfo>>();
        private static readonly Dictionary<Type, Dictionary<string, MemberInfo>> InstanceMemberLookupCache =
            new Dictionary<Type, Dictionary<string, MemberInfo>>();
        private static readonly Dictionary<Type, Dictionary<string, MemberInfo>> StaticMemberLookupCache =
            new Dictionary<Type, Dictionary<string, MemberInfo>>();
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

        private static object GetStaticMember(Type type, string name)
        {
            if (type == null || string.IsNullOrEmpty(name)) return null;
            try
            {
                MemberInfo member = FindCachedMember(type, name, true);
                if (member == null) return null;
                return GetMemberValue(null, member);
            }
            catch { }
            return null;
        }

        private static object GetMember(object obj, string name)
        {
            if (obj == null || string.IsNullOrEmpty(name)) return null;
            try
            {
                MemberInfo member = FindCachedMember(obj.GetType(), name, false);
                if (member == null) return null;
                return GetMemberValue(obj, member);
            }
            catch { }
            return null;
        }

        private static MemberInfo FindCachedMember(Type type, string name, bool isStatic)
        {
            if (type == null || string.IsNullOrEmpty(name)) return null;

            Dictionary<Type, Dictionary<string, MemberInfo>> ownerCache =
                isStatic ? StaticMemberLookupCache : InstanceMemberLookupCache;

            Dictionary<string, MemberInfo> lookup;
            if (!ownerCache.TryGetValue(type, out lookup))
            {
                lookup = BuildMemberLookup(type, isStatic);
                ownerCache[type] = lookup;
            }

            MemberInfo member;
            if (lookup.TryGetValue(name, out member)) return member;

            string normalized = NormalizeMemberLookupName(name);
            if (lookup.TryGetValue(normalized, out member)) return member;

            // Cache misses as null to avoid repeating fallback work.
            lookup[name] = null;
            if (!string.Equals(name, normalized, StringComparison.OrdinalIgnoreCase))
                lookup[normalized] = null;
            return null;
        }

        private static Dictionary<string, MemberInfo> BuildMemberLookup(Type type, bool isStatic)
        {
            Dictionary<string, MemberInfo> result = new Dictionary<string, MemberInfo>(StringComparer.OrdinalIgnoreCase);
            BindingFlags flags = isStatic ? StaticFlags : InstanceFlags;

            try
            {
                PropertyInfo[] props = type.GetProperties(flags);
                for (int i = 0; i < props.Length; i++)
                {
                    PropertyInfo property = props[i];
                    if (!property.CanRead || property.GetIndexParameters().Length != 0) continue;
                    AddMemberLookupAlias(result, property.Name, property);
                    AddMemberLookupAlias(result, NormalizeMemberLookupName(property.Name), property);
                }
            }
            catch { }

            try
            {
                FieldInfo[] fields = type.GetFields(flags);
                for (int i = 0; i < fields.Length; i++)
                {
                    FieldInfo field = fields[i];
                    if (field.IsStatic != isStatic) continue;
                    AddMemberLookupAlias(result, field.Name, field);
                    AddMemberLookupAlias(result, NormalizeMemberLookupName(field.Name), field);
                }
            }
            catch { }

            return result;
        }

        private static void AddMemberLookupAlias(Dictionary<string, MemberInfo> map, string key, MemberInfo member)
        {
            if (map == null || string.IsNullOrEmpty(key) || member == null) return;
            if (!map.ContainsKey(key)) map[key] = member;
        }

        private static string NormalizeMemberLookupName(string name)
        {
            if (string.IsNullOrEmpty(name)) return string.Empty;
            string value = name.Trim();
            while (value.StartsWith("_", StringComparison.Ordinal)) value = value.Substring(1);
            if (value.StartsWith("<", StringComparison.Ordinal) && value.IndexOf(">k__BackingField", StringComparison.Ordinal) > 1)
                value = value.Substring(1, value.IndexOf(">k__BackingField", StringComparison.Ordinal) - 1);
            return value;
        }

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
            List<MemberInfo> members = GetReadableMembers(valueType);
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

        private static void MergeItemQuantities(Dictionary<string, int> target, Dictionary<string, int> source)
        {
            if (target == null || source == null) return;
            foreach (KeyValuePair<string, int> pair in source) AddQuantity(target, pair.Key, pair.Value);
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

        private static bool ContainerHasItem(object container, string itemId)
        {
            if (container == null || string.IsNullOrEmpty(itemId)) return false;
            int count;
            if (TryGetContainerItemCountFast(container, itemId, out count)) return true;
            return TryGetContainerItemCountDeep(container, itemId, 3, out count);
        }

        private static bool TryGetContainerItemCountDeep(object container, string itemId, int depth, out int count)
        {
            count = 0;
            if (container == null || string.IsNullOrEmpty(itemId) || depth < 0) return false;
            try
            {
                ContainerDeepSearchVisited.Clear();
                return TryFindContainerItemDeep(container, itemId, depth, out count, ContainerDeepSearchVisited);
            }
            finally
            {
                // Never retain arbitrary runtime/save objects beyond this bounded lookup.
                ContainerDeepSearchVisited.Clear();
            }
        }

        private static bool TryFindContainerItemDeep(object value, string itemId, int depth, out int count, HashSet<object> visited)
        {
            count = 0;
            if (value == null || depth < 0) return false;

            string directString = value as string;
            if (directString != null)
            {
                if (!string.Equals(directString, itemId, StringComparison.OrdinalIgnoreCase)) return false;
                count = 1;
                return true;
            }

            Type valueType = value.GetType();
            if (IsSimple(valueType)) return false;
            if (visited != null)
            {
                if (visited.Contains(value)) return false;
                visited.Add(value);
            }

            string directId = GetItemIdDeep(value, 0);
            if (string.Equals(directId, itemId, StringComparison.OrdinalIgnoreCase))
            {
                count = ExtractMatchedContainerCount(value);
                return true;
            }

            IDictionary dict = value as IDictionary;
            if (dict != null)
            {
                int visitedEntries = 0;
                foreach (DictionaryEntry entry in dict)
                {
                    if (++visitedEntries > 4096) break;
                    string keyId = FirstNonEmpty(
                        GetItemIdDeep(entry.Key, 0), entry.Key as string, ConvertToStableString(entry.Key));
                    if (string.Equals(keyId, itemId, StringComparison.OrdinalIgnoreCase))
                    {
                        count = ExtractMatchedContainerCount(entry.Value);
                        return true;
                    }
                    if (TryFindContainerItemDeep(entry.Key, itemId, depth - 1, out count, visited) ||
                        TryFindContainerItemDeep(entry.Value, itemId, depth - 1, out count, visited))
                        return true;
                }
                return false;
            }

            IEnumerable enumerable = value as IEnumerable;
            if (enumerable != null)
            {
                int visitedEntries = 0;
                foreach (object entry in enumerable)
                {
                    if (++visitedEntries > 4096) break;
                    if (TryFindContainerItemDeep(entry, itemId, depth - 1, out count, visited)) return true;
                }
                return false;
            }

            List<MemberInfo> members = GetReadableMembers(valueType);
            for (int i = 0; i < members.Count; i++)
            {
                string name = members[i].Name ?? string.Empty;
                if (name.IndexOf("Price", StringComparison.OrdinalIgnoreCase) < 0 &&
                    name.IndexOf("Item", StringComparison.OrdinalIgnoreCase) < 0 &&
                    name.IndexOf("Cost", StringComparison.OrdinalIgnoreCase) < 0 &&
                    name.IndexOf("Ingredient", StringComparison.OrdinalIgnoreCase) < 0 &&
                    name.IndexOf("Material", StringComparison.OrdinalIgnoreCase) < 0 &&
                    name.IndexOf("Requirement", StringComparison.OrdinalIgnoreCase) < 0 &&
                    name.IndexOf("Input", StringComparison.OrdinalIgnoreCase) < 0 &&
                    name.IndexOf("Output", StringComparison.OrdinalIgnoreCase) < 0 &&
                    name.IndexOf("Produce", StringComparison.OrdinalIgnoreCase) < 0 &&
                    name.IndexOf("Consume", StringComparison.OrdinalIgnoreCase) < 0 &&
                    name.IndexOf("Value", StringComparison.OrdinalIgnoreCase) < 0)
                    continue;
                object child = GetMemberValue(value, members[i]);
                if (child == null || object.ReferenceEquals(child, value)) continue;
                if (TryFindContainerItemDeep(child, itemId, depth - 1, out count, visited)) return true;
            }
            return false;
        }

        private static bool TryGetContainerItemCountFast(object container, string itemId, out int count)
        {
            count = 0;
            if (container == null || string.IsNullOrEmpty(itemId)) return false;

            string directString = container as string;
            if (directString != null)
            {
                if (string.Equals(directString, itemId, StringComparison.OrdinalIgnoreCase))
                {
                    count = 1;
                    return true;
                }
                return false;
            }

            IDictionary dict = container as IDictionary;
            if (dict != null)
            {
                try
                {
                    if (dict.Contains(itemId))
                    {
                        count = ExtractMatchedContainerCount(dict[itemId]);
                        return true;
                    }
                }
                catch { }

                int visited = 0;
                foreach (DictionaryEntry entry in dict)
                {
                    if (++visited > 4096) break;
                    string keyId = FirstNonEmpty(
                        GetItemIdDeep(entry.Key, 0), entry.Key as string, ConvertToStableString(entry.Key));
                    if (string.Equals(keyId, itemId, StringComparison.OrdinalIgnoreCase))
                    {
                        count = ExtractMatchedContainerCount(entry.Value);
                        return true;
                    }
                    string valueId = GetItemIdDeep(entry.Value, 0);
                    if (string.Equals(valueId, itemId, StringComparison.OrdinalIgnoreCase))
                    {
                        count = ExtractMatchedContainerCount(entry.Value);
                        return true;
                    }
                }
                return false;
            }

            IEnumerable enumerable = container as IEnumerable;
            if (enumerable != null)
            {
                int visited = 0;
                foreach (object entry in enumerable)
                {
                    if (++visited > 4096) break;
                    if (entry == null) continue;
                    object key = GetMember(entry, "Key");
                    object value = GetMember(entry, "Value");
                    string keyId = FirstNonEmpty(
                        GetItemIdDeep(key, 0), key as string, ConvertToStableString(key));
                    if (string.Equals(keyId, itemId, StringComparison.OrdinalIgnoreCase))
                    {
                        count = ExtractMatchedContainerCount(value ?? entry);
                        return true;
                    }
                    string entryId = GetItemIdDeep(entry, 0);
                    if (string.Equals(entryId, itemId, StringComparison.OrdinalIgnoreCase))
                    {
                        count = ExtractMatchedContainerCount(entry);
                        return true;
                    }
                }
                return false;
            }

            string directId = GetItemIdDeep(container, 0);
            if (string.Equals(directId, itemId, StringComparison.OrdinalIgnoreCase))
            {
                count = ExtractMatchedContainerCount(container);
                return true;
            }
            return false;
        }

        private static int ExtractMatchedContainerCount(object value)
        {
            int parsed;
            if (TryToInt(value, out parsed)) return Math.Max(0, parsed);
            object rawCount = FirstNonNull(
                GetMember(value, "Count"), GetMember(value, "Quantity"),
                GetMember(value, "Amount"), GetMember(value, "ItemsCount"));
            if (TryToInt(rawCount, out parsed)) return Math.Max(0, parsed);
            return 1;
        }

        private static MethodInfo[] GetCachedContainerCountMethods(Type type)
        {
            if (type == null) return new MethodInfo[0];
            MethodInfo[] cached;
            if (ContainerCountMethodsByType.TryGetValue(type, out cached)) return cached;

            List<MethodInfo> matches = new List<MethodInfo>();
            try
            {
                MethodInfo[] methods = type.GetMethods(InstanceFlags);
                for (int i = 0; i < methods.Length; i++)
                {
                    MethodInfo method = methods[i];
                    if (method == null) continue;
                    string name = method.Name ?? string.Empty;
                    if (name.IndexOf("Count", StringComparison.OrdinalIgnoreCase) < 0 &&
                        name.IndexOf("Amount", StringComparison.OrdinalIgnoreCase) < 0)
                        continue;
                    ParameterInfo[] parameters = method.GetParameters();
                    if (parameters != null && parameters.Length == 1) matches.Add(method);
                }
            }
            catch { }
            cached = matches.ToArray();
            ContainerCountMethodsByType[type] = cached;
            return cached;
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

        private static List<MemberInfo> GetReadableMembers(Type type)
        {
            if (type == null) return new List<MemberInfo>();

            List<MemberInfo> cached;
            if (ReadableMemberCache.TryGetValue(type, out cached))
                return cached;

            List<MemberInfo> result = new List<MemberInfo>();
            try
            {
                FieldInfo[] fields = type.GetFields(InstanceFlags);
                for (int i = 0; i < fields.Length; i++)
                    if (!fields[i].IsStatic) result.Add(fields[i]);
            }
            catch { }

            try
            {
                PropertyInfo[] props = type.GetProperties(InstanceFlags);
                for (int i = 0; i < props.Length; i++)
                    if (props[i].GetIndexParameters().Length == 0 && props[i].CanRead)
                        result.Add(props[i]);
            }
            catch { }

            ReadableMemberCache[type] = result;
            return result;
        }

        private static object GetMemberValue(object obj, MemberInfo member)
        {
            try
            {
                FieldInfo f = member as FieldInfo;
                if (f != null) return f.GetValue(obj);
                PropertyInfo p = member as PropertyInfo;
                if (p != null) return p.GetValue(obj, null);
            }
            catch { }
            return null;
        }
    }
}
