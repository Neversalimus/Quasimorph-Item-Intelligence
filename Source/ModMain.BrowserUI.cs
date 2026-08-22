using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Runtime.InteropServices;
using System.Threading;
using MGSC;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ItemIntelligence
{
    public static partial class ModMain
    {
        // Test13 generic controller owner: hotkey/modal input, browser lifecycle,
        // search, native tooltip bindings and generic row actions. Catalog behavior
        // and both presentation layers have dedicated partial owners.

        private static void SetShowInspectorHint(bool value)
        {
            ShowInspectorHint = value;

            // MCM can be changed while the mouse is still over an item. In that case
            // no pointer-exit event is generated, so an already-visible HUD hint would
            // otherwise remain on screen until the next hover transition.
            if (!ShowInspectorHint)
                HideHoverHint();
        }

        private static bool SetInspectorKey(string value, string source)
        {
            string normalized = NormalizeInspectorKeyConfigValue(value);
            KeyCode parsed;
            if (!TryParseInspectorKey(normalized, out parsed))
            {
                Debug.LogWarning("[ItemIntelligence] Invalid InspectorKey='" + (value ?? string.Empty) +
                    "' from " + (source ?? "settings") + "; keeping " + InspectorKeyName + ".");
                return false;
            }

            InspectorKeyCode = parsed;
            InspectorKeyName = GetInspectorKeyConfigName(parsed);

            if (_hoverHintText != null)
                _hoverHintText.text = HotkeyUi("ui.f2_item_analysis");
            if (_browserCloseText != null)
                _browserCloseText.text = GetBrowserCloseButtonLabel();

            Debug.Log("[ItemIntelligence] Inspector hotkey: " + GetInspectorKeyDisplayName() +
                " (" + InspectorKeyName + ", source=" + (source ?? "settings") + ").");
            return true;
        }

        private static string NormalizeInspectorKeyConfigValue(string value)
        {
            string text = (value ?? string.Empty).Trim();
            if (text.Length == 1 && text[0] >= '0' && text[0] <= '9')
                return "Alpha" + text;
            if (string.Equals(text, "PgUp", StringComparison.OrdinalIgnoreCase)) return "PageUp";
            if (string.Equals(text, "PgDn", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(text, "PgDown", StringComparison.OrdinalIgnoreCase)) return "PageDown";
            if (string.Equals(text, "Del", StringComparison.OrdinalIgnoreCase)) return "Delete";
            if (string.Equals(text, "Ins", StringComparison.OrdinalIgnoreCase)) return "Insert";
            if (string.Equals(text, "Spacebar", StringComparison.OrdinalIgnoreCase)) return "Space";
            return text;
        }

        private static bool TryParseInspectorKey(string value, out KeyCode key)
        {
            key = KeyCode.F2;
            if (string.IsNullOrEmpty(value)) return false;
            try
            {
                object parsed = Enum.Parse(typeof(KeyCode), value, true);
                if (!(parsed is KeyCode)) return false;
                key = (KeyCode)parsed;
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static string GetInspectorKeyConfigName(KeyCode key)
        {
            return key.ToString();
        }

        private static string GetInspectorKeyDisplayName()
        {
            string name = InspectorKeyCode.ToString();
            if (name.StartsWith("Alpha", StringComparison.Ordinal) && name.Length == 6)
                return name.Substring(5);
            if (name.StartsWith("Keypad", StringComparison.Ordinal) && name.Length > 6)
                return "Num " + name.Substring(6);
            return name;
        }

        private static bool IsInspectorTextEntryKey(KeyCode key)
        {
            if (key >= KeyCode.A && key <= KeyCode.Z) return true;
            if (key >= KeyCode.Alpha0 && key <= KeyCode.Alpha9) return true;
            if (key >= KeyCode.Keypad0 && key <= KeyCode.Keypad9) return true;
            return key == KeyCode.Space || key == KeyCode.Minus || key == KeyCode.Equals ||
                key == KeyCode.LeftBracket || key == KeyCode.RightBracket ||
                key == KeyCode.Backslash || key == KeyCode.Semicolon ||
                key == KeyCode.Quote || key == KeyCode.Comma || key == KeyCode.Period ||
                key == KeyCode.Slash || key == KeyCode.BackQuote;
        }


        private static bool IsInspectorHotkeyDownForBrowserToggle()
        {
            if (_diagnosticsHotkeyConsumedFrame == Time.frameCount) return false;
            if (!Input.GetKeyDown(InspectorKeyCode)) return false;

            // A letter/digit/punctuation binding must remain usable as search text. ESC
            // still closes the browser while the search field is focused. Function keys
            // and other non-text bindings continue to toggle normally.
            if (_inspectorOpen && _browserSearchInput != null && _browserSearchInput.isFocused &&
                IsInspectorTextEntryKey(InspectorKeyCode))
                return false;

            return true;
        }

        private static bool ShouldCaptureInspectorHotkeyOpeningFrame()
        {
            if (!EnableItemIntelligence || !InspectorEnabled || !_compatCore || !_compatInputGuard ||
                _inspectorOpen || !Input.GetKeyDown(InspectorKeyCode))
                return false;

            int frame = Time.frameCount;
            if (_inspectorHotkeyCaptureFrame != frame)
            {
                _inspectorHotkeyCaptureFrame = frame;
                _inspectorHotkeyCaptureHasTarget = !string.IsNullOrEmpty(ResolveInspectorTargetOnDemand());
            }

            return _inspectorHotkeyCaptureHasTarget;
        }

        private static List<string> GetInspectorHotkeyOptions()
        {
            List<string> keys = new List<string>();
            for (int i = 1; i <= 12; i++) keys.Add("F" + i.ToString(CultureInfo.InvariantCulture));
            for (char c = 'A'; c <= 'Z'; c++) keys.Add(c.ToString());
            for (int i = 0; i <= 9; i++) keys.Add(i.ToString(CultureInfo.InvariantCulture));
            keys.Add("Insert");
            keys.Add("Delete");
            keys.Add("Home");
            keys.Add("End");
            keys.Add("PageUp");
            keys.Add("PageDown");
            keys.Add("Space");
            keys.Add("BackQuote");
            keys.Add("Minus");
            keys.Add("Equals");
            keys.Add("LeftBracket");
            keys.Add("RightBracket");
            keys.Add("Backslash");
            keys.Add("Semicolon");
            keys.Add("Quote");
            keys.Add("Comma");
            keys.Add("Period");
            keys.Add("Slash");

            // Preserve manually configured but valid Unity KeyCode values. This keeps
            // config.ini power-users compatible even when their key is not in the
            // curated common-key list above.
            string current = GetInspectorKeyDisplayName();
            if (!keys.Contains(current))
                keys.Add(current);

            return keys;
        }

        private static int PatchAllDeclaredMethodsWhileInspectorOpen(Harmony harmony, string typeName)
        {
            Type type = AccessTools.TypeByName(typeName);
            if (type == null) return 0;

            MethodInfo prefix = typeof(ModMain).GetMethod("BlockVanillaInventoryPointerPrefix", StaticFlags);
            if (prefix == null) return 0;

            int patched = 0;
            int failed = 0;
            Exception firstFailure = null;
            try
            {
                MethodInfo[] methods = type.GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                for (int i = 0; i < methods.Length; i++)
                {
                    MethodInfo method = methods[i];
                    if (method == null || method.IsAbstract || method.ContainsGenericParameters || method.IsSpecialName) continue;
                    try
                    {
                        harmony.Patch(method, prefix: new HarmonyMethod(prefix));
                        patched++;
                    }
                    catch (Exception ex)
                    {
                        failed++;
                        if (firstFailure == null) firstFailure = ex;
                    }
                }
            }
            catch (Exception ex)
            {
                failed++;
                if (firstFailure == null) firstFailure = ex;
            }

            if (failed > 0)
                LogRuntimeBoundaryWarningOnce(
                    "harmony.declared." + typeName,
                    "Modal guard for " + typeName + " skipped " + failed + " method(s).",
                    firstFailure);

            Debug.Log("[ItemIntelligence] Modal guard " + typeName + ": patched " + patched + " declared methods.");
            return patched;
        }

        private static BrowserItemTooltipBinding ResolveBrowserItemTooltipBinding(object handler)
        {
            Component component = handler as Component;
            if (component == null || component.gameObject == null) return null;
            try { return component.GetComponent<BrowserItemTooltipBinding>(); }
            catch { return null; }
        }

        private static bool IsBrowserOwnedItemTooltipHandler(object handler)
        {
            if (handler == null) return false;
            if (_browserPreviewTooltipHandler != null &&
                object.ReferenceEquals(handler, _browserPreviewTooltipHandler))
                return true;

            BrowserItemTooltipBinding binding = ResolveBrowserItemTooltipBinding(handler);
            return binding != null;
        }

        private static void PrepareBrowserBoundTooltipHandler(
            ItemTooltipHandler handler,
            BrowserItemTooltipBinding binding)
        {
            if (handler == null || binding == null ||
                string.IsNullOrEmpty(binding.ItemId))
                return;

            string itemId = binding.ItemId;
            if (string.Equals(binding.PreparedItemId, itemId, StringComparison.OrdinalIgnoreCase))
                return;

            // Pooled browser icons are reused between scroll positions/tabs. Clear a tooltip from
            // the previous binding before reinitializing this exact native handler.
            if (!string.IsNullOrEmpty(binding.PreparedItemId))
            {
                try { handler.OnPointerExit(null); }
                catch { }
                RestoreBrowserTooltipLayer();
            }

            try
            {
                BasePickupItem previewItem = CreateBrowserTooltipPreviewItem(itemId);
                BasePickupItemRecord record = ResolveBrowserPreviewRecord(itemId, previewItem);
                if (previewItem != null && record != null)
                {
                    // ItemTooltipHandler.OnPointerEnter uses BuildItemTooltip(_item) only
                    // when Initialize(BasePickupItem, record) populated the live-item field.
                    // Initialize(itemId) populates only _itemRecord and produces the short
                    // PropertiesTooltip seen in v1.7.3. A detached factory item follows the
                    // same full vanilla path as a real inventory slot without inserting it
                    // into any storage or changing game state.
                    handler.Initialize(previewItem, record);
                    binding.PreviewItem = previewItem;
                    binding.PreparedItemId = itemId;
                    return;
                }

                if (record != null)
                {
                    handler.Initialize(itemId);
                    binding.PreparedItemId = itemId;
                    return;
                }
            }
            catch { }

            string baseId = ResolveStaticRelationItemId(itemId);
            if (!string.IsNullOrEmpty(baseId) &&
                !string.Equals(baseId, itemId, StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    handler.Initialize(baseId);
                    binding.PreparedItemId = itemId;
                    return;
                }
                catch { }
            }

            try
            {
                handler.InitializeSimpleTooltip(LocalizeItem(itemId));
                binding.PreparedItemId = itemId;
            }
            catch { }
        }

        private static void AttachBrowserItemTooltipTarget(Image image)
        {
            if (image == null || image.gameObject == null) return;
            image.raycastTarget = false;
            if (!_compatTooltip) return;

            GameObject go = image.gameObject;
            if (go.GetComponent<ItemTooltipHandler>() == null)
                go.AddComponent<ItemTooltipHandler>();
            if (go.GetComponent<BrowserItemTooltipBinding>() == null)
                go.AddComponent<BrowserItemTooltipBinding>();
            if (go.GetComponent<BrowserModalTooltipLayerGuard>() == null)
                go.AddComponent<BrowserModalTooltipLayerGuard>();
        }

        private static void SetBrowserItemTooltipTarget(
            Image image, string itemId, bool enabled, bool navigationTarget = false)
        {
            if (image == null || image.gameObject == null) return;

            BrowserItemTooltipBinding binding = null;
            try { binding = image.gameObject.GetComponent<BrowserItemTooltipBinding>(); }
            catch { }

            bool active = enabled && !string.IsNullOrEmpty(itemId) && binding != null;
            bool navigationActive = navigationTarget && enabled && !string.IsNullOrEmpty(itemId);
            image.raycastTarget = active || navigationActive;
            if (binding != null)
            {
                string nextId = active ? itemId : string.Empty;
                if (!string.IsNullOrEmpty(binding.PreparedItemId) &&
                    !string.Equals(binding.PreparedItemId, nextId, StringComparison.OrdinalIgnoreCase))
                {
                    try
                    {
                        ItemTooltipHandler handler = image.gameObject.GetComponent<ItemTooltipHandler>();
                        if (handler != null) handler.OnPointerExit(null);
                    }
                    catch { }
                    RestoreBrowserTooltipLayer();
                    binding.PreparedItemId = string.Empty;
                    binding.PreviewItem = null;
                }

                binding.ItemId = nextId;
                if (!active)
                {
                    binding.PreparedItemId = string.Empty;
                    binding.PreviewItem = null;
                }
            }
        }

        private static void EnsureInspectorDriver()
        {
            if (_inspectorDriverObject != null) return;
            try
            {
                GameObject existing = GameObject.Find("QII_ItemIntelligence_Driver");
                if (existing != null)
                {
                    _inspectorDriverObject = existing;
                    if (existing.GetComponent<ItemIntelligenceInspectorDriver>() == null)
                        existing.AddComponent<ItemIntelligenceInspectorDriver>();
                    return;
                }

                GameObject go = new GameObject("QII_ItemIntelligence_Driver");
                UnityEngine.Object.DontDestroyOnLoad(go);
                go.AddComponent<ItemIntelligenceInspectorDriver>();
                _inspectorDriverObject = go;
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[ItemIntelligence] Inspector driver could not start: " + ex.Message);
            }
        }

        private static bool HasObservedInspectorItemUi()
        {
            return _lastItemSlot != null ||
                _lastItemPointerHandler != null ||
                _itemPointerScope ||
                _priceBlockFrame >= Time.frameCount - 2;
        }

        private static string ResolveInspectorTargetOnDemand()
        {
            // v1.7.31: Loading a save that is already inside a mission does not pass
            // through the normal SpaceStarted lifecycle. MainMenuStarted intentionally
            // clears the reverse indexes, so the old resolver could reject every mission
            // item as "unknown" before OpenInspector had a chance to run its existing
            // runtime readiness check. Lazy-bootstrap the data here, only on an explicit
            // browser hotkey target resolution. This keeps main-menu/bootstrap loading
            // unchanged and makes direct mission resume self-healing.
            bool hasObservedItemUi = HasObservedInspectorItemUi();

            if (hasObservedItemUi && (KnownItemIds.Count == 0 || ItemRecordsById.Count == 0))
            {
                int before = KnownItemIds.Count;
                EnsureRuntimeIndexesReady();
                if (before == 0 && KnownItemIds.Count > 0)
                    Debug.Log("[ItemIntelligence] Direct mission resume: runtime indexes initialized on browser request. KnownItems=" +
                        KnownItemIds.Count.ToString(CultureInfo.InvariantCulture) + ".");
            }

            string itemId = _lastHoveredItemId;

            if (!string.IsNullOrEmpty(itemId) && IsKnownItemId(itemId))
                return itemId;

            if (!string.IsNullOrEmpty(_itemPointerScopeItemId) && IsKnownItemId(_itemPointerScopeItemId))
                return _itemPointerScopeItemId;

            if (_lastItemPointerHandler != null)
            {
                itemId = ResolveItemTooltipHandlerItemId(_lastItemPointerHandler);
                if (!string.IsNullOrEmpty(itemId) && IsKnownItemId(itemId))
                {
                    _lastHoveredItemId = itemId;
                    return itemId;
                }
            }

            if (_lastItemSlot != null)
            {
                itemId = ResolveItemSlotItemId(_lastItemSlot);
                if (!string.IsNullOrEmpty(itemId) && IsKnownItemId(itemId))
                {
                    _lastHoveredItemId = itemId;
                    return itemId;
                }
            }

            if (_priceBlockFrame >= Time.frameCount - 2 &&
                !string.IsNullOrEmpty(_priceBlockItemId) &&
                IsKnownItemId(_priceBlockItemId))
                return _priceBlockItemId;

            return string.Empty;
        }

        internal static void InspectorTick()
        {
            TickBrowserRowsRefresh(); // QII_MAGNUM_REFRESH_TICK
            if (_applicationQuitting) return;
            EnforceInspectorModalInvariantSafe();

            // v1.7.38.1: true main-menu idle fast path. MainMenuStarted clears the
            // gameplay indexes and restores any QII-owned Starmap visual suspension,
            // so there is no feature warmup, resolver, diagnostics hotkey or tooltip
            // maintenance to tick until a gameplay session exists again. This keeps
            // the persistent driver effectively dormant on older main-thread-bound CPUs.
            // v1.7.38.3-test1 BuildFix1: direct mission resume can enter Dungeon
            // without ever receiving SpaceStarted, leaving _indexesBuilt=false. The
            // 1.7.38.1 main-menu idle fast path therefore used to swallow the first F2
            // unless a vanilla InputController query happened to run on that same frame.
            // Probe only while a real item UI is observed and only for the configured
            // opening hotkey. Main Menu remains fully dormant because its item UI state
            // is cleared at the session boundary. The normal resolver below performs the
            // existing lazy bootstrap and opens the browser on this very same key press.
            bool directMissionHotkeyBootstrap =
                !_indexesBuilt && !_inspectorOpen &&
                EnableItemIntelligence && InspectorEnabled &&
                _compatCore && _compatInputGuard &&
                HasObservedInspectorItemUi() &&
                Input.GetKeyDown(InspectorKeyCode);

            if (!_indexesBuilt && !_inspectorOpen &&
                string.IsNullOrEmpty(_pendingStarmapTargetId) &&
                StarmapSourceViewVisualStates.Count == 0 &&
                !directMissionHotkeyBootstrap)
                return;

            HandleDiagnosticsHotkey();

            TickFeatureFrameWork();

            // One bool check only. This closes the stale-Canvas edge case even if the
            // setting changes while no pointer enter/exit event is generated.
            if (!ShowInspectorHint && _hoverHintCanvas != null && _hoverHintCanvas.activeSelf)
                HideHoverHint();

            // exp2 source-view presentation restoration must run even if the browser
            // itself is disabled while a test Starmap transition is active.
            TickStarmapSourceViewVisualSuspension();

            if (!EnableItemIntelligence || !InspectorEnabled)
            {
                if (_inspectorOpen) CloseInspector();
                return;
            }

            try
            {
                TickPendingStarmapNavigation();

                // MainMenuStarted clears runtime indexes and
                // service references. Do not retry the reflective state resolver forever
                // while idling in main/options menus; resume it only for a gameplay
                // session or an explicitly opened browser.
                if (_indexesBuilt || _inspectorOpen)
                    TickStateServiceResolver();

                if (IsInspectorHotkeyDownForBrowserToggle())
                {
                    if (_inspectorOpen)
                    {
                        CloseInspector();
                    }
                    else
                    {
                        float openRequestStarted = Time.realtimeSinceStartup;
                        string targetItemId = ResolveInspectorTargetOnDemand();
                        float targetResolveMs = (Time.realtimeSinceStartup - openRequestStarted) * 1000f;

                        if (!string.IsNullOrEmpty(targetItemId))
                        {
                            Debug.Log("[ItemIntelligence] " + GetInspectorKeyDisplayName() + " target resolved: " + targetItemId + ".");
                            OpenInspector(targetItemId, openRequestStarted, targetResolveMs);
                        }
                        else
                        {
                            Debug.LogWarning("[ItemIntelligence] " + GetInspectorKeyDisplayName() + " pressed but no item target was resolved.");
                        }
                    }

                    return;
                }

                if (!_inspectorOpen) return;

                TickBrowserSearchIndexWarmup();

                if (Input.GetKeyDown(KeyCode.Escape))
                {
                    if (_browserCatalogOpen)
                    {
                        CloseBrowserCatalog();
                        return;
                    }
                    CloseInspector();
                    return;
                }

                if (_browserCatalogOpen)
                {
                    if (Input.GetKeyDown(KeyCode.PageUp)) ScrollBrowserCatalogRows(-(BrowserCatalogVisibleRows - 1));
                    else if (Input.GetKeyDown(KeyCode.PageDown)) ScrollBrowserCatalogRows(BrowserCatalogVisibleRows - 1);
                    else
                    {
                        float catalogWheel = Input.mouseScrollDelta.y;
                        if (catalogWheel > 0.1f) ScrollBrowserCatalogRows(-3);
                        else if (catalogWheel < -0.1f) ScrollBrowserCatalogRows(3);
                    }

                    TickMarketScanCompatibilitySafe();
                    return;
                }

                // Text input owns letters, digits, arrows and wheel while focused.
                // The configured hotkey/Esc above still close the browser normally.
                if (_browserSearchInput != null && _browserSearchInput.isFocused)
                {
                    if (!_browserSearchCaptureLogged)
                    {
                        _browserSearchCaptureLogged = true;
                        Debug.Log("[ItemIntelligence] Search field focused; InventorySearch-style modal input guard active.");
                        // onValueChanged owns normal query refreshes. Focus restoration only
                        // needs one cache-aware refresh to re-show the existing result window.
                        if (!string.IsNullOrEmpty(_browserSearchInput.text))
                            RefreshBrowserSearchSuggestions(_browserSearchInput.text);
                    }

                    if (UnityEngine.EventSystems.EventSystem.current != null)
                        UnityEngine.EventSystems.EventSystem.current.sendNavigationEvents = false;


                    // Search keeps the fixed eight-row pool but scrolls through matches
                    // by row, so the dropdown behaves like a normal virtualized list.
                    if (_browserSearchDropdown != null && _browserSearchDropdown.activeSelf)
                    {
                        float searchWheel = Input.mouseScrollDelta.y;
                        if (searchWheel > 0.1f) ScrollBrowserSearchRows(-3);
                        else if (searchWheel < -0.1f) ScrollBrowserSearchRows(3);
                    }

                    TickMarketScanCompatibilitySafe();
                    return;
                }
                else
                {
                    if (_browserSearchCaptureLogged)
                        _browserSearchCaptureLogged = false;


                    if (UnityEngine.EventSystems.EventSystem.current != null)
                        UnityEngine.EventSystems.EventSystem.current.sendNavigationEvents = true;
                }

                if (Input.GetKeyDown(KeyCode.Alpha1)) SetBrowserTab((int)BrowserTabId.Overview);
                else if (Input.GetKeyDown(KeyCode.Alpha2)) SetBrowserTab((int)BrowserTabId.Magnum);
                else if (Input.GetKeyDown(KeyCode.Alpha3)) SetBrowserTab((int)BrowserTabId.Recipes);
                else if (Input.GetKeyDown(KeyCode.Alpha4)) SetBrowserTab((int)BrowserTabId.Trade);
                else if (Input.GetKeyDown(KeyCode.Alpha5)) SetBrowserTab((int)BrowserTabId.Ammo);
                else if (Input.GetKeyDown(KeyCode.Alpha6)) SetBrowserTab((int)BrowserTabId.Factions);
                else if (Input.GetKeyDown(KeyCode.Alpha7)) SetBrowserTab((int)BrowserTabId.Loot);
                else if (Input.GetKeyDown(KeyCode.Q) || Input.GetKeyDown(KeyCode.LeftArrow)) CycleBrowserTab(-1);
                else if (Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.RightArrow)) CycleBrowserTab(1);
                else if (Input.GetKeyDown(KeyCode.PageUp)) ScrollBrowserRows(-(BrowserVisibleRows - 1));
                else if (Input.GetKeyDown(KeyCode.PageDown)) ScrollBrowserRows(BrowserVisibleRows - 1);
                else
                {
                    float wheel = Input.mouseScrollDelta.y;
                    if (wheel > 0.1f) ScrollBrowserRows(-3);
                    else if (wheel < -0.1f) ScrollBrowserRows(3);
                }

                TickMarketScanCompatibilitySafe();
            }
            catch (Exception ex)
            {
                // A browser/input exception must never leave the global modal flag latched.
                // Fail closed by closing only QII-owned UI and restoring vanilla input.
                LogRuntimeBoundaryWarningOnce(
                    "browser.tick",
                    "Browser tick recovered from an exception; closing Item Intelligence to preserve vanilla input.",
                    ex);
                if (_inspectorOpen) CloseInspector();
            }
        }

        private static void EnforceInspectorModalInvariantSafe()
        {
            if (!_inspectorOpen) return;

            bool stale = false;
            try
            {
                stale =
                    _inspectorRoot == null ||
                    _inspectorCanvas == null ||
                    _inspectorCanvasObject == null ||
                    _inspectorInputBlocker == null ||
                    !_inspectorRoot.activeInHierarchy;
            }
            catch
            {
                stale = true;
            }

            if (!stale) return;

            LogRuntimeBoundaryWarningOnce(
                "browser.modal.stale",
                "Recovered a stale Item Intelligence modal after a scene/UI boundary; vanilla input is being restored.",
                null);
            CloseInspector();
        }

        private static void OpenInspector(string itemId, float openRequestStarted, float targetResolveMs)
        {
            if (!EnableItemIntelligence ||
                !InspectorEnabled ||
                string.IsNullOrEmpty(itemId))
                return;

            if (!_compatCore)
            {
                Debug.LogWarning(
                    "[ItemIntelligence] Compatibility Shield blocked Item Intelligence browser: " +
                    GetCompatibilityReason("Core"));
                return;
            }

            if (!_compatInputGuard)
            {
                Debug.LogWarning(
                    "[ItemIntelligence] Compatibility Shield blocked Item Intelligence browser because modal input safety is unavailable: " +
                    GetCompatibilityReason("InputGuard"));
                return;
            }

            try
            {
                bool firstPanelBuild = _inspectorRoot == null;
                float perfTotalStart = openRequestStarted;
                float perfStageStart = Time.realtimeSinceStartup;

                EnsureRuntimeIndexesReady();
                float perfIndexesMs = (Time.realtimeSinceStartup - perfStageStart) * 1000f;
                EnsureTradeStateDependencies();
                EnsureCatalogPreferencesLoaded();

                perfStageStart = Time.realtimeSinceStartup;
                EnsureInspectorPanel();
                float perfPanelMs = (Time.realtimeSinceStartup - perfStageStart) * 1000f;
                if (_inspectorRoot == null) return;

                _browserPreviewLiveItem = ResolveBrowserPreviewLiveItem(itemId);
                HideSourceVanillaTooltip();
                _inspectorOpen = true;
                HideHoverHint();
                _inspectorItemId = itemId;
                BrowserTabId adaptiveEntryTab = ResolveAdaptiveEntryTab(itemId);
                BrowserNavigation.Tab = (int)adaptiveEntryTab;
                Debug.Log("[ItemIntelligence][AdaptiveEntry] item=" + itemId + ", tab=" + adaptiveEntryTab.ToString() + ".");
                BrowserNavigation.ScrollOffset = 0;
                BrowserNavigation.History.Clear();
                Array.Clear(BrowserNavigation.ScrollOffsets, 0, BrowserNavigation.ScrollOffsets.Length);
                _secretDataSelectedFactionId = string.Empty;
                _browserCatalogOpen = false;
                _browserCatalogScrollOffset = 0;
                RecordBrowserItemVisit(itemId);

                // Search/catalog indexing is deliberately demand-driven in the release-polish branch.
                // Opening an item no longer enumerates/sorts all known item ids.
                ClearBrowserSearchField();

                SuppressVanillaGraphicRaycasters();

                if (_inspectorInputBlocker != null)
                {
                    _inspectorInputBlocker.SetActive(true);
                    _inspectorInputBlocker.transform.SetAsLastSibling();
                }
                _inspectorRoot.SetActive(true);
                _inspectorRoot.transform.SetAsLastSibling();
                PositionInspectorPanel();
                RefreshInspectorAnchorFromTooltip();
                perfStageStart = Time.realtimeSinceStartup;
                RenderBrowser(itemId);
                float perfRenderMs = (Time.realtimeSinceStartup - perfStageStart) * 1000f;
                float perfTotalMs = (Time.realtimeSinceStartup - perfTotalStart) * 1000f;
                ReportBrowserPerformanceBudget(itemId, firstPanelBuild, perfTotalMs, perfRenderMs);
                if (firstPanelBuild)
                {
                    float knownStagesMs = targetResolveMs + perfIndexesMs + perfPanelMs + perfRenderMs;
                    float perfMiscMs = Mathf.Max(0f, perfTotalMs - knownStagesMs);
                    float coreBuildMs = _lastCoreIndexBuildFrame == Time.frameCount ? _lastCoreIndexBuildMs : 0f;
                    Debug.Log("[ItemIntelligence][FirstOpenPerf] targetResolve=" +
                        targetResolveMs.ToString("0.0", CultureInfo.InvariantCulture) +
                        "ms, coreBuild=" + coreBuildMs.ToString("0.0", CultureInfo.InvariantCulture) +
                        "ms, runtimeReady=" + perfIndexesMs.ToString("0.0", CultureInfo.InvariantCulture) +
                        "ms, panel=" + perfPanelMs.ToString("0.0", CultureInfo.InvariantCulture) +
                        "ms, render=" + perfRenderMs.ToString("0.0", CultureInfo.InvariantCulture) +
                        "ms, misc=" + perfMiscMs.ToString("0.0", CultureInfo.InvariantCulture) +
                        "ms, total=" + perfTotalMs.ToString("0.0", CultureInfo.InvariantCulture) +
                        "ms, searchIndex=" + (_browserSearchWarmupActive ? "warming" : "deferred") +
                        ", searchDropdown=" + (_browserSearchDropdown == null ? "deferred" : "ready") +
                        ", catalog=" + (_browserCatalogPanel == null ? "deferred" : "ready") +
                        ", factionColumns=" + (BrowserRowFactionReward[0] == null ? "deferred" : "ready") +
                        ", recipeContext=" + (BrowserRowChipIcons[0] == null ? "deferred" : "ready") +
                        ", lootProgress=" + (_lootProgressRoot == null ? "deferred" : "ready") + ".");
                }

                if (BarterItemIds.Contains(itemId) && !HasInspectorData(itemId))
                {
                    Debug.LogWarning("[ItemIntelligence] Trade item opened with no indexed links: " + itemId +
                        ". Magnum=" + GetListCount(MagnumUses, itemId) +
                        ", UsedIn=" + GetListCount(UsedInRecipes, itemId) +
                        ", CraftedFrom=" + GetListCount(CraftedFromRecipes, itemId) +
                        ", BarterSources=" + GetListCount(BarterSources, itemId) +
                        ", BarterConsumers=" + GetListCount(BarterConsumers, itemId) + ".");
                }

                Debug.Log("[ItemIntelligence] Browser opened for " + itemId + ".");
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[ItemIntelligence] Browser open failed: " + ex.Message);
                CloseInspector();
            }
        }

        private static void CloseInspector()
        {
            _inspectorOpen = false;
            _inspectorItemId = string.Empty;
            _browserPreviewLiveItem = null;
            BrowserNavigation.ScrollOffset = 0;
            BrowserNavigation.History.Clear();
            ResetLootAccordionState();
            _secretDataSelectedFactionId = string.Empty;
            _marketScanActive = false;
            HideBrowserSearchDropdown();
            CloseBrowserCatalog();
            HideBrowserPreviewTooltip();
            HideBrowserWeaponModeTooltip();
            _browserPreviewTooltipItemId = string.Empty;
            ClearBrowserTooltipPreviewBindings();

            try
            {
                if (UnityEngine.EventSystems.EventSystem.current != null)
                    UnityEngine.EventSystems.EventSystem.current.sendNavigationEvents = true;

                _browserSearchCaptureLogged = false;

                if (_browserSearchInput != null)
                    _browserSearchInput.DeactivateInputField();

                if (_inspectorRoot != null)
                    _inspectorRoot.SetActive(false);
                if (_inspectorInputBlocker != null)
                    _inspectorInputBlocker.SetActive(false);
            }
            catch (Exception ex)
            {
                LogRuntimeBoundaryWarningOnce(
                    "browser.close.cleanup",
                    "Browser close cleanup hit a destroyed UI object; continuing with vanilla raycaster restoration.",
                    ex);
            }

            RestoreVanillaGraphicRaycasters();
        }

        private static void ClearBrowserTooltipPreviewBindings()
        {
            // Browser rows are pooled. Clear only QII-owned detached preview item
            // references; the native ItemTooltipHandler components remain intact and
            // are reinitialized lazily when a row becomes visible again.
            try
            {
                for (int i = 0; i < BrowserRowIcons.Length; i++)
                {
                    SetBrowserItemTooltipTarget(BrowserRowIcons[i], string.Empty, false);
                    SetBrowserItemTooltipTarget(BrowserRowChipIcons[i], string.Empty, false);
                }
                for (int i = 0; i < BrowserSearchRowIcons.Length; i++)
                    SetBrowserItemTooltipTarget(BrowserSearchRowIcons[i], string.Empty, false);
                for (int i = 0; i < BrowserCatalogRowIcons.Length; i++)
                    SetBrowserItemTooltipTarget(BrowserCatalogRowIcons[i], string.Empty, false);
            }
            catch { }
        }

        private static void SetBrowserTab(int tab)
        {
            if (!_inspectorOpen) return;
            if (tab < 0) tab = 0;
            if (tab >= BrowserTabCount) tab = BrowserTabCount - 1;
            if (BrowserNavigation.Tab == tab)
            {
                if (tab == (int)BrowserTabId.Trade && (ShowSources || ShowTradeInformation))
                {
                    StartMarketScan(_inspectorItemId, true);
                    RenderBrowser(_inspectorItemId);
                }
                return;
            }

            if (BrowserNavigation.Tab >= 0 && BrowserNavigation.Tab < BrowserNavigation.ScrollOffsets.Length)
                BrowserNavigation.ScrollOffsets[BrowserNavigation.Tab] = Math.Max(0, BrowserNavigation.ScrollOffset);

            BrowserNavigation.Tab = tab;
            BrowserNavigation.ScrollOffset = BrowserNavigation.ScrollOffsets[BrowserNavigation.Tab];
            if (BrowserNavigation.Tab != (int)BrowserTabId.Factions) _secretDataSelectedFactionId = string.Empty;
            CloseBrowserCatalog();
            if (BrowserNavigation.Tab == (int)BrowserTabId.Trade && (ShowSources || ShowTradeInformation))
                StartMarketScan(_inspectorItemId, true);
            RenderBrowser(_inspectorItemId);
        }

        private static void CycleBrowserTab(int delta)
        {
            if (!_inspectorOpen) return;
            int next = (BrowserNavigation.Tab + delta) % BrowserTabCount;
            if (next < 0) next += BrowserTabCount;
            SetBrowserTab(next);
        }

        private static void ScrollBrowserRows(int delta)
        {
            if (!_inspectorOpen || delta == 0) return;
            int maxOffset = Math.Max(0, BrowserLines.Count - BrowserVisibleRows);
            int next = Mathf.Clamp(BrowserNavigation.ScrollOffset + delta, 0, maxOffset);
            if (next == BrowserNavigation.ScrollOffset) return;
            BrowserNavigation.ScrollOffset = next;
            if (BrowserNavigation.Tab >= 0 && BrowserNavigation.Tab < BrowserNavigation.ScrollOffsets.Length)
                BrowserNavigation.ScrollOffsets[BrowserNavigation.Tab] = BrowserNavigation.ScrollOffset;
            RenderBrowserRowsOnly();
        }


        // Text editing is owned exclusively by TMP_InputField while search has focus.
        // Older builds also polled the physical Backspace key and manually removed text;
        // TMP processed the same key independently, causing intermittent double deletion.
        // Keep no second Backspace path here.

        private static BasePickupItem ResolveBrowserPreviewLiveItem(string itemId)
        {
            if (string.IsNullOrEmpty(itemId)) return null;

            try
            {
                object raw = GetMember(_lastItemPointerHandler, "_item");
                BasePickupItem pickup = raw as BasePickupItem;
                if (pickup != null && string.Equals(pickup.Id, itemId, StringComparison.OrdinalIgnoreCase))
                    return pickup;
            }
            catch { }

            try
            {
                string[] memberNames = new string[] { "_item", "Item", "PickupItem", "CurrentItem" };
                for (int i = 0; i < memberNames.Length; i++)
                {
                    object raw = GetMember(_lastItemSlot, memberNames[i]);
                    BasePickupItem pickup = raw as BasePickupItem;
                    if (pickup != null && string.Equals(pickup.Id, itemId, StringComparison.OrdinalIgnoreCase))
                        return pickup;
                }
            }
            catch { }

            return null;
        }

        private static BasePickupItem CreateBrowserTooltipPreviewItem(string itemId)
        {
            if (string.IsNullOrEmpty(itemId)) return null;

            try
            {
                ItemFactory factory = SingletonMonoBehaviour<ItemFactory>.Instance;
                if (factory == null) return null;

                // CreateForInventory creates a detached data object. It does not place the
                // item in cargo, inventory or on the floor. randomize=false keeps the
                // preview stable while still supplying every runtime component required by
                // TooltipFactory.BuildItemTooltip(BasePickupItem).
                return factory.CreateForInventory(itemId, false, false);
            }
            catch { return null; }
        }

        private static BasePickupItemRecord ResolveBrowserPreviewRecord(string itemId, BasePickupItem liveItem)
        {
            if (liveItem != null)
            {
                try
                {
                    BasePickupItemRecord liveRecord = liveItem.Record<BasePickupItemRecord>();
                    if (liveRecord != null) return liveRecord;
                }
                catch { }
            }

            if (string.IsNullOrEmpty(itemId) || Data.Items == null) return null;
            try { return Data.Items.GetSimpleRecord<ItemRecord>(itemId, true); }
            catch { return null; }
        }

        private static void PrepareBrowserPreviewTooltipHandler(string itemId)
        {
            if (_browserPreviewTooltipHandler == null || string.IsNullOrEmpty(itemId)) return;
            if (string.Equals(_browserPreviewTooltipItemId, itemId, StringComparison.OrdinalIgnoreCase)) return;

            HideBrowserPreviewTooltip();

            BasePickupItem liveItem = _browserPreviewLiveItem;
            if (liveItem != null && !string.Equals(liveItem.Id, itemId, StringComparison.OrdinalIgnoreCase))
                liveItem = null;

            BasePickupItemRecord record = ResolveBrowserPreviewRecord(itemId, liveItem);

            if (liveItem != null && record != null)
            {
                try
                {
                    // Exact LoadoutPresets R18 path: preserve the real instance so modified
                    // weapon/armor stats are the same values shown by vanilla inventory hover.
                    _browserPreviewTooltipHandler.Initialize(liveItem, record);
                    _browserPreviewTooltipItemId = itemId;
                    return;
                }
                catch { }
            }

            if (record != null)
            {
                try
                {
                    BasePickupItem previewItem = CreateBrowserTooltipPreviewItem(itemId);
                    BasePickupItemRecord previewRecord = ResolveBrowserPreviewRecord(itemId, previewItem);
                    if (previewItem != null && previewRecord != null)
                    {
                        _browserPreviewTooltipHandler.Initialize(previewItem, previewRecord);
                        _browserPreviewTooltipItemId = itemId;
                        return;
                    }

                    _browserPreviewTooltipHandler.Initialize(itemId);
                    _browserPreviewTooltipItemId = itemId;
                    return;
                }
                catch { }
            }

            string baseId = ResolveStaticRelationItemId(itemId);
            if (!string.IsNullOrEmpty(baseId) &&
                !string.Equals(baseId, itemId, StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    _browserPreviewTooltipHandler.Initialize(baseId);
                    _browserPreviewTooltipItemId = itemId;
                    return;
                }
                catch { }
            }

            try
            {
                _browserPreviewTooltipHandler.InitializeSimpleTooltip(LocalizeItem(itemId));
                _browserPreviewTooltipItemId = itemId;
            }
            catch { }
        }

        internal static void RequestBrowserTooltipLayerRaise()
        {
            if (!_inspectorOpen || _inspectorCanvas == null || _browserPreviewTooltipHandler == null) return;

            // Assembly-CSharp audit: ItemTooltipHandler does not store a tooltip object;
            // _createdTooltip is only a bool. The actual item tooltip is the singleton
            // TooltipFactory._tooltip (PropertiesTooltip), so raise exactly that one object.
            Component tooltip = ResolveActiveVanillaItemTooltip();
            if (tooltip == null) return;

            try
            {
                Canvas layer = tooltip.GetComponent<Canvas>();
                if (layer == null || _browserRaisedTooltipCanvas != layer)
                {
                    RestoreBrowserTooltipLayer();
                    layer = tooltip.GetComponent<Canvas>();
                    _browserRaisedTooltipCanvasAdded = layer == null;
                    if (layer == null) layer = tooltip.gameObject.AddComponent<Canvas>();

                    _browserRaisedTooltipCanvas = layer;
                    _browserRaisedTooltipOriginalOverrideSorting = layer.overrideSorting;
                    _browserRaisedTooltipOriginalSortingLayerId = layer.sortingLayerID;
                    _browserRaisedTooltipOriginalSortingOrder = layer.sortingOrder;
                    _browserRaisedTooltipOriginalShaderChannels = layer.additionalShaderChannels;
                    _browserRaisedTooltipOriginalWorldCamera = layer.worldCamera;
                }

                layer.overrideSorting = true;
                layer.sortingLayerID = _inspectorCanvas.sortingLayerID;
                layer.sortingOrder = Math.Max(layer.sortingOrder, _inspectorCanvas.sortingOrder + 100);
                layer.additionalShaderChannels = _inspectorCanvas.additionalShaderChannels;
                layer.worldCamera = _inspectorCanvas.worldCamera;

                if (!_browserPreviewTooltipTypeLogged)
                {
                    _browserPreviewTooltipTypeLogged = true;
                    Debug.Log("[ItemIntelligence] Browser native tooltip active: " + tooltip.GetType().FullName + ".");
                }
            }
            catch (Exception ex)
            {
                if (!_browserPreviewTooltipWarningLogged)
                {
                    _browserPreviewTooltipWarningLogged = true;
                    Debug.LogWarning(
                        "[ItemIntelligence] Browser tooltip layer raise skipped: " +
                        ex.Message);
                }

                TripCompatibilityFeatureRuntime(
                    "Tooltip",
                    ex);
            }
        }

        internal static void RestoreBrowserTooltipLayer()
        {
            Canvas layer = _browserRaisedTooltipCanvas;

            if (layer != null)
            {
                try
                {
                    if (_browserRaisedTooltipCanvasAdded)
                    {
                        UnityEngine.Object.Destroy(layer);
                    }
                    else
                    {
                        layer.overrideSorting = _browserRaisedTooltipOriginalOverrideSorting;
                        layer.sortingLayerID = _browserRaisedTooltipOriginalSortingLayerId;
                        layer.sortingOrder = _browserRaisedTooltipOriginalSortingOrder;
                        layer.additionalShaderChannels = _browserRaisedTooltipOriginalShaderChannels;
                        layer.worldCamera = _browserRaisedTooltipOriginalWorldCamera;
                    }
                }
                catch { }
            }

            // Always clear our bookkeeping even if Unity destroyed the tooltip/canvas first.
            _browserRaisedTooltipCanvas = null;
            _browserRaisedTooltipCanvasAdded = false;
            _browserRaisedTooltipOriginalOverrideSorting = false;
            _browserRaisedTooltipOriginalSortingLayerId = 0;
            _browserRaisedTooltipOriginalSortingOrder = 0;
            _browserRaisedTooltipOriginalShaderChannels = AdditionalCanvasShaderChannels.None;
            _browserRaisedTooltipOriginalWorldCamera = null;
        }

        private static void HideBrowserPreviewTooltip()
        {
            if (_browserPreviewTooltipHandler == null) return;
            try
            {
                // Audited ItemTooltipHandler.OnPointerExit hides TooltipFactory._tooltip
                // and resets its internal _createdTooltip bool. Do not reflect that bool
                // as if it were a GameObject (the old v1.6.1/v1.6.2 code did exactly that).
                _browserPreviewTooltipHandler.OnPointerExit(null);
            }
            catch { }
            RestoreBrowserTooltipLayer();
        }

        private static void UpdateBrowserPreview(string itemId)
        {
            HideBrowserPreviewTooltip();
            _browserPreviewTooltipItemId = string.Empty;

            if (_browserPreviewImage != null)
            {
                _browserPreviewImage.sprite = TryResolveItemSmallIcon(itemId);
                _browserPreviewImage.enabled = _browserPreviewImage.sprite != null;
            }

            // Configure before pointer enter, exactly like LoadoutPresets does while
            // rebuilding its item rows. EventSystem then invokes the native handler.
            PrepareBrowserPreviewTooltipHandler(itemId);
        }
        private static void AttachBrowserItemIconNavigation(Image image, int visibleRow, bool chipIcon)
        {
            if (image == null || image.gameObject == null) return;

            UnityEngine.EventSystems.EventTrigger trigger =
                image.gameObject.GetComponent<UnityEngine.EventSystems.EventTrigger>();
            if (trigger == null)
                trigger = image.gameObject.AddComponent<UnityEngine.EventSystems.EventTrigger>();
            if (trigger.triggers == null)
                trigger.triggers = new List<UnityEngine.EventSystems.EventTrigger.Entry>();

            UnityEngine.EventSystems.EventTrigger.Entry entry =
                new UnityEngine.EventSystems.EventTrigger.Entry();
            entry.eventID = UnityEngine.EventSystems.EventTriggerType.PointerClick;
            entry.callback.AddListener(delegate(UnityEngine.EventSystems.BaseEventData eventData)
            {
                UnityEngine.EventSystems.PointerEventData pointer =
                    eventData as UnityEngine.EventSystems.PointerEventData;
                if (pointer != null &&
                    pointer.button != UnityEngine.EventSystems.PointerEventData.InputButton.Left)
                    return;
                HandleBrowserItemIconClick(visibleRow, chipIcon);
            });
            trigger.triggers.Add(entry);

            UnityEngine.EventSystems.EventTrigger.Entry enter =
                new UnityEngine.EventSystems.EventTrigger.Entry();
            enter.eventID = UnityEngine.EventSystems.EventTriggerType.PointerEnter;
            enter.callback.AddListener(delegate(UnityEngine.EventSystems.BaseEventData eventData)
            {
                ShowBrowserItemNavigationHint(visibleRow, chipIcon);
            });
            trigger.triggers.Add(enter);

            UnityEngine.EventSystems.EventTrigger.Entry exit =
                new UnityEngine.EventSystems.EventTrigger.Entry();
            exit.eventID = UnityEngine.EventSystems.EventTriggerType.PointerExit;
            exit.callback.AddListener(delegate(UnityEngine.EventSystems.BaseEventData eventData)
            {
                RestoreBrowserNavigationHelp();
            });
            trigger.triggers.Add(exit);
        }

        private static void AttachBrowserItemTextNavigation(TMP_Text text, int visibleRow)
        {
            if (text == null || text.gameObject == null) return;
            UnityEngine.EventSystems.EventTrigger trigger =
                text.gameObject.GetComponent<UnityEngine.EventSystems.EventTrigger>();
            if (trigger == null) trigger = text.gameObject.AddComponent<UnityEngine.EventSystems.EventTrigger>();
            if (trigger.triggers == null) trigger.triggers = new List<UnityEngine.EventSystems.EventTrigger.Entry>();

            UnityEngine.EventSystems.EventTrigger.Entry click = new UnityEngine.EventSystems.EventTrigger.Entry();
            click.eventID = UnityEngine.EventSystems.EventTriggerType.PointerClick;
            click.callback.AddListener(delegate(UnityEngine.EventSystems.BaseEventData eventData)
            {
                UnityEngine.EventSystems.PointerEventData pointer = eventData as UnityEngine.EventSystems.PointerEventData;
                if (pointer != null && pointer.button != UnityEngine.EventSystems.PointerEventData.InputButton.Left) return;
                HandleBrowserItemIconClick(visibleRow, false);
            });
            trigger.triggers.Add(click);

            UnityEngine.EventSystems.EventTrigger.Entry enter = new UnityEngine.EventSystems.EventTrigger.Entry();
            enter.eventID = UnityEngine.EventSystems.EventTriggerType.PointerEnter;
            enter.callback.AddListener(delegate(UnityEngine.EventSystems.BaseEventData eventData)
            {
                ShowBrowserItemNavigationHint(visibleRow, false);
            });
            trigger.triggers.Add(enter);

            UnityEngine.EventSystems.EventTrigger.Entry exit = new UnityEngine.EventSystems.EventTrigger.Entry();
            exit.eventID = UnityEngine.EventSystems.EventTriggerType.PointerExit;
            exit.callback.AddListener(delegate(UnityEngine.EventSystems.BaseEventData eventData)
            {
                RestoreBrowserNavigationHelp();
            });
            trigger.triggers.Add(exit);
        }

        private static void ShowBrowserItemNavigationHint(int visibleRow, bool chipIcon)
        {
            if (_browserHelpText == null || visibleRow < 0 || visibleRow >= BrowserVisibleRows) return;
            int index = BrowserNavigation.ScrollOffset + visibleRow;
            if (index < 0 || index >= BrowserLines.Count) return;
            BrowserLine line = BrowserLines[index];
            if (line == null) return;
            string targetItemId = chipIcon ? line.ChipItemId : (line.LeftContentKind == BrowserLeftContentKind.Item ? line.Left : string.Empty);
            if (!string.IsNullOrEmpty(targetItemId) && IsKnownItemId(targetItemId))
                _browserHelpText.text = NormalizeModUiText(Ui("ui.lmb_open_item"));
        }

        private static void RestoreBrowserNavigationHelp()
        {
            if (_browserHelpText != null)
                _browserHelpText.text = NormalizeModUiText(Ui("ui.1_7_section_q_e_tab_wheel_page_esc_close"));
        }

        private static void HandleBrowserItemIconClick(int visibleRow, bool chipIcon)
        {
            if (!_inspectorOpen || visibleRow < 0 || visibleRow >= BrowserVisibleRows) return;
            int index = BrowserNavigation.ScrollOffset + visibleRow;
            if (index < 0 || index >= BrowserLines.Count) return;

            BrowserLine line = BrowserLines[index];
            if (line == null) return;

            string targetItemId = chipIcon
                ? line.ChipItemId
                : (line.LeftContentKind == BrowserLeftContentKind.Item ? line.Left : string.Empty);
            if (string.IsNullOrEmpty(targetItemId) || !IsKnownItemId(targetItemId)) return;

            NavigateBrowserToItem(targetItemId, false, chipIcon ? "Chip icon" : "Item icon");
        }

        private static void HandleBrowserRowClick(int visibleRow)
        {
            if (!_inspectorOpen || visibleRow < 0 || visibleRow >= BrowserVisibleRows) return;
            int index = BrowserNavigation.ScrollOffset + visibleRow;
            if (index < 0 || index >= BrowserLines.Count) return;
            BrowserLine line = BrowserLines[index];
            if (line == null || line.Action.IsNone) return;

            BrowserAction action = line.Action;
            switch (action.Kind)
            {
                case BrowserActionKind.SecretDataBack:
                    _secretDataSelectedFactionId = string.Empty;
                    BrowserNavigation.ScrollOffset = 0;
                    RenderBrowser(_inspectorItemId);
                    return;

                case BrowserActionKind.SecretDataFaction:
                    if (!string.IsNullOrEmpty(action.Payload))
                    {
                        _secretDataSelectedFactionId = action.Payload;
                        BrowserNavigation.ScrollOffset = 0;
                        Debug.Log("[ItemIntelligence] Secret Data faction package selected: " + action.Payload + ".");
                        RenderBrowser(_inspectorItemId);
                    }
                    return;

                case BrowserActionKind.CopyText:
                    if (!string.IsNullOrEmpty(action.Payload))
                    {
                        GUIUtility.systemCopyBuffer = action.Payload;
                        Debug.Log("[ItemIntelligence][ModderMode] copied: " + action.Payload + ".");
                    }
                    return;

                case BrowserActionKind.ToggleLootSection:
                    HandleLootSectionToggleAction(action.Payload, index);
                    return;

                case BrowserActionKind.SwitchTab:
                    SetBrowserTab((int)action.Tab);
                    return;

                case BrowserActionKind.LootModifier:
                    HandleLootModifierAction(action.LootModifierCommand);
                    return;

                case BrowserActionKind.OpenItem:
                    if (!string.IsNullOrEmpty(action.Payload) && IsKnownItemId(action.Payload))
                        NavigateBrowserToItem(action.Payload, false, "Related item");
                    return;

                case BrowserActionKind.FactionTechnology:
                    BeginFactionTechnologyNavigation(action.Payload);
                    return;

                case BrowserActionKind.OpenStarmap:
                    BeginStarmapNavigation(action.Payload);
                    return;
            }
        }


    }
}
