namespace ItemIntelligence
{
    public static partial class ModMain
    {
        private const string BrowserTooltipMoreActionId = "UI_TooltipMore";
        private const string BrowserTooltipMoreLayout = "UI";
        private static bool _compatTooltipMore;
        private static bool _inspectorOpen;
        private static bool _browserNativeTooltipMoreActive;
        private static string _browserNativeTooltipMoreItemId = string.Empty;

        private static void RunTooltipInteractionCases()
        {
            _compatTooltipMore = true;
            _inspectorOpen = true;
            _browserNativeTooltipMoreActive = true;
            _browserNativeTooltipMoreItemId = "test_item";

            Check(ShouldAllowBrowserTooltipMoreAction("UI_TooltipMore", "UI", false),
                "native tooltip-more action is allowed for the active QII item tooltip");
            Check(!ShouldAllowBrowserTooltipMoreAction("UI_TooltipMore", "UI", true),
                "raw tooltip-more query remains blocked");
            Check(!ShouldAllowBrowserTooltipMoreAction("UI_TooltipMore", "Gameplay", false),
                "non-UI layout remains blocked");
            Check(!ShouldAllowBrowserTooltipMoreAction("Back", "UI", false),
                "other UI actions remain blocked");

            _browserNativeTooltipMoreItemId = string.Empty;
            Check(!ShouldAllowBrowserTooltipMoreAction("UI_TooltipMore", "UI", false),
                "missing QII item target fails closed");
            _browserNativeTooltipMoreItemId = "test_item";
            _browserNativeTooltipMoreActive = false;
            Check(!ShouldAllowBrowserTooltipMoreAction("UI_TooltipMore", "UI", false),
                "inactive native item tooltip fails closed");
            _browserNativeTooltipMoreActive = true;
            _inspectorOpen = false;
            Check(!ShouldAllowBrowserTooltipMoreAction("UI_TooltipMore", "UI", false),
                "closed QII browser fails closed");
            _inspectorOpen = true;
            _compatTooltipMore = false;
            Check(!ShouldAllowBrowserTooltipMoreAction("UI_TooltipMore", "UI", false),
                "compatibility failure disables only tooltip-more pass-through");

            _compatTooltipMore = true;
            _inspectorOpen = false;
            _browserNativeTooltipMoreActive = false;
            _browserNativeTooltipMoreItemId = string.Empty;
        }
    }
}
