using System;
using System.Collections;
using System.Collections.Generic;
using HarmonyLib;
using TMPro;
using UnityEngine;

namespace ItemIntelligence
{
    public static partial class ModMain
    {
        private static readonly Dictionary<string, TMP_FontAsset> UiMixedFonts =
            new Dictionary<string, TMP_FontAsset>(StringComparer.Ordinal);
        private static string _uiFontSignature = string.Empty;
        private static TMP_FontAsset _uiResolvedFont;
        private static int _uiFontRetryFrame;

        private static TMP_FontAsset FindGameLanguageFont(string language)
        {
            Type keeperType = AccessTools.TypeByName("MGSC.LocalizationFontKeeper");
            object keeper = GetLocalizationOwnerInstance(keeperType);
            IEnumerable presets = GetMember(keeper, "FontPresets") as IEnumerable;
            if (presets == null) return null;
            string preference = NormalizeUiLanguagePreference(language);
            foreach (object preset in presets)
            {
                IEnumerable languages = GetMember(preset, "AvaialableLangs") as IEnumerable;
                if (languages == null) continue;
                foreach (object candidate in languages)
                {
                    string token = ConvertToStableString(candidate);
                    bool match = preference != "Auto (Game)"
                        ? preference == NormalizeUiLanguagePreference(token)
                        : string.Equals(language, token, StringComparison.OrdinalIgnoreCase);
                    if (match) return GetMember(preset, "FontAsset") as TMP_FontAsset;
                }
            }
            return null;
        }

        private static TMP_FontAsset ResolveUiFont()
        {
            string signature = GetUiLanguageSignature() + "|" + GetLanguageSignature();
            if (signature == _uiFontSignature &&
                (_uiResolvedFont != null || Time.frameCount < _uiFontRetryFrame))
                return _uiResolvedFont ?? _inspectorFont;
            _uiFontSignature = signature;
            _uiFontRetryFrame = Time.frameCount + 240;
            try
            {
                TMP_FontAsset ui = FindGameLanguageFont(GetUiLanguageSignature());
                TMP_FontAsset game = FindGameLanguageFont(GetLanguageSignature()) ?? _inspectorFont;
                if (ui == null) ui = game;
                _uiResolvedFont = ui;
                if (ui != null && game != null && ui != game)
                {
                    string key = ui.GetInstanceID() + "|" + game.GetInstanceID();
                    TMP_FontAsset mixed;
                    if (!UiMixedFonts.TryGetValue(key, out mixed) || mixed == null)
                    {
                        // Only this private asset gets a new fallback list. Never modify
                        // a vanilla font, its material, or TMP's global fallback settings.
                        mixed = UnityEngine.Object.Instantiate(ui);
                        mixed.name = "QII_" + ui.name + "_" + game.name;
                        mixed.hideFlags = HideFlags.HideAndDontSave;
                        mixed.fallbackFontAssetTable = ui.fallbackFontAssetTable == null
                            ? new List<TMP_FontAsset>()
                            : new List<TMP_FontAsset>(ui.fallbackFontAssetTable);
                        if (!mixed.fallbackFontAssetTable.Contains(game))
                            mixed.fallbackFontAssetTable.Add(game);
                        UiMixedFonts[key] = mixed;
                    }
                    _uiResolvedFont = mixed;
                }
                Debug.Log("[ItemIntelligence] UI font: " + signature + "; asset=" +
                    (_uiResolvedFont == null ? "<pending>" : _uiResolvedFont.name) + ".");
            }
            catch (Exception ex)
            {
                _uiResolvedFont = null;
                Debug.LogWarning("[ItemIntelligence] UI font lookup unavailable: " + ex.Message);
            }
            return _uiResolvedFont ?? _inspectorFont;
        }

        private static void ApplyUiFont(TMP_Text text)
        {
            if (text == null) return;
            TMP_FontAsset font = ResolveUiFont();
            if (font != null && text.font != font) text.font = font;
        }

        private static string _browserUiFontSignature = string.Empty;
        private static TMP_FontAsset _browserUiFont;
        private static GameObject _browserUiFontRoot;

        private static void RefreshBrowserLanguageFonts()
        {
            TMP_FontAsset font = ResolveUiFont();
            string signature = _uiFontSignature;
            if (font == null || (_browserUiFont == font && _browserUiFontRoot == _inspectorRoot &&
                _browserUiFontSignature == signature)) return;
            _browserUiFont = font;
            _browserUiFontRoot = _inspectorRoot;
            _browserUiFontSignature = signature;
            if (_inspectorRoot != null)
            {
                TMP_Text[] texts = _inspectorRoot.GetComponentsInChildren<TMP_Text>(true);
                for (int i = 0; i < texts.Length; i++) ApplyUiFont(texts[i]);
            }
            ApplyUiFont(_hoverHintText);
            if (_browserWeaponModeTooltipRoot != null)
            {
                TMP_Text[] texts = _browserWeaponModeTooltipRoot.GetComponentsInChildren<TMP_Text>(true);
                for (int i = 0; i < texts.Length; i++) ApplyUiFont(texts[i]);
            }
            InvalidateBrowserRowRenderCache();
        }
    }
}
