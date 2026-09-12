namespace ItemIntelligence
{
    public static partial class ModMain
    {
        private static int _viewportAssertions;
        private static void CheckViewport(bool condition, string message)
        {
            _viewportAssertions++;
            if (!condition) throw new System.Exception("Viewport: " + message);
        }

        public static int RunViewportCases()
        {
            _viewportAssertions = 0;
            int[,] resolutions = { {800,600}, {1024,768}, {1280,720}, {1366,768},
                {1600,900}, {1920,1080}, {1920,1200}, {2560,1440}, {3440,1440},
                {3840,2160}, {5120,1440}, {1080,1920} };
            for (int r = 0; r < resolutions.GetLength(0); r++)
            for (int expanded = 0; expanded < 2; expanded++)
            for (int drawer = 0; drawer < 2; drawer++)
            for (int zoom = 100; zoom <= 200; zoom += 25)
            {
                BrowserViewportGeometry g = CalculateBrowserViewport(
                    resolutions[r,0], resolutions[r,1], expanded != 0, zoom, drawer != 0);
                CheckViewport(g.Scale > 0f && g.Scale <= zoom / 100f, "uniform zoom bounded");
                CheckViewport(g.Width >= 736f, "columns never squeezed below design width");
                float widthFraction = (g.Width + (drawer != 0 ? 232f : 0f)) * g.Scale / g.CanvasWidth;
                float heightFraction = g.Height * g.Scale / g.CanvasHeight;
                CheckViewport(widthFraction <= 0.94001f && heightFraction <= 0.94001f, "screen margins include drawer");
                if (expanded != 0)
                    CheckViewport(System.Math.Abs(widthFraction - 0.94f) < 0.00001f &&
                        System.Math.Abs(heightFraction - 0.94f) < 0.00001f, "expanded screen coverage");
                else CheckViewport(g.Width == 736f && g.Height <= 870f, "normal window limits");
                CheckViewport(g.Rows >= 4 && g.Rows <= BrowserRowCapacity, "bounded visible pool");
                CheckViewport(221f + g.Rows * 39f <= g.Height - 100f + 0.001f, "rows above footer");
                CheckViewport(g.Rows == BrowserRowCapacity || 221f + (g.Rows + 1) * 39f > g.Height - 100f,
                    "all available rows used");
                CheckViewport(g.CatalogRows >= 3 && g.CatalogRows <= BrowserCatalogRowCapacity, "catalog pool bounds");
                CheckViewport(109f + 214f + g.CatalogRows * 33f <= g.Height - 52f + 0.001f,
                    "catalog does not cover zoom controls");
                CheckViewport(109f + 8f * 35f + 8f <= g.Height - 52f, "search does not cover zoom controls");
                CheckViewport(BrowserColumnCoordinate(698f, g.Width) <= g.Width - 28f,
                    "full note stays inside row");
                if (zoom < 200)
                {
                    BrowserViewportGeometry next = CalculateBrowserViewport(resolutions[r,0], resolutions[r,1],
                        expanded != 0, zoom + 25, drawer != 0);
                    CheckViewport(next.Scale >= g.Scale && next.Rows <= g.Rows, "zoom grows text and reduces row count");
                }
                VerifyViewportChrome(g, false);
                VerifyViewportChrome(g, true);
                for (int pinned = 0; pinned < 2; pinned++)
                foreach (float rowY in new float[] { -221f, -650f, -1300f })
                {
                    float x, y;
                    CalculateBrowserWeaponTooltipPosition(g, expanded != 0, pinned != 0, drawer != 0,
                        rowY + 18f, 312f, out x, out y);
                    float margin = g.CanvasWidth * 0.03f;
                    float rootLeft = expanded != 0
                        ? (g.CanvasWidth - g.Width * g.Scale + (drawer != 0 ? 232f * g.Scale : 0f)) * 0.5f
                        : (pinned != 0 ? margin : g.CanvasWidth - margin - g.Width * g.Scale);
                    CheckViewport(rootLeft + x * g.Scale >= -0.001f &&
                        rootLeft + (x + 390f) * g.Scale <= g.CanvasWidth + 0.001f, "hover card screen bounds");
                    CheckViewport(-y >= 92f && -y + 312f <= g.Height - 52f + 0.001f, "hover card above view controls");
                }
            }
            BrowserViewportGeometry normal = CalculateBrowserViewport(1920, 1080, false, 100, false);
            CheckViewport(normal.Rows == 14 && normal.Height == 870f && normal.Width == 736f && normal.Scale == 1f,
                "normal default retains legacy content geometry");
            BrowserViewportGeometry large = CalculateBrowserViewport(1920, 1080, true, 150, false);
            CheckViewport(large.Rows == 9 && large.Scale == 1.5f, "1080p large default gives 50 percent larger text");
            CheckViewport(NormalizeBrowserZoom(int.MinValue) == 100 && NormalizeBrowserZoom(int.MaxValue) == 200,
                "invalid config bounds");
            CheckViewport(NormalizeBrowserZoom(137) == 125 && NormalizeBrowserZoom(138) == 150,
                "config snaps to supported increments");
            BrowserViewportGeometry invalid = CalculateBrowserViewport(0, -1, true, -999, true);
            CheckViewport(!float.IsNaN(invalid.Scale) && !float.IsInfinity(invalid.Height), "startup dimension guard");
            return _viewportAssertions;
        }

        private static void VerifyViewportChrome(BrowserViewportGeometry g, bool icons)
        {
            _browserViewport = g;
            BrowserVisibleRows = g.Rows;
            BrowserCatalogVisibleRows = g.CatalogRows;
            _inspectorRoot = new GameObject();
            string[] names = { "Title", "ItemId", "FavoriteButton", "BackButton", "CloseButton",
                "GlobalItemSearch", "SearchStatus", "CatalogButton", "Stats", "TradeLayoutControls",
                "Rule_64", "Rule_202", "Rule_775", "BrowserPageScrollbar", "ScrollStatus", "Help",
                "ViewModeButton", "ZoomLabel", "ZoomMinusButton", "ZoomValue", "ZoomPlusButton", "LootIndexProgress" };
            foreach (string name in names) _inspectorRoot.transform.Children.Add(name, new Transform());
            _browserCatalogPanel = new GameObject();
            _browserCatalogPanel.transform.Children.Add("CatalogScrollbar", new Transform());
            _browserCatalogPanel.transform.Children.Add("CatalogScrollStatus", new Transform());
            _lootProgressText = new Graphic();
            _browserInterfaceCatalogIcon = new Graphic();
            _browserInterfaceCatalogIcon.enabled = icons;
            for (int i = 0; i < BrowserTabCount; i++)
            {
                BrowserTabBackgrounds[i] = new Graphic();
                BrowserTabTexts[i] = new Graphic();
                BrowserTabTexts[i].rectTransform.anchoredPosition = icons ? new Vector2(2f, -14f) : new Vector2(0f, 0f);
                BrowserInterfaceTabIcons[i] = new Graphic();
            }
            LayoutBrowserViewportChrome();
            LayoutBrowserCatalogViewport();
            foreach (string name in names)
            {
                RectTransform rect = _inspectorRoot.transform.Children[name].Rect;
                CheckViewport(rect.sizeDelta.x > 0f && rect.sizeDelta.y > 0f, "positive rectangle " + name);
                CheckViewport(rect.anchoredPosition.x >= 0f && rect.anchoredPosition.x + rect.sizeDelta.x <= g.Width + 0.001f,
                    "horizontal boundary " + name);
                CheckViewport(-rect.anchoredPosition.y >= 0f && -rect.anchoredPosition.y + rect.sizeDelta.y <= g.Height + 0.001f,
                    "vertical boundary " + name);
            }
            CheckHorizontalGap("Title", "FavoriteButton");
            CheckHorizontalGap("FavoriteButton", "BackButton");
            CheckHorizontalGap("BackButton", "CloseButton");
            CheckHorizontalGap("GlobalItemSearch", "SearchStatus");
            CheckHorizontalGap("SearchStatus", "CatalogButton");
            CheckHorizontalGap("Stats", "TradeLayoutControls");
            CheckHorizontalGap("ScrollStatus", "Help");
            CheckVerticalGap("Title", "ItemId");
            CheckVerticalGap("ItemId", "Rule_64");
            CheckVerticalGap("Rule_775", "Help");
            CheckVerticalGap("Help", "ZoomLabel");
            CheckVerticalGap("ScrollStatus", "ViewModeButton");
            CheckHorizontalGap("ViewModeButton", "ZoomLabel");
            CheckHorizontalGap("ZoomLabel", "ZoomMinusButton");
            CheckHorizontalGap("ZoomMinusButton", "ZoomValue");
            CheckHorizontalGap("ZoomValue", "ZoomPlusButton");
            for (int i = 0; i < BrowserTabCount; i++)
            {
                RectTransform rect = BrowserTabBackgrounds[i].rectTransform;
                RectTransform label = BrowserTabTexts[i].rectTransform;
                CheckViewport(label.anchoredPosition.x + label.sizeDelta.x <= rect.sizeDelta.x + 0.001f,
                    "tab label stays inside expanded button");
                CheckViewport(-label.anchoredPosition.y + label.sizeDelta.y <= rect.sizeDelta.y,
                    "two-level tab label stays above progress bar");
                if (icons)
                    CheckViewport(_inspectorRoot.transform.Children["CatalogButton"].Rect.sizeDelta.x >= 97f,
                        "catalog label and glyph fit button");
                if (i + 1 < BrowserTabCount)
                    CheckViewport(rect.anchoredPosition.x + rect.sizeDelta.x < BrowserTabBackgrounds[i+1].rectTransform.anchoredPosition.x,
                        "tabs separated");
            }
            RectTransform catalog = _browserCatalogPanel.transform.Rect;
            CheckViewport(catalog.anchoredPosition.x >= 0f && catalog.anchoredPosition.x + catalog.sizeDelta.x <= g.Width,
                "catalog horizontal fit");
            float titleWidth = _inspectorRoot.transform.Children["Title"].Rect.sizeDelta.x;
            LayoutBrowserViewportChrome();
            CheckViewport(_inspectorRoot.transform.Children["Title"].Rect.sizeDelta.x == titleWidth, "idempotent layout");
        }

        private static void CheckHorizontalGap(string first, string second)
        {
            RectTransform a = _inspectorRoot.transform.Children[first].Rect;
            RectTransform b = _inspectorRoot.transform.Children[second].Rect;
            CheckViewport(a.anchoredPosition.x + a.sizeDelta.x <= b.anchoredPosition.x + 0.001f,
                first + " separated from " + second);
        }

        private static void CheckVerticalGap(string first, string second)
        {
            RectTransform a = _inspectorRoot.transform.Children[first].Rect;
            RectTransform b = _inspectorRoot.transform.Children[second].Rect;
            CheckViewport(-a.anchoredPosition.y + a.sizeDelta.y <= -b.anchoredPosition.y + 0.001f,
                first + " stays above " + second);
        }

        private struct Vector2 { public float x, y; public Vector2(float a, float b) { x = a; y = b; } }
        private sealed class RectTransform { public Vector2 anchoredPosition, sizeDelta; }
        private sealed class Transform
        {
            public readonly Dictionary<string, Transform> Children = new Dictionary<string, Transform>();
            public readonly RectTransform Rect = new RectTransform();
            public Transform Find(string name) { Transform value; return Children.TryGetValue(name, out value) ? value : null; }
            public T GetComponent<T>() where T : class { return Rect as T; }
        }
        private sealed class GameObject
        {
            public readonly Transform transform = new Transform();
            public T GetComponent<T>() where T : class { return transform.GetComponent<T>(); }
        }
        private sealed class Graphic { public bool enabled; public readonly RectTransform rectTransform = new RectTransform(); }
        private const int BrowserTabCount = 7;
        private static readonly Graphic[] BrowserTabBackgrounds = new Graphic[BrowserTabCount];
        private static readonly Graphic[] BrowserTabTexts = new Graphic[BrowserTabCount];
        private static readonly Graphic[] BrowserInterfaceTabIcons = new Graphic[BrowserTabCount];
        private static Graphic _browserInterfaceCatalogIcon;
        private static GameObject _inspectorRoot, _browserCatalogPanel;
        private static Graphic _lootProgressText;
        private static int BrowserVisibleRows, BrowserCatalogVisibleRows;
        private static BrowserViewportGeometry _browserViewport;
        private static void SetBrowserRectPositionIfChanged(RectTransform rect, float x, float y)
        { if (rect != null) rect.anchoredPosition = new Vector2(x, y); }
        private static void SetBrowserRectSizeIfChanged(RectTransform rect, float width, float height)
        { if (rect != null) rect.sizeDelta = new Vector2(width, height); }
    }
}
