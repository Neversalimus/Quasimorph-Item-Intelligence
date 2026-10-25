using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;

namespace ItemIntelligence
{
    // Reads configured material membership, NOT quantities. ItemsGrades values are
    // denomination/grade inputs to project pricing, not counts of required items.
    internal static class GenomeUpgradeUseReader
    {
        internal const int MaximumPriceRows = 64;
        internal const int MaximumMaterialRows = 256;

        internal static bool TryRead(Type dataType, ISet<string> knownItems,
            out HashSet<string> materials, out string reason)
        {
            materials = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            reason = "data not ready";
            if (dataType == null || knownItems == null || knownItems.Count == 0) return false;
            try
            {
                string prefix = dataType.Namespace + ".";
                Type recordType = dataType.Assembly.GetType(prefix + "MagnumProjectPrice", false);
                Type enumType = dataType.Assembly.GetType(prefix + "MagnumProjectType", false);
                if (recordType == null || enumType == null || !enumType.IsEnum ||
                    !Enum.IsDefined(enumType, "Mercenary"))
                {
                    reason = "project price/enum contract unavailable";
                    return false;
                }
                PropertyInfo tableProperty = dataType.GetProperty("MagnumProjectPrices",
                    BindingFlags.Static | BindingFlags.Public);
                PropertyInfo kindProperty = recordType.GetProperty("ProjectType",
                    BindingFlags.Instance | BindingFlags.Public);
                PropertyInfo gradesProperty = recordType.GetProperty("ItemsGrades",
                    BindingFlags.Instance | BindingFlags.Public);
                if (!IsReadable(tableProperty, typeof(List<>).MakeGenericType(recordType)) ||
                    !IsReadable(kindProperty, enumType) ||
                    !IsReadable(gradesProperty, typeof(Dictionary<string, int>)))
                {
                    reason = "project price property shape changed";
                    return false;
                }
                IList rows = tableProperty.GetValue(null, null) as IList;
                if (rows == null || rows.Count == 0) return false;
                if (rows.Count > MaximumPriceRows)
                {
                    reason = "project price table exceeds bounded reader";
                    return false;
                }
                object genomePrice = null;
                for (int i = 0; i < rows.Count; i++)
                {
                    object row = rows[i];
                    if (row == null || row.GetType() != recordType)
                    {
                        reason = "invalid project price row";
                        return false;
                    }
                    object kind = kindProperty.GetValue(row, null);
                    if (!string.Equals(Enum.GetName(enumType, kind), "Mercenary",
                        StringComparison.Ordinal)) continue;
                    // Never guess which duplicate row a changed/modded loader selects.
                    if (genomePrice != null)
                    {
                        reason = "ambiguous duplicate genome price rows";
                        return false;
                    }
                    genomePrice = row;
                }
                if (genomePrice == null)
                {
                    reason = "genome price row unavailable";
                    return false;
                }
                Dictionary<string, int> grades = gradesProperty.GetValue(genomePrice, null)
                    as Dictionary<string, int>;
                if (grades == null || grades.Count > MaximumMaterialRows)
                {
                    reason = "genome material table unavailable or oversized";
                    return false;
                }
                HashSet<string> snapshot = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (KeyValuePair<string, int> entry in grades)
                {
                    // Vanilla's reviewed AddStepPrice skips non-positive grades. A
                    // positive grade proves configured membership, not a charge at
                    // every level. Do not infer a use from an item name or category.
                    if (entry.Value > 0 && !string.IsNullOrEmpty(entry.Key) &&
                        knownItems.Contains(entry.Key)) snapshot.Add(entry.Key);
                }
                materials = snapshot;
                reason = "configured genome material membership";
                return true;
            }
            catch (Exception ex)
            {
                // No partial result escapes a changing collection or failed getter.
                materials.Clear();
                reason = ex.ToString();
                return false;
            }
        }

        private static bool IsReadable(PropertyInfo property, Type valueType)
        {
            return property != null && property.PropertyType == valueType &&
                property.GetGetMethod(false) != null &&
                property.GetIndexParameters().Length == 0;
        }
    }
}
