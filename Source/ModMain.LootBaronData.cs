using System;
using System.Collections;
using System.Collections.Generic;
using MGSC;

namespace ItemIntelligence
{
    public static partial class ModMain
    {
        // Qmorphos uses ConfigRecordCollection<QmorphosRecord>, but we deliberately
        // do not assume one concrete collection wrapper here. The validated game has
        // changed collection exposure before; walk only the small set of record/value
        // containers until the actual QmorphosRecord instances are reached.
        private static List<QmorphosRecord> CollectQmorphosRecordsForBaronIndex()
        {
            List<QmorphosRecord> result = new List<QmorphosRecord>();
            HashSet<object> visited = new HashSet<object>(ReferenceComparer.Instance);
            CollectQmorphosRecordsRecursive(
                GetStaticMember(typeof(Data), "Qmorphos"), result, visited, 0);
            return result;
        }

        private static void CollectQmorphosRecordsRecursive(
            object value,
            List<QmorphosRecord> result,
            HashSet<object> visited,
            int depth)
        {
            if (value == null || result == null || visited == null || depth > 6) return;

            QmorphosRecord record = value as QmorphosRecord;
            if (record != null)
            {
                if (!result.Contains(record)) result.Add(record);
                return;
            }

            Type type = value.GetType();
            if (type.IsPrimitive || type.IsEnum || value is string || value is decimal) return;
            if (!type.IsValueType && !visited.Add(value)) return;

            IDictionary dictionary = value as IDictionary;
            if (dictionary != null)
            {
                foreach (DictionaryEntry entry in dictionary)
                    CollectQmorphosRecordsRecursive(entry.Value, result, visited, depth + 1);
                return;
            }

            IEnumerable enumerable = value as IEnumerable;
            if (enumerable != null)
            {
                int scanned = 0;
                foreach (object child in enumerable)
                {
                    if (++scanned > 4096) break;
                    CollectQmorphosRecordsRecursive(child, result, visited, depth + 1);
                }
                if (result.Count > 0) return;
            }

            string[] members = new string[] { "Records", "_records", "Values", "Items", "Value" };
            for (int i = 0; i < members.Length; i++)
            {
                object nested = GetMember(value, members[i]);
                if (nested == null || ReferenceEquals(nested, value)) continue;
                CollectQmorphosRecordsRecursive(nested, result, visited, depth + 1);
            }
        }
    }
}
