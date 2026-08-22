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
