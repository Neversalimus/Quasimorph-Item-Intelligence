using System;
using System.Collections.Generic;
using MGSC;

namespace ItemIntelligence
{
    internal sealed class UpgradeUse
    {
        internal readonly string Kind;
        internal readonly string TargetItemId;
        internal UpgradeUse(string kind, string target) { Kind = kind; TargetItemId = target; }
    }

    // ItemsGrades values are price denominations, NOT quantities. Equipment uses
    // the receipt override when nonempty, exactly as MagnumProject.InitRecord does.
    internal static class UpgradeUseReader
    {
        internal const int MaximumPriceRows = 64;
        internal const int MaximumReceiptRows = 4096;
        internal const int MaximumMaterialRows = 256;

        internal static bool TryRead(IList<MagnumProjectPrice> prices,
            IList<ItemProduceReceipt> receipts, ISet<string> knownItems,
            Func<string, MagnumProjectType> equipmentKind,
            out Dictionary<string, List<UpgradeUse>> materials, out string reason)
        {
            materials = new Dictionary<string, List<UpgradeUse>>(StringComparer.OrdinalIgnoreCase);
            reason = "upgrade data not ready";
            if (prices == null || receipts == null || knownItems == null || knownItems.Count == 0 ||
                equipmentKind == null || prices.Count == 0 || receipts.Count == 0) return false;
            try
            {
                if (prices.Count > MaximumPriceRows || receipts.Count > MaximumReceiptRows)
                    throw new InvalidOperationException("upgrade table exceeds bounded reader");
                var defaults = new Dictionary<MagnumProjectType, Dictionary<string, int>>();
                foreach (MagnumProjectPrice row in prices)
                {
                    if (row == null) throw new InvalidOperationException("invalid project price row");
                    if (defaults.ContainsKey(row.ProjectType))
                        throw new InvalidOperationException("ambiguous duplicate project price rows");
                    ValidateGrades(row.ItemsGrades);
                    defaults.Add(row.ProjectType, row.ItemsGrades);
                    if (row.ProjectType == MagnumProjectType.Mercenary ||
                        row.ProjectType == MagnumProjectType.MercenaryClass)
                        AddGrades(materials, knownItems, row.ItemsGrades, row.ProjectType.ToString(), string.Empty);
                }
                var targets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (ItemProduceReceipt receipt in receipts)
                {
                    if (receipt == null || string.IsNullOrEmpty(receipt.OutputItem))
                        throw new InvalidOperationException("invalid upgrade receipt");
                    if (!targets.Add(receipt.OutputItem))
                        throw new InvalidOperationException("ambiguous duplicate upgrade receipts");
                    if (!knownItems.Contains(receipt.OutputItem)) continue;
                    MagnumProjectType kind = equipmentKind(receipt.OutputItem);
                    string category = EquipmentCategory(kind);
                    if (category.Length == 0) continue;
                    Dictionary<string, int> grades;
                    if (!defaults.TryGetValue(kind, out grades))
                        throw new InvalidOperationException("equipment price row unavailable: " + kind);
                    ValidateGrades(receipt.ModifyItemsGrades);
                    if (receipt.ModifyItemsGrades.Count > 0) grades = receipt.ModifyItemsGrades;
                    AddGrades(materials, knownItems, grades, category, receipt.OutputItem);
                }
                reason = "configured upgrade material membership";
                return true;
            }
            catch (Exception ex)
            {
                materials.Clear();
                reason = ex.ToString();
                return false;
            }
        }

        private static string EquipmentCategory(MagnumProjectType kind)
        {
            switch (kind)
            {
                case MagnumProjectType.RangeWeapon: return "RangeWeapon";
                case MagnumProjectType.MeleeWeapon: return "MeleeWeapon";
                case MagnumProjectType.Helmet:
                case MagnumProjectType.Armor:
                case MagnumProjectType.Leggings:
                case MagnumProjectType.Boots: return "Armor";
                default: return string.Empty;
            }
        }

        private static void ValidateGrades(Dictionary<string, int> grades)
        {
            if (grades == null || grades.Count > MaximumMaterialRows)
                throw new InvalidOperationException("upgrade material table unavailable or oversized");
        }

        private static void AddGrades(Dictionary<string, List<UpgradeUse>> materials, ISet<string> knownItems,
            Dictionary<string, int> grades, string kind, string target)
        {
            foreach (KeyValuePair<string, int> entry in grades)
                if (entry.Value > 0 && !string.IsNullOrEmpty(entry.Key) && knownItems.Contains(entry.Key))
                    AddUse(materials, entry.Key, kind, target);
        }

        internal static void AddUse(Dictionary<string, List<UpgradeUse>> materials,
            string itemId, string kind, string target)
        {
            List<UpgradeUse> uses;
            if (!materials.TryGetValue(itemId, out uses))
                materials.Add(itemId, uses = new List<UpgradeUse>());
            foreach (UpgradeUse use in uses)
                if (use.Kind == kind && string.Equals(use.TargetItemId, target, StringComparison.OrdinalIgnoreCase)) return;
            uses.Add(new UpgradeUse(kind, target));
        }
    }
}
