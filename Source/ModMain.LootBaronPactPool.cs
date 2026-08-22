using System;

namespace ItemIntelligence
{
    public static partial class ModMain
    {
        private static bool IsBaronPactItem(string itemId)
        {
            if (string.IsNullOrEmpty(itemId)) return false;
            object record = ResolveCanonicalItemMetadataRecord(itemId);
            return record != null && string.Equals(
                ConvertToStableString(GetMember(record, "ItemClass")),
                "QuasiPact", StringComparison.OrdinalIgnoreCase);
        }
    }
}
