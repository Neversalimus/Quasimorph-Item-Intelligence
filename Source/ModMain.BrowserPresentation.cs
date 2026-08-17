using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Runtime.InteropServices;
using System.Threading;
using MGSC;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ItemIntelligence
{
    public static partial class ModMain
    {
        private static TMP_Text _inspectorItemIdText;

        // Test13 general presentation owner: browser shell, stable geometry, chrome and
        // tab rows. Catalog-specific presentation lives in BrowserCatalogPresentation.

        private static string GetBrowserCloseButtonLabel()
        {
            // The close button uses the localized action word only. The assigned hotkey
            // is already shown elsewhere in QII and keeping it out of this compact header
            // control avoids the duplicated "[X] ... X" look. The right-side glyph is
            // semantic (close), not a key binding.
            string text = NormalizeModUiText(HotkeyUi("chrome.close"));
            int bracket = text.IndexOf(']');
            if (bracket >= 0 && bracket + 1 < text.Length)
                text = text.Substring(bracket + 1).TrimStart();
            return text;
        }

        private static void EnsureInspectorPanel()
        {
            if (_inspectorCanvas == null || _inspectorCanvasObject == null)
            {
                GameObject existingCanvas = GameObject.Find("QII_ItemInspectorCanvas");
                if (existingCanvas != null)
                {
                    _inspectorCanvasObject = existingCanvas;
                    _inspectorCanvas = existingCanvas.GetComponent<Canvas>();
                    _inspectorGraphicRaycaster = existingCanvas.GetComponent<GraphicRaycaster>();
                }

                if (_inspectorCanvas == null)
                {
                    _inspectorCanvasObject = new GameObject("QII_ItemInspectorCanvas");
                    _inspectorCanvas = _inspectorCanvasObject.AddComponent<Canvas>();
                    _inspectorCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
                    _inspectorCanvas.sortingOrder = 32000;

                    CanvasScaler scaler = _inspectorCanvasObject.AddComponent<CanvasScaler>();
                    scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                    scaler.referenceResolution = new Vector2(1920f, 1080f);
                    scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
                    scaler.matchWidthOrHeight = 0.5f;

                    _inspectorGraphicRaycaster = _inspectorCanvasObject.AddComponent<GraphicRaycaster>();
                }
            }

            if (_inspectorCanvas == null)
                throw new InvalidOperationException("Item Intelligence overlay canvas could not be created.");

            CaptureInspectorStyle(_activeTooltip);

            // A dedicated full-screen raycast blocker sits below the inspector panel
            // but above every vanilla canvas. This prevents inventory cells behind F2
            // from receiving hover/click/drag events while Item Intelligence is open.
            if (_inspectorInputBlocker == null)
            {
                _inspectorInputBlocker = new GameObject("QII_ItemInspectorInputBlocker");
                _inspectorInputBlocker.transform.SetParent(_inspectorCanvas.transform, false);

                RectTransform blockerRect = _inspectorInputBlocker.AddComponent<RectTransform>();
                blockerRect.anchorMin = Vector2.zero;
                blockerRect.anchorMax = Vector2.one;
                blockerRect.offsetMin = Vector2.zero;
                blockerRect.offsetMax = Vector2.zero;

                Image blockerImage = _inspectorInputBlocker.AddComponent<Image>();
                blockerImage.color = new Color(0f, 0f, 0f, 0.001f);
                blockerImage.raycastTarget = true;

                Button blockerButton = _inspectorInputBlocker.AddComponent<Button>();
                blockerButton.transition = Selectable.Transition.None;
                blockerButton.targetGraphic = blockerImage;
                blockerButton.interactable = true;

                _inspectorInputBlocker.SetActive(false);
            }

            if (_inspectorRoot != null)
            {
                PositionInspectorPanel();
                return;
            }

            ResetBrowserInterfaceIconPresentation();
            _inspectorRoot = new GameObject("QII_ItemBrowser");
            _inspectorRoot.transform.SetParent(_inspectorCanvas.transform, false);
            _inspectorRoot.transform.localScale = Vector3.one;
            _inspectorRoot.transform.SetAsLastSibling();
            _inspectorRoot.transform.localRotation = Quaternion.identity;
            _inspectorRoot.transform.SetAsLastSibling();

            _inspectorRect = _inspectorRoot.AddComponent<RectTransform>();
            _inspectorRect.anchorMin = new Vector2(0f, 0.5f);
            _inspectorRect.anchorMax = new Vector2(0f, 0.5f);
            _inspectorRect.pivot = new Vector2(0f, 0.5f);
            _inspectorRect.sizeDelta = new Vector2(736f, 870f);
            _inspectorRect.anchoredPosition = new Vector2(22f, 0f);

            Image background = _inspectorRoot.AddComponent<Image>();
            background.color = new Color(0.010f, 0.027f, 0.023f, 0.985f);
            background.raycastTarget = true;
            CanvasGroup browserCanvasGroup = _inspectorRoot.AddComponent<CanvasGroup>();
            browserCanvasGroup.interactable = true;
            browserCanvasGroup.blocksRaycasts = true;
            browserCanvasGroup.ignoreParentGroups = true;

            Outline outline = _inspectorRoot.AddComponent<Outline>();
            outline.effectColor = new Color(0.48f, 0.77f, 0.59f, 0.96f);
            outline.effectDistance = new Vector2(1f, -1f);

            // Header
            CreateBrowserPreviewIcon();

            GameObject titleGo = CreateBrowserText("Title", _inspectorRoot.transform,
                new Vector2(78f, -5f), new Vector2(326f, 36f),
                27f, new Color(0.74f, 0.88f, 0.65f, 1f), FontStyles.Bold,
                TextAlignmentOptions.MidlineLeft);
            _inspectorTitle = titleGo.GetComponent<TMP_Text>();
            _inspectorTitle.enableWordWrapping = false;
            // The Item-ID micro update reduced this rect too aggressively: TMP can
            // vertically truncate the entire 27px title when only 32px are available.
            // Keep the compact two-line header, but let TMP shrink only if the current
            // font metrics/localized name need it. This preserves the 27px target size
            // while making the item name fail-visible instead of disappearing.
            _inspectorTitle.enableAutoSizing = true;
            _inspectorTitle.fontSizeMin = 18f;
            _inspectorTitle.fontSizeMax = 27f;
            _inspectorTitle.overflowMode = TextOverflowModes.Truncate;

            CreateBrowserItemIdLine();
            CreateBrowserHeaderNavigationControls();

            GameObject closeButtonGo = new GameObject("CloseButton");
            closeButtonGo.transform.SetParent(_inspectorRoot.transform, false);
            RectTransform closeButtonRt = closeButtonGo.AddComponent<RectTransform>();
            closeButtonRt.anchorMin = new Vector2(0f, 1f);
            closeButtonRt.anchorMax = new Vector2(0f, 1f);
            closeButtonRt.pivot = new Vector2(0f, 1f);
            closeButtonRt.anchoredPosition = new Vector2(576f, -14f);
            closeButtonRt.sizeDelta = new Vector2(126f, 34f);

            Image closeBg = closeButtonGo.AddComponent<Image>();
            // Header-control treatment: deliberately flatter and darker than a normal
            // action button so it reads as part of the QII chrome, not a sticker.
            closeBg.color = new Color(0.017f, 0.047f, 0.040f, 0.38f);
            closeBg.raycastTarget = true;

            Outline closeOutline = closeButtonGo.AddComponent<Outline>();
            closeOutline.effectColor = new Color(0.25f, 0.53f, 0.44f, 0.24f);
            closeOutline.effectDistance = new Vector2(1f, -1f);

            Button closeButton = closeButtonGo.AddComponent<Button>();
            closeButton.targetGraphic = closeBg;
            closeButton.transition = Selectable.Transition.ColorTint;
            ColorBlock closeColors = closeButton.colors;
            closeColors.normalColor = Color.white;
            closeColors.highlightedColor = new Color(1.08f, 1.08f, 1.08f, 1f);
            closeColors.pressedColor = new Color(0.90f, 0.98f, 0.92f, 1f);
            closeColors.selectedColor = Color.white;
            closeColors.disabledColor = new Color(0.45f, 0.45f, 0.45f, 0.5f);
            closeColors.colorMultiplier = 1f;
            closeButton.colors = closeColors;
            closeButton.onClick.AddListener(delegate { CloseInspector(); });

            // The localized action remains the authoritative label. The optional
            // procedural close glyph is a non-raycast child and the MCM switch restores
            // this exact text-only rectangle without changing click/Esc/hotkey behavior.
            GameObject closeTextGo = CreateBrowserText("CloseHint", closeButtonGo.transform,
                new Vector2(10f, -1f), new Vector2(106f, 32f),
                14f, new Color(0.43f, 0.68f, 0.59f, 0.96f), FontStyles.Normal,
                TextAlignmentOptions.MidlineLeft);
            _browserCloseText = closeTextGo.GetComponent<TMP_Text>();
            _browserCloseText.text = GetBrowserCloseButtonLabel();
            CreateBrowserCloseInterfaceIcon(closeButtonGo.transform);

            CreateBrowserRule(_inspectorRoot.transform, 64f);

            CreateBrowserSearchUi();

            GameObject statsGo = CreateBrowserText("Stats", _inspectorRoot.transform,
                new Vector2(18f, -116f), new Vector2(700f, 34f),
                15f, new Color(0.43f, 0.69f, 0.59f, 1f), FontStyles.Normal,
                TextAlignmentOptions.MidlineLeft);
            _browserStatsText = statsGo.GetComponent<TMP_Text>();

            // Tabs. They are constant objects and are only recolored/relabelled.
            string[] tabs = new string[]
            {
                Ui("tab.overview.short"), Ui("tab.magnum.short"), Ui("tab.recipes.short"), Ui("tab.trade.short"),
                Ui("tab.ammo.short"), Ui("tab.factions.short"), Ui("tab.loot.short")
            };
            float tabX = 16f;
            float tabWidth = 96f;
            for (int i = 0; i < BrowserTabCount; i++)
            {
                int capturedTab = i;
                GameObject tab = new GameObject("Tab_" + i.ToString(CultureInfo.InvariantCulture));
                tab.transform.SetParent(_inspectorRoot.transform, false);
                RectTransform tabRt = tab.AddComponent<RectTransform>();
                tabRt.anchorMin = new Vector2(0f, 1f);
                tabRt.anchorMax = new Vector2(0f, 1f);
                tabRt.pivot = new Vector2(0f, 1f);
                tabRt.anchoredPosition = new Vector2(tabX + i * (tabWidth + 4f), -156f);
                tabRt.sizeDelta = new Vector2(tabWidth, 38f);

                Image tabBg = tab.AddComponent<Image>();
                tabBg.color = new Color(0.025f, 0.065f, 0.055f, 0.92f);
                BrowserTabBackgrounds[i] = tabBg;

                Button button = tab.AddComponent<Button>();
                button.transition = Selectable.Transition.None;
                button.onClick.AddListener(delegate { SetBrowserTab(capturedTab); });

                GameObject labelGo = CreateBrowserText("Label", tab.transform,
                    new Vector2(0f, 0f), new Vector2(tabWidth, 38f),
                    13f, new Color(0.44f, 0.70f, 0.60f, 1f), FontStyles.Bold,
                    TextAlignmentOptions.Center);
                TMP_Text label = labelGo.GetComponent<TMP_Text>();
                label.text = NormalizeModUiText(tabs[i]);
                BrowserTabTexts[i] = label;
                CreateBrowserTabInterfaceIcon(tab.transform, i);
            }

            CreateBrowserRule(_inspectorRoot.transform, 202f);

            // Fixed row pool. No ContentSizeFitter, VerticalLayoutGroup, Destroy or Instantiate on F2.
            float rowTop = 221f;
            float rowHeight = 39f;
            for (int i = 0; i < BrowserVisibleRows; i++)
            {
                GameObject row = new GameObject("BrowserRow_" + i.ToString(CultureInfo.InvariantCulture));
                row.transform.SetParent(_inspectorRoot.transform, false);
                RectTransform rowRt = row.AddComponent<RectTransform>();
                rowRt.anchorMin = new Vector2(0f, 1f);
                rowRt.anchorMax = new Vector2(0f, 1f);
                rowRt.pivot = new Vector2(0f, 1f);
                rowRt.anchoredPosition = new Vector2(14f, -(rowTop + i * rowHeight));
                rowRt.sizeDelta = new Vector2(708f, rowHeight - 2f);

                Image rowBg = row.AddComponent<Image>();
                rowBg.color = i % 2 == 0
                    ? new Color(0.018f, 0.050f, 0.043f, 0.45f)
                    : new Color(0.010f, 0.034f, 0.030f, 0.18f);
                rowBg.raycastTarget = true;
                BrowserRowBackground[i] = rowBg;

                int capturedRow = i;
                Outline rowOutline = row.AddComponent<Outline>();
                rowOutline.effectColor = new Color(0.42f, 0.80f, 0.59f, 0.90f);
                rowOutline.effectDistance = new Vector2(1f, -1f);
                rowOutline.enabled = false;
                BrowserRowOutlines[i] = rowOutline;

                Button rowButton = row.AddComponent<Button>();
                rowButton.transition = Selectable.Transition.ColorTint;
                rowButton.targetGraphic = rowBg;
                rowButton.interactable = false;

                ColorBlock rowColors = rowButton.colors;
                rowColors.normalColor = Color.white;
                rowColors.highlightedColor = new Color(0.72f, 1.00f, 0.78f, 1f);
                rowColors.pressedColor = new Color(1.00f, 0.88f, 0.52f, 1f);
                rowColors.selectedColor = rowColors.highlightedColor;
                rowColors.disabledColor = Color.white;
                rowColors.colorMultiplier = 1.0f;
                rowColors.fadeDuration = 0.08f;
                rowButton.colors = rowColors;

                rowButton.onClick.AddListener(delegate { HandleBrowserRowClick(capturedRow); });
                BrowserRowButtons[i] = rowButton;
                AttachBrowserWeaponModeTooltipTarget(row);

                GameObject iconGo = new GameObject("Icon");
                iconGo.transform.SetParent(row.transform, false);
                RectTransform iconRt = iconGo.AddComponent<RectTransform>();
                iconRt.anchorMin = new Vector2(0f, 0.5f);
                iconRt.anchorMax = new Vector2(0f, 0.5f);
                iconRt.pivot = new Vector2(0f, 0.5f);
                iconRt.anchoredPosition = new Vector2(8f, 0f);
                iconRt.sizeDelta = new Vector2(22f, 22f);
                Image rowIcon = iconGo.AddComponent<Image>();
                rowIcon.preserveAspect = true;
                rowIcon.raycastTarget = false;
                rowIcon.enabled = false;
                AttachBrowserItemTooltipTarget(rowIcon);
                AttachBrowserItemIconNavigation(rowIcon, capturedRow, false);

                GameObject leftGo = CreateBrowserText("Left", row.transform,
                    new Vector2(36f, 0f), new Vector2(404f, rowHeight - 2f),
                    18f, new Color(0.46f, 0.72f, 0.61f, 1f), FontStyles.Normal,
                    TextAlignmentOptions.MidlineLeft);
                GameObject rightGo = CreateBrowserText("Right", row.transform,
                    new Vector2(438f, 0f), new Vector2(224f, rowHeight - 2f),
                    16f, new Color(0.86f, 0.85f, 0.61f, 1f), FontStyles.Normal,
                    TextAlignmentOptions.MidlineRight);

                BrowserRowRoots[i] = row;
                BrowserRowLeft[i] = leftGo.GetComponent<TMP_Text>();
                AttachBrowserItemTextNavigation(BrowserRowLeft[i], capturedRow);
                BrowserRowRight[i] = rightGo.GetComponent<TMP_Text>();
                BrowserRowIcons[i] = rowIcon;
            }

            _browserPageScrollbar = CreateBrowserPageScrollbar(
                "BrowserPageScrollbar",
                _inspectorRoot.transform,
                new Vector2(724f, -221f),
                new Vector2(10f, 544f),
                HandleBrowserPageScrollbar);

            CreateBrowserRule(_inspectorRoot.transform, 775f);

            GameObject pageGo = CreateBrowserText("Page", _inspectorRoot.transform,
                new Vector2(18f, -788f), new Vector2(210f, 42f),
                15f, new Color(0.70f, 0.82f, 0.60f, 1f), FontStyles.Bold,
                TextAlignmentOptions.MidlineLeft);
            _browserPageText = pageGo.GetComponent<TMP_Text>();

            GameObject helpGo = CreateBrowserText("Help", _inspectorRoot.transform,
                new Vector2(223f, -788f), new Vector2(495f, 42f),
                13f, new Color(0.34f, 0.58f, 0.52f, 1f), FontStyles.Normal,
                TextAlignmentOptions.MidlineRight);
            _browserHelpText = helpGo.GetComponent<TMP_Text>();

            FinalizeBrowserInterfaceIconPresentation();
            UpdateBrowserChromeLocalization();
            UpdateBrowserSearchStatus();

            RefreshInspectorAnchorFromTooltip();
            PositionInspectorPanel();
            _inspectorRoot.SetActive(false);
        }

        private static void CreateBrowserSearchUi()
        {
            GameObject searchGo = new GameObject("GlobalItemSearch");
            searchGo.transform.SetParent(_inspectorRoot.transform, false);

            RectTransform searchRt = searchGo.AddComponent<RectTransform>();
            searchRt.anchorMin = new Vector2(0f, 1f);
            searchRt.anchorMax = new Vector2(0f, 1f);
            searchRt.pivot = new Vector2(0f, 1f);
            searchRt.anchoredPosition = new Vector2(18f, -72f);
            searchRt.sizeDelta = new Vector2(536f, 34f);

            Image searchBg = searchGo.AddComponent<Image>();
            searchBg.color = new Color(0.015f, 0.046f, 0.039f, 0.98f);
            searchBg.raycastTarget = true;

            Outline searchOutline = searchGo.AddComponent<Outline>();
            searchOutline.effectColor = new Color(0.25f, 0.61f, 0.50f, 0.90f);
            searchOutline.effectDistance = new Vector2(1f, -1f);

            TMP_InputField input = searchGo.AddComponent<TMP_InputField>();
            _browserSearchInput = input;
            input.lineType = TMP_InputField.LineType.SingleLine;
            input.contentType = TMP_InputField.ContentType.Standard;
            input.characterLimit = 80;
            input.targetGraphic = searchBg;

            GameObject viewportGo = new GameObject("Text Area");
            viewportGo.transform.SetParent(searchGo.transform, false);
            RectTransform viewportRt = viewportGo.AddComponent<RectTransform>();
            viewportRt.anchorMin = Vector2.zero;
            viewportRt.anchorMax = Vector2.one;
            viewportRt.offsetMin = new Vector2(10f, 2f);
            viewportRt.offsetMax = new Vector2(-10f, -2f);
            viewportGo.AddComponent<RectMask2D>();
            input.textViewport = viewportRt;
            CreateBrowserSearchInterfaceIcon(viewportRt);

            GameObject placeholderGo = new GameObject("Placeholder");
            placeholderGo.transform.SetParent(viewportGo.transform, false);
            RectTransform placeholderRt = placeholderGo.AddComponent<RectTransform>();
            placeholderRt.anchorMin = Vector2.zero;
            placeholderRt.anchorMax = Vector2.one;
            placeholderRt.offsetMin = Vector2.zero;
            placeholderRt.offsetMax = Vector2.zero;

            TextMeshProUGUI placeholder = placeholderGo.AddComponent<TextMeshProUGUI>();
            ConfigureInspectorText(placeholder, 15f, new Color(0.32f, 0.56f, 0.49f, 0.86f), FontStyles.Italic);
            placeholder.alignment = TextAlignmentOptions.MidlineLeft;
            placeholder.enableWordWrapping = false;
            placeholder.raycastTarget = false;
            input.placeholder = placeholder;

            GameObject textGo = new GameObject("Text");
            textGo.transform.SetParent(viewportGo.transform, false);
            RectTransform textRt = textGo.AddComponent<RectTransform>();
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.offsetMin = Vector2.zero;
            textRt.offsetMax = Vector2.zero;

            TextMeshProUGUI inputText = textGo.AddComponent<TextMeshProUGUI>();
            ConfigureInspectorText(inputText, 16f, new Color(0.72f, 0.88f, 0.68f, 1f), FontStyles.Normal);
            inputText.alignment = TextAlignmentOptions.MidlineLeft;
            inputText.enableWordWrapping = false;
            inputText.raycastTarget = false;
            input.textComponent = inputText;

            input.onValueChanged.AddListener(delegate(string value)
            {
                if (_browserSearchSuppressEvents) return;
                if (!string.IsNullOrEmpty(value)) CloseBrowserCatalog();
                RefreshBrowserSearchSuggestions(value);
            });

            input.onSubmit.AddListener(delegate(string value)
            {
                SubmitBrowserSearch(value);
            });

            GameObject statusGo = CreateBrowserText("SearchStatus", _inspectorRoot.transform,
                new Vector2(562f, -72f), new Vector2(74f, 34f),
                11f, new Color(0.37f, 0.63f, 0.54f, 1f), FontStyles.Normal,
                TextAlignmentOptions.MidlineRight);
            _browserSearchStatusText = statusGo.GetComponent<TMP_Text>();

            CreateBrowserCatalogButton();
        }

        private static void CreateBrowserItemIdLine()
        {
            GameObject itemIdGo = CreateBrowserText("ItemId", _inspectorRoot.transform,
                new Vector2(78f, -42f), new Vector2(326f, 16f),
                10f, new Color(0.36f, 0.59f, 0.50f, 0.96f), FontStyles.Normal,
                TextAlignmentOptions.MidlineLeft);
            _inspectorItemIdText = itemIdGo.GetComponent<TMP_Text>();
            _inspectorItemIdText.enableWordWrapping = false;
            _inspectorItemIdText.overflowMode = TextOverflowModes.Truncate;
            _inspectorItemIdText.raycastTarget = true;

            Button copyButton = itemIdGo.AddComponent<Button>();
            copyButton.targetGraphic = _inspectorItemIdText;
            copyButton.transition = Selectable.Transition.ColorTint;
            ColorBlock colors = copyButton.colors;
            colors.normalColor = new Color(0.36f, 0.59f, 0.50f, 0.96f);
            colors.highlightedColor = new Color(0.58f, 0.84f, 0.67f, 1f);
            colors.pressedColor = new Color(0.94f, 0.86f, 0.52f, 1f);
            colors.selectedColor = colors.highlightedColor;
            colors.disabledColor = new Color(0.28f, 0.38f, 0.33f, 0.72f);
            colors.colorMultiplier = 1f;
            colors.fadeDuration = 0.05f;
            copyButton.colors = colors;
            UnityEngine.UI.Navigation navigation = copyButton.navigation;
            navigation.mode = UnityEngine.UI.Navigation.Mode.None;
            copyButton.navigation = navigation;
            copyButton.onClick.AddListener(delegate
            {
                if (string.IsNullOrEmpty(_inspectorItemId)) return;
                GUIUtility.systemCopyBuffer = _inspectorItemId;
                Debug.Log("[ItemIntelligence] Item ID copied: " + _inspectorItemId + ".");
            });
        }

        private static void CreateBrowserPreviewIcon()
        {
            GameObject preview = new GameObject("ItemPreview");
            preview.transform.SetParent(_inspectorRoot.transform, false);

            RectTransform rt = preview.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(18f, -8f);
            rt.sizeDelta = new Vector2(48f, 48f);

            Image bg = preview.AddComponent<Image>();
            bg.color = new Color(0.018f, 0.055f, 0.046f, 0.92f);
            bg.raycastTarget = true;

            Outline outline = preview.AddComponent<Outline>();
            outline.effectColor = new Color(0.34f, 0.67f, 0.53f, 0.92f);
            outline.effectDistance = new Vector2(1f, -1f);

            GameObject iconGo = new GameObject("Icon");
            iconGo.transform.SetParent(preview.transform, false);
            RectTransform iconRt = iconGo.AddComponent<RectTransform>();
            iconRt.anchorMin = new Vector2(0.5f, 0.5f);
            iconRt.anchorMax = new Vector2(0.5f, 0.5f);
            iconRt.pivot = new Vector2(0.5f, 0.5f);
            iconRt.anchoredPosition = Vector2.zero;
            iconRt.sizeDelta = new Vector2(40f, 40f);

            _browserPreviewImage = iconGo.AddComponent<Image>();
            _browserPreviewImage.preserveAspect = true;
            _browserPreviewImage.raycastTarget = false;

            // Ported literally from the working LoadoutPresets R18 architecture:
            // the native handler lives on the same raycast target as the icon and the
            // EventSystem calls it normally. A separate guard only raises the tooltip
            // canvas after ItemTooltipHandler has created it.
            if (_compatTooltip)
            {
                _browserPreviewTooltipHandler =
                    preview.AddComponent<ItemTooltipHandler>();

                if (preview.GetComponent<BrowserModalTooltipLayerGuard>() == null)
                    preview.AddComponent<BrowserModalTooltipLayerGuard>();
            }
            else
            {
                _browserPreviewTooltipHandler = null;
            }
        }

        private static void CreateBrowserSearchDropdown()
        {
            _browserSearchDropdown = new GameObject("GlobalItemSearchDropdown");
            _browserSearchDropdown.transform.SetParent(_inspectorRoot.transform, false);

            RectTransform dropdownRt = _browserSearchDropdown.AddComponent<RectTransform>();
            dropdownRt.anchorMin = new Vector2(0f, 1f);
            dropdownRt.anchorMax = new Vector2(0f, 1f);
            dropdownRt.pivot = new Vector2(0f, 1f);
            dropdownRt.anchoredPosition = new Vector2(18f, -109f);
            dropdownRt.sizeDelta = new Vector2(700f, BrowserSearchVisibleRows * 35f + 8f);

            Image dropdownBg = _browserSearchDropdown.AddComponent<Image>();
            dropdownBg.color = new Color(0.007f, 0.026f, 0.022f, 0.995f);
            dropdownBg.raycastTarget = true;

            Outline dropdownOutline = _browserSearchDropdown.AddComponent<Outline>();
            dropdownOutline.effectColor = new Color(0.32f, 0.69f, 0.54f, 0.96f);
            dropdownOutline.effectDistance = new Vector2(1f, -1f);

            for (int i = 0; i < BrowserSearchVisibleRows; i++)
            {
                int captured = i;

                GameObject row = new GameObject("SearchResult_" + i.ToString(CultureInfo.InvariantCulture));
                row.transform.SetParent(_browserSearchDropdown.transform, false);

                RectTransform rowRt = row.AddComponent<RectTransform>();
                rowRt.anchorMin = new Vector2(0f, 1f);
                rowRt.anchorMax = new Vector2(0f, 1f);
                rowRt.pivot = new Vector2(0f, 1f);
                rowRt.anchoredPosition = new Vector2(4f, -(4f + i * 35f));
                rowRt.sizeDelta = new Vector2(692f, 33f);

                Image rowBg = row.AddComponent<Image>();
                rowBg.color = i % 2 == 0
                    ? new Color(0.023f, 0.065f, 0.053f, 0.98f)
                    : new Color(0.014f, 0.044f, 0.038f, 0.98f);
                rowBg.raycastTarget = true;

                Button button = row.AddComponent<Button>();
                button.targetGraphic = rowBg;
                button.transition = Selectable.Transition.ColorTint;

                ColorBlock colors = button.colors;
                colors.normalColor = Color.white;
                colors.highlightedColor = new Color(0.72f, 1.00f, 0.79f, 1f);
                colors.pressedColor = new Color(0.96f, 0.88f, 0.58f, 1f);
                colors.selectedColor = colors.highlightedColor;
                colors.disabledColor = new Color(0.55f, 0.55f, 0.55f, 0.6f);
                colors.colorMultiplier = 1f;
                colors.fadeDuration = 0.06f;
                button.colors = colors;

                button.onClick.AddListener(delegate
                {
                    string itemId = BrowserSearchRowItemIds[captured];
                    if (!string.IsNullOrEmpty(itemId))
                        SelectBrowserSearchItem(itemId);
                });

                GameObject iconGo = new GameObject("Icon");
                iconGo.transform.SetParent(row.transform, false);
                RectTransform iconRt = iconGo.AddComponent<RectTransform>();
                iconRt.anchorMin = new Vector2(0f, 0.5f);
                iconRt.anchorMax = new Vector2(0f, 0.5f);
                iconRt.pivot = new Vector2(0f, 0.5f);
                iconRt.anchoredPosition = new Vector2(8f, 0f);
                iconRt.sizeDelta = new Vector2(24f, 24f);

                Image icon = iconGo.AddComponent<Image>();
                icon.preserveAspect = true;
                icon.raycastTarget = false;
                icon.enabled = false;
                AttachBrowserItemTooltipTarget(icon);

                GameObject nameGo = CreateBrowserText("Name", row.transform,
                    new Vector2(40f, 0f), new Vector2(466f, 33f),
                    16f, new Color(0.68f, 0.87f, 0.68f, 1f), FontStyles.Normal,
                    TextAlignmentOptions.MidlineLeft);

                GameObject idGo = CreateBrowserText("Id", row.transform,
                    new Vector2(508f, 0f), new Vector2(176f, 33f),
                    11f, new Color(0.30f, 0.53f, 0.47f, 1f), FontStyles.Normal,
                    TextAlignmentOptions.MidlineRight);

                BrowserSearchRowRoots[i] = row;
                BrowserSearchRowNames[i] = nameGo.GetComponent<TMP_Text>();
                BrowserSearchRowIds[i] = idGo.GetComponent<TMP_Text>();
                BrowserSearchRowIcons[i] = icon;
                BrowserSearchRowButtons[i] = button;
                BrowserSearchRowItemIds[i] = string.Empty;

                row.SetActive(false);
            }

            _browserSearchScrollbar = CreateBrowserPageScrollbar(
                "SearchPageScrollbar",
                _browserSearchDropdown.transform,
                new Vector2(687f, -4f),
                new Vector2(8f, BrowserSearchVisibleRows * 35f - 8f),
                HandleBrowserSearchScrollbar);

            _browserSearchDropdown.SetActive(false);
        }

        private static void RenderBrowserSearchCurrentPage()
        {
            int total = BrowserSearchCurrentMatches.Count;
            int pages = Math.Max(1,
                (total + BrowserSearchVisibleRows - 1) / BrowserSearchVisibleRows);
            if (_browserSearchResultPage >= pages) _browserSearchResultPage = pages - 1;
            if (_browserSearchResultPage < 0) _browserSearchResultPage = 0;

            int start = _browserSearchResultPage * BrowserSearchVisibleRows;
            for (int i = 0; i < BrowserSearchVisibleRows; i++)
            {
                GameObject row = BrowserSearchRowRoots[i];
                if (row == null) continue;
                int matchIndex = start + i;

                if (matchIndex >= total)
                {
                    BrowserSearchRowItemIds[i] = string.Empty;
                    if (BrowserSearchRowIcons[i] != null)
                        SetBrowserItemTooltipTarget(
                            BrowserSearchRowIcons[i], string.Empty, false);
                    if (BrowserSearchRowButtons[i] != null)
                        BrowserSearchRowButtons[i].interactable = false;
                    row.SetActive(false);
                    continue;
                }

                BrowserSearchMatch match = BrowserSearchCurrentMatches[matchIndex];
                string display = match.DisplayName;
                if (string.IsNullOrEmpty(display))
                    display = HumanizeIdentifier(match.ItemId);

                BrowserSearchRowItemIds[i] = match.ItemId;
                if (BrowserSearchRowNames[i] != null)
                    BrowserSearchRowNames[i].text = NormalizeGameText(display);
                if (BrowserSearchRowIds[i] != null)
                    BrowserSearchRowIds[i].text = match.ItemId;

                Image icon = BrowserSearchRowIcons[i];
                if (icon != null)
                {
                    icon.sprite = TryResolveItemSmallIcon(match.ItemId);
                    icon.enabled = icon.sprite != null;
                    SetBrowserItemTooltipTarget(icon, match.ItemId, icon.enabled);
                }

                if (BrowserSearchRowButtons[i] != null)
                    BrowserSearchRowButtons[i].interactable = true;
                row.SetActive(true);
            }

            UpdateBrowserSearchStatus();
            SyncBrowserPageScrollbar(_browserSearchScrollbar, pages, _browserSearchResultPage);
            if (total == 0)
            {
                HideBrowserSearchDropdown();
                return;
            }

            _browserSearchDropdown.SetActive(true);
            _browserSearchDropdown.transform.SetAsLastSibling();
        }

        private static void UpdateBrowserSearchStatus()
        {
            if (_browserSearchInput != null && _browserSearchInput.placeholder != null)
            {
                TMP_Text placeholder = _browserSearchInput.placeholder as TMP_Text;
                if (placeholder != null)
                    placeholder.text = NormalizeModUiText(Ui(ModderMode ? "ui.search_item_modder" : "ui.search_item"));
            }

            if (_browserSearchStatusText == null) return;

            bool hasQuery =
                _browserSearchInput != null &&
                !string.IsNullOrEmpty(NormalizeBrowserSearchText(_browserSearchInput.text));

            if (hasQuery)
            {
                int pages = Math.Max(1,
                    (_browserSearchLastResultCount + BrowserSearchVisibleRows - 1) /
                    BrowserSearchVisibleRows);
                string pageSuffix = pages > 1
                    ? "  •  " + (_browserSearchResultPage + 1).ToString(CultureInfo.InvariantCulture) +
                      "/" + pages.ToString(CultureInfo.InvariantCulture)
                    : string.Empty;
                _browserSearchStatusText.text = NormalizeGameText(
                    _browserSearchLastResultCount.ToString(CultureInfo.InvariantCulture) + pageSuffix);
                return;
            }

            if (_browserSearchWarmupActive && BrowserSearchIndexItemIds.Count > 0)
            {
                int percent = (int)((long)_browserSearchWarmupIndex * 100L / BrowserSearchIndexItemIds.Count);
                _browserSearchStatusText.text = NormalizeGameText(
                    percent.ToString(CultureInfo.InvariantCulture) + "%");
            }
            else
            {
                _browserSearchStatusText.text = NormalizeGameText(
                    BrowserSearchIndexItemIds.Count.ToString(CultureInfo.InvariantCulture));
            }
        }

        private static GameObject CreateBrowserText(string name, Transform parent, Vector2 position, Vector2 size,
            float fontSize, Color color, FontStyles style, TextAlignmentOptions alignment)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            RectTransform rt = go.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = position;
            rt.sizeDelta = size;

            TMP_Text tmp = go.AddComponent<TextMeshProUGUI>();
            ConfigureInspectorText(tmp, fontSize, color, style);
            tmp.alignment = alignment;
            tmp.enableWordWrapping = false;
            tmp.overflowMode = TextOverflowModes.Truncate;
            tmp.raycastTarget = false;
                        if (string.Equals(name, "Label", StringComparison.Ordinal))
            {
                Outline outline = go.AddComponent<Outline>();
                outline.effectColor = new Color(0.04f, 0.16f, 0.12f, 0.95f);
                outline.effectDistance = new Vector2(1f, -1f);
            }
            return go;
        }

        private static void CreateBrowserRule(Transform parent, float y)
        {
            GameObject line = new GameObject("Rule");
            line.transform.SetParent(parent, false);
            RectTransform rt = line.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(12f, -y);
            rt.sizeDelta = new Vector2(712f, 1f);
            Image image = line.AddComponent<Image>();
            image.color = new Color(0.24f, 0.55f, 0.47f, 0.85f);
            image.raycastTarget = false;
        }

        private static Scrollbar CreateBrowserPageScrollbar(
            string name,
            Transform parent,
            Vector2 position,
            Vector2 size,
            Action<float> onValueChanged)
        {
            GameObject root = new GameObject(name);
            root.transform.SetParent(parent, false);

            RectTransform rt = root.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = position;
            rt.sizeDelta = size;

            Image track = root.AddComponent<Image>();
            track.color = new Color(0.010f, 0.038f, 0.032f, 0.96f);
            track.raycastTarget = true;

            Outline trackOutline = root.AddComponent<Outline>();
            trackOutline.effectColor = new Color(0.22f, 0.52f, 0.43f, 0.88f);
            trackOutline.effectDistance = new Vector2(1f, -1f);

            GameObject slidingArea = new GameObject("SlidingArea");
            slidingArea.transform.SetParent(root.transform, false);
            RectTransform slidingRt = slidingArea.AddComponent<RectTransform>();
            slidingRt.anchorMin = Vector2.zero;
            slidingRt.anchorMax = Vector2.one;
            slidingRt.pivot = new Vector2(0.5f, 0.5f);
            slidingRt.offsetMin = new Vector2(2f, 2f);
            slidingRt.offsetMax = new Vector2(-2f, -2f);

            GameObject handle = new GameObject("Handle");
            handle.transform.SetParent(slidingArea.transform, false);
            RectTransform handleRt = handle.AddComponent<RectTransform>();
            handleRt.anchorMin = Vector2.zero;
            handleRt.anchorMax = Vector2.one;
            handleRt.pivot = new Vector2(0.5f, 0.5f);
            handleRt.offsetMin = Vector2.zero;
            handleRt.offsetMax = Vector2.zero;

            Image handleImage = handle.AddComponent<Image>();
            handleImage.color = new Color(0.37f, 0.73f, 0.57f, 0.98f);
            handleImage.raycastTarget = true;

            Outline handleOutline = handle.AddComponent<Outline>();
            handleOutline.effectColor = new Color(0.63f, 0.91f, 0.68f, 0.88f);
            handleOutline.effectDistance = new Vector2(1f, -1f);

            Scrollbar scrollbar = root.AddComponent<Scrollbar>();
            scrollbar.handleRect = handleRt;
            scrollbar.targetGraphic = handleImage;
            scrollbar.direction = Scrollbar.Direction.BottomToTop;
            scrollbar.size = 1f;
            scrollbar.value = 1f;

            ColorBlock colors = scrollbar.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(0.78f, 1.00f, 0.82f, 1f);
            colors.pressedColor = new Color(1.00f, 0.90f, 0.58f, 1f);
            colors.selectedColor = colors.highlightedColor;
            colors.disabledColor = new Color(0.46f, 0.58f, 0.50f, 0.72f);
            colors.colorMultiplier = 1f;
            colors.fadeDuration = 0.06f;
            scrollbar.colors = colors;

            UnityEngine.UI.Navigation navigation = scrollbar.navigation;
            navigation.mode = UnityEngine.UI.Navigation.Mode.None;
            scrollbar.navigation = navigation;

            if (onValueChanged != null)
                scrollbar.onValueChanged.AddListener(delegate(float value)
                {
                    if (!_browserScrollbarSync)
                        onValueChanged(value);
                });

            root.SetActive(false);
            return scrollbar;
        }

        private static void SyncBrowserPageScrollbar(Scrollbar scrollbar, int pages, int page)
        {
            if (scrollbar == null) return;

            bool visible = pages > 1;
            if (scrollbar.gameObject.activeSelf != visible)
                scrollbar.gameObject.SetActive(visible);
            if (!visible) return;

            int clampedPage = Mathf.Clamp(page, 0, pages - 1);
            _browserScrollbarSync = true;
            try
            {
                scrollbar.numberOfSteps = pages;
                scrollbar.size = Mathf.Clamp(1f / pages, 0.065f, 0.50f);
                scrollbar.value = pages <= 1
                    ? 1f
                    : 1f - ((float)clampedPage / (float)(pages - 1));
            }
            finally
            {
                _browserScrollbarSync = false;
            }
        }

        private static int BrowserPageFromScrollbarValue(float value, int pages)
        {
            if (pages <= 1) return 0;
            return Mathf.Clamp(
                Mathf.RoundToInt((1f - Mathf.Clamp01(value)) * (pages - 1)),
                0,
                pages - 1);
        }

        private static void HandleBrowserPageScrollbar(float value)
        {
            if (!_inspectorOpen || _browserCatalogOpen) return;

            int pages = Math.Max(1, (BrowserLines.Count + BrowserVisibleRows - 1) / BrowserVisibleRows);
            int next = BrowserPageFromScrollbarValue(value, pages);
            if (next == _browserPage) return;
            _browserPage = next;
            RenderBrowserRowsOnly();
        }

        private static void HandleBrowserCatalogScrollbar(float value)
        {
            if (!_inspectorOpen || !_browserCatalogOpen) return;
            int pages = Math.Max(1,
                (BrowserCatalogFilteredItemIds.Count + BrowserCatalogVisibleRows - 1) /
                BrowserCatalogVisibleRows);
            int next = BrowserPageFromScrollbarValue(value, pages);
            if (next == _browserCatalogPage) return;
            _browserCatalogPage = next;
            RenderBrowserCatalogRows();
        }

        private static void HandleBrowserSearchScrollbar(float value)
        {
            if (!_inspectorOpen ||
                _browserSearchDropdown == null ||
                !_browserSearchDropdown.activeSelf)
                return;

            int pages = Math.Max(1,
                (BrowserSearchCurrentMatches.Count + BrowserSearchVisibleRows - 1) /
                BrowserSearchVisibleRows);
            int next = BrowserPageFromScrollbarValue(value, pages);
            if (next == _browserSearchResultPage) return;
            _browserSearchResultPage = next;
            RenderBrowserSearchCurrentPage();
        }

        private static void CaptureInspectorStyle(Component tooltip)
        {
            if (tooltip == null) return;
            try
            {
                TMP_Text[] texts = tooltip.GetComponentsInChildren<TMP_Text>(true);
                for (int i = 0; i < texts.Length; i++)
                {
                    TMP_Text candidate = texts[i];
                    if (candidate == null || candidate.font == null) continue;
                    _inspectorFont = candidate.font;
                    break;
                }

                // Font size is intentionally not inherited. Quasimorph uses multiple
                // canvases/scales; inheriting the numeric TMP size can produce giant text.
            }
            catch { }
        }

        private static void RefreshInspectorAnchorFromTooltip()
        {
            // Item Intelligence is a modal browser, not a tooltip follower. Always keep it
            // on the same side so Alt-Tab, hover changes and different inventory layouts
            // cannot make the window jump between left and right.
            _inspectorPinnedTooltipOnRight = false;
        }

        private static void PositionInspectorPanel()
        {
            if (_inspectorRect == null || _inspectorCanvas == null) return;

            if (_inspectorPinnedTooltipOnRight)
            {
                _inspectorRect.anchorMin = new Vector2(0f, 0.5f);
                _inspectorRect.anchorMax = new Vector2(0f, 0.5f);
                _inspectorRect.pivot = new Vector2(0f, 0.5f);
                _inspectorRect.anchoredPosition = new Vector2(26f, 0f);
            }
            else
            {
                _inspectorRect.anchorMin = new Vector2(1f, 0.5f);
                _inspectorRect.anchorMax = new Vector2(1f, 0.5f);
                _inspectorRect.pivot = new Vector2(1f, 0.5f);
                _inspectorRect.anchoredPosition = new Vector2(-26f, 0f);
            }

            _inspectorRect.localScale = Vector3.one;
            _inspectorRoot.transform.SetAsLastSibling();
        }

        private static void RenderBrowser(string itemId)
        {
            if (_inspectorRoot == null || string.IsNullOrEmpty(itemId)) return;

            EnsureLocalizationCacheLanguage();
            if (_inspectorTitle != null)
                _inspectorTitle.text = NormalizeGameText(LocalizeItem(itemId));
            if (_inspectorItemIdText != null)
                _inspectorItemIdText.text = NormalizeModUiText(Ui("ui.item_id")) + ": " + itemId;

            UpdateBrowserPreview(itemId);
            UpdateBrowserChromeLocalization();
            UpdateBrowserTabs();
            UpdateBrowserStats(itemId);

            BrowserLines.Clear();
            switch ((BrowserTabId)_browserTab)
            {
                case BrowserTabId.Overview:
                    BuildBrowserOverview(itemId);
                    break;
                case BrowserTabId.Magnum:
                    if (ShowMagnumUses) { EnsureBrowserFactionColumnsUi(); BuildBrowserMagnum(itemId); }
                    break;
                case BrowserTabId.Recipes:
                    BuildBrowserRecipes(itemId);
                    break;
                case BrowserTabId.Trade:
                    // Trade and Factions share the four pooled table-column texts.
                    // Since v1.7.37 they are lazy, so Trade must explicitly materialize them too.
                    EnsureBrowserFactionColumnsUi();
                    BuildBrowserTrade(itemId);
                    break;
                case BrowserTabId.Ammo:
                    if (ShowAmmoRelations) BuildBrowserAmmo(itemId);
                    break;
                case BrowserTabId.Factions:
                    EnsureBrowserFactionColumnsUi();
                    BuildBrowserFactionTechnology(itemId);
                    break;
                case BrowserTabId.Loot:
                    // Loot reuses the same lazy pooled table columns as Trade/Factions.
                    // Materialize them before rendering or only the left column is visible
                    // when Loot is the first table tab opened in a fresh inspector session.
                    EnsureBrowserFactionColumnsUi();
                    EnsureBrowserLootProgressUi();
                    if (ShowSources) BuildBrowserLootSources(itemId);
                    break;
                default:
                    _browserTab = (int)BrowserTabId.Overview;
                    BuildBrowserOverview(itemId);
                    break;
            }

            if (BrowserLinesNeedRecipeContextUi()) EnsureBrowserRecipeContextUi();

            int pages = Math.Max(1, (BrowserLines.Count + BrowserVisibleRows - 1) / BrowserVisibleRows);
            if (_browserPage >= pages) _browserPage = pages - 1;
            if (_browserPage < 0) _browserPage = 0;
            if (_browserTab >= 0 && _browserTab < BrowserPageByTab.Length)
                BrowserPageByTab[_browserTab] = _browserPage;
            RenderBrowserRowsOnly();
            UpdateLootProgressUi();
            PositionInspectorPanel();
        }

        private static void RenderBrowserRowsOnly()
        {
            HideBrowserWeaponModeTooltip();
            int total = BrowserLines.Count;
            int pages = Math.Max(1, (total + BrowserVisibleRows - 1) / BrowserVisibleRows);
            if (_browserPage >= pages) _browserPage = pages - 1;
            if (_browserPage < 0) _browserPage = 0;
            if (_browserTab >= 0 && _browserTab < BrowserPageByTab.Length)
                BrowserPageByTab[_browserTab] = _browserPage;

            int startIndex = _browserPage * BrowserVisibleRows;
            for (int i = 0; i < BrowserVisibleRows; i++)
            {
                GameObject root = BrowserRowRoots[i];
                TMP_Text left = BrowserRowLeft[i];
                TMP_Text right = BrowserRowRight[i];
                TMP_Text factionReward = BrowserRowFactionReward[i];
                TMP_Text factionUnlock = BrowserRowFactionUnlock[i];
                TMP_Text factionCurrent = BrowserRowFactionCurrent[i];
                TMP_Text factionState = BrowserRowFactionState[i];
                Image bg = BrowserRowBackground[i];
                if (root == null || left == null || right == null) continue;
                SetBrowserWeaponModeTooltipTarget(root, string.Empty, string.Empty, false);

                int lineIndex = startIndex + i;
                if (lineIndex >= total)
                {
                    if (BrowserRowButtons[i] != null) BrowserRowButtons[i].interactable = false;
                    left.raycastTarget = false;
                    root.SetActive(false);
                    continue;
                }

                BrowserLine line = BrowserLines[lineIndex];
                left.raycastTarget = line != null && line.LeftMode == 1 && IsKnownItemId(line.Left);
                root.SetActive(true);

                if (factionReward != null) factionReward.gameObject.SetActive(false);
                if (factionUnlock != null) factionUnlock.gameObject.SetActive(false);
                if (factionCurrent != null) factionCurrent.gameObject.SetActive(false);
                if (factionState != null) factionState.gameObject.SetActive(false);
                if (right != null) right.gameObject.SetActive(true);

                Button rowButton = BrowserRowButtons[i];
                bool actionable = !string.IsNullOrEmpty(line.ActionSpaceObjectId);
                if (rowButton != null) rowButton.interactable = actionable;

                Outline rowOutline = BrowserRowOutlines[i];
                if (rowOutline != null)
                {
                    rowOutline.enabled = actionable;
                    rowOutline.effectColor = new Color(0.42f, 0.80f, 0.59f, 0.90f);
                }

                string leftText = line.Left ?? string.Empty;
                if (line.LeftMode == 1) leftText = LocalizeItem(leftText);
                else if (line.LeftMode == 2) leftText = LocalizeMagnumPerk(leftText);

                bool showIcon = false;
                Image itemIconImage = BrowserRowIcons[i];
                Image chipIconImage = BrowserRowChipIcons[i];
                Image chipStatusImage = BrowserRowChipStatusIcons[i];

                if (itemIconImage != null)
                {
                    SetBrowserItemTooltipTarget(itemIconImage, string.Empty, false);
                    itemIconImage.sprite = null;
                    itemIconImage.enabled = false;
                    itemIconImage.color = Color.white;
                }
                if (chipIconImage != null)
                {
                    SetBrowserItemTooltipTarget(chipIconImage, string.Empty, false);
                    chipIconImage.sprite = null;
                    chipIconImage.enabled = false;
                    chipIconImage.color = Color.white;
                }
                if (chipStatusImage != null)
                {
                    chipStatusImage.sprite = null;
                    chipStatusImage.enabled = false;
                    chipStatusImage.color = Color.white;
                }

                if (line.LeftMode == 1 && itemIconImage != null)
                {
                    Sprite icon = TryResolveItemSmallIcon(line.Left);
                    if (icon != null)
                    {
                        itemIconImage.sprite = icon;
                        itemIconImage.enabled = true;
                        SetBrowserItemTooltipTarget(itemIconImage, line.Left, true, true);
                        showIcon = true;
                    }
                }
                else if (line.LeftMode == 3 && itemIconImage != null)
                {
                    Sprite modeIcon = TryResolveWeaponModeSmallIcon(line.ContainerIconId);
                    if (modeIcon != null)
                    {
                        itemIconImage.sprite = modeIcon;
                        itemIconImage.enabled = true;
                        itemIconImage.color = Color.white;
                        showIcon = true;
                    }
                }
                else if (!string.IsNullOrEmpty(line.FactionId) && itemIconImage != null)
                {
                    Sprite factionIcon = TryResolveFactionSmallIcon(
                        line.FactionId, ResolveFactionById(line.FactionId));
                    if (factionIcon != null)
                    {
                        itemIconImage.sprite = factionIcon;
                        itemIconImage.enabled = true;
                        itemIconImage.color = Color.white;
                        showIcon = true;
                    }
                }

                else if (!string.IsNullOrEmpty(line.ContainerIconId) && itemIconImage != null)
                {
                    Sprite containerIcon = TryResolveLootContainerSmallIcon(line.ContainerIconId);
                    if (containerIcon != null)
                    {
                        itemIconImage.sprite = containerIcon;
                        itemIconImage.enabled = true;
                        itemIconImage.color = Color.white;
                        showIcon = true;
                    }
                }

                if (line.LeftMode == 3)
                    SetBrowserWeaponModeTooltipTarget(root, line.ContainerIconId, leftText, true);

                bool showRecipeContext = line.ShowRecipeChipContext;
                bool showChipUnlockStatus = line.RowKind == BrowserRowKind.ChipUnlock;
                if (showRecipeContext && chipIconImage != null)
                {
                    if (!string.IsNullOrEmpty(line.ChipItemId))
                    {
                        Sprite chipSprite = TryResolveItemSmallIcon(line.ChipItemId);
                        if (chipSprite != null)
                        {
                            chipIconImage.sprite = chipSprite;
                            chipIconImage.color = Color.white;
                        }
                        else
                        {
                            chipIconImage.sprite = _qiiNoDatadiskSprite;
                            chipIconImage.color = new Color(0.48f, 0.62f, 0.56f, 1f);
                        }
                    }
                    else
                    {
                        chipIconImage.sprite = _qiiNoDatadiskSprite;
                        chipIconImage.color = new Color(0.48f, 0.62f, 0.56f, 1f);
                    }
                    chipIconImage.enabled = chipIconImage.sprite != null;
                    SetBrowserItemTooltipTarget(
                        chipIconImage,
                        line.ChipItemId,
                        chipIconImage.enabled && !string.IsNullOrEmpty(line.ChipItemId),
                        true);
                }

                if (((showRecipeContext && !string.IsNullOrEmpty(line.ChipItemId)) || showChipUnlockStatus) && chipStatusImage != null)
                {
                    if (line.ChipStatus > 0)
                    {
                        chipStatusImage.sprite = _qiiUnlockedMarkerSprite;
                        chipStatusImage.color = line.ChipStatus == 1
                            ? new Color(0.46f, 0.92f, 0.54f, 1f)
                            : new Color(0.92f, 0.82f, 0.38f, 1f);
                        chipStatusImage.enabled = chipStatusImage.sprite != null;
                    }
                    else if (line.ChipStatus < 0)
                    {
                        chipStatusImage.sprite = _qiiLockedMarkerSprite;
                        chipStatusImage.color = new Color(0.94f, 0.34f, 0.30f, 1f);
                        chipStatusImage.enabled = chipStatusImage.sprite != null;
                    }
                }

                RectTransform itemRt = itemIconImage == null ? null : itemIconImage.rectTransform;
                RectTransform chipRt = chipIconImage == null ? null : chipIconImage.rectTransform;
                RectTransform statusRt = chipStatusImage == null ? null : chipStatusImage.rectTransform;
                RectTransform leftRt = left.rectTransform;
                RectTransform rightRt = right.rectTransform;

                // The same fixed row objects are reused on every page and tab. Reset
                // typography before applying a specialized table layout so Loot/Faction
                // rows cannot leak their font sizes or styles into normal browser rows.
                left.enableAutoSizing = false;
                left.fontSize = 18f;
                right.fontSize = 16f;
                left.fontStyle = FontStyles.Normal;
                right.fontStyle = FontStyles.Normal;
                right.alignment = TextAlignmentOptions.MidlineRight;
                if (factionReward != null)
                {
                    factionReward.fontSize = 15f;
                    factionReward.fontStyle = FontStyles.Normal;
                }
                if (factionUnlock != null)
                {
                    factionUnlock.fontSize = 15f;
                    factionUnlock.fontStyle = FontStyles.Normal;
                }
                if (factionCurrent != null)
                {
                    factionCurrent.fontSize = 15f;
                    factionCurrent.fontStyle = FontStyles.Normal;
                }
                if (factionState != null)
                {
                    factionState.fontSize = 13f;
                    factionState.fontStyle = FontStyles.Normal;
                }

                if (rightRt != null)
                {
                    rightRt.anchoredPosition = new Vector2(494f, 0f);
                    rightRt.sizeDelta = new Vector2(194f, rightRt.sizeDelta.y);
                }

                if (showRecipeContext)
                {
                    if (statusRt != null) statusRt.anchoredPosition = new Vector2(5f, 0f);
                    if (chipRt != null) chipRt.anchoredPosition = new Vector2(23f, 0f);
                    if (itemRt != null) itemRt.anchoredPosition = new Vector2(51f, 0f);
                    if (leftRt != null)
                    {
                        leftRt.anchoredPosition = new Vector2(79f, 0f);
                        leftRt.sizeDelta = new Vector2(348f, leftRt.sizeDelta.y);
                    }
                }
                else if (showChipUnlockStatus)
                {
                    if (statusRt != null) statusRt.anchoredPosition = new Vector2(6f, 0f);
                    if (itemRt != null) itemRt.anchoredPosition = new Vector2(28f, 0f);
                    if (leftRt != null)
                    {
                        leftRt.anchoredPosition = new Vector2(56f, 0f);
                        leftRt.sizeDelta = new Vector2(368f, leftRt.sizeDelta.y);
                    }
                }
                else
                {
                    if (itemRt != null) itemRt.anchoredPosition = new Vector2(8f, 0f);
                    if (leftRt != null)
                    {
                        leftRt.anchoredPosition = showIcon ? new Vector2(36f, 0f) : new Vector2(10f, 0f);
                        leftRt.sizeDelta = showIcon ? new Vector2(388f, leftRt.sizeDelta.y) : new Vector2(414f, leftRt.sizeDelta.y);
                    }
                }

                if (line.RowKind == BrowserRowKind.FactionRewardHeader || line.RowKind == BrowserRowKind.FactionReward)
                {
                    // Stable faction table geometry: faction/name at left, then four
                    // fixed columns whose headers sit directly above their values.
                    if (right != null) right.gameObject.SetActive(false);

                    if (leftRt != null)
                    {
                        leftRt.anchoredPosition = line.RowKind == BrowserRowKind.FactionReward && showIcon
                            ? new Vector2(36f, 0f)
                            : new Vector2(10f, 0f);
                        leftRt.sizeDelta = line.RowKind == BrowserRowKind.FactionReward && showIcon
                            ? new Vector2(268f, leftRt.sizeDelta.y)
                            : new Vector2(294f, leftRt.sizeDelta.y);
                    }

                    if (factionReward != null)
                    {
                        RectTransform rt = factionReward.rectTransform;
                        rt.anchoredPosition = new Vector2(304f, 0f);
                        rt.sizeDelta = new Vector2(82f, rt.sizeDelta.y);
                        factionReward.gameObject.SetActive(true);
                        factionReward.text = NormalizeModUiText(line.ColumnReward);
                    }
                    if (factionUnlock != null)
                    {
                        RectTransform rt = factionUnlock.rectTransform;
                        rt.anchoredPosition = new Vector2(386f, 0f);
                        rt.sizeDelta = new Vector2(78f, rt.sizeDelta.y);
                        factionUnlock.gameObject.SetActive(true);
                        factionUnlock.text = NormalizeModUiText(line.ColumnUnlock);
                    }
                    if (factionCurrent != null)
                    {
                        RectTransform rt = factionCurrent.rectTransform;
                        rt.anchoredPosition = new Vector2(464f, 0f);
                        rt.sizeDelta = new Vector2(104f, rt.sizeDelta.y);
                        factionCurrent.gameObject.SetActive(true);
                        factionCurrent.text = NormalizeModUiText(line.ColumnCurrent);
                    }
                    if (factionState != null)
                    {
                        RectTransform rt = factionState.rectTransform;
                        rt.anchoredPosition = new Vector2(568f, 0f);
                        rt.sizeDelta = new Vector2(94f, rt.sizeDelta.y);
                        factionState.gameObject.SetActive(true);
                        factionState.text = NormalizeModUiText(line.ColumnState);
                    }

                    left.text = NormalizeModUiText(leftText);

                    if (line.RowKind == BrowserRowKind.FactionRewardHeader)
                    {
                        left.fontStyle = FontStyles.Italic;
                        left.color = new Color(0.35f, 0.58f, 0.52f, 1f);
                        Color headerColor = new Color(0.35f, 0.58f, 0.52f, 1f);
                        factionReward.color = headerColor;
                        factionUnlock.color = headerColor;
                        factionCurrent.color = headerColor;
                        factionState.color = headerColor;
                        factionReward.fontStyle = FontStyles.Italic;
                        factionUnlock.fontStyle = FontStyles.Italic;
                        factionCurrent.fontStyle = FontStyles.Italic;
                        factionState.fontStyle = FontStyles.Italic;
                        if (bg != null) bg.color = new Color(0.010f, 0.030f, 0.027f, 0.30f);
                    }
                    else
                    {
                        Color valueColor = new Color(0.92f, 0.86f, 0.52f, 1f);
                        factionReward.color = valueColor;
                        factionUnlock.color = valueColor;
                        factionCurrent.color = valueColor;
                        factionState.color = line.Style == 3
                            ? new Color(0.64f, 0.85f, 0.67f, 1f)
                            : valueColor;
                    }
                }
                else if (line.RowKind == BrowserRowKind.LootHeader || line.RowKind == BrowserRowKind.LootRow)
                {
                    // Loot uses the already pooled faction-table text objects instead of
                    // creating new UI objects. The dedicated geometry makes source,
                    // context, chance, rolls/Tech and status readable at a glance.
                    if (right != null) right.gameObject.SetActive(false);

                    bool hasFourthColumn = !string.IsNullOrEmpty(line.ColumnState);
                    bool showContainerIcon =
                        line.RowKind == BrowserRowKind.LootRow &&
                        !string.IsNullOrEmpty(line.ContainerIconId) &&
                        showIcon && itemRt != null;
                    if (leftRt != null)
                    {
                        // Container rows now follow the same icon-before-name grammar as
                        // item/faction rows throughout QII.
                        leftRt.anchoredPosition = showContainerIcon
                            ? new Vector2(36f, 0f)
                            : new Vector2(10f, 0f);
                        leftRt.sizeDelta = new Vector2(
                            showContainerIcon ? 260f : (hasFourthColumn ? 286f : 304f),
                            leftRt.sizeDelta.y);
                    }
                    if (showContainerIcon && itemRt != null)
                    {
                        itemRt.anchoredPosition = new Vector2(8f, 0f);
                        itemRt.sizeDelta = new Vector2(22f, 22f);
                    }

                    if (hasFourthColumn)
                    {
                        ConfigureLootColumn(factionReward, 296f, 150f, line.ColumnReward, 13.5f);
                        ConfigureLootColumn(factionUnlock, 446f, 86f, line.ColumnUnlock, 13.5f);
                        ConfigureLootColumn(factionCurrent, 532f, 88f, line.ColumnCurrent, 13.5f);

                        bool eligibilityStatus =
                            string.Equals(line.ColumnState, "eligible", StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(line.ColumnState, "ineligible", StringComparison.OrdinalIgnoreCase);
                        if (eligibilityStatus && line.RowKind == BrowserRowKind.LootRow && chipStatusImage != null)
                        {
                            ConfigureLootColumn(factionState, 620f, 70f, string.Empty, 12f);
                            if (statusRt != null)
                            {
                                statusRt.anchoredPosition = new Vector2(646f, 0f);
                                statusRt.sizeDelta = new Vector2(16f, 16f);
                            }
                            bool eligible = string.Equals(line.ColumnState, "eligible", StringComparison.OrdinalIgnoreCase);
                            chipStatusImage.sprite = eligible ? _qiiUnlockedMarkerSprite : _qiiLockedMarkerSprite;
                            chipStatusImage.color = eligible
                                ? new Color(0.46f, 0.92f, 0.54f, 1f)
                                : new Color(0.94f, 0.34f, 0.30f, 1f);
                            chipStatusImage.enabled = chipStatusImage.sprite != null;
                        }
                        else
                        {
                            ConfigureLootColumn(factionState, 620f, 70f, line.ColumnState, 12f);
                        }
                    }
                    else
                    {
                        ConfigureLootColumn(factionReward, 314f, 184f, line.ColumnReward, 13.5f);
                        ConfigureLootColumn(factionUnlock, 498f, 92f, line.ColumnUnlock, 13.5f);

                        bool eligibilityStatus =
                            string.Equals(line.ColumnCurrent, "eligible", StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(line.ColumnCurrent, "ineligible", StringComparison.OrdinalIgnoreCase);
                        if (eligibilityStatus && line.RowKind == BrowserRowKind.LootRow && chipStatusImage != null)
                        {
                            ConfigureLootColumn(factionCurrent, 590f, 96f, string.Empty, 13.5f);
                            if (statusRt != null)
                            {
                                statusRt.anchoredPosition = new Vector2(630f, 0f);
                                statusRt.sizeDelta = new Vector2(16f, 16f);
                            }
                            bool eligible = string.Equals(line.ColumnCurrent, "eligible", StringComparison.OrdinalIgnoreCase);
                            chipStatusImage.sprite = eligible ? _qiiUnlockedMarkerSprite : _qiiLockedMarkerSprite;
                            chipStatusImage.color = eligible
                                ? new Color(0.46f, 0.92f, 0.54f, 1f)
                                : new Color(0.94f, 0.34f, 0.30f, 1f);
                            chipStatusImage.enabled = chipStatusImage.sprite != null;
                        }
                        else
                        {
                            ConfigureLootColumn(factionCurrent, 590f, 96f, line.ColumnCurrent, 13.5f);
                        }
                        ConfigureLootColumn(factionState, 662f, 0f, string.Empty, 12f);
                    }

                    left.text = NormalizeModUiText(leftText);
                    left.fontSize = line.RowKind == BrowserRowKind.LootHeader ? 13f : 16f;

                    if (line.RowKind == BrowserRowKind.LootHeader)
                    {
                        Color headerColor = new Color(0.35f, 0.58f, 0.52f, 1f);
                        left.color = headerColor;
                        left.fontStyle = FontStyles.Italic;
                        SetLootColumnHeaderStyle(factionReward, headerColor);
                        SetLootColumnHeaderStyle(factionUnlock, headerColor);
                        SetLootColumnHeaderStyle(factionCurrent, headerColor);
                        SetLootColumnHeaderStyle(factionState, headerColor);
                        if (bg != null) bg.color = new Color(0.010f, 0.030f, 0.027f, 0.30f);
                    }
                    else
                    {
                        if (factionReward != null)
                            factionReward.color = new Color(0.52f, 0.74f, 0.63f, 1f);
                        if (factionUnlock != null)
                            factionUnlock.color = new Color(0.92f, 0.86f, 0.52f, 1f);
                        if (factionCurrent != null)
                            factionCurrent.color = new Color(0.92f, 0.86f, 0.52f, 1f);
                        if (factionState != null)
                            factionState.color = new Color(0.62f, 0.82f, 0.66f, 1f);
                    }
                }
                else if (line.RowKind == BrowserRowKind.LootHeaderSixColumns || line.RowKind == BrowserRowKind.LootRowSixColumns)
                {
                    // Six-column enemy Loot table. Reuse the pooled Right text as the
                    // sixth column so no extra runtime UI objects are allocated.
                    if (right != null)
                    {
                        right.gameObject.SetActive(true);
                        right.alignment = TextAlignmentOptions.Center;
                    }
                    if (itemRt != null)
                    {
                        itemRt.anchoredPosition = new Vector2(8f, 0f);
                        itemRt.sizeDelta = new Vector2(20f, 20f);
                    }
                    if (leftRt != null)
                    {
                        // Dedicated faction-icon cell: 8..30 px. Enemy text always starts
                        // after that cell, so faction emblems can never overlap the name.
                        leftRt.anchoredPosition = new Vector2(38f, 0f);
                        leftRt.sizeDelta = new Vector2(172f, leftRt.sizeDelta.y);
                    }

                    // Give probability ranges substantially more room. Values such as
                    // 0.548%-0.912% must remain readable in both EN and RU layouts.
                    ConfigureLootColumn(factionReward, 210f, 112f, line.ColumnReward, 12f);
                    ConfigureLootColumn(factionUnlock, 322f, 112f, line.ColumnUnlock, 11.5f);
                    ConfigureLootColumn(factionCurrent, 434f, 54f, line.ColumnCurrent, 12f);
                    ConfigureLootColumn(factionState, 488f, 96f, line.ColumnState, 11.5f);
                    ConfigureLootColumn(right, 584f, 104f, line.Right, 11.5f);

                    left.text = NormalizeModUiText(leftText);
                    left.fontSize = line.RowKind == BrowserRowKind.LootHeaderSixColumns ? 12f : 14f;

                    if (line.RowKind == BrowserRowKind.LootHeaderSixColumns)
                    {
                        Color headerColor = new Color(0.35f, 0.58f, 0.52f, 1f);
                        left.color = headerColor;
                        left.fontStyle = FontStyles.Italic;
                        SetLootColumnHeaderStyle(factionReward, headerColor);
                        SetLootColumnHeaderStyle(factionUnlock, headerColor);
                        SetLootColumnHeaderStyle(factionCurrent, headerColor);
                        SetLootColumnHeaderStyle(factionState, headerColor);
                        SetLootColumnHeaderStyle(right, headerColor);
                        if (bg != null) bg.color = new Color(0.010f, 0.030f, 0.027f, 0.30f);
                    }
                    else
                    {
                        if (factionReward != null)
                            factionReward.color = new Color(0.52f, 0.74f, 0.63f, 1f);
                        if (factionUnlock != null)
                            factionUnlock.color = new Color(0.92f, 0.86f, 0.52f, 1f);
                        if (factionCurrent != null)
                            factionCurrent.color = new Color(0.76f, 0.88f, 0.68f, 1f);
                        if (factionState != null)
                            factionState.color = new Color(0.92f, 0.86f, 0.52f, 1f);
                        if (right != null)
                            right.color = new Color(0.62f, 0.82f, 0.66f, 1f);
                    }
                }
                else if (line.RowKind == BrowserRowKind.MagnumResearch)
                {
                    // Magnum research route | quantity | full state. This avoids the old
                    // right-edge truncation ("compl") while keeping long routes readable.
                    if (right != null) right.gameObject.SetActive(false);
                    if (leftRt != null)
                    {
                        leftRt.anchoredPosition = new Vector2(10f, 0f);
                        leftRt.sizeDelta = new Vector2(478f, leftRt.sizeDelta.y);
                    }
                    ConfigureLootColumn(factionReward, 488f, 54f, line.ColumnReward, 14f);
                    ConfigureLootColumn(factionUnlock, 542f, 146f, line.ColumnUnlock, 13.5f);
                    ConfigureLootColumn(factionCurrent, 688f, 0f, string.Empty, 12f);
                    ConfigureLootColumn(factionState, 688f, 0f, string.Empty, 12f);
                    left.text = NormalizeModUiText(leftText);
                    left.fontSize = 16f;
                    if (factionReward != null) factionReward.color = new Color(0.92f, 0.86f, 0.52f, 1f);
                    if (factionUnlock != null) factionUnlock.color = new Color(0.76f, 0.88f, 0.68f, 1f);
                }
                else if (line.RowKind == BrowserRowKind.TradeHeader || line.RowKind == BrowserRowKind.TradeStation)
                {
                    // v1.7.39-test11 Trade geometry: station | price | stock | mission remaining | vanilla travel.
                    // Reuse the existing pooled faction-table texts; no per-row objects.
                    // Price 0 remains a valid numeric price.
                    if (right != null) right.gameObject.SetActive(false);
                    if (leftRt != null)
                    {
                        leftRt.anchoredPosition = new Vector2(10f, 0f);
                        leftRt.sizeDelta = new Vector2(350f, leftRt.sizeDelta.y);
                    }
                    ConfigureLootColumn(factionReward, 360f, 65f, line.ColumnReward, 13.5f);
                    ConfigureLootColumn(factionUnlock, 425f, 58f, line.ColumnUnlock, 13.5f);
                    ConfigureLootColumn(factionCurrent, 483f, 80f, line.ColumnCurrent, 12.5f);
                    ConfigureLootColumn(factionState, 563f, 125f, line.ColumnState, 13f);

                    left.text = NormalizeModUiText((actionable && line.RowKind == BrowserRowKind.TradeStation ? ">  " : string.Empty) + leftText);
                    left.fontSize = line.RowKind == BrowserRowKind.TradeHeader ? 13f : 16f;

                    if (line.RowKind == BrowserRowKind.TradeHeader)
                    {
                        Color headerColor = new Color(0.35f, 0.58f, 0.52f, 1f);
                        left.color = headerColor;
                        left.fontStyle = FontStyles.Italic;
                        SetLootColumnHeaderStyle(factionReward, headerColor);
                        SetLootColumnHeaderStyle(factionUnlock, headerColor);
                        SetLootColumnHeaderStyle(factionCurrent, headerColor);
                        SetLootColumnHeaderStyle(factionState, headerColor);
                        if (bg != null) bg.color = new Color(0.010f, 0.030f, 0.027f, 0.30f);
                    }
                    else
                    {
                        if (factionReward != null) factionReward.color = new Color(0.92f, 0.86f, 0.52f, 1f);
                        if (factionUnlock != null) factionUnlock.color = new Color(0.76f, 0.88f, 0.68f, 1f);
                        if (factionCurrent != null)
                        {
                            // Mission column keeps the existing width. Orange means the mission
                            // is still present when travel would finish; muted green means it expires first.
                            factionCurrent.color = line.MetaState == 3
                                ? new Color(0.95f, 0.62f, 0.34f, 1f)
                                : line.MetaState == 2
                                    ? new Color(0.56f, 0.72f, 0.58f, 1f)
                                    : line.MetaState == 1
                                        ? new Color(0.92f, 0.76f, 0.42f, 1f)
                                        : new Color(0.50f, 0.58f, 0.54f, 1f);
                        }
                        if (factionState != null) factionState.color = new Color(0.70f, 0.78f, 0.73f, 1f);
                    }
                }
                else if (line.RowKind == BrowserRowKind.FullNote ||
                         line.RowKind == BrowserRowKind.FullSection)
                {
                    // Full-width informational rows avoid wasting the unused right
                    // column. FullSection is used by long localized section titles;
                    // FullNote lines are pre-wrapped to preserve the fixed row pool.
                    if (right != null) right.gameObject.SetActive(false);
                    if (leftRt != null)
                    {
                        leftRt.anchoredPosition = new Vector2(10f, 0f);
                        leftRt.sizeDelta = new Vector2(688f, leftRt.sizeDelta.y);
                    }
                    if (line.RowKind == BrowserRowKind.FullSection)
                    {
                        // Full-width section titles may include a localized parenthetical.
                        // TMP auto-sizing keeps the entire single line inside the fixed 688px pool.
                        left.enableAutoSizing = true;
                        left.fontSizeMin = 12.5f;
                        left.fontSizeMax = 17f;
                        left.fontSize = 17f;
                    }
                    else
                    {
                        left.fontSize = 12.5f;
                    }
                    left.text = NormalizeModUiText(leftText);
                }
                else if (line.RowKind == BrowserRowKind.ChipNote)
                {
                    // Chip unlock chance explanation uses the full content width.
                    if (right != null) right.gameObject.SetActive(false);
                    if (leftRt != null)
                    {
                        leftRt.anchoredPosition = new Vector2(10f, 0f);
                        leftRt.sizeDelta = new Vector2(688f, leftRt.sizeDelta.y);
                    }
                    left.fontSize = 13.0f;
                    left.text = NormalizeModUiText(leftText);
                }
                else
                {
                    if (actionable)
                    {
                        leftText = ">  " + leftText;
                        string actionRight = line.Right ?? string.Empty;
                        right.text = NormalizeModUiText(actionRight + "   >>");
                    }
                    else
                    {
                        right.text = NormalizeModUiText(line.Right ?? string.Empty);
                    }
                    left.text = NormalizeModUiText(leftText);
                }

                if (line.Style == 1)
                {
                    left.fontStyle = FontStyles.Bold;
                    left.color = new Color(0.74f, 0.86f, 0.62f, 1f);
                    right.color = new Color(0.48f, 0.72f, 0.62f, 1f);
                    if (bg != null) bg.color = new Color(0.032f, 0.092f, 0.073f, 0.95f);
                }
                else if (line.Style == 2)
                {
                    left.fontStyle = FontStyles.Italic;
                    left.color = new Color(0.35f, 0.58f, 0.52f, 1f);
                    right.color = new Color(0.35f, 0.58f, 0.52f, 1f);
                    if (bg != null) bg.color = new Color(0.010f, 0.030f, 0.027f, 0.30f);
                }
                else if (line.Style == 3)
                {
                    left.fontStyle = FontStyles.Bold;
                    left.color = new Color(0.64f, 0.85f, 0.67f, 1f);
                    right.color = new Color(0.92f, 0.86f, 0.52f, 1f);
                    if (bg != null) bg.color = new Color(0.046f, 0.106f, 0.073f, 0.80f);
                }
                else
                {
                    left.fontStyle = FontStyles.Normal;
                    left.color = new Color(0.46f, 0.72f, 0.61f, 1f);
                    right.color = new Color(0.86f, 0.85f, 0.61f, 1f);
                    if (bg != null) bg.color = i % 2 == 0
                        ? new Color(0.018f, 0.050f, 0.043f, 0.45f)
                        : new Color(0.010f, 0.034f, 0.030f, 0.18f);
                }

                if (actionable)
                {
                    left.fontStyle = FontStyles.Bold;
                    left.color = new Color(0.68f, 0.90f, 0.68f, 1f);
                    right.color = new Color(0.95f, 0.88f, 0.52f, 1f);
                    if (bg != null) bg.color = new Color(0.030f, 0.090f, 0.064f, 0.88f);
                }

                if (actionable && !string.IsNullOrEmpty(line.FactionId))
                {
                    if (line.FactionRelation == 1)
                    {
                        left.color = new Color(0.43f, 0.92f, 0.55f, 1f);
                        if (bg != null) bg.color = new Color(0.025f, 0.095f, 0.048f, 0.90f);
                        if (rowOutline != null)
                            rowOutline.effectColor = new Color(0.30f, 0.82f, 0.46f, 0.92f);
                    }
                    else if (line.FactionRelation == -1)
                    {
                        left.color = new Color(0.96f, 0.42f, 0.38f, 1f);
                        if (bg != null) bg.color = new Color(0.105f, 0.028f, 0.030f, 0.90f);
                        if (rowOutline != null)
                            rowOutline.effectColor = new Color(0.90f, 0.30f, 0.28f, 0.94f);
                    }
                    else if (line.FactionRelation == 0)
                    {
                        left.color = new Color(0.72f, 0.78f, 0.69f, 1f);
                        if (bg != null) bg.color = new Color(0.046f, 0.060f, 0.052f, 0.88f);
                        if (rowOutline != null)
                            rowOutline.effectColor = new Color(0.47f, 0.58f, 0.52f, 0.84f);
                    }
                    else
                    {
                        // Unknown relation is not neutral. Use a subdued amber treatment
                        // instead of claiming a relationship state that was not resolved.
                        left.color = new Color(0.82f, 0.74f, 0.50f, 1f);
                        if (bg != null) bg.color = new Color(0.075f, 0.061f, 0.036f, 0.88f);
                        if (rowOutline != null)
                            rowOutline.effectColor = new Color(0.67f, 0.56f, 0.34f, 0.86f);
                    }
                }
            }

            if (_browserPageText != null)
            {
                bool ru = IsRussian();
                _browserPageText.text = NormalizeModUiText(
                    (Ui("ui.page")) +
                    (_browserPage + 1).ToString(CultureInfo.InvariantCulture) + "/" +
                    pages.ToString(CultureInfo.InvariantCulture) +
                    "   •   " +
                    total.ToString(CultureInfo.InvariantCulture) +
                    (Ui("ui.rows")));
            }

            SyncBrowserPageScrollbar(_browserPageScrollbar, pages, _browserPage);
        }

        private static void UpdateBrowserChromeLocalization()
        {
            bool ru = IsRussian();

            if (_browserCloseText != null)
                _browserCloseText.text = GetBrowserCloseButtonLabel();

            if (_browserHelpText != null)
                _browserHelpText.text = NormalizeModUiText(Ui("ui.1_7_section_q_e_tab_wheel_page_esc_close"));

            UpdateBrowserSearchStatus();
            UpdateBrowserCatalogButtonStyle();
            UpdateBrowserHeaderActions();
            if (_browserCatalogOpen) RefreshBrowserCatalog();
        }

        private static void UpdateBrowserTabs()
        {
            bool ru = IsRussian();
            string[] labels = new string[]
            {
                Ui("tab.overview"), Ui("tab.magnum"), Ui("tab.recipes"), Ui("tab.trade"),
                Ui("tab.ammo"), Ui("tab.factions"), Ui("tab.loot")
            };

            for (int i = 0; i < BrowserTabCount; i++)
            {
                if (BrowserTabTexts[i] != null)
                {
                    BrowserTabTexts[i].text = NormalizeModUiText(GetBrowserInterfaceTabLabel(i, labels[i]));
                    BrowserTabTexts[i].fontSize = GetBrowserInterfaceTabFontSize(ru);
                    bool available = IsBrowserTabCompatibilityAvailable(i);
                    BrowserTabTexts[i].color = !available
                        ? new Color(0.38f, 0.38f, 0.38f, 1f)
                        : (i == _browserTab
                            ? new Color(0.88f, 0.90f, 0.62f, 1f)
                            : new Color(0.42f, 0.68f, 0.58f, 1f));
                    UpdateBrowserTabInterfaceIconStyle(i, available, i == _browserTab);
                }
                if (BrowserTabBackgrounds[i] != null)
                    BrowserTabBackgrounds[i].color = i == _browserTab
                        ? new Color(0.060f, 0.145f, 0.100f, 0.98f)
                        : new Color(0.025f, 0.065f, 0.055f, 0.92f);
            }
        }

        private static void AddWrappedBrowserValue(string label, IList<string> values, int maxChars)
        {
            if (values == null || values.Count == 0) return;
            maxChars = Math.Max(12, maxChars);
            string current = string.Empty;
            bool first = true;
            for (int i = 0; i < values.Count; i++)
            {
                string value = values[i] ?? string.Empty;
                if (string.IsNullOrEmpty(value)) continue;
                string candidate = string.IsNullOrEmpty(current) ? value : current + ", " + value;
                if (!string.IsNullOrEmpty(current) && candidate.Length > maxChars)
                {
                    BrowserLines.Add(BrowserLine.Normal(first ? label : string.Empty, current));
                    first = false;
                    current = value;
                }
                else
                {
                    current = candidate;
                }
            }
            if (!string.IsNullOrEmpty(current))
                BrowserLines.Add(BrowserLine.Normal(first ? label : string.Empty, current));
        }

        private static void BuildBrowserOverview(string itemId)
        {
            bool ru = IsRussian();

            BrowserLines.Add(BrowserLine.Section(Ui("ui.profile")));

            int magnum = ShowMagnumUses ? GetVisibleMagnumRequired(itemId) : 0;
            PriceSnapshot price = null;
            bool havePrice = ShowMagnumUses && PriceByItem.TryGetValue(itemId, out price);
            int used = ShowRecipes ? GetUniqueRecipeOutputCount(itemId) : 0;
            int crafted = ShowRecipes ? GetStaticRelationListCount(CraftedFromRecipes, itemId) : 0;
            int sources = ShowSources ? GetUniqueRelationCount(itemId, true) : 0;
            int consumers = ShowTradeInformation ? GetUniqueRelationCount(itemId, false) : 0;
            int ammo = ShowAmmoRelations ? GetAmmoRelationCount(itemId) : 0;

            string relationId = ResolveStaticRelationItemId(itemId);

            List<string> roles = new List<string>();
            if ((ShowSources || ShowTradeInformation) && BarterItemIds.Contains(itemId)) roles.Add(Ui("ui.trade"));
            if (ShowRecipes && crafted > 0) roles.Add(Ui("ui.craftable"));
            if (ShowRecipes && used > 0) roles.Add(Ui("ui.ingredient"));
            if (GetDisassemblyOutputCount(itemId) > 0) roles.Add(Ui("ui.recyclable"));
            if (ShowAmmoRelations && CompatibleWeaponsByAmmo.ContainsKey(relationId)) roles.Add(Ui("ui.ammo"));
            WeaponInfo weapon;
            if (ShowAmmoRelations && WeaponsByItem.TryGetValue(relationId, out weapon) &&
                weapon != null && weapon.CompatibleAmmo.Count > 0)
                roles.Add(Ui("ui.weapon"));
            if (roles.Count > 0)
                AddWrappedBrowserValue(Ui("ui.role"), roles, ru ? 18 : 28);

            if (UsesInheritedStaticRelations(itemId))
                AddModifiedRelationBrowserNote(itemId);

            BrowserLines.Add(BrowserLine.Section(Ui("ui.key_intel")));

            if (ShowMagnumUses && magnum > 0)
            {
                string right = magnum.ToString(CultureInfo.InvariantCulture);
                if (havePrice)
                    right += Ui("ui.remaining");
                BrowserLines.Add(BrowserLine.Accent(Ui("ui.magnum_research"), right));
                if (havePrice)
                {
                    BrowserLines.Add(BrowserLine.Normal(Ui("ui.owned"), price.Owned.ToString(CultureInfo.InvariantCulture)));
                    if (ShowMagnumSurplus)
                        BrowserLines.Add(BrowserLine.Normal(Ui("ui.after_all_research"),
                            Math.Max(0, price.Owned - magnum).ToString(CultureInfo.InvariantCulture)));
                }
            }
            else if (ShowMagnumUses)
            {
                BrowserLines.Add(BrowserLine.Normal(Ui("ui.magnum"), Ui("ui.not_required")));
            }

            if (ShowRecipes)
            {
                BrowserLines.Add(BrowserLine.Normal(
                    Ui("ui.used_by_recipes"),
                    used > 0 ? used.ToString(CultureInfo.InvariantCulture) : (Ui("ui.none"))));
                BrowserLines.Add(BrowserLine.Normal(
                    Ui("ui.crafted_by_recipes"),
                    crafted > 0 ? crafted.ToString(CultureInfo.InvariantCulture) : (Ui("ui.none"))));
            }

            if (ShowSources || ShowTradeInformation)
            {
                BrowserLines.Add(BrowserLine.Normal(
                    Ui("ui.trade_links"),
                    (sources + consumers) > 0
                        ? FormatVisibleTradeCounts(sources, consumers)
                        : Ui("ui.not_found")));
            }

            if (ShowAmmoRelations)
            {
                BrowserLines.Add(BrowserLine.Normal(
                    Ui("ui.ammo_links"),
                    ammo > 0 ? ammo.ToString(CultureInfo.InvariantCulture) : (Ui("ui.none"))));
            }

            List<WeaponModeDescriptor> weaponModes = GetWeaponModesForItem(itemId);
            if (ShowAmmoRelations && weaponModes.Count > 0)
            {
                BrowserLines.Add(BrowserLine.Section(Ui("ui.weapon_modes")));
                for (int i = 0; i < weaponModes.Count; i++)
                {
                    WeaponModeDescriptor mode = weaponModes[i];
                    if (mode == null) continue;
                    string modeLabel = ResolveWeaponModeDisplayLabel(mode);
                    if (string.IsNullOrEmpty(modeLabel)) continue;
                    BrowserLines.Add(BrowserLine.WeaponMode(modeLabel, mode.Key, string.Empty));
                }
            }

            List<FactionTechUnlock> factionUnlocks;
            int factionLinks = FactionTechUnlocksByItem.TryGetValue(itemId, out factionUnlocks) && factionUnlocks != null
                ? factionUnlocks.Count : 0;
            BrowserLines.Add(BrowserLine.Normal(
                Ui("ui.faction_technology"),
                factionLinks > 0 ? factionLinks.ToString(CultureInfo.InvariantCulture) : (Ui("ui.none"))));

            // Chip/datadisk contents belong on the item itself rather than in another tab.
            // The list reuses the datadisk graph already indexed for recipe chip indicators.
            List<string> chipUnlockItems = GetDatadiskUnlockedItemsSorted(itemId);
            if (chipUnlockItems.Count > 0)
            {
                BrowserLines.Add(BrowserLine.Section(
                    Ui("ui.chip_unlocks") + "  •  " + chipUnlockItems.Count.ToString(CultureInfo.InvariantCulture)));
                // Percentages are shown only while the current vanilla IL contract proves
                // UnlockIds -> Count -> Random.Range -> get_Item -> SetUnlockId. Unlock
                // contents remain useful even if a future game update invalidates that proof.
                if (_chipUnlockChanceContractVerified)
                    BrowserLines.Add(BrowserLine.ChipNote(Ui("ui.chip_unlock_chance_note")));

                for (int i = 0; i < chipUnlockItems.Count; i++)
                {
                    string unlockedItemId = chipUnlockItems[i];
                    int hits, total;
                    float chance;
                    string right = string.Empty;
                    if (TryGetDatadiskUnlockChance(itemId, unlockedItemId, out hits, out total, out chance))
                        right = FormatChipUnlockChance(chance);
                    bool? learned = IsProductionItemUnlocked(unlockedItemId);
                    int unlockStatus = !learned.HasValue ? 2 : (learned.Value ? 1 : -1);
                    BrowserLines.Add(BrowserLine.ChipUnlockAction(unlockedItemId, right, unlockStatus));
                }
            }

            if ((ShowSources || ShowTradeInformation) && BarterItemIds.Contains(itemId) &&
                magnum == 0 && used == 0 && crafted == 0 && sources == 0 && consumers == 0 && ammo == 0)
            {
                BrowserLines.Add(BrowserLine.Note(Ui("ui.recognized_as_a_trade_item_but_current_game_tabl")));
            }

            AppendBrowserModderOverview(itemId);
        }

        private static void BuildBrowserMagnum(string itemId)
        {
            bool ru = IsRussian();

            if (!ShowMagnumUses) return;

            if (!_compatMagnum)
            {
                AddCompatibilityUnavailableLine("Magnum");
                return;
            }
            EnsureRuntimeIndexesReady();
            TryResolveMagnumProgressionLightweight();

            List<MagnumUse> uses;
            if (!MagnumUses.TryGetValue(itemId, out uses) || uses == null || uses.Count == 0)
            {
                int required = GetVisibleMagnumRequired(itemId);
                if (required > 0)
                {
                    BrowserLines.Add(BrowserLine.Section(Ui("label.magnum")));
                    BrowserLines.Add(BrowserLine.Accent(Ui("ui.total_remaining"), required.ToString(CultureInfo.InvariantCulture)));
                    BrowserLines.Add(BrowserLine.FullNote(Ui("ui.vanilla_exposes_the_remaining_total_but_the_rela")));
                }
                else if (ShowFutureMagnumUses)
                    BrowserLines.Add(BrowserLine.FullNote(Ui("ui.no_related_magnum_research_was_found")));
                return;
            }

            if (!ShowFutureMagnumUses)
            {
                bool anyVisibleUse = false;
                for (int i = 0; i < uses.Count; i++)
                {
                    MagnumUse use = uses[i];
                    if (use == null) continue;
                    bool? purchased = CallBool(_magnumProgression, "IsPerkPurchased", use.PerkId);
                    if (purchased.HasValue && !purchased.Value)
                    {
                        bool? available = CallBool(_magnumProgression, "IsAvailableToUpgrade", use.PerkId);
                        if (!available.HasValue || !available.Value) continue;
                    }
                    anyVisibleUse = true;
                    break;
                }
                if (!anyVisibleUse) return;
            }

            int current = 0;
            int future = 0;
            int completed = 0;
            int unknownState = 0;
            int unknownRequired = 0;
            BrowserLines.Add(BrowserLine.Section(Ui("ui.magnum_research_2")));

            for (int i = 0; i < uses.Count; i++)
            {
                MagnumUse use = uses[i];
                if (use == null) continue;

                bool? purchased = CallBool(_magnumProgression, "IsPerkPurchased", use.PerkId);
                string state;
                if (!purchased.HasValue)
                {
                    state = Ui("ui.state_unknown");
                    unknownState++;
                    unknownRequired += Math.Max(0, use.Quantity);
                }
                else if (purchased.Value)
                {
                    state = Ui("ui.completed");
                    completed++;
                }
                else
                {
                    bool? available = CallBool(_magnumProgression, "IsAvailableToUpgrade", use.PerkId);
                    if (available.HasValue && available.Value)
                    {
                        state = Ui("ui.available");
                        current += use.Quantity;
                    }
                    else
                    {
                        if (!ShowFutureMagnumUses) continue;
                        state = Ui("ui.locked");
                        future += use.Quantity;
                    }
                }

                string route = BuildMagnumResearchRoute(use, ru);
                BrowserLines.Add(BrowserLine.MagnumResearchRow(
                    route,
                    "x" + use.Quantity.ToString(CultureInfo.InvariantCulture),
                    state));
            }

            BrowserLines.Add(BrowserLine.Section(Ui("ui.status")));
            if (completed > 0) BrowserLines.Add(BrowserLine.Normal(Ui("ui.completed_research"), completed.ToString(CultureInfo.InvariantCulture)));
            if (current > 0) BrowserLines.Add(BrowserLine.Normal(Ui("ui.available_now"), current.ToString(CultureInfo.InvariantCulture)));
            if (ShowFutureMagnumUses && future > 0)
                BrowserLines.Add(BrowserLine.Normal(Ui("ui.in_locked_branches"), future.ToString(CultureInfo.InvariantCulture)));

            int vanillaTotal = GetSafeMagnumRequired(itemId);
            int openResearchTotal = current +
                (ShowFutureMagnumUses ? unknownRequired + future : 0);

            // If MagnumProgression returned a definite purchased state for every linked
            // research, the per-research graph is more informative than vanilla /N.
            // In particular, "all linked research completed" must mean zero remaining.
            int effectiveTotal = ShowFutureMagnumUses && unknownState > 0
                ? vanillaTotal
                : openResearchTotal;

            BrowserLines.Add(BrowserLine.Accent(
                Ui("ui.required_for_open_research"),
                effectiveTotal.ToString(CultureInfo.InvariantCulture)));

            PriceSnapshot price;
            if (PriceByItem.TryGetValue(itemId, out price))
            {
                BrowserLines.Add(BrowserLine.Normal(Ui("ui.owned"), price.Owned.ToString(CultureInfo.InvariantCulture)));
                if (ShowMagnumSurplus)
                    BrowserLines.Add(BrowserLine.Normal(Ui("ui.after_open_research"),
                        Math.Max(0, price.Owned - effectiveTotal).ToString(CultureInfo.InvariantCulture)));
            }

            if (effectiveTotal == 0 && completed > 0 && unknownState == 0)
                BrowserLines.Add(BrowserLine.FullNote(Ui("ui.all_related_research_is_already_completed")));

            if (ShowFutureMagnumUses && unknownState > 0 && vanillaTotal != openResearchTotal)
            {
                Debug.LogWarning("[ItemIntelligence] Magnum remaining mismatch for " + itemId +
                    ": vanilla=" + vanillaTotal +
                    ", indexedOpen=" + openResearchTotal +
                    ", unknownStates=" + unknownState + ".");
            }
        }

        private static void BuildBrowserRecipes(string itemId)
        {
            bool ru = IsRussian();

            bool recipesAvailable = ShowRecipes && _compatRecipes;
            bool disassemblyAvailable = _compatDisassembly;

            if (!recipesAvailable && !disassemblyAvailable)
            {
                if (ShowRecipes) AddCompatibilityUnavailableLine("Recipes");
                AddCompatibilityUnavailableLine("Disassembly");

                return;
            }

            string relationId =
                ResolveStaticRelationItemId(itemId);

            List<RecipeUse> used = null;
            List<RecipeDef> crafted = null;
            List<DisassemblyOutput> disassembly = null;
            List<DisassemblySource> disassemblySources = null;

            bool hasUsed =
                recipesAvailable &&
                UsedInRecipes.TryGetValue(
                    relationId,
                    out used) &&
                used != null &&
                used.Count > 0;

            bool hasCrafted =
                recipesAvailable &&
                CraftedFromRecipes.TryGetValue(
                    relationId,
                    out crafted) &&
                crafted != null &&
                crafted.Count > 0;

            bool hasDisassembly =
                disassemblyAvailable &&
                DisassemblyOutputsByItem.TryGetValue(
                    itemId,
                    out disassembly) &&
                disassembly != null &&
                disassembly.Count > 0;

            bool hasDisassemblySources =
                disassemblyAvailable &&
                _disassemblyWarmupComplete &&
                DisassemblySourcesByOutputItem.TryGetValue(
                    itemId,
                    out disassemblySources) &&
                disassemblySources != null &&
                disassemblySources.Count > 0;

            if (!hasUsed && !hasCrafted && !hasDisassembly && !hasDisassemblySources)
            {
                if (_disassemblyWarmupActive)
                {
                    int pct = DisassemblyWarmupItems.Count <= 0 ? 0 :
                        Mathf.Clamp((_disassemblyWarmupIndex * 100) / DisassemblyWarmupItems.Count, 0, 100);
                    BrowserLines.Add(BrowserLine.Note((Ui("ui.disassembly_index")) +
                        pct.ToString(CultureInfo.InvariantCulture) + "%"));
                }
                else
                {
                    if (recipesAvailable && disassemblyAvailable)
                    {
                        BrowserLines.Add(
                            BrowserLine.Note(
                                Ui("ui.no_recipe_or_disassembly_relationships")));
                    }
                    else if (recipesAvailable)
                    {
                        BrowserLines.Add(
                            BrowserLine.Note(
                                Ui("ui.no_recipe_relationships")));

                        AddCompatibilityUnavailableLine(
                            "Disassembly");
                    }
                    else if (disassemblyAvailable)
                    {
                        BrowserLines.Add(
                            BrowserLine.Note(
                                Ui("ui.no_disassembly_relationships")));
                    }
                }
                return;
            }

            if (UsesInheritedStaticRelations(itemId))
                AddModifiedRelationBrowserNote(itemId);

            if (hasUsed)
            {
                Dictionary<string, RecipeUseGroup> groups = new Dictionary<string, RecipeUseGroup>(StringComparer.OrdinalIgnoreCase);
                for (int i = 0; i < used.Count; i++)
                {
                    RecipeUse use = used[i];
                    if (use == null || string.IsNullOrEmpty(use.OutputItemId)) continue;
                    string key = use.OutputItemId + "|" + use.Kind;
                    RecipeUseGroup group;
                    if (!groups.TryGetValue(key, out group))
                    {
                        group = new RecipeUseGroup(use.OutputItemId, use.Kind);
                        groups[key] = group;
                    }

                    group.Variants++;
                    if (!string.IsNullOrEmpty(use.RecipeId) && !group.RecipeIds.Contains(use.RecipeId))
                        group.RecipeIds.Add(use.RecipeId);
                    if (group.MinQuantity <= 0 || use.Quantity < group.MinQuantity) group.MinQuantity = use.Quantity;
                    if (use.Quantity > group.MaxQuantity) group.MaxQuantity = use.Quantity;
                }

                List<RecipeUseGroup> ordered = ConsolidateRecipeUseFamilies(new List<RecipeUseGroup>(groups.Values));
                BrowserLines.Add(BrowserLine.Section((Ui("ui.used_in_2")) + "  •  " + ordered.Count.ToString(CultureInfo.InvariantCulture)));
                ordered.Sort(delegate(RecipeUseGroup a, RecipeUseGroup b)
                {
                    return string.Compare(LocalizeItem(a.OutputItemId), LocalizeItem(b.OutputItemId), StringComparison.OrdinalIgnoreCase);
                });

                for (int i = 0; i < ordered.Count; i++)
                {
                    RecipeUseGroup group = ordered[i];
                    string quantity = group.MinQuantity == group.MaxQuantity
                        ? "x" + group.MinQuantity.ToString(CultureInfo.InvariantCulture)
                        : "x" + group.MinQuantity.ToString(CultureInfo.InvariantCulture) + "-" + group.MaxQuantity.ToString(CultureInfo.InvariantCulture);
                    string right = quantity + "  •  " + LocalizeKind(group.Kind, ru);
                    if (group.Variants > 1)
                        right += "  •  " + group.Variants.ToString(CultureInfo.InvariantCulture) + (Ui("ui.var"));

                    string chipItemId = GetFamilyPrimaryDatadisk(group.OutputItemIds);
                    int chipStatus = GetFamilyDatadiskStatus(group.OutputItemIds, chipItemId);
                    BrowserLines.Add(BrowserLine.RecipeItem(group.OutputItemId, right, chipItemId, chipStatus));
                }
            }

            if (hasCrafted)
            {
                BrowserLines.Add(BrowserLine.Section((Ui("ui.crafted_from_2")) + "  •  " + crafted.Count.ToString(CultureInfo.InvariantCulture)));
                for (int r = 0; r < crafted.Count; r++)
                {
                    RecipeDef recipe = crafted[r];
                    if (recipe == null) continue;
                    string kind = LocalizeKind(recipe.Kind, ru);
                    string label = (Ui("ui.recipe")) + (r + 1).ToString(CultureInfo.InvariantCulture);
                    string chipItemId = GetPrimaryDatadiskForItem(recipe.OutputItemId);
                    int chipStatus = GetDatadiskStatus(recipe.OutputItemId, chipItemId);
                    BrowserLines.Add(BrowserLine.RecipeHeader(label, kind, chipItemId, chipStatus));

                    foreach (KeyValuePair<string, int> ingredient in recipe.Ingredients)
                        BrowserLines.Add(BrowserLine.Item(ingredient.Key, "x" + ingredient.Value.ToString(CultureInfo.InvariantCulture)));
                }
            }

            if (hasDisassembly)
            {
                bool randomDisassemblyPool = IsRandomDisassemblyPool(itemId, disassembly);

                BrowserLines.Add(BrowserLine.Section(
                    (Ui("ui.disassembly_outputs")) +
                    "  •  " + disassembly.Count.ToString(CultureInfo.InvariantCulture)));

                if (randomDisassemblyPool)
                    BrowserLines.Add(BrowserLine.Note(Ui("ui.possible_outcomes")));

                List<DisassemblyOutput> orderedOutputs = new List<DisassemblyOutput>(disassembly);
                orderedOutputs.Sort(delegate(DisassemblyOutput a, DisassemblyOutput b)
                {
                    return string.Compare(LocalizeItem(a.ItemId), LocalizeItem(b.ItemId), StringComparison.OrdinalIgnoreCase);
                });

                for (int i = 0; i < orderedOutputs.Count; i++)
                {
                    DisassemblyOutput output = orderedOutputs[i];
                    if (output == null || string.IsNullOrEmpty(output.ItemId)) continue;
                    BrowserLines.Add(BrowserLine.Item(output.ItemId, FormatDisassemblyOutput(output, ru)));
                }
            }

            if (hasDisassemblySources)
            {
                List<DisassemblySource> orderedSources = new List<DisassemblySource>(disassemblySources);
                orderedSources.Sort(delegate(DisassemblySource a, DisassemblySource b)
                {
                    return string.Compare(LocalizeItem(a.ItemId), LocalizeItem(b.ItemId), StringComparison.OrdinalIgnoreCase);
                });

                BrowserLines.Add(BrowserLine.Section(
                    Ui("ui.obtained_by_disassembling") +
                    "  •  " + orderedSources.Count.ToString(CultureInfo.InvariantCulture)));

                for (int i = 0; i < orderedSources.Count; i++)
                {
                    DisassemblySource source = orderedSources[i];
                    if (source == null || string.IsNullOrEmpty(source.ItemId)) continue;
                    BrowserLines.Add(BrowserLine.Item(source.ItemId, FormatDisassemblySource(source, ru)));
                }
            }
        }

        private static void ConfigureInspectorText(TMP_Text text, float size, Color color, FontStyles style)
        {
            if (text == null) return;
            if (_inspectorFont != null) text.font = _inspectorFont;
            text.fontSize = size;
            text.color = color;
            text.fontStyle = style;
            text.raycastTarget = false;
            text.overflowMode = TextOverflowModes.Truncate;
            text.margin = new Vector4(6f, 0f, 6f, 0f);
        }

        private static void AddModifiedRelationBrowserNote(string itemId)
        {
            if (!UsesInheritedStaticRelations(itemId))
                return;

            string baseId = ResolveStaticRelationItemId(itemId);
            string baseName = NormalizeGameText(LocalizeItem(baseId));

            if (string.IsNullOrEmpty(baseName) ||
                string.Equals(baseName, baseId, StringComparison.OrdinalIgnoreCase))
                baseName = HumanizeIdentifier(baseId);

            BrowserLines.Add(BrowserLine.Note(Ui("note.improved_variant")));
        }
    }
}
