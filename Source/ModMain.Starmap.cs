using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using MGSC;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ItemIntelligence
{
    public static partial class ModMain
    {

        // v1.7.36-test2: feature-owned state moved out of Runtime.cs.
        // Declaration ownership only; lifecycle and behavior are unchanged.

        private static string _pendingStarmapTargetId = string.Empty;
        private static int _pendingStarmapFrames;
        private static int _pendingStarmapPhase;
        private static int _pendingStarmapPhaseFrames;
        private static int _pendingStarmapBackAttempts;
        private static bool _pendingStarmapOpenIssued;
        // exp3: captured vanilla fallback for direct MGSC.UI.Show(StarmapScreen, fallback).
        private static Type _pendingStarmapFallbackType;
        private static string _pendingStarmapFallbackLabel = string.Empty;
        private static int _starmapExperimentRecoveryLastFrame = -1000;
        // exp3: UI.Show correctly preserves the fallback chain. Quasimorph can keep several
        // layered source roots alive (for example SpaceshipScreen + Technology Tree +
        // Confirm window), while GetActiveViews may report only the top modal. Suspend the
        // full visible MGSC source stack under UI/Content; do NOT deactivate GameObjects
        // or invoke Back.
        private static readonly List<StarmapSourceViewVisualState> StarmapSourceViewVisualStates =
            new List<StarmapSourceViewVisualState>();
        private static int _starmapSourceVisualSuspendFrame = -1;
        private static int _starmapSourceVisualRestoreDueFrame = -1;

        // v1.7.36-test8: compact travel observer. Runtime test7 proved that a successful
        // vanilla TravelSystem.StartSpaceshipTravel assigns TargetSpaceObject and State,
        // while CurrentSpaceObject remains the origin/empty during flight and becomes the
        // destination on arrival. QII observes only that proven contract now; the earlier
        // assembly-agnostic scalar snapshot/delta probe has been retired.
        private static bool _starmapTravelSafetyPatchesReady;
        private static bool _observedSpaceshipTravelActive;
        private static string _observedSpaceshipTravelOriginId = string.Empty;
        private static string _observedSpaceshipTravelDestinationId = string.Empty;
        private static string _observedSpaceshipTravelTargetId = string.Empty;
        private static string _observedSpaceshipTravelState = string.Empty;
        private static int _observedSpaceshipTravelStartFrame = -1;
        private static string _travelStartOriginPending = string.Empty;
        private static string _travelStartDestinationPending = string.Empty;
        private static bool _qiiStarmapSessionOwned;
        private static bool _qiiStarmapShowFailedUnsafe;

        // Production builds keep only the proven travel-safety/navigation path.
        // The old runtime transition audit and Ctrl+Shift+F9 exporter are intentionally excluded.

        private static int InstallStarmapTravelSafetyPatches(Harmony harmony)
        {
            int count = 0;
            try
            {
                count += PatchNamedMethods(
                    harmony,
                    "MGSC.TravelSystem",
                    "StartSpaceshipTravel",
                    "TravelSystemStartObserverPrefix",
                    "TravelSystemStartObserverPostfix");
                count += PatchNamedMethods(
                    harmony,
                    "MGSC.StarmapScreen",
                    "DepartureButtonOnClick",
                    "QiiStarmapDepartureSafetyPrefix",
                    null);
                count += PatchNamedMethods(
                    harmony,
                    "MGSC.StarmapScreen",
                    "OnDisable",
                    "QiiStarmapOnDisableSafetyPrefix",
                    null);
            }
            catch (Exception ex)
            {
                LogRuntimeBoundaryWarningOnce(
                    "starmap.travel.safety.patch",
                    "Starmap travel-safety Harmony hooks failed to install.",
                    ex);
            }

            _starmapTravelSafetyPatchesReady = count >= 3;
            Debug.Log("[ItemIntelligence][StarmapTravelSafety] hooks=" +
                count.ToString(CultureInfo.InvariantCulture) +
                ", ready=" + (_starmapTravelSafetyPatchesReady ? "true" : "false") + ".");
            return count;
        }

        private static void ResetStarmapRuntimeSessionState()
        {
            // Restore any QII-owned visual suspension before dropping old-scene
            // references. This is a no-op in the normal settled path.
            RestoreStarmapSourceViewVisuals("session reset");
            _pendingStarmapTargetId = string.Empty;
            _pendingStarmapFrames = 0;
            _pendingStarmapPhase = 0;
            _pendingStarmapPhaseFrames = 0;
            _pendingStarmapBackAttempts = 0;
            _pendingStarmapOpenIssued = false;
            _pendingStarmapFallbackType = null;
            _pendingStarmapFallbackLabel = string.Empty;
            _starmapExperimentRecoveryLastFrame = -1000;
            ResetStarmapTravelSafetySession();
        }

        private static void ResetStarmapTravelSafetySession()
        {
            _observedSpaceshipTravelActive = false;
            _observedSpaceshipTravelOriginId = string.Empty;
            _observedSpaceshipTravelDestinationId = string.Empty;
            _observedSpaceshipTravelTargetId = string.Empty;
            _observedSpaceshipTravelState = string.Empty;
            _observedSpaceshipTravelStartFrame = -1;
            _travelStartOriginPending = string.Empty;
            _travelStartDestinationPending = string.Empty;
            _qiiStarmapSessionOwned = false;
            _qiiStarmapShowFailedUnsafe = false;
        }

        private static void ResetStarmapObservedTravelOnly()
        {
            _observedSpaceshipTravelActive = false;
            _observedSpaceshipTravelOriginId = string.Empty;
            _observedSpaceshipTravelDestinationId = string.Empty;
            _observedSpaceshipTravelTargetId = string.Empty;
            _observedSpaceshipTravelState = string.Empty;
            _observedSpaceshipTravelStartFrame = -1;
            _travelStartOriginPending = string.Empty;
            _travelStartDestinationPending = string.Empty;
        }

        private static void TravelSystemStartObserverPrefix(object[] __args)
        {
            try
            {
                object travelData = null;
                string destination = string.Empty;
                if (__args != null)
                {
                    for (int i = 0; i < __args.Length; i++)
                    {
                        object arg = __args[i];
                        if (arg == null) continue;
                        Type type = arg.GetType();
                        if (string.Equals(type.FullName, "MGSC.TravelMetadata", StringComparison.Ordinal))
                            travelData = arg;
                    }

                    for (int i = __args.Length - 1; i >= 0; i--)
                    {
                        string candidate = __args[i] as string;
                        if (candidate == null) continue;
                        destination = candidate;
                        break;
                    }
                }

                _travelStartOriginPending =
                    travelData == null ? string.Empty : GetStringMember(travelData, "CurrentSpaceObject");
                _travelStartDestinationPending = destination ?? string.Empty;
            }
            catch (Exception ex)
            {
                LogRuntimeBoundaryWarningOnce(
                    "starmap.travel.observe.prefix",
                    "Could not capture the pre-travel vanilla state.",
                    ex);
                _travelStartOriginPending = string.Empty;
                _travelStartDestinationPending = string.Empty;
            }
        }

        private static void TravelSystemStartObserverPostfix(object[] __args)
        {
            object travelData = null;
            try
            {
                if (__args != null)
                {
                    for (int i = 0; i < __args.Length; i++)
                    {
                        object arg = __args[i];
                        if (arg == null) continue;
                        if (string.Equals(arg.GetType().FullName, "MGSC.TravelMetadata", StringComparison.Ordinal))
                        {
                            travelData = arg;
                            break;
                        }
                    }
                }

                _observedSpaceshipTravelOriginId = _travelStartOriginPending ?? string.Empty;
                _observedSpaceshipTravelDestinationId = _travelStartDestinationPending ?? string.Empty;
                _observedSpaceshipTravelTargetId =
                    travelData == null ? string.Empty : GetStringMember(travelData, "TargetSpaceObject");
                _observedSpaceshipTravelState =
                    travelData == null ? string.Empty : GetStringMember(travelData, "State");
                if (string.IsNullOrEmpty(_observedSpaceshipTravelDestinationId) &&
                    !string.IsNullOrEmpty(_observedSpaceshipTravelTargetId))
                    _observedSpaceshipTravelDestinationId = _observedSpaceshipTravelTargetId;
                _observedSpaceshipTravelStartFrame = Time.frameCount;
                _observedSpaceshipTravelActive = true;

                Debug.Log("[ItemIntelligence][StarmapTravelSafety] observed vanilla travel start: origin=" +
                    (_observedSpaceshipTravelOriginId.Length == 0 ? "<empty>" : _observedSpaceshipTravelOriginId) +
                    ", destination=" +
                    (_observedSpaceshipTravelDestinationId.Length == 0 ? "<empty>" : _observedSpaceshipTravelDestinationId) +
                    ", target=" +
                    (_observedSpaceshipTravelTargetId.Length == 0 ? "<empty>" : _observedSpaceshipTravelTargetId) +
                    ", state=" +
                    (_observedSpaceshipTravelState.Length == 0 ? "<empty>" : _observedSpaceshipTravelState) + ".");
            }
            catch (Exception ex)
            {
                // StartSpaceshipTravel completed, therefore QII must fail closed even if
                // the compact metadata read itself failed.
                _observedSpaceshipTravelActive = true;
                _observedSpaceshipTravelOriginId = _travelStartOriginPending ?? string.Empty;
                _observedSpaceshipTravelDestinationId = _travelStartDestinationPending ?? string.Empty;
                _observedSpaceshipTravelTargetId = string.Empty;
                _observedSpaceshipTravelState = string.Empty;
                _observedSpaceshipTravelStartFrame = Time.frameCount;
                LogRuntimeBoundaryWarningOnce(
                    "starmap.travel.observe.postfix",
                    "Vanilla travel started, but TargetSpaceObject/State could not be observed; QII Starmap navigation will stay blocked until the destination is reached.",
                    ex);
            }
        }

        private static bool IsObservedSpaceshipTravelActive(out string reason)
        {
            reason = string.Empty;
            if (!_observedSpaceshipTravelActive)
            {
                // A save can be written while the ship is already travelling. Loading
                // that save does not call StartSpaceshipTravel again, so adopt the live
                // vanilla state on demand before allowing QII to open Starmap.
                if (TryAdoptLoadedVanillaTravelState(out reason)) return true;
                return false;
            }

            try
            {
                object travelData = _tradeTravelMetadata;
                if (travelData == null)
                {
                    Type travelType = AccessTools.TypeByName("MGSC.TravelMetadata");
                    if (travelType != null)
                        travelData = ResolveStateModule(travelType);
                }

                string currentSpaceObject =
                    travelData == null ? string.Empty : GetStringMember(travelData, "CurrentSpaceObject");
                string currentTarget =
                    travelData == null ? string.Empty : GetStringMember(travelData, "TargetSpaceObject");
                string currentState =
                    travelData == null ? string.Empty : GetStringMember(travelData, "State");

                bool destinationReached =
                    (!string.IsNullOrEmpty(_observedSpaceshipTravelDestinationId) &&
                     string.Equals(
                         currentSpaceObject,
                         _observedSpaceshipTravelDestinationId,
                         StringComparison.OrdinalIgnoreCase)) ||
                    (string.IsNullOrEmpty(_observedSpaceshipTravelDestinationId) &&
                     string.Equals(currentState, "Idle", StringComparison.OrdinalIgnoreCase));

                // Test7 proved CurrentSpaceObject does not become the destination until
                // vanilla travel completion (it is origin/empty during flight). Keep a
                // short frame guard only to avoid treating an immediate same-frame state
                // publication as arrival.
                if (destinationReached && Time.frameCount > _observedSpaceshipTravelStartFrame + 5)
                {
                    Debug.Log("[ItemIntelligence][StarmapTravelSafety] observed vanilla travel completed: destination=" +
                        (_observedSpaceshipTravelDestinationId.Length == 0
                            ? "<empty>"
                            : _observedSpaceshipTravelDestinationId) +
                        ", current=" + (currentSpaceObject.Length == 0 ? "<empty>" : currentSpaceObject) +
                        ", target=" + (currentTarget.Length == 0 ? "<empty>" : currentTarget) +
                        ", state=" + (currentState.Length == 0 ? "<empty>" : currentState) + ".");
                    ResetStarmapObservedTravelOnly();
                    return false;
                }

                reason = "observed active vanilla spaceship travel" +
                    " origin=" + (_observedSpaceshipTravelOriginId.Length == 0 ? "<empty>" : _observedSpaceshipTravelOriginId) +
                    " destination=" + (_observedSpaceshipTravelDestinationId.Length == 0 ? "<empty>" : _observedSpaceshipTravelDestinationId) +
                    " current=" + (currentSpaceObject.Length == 0 ? "<empty>" : currentSpaceObject) +
                    " target=" + (currentTarget.Length == 0 ? "<empty>" : currentTarget) +
                    " state=" + (currentState.Length == 0 ? "<empty>" : currentState);
                return true;
            }
            catch (Exception ex)
            {
                reason = "observed vanilla travel is still active; completion probe failed: " +
                    ex.GetType().Name + ": " + ex.Message;
                return true;
            }
        }

        private static bool TryAdoptLoadedVanillaTravelState(out string reason)
        {
            reason = string.Empty;
            try
            {
                object travelData = _tradeTravelMetadata;
                if (travelData == null)
                {
                    Type travelType = AccessTools.TypeByName("MGSC.TravelMetadata");
                    if (travelType != null)
                        travelData = ResolveStateModule(travelType);
                }
                if (travelData == null) return false;

                string current = GetStringMember(travelData, "CurrentSpaceObject");
                string target = GetStringMember(travelData, "TargetSpaceObject");
                string state = GetStringMember(travelData, "State");

                // Runtime test7 proved Idle as the completed state. Any non-Idle state
                // with vanilla TravelMetadata present is conservatively treated as an
                // already-running transition; this path performs no writes.
                if (string.IsNullOrEmpty(state) ||
                    string.Equals(state, "Idle", StringComparison.OrdinalIgnoreCase))
                    return false;

                reason = "loaded vanilla spaceship travel" +
                    " current=" + (current.Length == 0 ? "<empty>" : current) +
                    " target=" + (target.Length == 0 ? "<empty>" : target) +
                    " state=" + state;

                if (!string.IsNullOrEmpty(target))
                {
                    _observedSpaceshipTravelActive = true;
                    _observedSpaceshipTravelOriginId = current ?? string.Empty;
                    _observedSpaceshipTravelDestinationId = target;
                    _observedSpaceshipTravelTargetId = target;
                    _observedSpaceshipTravelState = state;
                    _observedSpaceshipTravelStartFrame = Time.frameCount;

                    Debug.Log("[ItemIntelligence][StarmapTravelSafety] adopted active vanilla travel from loaded session: current=" +
                        (current.Length == 0 ? "<empty>" : current) +
                        ", target=" + target +
                        ", state=" + state + ".");
                }

                return true;
            }
            catch (Exception ex)
            {
                LogRuntimeBoundaryWarningOnce(
                    "starmap.travel.adopt.loaded",
                    "Could not inspect a loaded vanilla travel session; the normal observed-travel and vanilla-command guards remain active.",
                    ex);
                return false;
            }
        }

        private static bool QiiStarmapDepartureSafetyPrefix(object __instance)
        {
            if (!_qiiStarmapSessionOwned) return true;

            string reason;
            if (_qiiStarmapShowFailedUnsafe)
            {
                reason = "QII Starmap OnEnable did not complete successfully";
            }
            else if (IsObservedSpaceshipTravelActive(out reason))
            {
                // reason supplied by observer.
            }
            else
            {
                return true;
            }

            Debug.LogError("[ItemIntelligence][StarmapTravelSafety] BLOCKED QII departure before vanilla TravelSystem: " +
                reason + ".");
            ShowBlockedStarmapNavigationMessage("ui.starmap_unavailable_during_travel");
            return false;
        }

        private static void QiiStarmapOnDisableSafetyPrefix(object __instance)
        {
            if (!_qiiStarmapSessionOwned) return;
            _qiiStarmapSessionOwned = false;
            _qiiStarmapShowFailedUnsafe = false;
        }

        private static void BeginStarmapNavigation(string spaceObjectId)
        {
            if (string.IsNullOrEmpty(spaceObjectId)) return;

            // v1.7.29: semantic travel-state guard. SpaceHudScreen can remain the
            // default view while the player is inside Bramfatura/deployment flow, but
            // ordinary interplanetary Starmap navigation is not a legal vanilla action
            // there. Fail closed before closing Item Intelligence or touching MGSC.UI.
            string semanticReason;
            if (IsStarmapNavigationForbiddenByTravelState(out semanticReason))
            {
                string blockedKey = semanticReason != null &&
                    semanticReason.IndexOf("Bramfatura", StringComparison.OrdinalIgnoreCase) >= 0
                    ? "ui.starmap_unavailable_in_bramfatura"
                    : "ui.starmap_unavailable_during_travel";
                ShowBlockedStarmapNavigationMessage(blockedKey);
                Debug.LogWarning("[ItemIntelligence][StarmapNav] Navigation blocked for " +
                    spaceObjectId + ": " + semanticReason + ".");
                return;
            }

            // v1.7.28-exp3: use Quasimorph's own view/fallback system instead of
            // simulating the Space HUD starmap button or manually unwinding screens.
            // The runtime audit proved UI.Show(type, fallbackType, ...) stores an
            // explicit fallback for each view (for vanilla Starmap this is SpaceHud).
            // We capture the currently active vanilla view and ask MGSC.UI to show the
            // Starmap with that exact view as its fallback. Back should therefore
            // restore Inventory, Technology Tree, confirmation dialogs, etc. through
            // the same mechanism the game already uses everywhere else.
            string contextReason;
            if (!IsStarmapExperimentSpaceContext(out contextReason))
            {
                ShowBlockedStarmapNavigationMessage();
                Debug.LogWarning("[ItemIntelligence][StarmapNav] Navigation blocked for " +
                    spaceObjectId + ": " + contextReason + ".");
                return;
            }

            string fallbackSummary;
            Type fallbackType = ResolveStarmapExperimentFallbackType(out fallbackSummary);
            Type hudType = AccessTools.TypeByName("MGSC.SpaceHudScreen");
            if (fallbackType == null) fallbackType = hudType;

            // Deployment/raid-preparation views are not valid interplanetary-navigation
            // fallbacks. This is intentionally independent from TravelMetadata so the
            // known dangerous route remains blocked even if the Bramfatura probe is
            // temporarily unavailable or the same preparation UI is reused elsewhere.
            if (IsRaidPreparationStarmapFallback(fallbackType))
            {
                ShowBlockedStarmapNavigationMessage("ui.starmap_unavailable_during_raid_prep");
                Debug.LogWarning("[ItemIntelligence][StarmapNav] Navigation blocked for " +
                    spaceObjectId + ": raid preparation fallback=" +
                    (fallbackType == null ? "<null>" : fallbackType.FullName) + ".");
                return;
            }

            if (fallbackType == null)
            {
                ShowBlockedStarmapNavigationMessage();
                Debug.LogWarning("[ItemIntelligence][StarmapNav] Navigation blocked for " +
                    spaceObjectId + ": no vanilla fallback view could be resolved.");
                return;
            }

            _pendingStarmapTargetId = spaceObjectId;
            _pendingStarmapFallbackType = fallbackType;
            _pendingStarmapFallbackLabel = fallbackSummary ?? fallbackType.FullName;
            _pendingStarmapFrames = 0;
            _pendingStarmapPhase = 10;
            _pendingStarmapPhaseFrames = 0;
            _pendingStarmapBackAttempts = 0;
            _pendingStarmapOpenIssued = false;

            Debug.Log("[ItemIntelligence][StarmapNav] Captured fallback=" +
                fallbackType.FullName + " activeViews=" + _pendingStarmapFallbackLabel +
                " target=" + spaceObjectId + ".");
            CloseInspector();
        }

        private static bool IsStarmapNavigationForbiddenByTravelState(out string reason)
        {
            reason = string.Empty;
            try
            {
                // Authoritative vanilla state already used by the Trade travel-time
                // column. Normal point-to-point travel is deliberately unavailable while
                // IsInBramfatura=true, so station-to-Starmap navigation must obey the
                // same semantic boundary.
                if (RefreshTradeTravelOriginSnapshotSafe(false) && _tradeTravelInBramfatura)
                {
                    reason = "TravelMetadata.IsInBramfatura=true";
                    return true;
                }

                // v1.7.36-test7: the authoritative guard is now an observation of
                // TravelSystem.StartSpaceshipTravel itself. Test6 proved that the
                // SpaceHud starmap button may still look locally active/interactable
                // while the ship is already travelling.
                if (!_starmapTravelSafetyPatchesReady)
                {
                    reason = "TravelSystem safety observer is unavailable";
                    return true;
                }

                string observedTravelReason;
                if (IsObservedSpaceshipTravelActive(out observedTravelReason))
                {
                    reason = observedTravelReason;
                    return true;
                }

                // Keep the vanilla-command check as a second independent signal for
                // other navigation-locked ship states.
                // Direct UI.Show is intentionally used to preserve the source-screen
                // fallback, but that also bypasses SpaceHudScreen.StarmapButtonOnClick.
                // During an active flight vanilla disables/hides that command; opening
                // Starmap directly in that state can expose Departure and start a second
                // TravelSystem transition, which vanilla is not designed to handle.
                //
                // Check the underlying HUD button itself. If SpaceHud is the visible view,
                // use the full vanilla usability test. If another ship screen is layered
                // over it (Inventory/Arsenal/Tech Tree), ignore only parent visibility and
                // preserve the button's own active/interactable state. This keeps the
                // normal QII nested-screen navigation path while respecting travel locks.
                string availabilityReason;
                if (!IsUnderlyingVanillaStarmapCommandAvailable(out availabilityReason))
                {
                    reason = availabilityReason;
                    return true;
                }
            }
            catch (Exception ex)
            {
                // This navigation route can otherwise create an illegal second travel.
                // Fail closed when the vanilla availability contract cannot be checked.
                reason = "travel-state guard failed: " + ex.GetType().Name + ": " + ex.Message;
                return true;
            }
            return false;
        }

        private static bool IsUnderlyingVanillaStarmapCommandAvailable(out string reason)
        {
            reason = string.Empty;
            Type hudType = AccessTools.TypeByName("MGSC.SpaceHudScreen");
            if (hudType == null)
            {
                reason = "MGSC.SpaceHudScreen type is unavailable";
                return false;
            }

            object hud = FindActiveUnityObject(hudType);
            Component hudComponent = hud as Component;
            if (hud == null || hudComponent == null || hudComponent.gameObject == null)
            {
                reason = "SpaceHudScreen instance is unavailable";
                return false;
            }

            object button = GetMember(hud, "_starmapButton");
            Component buttonComponent = button as Component;
            GameObject buttonObject = button as GameObject;
            if (buttonObject == null && buttonComponent != null)
                buttonObject = buttonComponent.gameObject;
            if (buttonObject == null)
            {
                reason = "SpaceHudScreen._starmapButton is unavailable";
                return false;
            }

            // When HUD itself is visible, require exactly the same complete usability
            // condition used by the old vanilla-button navigation path.
            if (hudComponent.gameObject.activeInHierarchy)
            {
                if (IsUiObjectActuallyUsable(button)) return true;
                reason = "vanilla starmap command is not usable on the active Space HUD";
                return false;
            }

            // A nested ship view may hide the HUD via its parent CanvasGroup. Parent
            // visibility is not a travel-state signal, so inspect only the command's own
            // state here. Quasimorph disables/hides this button itself while travelling.
            if (!buttonObject.activeSelf)
            {
                reason = "vanilla starmap command is disabled/hidden by current ship state";
                return false;
            }

            Behaviour behaviour = buttonComponent as Behaviour;
            if (behaviour != null && !behaviour.enabled)
            {
                reason = "vanilla starmap command component is disabled";
                return false;
            }

            Selectable selectable = buttonComponent as Selectable;
            if (selectable == null) selectable = buttonObject.GetComponent<Selectable>();
            if (selectable != null && !selectable.interactable)
            {
                reason = "vanilla starmap command is non-interactable";
                return false;
            }

            CanvasGroup localGroup = buttonObject.GetComponent<CanvasGroup>();
            if (localGroup != null &&
                (localGroup.alpha <= 0.01f || !localGroup.interactable || !localGroup.blocksRaycasts))
            {
                reason = "vanilla starmap command local CanvasGroup is disabled";
                return false;
            }

            return true;
        }

        private static bool IsRaidPreparationStarmapFallback(Type fallbackType)
        {
            if (fallbackType == null) return false;
            string fullName = fallbackType.FullName ?? string.Empty;
            return string.Equals(fullName, "MGSC.PrepareRaidScreen", StringComparison.Ordinal) ||
                   string.Equals(fullName, "MGSC.SelectClassScreen", StringComparison.Ordinal);
        }

        private static bool IsStarmapExperimentSpaceContext(out string reason)
        {
            reason = string.Empty;
            try
            {
                Type uiType = AccessTools.TypeByName("MGSC.UI");
                Type hudType = AccessTools.TypeByName("MGSC.SpaceHudScreen");
                if (uiType == null || hudType == null)
                {
                    reason = "MGSC.UI/SpaceHudScreen type is unavailable";
                    return false;
                }

                object ui = FindActiveUnityObject(uiType);
                Type defaultType = null;
                if (ui != null)
                    defaultType = GetMember(ui, "_defaultViewType") as Type;

                // In space mode Quasimorph sets UI.DefaultView to SpaceHudScreen even
                // while nested ship screens are active. This keeps the experiment out
                // of missions/main menu where a Starmap view would be invalid.
                if (defaultType == hudType ||
                    (defaultType != null && string.Equals(defaultType.FullName, hudType.FullName, StringComparison.Ordinal)))
                    return true;

                Component activeHud = FindActiveUnityObject(hudType) as Component;
                if (activeHud != null && activeHud.gameObject != null && activeHud.gameObject.activeInHierarchy)
                    return true;

                reason = "current MGSC.UI default view is " +
                    (defaultType == null ? "<null>" : defaultType.FullName) + ", not SpaceHudScreen";
                return false;
            }
            catch (Exception ex)
            {
                reason = "space-context probe failed: " + ex.Message;
                return false;
            }
        }

        private static Type ResolveStarmapExperimentFallbackType(out string summary)
        {
            summary = string.Empty;
            try
            {
                Type uiType = AccessTools.TypeByName("MGSC.UI");
                Type mapType = AccessTools.TypeByName("MGSC.StarmapScreen");
                Type hudType = AccessTools.TypeByName("MGSC.SpaceHudScreen");
                if (uiType == null) return hudType;

                MethodInfo getActive = uiType.GetMethod("GetActiveViews", StaticFlags, null, Type.EmptyTypes, null);
                object result = getActive == null ? null : getActive.Invoke(null, null);
                IEnumerable enumerable = result as IEnumerable;
                List<Component> candidates = new List<Component>();
                List<string> names = new List<string>();
                if (enumerable != null)
                {
                    foreach (object value in enumerable)
                    {
                        Component component = value as Component;
                        if (component == null || component.gameObject == null || !component.gameObject.activeInHierarchy)
                            continue;
                        if (mapType != null && mapType.IsAssignableFrom(component.GetType()))
                            continue;
                        candidates.Add(component);
                        names.Add(component.GetType().FullName + "@" + component.gameObject.name);
                    }
                }

                summary = names.Count == 0 ? "[]" : "[" + string.Join(", ", names.ToArray()) + "]";
                if (candidates.Count == 0) return hudType;
                if (candidates.Count == 1) return candidates[0].GetType();

                // When more than one vanilla view is active, the visually/topologically
                // upper view normally has the highest sibling index under UI/Content.
                // This matters for modal windows layered over a Technology Tree.
                Component best = candidates[0];
                int bestSibling = best.transform == null ? -1 : best.transform.GetSiblingIndex();
                for (int i = 1; i < candidates.Count; i++)
                {
                    Component c = candidates[i];
                    int sibling = c.transform == null ? -1 : c.transform.GetSiblingIndex();
                    if (sibling >= bestSibling)
                    {
                        best = c;
                        bestSibling = sibling;
                    }
                }
                return best.GetType();
            }
            catch (Exception ex)
            {
                summary = "active-view resolution failed: " + ex.Message;
                return AccessTools.TypeByName("MGSC.SpaceHudScreen");
            }
        }

        private static bool IsStarmapVisualSourceComponent(Component component, Type mapType, Type hudType)
        {
            if (component == null || component.gameObject == null || !component.gameObject.activeInHierarchy)
                return false;

            Type type = component.GetType();
            if (mapType != null && mapType.IsAssignableFrom(type)) return false;
            if (hudType != null && hudType.IsAssignableFrom(type)) return false;

            string fullName = type.FullName ?? string.Empty;
            if (!fullName.StartsWith("MGSC.", StringComparison.Ordinal)) return false;

            string name = type.Name ?? string.Empty;
            bool viewLike = name.EndsWith("Screen", StringComparison.Ordinal) ||
                            name.EndsWith("Window", StringComparison.Ordinal) ||
                            name.EndsWith("Dialog", StringComparison.Ordinal) ||
                            name.EndsWith("Popup", StringComparison.Ordinal);
            if (!viewLike) return false;

            // Quasimorph's layered ship screens used in the failing path are direct
            // children of UI(Clone)/Content. Limit the broad scan to that container so
            // world-space MGSC components and utility behaviours are never suspended.
            Transform parent = component.transform == null ? null : component.transform.parent;
            if (parent == null || parent.gameObject == null ||
                !string.Equals(parent.gameObject.name, "Content", StringComparison.Ordinal))
                return false;

            return true;
        }

        private static void AddStarmapSourceVisualRoot(Component component, Type mapType, Type hudType,
            HashSet<GameObject> seen, List<string> suspended)
        {
            if (!IsStarmapVisualSourceComponent(component, mapType, hudType)) return;

            GameObject go = component.gameObject;
            if (go == null || !seen.Add(go)) return;

            CanvasGroup group = go.GetComponent<CanvasGroup>();
            bool added = false;
            if (group == null)
            {
                group = go.AddComponent<CanvasGroup>();
                added = true;
            }
            if (group == null) return;

            StarmapSourceViewVisualState state = new StarmapSourceViewVisualState();
            state.Root = go;
            state.Group = group;
            state.AddedGroup = added;
            state.Alpha = group.alpha;
            state.Interactable = group.interactable;
            state.BlocksRaycasts = group.blocksRaycasts;
            state.IgnoreParentGroups = group.ignoreParentGroups;
            StarmapSourceViewVisualStates.Add(state);

            group.alpha = 0f;
            group.interactable = false;
            group.blocksRaycasts = false;
            suspended.Add(component.GetType().FullName + "@" + go.name);
        }

        private static bool SuspendStarmapSourceViewVisuals(out string error)
        {
            error = string.Empty;
            RestoreStarmapSourceViewVisuals("replace previous suspension");
            try
            {
                Type uiType = AccessTools.TypeByName("MGSC.UI");
                Type mapType = AccessTools.TypeByName("MGSC.StarmapScreen");
                Type hudType = AccessTools.TypeByName("MGSC.SpaceHudScreen");
                if (uiType == null)
                {
                    error = "MGSC.UI type is unavailable";
                    return false;
                }

                HashSet<GameObject> seen = new HashSet<GameObject>();
                List<string> suspended = new List<string>();

                // First use Quasimorph's public active-view set. This covers normal
                // Inventory/Cargo/Technology Tree paths.
                MethodInfo getActive = uiType.GetMethod("GetActiveViews", StaticFlags, null, Type.EmptyTypes, null);
                IEnumerable enumerable = getActive == null ? null : getActive.Invoke(null, null) as IEnumerable;
                if (enumerable != null)
                {
                    foreach (object value in enumerable)
                        AddStarmapSourceVisualRoot(value as Component, mapType, hudType, seen, suspended);
                }

                // Important exp3 fix: when ConfirmMagnumUpgradeWindow is on top,
                // GetActiveViews() returns only that modal, although the Technology Tree
                // and SpaceshipScreen are still active and rendered underneath it. Scan
                // the actual UI/Content roots and suspend those layered source views too.
                MonoBehaviour[] behaviours = Resources.FindObjectsOfTypeAll<MonoBehaviour>();
                if (behaviours != null)
                {
                    for (int i = 0; i < behaviours.Length; i++)
                        AddStarmapSourceVisualRoot(behaviours[i], mapType, hudType, seen, suspended);
                }

                _starmapSourceVisualSuspendFrame = Time.frameCount;
                _starmapSourceVisualRestoreDueFrame = -1;
                Debug.Log("[ItemIntelligence][StarmapNav] Suspended full source visual stack count=" +
                    StarmapSourceViewVisualStates.Count + " views=[" + string.Join(", ", suspended.ToArray()) + "].");
                return true;
            }
            catch (TargetInvocationException ex)
            {
                Exception inner = ex.InnerException ?? ex;
                error = "source-view suspension threw: " + inner.GetType().Name + ": " + inner.Message;
                RestoreStarmapSourceViewVisuals("suspension exception");
                return false;
            }
            catch (Exception ex)
            {
                error = "source-view suspension failed: " + ex.GetType().Name + ": " + ex.Message;
                RestoreStarmapSourceViewVisuals("suspension exception");
                return false;
            }
        }

        private static void TickStarmapSourceViewVisualSuspension()
        {
            if (StarmapSourceViewVisualStates.Count == 0) return;
            if (Time.frameCount <= _starmapSourceVisualSuspendFrame) return;

            try
            {
                Type mapType = AccessTools.TypeByName("MGSC.StarmapScreen");
                Component map = FindActiveUnityObject(mapType) as Component;
                if (map != null && map.gameObject != null && map.gameObject.activeInHierarchy)
                {
                    _starmapSourceVisualRestoreDueFrame = -1;
                    return;
                }
            }
            catch { return; }

            // Back can remove Starmap before Quasimorph has finished reactivating the
            // fallback chain. Restoring CanvasGroups in that same frame produced the
            // confirmation/tree visual race. Give the vanilla UI two full frames to
            // settle, then restore the exact pre-map presentation.
            if (_starmapSourceVisualRestoreDueFrame < 0)
            {
                _starmapSourceVisualRestoreDueFrame = Time.frameCount + 2;
                return;
            }
            if (Time.frameCount < _starmapSourceVisualRestoreDueFrame) return;

            RestoreStarmapSourceViewVisuals("Starmap fallback settled");
            // Clear any stale hover created by the pointer position used on the map.
            HideSourceVanillaTooltip();
        }

        private static void RestoreStarmapSourceViewVisuals(string reason)
        {
            if (StarmapSourceViewVisualStates.Count == 0)
            {
                _starmapSourceVisualSuspendFrame = -1;
                _starmapSourceVisualRestoreDueFrame = -1;
                return;
            }

            int restored = 0;
            for (int i = StarmapSourceViewVisualStates.Count - 1; i >= 0; i--)
            {
                StarmapSourceViewVisualState state = StarmapSourceViewVisualStates[i];
                if (state == null || state.Group == null) continue;
                try
                {
                    state.Group.alpha = state.Alpha;
                    state.Group.interactable = state.Interactable;
                    state.Group.blocksRaycasts = state.BlocksRaycasts;
                    state.Group.ignoreParentGroups = state.IgnoreParentGroups;
                    if (state.AddedGroup)
                        UnityEngine.Object.Destroy(state.Group);
                    restored++;
                }
                catch { }
            }
            StarmapSourceViewVisualStates.Clear();
            _starmapSourceVisualSuspendFrame = -1;
            _starmapSourceVisualRestoreDueFrame = -1;
            Debug.Log("[ItemIntelligence][StarmapNav] Restored source view visuals count=" +
                restored + " reason=" + (reason ?? string.Empty) + ".");
        }

        private static bool TryShowPendingStarmapViaUiShow(out string error)
        {
            error = string.Empty;
            try
            {
                Type uiType = AccessTools.TypeByName("MGSC.UI");
                Type mapType = AccessTools.TypeByName("MGSC.StarmapScreen");
                if (uiType == null || mapType == null)
                {
                    error = "MGSC.UI/StarmapScreen type is unavailable";
                    return false;
                }

                MethodInfo show = uiType.GetMethod("Show", StaticFlags, null,
                    new Type[] { typeof(Type), typeof(Type), typeof(bool) }, null);
                if (show == null)
                {
                    error = "MGSC.UI.Show(Type, Type, bool) was not found";
                    return false;
                }

                Type fallback = _pendingStarmapFallbackType ?? AccessTools.TypeByName("MGSC.SpaceHudScreen");
                if (fallback == null)
                {
                    error = "fallback type is null";
                    return false;
                }

                // Clear any vanilla item tooltip that belongs to the source screen.
                // Tooltips are hosted outside the source view root and otherwise can
                // remain visible over the Starmap even after the source CanvasGroup is hidden.
                HideSourceVanillaTooltip();

                string suspendError;
                if (!SuspendStarmapSourceViewVisuals(out suspendError))
                {
                    error = suspendError;
                    return false;
                }

                Debug.Log("[ItemIntelligence][StarmapNav] UI.Show StarmapScreen fallback=" +
                    fallback.FullName + " target=" + _pendingStarmapTargetId + ".");
                _qiiStarmapSessionOwned = true;
                _qiiStarmapShowFailedUnsafe = false;
                show.Invoke(null, new object[] { mapType, fallback, true });
                _pendingStarmapOpenIssued = true;
                return true;
            }
            catch (TargetInvocationException ex)
            {
                Exception inner = ex.InnerException ?? ex;
                _qiiStarmapSessionOwned = true;
                _qiiStarmapShowFailedUnsafe = true;
                error = "UI.Show threw: " + inner.GetType().Name + ": " + inner.Message;
                Debug.LogError("[ItemIntelligence][StarmapTravelSafety] Starmap OnEnable/show failed; " +
                    "QII departure is hard-blocked until this Starmap instance is disabled. " +
                    inner.GetType().Name + ": " + inner.Message);
                RestoreStarmapSourceViewVisuals("UI.Show exception");
                return false;
            }
            catch (Exception ex)
            {
                _qiiStarmapSessionOwned = true;
                _qiiStarmapShowFailedUnsafe = true;
                error = "UI.Show failed: " + ex.GetType().Name + ": " + ex.Message;
                Debug.LogError("[ItemIntelligence][StarmapTravelSafety] Starmap show failed; " +
                    "QII departure is hard-blocked until this Starmap instance is disabled. " +
                    ex.GetType().Name + ": " + ex.Message);
                RestoreStarmapSourceViewVisuals("UI.Show exception");
                return false;
            }
        }

        private static void TryStarmapExperimentEmergencyRecovery()
        {
            if (Time.frameCount - _starmapExperimentRecoveryLastFrame < 20) return;
            _starmapExperimentRecoveryLastFrame = Time.frameCount;
            try
            {
                Type uiType = AccessTools.TypeByName("MGSC.UI");
                MethodInfo backToDefault = uiType == null ? null :
                    uiType.GetMethod("BackToDefault", StaticFlags, null, Type.EmptyTypes, null);
                if (backToDefault == null)
                {
                    Debug.LogWarning("[ItemIntelligence][StarmapNav] Emergency recovery unavailable: UI.BackToDefault not found.");
                    return;
                }
                CancelPendingStarmapNavigation("emergency recovery hotkey");
                RestoreStarmapSourceViewVisuals("emergency recovery hotkey");
                CloseInspector();
                backToDefault.Invoke(null, null);
                Debug.LogWarning("[ItemIntelligence][StarmapNav] EMERGENCY Ctrl+Shift+F10 -> UI.BackToDefault invoked.");
            }
            catch (Exception ex)
            {
                Debug.LogError("[ItemIntelligence][StarmapNav] Emergency recovery failed: " + ex);
            }
        }

        private static void ShowBlockedStarmapNavigationMessage()
        {
            ShowBlockedStarmapNavigationMessage("ui.close_current_ship_screen_before_starmap");
        }

        private static void ShowBlockedStarmapNavigationMessage(string uiKey)
        {
            try
            {
                if (_browserStatsText == null) return;
                if (string.IsNullOrEmpty(uiKey)) uiKey = "ui.close_current_ship_screen_before_starmap";
                _browserStatsText.text = NormalizeModUiText(Ui(uiKey));
                _browserStatsText.color = new Color(0.92f, 0.78f, 0.34f, 1f);
            }
            catch { }
        }

        private static bool IsSafeVanillaStarmapHostReady(out string reason)
        {
            reason = string.Empty;
            try
            {
                Component modal = FindActiveBlockingModalBeforeStarmap();
                if (modal != null)
                {
                    reason = "blocking modal is active";
                    return false;
                }

                Component tree = FindActiveTechnologyTreeOverlayBeforeStarmap();
                if (tree != null)
                {
                    reason = "technology tree is active";
                    return false;
                }

                // v1.7.28: ArsenalScreen is the actual stable host observed in the
                // runtime logs for both cargo-only and character inventory screens.
                // Child ItemsStorageView discovery was the reason v1.7.26/v1.7.27
                // rejected valid inventory hosts.  A plain ArsenalScreen is safe only
                // as a SOURCE context: BeginStarmapNavigation closes it exactly once
                // and phase 3 still requires the real Space HUD button before invoking
                // StarmapButtonOnClick.
                Component arsenal = FindActiveArsenalScreen();
                if (arsenal != null)
                {
                    reason = "active ArsenalScreen source host will be safely unwound once";
                    return true;
                }

                return IsVanillaStarmapInvocationReady(out reason);
            }
            catch (Exception ex)
            {
                reason = "safe-host check failed: " + ex.Message;
                return false;
            }
        }

        private static bool IsVanillaStarmapInvocationReady(out string reason)
        {
            reason = string.Empty;
            try
            {
                Component modal = FindActiveBlockingModalBeforeStarmap();
                if (modal != null)
                {
                    reason = "blocking modal is active";
                    return false;
                }

                Component tree = FindActiveTechnologyTreeOverlayBeforeStarmap();
                if (tree != null)
                {
                    reason = "technology tree is active";
                    return false;
                }

                Component arsenal = FindActiveArsenalScreen();
                if (arsenal != null)
                {
                    reason = "ArsenalScreen is still active";
                    return false;
                }

                Type hudType = AccessTools.TypeByName("MGSC.SpaceHudScreen");
                object hud = FindActiveUnityObject(hudType);
                if (hud == null)
                {
                    reason = "SpaceHudScreen is not active";
                    return false;
                }

                object button = GetMember(hud, "_starmapButton");
                if (!IsUiObjectActuallyUsable(button))
                {
                    reason = "the vanilla starmap button is not currently visible/usable";
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                reason = "starmap invocation readiness check failed: " + ex.Message;
                return false;
            }
        }

        private static void CancelPendingStarmapNavigation(string reason)
        {
            if (!string.IsNullOrEmpty(_pendingStarmapTargetId))
                Debug.LogWarning("[ItemIntelligence] Starmap navigation cancelled for " +
                    _pendingStarmapTargetId + ": " + (reason ?? "unsafe UI state") + ".");
            _pendingStarmapTargetId = string.Empty;
            _pendingStarmapFrames = 0;
            _pendingStarmapPhase = 0;
            _pendingStarmapPhaseFrames = 0;
            _pendingStarmapBackAttempts = 0;
            bool hadIssuedMap = _pendingStarmapOpenIssued;
            _pendingStarmapOpenIssued = false;
            _pendingStarmapFallbackType = null;
            _pendingStarmapFallbackLabel = string.Empty;
            if (!hadIssuedMap)
                RestoreStarmapSourceViewVisuals("navigation cancelled before Starmap opened");
        }

        private static Component FindActiveTechnologyTreeOverlayBeforeStarmap()
        {
            try
            {
                TMP_Text[] texts = Resources.FindObjectsOfTypeAll<TMP_Text>();
                if (texts == null) return null;

                for (int i = 0; i < texts.Length; i++)
                {
                    TMP_Text label = texts[i];
                    if (label == null || label.gameObject == null || !label.gameObject.activeInHierarchy) continue;
                    if (_inspectorRoot != null &&
                        (label.gameObject == _inspectorRoot || label.transform.IsChildOf(_inspectorRoot.transform)))
                        continue;
                    if (!IsTechnologyTreeHeaderText(label.text)) continue;

                    // The visible header lives inside the actual technology-tree window.
                    // Walk upward and prefer the first MGSC component whose GameObject is
                    // still active.  We only need a stable identity for logging; UI.Back
                    // performs the actual vanilla close operation.
                    Transform current = label.transform;
                    for (int depth = 0; current != null && depth < 8; depth++, current = current.parent)
                    {
                        Component[] components = current.GetComponents<Component>();
                        if (components == null) continue;
                        for (int c = 0; c < components.Length; c++)
                        {
                            Component component = components[c];
                            if (component == null) continue;
                            Type type = component.GetType();
                            string ns = type.Namespace ?? string.Empty;
                            if (string.Equals(ns, "MGSC", StringComparison.Ordinal) ||
                                ns.StartsWith("MGSC.", StringComparison.Ordinal))
                                return component;
                        }
                    }

                    // Fallback: return the label itself.  The caller only uses this as a
                    // positive structural signal and still closes through MGSC.UI.Back.
                    return label;
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[ItemIntelligence] Technology-tree probe failed: " + ex.Message);
            }
            return null;
        }

        private static Component FindActiveBlockingModalBeforeStarmap()
        {
            try
            {
                // v1.7.22: the Magnum technology confirmation shown by the game does not
                // expose a reliable Popup/Dialog class or GameObject name. Detect it by
                // its actual decision UI instead: two active YES/NO controls sharing a
                // message-bearing UI ancestor. This also covers localized confirmations.
                Component structuralDecision = FindActiveDecisionOverlayByStructure();
                if (structuralDecision != null) return structuralDecision;

                UnityEngine.Object[] components = Resources.FindObjectsOfTypeAll(typeof(Component));
                for (int i = 0; i < components.Length; i++)
                {
                    Component component = components[i] as Component;
                    if (component == null || component.gameObject == null ||
                        !component.gameObject.activeInHierarchy)
                        continue;

                    GameObject go = component.gameObject;
                    if (_inspectorRoot != null && (go == _inspectorRoot || go.transform.IsChildOf(_inspectorRoot.transform)))
                        continue;

                    Type type = component.GetType();
                    string ns = type.Namespace ?? string.Empty;
                    if (!string.Equals(ns, "MGSC", StringComparison.Ordinal) &&
                        !ns.StartsWith("MGSC.", StringComparison.Ordinal))
                        continue;

                    string typeName = type.Name ?? string.Empty;
                    string objectName = go.name ?? string.Empty;
                    if (LooksLikeBlockingModalName(typeName) || LooksLikeBlockingModalName(objectName))
                        return component;

                    // Some modal roots have generic component types but descriptive parent names.
                    Transform parent = go.transform.parent;
                    for (int depth = 0; parent != null && depth < 3; depth++, parent = parent.parent)
                    {
                        if (LooksLikeBlockingModalName(parent.name))
                            return component;
                    }
                }

                // Fallback for modal roots composed mostly from Unity UI components.
                // Keep this intentionally stricter than the MGSC-component probe so a
                // permanently active generic PopupRoot cannot cause repeated Back calls.
                UnityEngine.Object[] gameObjects = Resources.FindObjectsOfTypeAll(typeof(GameObject));
                for (int i = 0; i < gameObjects.Length; i++)
                {
                    GameObject go = gameObjects[i] as GameObject;
                    if (go == null || !go.activeInHierarchy) continue;
                    if (_inspectorRoot != null && (go == _inspectorRoot || go.transform.IsChildOf(_inspectorRoot.transform)))
                        continue;

                    string key = (go.name ?? string.Empty).Trim().ToLowerInvariant();
                    if (key.IndexOf("confirmationwindow", StringComparison.Ordinal) >= 0 ||
                        key.IndexOf("upgradeconfirmation", StringComparison.Ordinal) >= 0 ||
                        key.IndexOf("confirmpopup", StringComparison.Ordinal) >= 0 ||
                        key.IndexOf("messagebox", StringComparison.Ordinal) >= 0 ||
                        key.IndexOf("questionwindow", StringComparison.Ordinal) >= 0 ||
                        key.IndexOf("modaldialog", StringComparison.Ordinal) >= 0)
                        return go.transform;
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[ItemIntelligence] Blocking-modal probe failed: " + ex.Message);
            }
            return null;
        }

        private static bool TryVanillaBackForStarmap(string context)
        {
            try
            {
                MGSC.UI.Back(false);
                _pendingStarmapBackAttempts++;
                _pendingStarmapPhaseFrames = 0;
                Debug.Log("[ItemIntelligence] Vanilla UI.Back before starmap: " + context + ".");
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[ItemIntelligence] UI.Back failed before starmap (" + context + "): " + ex.Message);
                return false;
            }
        }

        private static bool TryOpenPendingStarmap()
        {
            if (_pendingStarmapOpenIssued) return true;
            string unsafeReason;
            if (!IsVanillaStarmapInvocationReady(out unsafeReason))
                return false;
            try
            {
                Type hudType = AccessTools.TypeByName("MGSC.SpaceHudScreen");
                object hud = FindActiveUnityObject(hudType);
                if (hud == null) return false;

                object button = GetMember(hud, "_starmapButton");
                MethodInfo method = hudType.GetMethod("StarmapButtonOnClick", InstanceFlags);
                if (method == null)
                {
                    CancelPendingStarmapNavigation("vanilla StarmapButtonOnClick was not found");
                    return false;
                }

                method.Invoke(hud, new object[] { button, 0 });
                _pendingStarmapOpenIssued = true;
                _pendingStarmapPhaseFrames = 0;
                Debug.Log("[ItemIntelligence] Opening starmap after safe UI unwind for " +
                    _pendingStarmapTargetId + ".");
                return true;
            }
            catch (Exception ex)
            {
                CancelPendingStarmapNavigation("could not invoke vanilla starmap action: " + ex.Message);
                return false;
            }
        }

        private static void TickPendingStarmapNavigation()
        {
            if (string.IsNullOrEmpty(_pendingStarmapTargetId)) return;
            _pendingStarmapFrames++;
            _pendingStarmapPhaseFrames++;

            if (_pendingStarmapFrames > 180)
            {
                CancelPendingStarmapNavigation("starmap transition timed out");
                return;
            }

            // exp3 phase 10: F2 has closed, but the underlying vanilla view is left
            // completely untouched. Show Starmap through MGSC.UI with the captured
            // active view as its fallback. No UI.Back and no HUD button simulation.
            if (_pendingStarmapPhase == 10)
            {
                if (_pendingStarmapPhaseFrames < 2) return;

                // Re-check after F2 closed and immediately before touching MGSC.UI.
                // This closes the tiny race where vanilla travel could begin between
                // selecting a Trade row and the delayed Starmap transition.
                string travelReason;
                if (IsStarmapNavigationForbiddenByTravelState(out travelReason))
                {
                    CancelPendingStarmapNavigation("vanilla starmap command became unavailable: " + travelReason);
                    return;
                }

                string error;
                if (!TryShowPendingStarmapViaUiShow(out error))
                {
                    CancelPendingStarmapNavigation(error);
                    return;
                }
                _pendingStarmapPhase = 11;
                _pendingStarmapPhaseFrames = 0;
                return;
            }

            // exp3 phase 11: wait for the real StarmapScreen and focus the station.
            if (_pendingStarmapPhase == 11)
            {
                try
                {
                    Type mapType = AccessTools.TypeByName("MGSC.StarmapScreen");
                    object map = FindActiveUnityObject(mapType);
                    Component mapComponent = map as Component;
                    if (mapComponent == null || mapComponent.gameObject == null || !mapComponent.gameObject.activeInHierarchy)
                    {
                        if (_pendingStarmapPhaseFrames < 60) return;
                        CancelPendingStarmapNavigation("MGSC.UI.Show did not produce an active StarmapScreen");
                        return;
                    }

                    MethodInfo select = mapType.GetMethod("SelectDestination", InstanceFlags, null,
                        new Type[] { typeof(string) }, null);
                    MethodInfo zoom = mapType.GetMethod("InstantCameraZoom", InstanceFlags, null,
                        new Type[] { typeof(string) }, null);
                    if (select != null) select.Invoke(map, new object[] { _pendingStarmapTargetId });
                    if (zoom != null) zoom.Invoke(map, new object[] { _pendingStarmapTargetId });
                    Debug.Log("[ItemIntelligence][StarmapNav] Starmap focused on " +
                        _pendingStarmapTargetId + "; Back fallback=" +
                        (_pendingStarmapFallbackType == null ? "<null>" : _pendingStarmapFallbackType.FullName) + ".");

                    _pendingStarmapTargetId = string.Empty;
                    _pendingStarmapFrames = 0;
                    _pendingStarmapPhase = 0;
                    _pendingStarmapPhaseFrames = 0;
                    _pendingStarmapBackAttempts = 0;
                    _pendingStarmapOpenIssued = false;
                    _pendingStarmapFallbackType = null;
                    _pendingStarmapFallbackLabel = string.Empty;
                }
                catch (Exception ex)
                {
                    CancelPendingStarmapNavigation("starmap focus failed: " + ex.Message);
                }
            }
        }
    }
}
