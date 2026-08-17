using System;
using System.Collections.Generic;
using System.Text;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ItemIntelligence
{
    /// <summary>
    /// Production-only structural UI resolution used by safe Starmap navigation.
    /// Diagnostic snapshot/export code is deliberately excluded from stable builds.
    /// </summary>
    public static partial class ModMain
    {
        private sealed class StarmapSourceViewVisualState
        {
            public GameObject Root;
            public CanvasGroup Group;
            public bool AddedGroup;
            public float Alpha;
            public bool Interactable;
            public bool BlocksRaycasts;
            public bool IgnoreParentGroups;
        }

        private static bool IsUiObjectActuallyUsable(object value)
        {
            if (value == null) return false;
            GameObject go = value as GameObject;
            Component component = value as Component;
            if (go == null && component != null) go = component.gameObject;
            if (go == null || !go.activeInHierarchy) return false;

            Behaviour behaviour = component as Behaviour;
            if (behaviour != null && !behaviour.enabled) return false;

            Selectable selectable = component as Selectable;
            if (selectable == null) selectable = go.GetComponent<Selectable>();
            if (selectable != null && !selectable.interactable) return false;

            CanvasGroup[] groups = go.GetComponentsInParent<CanvasGroup>(true);
            if (groups != null)
            {
                for (int i = 0; i < groups.Length; i++)
                {
                    CanvasGroup group = groups[i];
                    if (group == null) continue;
                    if (group.alpha <= 0.01f) return false;
                    if (!group.interactable || !group.blocksRaycasts) return false;
                }
            }
            return true;
        }







        private static Component FindActiveArsenalScreen()
        {
            try
            {
                Type arsenalType = AccessTools.TypeByName("MGSC.ArsenalScreen");
                Component arsenal = FindActiveUnityObject(arsenalType) as Component;
                if (arsenal != null && arsenal.gameObject != null && arsenal.gameObject.activeInHierarchy)
                    return arsenal;
            }
            catch { }
            return null;
        }

        private static bool IsTechnologyTreeHeaderText(string value)
        {
            if (string.IsNullOrEmpty(value)) return false;
            string key = NormalizeGameText(value).Trim().ToUpperInvariant();
            if (key.Length == 0) return false;
            return key.IndexOf("TECHNOLOGY TREE", StringComparison.Ordinal) >= 0 ||
                   key.IndexOf("TECH TREE", StringComparison.Ordinal) >= 0 ||
                   key.IndexOf("ДЕРЕВО ТЕХНОЛОГ", StringComparison.Ordinal) >= 0;
        }



        private static bool LooksLikeBlockingModalName(string value)
        {
            if (string.IsNullOrEmpty(value)) return false;
            string key = value.Trim().ToLowerInvariant();
            if (key.Length == 0) return false;

            // Avoid treating ordinary child controls such as ConfirmButton as a modal.
            if (key.IndexOf("button", StringComparison.Ordinal) >= 0 ||
                key.IndexOf("label", StringComparison.Ordinal) >= 0 ||
                key.IndexOf("text", StringComparison.Ordinal) >= 0 ||
                key.IndexOf("icon", StringComparison.Ordinal) >= 0)
                return false;

            return key.IndexOf("confirmation", StringComparison.Ordinal) >= 0 ||
                   key.IndexOf("confirmwindow", StringComparison.Ordinal) >= 0 ||
                   key.IndexOf("confirmpopup", StringComparison.Ordinal) >= 0 ||
                   key.IndexOf("popup", StringComparison.Ordinal) >= 0 ||
                   key.IndexOf("dialog", StringComparison.Ordinal) >= 0 ||
                   key.IndexOf("modal", StringComparison.Ordinal) >= 0 ||
                   key.IndexOf("messagebox", StringComparison.Ordinal) >= 0 ||
                   key.IndexOf("questionwindow", StringComparison.Ordinal) >= 0 ||
                   key.IndexOf("upgradeconfirmation", StringComparison.Ordinal) >= 0;
        }

        private static string NormalizeDecisionLabel(string value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            string text = NormalizeGameText(value).Trim().ToUpperInvariant();
            StringBuilder sb = new StringBuilder(text.Length);
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (char.IsLetterOrDigit(c)) sb.Append(c);
            }
            return sb.ToString();
        }

        private static bool MatchesDecisionLabelToken(string value, string token)
        {
            string key = NormalizeDecisionLabel(value);
            if (string.IsNullOrEmpty(key) || string.IsNullOrEmpty(token)) return false;
            if (string.Equals(key, token, StringComparison.Ordinal)) return true;

            // Quasimorph button captions can include their hotkey in the same text
            // object (for example "Y YES" / "N NO"). NormalizeDecisionLabel turns
            // those into YYES / NNO, so accept a short one- or two-character hotkey
            // prefix while still rejecting ordinary sentences ending in yes/no.
            if (key.Length > token.Length && key.Length <= token.Length + 2 &&
                key.EndsWith(token, StringComparison.Ordinal))
                return true;
            return false;
        }

        private static bool IsDecisionYesLabel(string value)
        {
            return MatchesDecisionLabelToken(value, "YES") ||
                   MatchesDecisionLabelToken(value, "ДА") ||
                   MatchesDecisionLabelToken(value, "ОК") ||
                   MatchesDecisionLabelToken(value, "OK");
        }

        private static bool IsDecisionNoLabel(string value)
        {
            return MatchesDecisionLabelToken(value, "NO") ||
                   MatchesDecisionLabelToken(value, "НЕТ") ||
                   MatchesDecisionLabelToken(value, "CANCEL") ||
                   MatchesDecisionLabelToken(value, "ОТМЕНА");
        }

        private static Transform FindNearestCommonUiAncestor(Transform a, Transform b)
        {
            if (a == null || b == null) return null;
            HashSet<Transform> aParents = new HashSet<Transform>();
            Transform current = a;
            for (int depth = 0; current != null && depth < 12; depth++, current = current.parent)
                aParents.Add(current);

            current = b;
            for (int depth = 0; current != null && depth < 12; depth++, current = current.parent)
                if (aParents.Contains(current)) return current;
            return null;
        }

        private static bool LooksLikeDecisionOverlayRoot(Transform root)
        {
            if (root == null || root.gameObject == null || !root.gameObject.activeInHierarchy) return false;
            if (_inspectorRoot != null &&
                (root.gameObject == _inspectorRoot || root.IsChildOf(_inspectorRoot.transform)))
                return false;

            try
            {
                // Do not require UnityEngine.UI.Selectable here. Quasimorph uses custom
                // CommonButton-style controls in several Magnum screens, so the visible
                // YES/NO controls are not guaranteed to be Selectable instances.
                // The yes/no pair has already been found by the caller; here we only
                // require a nearby message-bearing ancestor.
                TMP_Text[] texts = root.GetComponentsInChildren<TMP_Text>(false);
                if (texts != null)
                {
                    for (int i = 0; i < texts.Length; i++)
                    {
                        TMP_Text t = texts[i];
                        if (t == null || t.gameObject == null || !t.gameObject.activeInHierarchy) continue;
                        string raw = NormalizeGameText(t.text ?? string.Empty).Trim();
                        if (raw.Length >= 10 && !IsDecisionYesLabel(raw) && !IsDecisionNoLabel(raw))
                            return true;
                    }
                }

                // Some older/custom game UI still uses UnityEngine.UI.Text rather than
                // TMP. Include it so decision detection is renderer-agnostic.
                UnityEngine.UI.Text[] legacyTexts = root.GetComponentsInChildren<UnityEngine.UI.Text>(false);
                if (legacyTexts != null)
                {
                    for (int i = 0; i < legacyTexts.Length; i++)
                    {
                        UnityEngine.UI.Text t = legacyTexts[i];
                        if (t == null || t.gameObject == null || !t.gameObject.activeInHierarchy) continue;
                        string raw = NormalizeGameText(t.text ?? string.Empty).Trim();
                        if (raw.Length >= 10 && !IsDecisionYesLabel(raw) && !IsDecisionNoLabel(raw))
                            return true;
                    }
                }
                return false;
            }
            catch { return false; }
        }

        private static Transform FindDecisionOverlayRootForLabels(Transform yes, Transform no)
        {
            Transform common = FindNearestCommonUiAncestor(yes, no);
            if (common == null) return null;

            // The nearest common ancestor is commonly just the horizontal button row.
            // The actual confirmation message is a sibling one or two levels above it.
            // v1.7.22 checked only the button row and therefore missed the real Magnum
            // confirmation shown in the user's reproduction. Walk a small bounded part
            // of the hierarchy upward instead of ever falling back to the whole Canvas.
            Transform candidate = common;
            for (int depth = 0; candidate != null && depth < 6; depth++, candidate = candidate.parent)
            {
                if (LooksLikeDecisionOverlayRoot(candidate)) return candidate;
                if (candidate.GetComponent<Canvas>() != null) break;
            }
            return null;
        }

        private static Component FindActiveDecisionOverlayByStructure()
        {
            try
            {
                UnityEngine.Object[] rawTexts = Resources.FindObjectsOfTypeAll(typeof(TMP_Text));
                List<TMP_Text> yesLabels = new List<TMP_Text>();
                List<TMP_Text> noLabels = new List<TMP_Text>();

                for (int i = 0; i < rawTexts.Length; i++)
                {
                    TMP_Text text = rawTexts[i] as TMP_Text;
                    if (text == null || text.gameObject == null || !text.gameObject.activeInHierarchy) continue;
                    if (_inspectorRoot != null &&
                        (text.gameObject == _inspectorRoot || text.transform.IsChildOf(_inspectorRoot.transform)))
                        continue;

                    string value = text.text ?? string.Empty;
                    if (IsDecisionYesLabel(value)) yesLabels.Add(text);
                    else if (IsDecisionNoLabel(value)) noLabels.Add(text);
                }

                for (int y = 0; y < yesLabels.Count; y++)
                {
                    for (int n = 0; n < noLabels.Count; n++)
                    {
                        Transform root = FindDecisionOverlayRootForLabels(
                            yesLabels[y].transform, noLabels[n].transform);
                        if (root == null) continue;

                        Debug.Log("[ItemIntelligence] Structural decision overlay detected before starmap: root=" +
                            (root.gameObject == null ? "?" : root.gameObject.name) +
                            ", yes=" + (yesLabels[y].text ?? string.Empty) +
                            ", no=" + (noLabels[n].text ?? string.Empty) + ".");
                        return root;
                    }
                }


                // Legacy Unity UI fallback. Keep it separate from the TMP scan to avoid
                // allocating wrapper objects on every navigation attempt.
                UnityEngine.Object[] rawLegacyTexts = Resources.FindObjectsOfTypeAll(typeof(UnityEngine.UI.Text));
                List<UnityEngine.UI.Text> legacyYes = new List<UnityEngine.UI.Text>();
                List<UnityEngine.UI.Text> legacyNo = new List<UnityEngine.UI.Text>();
                for (int i = 0; i < rawLegacyTexts.Length; i++)
                {
                    UnityEngine.UI.Text text = rawLegacyTexts[i] as UnityEngine.UI.Text;
                    if (text == null || text.gameObject == null || !text.gameObject.activeInHierarchy) continue;
                    if (_inspectorRoot != null &&
                        (text.gameObject == _inspectorRoot || text.transform.IsChildOf(_inspectorRoot.transform)))
                        continue;
                    string value = text.text ?? string.Empty;
                    if (IsDecisionYesLabel(value)) legacyYes.Add(text);
                    else if (IsDecisionNoLabel(value)) legacyNo.Add(text);
                }
                for (int y = 0; y < legacyYes.Count; y++)
                {
                    for (int n = 0; n < legacyNo.Count; n++)
                    {
                        Transform root = FindDecisionOverlayRootForLabels(
                            legacyYes[y].transform, legacyNo[n].transform);
                        if (root == null) continue;
                        Debug.Log("[ItemIntelligence] Structural legacy decision overlay detected before starmap: root=" +
                            (root.gameObject == null ? "?" : root.gameObject.name) +
                            ", yes=" + (legacyYes[y].text ?? string.Empty) +
                            ", no=" + (legacyNo[n].text ?? string.Empty) + ".");
                        return root;
                    }
                }

                if (yesLabels.Count > 0 || noLabels.Count > 0 || legacyYes.Count > 0 || legacyNo.Count > 0)
                {
                    Debug.Log("[ItemIntelligence] Decision labels were visible but no safe overlay root was resolved: tmpYes=" +
                        yesLabels.Count + ", tmpNo=" + noLabels.Count + ", legacyYes=" + legacyYes.Count +
                        ", legacyNo=" + legacyNo.Count + ".");
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[ItemIntelligence] Structural decision-overlay probe failed: " + ex.Message);
            }
            return null;
        }









        private static object FindActiveUnityObject(Type type)
        {
            if (type == null) return null;
            try
            {
                UnityEngine.Object[] objects = Resources.FindObjectsOfTypeAll(type);
                for (int i = 0; i < objects.Length; i++)
                {
                    Component component = objects[i] as Component;
                    if (component != null && component.gameObject != null && component.gameObject.activeInHierarchy)
                        return component;
                }
                if (objects.Length > 0) return objects[0];
            }
            catch { }
            return null;
        }
    }
}
