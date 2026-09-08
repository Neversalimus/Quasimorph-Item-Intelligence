using System;
using System.Collections.Generic;

namespace ItemIntelligence
{
    public static partial class ModMain
    {
        // WoundSlotRecord.AmputatedDrop is List<Tuple<float, string>>, in weight/ID order.
        // This projects the base DropManager pool. Magnum's forced augmentation override
        // is explicitly excluded from the base-chance column; gameplay RNG is never called.
        private static bool TryExtractAmputationDropWeights(
            object value, out Dictionary<string, double> result)
        {
            result = new Dictionary<string, double>(StringComparer.Ordinal);
            List<Tuple<float, string>> rows = value as List<Tuple<float, string>>;
            if (rows == null || rows.Count == 0) return false;

            float vanillaTotal = 0f;
            for (int i = 0; i < rows.Count; i++)
            {
                Tuple<float, string> row = rows[i];
                if (row == null || string.IsNullOrEmpty(row.Item2) || row.Item1 <= 0f ||
                    float.IsNaN(row.Item1) || float.IsInfinity(row.Item1))
                {
                    result.Clear();
                    return false;
                }
                vanillaTotal += row.Item1;
                if (float.IsInfinity(vanillaTotal))
                {
                    result.Clear();
                    return false;
                }
                string id = row.Item2;
                double previous;
                result.TryGetValue(id, out previous);
                // Repeated outcomes are separate vanilla intervals; sum their weights.
                result[id] = previous + row.Item1;
            }
            return result.Count > 0;
        }
    }
}
