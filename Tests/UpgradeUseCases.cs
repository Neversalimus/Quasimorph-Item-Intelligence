namespace ItemIntelligence
{
    public static partial class ModMain
    {
        private static int _upgradeAssertions, _researchRows, _boundaryWarnings;
        private static bool ShowMagnumUses = true;
        private static bool ShowFutureMagnumUses = true;
        private static string _compatAssemblySha256;
        private static readonly HashSet<string> KnownItemIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private static readonly List<BrowserLine> BrowserLines = new List<BrowserLine>();
        private static void VerifyUpgrade(bool value, string message)
        { _upgradeAssertions++; if (!value) throw new Exception("Upgrade-use regression: " + message); }
        private static string Ui(string key) { return key; }
        private static void LogRuntimeBoundaryWarningOnce(string key, string message, Exception ex) { _boundaryWarnings++; }
        private static void BuildBrowserMagnumResearch(string itemId)
        { for (int i = 0; i < _researchRows; i++) BrowserLines.Add(BrowserLine.FullNote("research")); }
        private enum BrowserTabId { Magnum }
        private sealed class BrowserAction
        { public static BrowserAction SwitchTab(BrowserTabId tab) { return new BrowserAction(); } }
        private sealed class BrowserLine
        {
            public string Kind, Text;
            public static BrowserLine Section(string text) { return new BrowserLine { Kind = "section", Text = text }; }
            public static BrowserLine FullNote(string text) { return new BrowserLine { Kind = "note", Text = text }; }
            public static BrowserLine Item(string id, string right) { return new BrowserLine { Kind = "item", Text = id }; }
            public static BrowserLine InternalAction(string text, BrowserAction action)
            { return new BrowserLine { Kind = "action", Text = text }; }
        }
        private static Dictionary<string, int> Grades(string item, int grade = 1)
        { return new Dictionary<string, int> { { item, grade } }; }
        private static MagnumProjectPrice Price(MagnumProjectType kind, Dictionary<string, int> grades)
        { return new MagnumProjectPrice { ProjectType = kind, ItemsGrades = grades }; }
        private static ItemProduceReceipt Receipt(string target, Dictionary<string, int> grades = null)
        { return new ItemProduceReceipt { OutputItem = target, ModifyItemsGrades = grades ?? new Dictionary<string, int>() }; }
        private static bool Read(out Dictionary<string, List<UpgradeUse>> materials)
        {
            string reason;
            return UpgradeUseReader.TryRead(Data.MagnumProjectPrices, Data.ProduceReceipts, KnownItemIds,
                ResolveUpgradeEquipmentKind, out materials, out reason);
        }
        private static void Setup()
        {
            KnownItemIds.Clear();
            KnownItemIds.UnionWith(new [] { "resource", "override", "unused", "gun", "sword", "helmet", "armor", "pants", "boots", "food", "pact", "implant", "arm" });
            Data.Items = new ItemsCollection();
            Data.Items.Add("gun", new WeaponRecord()); Data.Items.Add("sword", new WeaponRecord { IsMelee = true });
            Data.Items.Add("helmet", new HelmetRecord()); Data.Items.Add("armor", new ArmorRecord());
            Data.Items.Add("pants", new LeggingsRecord()); Data.Items.Add("boots", new BootsRecord());
            Data.Items.Add("pact", new PactComponentRecord()); Data.Items.Add("implant", new ImplantRecord());
            Data.Items.Add("arm", new AugmentationRecord());
            Data.MagnumProjectPrices = new List<MagnumProjectPrice> {
                Price(MagnumProjectType.RangeWeapon, Grades("resource")), Price(MagnumProjectType.MeleeWeapon, Grades("resource")),
                Price(MagnumProjectType.Helmet, Grades("resource")), Price(MagnumProjectType.Armor, Grades("resource")),
                Price(MagnumProjectType.Leggings, Grades("resource")), Price(MagnumProjectType.Boots, Grades("resource")),
                Price(MagnumProjectType.Mercenary, Grades("resource", 12)), Price(MagnumProjectType.MercenaryClass, Grades("resource", 21)) };
            Data.ProduceReceipts = new List<ItemProduceReceipt> {
                Receipt("gun"), Receipt("sword"), Receipt("helmet"), Receipt("armor"), Receipt("pants"), Receipt("boots"),
                Receipt("food", Grades("unused")), Receipt("unregistered_target", Grades("unused")) };
            ShowMagnumUses = true; ShowFutureMagnumUses = true; _researchRows = 0;
            _compatAssemblySha256 = "D72CD91F98DE4D6CCE8EE420AC1A906D89E68BA0CB164694D266B56F46339EBE";
            ResetUpgradeUseCache(); BrowserLines.Clear();
        }
        public static int RunUpgradeUseCases()
        {
            _upgradeAssertions = 0; Setup();
            Dictionary<string, List<UpgradeUse>> result;
            VerifyUpgrade(Read(out result) && result.Count == 1 && result["resource"].Count == 8, "all eight project types");
            var categories = new HashSet<string>();
            foreach (var use in result["resource"]) categories.Add(use.Kind);
            VerifyUpgrade(categories.SetEquals(new [] { "RangeWeapon", "MeleeWeapon", "Armor", "Mercenary", "MercenaryClass" }), "armor slots grouped without losing targets");
            VerifyUpgrade(!result.ContainsKey("unused"), "non-equipment and unregistered targets excluded");
            Data.ProduceReceipts[0].ModifyItemsGrades = Grades("override", 1000);
            VerifyUpgrade(Read(out result) && result["override"].Count == 1 && result["resource"].Count == 7, "receipt replaces standard cost table");
            VerifyUpgrade(result["override"][0].TargetItemId == "gun" && result["override"][0].Kind == "RangeWeapon", "exact clickable target");
            result.Clear();
            VerifyUpgrade(Data.ProduceReceipts[0].ModifyItemsGrades["override"] == 1000, "snapshot cannot mutate source grades");
            foreach (int grade in new [] { int.MinValue, -1, 0, 1, 1000, int.MaxValue })
            {
                Data.ProduceReceipts[0].ModifyItemsGrades["override"] = grade;
                VerifyUpgrade(Read(out result) && result.ContainsKey("override") == (grade > 0), "positive denominations only");
                VerifyUpgrade(result["resource"].Count == 7, "nonpositive override never falls back to defaults");
                VerifyUpgrade(Data.ProduceReceipts[0].ModifyItemsGrades["override"] == grade, "no live grade normalization");
            }
            Data.ProduceReceipts[0].ModifyItemsGrades = Grades("unregistered_material");
            VerifyUpgrade(Read(out result) && !result.ContainsKey("unregistered_material"), "registered materials only");
            Data.ProduceReceipts[0].ModifyItemsGrades.Clear();
            VerifyUpgrade(Read(out result) && result["resource"].Count == 8, "empty override uses defaults");
            UpgradeUseReader.AddUse(result, "resource", "Armor", "helmet");
            VerifyUpgrade(result["resource"].Count == 8, "duplicate links suppressed");
            Data.MagnumProjectPrices[0].ItemsGrades = Grades("unused");
            Data.ProduceReceipts[0].ModifyItemsGrades = Grades("override");
            VerifyUpgrade(Read(out result) && !result.ContainsKey("unused"), "unused global fallback is not an actual use");
            Setup(); Data.MagnumProjectPrices.Add(Data.MagnumProjectPrices[0]);
            VerifyUpgrade(!Read(out result) && result.Count == 0, "duplicate project rows fail without partial data");
            Setup(); Data.ProduceReceipts.Add(Receipt("gun"));
            VerifyUpgrade(!Read(out result) && result.Count == 0, "duplicate equipment rows fail closed");
            Setup(); Data.MagnumProjectPrices[0] = null;
            VerifyUpgrade(!Read(out result) && result.Count == 0, "null price row");
            Setup(); Data.ProduceReceipts[0] = null;
            VerifyUpgrade(!Read(out result) && result.Count == 0, "null receipt");
            Setup(); Data.ProduceReceipts[0].OutputItem = null;
            VerifyUpgrade(!Read(out result), "missing output identity");
            Setup(); Data.ProduceReceipts[0].ModifyItemsGrades = null;
            VerifyUpgrade(!Read(out result) && result.Count == 0, "missing override table");
            Setup(); Data.MagnumProjectPrices[0].ItemsGrades = null;
            VerifyUpgrade(!Read(out result), "missing default table");
            Setup(); Data.MagnumProjectPrices.RemoveAt(0);
            VerifyUpgrade(!Read(out result), "missing equipment default price row");
            Setup(); Data.MagnumProjectPrices = null;
            VerifyUpgrade(!Read(out result), "bootstrap price table missing");
            Setup(); Data.ProduceReceipts.Clear();
            VerifyUpgrade(!Read(out result), "bootstrap receipts empty");
            Setup(); KnownItemIds.Clear();
            VerifyUpgrade(!Read(out result), "bootstrap registry empty");
            Setup();
            for (int i = 0; i < UpgradeUseReader.MaximumPriceRows; i++) Data.MagnumProjectPrices.Add(Data.MagnumProjectPrices[0]);
            VerifyUpgrade(!Read(out result), "price bound");
            Setup();
            for (int i = 0; i < UpgradeUseReader.MaximumReceiptRows; i++) Data.ProduceReceipts.Add(Receipt("extra" + i));
            VerifyUpgrade(!Read(out result), "receipt bound");
            Setup();
            for (int i = 0; i <= UpgradeUseReader.MaximumMaterialRows; i++) Data.ProduceReceipts[0].ModifyItemsGrades.Add("m" + i, 1);
            VerifyUpgrade(!Read(out result), "material bound");
            Setup(); Data.Items.ThrowOnRead = true;
            VerifyUpgrade(!Read(out result) && result.Count == 0, "record getter failure clears partial data");
            Setup();
            VerifyUpgrade(SupportsUpgradeUseSchema(_compatAssemblySha256.ToLowerInvariant()), "case-insensitive fingerprint");
            foreach (string hash in new [] { null, "", "unknown" }) VerifyUpgrade(!SupportsUpgradeUseSchema(hash), "unknown build rejected");
            foreach (int researchRows in new [] { 0, 1, 5 })
            foreach (bool future in new [] { false, true })
            {
                Setup(); _researchRows = researchRows; ShowFutureMagnumUses = future;
                BuildBrowserMagnum("resource");
                VerifyUpgrade(BrowserLines.Count == researchRows + 14, "categories and targets after every research result");
                VerifyUpgrade(BrowserLines[researchRows].Text == "ui.upgrade_uses", "separate section after research");
                VerifyUpgrade(ShowFutureMagnumUses == future, "research preference remains unchanged");
                int links = 0; foreach (var line in BrowserLines) if (line.Kind == "item") links++;
                VerifyUpgrade(links == 6, "six targets; denominations are not quantities");
                BrowserLines.Clear(); AppendOverviewUpgradeUses("resource");
                VerifyUpgrade(BrowserLines.Count == 6 && BrowserLines[1].Kind == "action", "overview links to Magnum even after completed research");
            }
            Setup();
            foreach (string id in new [] { "pact", "implant", "arm" })
            {
                BrowserLines.Clear(); BuildBrowserMagnum(id);
                VerifyUpgrade(BrowserLines.Count == 3, "consumed clone/pact item has dedicated use");
                VerifyUpgrade(BrowserLines[1].Text == (id == "pact" ? "ui.upgrade_pacts" : id == "implant" ? "ui.upgrade_implants" : "ui.upgrade_augmentations"), "operation label");
            }
            BrowserLines.Clear(); BuildBrowserMagnum("unused");
            VerifyUpgrade(BrowserLines.Count == 0, "no invented uses from names");
            ShowMagnumUses = false;
            BuildBrowserMagnum("resource"); AppendOverviewUpgradeUses("resource");
            VerifyUpgrade(BrowserLines.Count == 0 && !HasUpgradeUse("resource"), "visibility applies everywhere");
            ShowMagnumUses = true;
            VerifyUpgrade(!HasUpgradeUse(null) && !HasUpgradeUse(""), "empty identities");
            Data.ProduceReceipts[0].ModifyItemsGrades = Grades("override");
            BuildBrowserMagnum("override");
            VerifyUpgrade(BrowserLines.Count == 5, "in-place live override appears");
            BrowserLines.Clear(); Data.ProduceReceipts[0].ModifyItemsGrades.Clear(); BuildBrowserMagnum("override");
            VerifyUpgrade(BrowserLines.Count == 0 && !HasUpgradeUse("override"), "stale override removed");
            Data.MagnumProjectPrices = null; BuildBrowserMagnum("resource");
            VerifyUpgrade(BrowserLines.Count == 0 && !HasUpgradeUse("resource") && _boundaryWarnings > 0, "failed refresh clears cache");
            ResetUpgradeUseCache();
            VerifyUpgrade(UpgradeMaterials.Count == 0 && UpgradeUseLoggedItems.Count == 0 && !_upgradeUsesAttempted, "session reset clears feature state");
            return _upgradeAssertions;
        }
    }
}
namespace UpgradeUseFixtures
{
    public enum MagnumProjectType { None, Weapons, RangeWeapon, MeleeWeapon, Armors, Armor, Helmet, Boots, Leggings, Mercenary, MercenaryClass, QuasiPact, Augmentic }
    public class MagnumProjectPrice { public MagnumProjectType ProjectType { get; set; } public Dictionary<string, int> ItemsGrades { get; set; } }
    public class ItemProduceReceipt { public string OutputItem { get; set; } public Dictionary<string, int> ModifyItemsGrades { get; set; } }
    public class WeaponRecord { public bool IsMelee { get; set; } }
    public class ArmorRecord { } public class HelmetRecord { } public class LeggingsRecord { } public class BootsRecord { }
    public class PactComponentRecord { } public class ImplantRecord { } public class AugmentationRecord { }
    public class ItemsCollection
    {
        private Dictionary<string, List<object>> records = new Dictionary<string, List<object>>();
        public bool ThrowOnRead;
        public void Add(string id, object record)
        { if (!records.ContainsKey(id)) records.Add(id, new List<object>()); records[id].Add(record); }
        public T GetSimpleRecord<T>(string id, bool optional) where T : class
        {
            if (ThrowOnRead) throw new InvalidOperationException("fixture getter");
            if (records.ContainsKey(id)) foreach (object row in records[id]) if (row is T) return (T)row;
            return null;
        }
    }
    public static class Data
    {
        public static ItemsCollection Items;
        public static List<MagnumProjectPrice> MagnumProjectPrices;
        public static List<ItemProduceReceipt> ProduceReceipts;
    }
}
