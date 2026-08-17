using System;
using System.Collections.Generic;

namespace ItemIntelligence
{
    public static partial class ModMain
    {
        // v1.7.36-test8: Magnum session/index state has an explicit feature owner.
        // Behavior remains in the existing partial methods for this maintenance pass.
        private static object _magnumProgression;
        private static bool _magnumLightLookupAttempted;
        private static bool _runtimeMagnumIndexBuilt;
        private static readonly Dictionary<string, List<MagnumUse>> MagnumUses =
            new Dictionary<string, List<MagnumUse>>(StringComparer.OrdinalIgnoreCase);

        private static void ResetMagnumRuntimeSessionState()
        {
            _runtimeMagnumIndexBuilt = false;
            _magnumLightLookupAttempted = false;
            _magnumProgression = null;
        }

        private static void ResetMagnumIndexState()
        {
            MagnumUses.Clear();
            // ClearIndexes can run again when strategy data appears after bootstrap.
            // The runtime progression pass must be allowed to repopulate the new index.
            _runtimeMagnumIndexBuilt = false;
        }
    }
}
