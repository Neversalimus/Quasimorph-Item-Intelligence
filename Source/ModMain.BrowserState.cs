using System;
using System.Collections.Generic;
using MGSC;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ItemIntelligence
{
    public static partial class ModMain
    {
        private static string _lastHoveredItemId = string.Empty;
        private static string _priceBlockItemId = string.Empty;
        private static Component _activeTooltip;
        private static object _activeTooltipFactory;
        private static int _priceBlockFrame = -1000;

        // Generic station/production tooltips never enter QII's item-discovery scope.
        private static bool _itemPointerScope;
        private static int _itemPointerScopeFrame = -1000;
        private static string _itemPointerScopeItemId = string.Empty;
        private static object _lastItemPointerHandler;
        private static object _lastItemSlot;
        private static int _itemHoverResolveWarnings;
        private static int _itemSlotHoverResolveWarnings;

        private static GameObject _hoverHintCanvas;
        private static RectTransform _hoverHintRect;
        private static TMP_Text _hoverHintText;

        private static GameObject _inspectorRoot;
        private static RectTransform _inspectorRect;
        private static Canvas _inspectorCanvas;
        private static GraphicRaycaster _inspectorGraphicRaycaster;
        private static GameObject _inspectorInputBlocker;
        private static readonly List<GraphicRaycaster> SuppressedRaycasters =
            new List<GraphicRaycaster>();
        private static GameObject _inspectorCanvasObject;
        private static bool _inspectorOpen;
        private static int _inspectorHotkeyCaptureFrame = -1000;
        private static bool _inspectorHotkeyCaptureHasTarget;
        private static string _inspectorItemId = string.Empty;
        private static TMP_FontAsset _inspectorFont;

        // v1.7.36-test3: browser-owned mutable/UI state lives beside the browser
        // implementation instead of the shared runtime/bootstrap file. This is a
        // declaration-only move; pooled UI, navigation and search behavior are unchanged.
        // v1.2 browser UI: fixed-size object pool. F2 never creates/destroys a list
        // proportional to the number of recipes, sources or weapons.
        private const int BrowserVisibleRows = 14;

        private const int BrowserTabCount = (int)BrowserTabId.Count;
        private const int BrowserSearchVisibleRows = 8;
        private const int BrowserCatalogVisibleRows = 8;
        private const int BrowserCatalogCategoryCount = 9;
        private const int BrowserCatalogScopeCount = (int)BrowserCatalogScope.Count;
        private const int BrowserRecentItemLimit = 32;
        private const int BrowserNavigationHistoryLimit = 64;
        private static readonly BrowserNavigationSessionState BrowserNavigation = new BrowserNavigationSessionState();


        // v1.5.13 global item directory. Names are localized incrementally while the
        // browser is open, so the old hover/loading stutters are not reintroduced.
        private static TMP_InputField _browserSearchInput;
        private static TMP_Text _browserSearchStatusText;
        private static GameObject _browserSearchDropdown;
        private static readonly GameObject[] BrowserSearchRowRoots = new GameObject[BrowserSearchVisibleRows];
        private static readonly TMP_Text[] BrowserSearchRowNames = new TMP_Text[BrowserSearchVisibleRows];
        private static readonly TMP_Text[] BrowserSearchRowIds = new TMP_Text[BrowserSearchVisibleRows];
        private static readonly Image[] BrowserSearchRowIcons = new Image[BrowserSearchVisibleRows];
        private static readonly Button[] BrowserSearchRowButtons = new Button[BrowserSearchVisibleRows];
        private static readonly string[] BrowserSearchRowItemIds = new string[BrowserSearchVisibleRows];
        private static readonly List<string> BrowserSearchIndexItemIds = new List<string>();
        private static readonly Dictionary<string, string> BrowserSearchDisplayNames =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, string> BrowserSearchNormalizedNames =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, string> BrowserSearchNormalizedIds =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private static readonly List<BrowserSearchMatch> BrowserSearchCurrentMatches =
            new List<BrowserSearchMatch>();
        private static int _browserSearchScrollOffset;
        private static string _browserSearchLastNormalizedQuery = string.Empty;
        private static int _browserSearchWarmupIndex;
        private static bool _browserSearchWarmupActive;
        private static string _browserSearchIndexLanguage = string.Empty;
        // Search result cache generation. A zero-result query is still a valid cache
        // entry and must not be recomputed every frame merely because the dropdown is hidden.
        private static int _browserSearchIndexRevision;
        private static int _browserSearchLastResultRevision = -1;
        private static string _browserSearchLastResultLanguage = string.Empty;
        private static int _browserSearchLastRefreshFrame = -1000;
        private static int _browserSearchLastResultCount;
        private static bool _browserSearchSuppressEvents;
        private static bool _browserSearchCaptureLogged;

        // v1.6.1 item catalog. This reuses the already-localized global item index and
        // adds a lightweight cached category byte per item. The catalog is an overlay,
        // so opening it never rebuilds the main browser UI.
        private static bool _browserCatalogOpen;
        private static int _browserCatalogCategory;
        private static int _browserCatalogScrollOffset;
        private static BrowserCatalogScope _browserCatalogScope = BrowserCatalogScope.All;
        private static BrowserCatalogDataFilter _browserCatalogDataFilter = BrowserCatalogDataFilter.Any;
        private static BrowserCatalogSortMode _browserCatalogSortMode = BrowserCatalogSortMode.Name;
        private static bool _browserCatalogSortDescending;
        private static GameObject _browserCatalogPanel;
        private static TMP_Text _browserCatalogButtonText;
        private static Image _browserCatalogButtonBackground;
        private static TMP_Text _browserCatalogHeaderText;
        private static TMP_Text _browserCatalogScrollText;
        private static Button _browserCatalogDataFilterButton;
        private static TMP_Text _browserCatalogDataFilterText;
        private static Button _browserCatalogSortButton;
        private static TMP_Text _browserCatalogSortText;
        private static Button _browserCatalogDirectionButton;
        private static TMP_Text _browserCatalogDirectionText;
        private static Button _browserCatalogResetButton;
        private static TMP_Text _browserCatalogResetText;
        private static readonly Button[] BrowserCatalogScopeButtons = new Button[BrowserCatalogScopeCount];
        private static readonly TMP_Text[] BrowserCatalogScopeTexts = new TMP_Text[BrowserCatalogScopeCount];
        private static readonly Button[] BrowserCatalogCategoryButtons = new Button[BrowserCatalogCategoryCount];
        private static readonly TMP_Text[] BrowserCatalogCategoryTexts = new TMP_Text[BrowserCatalogCategoryCount];
        private static readonly GameObject[] BrowserCatalogRowRoots = new GameObject[BrowserCatalogVisibleRows];
        private static readonly TMP_Text[] BrowserCatalogRowNames = new TMP_Text[BrowserCatalogVisibleRows];
        private static readonly TMP_Text[] BrowserCatalogRowIds = new TMP_Text[BrowserCatalogVisibleRows];
        private static readonly Image[] BrowserCatalogRowIcons = new Image[BrowserCatalogVisibleRows];
        private static readonly Button[] BrowserCatalogRowFavoriteButtons = new Button[BrowserCatalogVisibleRows];
        private static readonly Image[] BrowserCatalogRowFavoriteBackgrounds = new Image[BrowserCatalogVisibleRows];
        private static readonly TMP_Text[] BrowserCatalogRowFavoriteTexts = new TMP_Text[BrowserCatalogVisibleRows];
        private static readonly string[] BrowserCatalogRowItemIds = new string[BrowserCatalogVisibleRows];
        private static readonly List<string> BrowserCatalogFilteredItemIds = new List<string>();
        private static readonly Dictionary<string, int> BrowserCatalogCategoryByItem =
            new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        private static readonly HashSet<string> BrowserFavoriteItemIds =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private static readonly List<string> BrowserRecentItemIds = new List<string>();
        private static bool _browserCatalogPreferencesLoaded;

        private static Button _browserBackButton;
        private static TMP_Text _browserBackButtonText;
        private static Image _browserBackButtonBackground;
        private static Button _browserFavoriteButton;
        private static TMP_Text _browserFavoriteButtonText;
        private static Image _browserFavoriteButtonBackground;

        // Header preview. v1.6.3 uses the native-tooltip pattern proven in
        // LoadoutPresets R18: ItemTooltipHandler lives directly on the raycast target,
        // and a deferred guard raises the native BaseTooltip canvas above our modal.
        private static Image _browserPreviewImage;
        private static ItemTooltipHandler _browserPreviewTooltipHandler;
        private static BasePickupItem _browserPreviewLiveItem;
        private static string _browserPreviewTooltipItemId = string.Empty;
        private static bool _browserPreviewTooltipWarningLogged;
        private static bool _browserPreviewTooltipTypeLogged;
        private static Canvas _browserRaisedTooltipCanvas;
        private static bool _browserRaisedTooltipCanvasAdded;
        private static bool _browserRaisedTooltipOriginalOverrideSorting;
        private static int _browserRaisedTooltipOriginalSortingLayerId;
        private static int _browserRaisedTooltipOriginalSortingOrder;
        private static AdditionalCanvasShaderChannels _browserRaisedTooltipOriginalShaderChannels;
        private static Camera _browserRaisedTooltipOriginalWorldCamera;

        private static TMP_Text _browserStatsText;
        private static TMP_Text _browserScrollText;

        // v1.7.40-test7: normal virtualized scrolling. The fixed row pools stay
        // allocation-safe, but the scrollbar/wheel now move by row rather than page.
        private static Scrollbar _browserScrollScrollbar;
        private static Scrollbar _browserCatalogScrollbar;
        private static Scrollbar _browserSearchScrollbar;
        private static bool _browserScrollbarSync;
        // Remember the first visible row per main tab. This is UI-only state and
        // resets whenever a new inspector session is opened.

        private static TMP_Text _browserHelpText;
        private static TMP_Text _browserCloseText;
        private static readonly GameObject[] BrowserRowRoots = new GameObject[BrowserVisibleRows];
        private static readonly TMP_Text[] BrowserRowLeft = new TMP_Text[BrowserVisibleRows];
        private static readonly TMP_Text[] BrowserRowRight = new TMP_Text[BrowserVisibleRows];
        private static readonly TMP_Text[] BrowserRowFactionReward = new TMP_Text[BrowserVisibleRows];
        private static readonly TMP_Text[] BrowserRowFactionUnlock = new TMP_Text[BrowserVisibleRows];
        private static readonly TMP_Text[] BrowserRowFactionCurrent = new TMP_Text[BrowserVisibleRows];
        private static readonly TMP_Text[] BrowserRowFactionState = new TMP_Text[BrowserVisibleRows];
        private static readonly Image[] BrowserRowBackground = new Image[BrowserVisibleRows];
        private static readonly Outline[] BrowserRowOutlines = new Outline[BrowserVisibleRows];
        private static readonly Image[] BrowserRowIcons = new Image[BrowserVisibleRows];
        private static readonly Image[] BrowserRowActionIcons = new Image[BrowserVisibleRows];
        private static readonly Image[] BrowserRowChipIcons = new Image[BrowserVisibleRows];
        private static readonly Image[] BrowserRowChipStatusIcons = new Image[BrowserVisibleRows];
        private static readonly Button[] BrowserRowButtons = new Button[BrowserVisibleRows];
        private static readonly TMP_Text[] BrowserTabTexts = new TMP_Text[BrowserTabCount];
        private static readonly Image[] BrowserTabBackgrounds = new Image[BrowserTabCount];
        private static readonly List<BrowserLine> BrowserLines = new List<BrowserLine>();

        private static void InitializeBrowserSpaceSessionState()
        {
            _priceBlockItemId = string.Empty;
            _priceBlockFrame = -1000;
            _itemPointerScope = false;
            _itemPointerScopeFrame = -1000;
            _itemPointerScopeItemId = string.Empty;
            _lastItemPointerHandler = null;
            _lastItemSlot = null;
            _itemHoverResolveWarnings = 0;
            _itemSlotHoverResolveWarnings = 0;
        }

        private static void ResetBrowserMenuSessionState()
        {
            _lastHoveredItemId = string.Empty;
            _priceBlockItemId = string.Empty;
            _priceBlockFrame = -1000;
            _itemPointerScope = false;
            _itemPointerScopeItemId = string.Empty;
            _lastItemPointerHandler = null;
            _lastItemSlot = null;
            _activeTooltip = null;
            _activeTooltipFactory = null;
            _browserPreviewLiveItem = null;
        }

        // v1.7.36-test5: Browser owns invalidation of its search/catalog index state.
        // Live GameObject/pool teardown remains in BrowserUI lifecycle methods.
        private static void ResetBrowserIndexState()
        {
            BrowserSearchIndexItemIds.Clear();
            BrowserSearchDisplayNames.Clear();
            BrowserSearchNormalizedNames.Clear();
            BrowserSearchNormalizedIds.Clear();
            BrowserSearchCurrentMatches.Clear();
            _browserSearchScrollOffset = 0;
            _browserSearchLastNormalizedQuery = string.Empty;
            _browserSearchWarmupIndex = 0;
            _browserSearchWarmupActive = false;
            _browserSearchIndexLanguage = string.Empty;
            _browserSearchIndexRevision = 0;
            _browserSearchLastResultRevision = -1;
            _browserSearchLastResultLanguage = string.Empty;
            _browserSearchLastResultCount = 0;
            _browserSearchCaptureLogged = false;
            BrowserCatalogCategoryByItem.Clear();
            ResetBrowserAdvancedSearchIndexState();
            BrowserCatalogFilteredItemIds.Clear();
            _browserCatalogOpen = false;
            _browserCatalogCategory = 0;
            _browserCatalogScrollOffset = 0;
            _browserCatalogScope = BrowserCatalogScope.All;
            _browserCatalogDataFilter = BrowserCatalogDataFilter.Any;
            _browserCatalogSortMode = BrowserCatalogSortMode.Name;
            _browserCatalogSortDescending = false;
        }
    }
}
