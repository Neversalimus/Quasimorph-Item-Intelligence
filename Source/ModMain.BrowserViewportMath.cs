using System;

namespace ItemIntelligence
{
    public static partial class ModMain
    {
        private const float BrowserBaseWidth = 736f;
        private const float BrowserRowTop = 221f;
        private const float BrowserRowPitch = 39f;
        private const float BrowserFooterHeight = 100f;
        private const float BrowserDrawerWidth = 232f;
        private const float BrowserScreenFraction = 0.94f;

        private struct BrowserViewportGeometry
        {
            public float Width, Height, Scale, CanvasWidth, CanvasHeight;
            public int Rows, CatalogRows;
        }

        private static int NormalizeBrowserZoom(int value)
        {
            value = Math.Max(100, Math.Min(200, value));
            return 100 + ((value - 100 + 12) / 25) * 25;
        }

        private static BrowserViewportGeometry CalculateBrowserViewport(
            int screenWidth, int screenHeight, bool expanded, int zoomPercent, bool drawer)
        {
            // Same geometric-mean scale as CanvasScaler's 1920x1080 / match=0.5.
            // No dependency on a not-yet-updated Canvas RectTransform during first F2.
            double w = Math.Max(1, screenWidth), h = Math.Max(1, screenHeight);
            double canvasScale = Math.Sqrt((w / 1920.0) * (h / 1080.0));
            BrowserViewportGeometry result = new BrowserViewportGeometry();
            result.CanvasWidth = (float)(w / canvasScale);
            result.CanvasHeight = (float)(h / canvasScale);
            float availableWidth = result.CanvasWidth * BrowserScreenFraction;
            float availableHeight = result.CanvasHeight * BrowserScreenFraction;
            float drawerWidth = drawer ? BrowserDrawerWidth : 0f;
            float minimumHeight = BrowserRowTop + 4f * BrowserRowPitch + BrowserFooterHeight;
            result.Scale = Math.Min(NormalizeBrowserZoom(zoomPercent) / 100f,
                Math.Min(availableWidth / (BrowserBaseWidth + drawerWidth), availableHeight / minimumHeight));
            result.Width = expanded ? availableWidth / result.Scale - drawerWidth : BrowserBaseWidth;
            result.Width = Math.Max(BrowserBaseWidth, result.Width);
            result.Height = expanded ? availableHeight / result.Scale : Math.Min(870f, availableHeight / result.Scale);
            result.Rows = Math.Max(4, Math.Min(BrowserRowCapacity,
                (int)Math.Floor((result.Height - BrowserRowTop - BrowserFooterHeight + 0.001f) / BrowserRowPitch)));
            // Fewer catalog rows keep its footer and view controls reachable at 200%.
            result.CatalogRows = Math.Max(3, Math.Min(BrowserCatalogRowCapacity,
                (int)Math.Floor((result.Height - 109f - 52f - 214f + 0.001f) / 33f)));
            return result;
        }

        private static float BrowserColumnCoordinate(float value, float width)
        {
            // Stretch column rectangles, never Transform.x or glyphs.
            return value * Math.Max(1f, (width - 28f) / 708f);
        }

        private static void CalculateBrowserWeaponTooltipPosition(BrowserViewportGeometry view,
            bool expanded, bool pinnedRight, bool drawer, float desiredY, float tooltipHeight,
            out float x, out float y)
        {
            float margin = view.CanvasWidth * (1f - BrowserScreenFraction) * 0.5f;
            float rootLeft = expanded
                ? (view.CanvasWidth - view.Width * view.Scale + (drawer ? BrowserDrawerWidth * view.Scale : 0f)) * 0.5f
                : (pinnedRight ? margin : view.CanvasWidth - margin - view.Width * view.Scale);
            float leftSpace = rootLeft / view.Scale;
            float rightSpace = (view.CanvasWidth - rootLeft) / view.Scale - view.Width;
            // Prefer free space outside. A full-screen hover card stays inside,
            // above the footer controls; all its graphics are non-interactive.
            if (leftSpace >= 410f) x = -398f;
            else if (rightSpace >= 410f) x = view.Width + 8f;
            else x = view.Width - 408f;
            y = -Math.Max(92f, Math.Min(-desiredY, view.Height - 52f - tooltipHeight));
        }
    }
}
