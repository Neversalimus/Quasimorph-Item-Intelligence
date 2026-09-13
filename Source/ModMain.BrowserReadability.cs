using TMPro;
using UnityEngine;

namespace ItemIntelligence
{
    public static partial class ModMain
    {
        // Only browser-owned text is affected. Window zoom and game/MCM fonts keep
        // their own settings. Both profiles preserve the same control rectangles.
        private static float BrowserNoteFontSize
        {
            get { return EnhancedReadability ? 15f : 13f; }
        }

        private static float BrowserHelpFontSize
        {
            get { return EnhancedReadability ? 14f : 13f; }
        }

        private static Color BrowserNoteColor
        {
            get { return EnhancedReadability
                ? new Color(0.72f, 0.84f, 0.75f, 1f)
                : new Color(0.52f, 0.70f, 0.61f, 1f); }
        }

        private static Color BrowserAvailableTabColor
        {
            get { return EnhancedReadability
                ? new Color(0.72f, 0.87f, 0.74f, 1f)
                : new Color(0.52f, 0.74f, 0.62f, 1f); }
        }

        private static Color BrowserHelpColor
        {
            get { return EnhancedReadability
                ? new Color(0.62f, 0.78f, 0.68f, 1f)
                : new Color(0.48f, 0.67f, 0.58f, 1f); }
        }

        private static void ApplyBrowserHelpReadability()
        {
            if (_browserHelpText == null) return;
            SetBrowserFontSizeIfChanged(_browserHelpText, BrowserHelpFontSize);
            SetBrowserGraphicColorIfChanged(_browserHelpText, BrowserHelpColor);
            SetBrowserFontStyleIfChanged(_browserHelpText, FontStyles.Normal);
        }
    }
}
