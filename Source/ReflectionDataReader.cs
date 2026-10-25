using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Reflection;

namespace ItemIntelligence
{
    // Owns immutable reflection metadata, never live game objects or their values.
    // Each reader has private caches. Metadata stays valid for the AppDomain lifetime.
    internal sealed class ReflectionDataReader
    {
        private static readonly ReadOnlyCollection<MemberInfo> EmptyMembers = new List<MemberInfo>().AsReadOnly();
        private static readonly BindingFlags InstanceFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private static readonly BindingFlags StaticFlags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        private readonly Dictionary<string, MethodInfo> BoolMethodCache =
            new Dictionary<string, MethodInfo>(StringComparer.Ordinal);
        // Reflection metadata is immutable for the lifetime of the AppDomain.
        private readonly Dictionary<Type, ReadOnlyCollection<MemberInfo>> ReadableMemberCache =
            new Dictionary<Type, ReadOnlyCollection<MemberInfo>>();
        private readonly Dictionary<Type, Dictionary<string, MemberInfo>> InstanceMemberLookupCache =
            new Dictionary<Type, Dictionary<string, MemberInfo>>();
        private readonly Dictionary<Type, Dictionary<string, MemberInfo>> StaticMemberLookupCache =
            new Dictionary<Type, Dictionary<string, MemberInfo>>();

        internal object GetStaticMember(Type type, string name)
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

        internal object GetMember(object obj, string name)
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

        internal MemberInfo FindCachedMember(Type type, string name, bool isStatic)
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

        internal static string NormalizeMemberLookupName(string name)
        {
            if (string.IsNullOrEmpty(name)) return string.Empty;
            string value = name.Trim();
            while (value.StartsWith("_", StringComparison.Ordinal)) value = value.Substring(1);
            if (value.StartsWith("<", StringComparison.Ordinal) && value.IndexOf(">k__BackingField", StringComparison.Ordinal) > 1)
                value = value.Substring(1, value.IndexOf(">k__BackingField", StringComparison.Ordinal) - 1);
            return value;
        }

        internal IList<MemberInfo> GetReadableMembers(Type type)
        {
            if (type == null) return EmptyMembers;

            ReadOnlyCollection<MemberInfo> cached;
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

            ReadOnlyCollection<MemberInfo> snapshot = result.AsReadOnly();
            ReadableMemberCache[type] = snapshot;
            return snapshot;
        }

        internal static object GetMemberValue(object obj, MemberInfo member)
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

        internal bool? CallBool(object instance, string methodName, string id)
        {
            if (instance == null || string.IsNullOrEmpty(id)) return null;
            try
            {
                Type type = instance.GetType();
                string cacheKey = type.AssemblyQualifiedName + "|" + methodName;
                MethodInfo method;
                if (!BoolMethodCache.TryGetValue(cacheKey, out method))
                {
                    MethodInfo[] methods = type.GetMethods(InstanceFlags);
                    for (int i = 0; i < methods.Length; i++)
                    {
                        MethodInfo candidate = methods[i];
                        if (!string.Equals(candidate.Name, methodName, StringComparison.Ordinal) || candidate.ReturnType != typeof(bool)) continue;
                        ParameterInfo[] p = candidate.GetParameters();
                        if (p.Length != 1) continue;
                        method = candidate;
                        break;
                    }
                    BoolMethodCache[cacheKey] = method;
                }

                if (method == null) return null;
                ParameterInfo[] parameters = method.GetParameters();
                object arg = ConvertArgument(id, parameters[0].ParameterType);
                if (arg == null && parameters[0].ParameterType.IsValueType) return null;
                return (bool)method.Invoke(instance, new object[] { arg });
            }
            catch { }
            return null;
        }

        private static object ConvertArgument(string value, Type targetType)
        {
            if (targetType == typeof(string) || targetType == typeof(object)) return value;
            try
            {
                if (targetType.IsEnum) return Enum.Parse(targetType, value, true);
                ConstructorInfo ctor = targetType.GetConstructor(new Type[] { typeof(string) });
                if (ctor != null) return ctor.Invoke(new object[] { value });
                return Convert.ChangeType(value, targetType, CultureInfo.InvariantCulture);
            }
            catch { return null; }
        }
    }
}
