namespace ItemIntelligence
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    public static partial class ModMain
    {
        private enum BrowserCatalogScope { All, Favorites, Recent }
        private enum BrowserCatalogDataFilter { Any, Recipes }
        private enum BrowserCatalogSortMode { Name, Tech, ItemId }
        private static readonly CatalogSortCache BrowserCatalogSort = new CatalogSortCache();
        private static readonly List<string> BrowserCatalogFilteredItemIds = new List<string>();
        private static readonly List<string> BrowserRecentItemIds = new List<string>();
        private static readonly List<string> BrowserSearchIndexItemIds = new List<string>();
        private static readonly HashSet<string> BrowserFavoriteItemIds = new HashSet<string>();
        private static readonly Dictionary<string, int> BrowserCatalogCategoryByItem = new Dictionary<string, int>();
        private static readonly Dictionary<string, string> BrowserSearchDisplayNames = new Dictionary<string, string>();
        private static readonly Dictionary<string, int> tech = new Dictionary<string, int>();
        private static readonly HashSet<string> missing = new HashSet<string>();
        private static readonly HashSet<string> excluded = new HashSet<string>();
        private static BrowserCatalogScope _browserCatalogScope;
        private static BrowserCatalogDataFilter _browserCatalogDataFilter;
        private static BrowserCatalogSortMode _browserCatalogSortMode;
        private static bool _browserCatalogSortDescending;
        private static int _browserCatalogCategory, _browserSearchIndexRevision;
        private static bool VerboseLogging;
        private static int filterReads, renders, assertions;
        private static string rendered;
        private static bool IsKnownItemId(string id) { return !missing.Contains(id); }
        private static bool IsBrowserCatalogDataFilterAvailable(BrowserCatalogDataFilter filter) { return true; }
        private static bool BrowserCatalogItemPassesDataFilter(string id) { filterReads++; return !excluded.Contains(id); }
        private static bool TryGetExactItemTechLevel(string id, out int value) { return tech.TryGetValue(id, out value); }
        private static void RenderBrowserCatalogRows() { renders++; rendered = string.Join(",", BrowserCatalogFilteredItemIds); }
        private static void VerboseLog(string text) { }
        private static void Check(bool value, string message) { assertions++; if (!value) throw new Exception("Catalog adapter: " + message); }

        public static int RunCatalogAdapterCases()
        {
            BrowserSearchIndexItemIds.AddRange(new string[] { "c", "a", "b", "pending", "gone", "" });
            BrowserSearchDisplayNames["a"] = "Zulu"; BrowserSearchDisplayNames["b"] = "Beta";
            BrowserSearchDisplayNames["c"] = "Alpha"; BrowserSearchDisplayNames["gone"] = "Gone";
            missing.Add("gone");
            BrowserCatalogCategoryByItem["a"] = 1; BrowserCatalogCategoryByItem["b"] = 2; BrowserCatalogCategoryByItem["c"] = 2;
            _browserSearchIndexRevision = 1;
            RefreshBrowserCatalog();
            Check(rendered == "c,b,a" && filterReads == 4, "initial refresh excludes missing IDs and pending names");
            filterReads = 0; RefreshBrowserCatalog();
            Check(rendered == "c,b,a" && filterReads == 4 && renders == 2, "cached sort still reads live membership and renders rows");
            excluded.Add("b"); RefreshBrowserCatalog();
            Check(rendered == "c,a", "live filter membership changes invalidate cached sort");
            excluded.Clear(); RefreshBrowserCatalog(); Check(rendered == "c,b,a", "live membership can return");
            missing.Add("c"); RefreshBrowserCatalog(); Check(rendered == "b,a", "removed record is not retained by cache");
            missing.Remove("c");
            BrowserSearchDisplayNames["pending"] = "Aardvark"; _browserSearchIndexRevision++;
            RefreshBrowserCatalog(); Check(rendered == "pending,c,b,a", "warmup completion adds newly localized item");
            BrowserSearchDisplayNames["a"] = "AAAA"; _browserSearchIndexRevision++;
            RefreshBrowserCatalog(); Check(rendered.StartsWith("a,"), "localized-name revision changes ordering");
            _browserCatalogCategory = 2; RefreshBrowserCatalog(); Check(rendered == "c,b", "category selection changes candidates");
            _browserCatalogCategory = 0; _browserCatalogScope = BrowserCatalogScope.Favorites;
            BrowserFavoriteItemIds.Add("b"); RefreshBrowserCatalog(); Check(rendered == "b", "favorites are filtered before sorting");
            BrowserFavoriteItemIds.Add("a"); RefreshBrowserCatalog(); Check(rendered == "a,b", "favorite changes are current");
            _browserCatalogScope = BrowserCatalogScope.Recent; BrowserRecentItemIds.AddRange(new string[] { "b", "c", "a" });
            RefreshBrowserCatalog(); Check(rendered == "b,c,a", "recent scope preserves visit order");
            _browserCatalogScope = BrowserCatalogScope.All; _browserCatalogSortMode = BrowserCatalogSortMode.ItemId;
            RefreshBrowserCatalog(); Check(rendered == "a,b,c,pending", "item ID sort uses production comparator");
            _browserCatalogSortDescending = true; RefreshBrowserCatalog(); Check(rendered == "pending,c,b,a", "direction change updates ordering");
            _browserCatalogSortDescending = false; _browserCatalogSortMode = BrowserCatalogSortMode.Tech;
            tech["a"] = 3; tech["b"] = 2; tech["c"] = 1; tech["pending"] = 4;
            RefreshBrowserCatalog(); Check(rendered == "c,b,a,pending", "Tech uses current record values");
            tech["a"] = 0; RefreshBrowserCatalog(); Check(rendered == "a,c,b,pending", "Tech changes bypass sort cache without index revision");
            _browserCatalogDataFilter = BrowserCatalogDataFilter.Recipes;
            int beforeRender = renders;
            VerboseLogging = true; RefreshBrowserCatalog(); Check(renders == beforeRender + 1, "diagnostics do not change refresh behavior");
            return assertions;
        }
    }
}
