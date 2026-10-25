using System;
using MGSC;
using UnityEngine;

namespace ItemIntelligence
{
    public static partial class ModMain
    {
        private static bool _browserGameplayActive;

        [Hook(ModHookType.DungeonStarted)]
        public static void BrowserDungeonStarted(IModContext context)
        {
            if (context != null) _modContext = context;
            _browserGameplayActive = true;
            EnsureInspectorDriver();
        }

        private static bool ShouldBootstrapInspectorFromHotkey()
        {
            // Only an explicit key press can bootstrap a resumed session. No data
            // lookup, scene search or reflection runs while idling in the main menu.
            return !_indexesBuilt && !_inspectorOpen &&
                EnableItemIntelligence && InspectorEnabled &&
                _compatCore && _compatInputGuard &&
                (_browserGameplayActive || HasObservedInspectorItemUi()) &&
                Input.GetKeyDown(InspectorKeyCode);
        }

        private static bool CanOpenInspectorFromRequest(string itemId)
        {
            return !string.IsNullOrEmpty(itemId) ||
                (_browserGameplayActive && _compatSearchCatalog && KnownItemIds.Count > 0);
        }

        private static void OpenInspectorFromHotkey()
        {
            float started = Time.realtimeSinceStartup;
            string itemId = ResolveInspectorTargetOnDemand();
            float resolveMs = (Time.realtimeSinceStartup - started) * 1000f;
            if (CanOpenInspectorFromRequest(itemId))
                OpenInspector(itemId, started, resolveMs);
        }
    }
}
