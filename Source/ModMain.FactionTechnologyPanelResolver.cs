using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using MGSC;
using UnityEngine;

namespace ItemIntelligence
{
    public static partial class ModMain
    {
        // Supported-build exact fallback. FactionsScreen.OnEnable creates
        // _factionsPanels in Factions.Values.OrderByDescending(<OnEnable>b__41_0)
        // order and filters the same sequence through Factions.IsEnabledFaction.
        // FactionPanel itself does not expose its bound Faction on this build, so
        // reproduce that vanilla ordering rather than matching localized UI text.
        private sealed class FactionPanelOrderEntry
        {
            public object Faction;
            public int Key;
            public int SourceIndex;
        }

        private static bool TryResolveFactionPanelByVanillaOnEnableOrder(
            object screen, object runtimeFaction, string factionId,
            out object targetPanel, out string reason)
        {
            targetPanel = null;
            reason = string.Empty;
            if (screen == null || runtimeFaction == null || string.IsNullOrEmpty(factionId))
            {
                reason = "target faction state is unavailable";
                return false;
            }

            IList panels = GetMember(screen, "_factionsPanels") as IList;
            object factionsState = GetMember(screen, "_factions");
            IEnumerable values = factionsState == null ? null : GetMember(factionsState, "Values") as IEnumerable;
            if (panels == null || values == null)
            {
                reason = "panels not ready";
                return false;
            }

            MethodInfo isEnabled = factionsState.GetType().GetMethod(
                "IsEnabledFaction", InstanceFlags, null, new Type[] { typeof(Faction) }, null);
            MethodInfo orderKey;
            object orderTarget;
            if (isEnabled == null || !TryResolveFactionsScreenOnEnableOrderKey(screen.GetType(), out orderKey, out orderTarget))
            {
                reason = "exact FactionsScreen.OnEnable ordering contract unavailable";
                return false;
            }

            List<FactionPanelOrderEntry> ordered = new List<FactionPanelOrderEntry>();
            int sourceIndex = 0;
            try
            {
                foreach (object value in values)
                {
                    Faction faction = value as Faction;
                    int currentSourceIndex = sourceIndex++;
                    if (faction == null) continue;
                    object enabledValue = isEnabled.Invoke(factionsState, new object[] { faction });
                    if (!(enabledValue is bool) || !(bool)enabledValue) continue;
                    object keyValue = orderKey.Invoke(orderTarget, new object[] { faction });
                    if (!(keyValue is int))
                    {
                        reason = "FactionsScreen.OnEnable order key returned an unexpected type";
                        return false;
                    }
                    ordered.Add(new FactionPanelOrderEntry
                    {
                        Faction = faction,
                        Key = (int)keyValue,
                        SourceIndex = currentSourceIndex
                    });
                }
            }
            catch (TargetInvocationException ex)
            {
                Exception inner = ex.InnerException ?? ex;
                reason = "FactionsScreen.OnEnable ordering threw " + inner.GetType().Name + ": " + inner.Message;
                return false;
            }
            catch (Exception ex)
            {
                reason = "FactionsScreen.OnEnable ordering failed " + ex.GetType().Name + ": " + ex.Message;
                return false;
            }

            ordered.Sort(delegate(FactionPanelOrderEntry a, FactionPanelOrderEntry b)
            {
                int key = b.Key.CompareTo(a.Key);
                return key != 0 ? key : a.SourceIndex.CompareTo(b.SourceIndex);
            });

            if (panels.Count < ordered.Count)
            {
                reason = "panels not ready";
                return false;
            }
            if (panels.Count != ordered.Count)
            {
                reason = "faction panel/order count mismatch";
                return false;
            }

            int targetIndex = -1;
            for (int i = 0; i < ordered.Count; i++)
            {
                object candidate = ordered[i].Faction;
                string candidateId = FirstNonEmpty(
                    GetStringMember(candidate, "Id"),
                    GetStringMember(candidate, "FactionId"));
                if (!ReferenceEquals(candidate, runtimeFaction) &&
                    !string.Equals(candidateId, factionId, StringComparison.OrdinalIgnoreCase))
                    continue;
                if (targetIndex >= 0)
                {
                    reason = "target faction order entry was ambiguous";
                    return false;
                }
                targetIndex = i;
            }

            if (targetIndex < 0 || targetIndex >= panels.Count || panels[targetIndex] == null)
            {
                reason = "target faction order entry not found";
                return false;
            }

            targetPanel = panels[targetIndex];
            Debug.Log("[ItemIntelligence][FactionTechNav] target panel resolved by exact FactionsScreen.OnEnable order: faction=" +
                factionId + ", index=" + targetIndex.ToString() + ", panels=" + panels.Count.ToString() + ".");
            return true;
        }

        private static bool TryResolveFactionsScreenOnEnableOrderKey(
            Type screenType, out MethodInfo orderKey, out object orderTarget)
        {
            orderKey = null;
            orderTarget = null;
            if (screenType == null) return false;
            try
            {
                Type[] nestedTypes = screenType.GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic);
                for (int i = 0; i < nestedTypes.Length; i++)
                {
                    Type nested = nestedTypes[i];
                    if (nested == null || !string.Equals(nested.Name, "<>c", StringComparison.Ordinal)) continue;
                    FieldInfo singleton = nested.GetField("<>9", StaticFlags);
                    MethodInfo candidate = nested.GetMethod(
                        "<OnEnable>b__41_0", InstanceFlags, null, new Type[] { typeof(Faction) }, null);
                    if (singleton == null || candidate == null || candidate.ReturnType != typeof(int)) continue;
                    object target = singleton.GetValue(null);
                    if (target == null) continue;
                    orderKey = candidate;
                    orderTarget = target;
                    return true;
                }
            }
            catch { }
            return false;
        }
    }
}
