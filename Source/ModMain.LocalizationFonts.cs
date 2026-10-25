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
        private static readonly UiFontResolver UiFonts = new UiFontResolver(
            FindGameLanguageFont, VerboseLog, message => Debug.LogWarning(message));

        private static TMP_FontAsset ResolveUiFont()
        {
            return UiFonts.Resolve(GetUiLanguageSignature(), GetLanguageSignature(), _inspectorFont, Time.frameCount);
        }

        private static void ResetUiFontSession()
        {
            UiFonts.Reset();
            _browserUiFontSignature = string.Empty;
            _browserUiFont = null;
            _browserUiFontRoot = null;
        }

        private static TMP_FontAsset FindGameLanguageFont(string language)
        {
            Type keeperType = AccessTools.TypeByName("MGSC.LocalizationFontKeeper");
            object keeper = GetLocalizationOwnerInstance(keeperType);
            IEnumerable presets = GetRequiredMember(keeper, "FontPresets") as IEnumerable;
            if (presets == null) return null;
            string preference = NormalizeUiLanguagePreference(language);
            foreach (object preset in presets)
            {
                IEnumerable languages = GetRequiredMember(preset, "AvaialableLangs") as IEnumerable;
                if (languages == null) continue;
                foreach (object candidate in languages)
                {
                    string token = ConvertToStableString(candidate);
                    bool match = preference != "Auto (Game)"
                        ? preference == NormalizeUiLanguagePreference(token)
                        : string.Equals(language, token, StringComparison.OrdinalIgnoreCase);
                    if (match) return GetRequiredMember(preset, "FontAsset") as TMP_FontAsset;
                }
            }
            return null;
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
            string signature = UiFonts.Signature;
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
