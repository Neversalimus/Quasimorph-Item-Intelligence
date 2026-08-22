using System.Globalization;

namespace ItemIntelligence
{
    public static partial class ModMain
    {
        private static string GetBrowserCatalogScopeLabel(BrowserCatalogScope scope, bool compact)
        {
            switch (scope)
            {
                case BrowserCatalogScope.Favorites:
                    return Ui(compact ? "catalog.scope.favorites.compact" : "catalog.scope.favorites");
                case BrowserCatalogScope.Recent:
                    return Ui(compact ? "catalog.scope.recent.compact" : "catalog.scope.recent");
                default:
                    return Ui(compact ? "catalog.scope.all.compact" : "catalog.scope.all");
            }
        }

        private static string GetBrowserCatalogDataFilterLabel(BrowserCatalogDataFilter filter)
        {
            switch (filter)
            {
                case BrowserCatalogDataFilter.Recipes: return Ui("catalog.data.recipes");
                case BrowserCatalogDataFilter.Sources: return Ui("catalog.data.sources");
                case BrowserCatalogDataFilter.Consumers: return Ui("catalog.data.consumers");
                case BrowserCatalogDataFilter.Magnum: return Ui("catalog.data.magnum");
                case BrowserCatalogDataFilter.Factions: return Ui("catalog.data.factions");
                case BrowserCatalogDataFilter.Ammo: return Ui("catalog.data.ammo");
                case BrowserCatalogDataFilter.Disassembly: return Ui("catalog.data.disassembly");
                default: return Ui("catalog.data.any");
            }
        }

        private static string GetBrowserCatalogSortLabel(BrowserCatalogSortMode mode)
        {
            switch (mode)
            {
                case BrowserCatalogSortMode.Tech: return Ui("catalog.sort.tech");
                case BrowserCatalogSortMode.ItemId: return Ui("catalog.sort.id");
                default: return Ui("catalog.sort.name");
            }
        }

        private static string GetBrowserCatalogRowMetadata(string itemId)
        {
            if (_browserCatalogScope == BrowserCatalogScope.Recent)
                return Ui("catalog.sort.recent") + "  " + itemId;
            if (_browserCatalogSortMode == BrowserCatalogSortMode.Tech)
            {
                int techLevel;
                string techText = TryGetExactItemTechLevel(itemId, out techLevel)
                    ? techLevel.ToString(CultureInfo.InvariantCulture)
                    : "?";
                return Ui("ui.tech") + " " + techText + "  " + itemId;
            }
            return itemId;
        }
    }
}
