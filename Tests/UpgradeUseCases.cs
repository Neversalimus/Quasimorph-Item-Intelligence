namespace ItemIntelligence
{
    public static partial class ModMain
    {
        private static int _upgradeAssertions;
        private static bool ShowMagnumUses = true;
        private static bool ShowFutureMagnumUses = true;
        private static string _compatAssemblySha256;
        private static readonly HashSet<string> KnownItemIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private static readonly List<BrowserLine> BrowserLines = new List<BrowserLine>();
        private static int _researchRows;
        private static int _researchRequired;
        private static int _researchOwned;
        private static int _researchSurplus;
        private static int _boundaryWarnings;

        private static void VerifyUpgrade(bool value, string message)
        {
            _upgradeAssertions++;
            if (!value) throw new Exception("Upgrade-use regression: " + message);
        }
        private static string Ui(string key) { return key; }
        private static void LogRuntimeBoundaryWarningOnce(string key, string message, Exception ex)
        { _boundaryWarnings++; }
        private static void BuildBrowserMagnumResearch(string itemId)
        {
            for (int i = 0; i < _researchRows; i++) BrowserLines.Add(BrowserLine.FullNote("research"));
        }
        private sealed class BrowserLine
        {
            public string Kind, Text;
            public static BrowserLine Section(string text) { return new BrowserLine { Kind = "section", Text = text }; }
            public static BrowserLine FullNote(string text) { return new BrowserLine { Kind = "note", Text = text }; }
        }
        private static UpgradeUseFixtures.MagnumProjectPrice Genome(Dictionary<string, int> grades)
        {
            return new UpgradeUseFixtures.MagnumProjectPrice
            { ProjectType = UpgradeUseFixtures.MagnumProjectType.Mercenary, ItemsGrades = grades };
        }
        private static bool ReadUpgradeFixture(out HashSet<string> materials, out string reason)
        { return GenomeUpgradeUseReader.TryRead(typeof(UpgradeUseFixtures.Data), KnownItemIds, out materials, out reason); }

        public static int RunUpgradeUseCases()
        {
            _upgradeAssertions = 0;
            KnownItemIds.Clear();
            KnownItemIds.UnionWith(new string[] { "resource_a", "resource_b", "medical_by_name_only" });
            Dictionary<string, int> grades = new Dictionary<string, int>
            { { "resource_a", 1000 }, { "resource_b", 5 }, { "unregistered", 3 } };
            UpgradeUseFixtures.Data.MagnumProjectPrices = new List<UpgradeUseFixtures.MagnumProjectPrice> { Genome(grades) };
            HashSet<string> materials;
            string reason;
            VerifyUpgrade(ReadUpgradeFixture(out materials, out reason), "read exact live table shape");
            VerifyUpgrade(materials.Count == 2, "only known registered resources");
            VerifyUpgrade(materials.Contains("resource_a") && materials.Contains("resource_b"), "positive grades imply membership");
            VerifyUpgrade(!materials.Contains("medical_by_name_only"), "no resource-name inference");
            VerifyUpgrade(grades["resource_a"] == 1000 && grades["resource_b"] == 5 && grades.Count == 3,
                "source dictionary remains untouched; grade is not a displayed quantity");
            materials.Clear();
            VerifyUpgrade(grades.Count == 3, "returned set does not own the live dictionary");

            foreach (int grade in new int[] { int.MinValue, -1, 0, 1, 1000, int.MaxValue })
            {
                grades["resource_a"] = grade;
                VerifyUpgrade(ReadUpgradeFixture(out materials, out reason), "valid dictionary remains readable");
                VerifyUpgrade(materials.Contains("resource_a") == (grade > 0), "non-positive grades are not materials");
                VerifyUpgrade(grades["resource_a"] == grade, "no grade normalization/mutation");
            }
            grades["resource_a"] = 1000;
            UpgradeUseFixtures.Data.MagnumProjectPrices.Add(new UpgradeUseFixtures.MagnumProjectPrice
            { ProjectType = UpgradeUseFixtures.MagnumProjectType.RangeWeapon,
                ItemsGrades = new Dictionary<string, int> { { "medical_by_name_only", 1 } } });
            VerifyUpgrade(ReadUpgradeFixture(out materials, out reason) && !materials.Contains("medical_by_name_only"),
                "equipment table is not mislabeled as genome upgrading");
            UpgradeUseFixtures.Data.MagnumProjectPrices.Add(Genome(new Dictionary<string, int>()));
            VerifyUpgrade(!ReadUpgradeFixture(out materials, out reason) && materials.Count == 0,
                "duplicate genome rows fail closed without partial materials");
            UpgradeUseFixtures.Data.MagnumProjectPrices = new List<UpgradeUseFixtures.MagnumProjectPrice>();
            VerifyUpgrade(!ReadUpgradeFixture(out materials, out reason), "empty bootstrap table is unresolved");
            UpgradeUseFixtures.Data.MagnumProjectPrices = null;
            VerifyUpgrade(!ReadUpgradeFixture(out materials, out reason), "missing table is unresolved");
            UpgradeUseFixtures.Data.MagnumProjectPrices = new List<UpgradeUseFixtures.MagnumProjectPrice> { null };
            VerifyUpgrade(!ReadUpgradeFixture(out materials, out reason), "null record fails closed");
            UpgradeUseFixtures.Data.MagnumProjectPrices = new List<UpgradeUseFixtures.MagnumProjectPrice> { Genome(null) };
            VerifyUpgrade(!ReadUpgradeFixture(out materials, out reason), "missing material dictionary fails closed");
            UpgradeUseFixtures.Data.MagnumProjectPrices = new List<UpgradeUseFixtures.MagnumProjectPrice>
            { new UpgradeUseFixtures.DerivedPrice { ProjectType = UpgradeUseFixtures.MagnumProjectType.Mercenary, ItemsGrades = grades } };
            VerifyUpgrade(!ReadUpgradeFixture(out materials, out reason), "unexpected row subtype fails closed");
            UpgradeUseFixtures.Data.MagnumProjectPrices = new List<UpgradeUseFixtures.MagnumProjectPrice>
            { new UpgradeUseFixtures.MagnumProjectPrice { ProjectType = UpgradeUseFixtures.MagnumProjectType.RangeWeapon, ItemsGrades = grades } };
            VerifyUpgrade(!ReadUpgradeFixture(out materials, out reason), "missing genome kind is not a confirmed empty result");
            VerifyUpgrade(!GenomeUpgradeUseReader.TryRead(typeof(UpgradeUseFixtures.WrongShapeData), KnownItemIds,
                out materials, out reason), "changed collection property shape fails closed");
            VerifyUpgrade(!GenomeUpgradeUseReader.TryRead(typeof(UpgradeUseFixtures.ThrowingData), KnownItemIds,
                out materials, out reason) && materials.Count == 0, "getter exception remains isolated");
            VerifyUpgrade(!GenomeUpgradeUseReader.TryRead(null, KnownItemIds, out materials, out reason), "missing owner type");
            VerifyUpgrade(!GenomeUpgradeUseReader.TryRead(typeof(UpgradeUseFixtures.Data), new HashSet<string>(),
                out materials, out reason), "wait for item registry before accepting metadata");
            UpgradeUseFixtures.Data.MagnumProjectPrices = new List<UpgradeUseFixtures.MagnumProjectPrice>();
            for (int i = 0; i <= GenomeUpgradeUseReader.MaximumPriceRows; i++)
                UpgradeUseFixtures.Data.MagnumProjectPrices.Add(Genome(grades));
            VerifyUpgrade(!ReadUpgradeFixture(out materials, out reason), "bounded project table");
            Dictionary<string, int> oversized = new Dictionary<string, int>();
            for (int i = 0; i <= GenomeUpgradeUseReader.MaximumMaterialRows; i++) oversized.Add("r" + i, 1);
            UpgradeUseFixtures.Data.MagnumProjectPrices = new List<UpgradeUseFixtures.MagnumProjectPrice> { Genome(oversized) };
            VerifyUpgrade(!ReadUpgradeFixture(out materials, out reason), "bounded material table");

            _compatAssemblySha256 = "D72CD91F98DE4D6CCE8EE420AC1A906D89E68BA0CB164694D266B56F46339EBE";
            VerifyUpgrade(SupportsGenomeUpgradeUseSchema(_compatAssemblySha256.ToLowerInvariant()), "fingerprint case does not matter");
            foreach (string hash in new string[] { null, "", "unknown", _compatAssemblySha256.Substring(0, 63) + "0" })
                VerifyUpgrade(!SupportsGenomeUpgradeUseSchema(hash), "unknown fingerprints do not acquire this feature gate");
            UpgradeUseFixtures.Data.MagnumProjectPrices = new List<UpgradeUseFixtures.MagnumProjectPrice> { Genome(grades) };
            foreach (int researchRows in new int[] { 0, 1, 5 })
            foreach (int required in new int[] { 0, 17 })
            foreach (bool future in new bool[] { false, true })
            {
                ResetGenomeUpgradeUseCache();
                BrowserLines.Clear();
                ShowMagnumUses = true; ShowFutureMagnumUses = future;
                _researchRows = researchRows; _researchRequired = required;
                _researchOwned = 40; _researchSurplus = 40 - required;
                BuildBrowserMagnum("resource_a");
                VerifyUpgrade(BrowserLines.Count == researchRows + 3, "upgrades survive every research early-return scenario");
                VerifyUpgrade(BrowserLines[researchRows].Text == "ui.upgrade_uses", "separate section after research");
                VerifyUpgrade(BrowserLines[researchRows + 1].Text == "ui.upgrade_genomes", "genome use label");
                VerifyUpgrade(BrowserLines[researchRows + 2].Text == "ui.upgrade_uses_note", "cost exclusion explained");
                VerifyUpgrade(_researchRequired == required && _researchOwned == 40 && _researchSurplus == 40 - required,
                    "research totals/owned/surplus untouched by production decorator");
                VerifyUpgrade(ShowFutureMagnumUses == future, "future research setting untouched");
            }
            BrowserLines.Clear(); _researchRows = 0;
            BuildBrowserMagnum("medical_by_name_only");
            VerifyUpgrade(BrowserLines.Count == 0, "unrelated resource gets no empty upgrade section");
            ShowMagnumUses = false;
            BuildBrowserMagnum("resource_a");
            VerifyUpgrade(BrowserLines.Count == 0 && !HasGenomeUpgradeUse("resource_a"), "disabled information stays hidden");
            ShowMagnumUses = true;
            VerifyUpgrade(!HasGenomeUpgradeUse(null) && !HasGenomeUpgradeUse(""), "empty resource identity");
            // Force-refresh on tab render sees in-place data changes, not just table replacement.
            grades.Remove("resource_a");
            BuildBrowserMagnum("resource_a");
            VerifyUpgrade(BrowserLines.Count == 0, "changed live material set drops stale use");
            grades.Add("resource_a", 1000);
            BuildBrowserMagnum("resource_a");
            VerifyUpgrade(BrowserLines.Count == 3, "new live association appears without restarting");
            BrowserLines.Clear(); UpgradeUseFixtures.Data.MagnumProjectPrices = null;
            BuildBrowserMagnum("resource_a");
            VerifyUpgrade(BrowserLines.Count == 0 && !HasGenomeUpgradeUse("resource_a") && _boundaryWarnings > 0,
                "failed refresh clears stale material cache and reports reason");
            ResetGenomeUpgradeUseCache();
            VerifyUpgrade(GenomeUpgradeMaterialIds.Count == 0 && GenomeUpgradeUseLoggedItems.Count == 0 &&
                !_genomeUpgradeUsesAttempted, "menu/index reset clears all feature-owned state");
            return _upgradeAssertions;
        }
    }
}
namespace UpgradeUseFixtures
{
    public enum MagnumProjectType { None, Mercenary, MercenaryClass, RangeWeapon }
    public class MagnumProjectPrice
    {
        public MagnumProjectType ProjectType { get; set; }
        public Dictionary<string, int> ItemsGrades { get; set; }
    }
    public sealed class DerivedPrice : MagnumProjectPrice { }
    public static class Data { public static List<MagnumProjectPrice> MagnumProjectPrices { get; set; } }
    public static class WrongShapeData { public static string MagnumProjectPrices { get { return "wrong"; } } }
    public static class ThrowingData
    {
        public static List<MagnumProjectPrice> MagnumProjectPrices { get { throw new InvalidOperationException("fixture getter"); } }
    }
}
