using System;

namespace ItemIntelligence
{
    public static partial class ModMain
    {
        // Vanilla Quasimorph action consumed by UI.Process/DungeonHudScreen.Process.
        // QII never emulates the ALT panel and never patches TooltipFactory's builders;
        // it only lets this one original action query pass while a QII-owned native
        // ItemTooltipHandler is actually active.
        private const string BrowserTooltipMoreActionId = "UI_TooltipMore";
        private const string BrowserTooltipMoreLayout = "UI";

        private static bool _browserNativeTooltipMoreActive;
        private static object _browserNativeTooltipMoreHandler;
        private static string _browserNativeTooltipMoreItemId = string.Empty;

        private static void SetBrowserNativeTooltipMoreTarget(object handler, string itemId)
        {
            if (!_inspectorOpen || !_compatTooltipMore || handler == null ||
                string.IsNullOrEmpty(itemId) || !IsBrowserOwnedItemTooltipHandler(handler))
            {
                ClearBrowserNativeTooltipMoreTarget(null);
                return;
            }

            _browserNativeTooltipMoreHandler = handler;
            _browserNativeTooltipMoreItemId = itemId;
            _browserNativeTooltipMoreActive = true;
        }

        private static void ClearBrowserNativeTooltipMoreTarget(object handler)
        {
            if (handler != null &&
                !object.ReferenceEquals(handler, _browserNativeTooltipMoreHandler))
                return;

            _browserNativeTooltipMoreActive = false;
            _browserNativeTooltipMoreHandler = null;
            _browserNativeTooltipMoreItemId = string.Empty;
        }

        internal static void ReleaseBrowserNativeTooltipMoreTarget(object handler)
        {
            ClearBrowserNativeTooltipMoreTarget(handler);
        }

        private static bool ShouldAllowBrowserTooltipMoreAction(
            string keyId, string layout, bool raw)
        {
            return _compatTooltipMore &&
                _inspectorOpen &&
                _browserNativeTooltipMoreActive &&
                !string.IsNullOrEmpty(_browserNativeTooltipMoreItemId) &&
                !raw &&
                string.Equals(keyId, BrowserTooltipMoreActionId, StringComparison.Ordinal) &&
                string.Equals(layout, BrowserTooltipMoreLayout, StringComparison.Ordinal);
        }
    }
}
