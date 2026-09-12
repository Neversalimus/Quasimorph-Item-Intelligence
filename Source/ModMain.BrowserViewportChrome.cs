using UnityEngine;

namespace ItemIntelligence
{
    public static partial class ModMain
    {
        private static void SetBrowserViewportRect(Transform parent, string name, float x, float y, float width, float height)
        {
            if (parent == null) return;
            Transform child = parent.Find(name);
            if (child == null) return;
            RectTransform rect = child.GetComponent<RectTransform>();
            if (rect == null) return;
            SetBrowserRectPositionIfChanged(rect, x, -y);
            SetBrowserRectSizeIfChanged(rect, width, height);
        }

        private static void LayoutBrowserViewportChrome()
        {
            if (_inspectorRoot == null) return;
            Transform root = _inspectorRoot.transform;
            float w = _browserViewport.Width, h = _browserViewport.Height, extra = w - BrowserBaseWidth;
            SetBrowserViewportRect(root, "Title", 78f, 5f, 326f + extra, 36f);
            SetBrowserViewportRect(root, "ItemId", 78f, 42f, 326f + extra, 20f);
            SetBrowserViewportRect(root, "FavoriteButton", 412f + extra, 14f, 54f, 34f);
            SetBrowserViewportRect(root, "BackButton", 474f + extra, 14f, 94f, 34f);
            SetBrowserViewportRect(root, "CloseButton", 576f + extra, 14f, 126f, 34f);
            SetBrowserViewportRect(root, "GlobalItemSearch", 18f, 72f, 536f + extra, 34f);
            bool catalogIcon = _browserInterfaceCatalogIcon != null && _browserInterfaceCatalogIcon.enabled;
            SetBrowserViewportRect(root, "SearchStatus", 562f + extra, 72f, catalogIcon ? 50f : 74f, 34f);
            SetBrowserViewportRect(root, "CatalogButton", (catalogIcon ? 618f : 642f) + extra, 72f,
                catalogIcon ? 100f : 76f, 34f);
            SetBrowserViewportRect(root, "Stats", 18f, 116f, 548f + extra, 34f);
            SetBrowserViewportRect(root, "TradeLayoutControls", 582f + extra, 118f, 136f, 30f);
            float tabWidth = 96f + extra / BrowserTabCount;
            for (int i = 0; i < BrowserTabCount; i++)
            {
                if (BrowserTabBackgrounds[i] == null) continue;
                RectTransform tab = BrowserTabBackgrounds[i].rectTransform;
                SetBrowserRectPositionIfChanged(tab, 16f + i * (tabWidth + 4f), -156f);
                SetBrowserRectSizeIfChanged(tab, tabWidth, 38f);
                if (BrowserTabTexts[i] != null)
                {
                    RectTransform label = BrowserTabTexts[i].rectTransform;
                    SetBrowserRectSizeIfChanged(label, tabWidth - 2f * label.anchoredPosition.x,
                        38f + label.anchoredPosition.y);
                }
                if (BrowserInterfaceTabIcons[i] != null)
                    SetBrowserRectPositionIfChanged(BrowserInterfaceTabIcons[i].rectTransform,
                        (tabWidth - 12f) * 0.5f, -2f);
            }
            SetBrowserViewportRect(root, "Rule_64", 12f, 64f, w - 24f, 1f);
            SetBrowserViewportRect(root, "Rule_202", 12f, 202f, w - 24f, 1f);
            SetBrowserViewportRect(root, "Rule_775", 12f, h - BrowserFooterHeight, w - 24f, 1f);
            SetBrowserViewportRect(root, "BrowserPageScrollbar", w - 12f, BrowserRowTop,
                10f, BrowserVisibleRows * BrowserRowPitch - 2f);
            SetBrowserViewportRect(root, "ScrollStatus", 18f, h - 98f, 210f, 52f);
            SetBrowserViewportRect(root, "Help", 235f, h - 98f, w - 253f, 52f);
            SetBrowserViewportRect(root, "ViewModeButton", 18f, h - 42f, 216f, 34f);
            SetBrowserViewportRect(root, "ZoomLabel", 242f, h - 42f, w - 498f, 34f);
            SetBrowserViewportRect(root, "ZoomMinusButton", w - 238f, h - 42f, 42f, 34f);
            SetBrowserViewportRect(root, "ZoomValue", w - 192f, h - 42f, 128f, 34f);
            SetBrowserViewportRect(root, "ZoomPlusButton", w - 60f, h - 42f, 42f, 34f);
            SetBrowserViewportRect(root, "LootIndexProgress", 18f, 207f, w - 36f, 12f);
            if (_lootProgressText != null)
                SetBrowserRectSizeIfChanged(_lootProgressText.rectTransform, w - 36f, 14f);
        }

        private static void LayoutBrowserCatalogViewport()
        {
            if (_browserCatalogPanel == null) return;
            float height = 214f + BrowserCatalogVisibleRows * 33f;
            RectTransform rect = _browserCatalogPanel.GetComponent<RectTransform>();
            SetBrowserRectPositionIfChanged(rect, (_browserViewport.Width - 700f) * 0.5f, -109f);
            SetBrowserRectSizeIfChanged(rect, 700f, height);
            SetBrowserViewportRect(_browserCatalogPanel.transform, "CatalogScrollbar", 687f, 175f,
                8f, BrowserCatalogVisibleRows * 33f - 2f);
            SetBrowserViewportRect(_browserCatalogPanel.transform, "CatalogScrollStatus", 8f,
                height - 37f, 676f, 27f);
        }
    }
}
