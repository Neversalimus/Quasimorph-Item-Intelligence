using System;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ItemIntelligence
{
    /// <summary>
    /// Owns the pooled browser row render loop. Row preparation, specialized layouts and
    /// final styling live in BrowserRowRendererParts so this owner stays orchestration-only.
    /// </summary>
    public static partial class ModMain
    {
        private static void RenderBrowserRowsOnly()
        {
            HideBrowserWeaponModeTooltip();
            BeginBrowserRowRenderReusePass();
            string renderLanguage = GetLanguageSignature();
            int total = BrowserLines.Count;
            int maxOffset = Math.Max(0, total - BrowserVisibleRows);
            BrowserNavigation.ScrollOffset = Mathf.Clamp(BrowserNavigation.ScrollOffset, 0, maxOffset);
            if (BrowserNavigation.Tab >= 0 && BrowserNavigation.Tab < BrowserNavigation.ScrollOffsets.Length)
                BrowserNavigation.ScrollOffsets[BrowserNavigation.Tab] = BrowserNavigation.ScrollOffset;

            int startIndex = BrowserNavigation.ScrollOffset;
            for (int i = 0; i < BrowserVisibleRows; i++)
            {
                GameObject root = BrowserRowRoots[i];
                TMP_Text left = BrowserRowLeft[i];
                TMP_Text right = BrowserRowRight[i];
                if (root == null || left == null || right == null) continue;

                int lineIndex = startIndex + i;
                if (lineIndex >= total)
                {
                    SetBrowserInteractableIfChanged(BrowserRowButtons[i], false);
                    SetBrowserRaycastTargetIfChanged(left, false);
                    SetBrowserActiveIfChanged(root, false);
                    continue;
                }

                BrowserLine line = BrowserLines[lineIndex];
                if (CanReuseBrowserRowRender(i, line, renderLanguage))
                {
                    RestoreCachedBrowserRowBindings(i, line);
                    continue;
                }

                BrowserRowRenderContext context = new BrowserRowRenderContext();
                InitializeBrowserRowRenderContext(ref context, i, line, root, left, right);
                PrepareBrowserRowForRender(ref context);
                RenderBrowserRowContent(ref context);
                ApplyBrowserRowFinalStyle(ref context);
                CaptureBrowserRowRenderStamp(i, line, renderLanguage);
            }

            UpdateBrowserRowScrollChrome(total);
            EndBrowserRowRenderReusePass();
        }
    }
}
