using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace ItemIntelligence
{
    /// <summary>
    /// Catalog controller and user-owned catalog data. Favorites persist in the mod
    /// configuration directory; recent items and Back navigation remain session-only.
    /// No game/save state is read through a mutation path or written by this owner.
    /// </summary>
    public static partial class ModMain
    {
        private static string BrowserFavoritesPath
        {
            get { return Path.Combine(ConfigDirectory, "favorites.txt"); }
        }

        private static void EnsureCatalogPreferencesLoaded()
        {
            if (_browserCatalogPreferencesLoaded) return;
            _browserCatalogPreferencesLoaded = true;
            BrowserFavoriteItemIds.Clear();

            try
            {
                Directory.CreateDirectory(ConfigDirectory);
                if (!File.Exists(BrowserFavoritesPath)) return;

                string[] lines = File.ReadAllLines(BrowserFavoritesPath, new UTF8Encoding(false, true));
                int limit = Math.Min(lines.Length, 4096);
                for (int i = 0; i < limit; i++)
                {
                    string itemId = (lines[i] ?? string.Empty).Trim();
                    if (itemId.Length == 0 || itemId.StartsWith("#", StringComparison.Ordinal)) continue;
                    if (itemId.Length > 256) continue;
                    BrowserFavoriteItemIds.Add(itemId);
                }

                Debug.Log("[ItemIntelligence] Catalog favorites loaded: " +
                    BrowserFavoriteItemIds.Count.ToString(CultureInfo.InvariantCulture) + ".");
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[ItemIntelligence] Catalog favorites load failed; in-memory favorites remain available: " +
                    ex.Message);
            }
        }

        private static void SaveCatalogFavorites()
        {
            EnsureCatalogPreferencesLoaded();
            string temporaryPath = BrowserFavoritesPath + ".tmp";

            try
            {
                Directory.CreateDirectory(ConfigDirectory);
                List<string> favorites = new List<string>(BrowserFavoriteItemIds);
                favorites.Sort(StringComparer.OrdinalIgnoreCase);

                List<string> lines = new List<string>(favorites.Count + 1);
                lines.Add("# Item Intelligence favorites - one stable item ID per line");
                lines.AddRange(favorites);

                if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
                File.WriteAllLines(temporaryPath, lines.ToArray(), new UTF8Encoding(false));

                if (File.Exists(BrowserFavoritesPath))
                {
                    try
                    {
                        File.Replace(temporaryPath, BrowserFavoritesPath, null);
                    }
                    catch
                    {
                        File.Copy(temporaryPath, BrowserFavoritesPath, true);
                        File.Delete(temporaryPath);
                    }
                }
                else
                {
                    File.Move(temporaryPath, BrowserFavoritesPath);
                }
            }
            catch (Exception ex)
            {
                try { if (File.Exists(temporaryPath)) File.Delete(temporaryPath); }
                catch { }
                Debug.LogWarning("[ItemIntelligence] Catalog favorites save failed; the current session still keeps the change: " +
                    ex.Message);
            }
        }

        private static bool IsBrowserFavorite(string itemId)
        {
            EnsureCatalogPreferencesLoaded();
            return !string.IsNullOrEmpty(itemId) && BrowserFavoriteItemIds.Contains(itemId);
        }

        private static void ToggleBrowserFavorite(string itemId)
        {
            if (string.IsNullOrEmpty(itemId) || !IsKnownItemId(itemId)) return;
            EnsureCatalogPreferencesLoaded();

            bool added;
            if (BrowserFavoriteItemIds.Contains(itemId))
            {
                BrowserFavoriteItemIds.Remove(itemId);
                added = false;
            }
            else
            {
                BrowserFavoriteItemIds.Add(itemId);
                added = true;
            }

            SaveCatalogFavorites();
            UpdateBrowserHeaderActions();
            if (_browserCatalogOpen) RefreshBrowserCatalog();
            Debug.Log("[ItemIntelligence] Catalog favorite " + (added ? "added: " : "removed: ") + itemId + ".");
        }

        private static void RecordBrowserItemVisit(string itemId)
        {
            if (string.IsNullOrEmpty(itemId) || !IsKnownItemId(itemId)) return;

            for (int i = BrowserRecentItemIds.Count - 1; i >= 0; i--)
            {
                if (string.Equals(BrowserRecentItemIds[i], itemId, StringComparison.OrdinalIgnoreCase))
                    BrowserRecentItemIds.RemoveAt(i);
            }

            BrowserRecentItemIds.Insert(0, itemId);
            while (BrowserRecentItemIds.Count > BrowserRecentItemLimit)
                BrowserRecentItemIds.RemoveAt(BrowserRecentItemIds.Count - 1);
        }

        private static void ClearBrowserRecentItems()
        {
            BrowserRecentItemIds.Clear();
            _browserCatalogPage = 0;
            RefreshBrowserCatalog();
            Debug.Log("[ItemIntelligence] Catalog session history cleared.");
        }

        private static void PushBrowserNavigationState()
        {
            if (string.IsNullOrEmpty(_inspectorItemId)) return;

            BrowserItemNavigationHistory.Add(new BrowserItemNavigationState(
                _inspectorItemId, _browserTab, _browserPage));
            if (BrowserItemNavigationHistory.Count > BrowserNavigationHistoryLimit)
                BrowserItemNavigationHistory.RemoveAt(0);
        }

        private static bool NavigateBrowserToItem(string itemId, bool resetToOverview, string source)
        {
            if (!_inspectorOpen || string.IsNullOrEmpty(itemId) || !IsKnownItemId(itemId))
                return false;

            bool changed = !string.Equals(_inspectorItemId, itemId, StringComparison.OrdinalIgnoreCase);
            if (changed) PushBrowserNavigationState();

            CloseBrowserCatalog();
            HideBrowserPreviewTooltip();
            _inspectorItemId = itemId;
            if (resetToOverview) _browserTab = (int)BrowserTabId.Overview;
            _browserPage = 0;
            if (_browserTab >= 0 && _browserTab < BrowserPageByTab.Length)
                BrowserPageByTab[_browserTab] = 0;
            _secretDataSelectedFactionId = string.Empty;
            _marketScanActive = false;
            _marketScanComplete = false;

            RecordBrowserItemVisit(itemId);
            if (_browserTab == (int)BrowserTabId.Trade && (ShowSources || ShowTradeInformation))
                StartMarketScan(itemId);

            RenderBrowser(itemId);
            if (!string.IsNullOrEmpty(source))
                Debug.Log("[ItemIntelligence] " + source + " selected: " + itemId + ".");
            return true;
        }

        private static bool NavigateBrowserBack()
        {
            while (BrowserItemNavigationHistory.Count > 0)
            {
                int last = BrowserItemNavigationHistory.Count - 1;
                BrowserItemNavigationState state = BrowserItemNavigationHistory[last];
                BrowserItemNavigationHistory.RemoveAt(last);
                if (state == null || string.IsNullOrEmpty(state.ItemId) || !IsKnownItemId(state.ItemId))
                    continue;

                CloseBrowserCatalog();
                HideBrowserPreviewTooltip();
                _inspectorItemId = state.ItemId;
                _browserTab = Math.Max(0, Math.Min(BrowserTabCount - 1, state.Tab));
                _browserPage = Math.Max(0, state.Page);
                BrowserPageByTab[_browserTab] = _browserPage;
                _secretDataSelectedFactionId = string.Empty;
                _marketScanActive = false;
                _marketScanComplete = false;
                RecordBrowserItemVisit(_inspectorItemId);
                if (_browserTab == (int)BrowserTabId.Trade && (ShowSources || ShowTradeInformation))
                    StartMarketScan(_inspectorItemId);
                RenderBrowser(_inspectorItemId);
                Debug.Log("[ItemIntelligence] Browser Back restored: " + _inspectorItemId + ".");
                return true;
            }

            UpdateBrowserHeaderActions();
            return false;
        }

        private static void ToggleBrowserCatalog()
        {
            if (_browserCatalogOpen) CloseBrowserCatalog();
            else OpenBrowserCatalog();
        }

        private static void OpenBrowserCatalog()
        {
            if (!_inspectorOpen) return;
            if (!_compatSearchCatalog)
            {
                Debug.LogWarning("[ItemIntelligence] Catalog unavailable: " +
                    GetCompatibilityReason("SearchCatalog"));
                return;
            }
            EnsureBrowserCatalogUi();
            if (_browserCatalogPanel == null) return;

            EnsureCatalogPreferencesLoaded();
            EnsureBrowserSearchIndexWarmup();
            HideBrowserSearchDropdown();
            if (_browserSearchInput != null) _browserSearchInput.DeactivateInputField();
            _browserCatalogOpen = true;
            _browserCatalogPage = 0;
            RefreshBrowserCatalog();
            _browserCatalogPanel.SetActive(true);
            _browserCatalogPanel.transform.SetAsLastSibling();
            UpdateBrowserCatalogButtonStyle();
        }

        private static void CloseBrowserCatalog()
        {
            _browserCatalogOpen = false;
            if (_browserCatalogPanel != null) _browserCatalogPanel.SetActive(false);
            UpdateBrowserCatalogButtonStyle();
        }

        private static void SetBrowserCatalogScope(int scope)
        {
            scope = Math.Max(0, Math.Min(BrowserCatalogScopeCount - 1, scope));
            _browserCatalogScope = (BrowserCatalogScope)scope;
            _browserCatalogPage = 0;
            RefreshBrowserCatalog();
        }

        private static void SetBrowserCatalogCategory(int category)
        {
            category = Math.Max(0, Math.Min(BrowserCatalogCategoryCount - 1, category));
            _browserCatalogCategory = category;
            _browserCatalogPage = 0;
            RefreshBrowserCatalog();
        }

        private static void CycleBrowserCatalogDataFilter()
        {
            int count = (int)BrowserCatalogDataFilter.Count;
            int current = (int)_browserCatalogDataFilter;
            for (int offset = 1; offset <= count; offset++)
            {
                BrowserCatalogDataFilter candidate = (BrowserCatalogDataFilter)((current + offset) % count);
                if (!IsBrowserCatalogDataFilterAvailable(candidate)) continue;
                _browserCatalogDataFilter = candidate;
                _browserCatalogPage = 0;
                RefreshBrowserCatalog();
                return;
            }
        }

        private static void CycleBrowserCatalogSortMode()
        {
            if (_browserCatalogScope == BrowserCatalogScope.Recent) return;
            int count = (int)BrowserCatalogSortMode.Count;
            _browserCatalogSortMode = (BrowserCatalogSortMode)(((int)_browserCatalogSortMode + 1) % count);
            _browserCatalogPage = 0;
            RefreshBrowserCatalog();
        }

        private static void ToggleBrowserCatalogSortDirection()
        {
            if (_browserCatalogScope == BrowserCatalogScope.Recent) return;
            _browserCatalogSortDescending = !_browserCatalogSortDescending;
            _browserCatalogPage = 0;
            RefreshBrowserCatalog();
        }

        private static void ResetBrowserCatalogFiltersOrHistory()
        {
            if (_browserCatalogScope == BrowserCatalogScope.Recent)
            {
                ClearBrowserRecentItems();
                return;
            }

            _browserCatalogCategory = 0;
            _browserCatalogDataFilter = BrowserCatalogDataFilter.Any;
            _browserCatalogSortMode = BrowserCatalogSortMode.Name;
            _browserCatalogSortDescending = false;
            _browserCatalogPage = 0;
            RefreshBrowserCatalog();
        }

        private static void ChangeBrowserCatalogPage(int delta)
        {
            if (!_browserCatalogOpen || delta == 0) return;
            int pages = Math.Max(1,
                (BrowserCatalogFilteredItemIds.Count + BrowserCatalogVisibleRows - 1) /
                BrowserCatalogVisibleRows);
            int next = Mathf.Clamp(_browserCatalogPage + delta, 0, pages - 1);
            if (next == _browserCatalogPage) return;
            _browserCatalogPage = next;
            RenderBrowserCatalogRows();
        }

        private static bool IsBrowserCatalogDataFilterAvailable(BrowserCatalogDataFilter filter)
        {
            switch (filter)
            {
                case BrowserCatalogDataFilter.Recipes: return _compatRecipes && ShowRecipes;
                case BrowserCatalogDataFilter.Sources: return _compatTrade && ShowSources;
                case BrowserCatalogDataFilter.Consumers: return _compatTrade && ShowTradeInformation;
                case BrowserCatalogDataFilter.Magnum: return _compatMagnum && ShowMagnumUses;
                case BrowserCatalogDataFilter.Factions: return _compatFactions;
                case BrowserCatalogDataFilter.Ammo: return _compatAmmo && ShowAmmoRelations;
                case BrowserCatalogDataFilter.Disassembly: return _compatDisassembly;
                default: return true;
            }
        }

        private static bool BrowserCatalogItemPassesDataFilter(string itemId)
        {
            string relationId = ResolveStaticRelationItemId(itemId);
            switch (_browserCatalogDataFilter)
            {
                case BrowserCatalogDataFilter.Recipes:
                    return GetListCount(UsedInRecipes, relationId) > 0 ||
                           GetListCount(CraftedFromRecipes, relationId) > 0;
                case BrowserCatalogDataFilter.Sources:
                    return GetListCount(BarterSources, itemId) > 0;
                case BrowserCatalogDataFilter.Consumers:
                    return GetListCount(BarterConsumers, itemId) > 0;
                case BrowserCatalogDataFilter.Magnum:
                    return HasVisibleMagnumUses(itemId);
                case BrowserCatalogDataFilter.Factions:
                    return FactionTechUnlocksByItem.ContainsKey(itemId);
                case BrowserCatalogDataFilter.Ammo:
                    return WeaponsByItem.ContainsKey(relationId) ||
                           CompatibleWeaponsByAmmo.ContainsKey(relationId);
                case BrowserCatalogDataFilter.Disassembly:
                    return GetDisassemblyOutputCount(itemId) > 0 ||
                           GetDisassemblySourceCount(itemId) > 0;
                default:
                    return true;
            }
        }

        private static int CompareBrowserCatalogItems(string a, string b)
        {
            int result = 0;
            if (_browserCatalogSortMode == BrowserCatalogSortMode.Tech)
            {
                int techA, techB;
                bool hasTechA = TryGetExactItemTechLevel(a, out techA);
                bool hasTechB = TryGetExactItemTechLevel(b, out techB);
                if (hasTechA != hasTechB) result = hasTechA ? -1 : 1;
                else if (hasTechA) result = techA.CompareTo(techB);
            }
            else if (_browserCatalogSortMode == BrowserCatalogSortMode.ItemId)
            {
                result = string.Compare(a, b, StringComparison.OrdinalIgnoreCase);
            }

            if (result == 0 && _browserCatalogSortMode != BrowserCatalogSortMode.ItemId)
            {
                string an;
                string bn;
                if (!BrowserSearchDisplayNames.TryGetValue(a, out an)) an = a;
                if (!BrowserSearchDisplayNames.TryGetValue(b, out bn)) bn = b;
                result = string.Compare(an, bn, StringComparison.CurrentCultureIgnoreCase);
            }

            if (result == 0)
                result = string.Compare(a, b, StringComparison.OrdinalIgnoreCase);
            return _browserCatalogSortDescending ? -result : result;
        }

        private static void RefreshBrowserCatalog()
        {
            if (!IsBrowserCatalogDataFilterAvailable(_browserCatalogDataFilter))
                _browserCatalogDataFilter = BrowserCatalogDataFilter.Any;

            BrowserCatalogFilteredItemIds.Clear();
            IList<string> source = _browserCatalogScope == BrowserCatalogScope.Recent
                ? (IList<string>)BrowserRecentItemIds
                : (IList<string>)BrowserSearchIndexItemIds;

            for (int i = 0; i < source.Count; i++)
            {
                string itemId = source[i];
                if (string.IsNullOrEmpty(itemId) || !IsKnownItemId(itemId)) continue;
                if (_browserCatalogScope == BrowserCatalogScope.Favorites &&
                    !BrowserFavoriteItemIds.Contains(itemId)) continue;

                int category;
                if (!BrowserCatalogCategoryByItem.TryGetValue(itemId, out category)) category = 8;
                if (_browserCatalogCategory != 0 && category != _browserCatalogCategory) continue;
                if (!BrowserCatalogItemPassesDataFilter(itemId)) continue;

                string name;
                if (!BrowserSearchDisplayNames.TryGetValue(itemId, out name) || string.IsNullOrEmpty(name))
                    continue;
                BrowserCatalogFilteredItemIds.Add(itemId);
            }

            if (_browserCatalogScope != BrowserCatalogScope.Recent)
                BrowserCatalogFilteredItemIds.Sort(CompareBrowserCatalogItems);

            RenderBrowserCatalogRows();
        }

        private static void SelectBrowserCatalogItem(string itemId)
        {
            NavigateBrowserToItem(itemId, false, "Catalog");
        }

        private static int ClassifyCatalogItem(string itemId)
        {
            object record;
            if (!ItemRecordsById.TryGetValue(itemId, out record) || record == null) return 8;

            List<object> graph = BuildRelevantItemGraph(record, 3, 48);
            bool weapon = false, armor = false, ammo = false, implant = false;
            bool consumable = false, chip = false, container = false;

            for (int i = 0; i < graph.Count; i++)
            {
                object node = graph[i];
                if (node == null) continue;
                ObserveCanonicalItemMetadataNode(itemId, node);
                string name = node.GetType().Name ?? string.Empty;

                if (name.IndexOf("AmmoRecord", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    name.IndexOf("AmmoDescriptor", StringComparison.OrdinalIgnoreCase) >= 0) ammo = true;
                else if (name.IndexOf("Implant", StringComparison.OrdinalIgnoreCase) >= 0 ||
                         name.IndexOf("Augmentation", StringComparison.OrdinalIgnoreCase) >= 0) implant = true;
                else if (name.IndexOf("WeaponRecord", StringComparison.OrdinalIgnoreCase) >= 0 ||
                         name.IndexOf("WeaponDescriptor", StringComparison.OrdinalIgnoreCase) >= 0 ||
                         name.IndexOf("ThrowingWeapon", StringComparison.OrdinalIgnoreCase) >= 0) weapon = true;
                else if (name.IndexOf("Armor", StringComparison.OrdinalIgnoreCase) >= 0 ||
                         name.IndexOf("Helmet", StringComparison.OrdinalIgnoreCase) >= 0 ||
                         name.IndexOf("Vest", StringComparison.OrdinalIgnoreCase) >= 0 ||
                         name.IndexOf("Backpack", StringComparison.OrdinalIgnoreCase) >= 0 ||
                         name.IndexOf("Boots", StringComparison.OrdinalIgnoreCase) >= 0 ||
                         name.IndexOf("Leggings", StringComparison.OrdinalIgnoreCase) >= 0) armor = true;
                else if (name.IndexOf("Consumable", StringComparison.OrdinalIgnoreCase) >= 0 ||
                         name.IndexOf("Food", StringComparison.OrdinalIgnoreCase) >= 0 ||
                         name.IndexOf("Medkit", StringComparison.OrdinalIgnoreCase) >= 0 ||
                         name.IndexOf("Medicine", StringComparison.OrdinalIgnoreCase) >= 0) consumable = true;
                else if (name.IndexOf("Datadisk", StringComparison.OrdinalIgnoreCase) >= 0 ||
                         name.IndexOf("DataDisk", StringComparison.OrdinalIgnoreCase) >= 0) chip = true;
                else if (name.IndexOf("Container", StringComparison.OrdinalIgnoreCase) >= 0) container = true;
            }

            string raw = itemId ?? string.Empty;
            if (raw.IndexOf("container", StringComparison.OrdinalIgnoreCase) >= 0 ||
                raw.IndexOf("_box", StringComparison.OrdinalIgnoreCase) >= 0 ||
                raw.IndexOf("case", StringComparison.OrdinalIgnoreCase) >= 0) container = true;

            if (ammo) return 3;
            if (implant) return 4;
            if (weapon) return 1;
            if (armor) return 2;
            if (consumable) return 5;
            if (chip) return 6;
            if (container) return 7;
            return 8;
        }

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
