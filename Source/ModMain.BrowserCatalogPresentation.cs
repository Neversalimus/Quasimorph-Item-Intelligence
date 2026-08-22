using System;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ItemIntelligence
{
    /// <summary>
    /// Fixed-pool catalog presentation. All controls and rows are created once; filter,
    /// sort, favorite and history changes only update pooled text/images and item IDs.
    /// </summary>
    public static partial class ModMain
    {
        private static Button CreateBrowserHeaderActionButton(
            string name,
            float x,
            float width,
            float fontSize,
            out Image background,
            out TMP_Text text,
            Action action)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(_inspectorRoot.transform, false);
            RectTransform rt = go.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(x, -14f);
            rt.sizeDelta = new Vector2(width, 34f);

            background = go.AddComponent<Image>();
            background.color = new Color(0.017f, 0.047f, 0.040f, 0.55f);
            background.raycastTarget = true;
            Outline outline = go.AddComponent<Outline>();
            outline.effectColor = new Color(0.25f, 0.53f, 0.44f, 0.34f);
            outline.effectDistance = new Vector2(1f, -1f);

            Button button = go.AddComponent<Button>();
            button.targetGraphic = background;
            button.transition = Selectable.Transition.ColorTint;
            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.10f, 1.10f, 1.10f, 1f);
            colors.pressedColor = new Color(0.88f, 0.98f, 0.90f, 1f);
            colors.selectedColor = Color.white;
            colors.disabledColor = new Color(0.40f, 0.40f, 0.40f, 0.45f);
            colors.colorMultiplier = 1f;
            button.colors = colors;
            button.onClick.AddListener(delegate { if (action != null) action(); });

            GameObject textGo = CreateBrowserText("Label", go.transform,
                Vector2.zero, new Vector2(width, 34f), fontSize,
                new Color(0.48f, 0.74f, 0.62f, 1f), FontStyles.Bold,
                TextAlignmentOptions.Center);
            text = textGo.GetComponent<TMP_Text>();
            return button;
        }

        private static void CreateBrowserHeaderNavigationControls()
        {
            _browserFavoriteButton = CreateBrowserHeaderActionButton(
                "FavoriteButton", 412f, 54f, 11f,
                out _browserFavoriteButtonBackground, out _browserFavoriteButtonText,
                delegate { ToggleBrowserFavorite(_inspectorItemId); });

            _browserBackButton = CreateBrowserHeaderActionButton(
                "BackButton", 474f, 94f, 12f,
                out _browserBackButtonBackground, out _browserBackButtonText,
                delegate { NavigateBrowserBack(); });
            CreateBrowserFavoriteInterfaceIcon();
            CreateBrowserBackInterfaceIcon();
        }

        private static void UpdateBrowserHeaderActions()
        {
            bool canFavorite = _inspectorOpen && !string.IsNullOrEmpty(_inspectorItemId) &&
                IsKnownItemId(_inspectorItemId);
            bool favorite = canFavorite && IsBrowserFavorite(_inspectorItemId);

            SetBrowserInteractableIfChanged(_browserFavoriteButton, canFavorite);
            if (_browserFavoriteButtonText != null)
            {
                SetBrowserTextIfChanged(_browserFavoriteButtonText, NormalizeModUiText(Ui("catalog.favorite.short")));
                SetBrowserGraphicColorIfChanged(_browserFavoriteButtonText, favorite
                    ? new Color(0.96f, 0.91f, 0.55f, 1f)
                    : new Color(0.48f, 0.74f, 0.62f, 1f));
            }
            if (_browserFavoriteButtonBackground != null)
                SetBrowserGraphicColorIfChanged(_browserFavoriteButtonBackground, favorite
                    ? new Color(0.105f, 0.205f, 0.120f, 0.98f)
                    : new Color(0.017f, 0.047f, 0.040f, 0.55f));

            bool canBack = _inspectorOpen && BrowserNavigation.History.Count > 0;
            SetBrowserInteractableIfChanged(_browserBackButton, canBack);
            if (_browserBackButtonText != null)
            {
                SetBrowserTextIfChanged(_browserBackButtonText, NormalizeModUiText(Ui("ui.back")));
                SetBrowserGraphicColorIfChanged(_browserBackButtonText, canBack
                    ? new Color(0.56f, 0.80f, 0.66f, 1f)
                    : new Color(0.31f, 0.45f, 0.39f, 0.72f));
            }
            if (_browserBackButtonBackground != null)
                SetBrowserGraphicColorIfChanged(_browserBackButtonBackground, canBack
                    ? new Color(0.025f, 0.070f, 0.056f, 0.82f)
                    : new Color(0.012f, 0.032f, 0.028f, 0.38f));
            UpdateBrowserHeaderInterfaceIconStyle(favorite, canBack);
        }

        private static Button CreateBrowserCatalogControlButton(
            string name,
            Vector2 position,
            Vector2 size,
            float fontSize,
            Action action,
            out TMP_Text text)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(_browserCatalogPanel.transform, false);
            RectTransform rt = go.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = position;
            rt.sizeDelta = size;

            Image background = go.AddComponent<Image>();
            background.color = new Color(0.020f, 0.060f, 0.050f, 0.98f);
            background.raycastTarget = true;
            Button button = go.AddComponent<Button>();
            button.targetGraphic = background;
            button.transition = Selectable.Transition.ColorTint;
            button.onClick.AddListener(delegate { if (action != null) action(); });

            GameObject textGo = CreateBrowserText("Label", go.transform,
                Vector2.zero, size, fontSize,
                new Color(0.48f, 0.74f, 0.62f, 1f), FontStyles.Bold,
                TextAlignmentOptions.Center);
            text = textGo.GetComponent<TMP_Text>();
            return button;
        }

        private static void CreateBrowserCatalogButton()
        {
            GameObject go = new GameObject("CatalogButton");
            go.transform.SetParent(_inspectorRoot.transform, false);
            RectTransform rt = go.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(642f, -72f);
            rt.sizeDelta = new Vector2(76f, 34f);

            _browserCatalogButtonBackground = go.AddComponent<Image>();
            _browserCatalogButtonBackground.color = new Color(0.105f, 0.165f, 0.115f, 0.98f);
            _browserCatalogButtonBackground.raycastTarget = true;
            Button button = go.AddComponent<Button>();
            button.targetGraphic = _browserCatalogButtonBackground;
            button.transition = Selectable.Transition.ColorTint;
            button.onClick.AddListener(delegate { ToggleBrowserCatalog(); });

            GameObject textGo = CreateBrowserText("Label", go.transform,
                Vector2.zero, new Vector2(76f, 34f), 10f,
                new Color(0.92f, 0.94f, 0.78f, 1f), FontStyles.Bold,
                TextAlignmentOptions.Center);
            _browserCatalogButtonText = textGo.GetComponent<TMP_Text>();
            CreateBrowserCatalogLauncherInterfaceIcon();
        }

        private static void CreateBrowserCatalogUi()
        {
            _browserCatalogPanel = new GameObject("ItemCatalogPanel");
            _browserCatalogPanel.transform.SetParent(_inspectorRoot.transform, false);
            RectTransform panelRt = _browserCatalogPanel.AddComponent<RectTransform>();
            panelRt.anchorMin = new Vector2(0f, 1f);
            panelRt.anchorMax = new Vector2(0f, 1f);
            panelRt.pivot = new Vector2(0f, 1f);
            panelRt.anchoredPosition = new Vector2(18f, -109f);
            panelRt.sizeDelta = new Vector2(700f, 478f);

            Image bg = _browserCatalogPanel.AddComponent<Image>();
            bg.color = new Color(0.007f, 0.026f, 0.022f, 0.997f);
            bg.raycastTarget = true;
            Outline outline = _browserCatalogPanel.AddComponent<Outline>();
            outline.effectColor = new Color(0.32f, 0.69f, 0.54f, 0.96f);
            outline.effectDistance = new Vector2(1f, -1f);

            float scopeWidth = 226f;
            for (int i = 0; i < BrowserCatalogScopeCount; i++)
            {
                int capturedScope = i;
                TMP_Text scopeText;
                Button scopeButton = CreateBrowserCatalogControlButton(
                    "Scope_" + i.ToString(CultureInfo.InvariantCulture),
                    new Vector2(5f + i * (scopeWidth + 4f), -5f),
                    new Vector2(scopeWidth, 30f), 10.5f,
                    delegate { SetBrowserCatalogScope(capturedScope); },
                    out scopeText);
                scopeButton.transition = Selectable.Transition.None;
                BrowserCatalogScopeButtons[i] = scopeButton;
                BrowserCatalogScopeTexts[i] = scopeText;
                CreateBrowserCatalogScopeInterfaceIcon(i);
            }

            // Full localized category names need a two-row grid. The variable widths
            // reserve extra room for ARMOR & EQUIPMENT / БРОНЯ И ЭКИПИРОВКА while
            // preserving the existing 700 px catalog panel.
            float[] categoryX = new float[] { 5f, 139f, 263f, 447f, 561f, 5f, 189f, 328f, 532f };
            float[] categoryWidth = new float[] { 130f, 120f, 180f, 110f, 134f, 180f, 135f, 200f, 163f };
            for (int i = 0; i < BrowserCatalogCategoryCount; i++)
            {
                int capturedCategory = i;
                TMP_Text categoryText;
                Button categoryButton = CreateBrowserCatalogControlButton(
                    "Category_" + i.ToString(CultureInfo.InvariantCulture),
                    new Vector2(categoryX[i], i < 5 ? -40f : -75f),
                    new Vector2(categoryWidth[i], 30f), 10f,
                    delegate { SetBrowserCatalogCategory(capturedCategory); },
                    out categoryText);
                categoryButton.transition = Selectable.Transition.None;
                BrowserCatalogCategoryButtons[i] = categoryButton;
                BrowserCatalogCategoryTexts[i] = categoryText;
                CreateBrowserCatalogCategoryInterfaceIcon(i);
            }

            _browserCatalogDataFilterButton = CreateBrowserCatalogControlButton(
                "DataFilter", new Vector2(5f, -110f), new Vector2(205f, 31f), 10.5f,
                CycleBrowserCatalogDataFilter, out _browserCatalogDataFilterText);
            _browserCatalogSortButton = CreateBrowserCatalogControlButton(
                "SortMode", new Vector2(214f, -110f), new Vector2(170f, 31f), 10.5f,
                CycleBrowserCatalogSortMode, out _browserCatalogSortText);
            _browserCatalogDirectionButton = CreateBrowserCatalogControlButton(
                "SortDirection", new Vector2(388f, -110f), new Vector2(135f, 31f), 10f,
                ToggleBrowserCatalogSortDirection, out _browserCatalogDirectionText);
            _browserCatalogResetButton = CreateBrowserCatalogControlButton(
                "Reset", new Vector2(527f, -110f), new Vector2(168f, 31f), 9.5f,
                ResetBrowserCatalogFiltersOrHistory, out _browserCatalogResetText);
            CreateBrowserCatalogToolbarInterfaceIcons();

            GameObject headerGo = CreateBrowserText("CatalogHeader", _browserCatalogPanel.transform,
                new Vector2(8f, -145f), new Vector2(676f, 28f), 12f,
                new Color(0.72f, 0.84f, 0.62f, 1f), FontStyles.Bold,
                TextAlignmentOptions.MidlineLeft);
            _browserCatalogHeaderText = headerGo.GetComponent<TMP_Text>();
            _browserCatalogHeaderText.enableWordWrapping = false;
            _browserCatalogHeaderText.overflowMode = TextOverflowModes.Truncate;

            for (int i = 0; i < BrowserCatalogVisibleRows; i++)
            {
                int capturedRow = i;
                GameObject row = new GameObject("CatalogRow_" + i.ToString(CultureInfo.InvariantCulture));
                row.transform.SetParent(_browserCatalogPanel.transform, false);
                RectTransform rrt = row.AddComponent<RectTransform>();
                rrt.anchorMin = new Vector2(0f, 1f);
                rrt.anchorMax = new Vector2(0f, 1f);
                rrt.pivot = new Vector2(0f, 1f);
                rrt.anchoredPosition = new Vector2(5f, -(175f + i * 33f));
                rrt.sizeDelta = new Vector2(678f, 31f);
                Image rbg = row.AddComponent<Image>();
                rbg.color = i % 2 == 0
                    ? new Color(0.022f, 0.064f, 0.052f, 0.98f)
                    : new Color(0.013f, 0.043f, 0.037f, 0.98f);
                Button rb = row.AddComponent<Button>();
                rb.targetGraphic = rbg;
                rb.transition = Selectable.Transition.ColorTint;
                rb.onClick.AddListener(delegate
                {
                    string id = BrowserCatalogRowItemIds[capturedRow];
                    if (!string.IsNullOrEmpty(id)) SelectBrowserCatalogItem(id);
                });

                GameObject iconGo = new GameObject("Icon");
                iconGo.transform.SetParent(row.transform, false);
                RectTransform irt = iconGo.AddComponent<RectTransform>();
                irt.anchorMin = new Vector2(0f, 0.5f);
                irt.anchorMax = new Vector2(0f, 0.5f);
                irt.pivot = new Vector2(0f, 0.5f);
                irt.anchoredPosition = new Vector2(7f, 0f);
                irt.sizeDelta = new Vector2(24f, 24f);
                Image icon = iconGo.AddComponent<Image>();
                icon.preserveAspect = true;
                icon.raycastTarget = false;
                AttachBrowserItemTooltipTarget(icon);
                BrowserCatalogRowIcons[i] = icon;

                GameObject nameGo = CreateBrowserText("Name", row.transform,
                    new Vector2(38f, 0f), new Vector2(390f, 31f), 15f,
                    new Color(0.68f, 0.87f, 0.68f, 1f), FontStyles.Normal,
                    TextAlignmentOptions.MidlineLeft);
                BrowserCatalogRowNames[i] = nameGo.GetComponent<TMP_Text>();
                BrowserCatalogRowNames[i].enableWordWrapping = false;
                BrowserCatalogRowNames[i].overflowMode = TextOverflowModes.Truncate;

                GameObject idGo = CreateBrowserText("Metadata", row.transform,
                    new Vector2(432f, 0f), new Vector2(188f, 31f), 9.5f,
                    new Color(0.30f, 0.53f, 0.47f, 1f), FontStyles.Normal,
                    TextAlignmentOptions.MidlineRight);
                BrowserCatalogRowIds[i] = idGo.GetComponent<TMP_Text>();
                BrowserCatalogRowIds[i].enableWordWrapping = false;
                BrowserCatalogRowIds[i].overflowMode = TextOverflowModes.Truncate;

                GameObject favoriteGo = new GameObject("Favorite");
                favoriteGo.transform.SetParent(row.transform, false);
                RectTransform favoriteRt = favoriteGo.AddComponent<RectTransform>();
                favoriteRt.anchorMin = new Vector2(0f, 0.5f);
                favoriteRt.anchorMax = new Vector2(0f, 0.5f);
                favoriteRt.pivot = new Vector2(0f, 0.5f);
                favoriteRt.anchoredPosition = new Vector2(628f, 0f);
                favoriteRt.sizeDelta = new Vector2(44f, 25f);
                Image favoriteBg = favoriteGo.AddComponent<Image>();
                favoriteBg.color = new Color(0.025f, 0.070f, 0.056f, 0.96f);
                Button favoriteButton = favoriteGo.AddComponent<Button>();
                favoriteButton.targetGraphic = favoriteBg;
                favoriteButton.transition = Selectable.Transition.ColorTint;
                favoriteButton.onClick.AddListener(delegate
                {
                    string id = BrowserCatalogRowItemIds[capturedRow];
                    if (!string.IsNullOrEmpty(id)) ToggleBrowserFavorite(id);
                });
                GameObject favoriteTextGo = CreateBrowserText("Label", favoriteGo.transform,
                    Vector2.zero, new Vector2(44f, 25f), 9f,
                    new Color(0.46f, 0.72f, 0.61f, 1f), FontStyles.Bold,
                    TextAlignmentOptions.Center);
                BrowserCatalogRowFavoriteButtons[i] = favoriteButton;
                BrowserCatalogRowFavoriteBackgrounds[i] = favoriteBg;
                BrowserCatalogRowFavoriteTexts[i] = favoriteTextGo.GetComponent<TMP_Text>();
                CreateBrowserCatalogRowFavoriteInterfaceIcon(i);
                BrowserCatalogRowRoots[i] = row;
            }

            _browserCatalogScrollbar = CreateBrowserPageScrollbar(
                "CatalogScrollbar", _browserCatalogPanel.transform,
                new Vector2(687f, -175f), new Vector2(8f, 262f),
                HandleBrowserCatalogScrollbar);

            GameObject scrollGo = CreateBrowserText("CatalogScrollStatus", _browserCatalogPanel.transform,
                new Vector2(8f, -441f), new Vector2(676f, 27f), 12f,
                new Color(0.48f, 0.72f, 0.62f, 1f), FontStyles.Normal,
                TextAlignmentOptions.Center);
            _browserCatalogScrollText = scrollGo.GetComponent<TMP_Text>();
            _browserCatalogPanel.SetActive(false);
        }

        private static void RenderBrowserCatalogRows()
        {
            int total = BrowserCatalogFilteredItemIds.Count;
            int maxOffset = Math.Max(0, total - BrowserCatalogVisibleRows);
            _browserCatalogScrollOffset = Mathf.Clamp(_browserCatalogScrollOffset, 0, maxOffset);

            if (_browserCatalogHeaderText != null)
            {
                string header = GetBrowserCatalogScopeLabel(_browserCatalogScope, false) + "  /  " +
                    GetBrowserCatalogCategoryLabel(_browserCatalogCategory, false) + "  /  " +
                    GetBrowserCatalogDataFilterLabel(_browserCatalogDataFilter) + "  •  " +
                    total.ToString(CultureInfo.InvariantCulture);
                SetBrowserTextIfChanged(_browserCatalogHeaderText, NormalizeModUiText(header));
            }

            int start = _browserCatalogScrollOffset;
            for (int i = 0; i < BrowserCatalogVisibleRows; i++)
            {
                GameObject row = BrowserCatalogRowRoots[i];
                int index = start + i;
                if (row == null) continue;
                if (index >= total)
                {
                    BrowserCatalogRowItemIds[i] = string.Empty;
                    if (BrowserCatalogRowIcons[i] != null)
                        SetBrowserItemTooltipTarget(BrowserCatalogRowIcons[i], string.Empty, false);
                    SetBrowserActiveIfChanged(row, false);
                    continue;
                }

                string itemId = BrowserCatalogFilteredItemIds[index];
                BrowserCatalogRowItemIds[i] = itemId;
                string name;
                if (!BrowserSearchDisplayNames.TryGetValue(itemId, out name) || string.IsNullOrEmpty(name))
                    name = HumanizeIdentifier(itemId);
                if (BrowserCatalogRowNames[i] != null)
                    SetBrowserTextIfChanged(BrowserCatalogRowNames[i], NormalizeGameText(name));
                if (BrowserCatalogRowIds[i] != null)
                    SetBrowserTextIfChanged(BrowserCatalogRowIds[i], NormalizeModUiText(GetBrowserCatalogRowMetadata(itemId)));
                if (BrowserCatalogRowIcons[i] != null)
                {
                    Sprite nextIcon = TryResolveItemSmallIcon(itemId);
                    SetBrowserImageSpriteIfChanged(BrowserCatalogRowIcons[i], nextIcon);
                    SetBrowserImageEnabledIfChanged(BrowserCatalogRowIcons[i], nextIcon != null);
                    SetBrowserItemTooltipTarget(
                        BrowserCatalogRowIcons[i], itemId, BrowserCatalogRowIcons[i].enabled);
                }

                bool favorite = IsBrowserFavorite(itemId);
                if (BrowserCatalogRowFavoriteTexts[i] != null)
                {
                    SetBrowserTextIfChanged(BrowserCatalogRowFavoriteTexts[i], NormalizeModUiText(Ui("catalog.favorite.short")));
                    SetBrowserGraphicColorIfChanged(BrowserCatalogRowFavoriteTexts[i], favorite
                        ? new Color(0.96f, 0.91f, 0.55f, 1f)
                        : new Color(0.42f, 0.66f, 0.56f, 1f));
                }
                if (BrowserCatalogRowFavoriteBackgrounds[i] != null)
                    SetBrowserGraphicColorIfChanged(BrowserCatalogRowFavoriteBackgrounds[i], favorite
                        ? new Color(0.105f, 0.205f, 0.120f, 0.98f)
                        : new Color(0.025f, 0.070f, 0.056f, 0.96f));
                UpdateBrowserCatalogRowFavoriteInterfaceIconStyle(i, favorite);
                SetBrowserActiveIfChanged(row, true);
            }

            if (_browserCatalogScrollText != null)
            {
                int first = total <= 0 ? 0 : _browserCatalogScrollOffset + 1;
                int last = Math.Min(total, _browserCatalogScrollOffset + BrowserCatalogVisibleRows);
                SetBrowserTextIfChanged(_browserCatalogScrollText, NormalizeModUiText(
                    Ui("ui.rows_visible") + " " +
                    first.ToString(CultureInfo.InvariantCulture) + "-" +
                    last.ToString(CultureInfo.InvariantCulture) + " / " +
                    total.ToString(CultureInfo.InvariantCulture) +
                    Ui("ui.wheel_pgup_pgdn")));
            }

            SyncBrowserContinuousScrollbar(
                _browserCatalogScrollbar, total, BrowserCatalogVisibleRows, _browserCatalogScrollOffset);
            UpdateBrowserCatalogControls();
        }

        private static void UpdateBrowserCatalogControls()
        {
            for (int i = 0; i < BrowserCatalogScopeCount; i++)
            {
                BrowserCatalogScope scope = (BrowserCatalogScope)i;
                string label = GetBrowserCatalogScopeLabel(scope, false);
                if (scope == BrowserCatalogScope.Favorites)
                    label += " " + BrowserFavoriteItemIds.Count.ToString(CultureInfo.InvariantCulture);
                else if (scope == BrowserCatalogScope.Recent)
                    label += " " + BrowserRecentItemIds.Count.ToString(CultureInfo.InvariantCulture);
                SetBrowserCatalogToggleStyle(
                    BrowserCatalogScopeButtons[i], BrowserCatalogScopeTexts[i],
                    scope == _browserCatalogScope, label);
                UpdateBrowserCatalogScopeInterfaceIconStyle(i, scope == _browserCatalogScope);
            }

            for (int i = 0; i < BrowserCatalogCategoryCount; i++)
            {
                SetBrowserCatalogToggleStyle(
                    BrowserCatalogCategoryButtons[i], BrowserCatalogCategoryTexts[i],
                    i == _browserCatalogCategory, GetBrowserCatalogCategoryLabel(i, false));
                UpdateBrowserCatalogCategoryInterfaceIconStyle(i, i == _browserCatalogCategory);
            }

            if (_browserCatalogDataFilterText != null)
                SetBrowserTextIfChanged(_browserCatalogDataFilterText, NormalizeModUiText(
                    Ui("catalog.label.data") + ": " +
                    GetBrowserCatalogDataFilterLabel(_browserCatalogDataFilter) + "  ›"));
            if (_browserCatalogSortText != null)
                SetBrowserTextIfChanged(_browserCatalogSortText, NormalizeModUiText(
                    Ui("catalog.label.sort") + ": " +
                    (_browserCatalogScope == BrowserCatalogScope.Recent
                        ? Ui("catalog.sort.recent")
                        : GetBrowserCatalogSortLabel(_browserCatalogSortMode) + "  ›")));
            if (_browserCatalogDirectionText != null)
                SetBrowserTextIfChanged(_browserCatalogDirectionText, NormalizeModUiText(
                    _browserCatalogScope == BrowserCatalogScope.Recent
                        ? Ui("catalog.sort.fixed")
                        : (_browserCatalogSortDescending ? Ui("catalog.sort.desc") : Ui("catalog.sort.asc"))));
            if (_browserCatalogResetText != null)
                SetBrowserTextIfChanged(_browserCatalogResetText, NormalizeModUiText(
                    _browserCatalogScope == BrowserCatalogScope.Recent
                        ? Ui("catalog.clear.history")
                        : Ui("catalog.reset")));

            bool sortable = _browserCatalogScope != BrowserCatalogScope.Recent;
            SetBrowserInteractableIfChanged(_browserCatalogSortButton, sortable);
            SetBrowserInteractableIfChanged(_browserCatalogDirectionButton, sortable);
            if (_browserCatalogSortText != null)
                SetBrowserGraphicColorIfChanged(_browserCatalogSortText, sortable
                    ? new Color(0.48f, 0.74f, 0.62f, 1f)
                    : new Color(0.31f, 0.45f, 0.39f, 0.72f));
            if (_browserCatalogDirectionText != null)
                SetBrowserGraphicColorIfChanged(_browserCatalogDirectionText, sortable
                    ? new Color(0.48f, 0.74f, 0.62f, 1f)
                    : new Color(0.31f, 0.45f, 0.39f, 0.72f));
            UpdateBrowserCatalogToolbarInterfaceIconStyle(sortable);
        }

        private static void SetBrowserCatalogToggleStyle(
            Button button, TMP_Text text, bool selected, string label)
        {
            if (text != null)
            {
                SetBrowserTextIfChanged(text, NormalizeModUiText(label));
                SetBrowserGraphicColorIfChanged(text, selected
                    ? new Color(0.90f, 0.90f, 0.62f, 1f)
                    : new Color(0.48f, 0.74f, 0.62f, 1f));
            }
            if (button != null && button.targetGraphic != null)
                SetBrowserGraphicColorIfChanged(button.targetGraphic, selected
                    ? new Color(0.060f, 0.145f, 0.100f, 0.98f)
                    : new Color(0.020f, 0.060f, 0.050f, 0.98f));
        }

        private static void UpdateBrowserCatalogButtonStyle()
        {
            if (_browserCatalogButtonText != null)
            {
                SetBrowserTextIfChanged(_browserCatalogButtonText, NormalizeModUiText(Ui("ui.catalog")));
                SetBrowserGraphicColorIfChanged(_browserCatalogButtonText, _browserCatalogOpen
                    ? new Color(0.96f, 0.97f, 0.84f, 1f)
                    : new Color(0.92f, 0.94f, 0.78f, 1f));
            }
            if (_browserCatalogButtonBackground != null)
                SetBrowserGraphicColorIfChanged(_browserCatalogButtonBackground, _browserCatalogOpen
                    ? new Color(0.155f, 0.285f, 0.185f, 0.99f)
                    : new Color(0.105f, 0.165f, 0.115f, 0.98f));
            UpdateBrowserCatalogLauncherInterfaceIconStyle();
        }

        private static string GetBrowserCatalogCategoryLabel(int category, bool compact)
        {
            category = Math.Max(0, Math.Min(BrowserCatalogCategoryCount - 1, category));
            if (compact)
            {
                switch (category)
                {
                    case 0: return Ui("catalog.compact.all");
                    case 1: return Ui("catalog.compact.weapons");
                    case 2: return Ui("catalog.compact.armor");
                    case 3: return Ui("catalog.compact.ammo");
                    case 4: return Ui("catalog.compact.implants");
                    case 5: return Ui("catalog.compact.consumables");
                    case 6: return Ui("catalog.compact.chips");
                    case 7: return Ui("catalog.compact.containers");
                    default: return Ui("catalog.compact.other");
                }
            }

            switch (category)
            {
                case 0: return Ui("catalog.full.all");
                case 1: return Ui("catalog.full.weapons");
                case 2: return Ui("catalog.full.armor");
                case 3: return Ui("catalog.full.ammo");
                case 4: return Ui("catalog.full.implants");
                case 5: return Ui("catalog.full.consumables");
                case 6: return Ui("catalog.full.chips");
                case 7: return Ui("catalog.full.containers");
                default: return Ui("catalog.full.other");
            }
        }
    }
}
