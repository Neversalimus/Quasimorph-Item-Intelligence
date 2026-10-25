using System;
using System.Collections.Generic;
using MGSC;

namespace ItemIntelligence
{
    public static partial class ModMain
    {
        private static bool _upgradeUsesAttempted;
        private static readonly Dictionary<string, List<UpgradeUse>> UpgradeMaterials =
            new Dictionary<string, List<UpgradeUse>>(StringComparer.OrdinalIgnoreCase);
        private static readonly HashSet<string> UpgradeUseLoggedItems =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        private static bool SupportsUpgradeUseSchema(string assemblySha)
        {
            return string.Equals(assemblySha,
                "D72CD91F98DE4D6CCE8EE420AC1A906D89E68BA0CB164694D266B56F46339EBE",
                StringComparison.OrdinalIgnoreCase);
        }

        private static void ResetUpgradeUseCache()
        {
            _upgradeUsesAttempted = false;
            UpgradeMaterials.Clear();
            UpgradeUseLoggedItems.Clear();
        }

        private static void RefreshUpgradeUses(bool force)
        {
            if (!ShowMagnumUses || KnownItemIds.Count == 0) return;
            if (_upgradeUsesAttempted && !force) return;
            _upgradeUsesAttempted = true;
            UpgradeMaterials.Clear();
            if (!SupportsUpgradeUseSchema(_compatAssemblySha256))
            {
                LogRuntimeBoundaryWarningOnce("upgrade-use.build",
                    "Upgrade-use metadata hidden: unsupported game fingerprint.", null);
                return;
            }
            try
            {
                Dictionary<string, List<UpgradeUse>> materials;
                string reason;
                if (Data.Items == null || !UpgradeUseReader.TryRead(Data.MagnumProjectPrices,
                    Data.ProduceReceipts, KnownItemIds, ResolveUpgradeEquipmentKind, out materials, out reason))
                {
                    LogRuntimeBoundaryWarningOnce("upgrade-use.schema",
                        "Upgrade-use metadata unavailable; waiting for valid game tables.", null);
                    return;
                }
                AddCloneAndPactUpgradeUses(materials);
                foreach (var entry in materials) UpgradeMaterials.Add(entry.Key, entry.Value);
            }
            catch (Exception ex)
            {
                UpgradeMaterials.Clear();
                LogRuntimeBoundaryWarningOnce("upgrade-use.read", "Upgrade-use metadata hidden.", ex);
            }
        }

        private static bool HasUpgradeUse(string itemId)
        {
            if (!ShowMagnumUses || string.IsNullOrEmpty(itemId) || !KnownItemIds.Contains(itemId)) return false;
            RefreshUpgradeUses(false);
            return UpgradeMaterials.ContainsKey(itemId);
        }

        private static void LogUpgradeUse(string itemId)
        {
            if (UpgradeUseLoggedItems.Count >= 32 || !UpgradeUseLoggedItems.Add(itemId)) return;
            VerboseLog("[ItemIntelligence][UpgradeUses] item=" + itemId +
                ", source=MagnumProjectPrices+ProduceReceipts+item-records" +
                ", membership=configured, quantities=not-projected, shipResearchTotal=unchanged.");
        }
    }
}
