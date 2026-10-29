using System;
using System.Globalization;
using MGSC;

namespace ItemIntelligence
{
    public static partial class ModMain
    {
        private static readonly ItemDecayIndex ItemDecay = new ItemDecayIndex();

        private static void ResetItemDecayIndex()
        {
            ItemDecay.Clear();
        }

        private static void BuildItemDecayIndex()
        {
            ResetItemDecayIndex();
            try
            {
                if (Data.ItemExpire == null) return;
                foreach (ItemExpireRecord row in Data.ItemExpire.Records)
                {
                    if (row == null || string.IsNullOrEmpty(row.Id) || !KnownItemIds.Contains(row.Id)) continue;
                    object itemRecord;
                    if (!ItemRecordsById.TryGetValue(row.Id, out itemRecord) || !IsExpirableItemRecord(itemRecord)) continue;
                    if (!string.IsNullOrEmpty(row.ConvertedItemId) && !KnownItemIds.Contains(row.ConvertedItemId)) continue;
                    ItemDecay.Add(row.Id, row.ConvertedItemId, row.ExpiresAfterHours);
                }
                VerboseLog("[ItemIntelligence] Item spoilage links ready: " +
                    ItemDecay.Count.ToString(CultureInfo.InvariantCulture) + ".");
            }
            catch
            {
                // A changed optional table must not leave a partially built graph.
                ResetItemDecayIndex();
                throw;
            }
        }

        private static bool IsExpirableItemRecord(object itemRecord)
        {
            // ItemFactory.CreateComponent adds an active ExpireComponent only to
            // IExpirableRecord components with a matching Data.ItemExpire entry.
            if (itemRecord is IExpirableRecord) return true;
            CompositeItemRecord composite = itemRecord as CompositeItemRecord;
            if (composite == null || composite.Records == null) return false;
            foreach (BasePickupItemRecord component in composite.Records)
                if (component is IExpirableRecord) return true;
            return false;
        }

        private static int GetItemDecayRelationCount(string itemId)
        {
            ItemDecayRelation result;
            return (ItemDecay.TryGetResult(itemId, out result) ? 1 : 0) + ItemDecay.GetSourceCount(itemId);
        }

        private static bool AppendItemDecayRelations(string itemId)
        {
            ItemDecayRelation result;
            bool hasResult = ItemDecay.TryGetResult(itemId, out result);
            ItemDecayRelation[] sources = ItemDecay.GetSources(itemId);
            if (!hasResult && sources.Length == 0) return false;

            if (hasResult)
            {
                BrowserLines.Add(BrowserLine.Section(Ui("ui.decay_result")));
                string duration = FormatItemDecayShelfLife(result);
                BrowserLines.Add(string.IsNullOrEmpty(result.ResultItemId)
                    ? BrowserLine.Normal(Ui("ui.decay_disappears"), duration)
                    : BrowserLine.ItemAction(result.ResultItemId, duration));
            }
            if (sources.Length > 0)
            {
                Array.Sort(sources, delegate(ItemDecayRelation a, ItemDecayRelation b)
                {
                    int nameOrder = string.Compare(LocalizeItem(a.SourceItemId), LocalizeItem(b.SourceItemId),
                        StringComparison.CurrentCultureIgnoreCase);
                    return nameOrder != 0 ? nameOrder : string.Compare(a.SourceItemId, b.SourceItemId, StringComparison.Ordinal);
                });
                BrowserLines.Add(BrowserLine.Section(Ui("ui.decay_sources")));
                foreach (ItemDecayRelation source in sources)
                    BrowserLines.Add(BrowserLine.ItemAction(source.SourceItemId, FormatItemDecayShelfLife(source)));
            }
            AddWrappedBrowserNote("ui.decay_shelf_life_note", 110, 120);
            return true;
        }

        private static string FormatItemDecayShelfLife(ItemDecayRelation relation)
        {
            return relation.ShelfLifeHours.ToString("0.##", GetUiNumberCulture()) + " " + Ui("ui.unit_hour_short");
        }
    }
}
