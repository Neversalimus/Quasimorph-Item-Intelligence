using System;
using System.Collections.Generic;
using MGSC;

namespace ItemIntelligence
{
    public static partial class ModMain
    {
        private static bool _genomeUpgradeUsesAttempted;
        private static readonly HashSet<string> GenomeUpgradeMaterialIds =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private static readonly HashSet<string> GenomeUpgradeUseLoggedItems =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        private static bool SupportsGenomeUpgradeUseSchema(string assemblySha)
        {
            // This is a narrow metadata adapter under test for the current build,
            // not an approval of project-cost formulas or the global compatibility gate.
            return string.Equals(assemblySha,
                "D72CD91F98DE4D6CCE8EE420AC1A906D89E68BA0CB164694D266B56F46339EBE",
                StringComparison.OrdinalIgnoreCase);
        }

        private static void ResetGenomeUpgradeUseCache()
        {
            _genomeUpgradeUsesAttempted = false;
            GenomeUpgradeMaterialIds.Clear();
            GenomeUpgradeUseLoggedItems.Clear();
        }

        private static void RefreshGenomeUpgradeUses(bool force)
        {
            if (!ShowMagnumUses || KnownItemIds.Count == 0) return;
            if (_genomeUpgradeUsesAttempted && !force) return;
            _genomeUpgradeUsesAttempted = true;
            GenomeUpgradeMaterialIds.Clear();
            if (!SupportsGenomeUpgradeUseSchema(_compatAssemblySha256))
            {
                LogRuntimeBoundaryWarningOnce("genome-upgrade-use.build",
                    "Genome upgrade-use metadata hidden: unsupported game fingerprint.", null);
                return;
            }
            HashSet<string> materials;
            string reason;
            if (!GenomeUpgradeUseReader.TryRead(typeof(Data), KnownItemIds, out materials, out reason))
            {
                LogRuntimeBoundaryWarningOnce("genome-upgrade-use.schema",
                    "Genome upgrade-use metadata hidden: " + reason, null);
                return;
            }
            GenomeUpgradeMaterialIds.UnionWith(materials);
        }

        private static bool HasGenomeUpgradeUse(string itemId)
        {
            if (!ShowMagnumUses || string.IsNullOrEmpty(itemId) ||
                !KnownItemIds.Contains(itemId)) return false;
            RefreshGenomeUpgradeUses(false);
            return GenomeUpgradeMaterialIds.Contains(itemId);
        }

        private static void LogGenomeUpgradeUse(string itemId)
        {
            if (GenomeUpgradeUseLoggedItems.Count >= 24 ||
                !GenomeUpgradeUseLoggedItems.Add(itemId)) return;
            VerboseLog("[ItemIntelligence][UpgradeUses] item=" + itemId +
                ", use=Genome, source=Data.MagnumProjectPrices/Mercenary/ItemsGrades" +
                ", membership=configured, quantities=not-projected, shipResearchTotal=unchanged.");
        }
    }
}
