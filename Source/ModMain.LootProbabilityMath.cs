using System;

namespace ItemIntelligence
{
    // Deterministic probability core: compiled unchanged by the behavior test suite.
    public static partial class ModMain
    {
        private static double CorpseBonusAtLeastOnceChance(double perRoll, double expectedRolls)
        {
            if (double.IsNaN(perRoll) || double.IsInfinity(perRoll) ||
                double.IsNaN(expectedRolls) || double.IsInfinity(expectedRolls))
                return double.NaN;
            perRoll = Math.Max(0.0, Math.Min(1.0, perRoll));
            expectedRolls = Math.Max(0.0, expectedRolls);
            if (perRoll <= 0.0 || expectedRolls <= 0.0) return 0.0;

            // CreatureData.RollExpectedCount(expected) resolves floor(expected) rolls
            // plus one extra roll with probability equal to the fractional remainder.
            // Integrating both outcomes gives the exact chance without consuming the
            // gameplay RNG from the information UI.
            double floorRolls = Math.Floor(expectedRolls);
            double fraction = expectedRolls - floorRolls;
            double pFloor = 1.0 - Math.Pow(1.0 - perRoll, floorRolls);
            double pCeil = 1.0 - Math.Pow(1.0 - perRoll, floorRolls + 1);
            return (1.0 - fraction) * pFloor + fraction * pCeil;
        }
    }
}
