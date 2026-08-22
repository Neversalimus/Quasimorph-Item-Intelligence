using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ItemIntelligence
{
    /// <summary>
    /// Presents related-item navigation as an intentional card instead of the retired
    /// debug-looking text protocol. Only true item links receive the navigation glyph;
    /// dense tables, notes and numeric rows deliberately remain icon-free.
    /// </summary>
    public static partial class ModMain
    {
        private static void ResetBrowserLinkPresentation(int slot)
        {
            if (slot < 0 || slot >= BrowserRowActionIcons.Length) return;
            Image icon = BrowserRowActionIcons[slot];
            if (icon == null) return;
            SetBrowserImageSpriteIfChanged(icon, null);
            SetBrowserImageEnabledIfChanged(icon, false);
        }

        private static bool TryRenderBrowserItemLink(ref BrowserRowRenderContext ctx)
        {
            BrowserLine line = ctx.Line;
            if (line == null || line.Action.Kind != BrowserActionKind.OpenItem) return false;

            TMP_Text right = ctx.Right;
            string rightText = string.IsNullOrEmpty(line.Right)
                ? Ui("ui.open_item_link")
                : line.Right;
            SetBrowserTextIfChanged(ctx.Left, NormalizeModUiText(ctx.LeftText));
            SetBrowserTextIfChanged(right, NormalizeModUiText(rightText));
            SetBrowserFontSizeIfChanged(right, 13.5f);
            SetBrowserFontStyleIfChanged(right, FontStyles.Bold);

            Image icon = BrowserRowActionIcons[ctx.Slot];
            if (icon != null && BrowserInterfaceIconLayoutEnabled)
            {
                SetBrowserImageSpriteIfChanged(icon,
                    GetBrowserInterfaceIconSprite(BrowserInterfaceIconKind.OpenItem));
                SetBrowserGraphicColorIfChanged(icon, new Color(0.94f, 0.86f, 0.53f, 1f));
                SetBrowserImageEnabledIfChanged(icon, icon.sprite != null);
                if (ctx.RightRt != null)
                    SetBrowserRectSizeIfChanged(ctx.RightRt, 166f, ctx.RightRt.sizeDelta.y);
            }
            return true;
        }

        private static void ApplyBrowserItemLinkFinalStyle(ref BrowserRowRenderContext ctx)
        {
            BrowserLine line = ctx.Line;
            if (line == null || line.Action.Kind != BrowserActionKind.OpenItem) return;
            SetBrowserFontStyleIfChanged(ctx.Left, FontStyles.Bold);
            SetBrowserGraphicColorIfChanged(ctx.Left, new Color(0.72f, 0.92f, 0.72f, 1f));
            SetBrowserGraphicColorIfChanged(ctx.Right, new Color(0.95f, 0.86f, 0.52f, 1f));
            if (ctx.Background != null)
                SetBrowserGraphicColorIfChanged(ctx.Background, new Color(0.024f, 0.082f, 0.060f, 0.94f));
            if (ctx.RowOutline != null)
            {
                SetBrowserOutlineColorIfChanged(ctx.RowOutline, new Color(0.35f, 0.73f, 0.53f, 0.82f));
                SetBrowserOutlineDistanceIfChanged(ctx.RowOutline, new Vector2(1f, -1f));
            }
        }
    }
}
