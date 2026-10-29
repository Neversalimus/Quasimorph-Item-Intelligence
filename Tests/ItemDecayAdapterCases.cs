namespace MGSC
{
    public interface IExpirableRecord { }
    public class BasePickupItemRecord { }
    public sealed class FoodRecord : BasePickupItemRecord, IExpirableRecord { }
    public sealed class CompositeItemRecord : BasePickupItemRecord
    {
        public System.Collections.Generic.List<BasePickupItemRecord> Records = new System.Collections.Generic.List<BasePickupItemRecord>();
    }
    public sealed class ItemExpireRecord
    {
        public string Id, ConvertedItemId;
        public float ExpiresAfterHours;
    }
    public sealed class ExpireTable
    {
        public System.Collections.Generic.IEnumerable<ItemExpireRecord> Records;
    }
    public static class Data { public static ExpireTable ItemExpire; }
}
namespace ItemIntelligence
{
    public static partial class ModMain
    {
        private static readonly System.Collections.Generic.HashSet<string> KnownItemIds =
            new System.Collections.Generic.HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
        private static readonly System.Collections.Generic.Dictionary<string, object> ItemRecordsById =
            new System.Collections.Generic.Dictionary<string, object>(System.StringComparer.OrdinalIgnoreCase);
        private static readonly System.Collections.Generic.Dictionary<string, string> Names = new System.Collections.Generic.Dictionary<string, string>();
        private static readonly System.Collections.Generic.List<BrowserLine> BrowserLines = new System.Collections.Generic.List<BrowserLine>();
        private static System.Globalization.CultureInfo numberCulture;
        private static int assertions;
        private static string LocalizeItem(string id) { string name; return Names.TryGetValue(id, out name) ? name : id; }
        private static string Ui(string key) { return key == "ui.unit_hour_short" ? "h" : key; }
        private static System.Globalization.CultureInfo GetUiNumberCulture() { return numberCulture; }
        private static void VerboseLog(string message) { }
        private static void AddWrappedBrowserNote(string key, int ru, int en) { BrowserLines.Add(BrowserLine.FullNote(key)); }
        private static void Check(bool value, string message)
        {
            assertions++;
            if (!value) throw new System.Exception("Item spoilage adapter regression: " + message);
        }
        private static MGSC.ItemExpireRecord Expiry(string source, string result, float hours)
        {
            return new MGSC.ItemExpireRecord { Id = source, ConvertedItemId = result, ExpiresAfterHours = hours };
        }
        private static void AddItem(string id, object record)
        {
            KnownItemIds.Add(id); ItemRecordsById[id] = record;
        }
        private static System.Collections.Generic.IEnumerable<MGSC.ItemExpireRecord> BrokenTable()
        {
            yield return Expiry("fresh", "rotten", 48);
            throw new System.InvalidOperationException("changed table");
        }
        public static int RunItemDecayAdapterCases()
        {
            assertions = 0;
            KnownItemIds.Clear(); ItemRecordsById.Clear(); BrowserLines.Clear(); Names.Clear(); ResetItemDecayIndex();
            numberCulture = System.Globalization.CultureInfo.InvariantCulture;
            AddItem("fresh", new MGSC.FoodRecord());
            var cooked = new MGSC.CompositeItemRecord();
            cooked.Records.Add(new MGSC.BasePickupItemRecord()); cooked.Records.Add(new MGSC.FoodRecord());
            AddItem("cooked", cooked);
            AddItem("rotten", new MGSC.FoodRecord());
            AddItem("kit", new MGSC.FoodRecord());
            AddItem("vanishes", new MGSC.FoodRecord());
            AddItem("non_expirable", new MGSC.BasePickupItemRecord());
            AddItem("unknown_result", new MGSC.FoodRecord());
            AddItem("invalid_time", new MGSC.FoodRecord());
            KnownItemIds.Add("missing_record");
            Data.ItemExpire = new MGSC.ExpireTable { Records = new[] {
                Expiry("fresh", "rotten", 48), Expiry("cooked", "rotten", 96), Expiry("kit", "fresh", 504),
                Expiry("vanishes", "", 24), Expiry("unknown_source", "rotten", 2),
                Expiry("non_expirable", "rotten", 2), Expiry("unknown_result", "missing", 2),
                Expiry("invalid_time", "rotten", float.NaN), Expiry("missing_record", "rotten", 1), null } };
            BuildItemDecayIndex();
            Check(ItemDecay.Count == 4, "only known expirable records with valid output and time indexed");
            Check(GetItemDecayRelationCount("rotten") == 2 && GetItemDecayRelationCount("fresh") == 2,
                "summary counts both directions without following chain");
            Check(GetItemDecayRelationCount("fresh_custom") == 0, "variant does not inherit an unverified expiry rule");
            Check(!IsExpirableItemRecord(null) && !IsExpirableItemRecord(new MGSC.CompositeItemRecord()), "empty composites rejected");
            BrowserLines.Clear();
            Check(AppendItemDecayRelations("fresh"), "perishable page has relations");
            Check(BrowserLines.Count == 5 && BrowserLines[0].Left == "ui.decay_result" && BrowserLines[2].Left == "ui.decay_sources",
                "intermediate item shows its result and its source");
            Check(BrowserLines[1].Action.Kind == BrowserActionKind.OpenItem && BrowserLines[1].Action.Payload == "rotten" &&
                BrowserLines[1].Right == "48 h", "result opens exact item and shows shelf life");
            Check(BrowserLines[3].Action.Kind == BrowserActionKind.OpenItem && BrowserLines[3].Action.Payload == "kit" &&
                BrowserLines[3].Right == "504 h", "source opens exact item with its own shelf life");
            Check(BrowserLines[4].RowKind == BrowserRowKind.FullNote && BrowserLines[4].Left == "ui.decay_shelf_life_note",
                "shelf life and refrigeration explanation appears once");
            BrowserLines.Clear(); Names["fresh"] = "Z"; Names["cooked"] = "A";
            AppendItemDecayRelations("rotten");
            Check(BrowserLines.Count == 4 && BrowserLines[1].Left == "cooked" && BrowserLines[2].Left == "fresh",
                "all direct sources sorted by localized names");
            BrowserLines.Clear(); Names["fresh"] = "A"; Names["cooked"] = "Z";
            AppendItemDecayRelations("rotten");
            Check(BrowserLines[1].Left == "fresh", "language change updates source order");
            BrowserLines.Clear(); AppendItemDecayRelations("vanishes");
            Check(BrowserLines.Count == 3 && BrowserLines[1].Left == "ui.decay_disappears" && BrowserLines[1].Action.IsNone,
                "disappearance has no fake item link");
            BrowserLines.Clear();
            Check(!AppendItemDecayRelations("no_expiry") && BrowserLines.Count == 0, "unrelated item has no empty sections");
            numberCulture = System.Globalization.CultureInfo.GetCultureInfo("de-DE");
            Check(FormatItemDecayShelfLife(new ItemDecayRelation("a", "b", 1.5f)) == "1,5 h", "localized fractional hours");
            Data.ItemExpire = new MGSC.ExpireTable { Records = BrokenTable() };
            bool failed = false;
            try { BuildItemDecayIndex(); } catch (System.InvalidOperationException) { failed = true; }
            Check(failed && ItemDecay.Count == 0 && GetItemDecayRelationCount("rotten") == 0, "failed build clears partial graph");
            Data.ItemExpire = null; BuildItemDecayIndex();
            Check(ItemDecay.Count == 0, "missing optional table leaves feature empty");
            ItemDecay.Add("old", "rotten", 1); ResetItemDecayIndex();
            Check(ItemDecay.Count == 0 && GetItemDecayRelationCount("rotten") == 0, "session reset clears both directions");
            return assertions;
        }
    }
}
