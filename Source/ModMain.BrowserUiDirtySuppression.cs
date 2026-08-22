using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ItemIntelligence
{
    /// <summary>
    /// Tiny no-op guards for pooled browser UI. Unity/TMP setters can dirty geometry even when
    /// the effective value did not change; these helpers preserve identical output while avoiding
    /// redundant Canvas/TMP work on warm redraws and tab re-entry.
    /// </summary>
    public static partial class ModMain
    {
        private static void SetBrowserActiveIfChanged(GameObject target, bool active)
        {
            if (target != null && target.activeSelf != active) target.SetActive(active);
        }

        private static void SetBrowserTextIfChanged(TMP_Text target, string value)
        {
            if (target == null) return;
            string next = value ?? string.Empty;
            if (!string.Equals(target.text, next, StringComparison.Ordinal)) target.text = next;
        }

        private static void SetBrowserRectPositionIfChanged(RectTransform target, float x, float y)
        {
            if (target == null) return;
            Vector2 next = new Vector2(x, y);
            if (target.anchoredPosition != next) target.anchoredPosition = next;
        }

        private static void SetBrowserRectSizeIfChanged(RectTransform target, float width, float height)
        {
            if (target == null) return;
            Vector2 next = new Vector2(width, height);
            if (target.sizeDelta != next) target.sizeDelta = next;
        }

        private static void SetBrowserFontSizeIfChanged(TMP_Text target, float value)
        {
            if (target != null && target.fontSize != value) target.fontSize = value;
        }

        private static void SetBrowserFontStyleIfChanged(TMP_Text target, FontStyles value)
        {
            if (target != null && target.fontStyle != value) target.fontStyle = value;
        }

        private static void SetBrowserAutoSizingIfChanged(TMP_Text target, bool value)
        {
            if (target != null && target.enableAutoSizing != value) target.enableAutoSizing = value;
        }

        private static void SetBrowserFontSizeMinIfChanged(TMP_Text target, float value)
        {
            if (target != null && target.fontSizeMin != value) target.fontSizeMin = value;
        }

        private static void SetBrowserFontSizeMaxIfChanged(TMP_Text target, float value)
        {
            if (target != null && target.fontSizeMax != value) target.fontSizeMax = value;
        }

        private static void SetBrowserWordWrappingIfChanged(TMP_Text target, bool value)
        {
#pragma warning disable 0618
            if (target != null && target.enableWordWrapping != value) target.enableWordWrapping = value;
#pragma warning restore 0618
        }

        private static void SetBrowserOverflowIfChanged(TMP_Text target, TextOverflowModes value)
        {
            if (target != null && target.overflowMode != value) target.overflowMode = value;
        }

        private static void SetBrowserInteractableIfChanged(Selectable target, bool value)
        {
            if (target != null && target.interactable != value) target.interactable = value;
        }

        private static void SetBrowserRaycastTargetIfChanged(Graphic target, bool value)
        {
            if (target != null && target.raycastTarget != value) target.raycastTarget = value;
        }

        private static void SetBrowserGraphicColorIfChanged(Graphic target, Color value)
        {
            if (target != null && target.color != value) target.color = value;
        }

        private static void SetBrowserImageSpriteIfChanged(Image target, Sprite value)
        {
            if (target != null && target.sprite != value) target.sprite = value;
        }

        private static void SetBrowserImageEnabledIfChanged(Image target, bool value)
        {
            if (target != null && target.enabled != value) target.enabled = value;
        }

        private static void SetBrowserAlignmentIfChanged(TMP_Text target, TextAlignmentOptions value)
        {
            if (target != null && target.alignment != value) target.alignment = value;
        }

        private static void SetBrowserOutlineEnabledIfChanged(Outline target, bool value)
        {
            if (target != null && target.enabled != value) target.enabled = value;
        }

        private static void SetBrowserOutlineColorIfChanged(Outline target, Color value)
        {
            if (target != null && target.effectColor != value) target.effectColor = value;
        }

        private static void SetBrowserOutlineDistanceIfChanged(Outline target, Vector2 value)
        {
            if (target != null && target.effectDistance != value) target.effectDistance = value;
        }
    }
}
