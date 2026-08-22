using System;
using UnityEngine;

namespace ItemIntelligence
{
    public static partial class ModMain
    {
        // Preserve vanilla per-instance rounding while rejecting values that would
        // overflow an int during the subsequent fragment/cast aggregation.
        private static bool TryRoundAndScaleDamage(
            float value, int firstCount, int secondCount, out int result)
        {
            result = 0;
            if (float.IsNaN(value) || float.IsInfinity(value) || value < 0f ||
                (double)value > int.MaxValue || firstCount <= 0 || secondCount <= 0)
                return false;

            int rounded = Mathf.RoundToInt(value);
            if (rounded < 0) return false;
            long wide = (long)rounded * firstCount;
            if (wide > int.MaxValue) return false;
            wide *= secondCount;
            if (wide > int.MaxValue) return false;
            result = (int)wide;
            return true;
        }
    }
}
