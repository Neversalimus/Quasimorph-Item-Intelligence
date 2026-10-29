namespace UnityEngine
{
    public static class Time { public static float realtimeSinceStartup = 0f; }
    public static class Mathf
    {
        public static int Clamp(int value, int min, int max) { return Math.Max(min, Math.Min(max, value)); }
    }
}
namespace ItemIntelligence
{
    public static partial class ModMain
    {
        private enum BrowserTabId { Overview, Magnum, Recipes, Trade, Ammo, Factions, Loot, Count }
        private const int BrowserTabCount = (int)BrowserTabId.Count;
        private static readonly BrowserNavigationSessionState BrowserNavigation = new BrowserNavigationSessionState(7, 64);
        private static bool _inspectorOpen = false;
        private static string _inspectorItemId = "food";
        private static string _secretDataSelectedFactionId = "selected";
        private static bool _marketScanActive = true, _marketScanComplete = true;
        private static bool ShowSources = true, ShowTradeInformation = false;
        private static readonly List<int> BrowserLines = new List<int>();
        private static int BrowserVisibleRows = 10;
        private static readonly Dictionary<string, bool> LootSectionExpanded = new Dictionary<string, bool>();
        private static readonly HashSet<string> KnownItems = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            { "food", "weapon", "medical" };
        private static readonly List<string> RenderedItems = new List<string>(), VisitedItems = new List<string>();
        private static int assertions, catalogCloses, tooltipHides, marketScans, rowRenders, headerUpdates;
        private static bool lastForceScan = false;
        private static int lastHiddenTab = 0, lastHiddenOffset = 0;
        private static string lastHiddenItem = "";
        private static void CheckNavigationAdapter(bool condition, string message)
        {
            assertions++;
            if (!condition) throw new Exception("Browser navigation adapter: " + message);
        }
        private static bool IsKnownItemId(string id) { return KnownItems.Contains(id); }
        private static BrowserTabId ResolveAdaptiveEntryTab(string id)
        { return string.Equals(id, "weapon", StringComparison.OrdinalIgnoreCase) ? BrowserTabId.Ammo : BrowserTabId.Magnum; }
        private static void CloseBrowserCatalog() { catalogCloses++; }
        private static void HideBrowserPreviewTooltip()
        {
            tooltipHides++;
            lastHiddenTab = BrowserNavigation.Tab;
            lastHiddenOffset = BrowserNavigation.ScrollOffset;
            lastHiddenItem = _inspectorItemId;
        }
        private static void RecordBrowserItemVisit(string id) { VisitedItems.Add(id); }
        private static void StartMarketScan(string id, bool force = false) { marketScans++; lastForceScan = force; }
        private static void RenderBrowser(string id) { RenderedItems.Add(id); }
        private static void RenderBrowserRowsOnly() { rowRenders++; }
        private static void UpdateBrowserHeaderActions() { headerUpdates++; }
        private static void ReportBrowserPerformanceBudget(string id, bool cold, float total, float render) { }
        private static void VerboseLog(string message) { }

        public static int RunNavigationAdapterCases()
        {
            CheckNavigationAdapter(!NavigateBrowserToItem("weapon", false, "test") && BrowserNavigation.HistoryCount == 0 &&
                RenderedItems.Count == 0 && catalogCloses == 0, "closed inspector performs no navigation side effects");
            _inspectorOpen = true;
            CheckNavigationAdapter(!NavigateBrowserToItem("missing", false, "test") && _inspectorItemId == "food" &&
                BrowserNavigation.HistoryCount == 0, "unknown targets do not change selection or history");
            CheckNavigationAdapter(!NavigateBrowserToItem("", false, "test"), "empty target is rejected");
            BrowserNavigation.Begin((int)BrowserTabId.Overview);
            BrowserNavigation.SetScrollOffset(8);
            CheckNavigationAdapter(NavigateBrowserToItem("weapon", false, "test") && _inspectorItemId == "weapon" &&
                BrowserNavigation.Tab == (int)BrowserTabId.Ammo && BrowserNavigation.ScrollOffset == 0 &&
                BrowserNavigation.HistoryCount == 1, "landing navigation chooses the item's adaptive tab and saves the previous page");
            CheckNavigationAdapter(catalogCloses == 1 && tooltipHides == 1 && VisitedItems[0] == "weapon" &&
                RenderedItems[0] == "weapon", "item navigation closes overlays and visits/renders the selected item");
            CheckNavigationAdapter(_secretDataSelectedFactionId == "" && !_marketScanActive && !_marketScanComplete,
                "item navigation invalidates item-specific panel state");
            for (int i = 0; i < 40; i++) BrowserLines.Add(i);
            ScrollBrowserRows(100);
            CheckNavigationAdapter(BrowserNavigation.ScrollOffset == 30 && rowRenders == 1,
                "wheel clamps to visible rows and renders once");
            ScrollBrowserRows(1);
            ScrollBrowserRows(0);
            CheckNavigationAdapter(rowRenders == 1, "unchanged wheel position does not redraw");
            ScrollBrowserRows(-100);
            CheckNavigationAdapter(BrowserNavigation.ScrollOffset == 0 && rowRenders == 2, "wheel clamps its lower bound");
            SetBrowserTab((int)BrowserTabId.Trade);
            CheckNavigationAdapter(marketScans == 1 && lastForceScan && BrowserNavigation.HistoryCount == 1,
                "entering Trade forces its refresh without adding an item-history entry");
            BrowserNavigation.SetScrollOffset(7);
            int previousCloses = catalogCloses;
            SetBrowserTab((int)BrowserTabId.Trade);
            CheckNavigationAdapter(marketScans == 2 && lastForceScan && BrowserNavigation.ScrollOffset == 7 &&
                catalogCloses == previousCloses, "selecting Trade again keeps scroll and still refreshes prices");
            CheckNavigationAdapter(NavigateBrowserToItem("medical", false, "test") && BrowserNavigation.Tab == (int)BrowserTabId.Trade &&
                BrowserNavigation.ScrollOffset == 0 && BrowserNavigation.HistoryCount == 2 && !lastForceScan,
                "item navigation keeps an explicitly selected tab and restarts its item scan");
            CheckNavigationAdapter(NavigateBrowserBack() && _inspectorItemId == "weapon" &&
                BrowserNavigation.Tab == (int)BrowserTabId.Trade && BrowserNavigation.ScrollOffset == 7 && !lastForceScan,
                "back restores item/tab/scroll and refreshes Trade for that item");
            CheckNavigationAdapter(NavigateBrowserBack() && _inspectorItemId == "food" &&
                BrowserNavigation.Tab == (int)BrowserTabId.Overview && BrowserNavigation.ScrollOffset == 8 &&
                BrowserNavigation.HistoryCount == 0, "back returns to the original page");
            CheckNavigationAdapter(lastHiddenTab == (int)BrowserTabId.Trade && lastHiddenOffset == 7 && lastHiddenItem == "weapon",
                "back hides old overlays before changing item/tab/scroll");
            int previousRenders = RenderedItems.Count;
            CheckNavigationAdapter(!NavigateBrowserBack() && headerUpdates == 1 && RenderedItems.Count == previousRenders &&
                _inspectorItemId == "food", "empty back history refreshes buttons without changing the page");
            CheckNavigationAdapter(NavigateBrowserToItem("FOOD", false, "test") && BrowserNavigation.HistoryCount == 0 &&
                BrowserNavigation.Tab == (int)BrowserTabId.Overview && BrowserNavigation.ScrollOffset == 0,
                "same item with different ID case does not add history or choose a new adaptive tab");
            BrowserNavigation.Begin((int)BrowserTabId.Factions);
            _inspectorItemId = "food";
            CheckNavigationAdapter(NavigateBrowserToItem("weapon", false, "test") && BrowserNavigation.Tab == (int)BrowserTabId.Factions,
                "Factions remains selected across item links");
            CheckNavigationAdapter(NavigateBrowserToItem("weapon", true, "test") && BrowserNavigation.Tab == (int)BrowserTabId.Ammo &&
                BrowserNavigation.HistoryCount == 1, "forced adaptive entry also works for the same item");

            BrowserNavigation.Begin((int)BrowserTabId.Overview);
            BrowserNavigation.SetScrollOffset(4);
            _inspectorItemId = "food";
            NavigateBrowserToItem("weapon", false, "test");
            NavigateBrowserToItem("medical", false, "test");
            KnownItems.Remove("weapon");
            CheckNavigationAdapter(NavigateBrowserBack() && _inspectorItemId == "food" && BrowserNavigation.ScrollOffset == 4 &&
                BrowserNavigation.HistoryCount == 0, "back resolves stale entries against current game data");
            BrowserNavigation.Begin(0);
            CycleBrowserTab(-1);
            CheckNavigationAdapter(BrowserNavigation.Tab == (int)BrowserTabId.Loot, "previous-tab shortcut wraps to the final tab");
            CycleBrowserTab(1);
            CheckNavigationAdapter(BrowserNavigation.Tab == (int)BrowserTabId.Overview, "next-tab shortcut wraps to Overview");
            _inspectorOpen = false;
            previousRenders = RenderedItems.Count;
            CycleBrowserTab(1);
            SetBrowserTab((int)BrowserTabId.Trade);
            CheckNavigationAdapter(BrowserNavigation.Tab == 0 && RenderedItems.Count == previousRenders,
                "closed inspector ignores tab shortcuts");
            _inspectorOpen = true;
            BrowserNavigation.Begin((int)BrowserTabId.Loot);
            HandleLootSectionToggleAction("header", 18);
            CheckNavigationAdapter(!LootSectionExpanded["header"] && BrowserNavigation.ScrollOffset == 17,
                "loot section toggle keeps the clicked header in view");
            HandleLootSectionToggleAction("header", 0);
            CheckNavigationAdapter(LootSectionExpanded["header"] && BrowserNavigation.ScrollOffset == 0,
                "loot header anchor cannot become negative");
            previousRenders = RenderedItems.Count;
            HandleLootSectionToggleAction("", 18);
            CheckNavigationAdapter(RenderedItems.Count == previousRenders, "empty loot section does not navigate or redraw");
            return assertions;
        }
    }
}
