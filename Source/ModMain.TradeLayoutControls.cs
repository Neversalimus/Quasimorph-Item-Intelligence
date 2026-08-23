using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ItemIntelligence
{
    /// <summary>
    /// Direct Trade-layout UI. The in-browser controls are authoritative for the
    /// current session and persist the same config key used by MCM.
    /// </summary>
    public static partial class ModMain
    {
        private static GameObject _browserTradeLayoutRoot;
        private static TMP_Text _browserTradeLayoutLabel;
        private static Button _browserTradeCardsButton;
        private static Button _browserTradeTableButton;
        private static Image _browserTradeCardsBackground;
        private static Image _browserTradeTableBackground;
        private static TMP_Text _browserTradeCardsText;
        private static TMP_Text _browserTradeTableText;
        private static Image _browserTradeCardsIcon;
        private static Image _browserTradeTableIcon;

        private static void CreateBrowserTradeLayoutControls()
        {
            if (_inspectorRoot == null || _browserTradeLayoutRoot != null) return;

            _browserTradeLayoutRoot = new GameObject("TradeLayoutControls");
            _browserTradeLayoutRoot.transform.SetParent(_inspectorRoot.transform, false);
            RectTransform rootRt = _browserTradeLayoutRoot.AddComponent<RectTransform>();
            rootRt.anchorMin = new Vector2(0f, 1f);
            rootRt.anchorMax = new Vector2(0f, 1f);
            rootRt.pivot = new Vector2(0f, 1f);
            rootRt.anchoredPosition = new Vector2(582f, -118f);
            rootRt.sizeDelta = new Vector2(136f, 30f);

            GameObject labelGo = CreateBrowserText("TradeLayoutLabel", _browserTradeLayoutRoot.transform,
                new Vector2(0f, 0f), new Vector2(42f, 28f),
                11.0f, new Color(0.35f, 0.58f, 0.52f, 0.95f), FontStyles.Normal,
                TextAlignmentOptions.MidlineRight);
            _browserTradeLayoutLabel = labelGo.GetComponent<TMP_Text>();

            _browserTradeCardsButton = CreateTradeLayoutMiniButton(
                "TradeCardsButton", 44f, out _browserTradeCardsBackground, out _browserTradeCardsText);
            _browserTradeTableButton = CreateTradeLayoutMiniButton(
                "TradeTableButton", 90f, out _browserTradeTableBackground, out _browserTradeTableText);

            _browserTradeCardsButton.onClick.AddListener(delegate { SetTradeLayoutFromBrowser(false); });
            _browserTradeTableButton.onClick.AddListener(delegate { SetTradeLayoutFromBrowser(true); });

            _browserTradeCardsIcon = CreateBrowserInterfaceIcon(
                "TradeCardsGlyph", _browserTradeCardsButton.transform,
                BrowserInterfaceIconKind.Catalog, new Vector2(11f, -6f), new Vector2(16f, 16f),
                new Color(0.52f, 0.78f, 0.64f, 1f), _browserTradeCardsText, true,
                Vector2.zero, Vector2.zero, TextAlignmentOptions.Center);
            _browserTradeTableIcon = CreateBrowserInterfaceIcon(
                "TradeTableGlyph", _browserTradeTableButton.transform,
                BrowserInterfaceIconKind.Sort, new Vector2(11f, -6f), new Vector2(16f, 16f),
                new Color(0.52f, 0.78f, 0.64f, 1f), _browserTradeTableText, true,
                Vector2.zero, Vector2.zero, TextAlignmentOptions.Center);

            UpdateBrowserTradeLayoutControls();
        }

        private static Button CreateTradeLayoutMiniButton(
            string name, float x, out Image background, out TMP_Text label)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(_browserTradeLayoutRoot.transform, false);
            RectTransform rt = go.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(x, 0f);
            rt.sizeDelta = new Vector2(40f, 28f);

            background = go.AddComponent<Image>();
            background.color = new Color(0.018f, 0.055f, 0.046f, 0.92f);
            background.raycastTarget = true;

            Outline outline = go.AddComponent<Outline>();
            outline.effectColor = new Color(0.30f, 0.63f, 0.50f, 0.42f);
            outline.effectDistance = new Vector2(1f, -1f);

            Button button = go.AddComponent<Button>();
            button.targetGraphic = background;
            button.transition = Selectable.Transition.ColorTint;
            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.08f, 1.08f, 1.08f, 1f);
            colors.pressedColor = new Color(0.88f, 0.96f, 0.90f, 1f);
            colors.selectedColor = Color.white;
            colors.disabledColor = new Color(0.45f, 0.45f, 0.45f, 0.55f);
            colors.colorMultiplier = 1f;
            colors.fadeDuration = 0.06f;
            button.colors = colors;
            Navigation navigation = button.navigation;
            navigation.mode = Navigation.Mode.None;
            button.navigation = navigation;

            GameObject textGo = CreateBrowserText("TradeLayoutFallback", go.transform,
                Vector2.zero, new Vector2(40f, 28f),
                12f, new Color(0.52f, 0.78f, 0.64f, 1f), FontStyles.Bold,
                TextAlignmentOptions.Center);
            label = textGo.GetComponent<TMP_Text>();
            return button;
        }

        private static void UpdateBrowserTradeLayoutControls()
        {
            if (_browserTradeLayoutRoot == null) return;

            bool visible = BrowserNavigation.Tab == (int)BrowserTabId.Trade &&
                (ShowSources || ShowTradeInformation) && _compatTrade;
            if (_browserTradeLayoutRoot.activeSelf != visible)
                _browserTradeLayoutRoot.SetActive(visible);
            if (!visible) return;

            if (_browserTradeLayoutLabel != null)
                SetBrowserTextIfChanged(_browserTradeLayoutLabel, NormalizeModUiText(Ui("ui.trade_layout_view")));
            if (_browserTradeCardsText != null)
                SetBrowserTextIfChanged(_browserTradeCardsText, NormalizeModUiText(Ui("ui.trade_layout_cards_short")));
            if (_browserTradeTableText != null)
                SetBrowserTextIfChanged(_browserTradeTableText, NormalizeModUiText(Ui("ui.trade_layout_table_short")));

            Color activeBg = new Color(0.070f, 0.165f, 0.112f, 0.98f);
            Color idleBg = new Color(0.018f, 0.055f, 0.046f, 0.92f);
            Color activeFg = new Color(0.90f, 0.90f, 0.62f, 1f);
            Color idleFg = new Color(0.52f, 0.78f, 0.64f, 1f);

            bool table = UsePreviousTradeLayout;
            SetBrowserGraphicColorIfChanged(_browserTradeCardsBackground, table ? idleBg : activeBg);
            SetBrowserGraphicColorIfChanged(_browserTradeTableBackground, table ? activeBg : idleBg);
            SetBrowserGraphicColorIfChanged(_browserTradeCardsText, table ? idleFg : activeFg);
            SetBrowserGraphicColorIfChanged(_browserTradeTableText, table ? activeFg : idleFg);
            SetBrowserInterfaceIconColor(_browserTradeCardsIcon, table ? idleFg : activeFg);
            SetBrowserInterfaceIconColor(_browserTradeTableIcon, table ? activeFg : idleFg);
        }

        private static void SetTradeLayoutFromBrowser(bool table)
        {
            if (!_inspectorOpen || BrowserNavigation.Tab != (int)BrowserTabId.Trade) return;
            if (UsePreviousTradeLayout == table)
            {
                UpdateBrowserTradeLayoutControls();
                return;
            }

            string before = UsePreviousTradeLayout ? "Table" : "Cards";
            UsePreviousTradeLayout = table;
            bool persisted = SaveConfig();
            UpdateBrowserTradeLayoutControls();

            Debug.Log("[ItemIntelligence][TradeLayoutSwitch] source=TradeWindow, " + before +
                " -> " + (UsePreviousTradeLayout ? "Table" : "Cards") +
                ", persisted=" + persisted + ".");

            if (!string.IsNullOrEmpty(_inspectorItemId))
                RenderBrowser(_inspectorItemId);
        }
    }
}
