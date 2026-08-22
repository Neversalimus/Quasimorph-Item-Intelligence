using System;
using System.Collections;
using System.Reflection;
using HarmonyLib;
using MGSC;
using TMPro;
using UnityEngine;

namespace ItemIntelligence
{
    public static partial class ModMain
    {
        private static string _pendingFactionTechnologyFactionId = string.Empty;
        private static object _pendingFactionTechnologyRuntimeFaction;
        private static Type _pendingFactionTechnologyFallbackType;
        private static int _pendingFactionTechnologyPhase;
        private static int _pendingFactionTechnologyFrames;
        private static int _pendingFactionTechnologyPhaseFrames;

        private static void BeginFactionTechnologyNavigation(string factionId)
        {
            if (string.IsNullOrEmpty(factionId)) return;

            string travelReason;
            if (IsStarmapNavigationForbiddenByTravelState(out travelReason))
            {
                ShowBlockedFactionTechnologyNavigationMessage();
                Debug.LogWarning("[ItemIntelligence][FactionTechNav] Navigation blocked for " + factionId + ": " + travelReason + ".");
                return;
            }

            string contextReason;
            if (!IsStarmapExperimentSpaceContext(out contextReason))
            {
                ShowBlockedFactionTechnologyNavigationMessage();
                Debug.LogWarning("[ItemIntelligence][FactionTechNav] Navigation blocked for " + factionId + ": " + contextReason + ".");
                return;
            }

            string fallbackSummary;
            Type fallbackType = ResolveStarmapExperimentFallbackType(out fallbackSummary);
            if (fallbackType == null || IsRaidPreparationStarmapFallback(fallbackType))
            {
                ShowBlockedFactionTechnologyNavigationMessage();
                Debug.LogWarning("[ItemIntelligence][FactionTechNav] Navigation blocked for " + factionId +
                    ": invalid fallback=" + (fallbackType == null ? "<null>" : fallbackType.FullName) + ".");
                return;
            }

            object runtimeFaction = ResolveFactionById(factionId);
            int availability = ResolveFactionAvailabilityForCurrentSave(factionId);
            if (runtimeFaction == null || availability == 0)
            {
                ShowBlockedFactionTechnologyNavigationMessage();
                Debug.LogWarning("[ItemIntelligence][FactionTechNav] Navigation blocked for " + factionId +
                    ": faction is unresolved or disabled in the current save.");
                return;
            }

            Type factionsScreenType = AccessTools.TypeByName("MGSC.FactionsScreen");
            Type factionTechnologyWindowType = AccessTools.TypeByName("MGSC.FactionTechnologyWindow");
            Type uiType = AccessTools.TypeByName("MGSC.UI");
            if (factionsScreenType == null || factionTechnologyWindowType == null || uiType == null)
            {
                ShowBlockedFactionTechnologyNavigationMessage();
                Debug.LogWarning("[ItemIntelligence][FactionTechNav] Navigation blocked: vanilla faction UI contract is unavailable.");
                return;
            }

            MethodInfo show = uiType.GetMethod("Show", StaticFlags, null,
                new Type[] { typeof(Type), typeof(Type), typeof(bool) }, null);
            if (show == null)
            {
                ShowBlockedFactionTechnologyNavigationMessage();
                Debug.LogWarning("[ItemIntelligence][FactionTechNav] Navigation blocked: UI.Show(Type,Type,bool) unavailable.");
                return;
            }

            _pendingFactionTechnologyFactionId = factionId;
            _pendingFactionTechnologyRuntimeFaction = runtimeFaction;
            _pendingFactionTechnologyFallbackType = fallbackType;
            _pendingFactionTechnologyPhase = 10;
            _pendingFactionTechnologyFrames = 0;
            _pendingFactionTechnologyPhaseFrames = 0;

            Debug.Log("[ItemIntelligence][FactionTechNav] Captured faction=" + factionId +
                ", fallback=" + fallbackType.FullName + ", activeViews=" + (fallbackSummary ?? string.Empty) + ".");
            CloseInspector();
        }

        private static void TickFactionTechnologyNavigation()
        {
            if (_pendingFactionTechnologyPhase == 0) return;
            _pendingFactionTechnologyFrames++;
            _pendingFactionTechnologyPhaseFrames++;
            if (_pendingFactionTechnologyFrames > 180)
            {
                CancelFactionTechnologyNavigation("timeout");
                return;
            }

            Type factionsScreenType = AccessTools.TypeByName("MGSC.FactionsScreen");
            Type factionTechnologyWindowType = AccessTools.TypeByName("MGSC.FactionTechnologyWindow");
            Type uiType = AccessTools.TypeByName("MGSC.UI");
            if (factionsScreenType == null || factionTechnologyWindowType == null || uiType == null)
            {
                CancelFactionTechnologyNavigation("vanilla faction UI types disappeared");
                return;
            }

            if (_pendingFactionTechnologyPhase == 10)
            {
                if (_pendingFactionTechnologyPhaseFrames < 2) return;
                try
                {
                    MethodInfo show = uiType.GetMethod("Show", StaticFlags, null,
                        new Type[] { typeof(Type), typeof(Type), typeof(bool) }, null);
                    if (show == null || _pendingFactionTechnologyFallbackType == null)
                    {
                        CancelFactionTechnologyNavigation("UI.Show/fallback unavailable");
                        return;
                    }
                    HideSourceVanillaTooltip();
                    show.Invoke(null, new object[]
                    {
                        factionsScreenType,
                        _pendingFactionTechnologyFallbackType,
                        true
                    });
                    _pendingFactionTechnologyPhase = 20;
                    _pendingFactionTechnologyPhaseFrames = 0;
                    return;
                }
                catch (TargetInvocationException ex)
                {
                    Exception inner = ex.InnerException ?? ex;
                    CancelFactionTechnologyNavigation("FactionsScreen UI.Show threw " + inner.GetType().Name + ": " + inner.Message);
                    return;
                }
                catch (Exception ex)
                {
                    CancelFactionTechnologyNavigation("FactionsScreen UI.Show failed " + ex.GetType().Name + ": " + ex.Message);
                    return;
                }
            }

            if (_pendingFactionTechnologyPhase == 20)
            {
                object screen = FindActiveUnityObject(factionsScreenType);
                Component screenComponent = screen as Component;
                if (screen == null || screenComponent == null || screenComponent.gameObject == null ||
                    !screenComponent.gameObject.activeInHierarchy)
                {
                    if (_pendingFactionTechnologyPhaseFrames < 90) return;
                    CancelFactionTechnologyNavigation("FactionsScreen did not become active");
                    return;
                }

                object targetPanel;
                string panelReason;
                if (!TryResolveFactionTechnologyPanel(screen, _pendingFactionTechnologyRuntimeFaction,
                    _pendingFactionTechnologyFactionId, out targetPanel, out panelReason))
                {
                    if (_pendingFactionTechnologyPhaseFrames < 90 && panelReason == "panels not ready") return;
                    CancelFactionTechnologyNavigation(panelReason);
                    return;
                }

                MethodInfo select = FindFactionPanelSelectedCallback(factionsScreenType, targetPanel.GetType());
                if (select == null)
                {
                    CancelFactionTechnologyNavigation("FactionPanelOnSelected callback unavailable");
                    return;
                }
                try
                {
                    select.Invoke(screen, new object[] { targetPanel });
                    _pendingFactionTechnologyPhase = 30;
                    _pendingFactionTechnologyPhaseFrames = 0;
                    return;
                }
                catch (TargetInvocationException ex)
                {
                    Exception inner = ex.InnerException ?? ex;
                    CancelFactionTechnologyNavigation("FactionPanelOnSelected threw " + inner.GetType().Name + ": " + inner.Message);
                    return;
                }
                catch (Exception ex)
                {
                    CancelFactionTechnologyNavigation("FactionPanelOnSelected failed " + ex.GetType().Name + ": " + ex.Message);
                    return;
                }
            }

            if (_pendingFactionTechnologyPhase == 30)
            {
                if (_pendingFactionTechnologyPhaseFrames < 2) return;
                object screen = FindActiveUnityObject(factionsScreenType);
                Component screenComponent = screen as Component;
                if (screen == null || screenComponent == null || screenComponent.gameObject == null ||
                    !screenComponent.gameObject.activeInHierarchy)
                {
                    CancelFactionTechnologyNavigation("FactionsScreen disappeared before Technology action");
                    return;
                }

                Type factionWindowType = AccessTools.TypeByName("MGSC.FactionWindow");
                Component factionWindow = FindActiveUnityObject(factionWindowType) as Component;
                if (factionWindow == null || factionWindow.gameObject == null || !factionWindow.gameObject.activeInHierarchy)
                {
                    if (_pendingFactionTechnologyPhaseFrames < 60) return;
                    CancelFactionTechnologyNavigation("FactionWindow did not become active after faction selection");
                    return;
                }

                MethodInfo showTechnology = factionsScreenType.GetMethod(
                    "FactionWindowOnShowTechnologyWindow", InstanceFlags, null, Type.EmptyTypes, null);
                if (showTechnology == null)
                {
                    CancelFactionTechnologyNavigation("FactionWindowOnShowTechnologyWindow callback unavailable");
                    return;
                }
                try
                {
                    showTechnology.Invoke(screen, null);
                    _pendingFactionTechnologyPhase = 40;
                    _pendingFactionTechnologyPhaseFrames = 0;
                    return;
                }
                catch (TargetInvocationException ex)
                {
                    Exception inner = ex.InnerException ?? ex;
                    CancelFactionTechnologyNavigation("Technology callback threw " + inner.GetType().Name + ": " + inner.Message);
                    return;
                }
                catch (Exception ex)
                {
                    CancelFactionTechnologyNavigation("Technology callback failed " + ex.GetType().Name + ": " + ex.Message);
                    return;
                }
            }

            if (_pendingFactionTechnologyPhase == 40)
            {
                Component technology = FindActiveUnityObject(factionTechnologyWindowType) as Component;
                if (technology != null && technology.gameObject != null && technology.gameObject.activeInHierarchy)
                {
                    Debug.Log("[ItemIntelligence][FactionTechNav] opened faction=" +
                        _pendingFactionTechnologyFactionId +
                        ", screen=FactionsScreen, target=FactionTechnologyWindow.");
                    ClearFactionTechnologyNavigationState();
                    return;
                }
                if (_pendingFactionTechnologyPhaseFrames < 60) return;
                CancelFactionTechnologyNavigation("FactionTechnologyWindow did not become active");
            }
        }

        private static bool TryResolveFactionTechnologyPanel(
            object screen, object runtimeFaction, string factionId, out object targetPanel, out string reason)
        {
            targetPanel = null;
            reason = string.Empty;
            if (screen == null || runtimeFaction == null || string.IsNullOrEmpty(factionId))
            {
                reason = "target faction state is unavailable";
                return false;
            }

            IEnumerable panels = GetMember(screen, "_factionsPanels") as IEnumerable;
            if (panels == null)
            {
                reason = "panels not ready";
                return false;
            }

            int matches = 0;
            foreach (object panel in panels)
            {
                if (panel == null) continue;
                object panelFaction = ResolveFactionObjectFromPanel(panel);
                if (panelFaction == null) continue;
                string panelFactionId = FirstNonEmpty(
                    GetStringMember(panelFaction, "Id"),
                    GetStringMember(panelFaction, "FactionId"));
                if (!ReferenceEquals(panelFaction, runtimeFaction) &&
                    !string.Equals(panelFactionId, factionId, StringComparison.OrdinalIgnoreCase))
                    continue;
                targetPanel = panel;
                matches++;
            }

            if (matches == 1 && targetPanel != null) return true;
            if (matches == 0)
            {
                if (TryResolveFactionPanelByVanillaOnEnableOrder(
                    screen, runtimeFaction, factionId, out targetPanel, out reason))
                    return true;
                return false;
            }
            reason = "target faction panel was ambiguous";
            targetPanel = null;
            return false;
        }

        private static object ResolveFactionObjectFromPanel(object panel)
        {
            if (panel == null) return null;
            object exact = FirstNonNull(
                GetMember(panel, "_faction"),
                GetMember(panel, "Faction"),
                GetMember(panel, "_record"),
                GetMember(panel, "Record"));
            if (exact is Faction) return exact;

            // Fail-soft schema fallback limited to this one vanilla panel instance:
            // only members typed exactly as Faction are eligible; no names/text are
            // interpreted as faction identity.
            try
            {
                FieldInfo[] fields = panel.GetType().GetFields(InstanceFlags);
                for (int i = 0; i < fields.Length; i++)
                {
                    FieldInfo field = fields[i];
                    if (field == null || field.FieldType != typeof(Faction)) continue;
                    object value = field.GetValue(panel);
                    if (value != null) return value;
                }
                PropertyInfo[] properties = panel.GetType().GetProperties(InstanceFlags);
                for (int i = 0; i < properties.Length; i++)
                {
                    PropertyInfo property = properties[i];
                    if (property == null || property.PropertyType != typeof(Faction) ||
                        property.GetIndexParameters().Length != 0 || !property.CanRead) continue;
                    object value = property.GetValue(panel, null);
                    if (value != null) return value;
                }
            }
            catch { }
            return null;
        }

        private static MethodInfo FindFactionPanelSelectedCallback(Type screenType, Type panelType)
        {
            if (screenType == null || panelType == null) return null;
            try
            {
                MethodInfo[] methods = screenType.GetMethods(InstanceFlags);
                for (int i = 0; i < methods.Length; i++)
                {
                    MethodInfo method = methods[i];
                    if (method == null || !string.Equals(method.Name, "FactionPanelOnSelected", StringComparison.Ordinal))
                        continue;
                    ParameterInfo[] parameters = method.GetParameters();
                    if (parameters.Length == 1 && parameters[0].ParameterType.IsAssignableFrom(panelType))
                        return method;
                }
            }
            catch { }
            return null;
        }

        private static void ShowBlockedFactionTechnologyNavigationMessage()
        {
            try
            {
                if (_browserStatsText == null) return;
                _browserStatsText.text = NormalizeModUiText(Ui("ui.faction_technology_navigation_unavailable"));
                _browserStatsText.color = new Color(0.92f, 0.78f, 0.34f, 1f);
            }
            catch { }
        }

        private static void CancelFactionTechnologyNavigation(string reason)
        {
            if (_pendingFactionTechnologyPhase != 0 && !string.IsNullOrEmpty(reason))
                Debug.LogWarning("[ItemIntelligence][FactionTechNav] cancelled: " + reason + ".");
            ClearFactionTechnologyNavigationState();
        }

        private static void ClearFactionTechnologyNavigationState()
        {
            _pendingFactionTechnologyFactionId = string.Empty;
            _pendingFactionTechnologyRuntimeFaction = null;
            _pendingFactionTechnologyFallbackType = null;
            _pendingFactionTechnologyPhase = 0;
            _pendingFactionTechnologyFrames = 0;
            _pendingFactionTechnologyPhaseFrames = 0;
        }
    }
}
