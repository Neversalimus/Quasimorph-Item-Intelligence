namespace MGSC
{
    public enum ModHookType { DungeonStarted }
    public sealed class HookAttribute : Attribute { public HookAttribute(ModHookType hook) { } }
    public interface IModContext { }
}
namespace UnityEngine
{
    public enum KeyCode { F2, F7 }
    public static class Time { public static int frameCount; public static float realtimeSinceStartup = 0; }
    public static class Input
    {
        public static KeyCode Pressed;
        public static int Queries;
        public static bool GetKeyDown(KeyCode key) { Queries++; return key == Pressed; }
    }
}
namespace ItemIntelligence
{
    public static partial class ModMain
    {
        private static bool _indexesBuilt, _inspectorOpen, EnableItemIntelligence, InspectorEnabled;
        private static bool _compatCore, _compatInputGuard, _compatSearchCatalog, _itemPointerScope;
        private static int _priceBlockFrame, _inspectorHotkeyCaptureFrame;
        private static bool _inspectorHotkeyCaptureHasTarget;
        private static object _lastItemSlot, _lastItemPointerHandler;
        private static string _lastHoveredItemId, _itemPointerScopeItemId, _priceBlockItemId;
        private static IModContext _modContext;
        private static KeyCode InspectorKeyCode;
        private static readonly HashSet<string> KnownItemIds = new HashSet<string>();
        private static readonly Dictionary<string, object> ItemRecordsById = new Dictionary<string, object>();
        private static int checks, indexBuilds, driverStarts, openCalls;
        private static string openedItem;
        private static bool dataReady;
        private static void Check(bool condition, string message) { checks++; if (!condition) throw new Exception(message); }
        private static void EnsureInspectorDriver() { driverStarts++; }
        private static void EnsureRuntimeIndexesReady()
        {
            indexBuilds++;
            if (!dataReady) return;
            KnownItemIds.Add("venus_axe_1"); ItemRecordsById["venus_axe_1"] = new object(); _indexesBuilt = true;
        }
        private static bool IsKnownItemId(string id) { return KnownItemIds.Contains(id); }
        private static string ResolveItemTooltipHandlerItemId(object handler) { return handler as string; }
        private static string ResolveItemSlotItemId(object slot) { return slot as string; }
        private static void OpenInspector(string itemId, float start, float resolveMs) { openCalls++; openedItem = itemId; }
        private static void ResetEntry()
        {
            _browserGameplayActive = _indexesBuilt = _inspectorOpen = _itemPointerScope = false;
            EnableItemIntelligence = InspectorEnabled = _compatCore = _compatInputGuard = _compatSearchCatalog = dataReady = true;
            _priceBlockFrame = _inspectorHotkeyCaptureFrame = -1000; _inspectorHotkeyCaptureHasTarget = false;
            _lastItemSlot = _lastItemPointerHandler = null; _modContext = null;
            _lastHoveredItemId = _itemPointerScopeItemId = _priceBlockItemId = "";
            KnownItemIds.Clear(); ItemRecordsById.Clear(); indexBuilds = driverStarts = openCalls = Input.Queries = 0;
            openedItem = null; Time.frameCount = 100; InspectorKeyCode = Input.Pressed = KeyCode.F2;
        }
        public static int RunBrowserEntryCases()
        {
            checks = 0;
            ResetEntry();
            bool dormant = true;
            for (int i = 0; i < 1000; i++) dormant &= !ShouldBootstrapInspectorFromHotkey();
            Check(dormant, "menu stays dormant for 1000 ticks");
            Check(Input.Queries == 0 && indexBuilds == 0, "menu idle has no input polling or data scan");
            Check(!ShouldCaptureInspectorHotkeyOpeningFrame(), "menu hotkey does not steal vanilla input");
            OpenInspectorFromHotkey(); Check(openCalls == 0 && indexBuilds == 0, "menu cannot open catalog");

            ResetEntry(); _browserGameplayActive = true;
            Check(ShouldBootstrapInspectorFromHotkey(), "first space hotkey needs no hover");
            Check(ShouldCaptureInspectorHotkeyOpeningFrame(), "catalog opening frame is modal");
            Check(ShouldCaptureInspectorHotkeyOpeningFrame() && indexBuilds == 1, "repeated vanilla input queries reuse opening frame");
            OpenInspectorFromHotkey();
            Check(openCalls == 1 && openedItem == "" && indexBuilds == 1, "fresh space opens catalog without fake selected item");
            _lastHoveredItemId = "venus_axe_1"; OpenInspectorFromHotkey();
            Check(openedItem == "venus_axe_1", "explicit item target retains item view");
            _lastHoveredItemId = "removed_mod_item"; OpenInspectorFromHotkey();
            Check(openedItem == "", "stale item target falls back to catalog");

            ResetEntry(); BrowserDungeonStarted(null); InspectorKeyCode = Input.Pressed = KeyCode.F7;
            Check(driverStarts == 1 && _browserGameplayActive, "mission resume establishes gameplay without SpaceStarted");
            Check(ShouldBootstrapInspectorFromHotkey() && ShouldCaptureInspectorHotkeyOpeningFrame(), "custom F7 works on first mission press");
            OpenInspectorFromHotkey(); Check(openCalls == 1 && openedItem == "", "direct mission resume opens catalog");
            Input.Pressed = KeyCode.F2; Check(!ShouldCaptureInspectorHotkeyOpeningFrame(), "unconfigured key is not captured");

            ResetEntry(); _lastItemSlot = "venus_axe_1";
            Check(ShouldBootstrapInspectorFromHotkey() && ShouldCaptureInspectorHotkeyOpeningFrame(), "observed-item bootstrap remains supported");
            OpenInspectorFromHotkey(); Check(openedItem == "venus_axe_1", "slot item resolves after lazy data readiness");

            ResetEntry(); _browserGameplayActive = true; dataReady = false;
            Check(!ShouldCaptureInspectorHotkeyOpeningFrame(), "unavailable item data does not capture hotkey");
            OpenInspectorFromHotkey(); Check(openCalls == 0, "unavailable data does not open empty broken catalog");
            dataReady = true; Time.frameCount++;
            Check(ShouldCaptureInspectorHotkeyOpeningFrame(), "later key request retries data readiness");

            ResetEntry(); _browserGameplayActive = true; _compatSearchCatalog = false;
            Check(!ShouldCaptureInspectorHotkeyOpeningFrame(), "unavailable catalog leaves no-target input to game");
            _lastHoveredItemId = "venus_axe_1"; Time.frameCount++;
            Check(ShouldCaptureInspectorHotkeyOpeningFrame(), "catalog compatibility does not disable item entry");
            foreach (string setting in new[] { "enabled", "inspector", "core", "guard" })
            {
                ResetEntry(); _browserGameplayActive = true;
                if (setting == "enabled") EnableItemIntelligence = false;
                if (setting == "inspector") InspectorEnabled = false;
                if (setting == "core") _compatCore = false;
                if (setting == "guard") _compatInputGuard = false;
                Check(!ShouldBootstrapInspectorFromHotkey() && !ShouldCaptureInspectorHotkeyOpeningFrame() && indexBuilds == 0,
                    "disabled setting preserves input and avoids scan: " + setting);
            }
            return checks;
        }
    }
}
