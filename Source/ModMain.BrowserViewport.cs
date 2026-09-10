using System;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ItemIntelligence
{
    public static partial class ModMain
    {
        private static bool BrowserExpanded;
        private static int BrowserWindowZoom = 100;
        private static int BrowserExpandedZoom = 150;
        private static BrowserViewportGeometry _browserViewport = CalculateBrowserViewport(1920, 1080, false, 100, false);
        private static int _browserViewportScreenWidth, _browserViewportScreenHeight;
        private static bool _browserViewportExpanded, _browserViewportDrawer;
        private static int _browserViewportZoom;
        private static TMP_Text _browserViewModeText, _browserZoomLabel, _browserZoomValue;
        private static Button _browserZoomMinus, _browserZoomPlus;

        private static int BrowserZoomPercent
        {
            get { return BrowserExpanded ? BrowserExpandedZoom : BrowserWindowZoom; }
        }

        private static bool ApplyBrowserViewport()
        {
            if (_inspectorRect == null) return false;
            int width = Math.Max(1, Screen.width), height = Math.Max(1, Screen.height);
            if (_browserViewportScreenWidth == width && _browserViewportScreenHeight == height &&
                _browserViewportExpanded == BrowserExpanded && _browserViewportDrawer == ModderMode &&
                _browserViewportZoom == BrowserZoomPercent) return false;
            _browserViewportScreenWidth = width;
            _browserViewportScreenHeight = height;
            _browserViewportExpanded = BrowserExpanded;
            _browserViewportDrawer = ModderMode;
            _browserViewportZoom = BrowserZoomPercent;
            _browserViewport = CalculateBrowserViewport(width, height, BrowserExpanded, BrowserZoomPercent, ModderMode);
            BrowserVisibleRows = _browserViewport.Rows;
            BrowserCatalogVisibleRows = _browserViewport.CatalogRows;
            _inspectorRect.sizeDelta = new Vector2(_browserViewport.Width, _browserViewport.Height);
            _inspectorRect.localScale = Vector3.one * _browserViewport.Scale;
            for (int i = 0; i < BrowserRowCapacity; i++)
            {
                GameObject row = BrowserRowRoots[i];
                if (row == null) continue;
                RectTransform rt = row.GetComponent<RectTransform>();
                SetBrowserRectSizeIfChanged(rt, _browserViewport.Width - 28f, 37f);
                if (BrowserRowActionIcons[i] != null)
                    SetBrowserRowRectPosition(BrowserRowActionIcons[i].rectTransform, 674f, 0f);
                if (i >= BrowserVisibleRows) SetBrowserActiveIfChanged(row, false);
            }
            InvalidateBrowserRowRenderCache();
            LayoutBrowserViewportChrome();
            LayoutBrowserCatalogViewport();
            UpdateBrowserViewportControls();
            Debug.Log("[ItemIntelligence][BrowserViewport] screen=" + width + "x" + height +
                ", expanded=" + BrowserExpanded + ", requestedZoom=" + BrowserZoomPercent +
                ", effectiveZoom=" + (_browserViewport.Scale * 100f).ToString("0.#", CultureInfo.InvariantCulture) +
                ", rows=" + BrowserVisibleRows + ", catalogRows=" + BrowserCatalogVisibleRows + ".");
            return true;
        }

        private static void TickBrowserViewport()
        {
            if (!_inspectorOpen || !ApplyBrowserViewport()) return;
            // Rebuild width-dependent notes and clamp scroll offsets on resize.
            RenderBrowser(_inspectorItemId);
        }

        private static void ChangeBrowserView(bool toggleMode, int zoomStep)
        {
            if (toggleMode) BrowserExpanded = !BrowserExpanded;
            else
            {
                int requested = BrowserZoomPercent + zoomStep;
                if (zoomStep < 0 && _browserViewport.Scale * 100f < BrowserZoomPercent - 0.1f)
                    requested = (int)Math.Floor((_browserViewport.Scale * 100f - 0.1f) / 25f) * 25;
                if (BrowserExpanded) BrowserExpandedZoom = NormalizeBrowserZoom(requested);
                else BrowserWindowZoom = NormalizeBrowserZoom(requested);
            }
            ApplyBrowserViewport();
            RenderBrowser(_inspectorItemId);
            if (!SaveConfig() && _browserHelpText != null)
                SetBrowserTextIfChanged(_browserHelpText, NormalizeModUiText(Ui("ui.view_save_failed")));
        }

        private static void CreateBrowserViewportControls()
        {
            Image background;
            TMP_Text glyph;
            Button mode = CreateBrowserHeaderActionButton("ViewModeButton", 18f, 216f, 14f,
                out background, out _browserViewModeText, delegate { ChangeBrowserView(true, 0); });
            _browserZoomMinus = CreateBrowserHeaderActionButton("ZoomMinusButton", 498f, 42f, 22f,
                out background, out glyph, delegate { ChangeBrowserView(false, -25); });
            glyph.text = "-";
            _browserZoomPlus = CreateBrowserHeaderActionButton("ZoomPlusButton", 676f, 42f, 22f,
                out background, out glyph, delegate { ChangeBrowserView(false, 25); });
            glyph.text = "+";
            // Reuse button styling; the view controls live in the footer.
            mode.navigation = NoBrowserViewNavigation();
            _browserZoomMinus.navigation = NoBrowserViewNavigation();
            _browserZoomPlus.navigation = NoBrowserViewNavigation();
            _browserZoomLabel = CreateBrowserText("ZoomLabel", _inspectorRoot.transform,
                Vector2.zero, new Vector2(248f, 34f), 14f,
                new Color(0.48f, 0.74f, 0.62f, 1f), FontStyles.Normal,
                TextAlignmentOptions.MidlineRight).GetComponent<TMP_Text>();
            _browserZoomValue = CreateBrowserText("ZoomValue", _inspectorRoot.transform,
                Vector2.zero, new Vector2(128f, 34f), 16f,
                new Color(0.88f, 0.90f, 0.62f, 1f), FontStyles.Bold,
                TextAlignmentOptions.Center).GetComponent<TMP_Text>();
        }

        private static Navigation NoBrowserViewNavigation()
        {
            Navigation navigation = new Navigation();
            navigation.mode = Navigation.Mode.None;
            return navigation;
        }

        private static void UpdateBrowserViewportControls()
        {
            SetBrowserTextIfChanged(_browserViewModeText, NormalizeModUiText(Ui(
                BrowserExpanded ? "ui.view_window" : "ui.view_expand")));
            SetBrowserTextIfChanged(_browserZoomLabel, NormalizeModUiText(Ui("ui.view_zoom")));
            // Retain the preference when automatic screen fitting limits actual zoom.
            SetBrowserTextIfChanged(_browserZoomValue,
                (_browserViewport.Scale * 100f).ToString("0", CultureInfo.InvariantCulture) + "%");
            SetBrowserInteractableIfChanged(_browserZoomMinus, BrowserZoomPercent > 100);
            SetBrowserInteractableIfChanged(_browserZoomPlus, BrowserZoomPercent < 200 &&
                _browserViewport.Scale * 100f >= BrowserZoomPercent - 0.1f);
        }

        private static void SetBrowserRowRectPosition(RectTransform rect, float x, float y)
        {
            SetBrowserRectPositionIfChanged(rect, BrowserColumnCoordinate(x, _browserViewport.Width), y);
        }

        private static void SetBrowserRowTextSize(RectTransform rect, float width, float height)
        {
            SetBrowserRectSizeIfChanged(rect, BrowserColumnCoordinate(width, _browserViewport.Width), height);
        }
    }
}
