namespace MGSC
{
    public sealed class ItemStorage
    {
        public readonly List<KeyValuePair<string, int>> Items = new List<KeyValuePair<string, int>>();
        public bool ThrowOnRead;
        public int Reads;
        public int CountItems(string id)
        {
            Reads++;
            if (ThrowOnRead) throw new InvalidOperationException("unavailable storage");
            int total = 0;
            foreach (var item in Items) if (item.Key == id) total += item.Value;
            return total;
        }
    }
    public sealed class MagnumCargo
    {
        public List<ItemStorage> ShipCargo = new List<ItemStorage>();
        public ItemStorage FridgeStorage;
        public ItemStorage RecyclingStorage;
        public bool RecyclingInProgress;
    }
    public sealed class State
    {
        public MagnumCargo Cargo;
        public readonly ItemStorage CloneInventory = new ItemStorage();
        public int Reads;
        public T Get<T>() where T : class { Reads++; return Cargo as T; }
    }
    public sealed class ModContext
    {
        public State Current;
        public bool ThrowOnRead;
        public State State { get { if (ThrowOnRead) throw new InvalidOperationException(); return Current; } }
    }
}
namespace UnityEngine
{
    public static class Time { public static float realtimeSinceStartup; }
}
namespace ItemIntelligence
{
    public static partial class ModMain
    {
        private static int _cargoAssertions;
        private static void CheckCargo(bool value, string name)
        {
            _cargoAssertions++;
            if (!value) throw new Exception("Ship cargo: " + name);
        }
        private static ItemStorage Storage(string id, int count)
        {
            var storage = new ItemStorage();
            storage.Items.Add(new KeyValuePair<string, int>(id, count));
            return storage;
        }
        private static void ExpectCount(MagnumCargo cargo, string id, int expected, string name)
        {
            int count;
            CheckCargo(ShipCargoReader.TryGetCount(cargo, id, out count) && count == expected, name);
        }
        private static void ExpectUnknown(MagnumCargo cargo, string id, string name)
        {
            int count;
            CheckCargo(!ShipCargoReader.TryGetCount(cargo, id, out count) && count == 0, name);
        }
        public static int RunShipCargoCases()
        {
            var cargo = new MagnumCargo();
            ExpectCount(cargo, "food", 0, "empty warehouse is a known zero");
            ExpectUnknown(null, "food", "missing cargo is unknown");
            ExpectUnknown(new MagnumCargo { ShipCargo = null }, "food", "missing cargo list is unknown");
            ExpectUnknown(cargo, null, "missing ID");
            ExpectUnknown(cargo, "", "empty ID");
            var first = Storage("food", 12);
            first.Items.Add(new KeyValuePair<string, int>("food", 7));
            first.Items.Add(new KeyValuePair<string, int>("Food", 99));
            first.Items.Add(new KeyValuePair<string, int>("food_custom1", 3));
            first.Items.Add(new KeyValuePair<string, int>("ammo", 60));
            var second = Storage("food", 4);
            cargo.ShipCargo.Add(first);
            cargo.ShipCargo.Add(second);
            ExpectCount(cargo, "food", 23, "sum stacks across cargo tabs");
            ExpectCount(cargo, "Food", 99, "native case-sensitive ID");
            ExpectCount(cargo, "food_custom1", 3, "upgraded ID stays separate");
            ExpectCount(cargo, "food_custom2", 0, "missing upgraded ID does not inherit base count");
            ExpectCount(cargo, "ammo", 60, "count ammunition units, not stacks");
            ExpectCount(cargo, "absent", 0, "unrelated cargo is zero");
            cargo.FridgeStorage = Storage("food", 5);
            cargo.RecyclingStorage = Storage("food", 2);
            ExpectCount(cargo, "food", 30, "include fridge and idle recycler");
            cargo.RecyclingInProgress = true;
            ExpectCount(cargo, "food", 28, "active recycling is no longer available");
            cargo.RecyclingInProgress = false;
            cargo.ShipCargo.Add(first);
            ExpectCount(cargo, "food", 30, "duplicate cargo reference counted once");
            cargo.FridgeStorage = first;
            cargo.RecyclingStorage = first;
            ExpectCount(cargo, "food", 23, "same storage across categories counted once");
            cargo.FridgeStorage = Storage("food", 6);
            cargo.RecyclingStorage = cargo.FridgeStorage;
            ExpectCount(cargo, "food", 29, "fridge and recycler alias counted once");
            first.Items.Clear();
            ExpectCount(cargo, "food", 10, "read changed contents without count cache");
            second.ThrowOnRead = true;
            ExpectUnknown(cargo, "food", "failed storage must not show a partial total");
            second.ThrowOnRead = false;
            ExpectCount(cargo, "food", 10, "recover after failed read");
            cargo.ShipCargo.Add(null);
            ExpectUnknown(cargo, "food", "incomplete cargo entry is unknown");
            cargo = new MagnumCargo();
            cargo.ShipCargo.Add(Storage("food", -1));
            ExpectUnknown(cargo, "food", "negative count rejected");
            cargo.ShipCargo[0] = Storage("food", int.MaxValue);
            ExpectCount(cargo, "food", int.MaxValue, "large valid count");
            cargo.ShipCargo.Add(Storage("food", 1));
            ExpectUnknown(cargo, "food", "combined count overflow rejected");

            cargo = new MagnumCargo();
            first = Storage("food", 15);
            cargo.ShipCargo.Add(first);
            _modContext = new ModContext { Current = new State { Cargo = cargo } };
            _modContext.Current.CloneInventory.Items.Add(new KeyValuePair<string, int>("food", 999));
            _browserGameplayActive = true;
            CheckCargo(ReadOverviewShipCargoCount("food") == 15, "clone inventory excluded during mission");
            _modContext.Current = new State { Cargo = new MagnumCargo() };
            CheckCargo(ReadOverviewShipCargoCount("food") == 0, "use replacement save state immediately");
            _modContext.Current.Cargo = cargo;
            _browserGameplayActive = false;
            CheckCargo(!ReadOverviewShipCargoCount("food").HasValue, "menu never exposes previous save count");
            _browserGameplayActive = true;
            _modContext.ThrowOnRead = true;
            CheckCargo(!ReadOverviewShipCargoCount("food").HasValue, "unavailable context is unknown");
            _modContext.ThrowOnRead = false;
            _modContext.Current.Cargo = null;
            CheckCargo(!ReadOverviewShipCargoCount("food").HasValue, "cargo not loaded yet");
            _modContext.Current.Cargo = cargo;
            CheckCargo(ReadOverviewShipCargoCount("food") == 15, "late cargo recovers");

            // Execute the actual Overview entry point with unrelated relationships
            // empty. Stock must also exist for items with no research/recipe links.
            _inspectorItemId = "food";
            BrowserLines.Clear();
            BuildBrowserOverview("food");
            CheckCargo(BrowserLines[0].Left == "On ship" && BrowserLines[0].Right == "15", "first Overview row for ordinary loot");
            CheckCargo(BrowserLines.Count == 2 && BrowserLines[1].Left == "ui.overview_no_core_links", "preserve empty relationship explanation");
            BrowserLines.Clear();
            BuildBrowserOverview("");
            CheckCargo(BrowserLines.Count == 0, "catalog has no item count");
            BuildBrowserOverview("absent");
            CheckCargo(BrowserLines[0].Right == "0", "show zero instead of hiding row");

            BrowserLines.Clear();
            Time.realtimeSinceStartup = 10f;
            _inspectorItemId = "food";
            _inspectorOpen = true;
            _inspectorRoot = new object();
            BrowserNavigation.Tab = (int)BrowserTabId.Overview;
            AppendOverviewShipCargo("food");
            var other = BrowserLine.Normal("other", "unchanged");
            BrowserLines.Add(other);
            BrowserNavigation.ScrollOffset = 4;
            int reads = _modContext.Current.Reads;
            TickOverviewShipCargo();
            CheckCargo(_modContext.Current.Reads == reads, "poll interval avoids immediate rescan");
            Time.realtimeSinceStartup = 10.5f;
            TickOverviewShipCargo();
            CheckCargo(_modContext.Current.Reads == reads + 1 && _rowRenders == 0, "unchanged stock causes no redraw");
            first.Items.Clear();
            first.Items.Add(new KeyValuePair<string, int>("food", 3));
            Time.realtimeSinceStartup = 11f;
            TickOverviewShipCargo();
            CheckCargo(BrowserLines[0].Right == "3" && _rowRenders == 1, "live row refresh after spending or transfer");
            CheckCargo(ReferenceEquals(BrowserLines[1], other) && BrowserNavigation.ScrollOffset == 4, "preserve other rows and scroll");
            _modContext.Current.Cargo = null;
            Time.realtimeSinceStartup = 11.5f;
            TickOverviewShipCargo();
            CheckCargo(BrowserLines[0].Right == "?", "lost state clears stale count");
            _modContext.Current.Cargo = cargo;
            Time.realtimeSinceStartup = 12f;
            TickOverviewShipCargo();
            CheckCargo(BrowserLines[0].Right == "3", "recover displayed count");
            reads = _modContext.Current.Reads;
            Time.realtimeSinceStartup = 20f;
            _inspectorOpen = false;
            TickOverviewShipCargo();
            CheckCargo(_modContext.Current.Reads == reads, "closed browser does no cargo work");
            _inspectorOpen = true;
            _browserCatalogOpen = true;
            TickOverviewShipCargo();
            CheckCargo(_modContext.Current.Reads == reads, "catalog does no cargo work");
            _browserCatalogOpen = false;
            BrowserNavigation.Tab = (int)BrowserTabId.Recipes;
            TickOverviewShipCargo();
            CheckCargo(_modContext.Current.Reads == reads, "other tabs do no cargo work");
            BrowserNavigation.Tab = (int)BrowserTabId.Overview;
            _inspectorItemId = "other";
            TickOverviewShipCargo();
            CheckCargo(_modContext.Current.Reads == reads, "cannot update previous item's row");
            _inspectorItemId = "food";
            ResetOverviewShipCargo();
            TickOverviewShipCargo();
            CheckCargo(_modContext.Current.Reads == reads && _overviewShipCargoLine == null, "reset releases row state");
            _modContext = null;
            CheckCargo(!ReadOverviewShipCargoCount("food").HasValue, "missing context");
            _numberCulture = CultureInfo.GetCultureInfo("de-DE");
            CheckCargo(CreateOverviewShipCargoLine(12345).Right == "12.345", "group numbers using selected UI language");
            return _cargoAssertions;
        }

        private static ModContext _modContext;
        private static bool _browserGameplayActive;
        private static bool _inspectorOpen, _browserCatalogOpen;
        private static object _inspectorRoot;
        private static string _inspectorItemId;
        private static int _rowRenders;
        private static CultureInfo _numberCulture = CultureInfo.InvariantCulture;
        private static CultureInfo GetUiNumberCulture() { return _numberCulture; }
        private static string Ui(string key) { return key == "ui.ship_cargo" ? "On ship" : key; }
        private static void RenderBrowserRowsOnly() { _rowRenders++; }
        private enum BrowserTabId { Overview, Recipes }
        private sealed class Navigation { public int Tab; public int ScrollOffset; }
        private static readonly Navigation BrowserNavigation = new Navigation();
        private sealed class BrowserLine
        {
            public string Left, Right;
            public static BrowserLine Normal(string left, string right) { return new BrowserLine { Left = left, Right = right }; }
            public static BrowserLine FullNote(string left) { return Normal(left, ""); }
        }
        private static readonly List<BrowserLine> BrowserLines = new List<BrowserLine>();
        private sealed class WeaponModeDescriptor { }
        private sealed class OverviewPreview { public readonly List<string> ItemIds = new List<string>(); }
        private static bool ShowAmmoRelations { get { return false; } }
        private static string ResolveStaticRelationItemId(string id) { return id; }
        private static List<WeaponModeDescriptor> GetWeaponModesForItem(string id) { return new List<WeaponModeDescriptor>(); }
        private static void AppendOverviewCombat(List<WeaponModeDescriptor> modes) { }
        private static void AppendOverviewBaronSpecial(string id) { }
        private static void AppendOverviewArmWeaponLinks(string id) { }
        private static OverviewPreview ResolveOverviewPreview(string id, string relationId) { return null; }
        private static void AppendOverviewRelationships(string id, string relationId, OverviewPreview preview) { }
        private static void AppendOverviewUpgradeUses(string id) { }
        private static bool UsesInheritedStaticRelations(string id) { return false; }
        private static void AddModifiedRelationBrowserNote(string id) { }
        private static void AppendOverviewPreview(OverviewPreview preview) { }
        private static void AppendOverviewChipUnlocks(string id) { }
        private static void AppendBrowserModderOverview(string id) { }
    }
}
