using System;
using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ItemIntelligence
{
    public static partial class ModMain
    {
        // v1.7.37-test2: weapon-mode rows stay compact. The exact modeKey carried by
        // each row owns its hover payload, so two identically named modes can expose
        // different FireModeRecord stats without any label-based lookup.
        private const int WeaponModeTooltipMaxRows = 7;
        private static GameObject _browserWeaponModeTooltipRoot;
        private static RectTransform _browserWeaponModeTooltipRect;
        private static TMP_Text _browserWeaponModeTooltipTitle;
        private static TMP_Text _browserWeaponModeTooltipSubtitle;
        private static readonly TMP_Text[] BrowserWeaponModeTooltipNames = new TMP_Text[WeaponModeTooltipMaxRows];
        private static readonly TMP_Text[] BrowserWeaponModeTooltipValues = new TMP_Text[WeaponModeTooltipMaxRows];

        private static void AttachBrowserWeaponModeTooltipTarget(GameObject row)
        {
            if (row == null) return;
            BrowserWeaponModeTooltipBinding binding = row.GetComponent<BrowserWeaponModeTooltipBinding>();
            if (binding == null) binding = row.AddComponent<BrowserWeaponModeTooltipBinding>();
            binding.enabled = false;
        }

        private static void SetBrowserWeaponModeTooltipTarget(
            GameObject row, string modeKey, string label, bool enabled)
        {
            if (row == null) return;
            BrowserWeaponModeTooltipBinding binding = row.GetComponent<BrowserWeaponModeTooltipBinding>();
            if (binding == null) return;

            bool active = enabled && !string.IsNullOrEmpty(modeKey) && !string.IsNullOrEmpty(label) &&
                ResolveWeaponModeStatsByKey(modeKey) != null;
            if (!active || !string.Equals(binding.ModeKey, modeKey, StringComparison.Ordinal))
                HideBrowserWeaponModeTooltip();

            binding.ModeKey = active ? modeKey : string.Empty;
            binding.Label = active ? label : string.Empty;
            binding.enabled = active;
        }

        internal static void ShowBrowserWeaponModeTooltip(
            string modeKey, string label, RectTransform sourceRow)
        {
            if (!_inspectorOpen || string.IsNullOrEmpty(modeKey) || string.IsNullOrEmpty(label)) return;
            WeaponModeStaticStats stats = ResolveWeaponModeStatsByKey(modeKey);
            if (stats == null) return;

            EnsureBrowserWeaponModeTooltip();
            if (_browserWeaponModeTooltipRoot == null || _browserWeaponModeTooltipRect == null) return;

            List<KeyValuePair<string, string>> rows = BuildWeaponModeTooltipRows(modeKey, stats);
            _browserWeaponModeTooltipTitle.text = NormalizeModUiText(label);
            bool melee = IsWeaponModeMelee(modeKey, stats);
            _browserWeaponModeTooltipSubtitle.text = Ui(melee ? "ui.mode_tooltip_attack" : "ui.mode_tooltip_fire");

            int visibleRows = Math.Min(rows.Count, WeaponModeTooltipMaxRows);
            for (int i = 0; i < WeaponModeTooltipMaxRows; i++)
            {
                bool show = i < visibleRows;
                if (BrowserWeaponModeTooltipNames[i] != null)
                {
                    BrowserWeaponModeTooltipNames[i].gameObject.SetActive(show);
                    BrowserWeaponModeTooltipNames[i].text = show ? NormalizeModUiText(rows[i].Key) : string.Empty;
                }
                if (BrowserWeaponModeTooltipValues[i] != null)
                {
                    BrowserWeaponModeTooltipValues[i].gameObject.SetActive(show);
                    BrowserWeaponModeTooltipValues[i].text = show ? NormalizeModUiText(rows[i].Value) : string.Empty;
                }
            }

            float height = 76f + visibleRows * 32f + 12f;
            _browserWeaponModeTooltipRect.sizeDelta = new Vector2(390f, height);
            float desiredY = sourceRow == null ? -180f : sourceRow.anchoredPosition.y + 18f;
            float minY = -Mathf.Max(120f, 830f - height);
            float y = Mathf.Clamp(desiredY, minY, -92f);
            _browserWeaponModeTooltipRect.anchoredPosition = new Vector2(-398f, y);
            _browserWeaponModeTooltipRoot.transform.SetAsLastSibling();
            _browserWeaponModeTooltipRoot.SetActive(true);
        }

        internal static void HideBrowserWeaponModeTooltip()
        {
            if (_browserWeaponModeTooltipRoot != null)
                _browserWeaponModeTooltipRoot.SetActive(false);
        }

        private static WeaponModeStaticStats ResolveWeaponModeStatsByKey(string modeKey)
        {
            if (string.IsNullOrEmpty(modeKey)) return null;
            WeaponModeStaticStats exact;
            if (WeaponModeStatsByKey.TryGetValue(modeKey, out exact) && exact != null) return exact;

            string rawId;
            if (!WeaponModeRawIdByKey.TryGetValue(modeKey, out rawId) || string.IsNullOrEmpty(rawId)) return null;
            WeaponModeStaticStats fallback;
            return WeaponModeStatsByRawId.TryGetValue(rawId, out fallback) ? fallback : null;
        }

        private static List<KeyValuePair<string, string>> BuildWeaponModeTooltipRows(string modeKey, WeaponModeStaticStats stats)
        {
            List<KeyValuePair<string, string>> rows = new List<KeyValuePair<string, string>>(7);
            if (stats == null) return rows;

            // Match MGSC.TooltipFactory.BuildFiremodeTooltip exactly for the fields it
            // presents. Delay remains indexed for diagnostics only.
            if (stats.WeaponCastsCount > 1)
                rows.Add(new KeyValuePair<string, string>(Ui("ui.mode_rate_of_fire"), stats.WeaponCastsCount.ToString(CultureInfo.InvariantCulture)));
            if (stats.DamageMult.HasValue && !float.IsNaN(stats.DamageMult.Value) &&
                !float.IsInfinity(stats.DamageMult.Value))
                rows.Add(new KeyValuePair<string, string>(Ui("ui.mode_damage_modifier"), FormatWeaponModeMultiplierPercent(stats.DamageMult.Value)));

            int damagePerApMin;
            int damagePerApMax;
            if (TryCalculateWeaponModeDamagePerAp(modeKey, stats, out damagePerApMin, out damagePerApMax))
                rows.Add(new KeyValuePair<string, string>(Ui("ui.mode_damage_per_ap_default"), FormatWeaponModeDamagePerAp(damagePerApMin, damagePerApMax)));

            int criticalDamagePerApMin;
            int criticalDamagePerApMax;
            if (TryCalculateWeaponModeCriticalDamagePerAp(modeKey, stats, out criticalDamagePerApMin, out criticalDamagePerApMax))
                rows.Add(new KeyValuePair<string, string>(Ui("ui.mode_critical_damage_per_ap_default"), FormatWeaponModeDamagePerAp(criticalDamagePerApMin, criticalDamagePerApMax)));
            if (stats.AmmoPerShot > 0)
                rows.Add(new KeyValuePair<string, string>(Ui("ui.mode_ammo_consumption"), stats.AmmoPerShot.ToString(CultureInfo.InvariantCulture)));
            if (stats.Accuracy.HasValue && !float.IsNaN(stats.Accuracy.Value) &&
                !float.IsInfinity(stats.Accuracy.Value) && Math.Abs(stats.Accuracy.Value) > 0.0001f)
                rows.Add(new KeyValuePair<string, string>(Ui("ui.mode_accuracy"), FormatSignedModePercent(stats.Accuracy.Value)));

            float scatter;
            if (TryCalculateVanillaFiremodeScatter(modeKey, stats, out scatter))
                rows.Add(new KeyValuePair<string, string>(Ui("ui.mode_scatter"), FormatWeaponModeScatter(scatter)));
            return rows;
        }

        private static string FormatWeaponModeMultiplierPercent(float value)
        {
            float percent = value * 100f;
            return FormatWeaponModeLocalizedNumber(percent, 1) + "%";
        }

        private static string FormatSignedModePercent(float value)
        {
            float percent = value * 100f;
            string formatted = FormatWeaponModeLocalizedNumber(percent, 1);
            return percent > 0.0001f ? "+" + formatted + "%" : formatted + "%";
        }

        private static string FormatWeaponModeLocalizedNumber(float value, int maxDecimals)
        {
            string format = maxDecimals <= 0 ? "0" : "0." + new string('#', maxDecimals);
            CultureInfo culture = IsRussian() ? CultureInfo.GetCultureInfo("ru-RU") : CultureInfo.InvariantCulture;
            return value.ToString(format, culture);
        }

        private static void EnsureBrowserWeaponModeTooltip()
        {
            if (_browserWeaponModeTooltipRoot != null || _inspectorRoot == null) return;

            GameObject root = new GameObject("WeaponModeHoverTooltip");
            root.transform.SetParent(_inspectorRoot.transform, false);
            RectTransform rt = root.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.sizeDelta = new Vector2(390f, 220f);
            Image bg = root.AddComponent<Image>();
            bg.color = new Color(0.005f, 0.017f, 0.014f, 0.985f);
            bg.raycastTarget = false;
            Outline outline = root.AddComponent<Outline>();
            outline.effectColor = new Color(0.38f, 0.76f, 0.54f, 0.95f);
            outline.effectDistance = new Vector2(2f, -2f);

            _browserWeaponModeTooltipRoot = root;
            _browserWeaponModeTooltipRect = rt;
            _browserWeaponModeTooltipTitle = CreateWeaponModeTooltipText(root.transform, "Title", new Vector2(14f, -7f), new Vector2(362f, 30f), 19f, new Color(0.72f, 0.88f, 0.62f, 1f), TextAlignmentOptions.MidlineLeft);
            _browserWeaponModeTooltipSubtitle = CreateWeaponModeTooltipText(root.transform, "Subtitle", new Vector2(14f, -34f), new Vector2(362f, 24f), 15f, new Color(0.05f, 0.68f, 0.56f, 1f), TextAlignmentOptions.MidlineLeft);

            GameObject rule = new GameObject("Rule");
            rule.transform.SetParent(root.transform, false);
            RectTransform ruleRt = rule.AddComponent<RectTransform>();
            ruleRt.anchorMin = new Vector2(0f, 1f);
            ruleRt.anchorMax = new Vector2(0f, 1f);
            ruleRt.pivot = new Vector2(0f, 1f);
            ruleRt.anchoredPosition = new Vector2(4f, -62f);
            ruleRt.sizeDelta = new Vector2(382f, 2f);
            Image ruleImage = rule.AddComponent<Image>();
            ruleImage.color = new Color(0.75f, 0.88f, 0.58f, 0.90f);
            ruleImage.raycastTarget = false;

            for (int i = 0; i < WeaponModeTooltipMaxRows; i++)
            {
                float y = -70f - i * 32f;
                BrowserWeaponModeTooltipNames[i] = CreateWeaponModeTooltipText(root.transform, "Property_" + i + "_Name", new Vector2(18f, y), new Vector2(250f, 30f), 16f, new Color(0.45f, 0.76f, 0.55f, 1f), TextAlignmentOptions.MidlineLeft);
                BrowserWeaponModeTooltipValues[i] = CreateWeaponModeTooltipText(root.transform, "Property_" + i + "_Value", new Vector2(270f, y), new Vector2(102f, 30f), 16f, new Color(0.92f, 0.91f, 0.72f, 1f), TextAlignmentOptions.MidlineRight);
            }

            root.SetActive(false);
        }

        private static TMP_Text CreateWeaponModeTooltipText(
            Transform parent, string name, Vector2 position, Vector2 size,
            float fontSize, Color color, TextAlignmentOptions alignment)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            RectTransform rt = go.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = position;
            rt.sizeDelta = size;
            TextMeshProUGUI text = go.AddComponent<TextMeshProUGUI>();
            if (_inspectorFont != null) text.font = _inspectorFont;
            text.fontSize = fontSize;
            text.color = color;
            text.alignment = alignment;
            text.enableWordWrapping = false;
            text.overflowMode = TextOverflowModes.Truncate;
            text.raycastTarget = false;
            return text;
        }
    }

    public sealed class BrowserWeaponModeTooltipBinding : MonoBehaviour,
        IPointerEnterHandler, IPointerExitHandler
    {
        public string ModeKey = string.Empty;
        public string Label = string.Empty;

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (!enabled || string.IsNullOrEmpty(ModeKey)) return;
            ModMain.ShowBrowserWeaponModeTooltip(ModeKey, Label, transform as RectTransform);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            ModMain.HideBrowserWeaponModeTooltip();
        }

        private void OnDisable()
        {
            ModMain.HideBrowserWeaponModeTooltip();
        }
    }
}
