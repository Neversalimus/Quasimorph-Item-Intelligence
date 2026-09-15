using System;
using System.Collections.Generic;
using HarmonyLib;
using TMPro;
using UnityEngine;

namespace ItemIntelligence
{
    public static partial class ModMain
    {
        private static string _mcmRegisteredName = string.Empty;
        private static readonly Dictionary<TMP_FontAsset, TMP_FontAsset> McmMixedFonts =
            new Dictionary<TMP_FontAsset, TMP_FontAsset>();
        private static readonly Dictionary<TMP_FontAsset, TMP_FontAsset> McmOriginalFonts =
            new Dictionary<TMP_FontAsset, TMP_FontAsset>();
        private static TMP_FontAsset _mcmHanFont, _mcmLatinFont;
        private static int _mcmFontRetryFrame;
        private static bool _mcmFontWarningLogged, _mcmFontReadyLogged;

        private static void InstallMcmFontSupport(string registeredName)
        {
            _mcmRegisteredName = registeredName;
            try
            {
                Harmony harmony = new Harmony(HarmonyId + ".McmFonts");
                int count = PatchNamedMethods(harmony, "ModConfigMenu.ModConfigMenu", "BuildModConfig",
                    null, "McmConfigBuiltPostfix");
                count += PatchNamedMethods(harmony, "ModConfigMenu.ModConfigMenu", "CreateButtonsForEveryMod",
                    null, "McmButtonsBuiltPostfix");
                count += PatchNamedMethods(harmony, "ModConfigMenu.GenericHoverTooltip", "OnPointerEnter",
                    "McmHoverFontPrefix", null);
                VerboseLog("[ItemIntelligence][McmFonts] scoped hooks=" + count + "/3.");
                if (count < 3) LogMcmFontWarning("Some MCM font hooks are unavailable in this MCM version.");
            }
            catch (Exception ex) { LogMcmFontWarning(ex.Message); }
        }

        private static void McmConfigBuiltPostfix(object __0, Transform __result)
        {
            try
            {
                if (__result == null || string.IsNullOrEmpty(_mcmRegisteredName) ||
                    !string.Equals(GetMember(__0, "ModName") as string, _mcmRegisteredName,
                        StringComparison.Ordinal)) return;
                SetMcmFontScope(__result, true);
                // TMP clones the template when opening the options. Mark the template
                // itself so the clone keeps ownership even if its parent changes.
                TMP_Dropdown[] dropdowns = __result.GetComponentsInChildren<TMP_Dropdown>(true);
                for (int i = 0; i < dropdowns.Length; i++)
                    if (dropdowns[i].template != null) SetMcmFontScope(dropdowns[i].template, true);
            }
            catch (Exception ex) { LogMcmFontWarning(ex.Message); }
        }

        private static void McmButtonsBuiltPostfix(object __instance)
        {
            try
            {
                Transform list = GetMember(__instance, "ModListRoot") as Transform;
                if (list == null || string.IsNullOrEmpty(_mcmRegisteredName)) return;
                string name = "[" + _mcmRegisteredName.Replace(" ", string.Empty) + "]";
                for (int i = 0; i < list.childCount; i++)
                {
                    Transform child = list.GetChild(i);
                    if (string.Equals(child.name, name, StringComparison.Ordinal))
                        SetMcmFontScope(child, true);
                }
            }
            catch (Exception ex) { LogMcmFontWarning(ex.Message); }
        }

        private static void McmHoverFontPrefix(Component __instance)
        {
            try
            {
                if (__instance == null) return;
                Component tooltip = GetMember(__instance, "_tooltip") as Component;
                if (tooltip == null) return;
                QiiMcmFontScope owner = __instance.GetComponentInParent<QiiMcmFontScope>();
                // MCM shares this tooltip among mods. A hover outside our marked root
                // explicitly releases our font scope before MCM replaces the text.
                SetMcmFontScope(tooltip.transform, owner != null && owner.enabled);
            }
            catch (Exception ex) { LogMcmFontWarning(ex.Message); }
        }

        private static void SetMcmFontScope(Transform root, bool owned)
        {
            if (root == null) return;
            QiiMcmFontScope scope = root.GetComponent<QiiMcmFontScope>();
            if (scope == null && owned) scope = root.gameObject.AddComponent<QiiMcmFontScope>();
            if (scope == null) return;
            scope.enabled = owned;
            if (owned) scope.RefreshTexts();
            else scope.RestoreFonts();
        }

        private static TMP_FontAsset GetMcmMixedFont(TMP_FontAsset original)
        {
            if (original == null) return null;
            if (McmOriginalFonts.ContainsKey(original)) return original;
            TMP_FontAsset mixed;
            if (McmMixedFonts.TryGetValue(original, out mixed) && mixed != null) return mixed;
            if (_mcmHanFont == null || _mcmLatinFont == null)
            {
                if (Time.frameCount < _mcmFontRetryFrame) return null;
                _mcmFontRetryFrame = Time.frameCount + 120;
                _mcmHanFont = FindGameLanguageFont("ChineseSimplified");
                _mcmLatinFont = FindGameLanguageFont("EnglishUS");
                if (_mcmHanFont == null || _mcmLatinFont == null) return null;
            }
            // Preserve MCM's primary font and metrics. Add both scripts only on a
            // private asset; no game font or global TMP fallback list is modified.
            mixed = UnityEngine.Object.Instantiate(original);
            mixed.name = "QII_MCM_" + original.name;
            mixed.hideFlags = HideFlags.HideAndDontSave;
            mixed.fallbackFontAssetTable = original.fallbackFontAssetTable == null
                ? new List<TMP_FontAsset>() : new List<TMP_FontAsset>(original.fallbackFontAssetTable);
            if (_mcmHanFont != original && !mixed.fallbackFontAssetTable.Contains(_mcmHanFont))
                mixed.fallbackFontAssetTable.Add(_mcmHanFont);
            if (_mcmLatinFont != original && !mixed.fallbackFontAssetTable.Contains(_mcmLatinFont))
                mixed.fallbackFontAssetTable.Add(_mcmLatinFont);
            McmMixedFonts[original] = mixed;
            McmOriginalFonts[mixed] = original;
            return mixed;
        }

        internal static void ApplyMcmFonts(TMP_Text[] texts)
        {
            if (texts == null) return;
            try
            {
                for (int i = 0; i < texts.Length; i++)
                {
                    TMP_Text text = texts[i];
                    if (text == null) continue;
                    TMP_FontAsset font = GetMcmMixedFont(text.font);
                    if (font == null || text.font == font) continue;
                    text.font = font;
                    if (!_mcmFontReadyLogged)
                    {
                        _mcmFontReadyLogged = true;
                        VerboseLog("[ItemIntelligence][McmFonts] private fallback ready; Han=" +
                            _mcmHanFont.name + "; Latin=" + _mcmLatinFont.name + ".");
                    }
                }
            }
            catch (Exception ex) { LogMcmFontWarning(ex.Message); }
        }

        internal static void RestoreMcmFonts(TMP_Text[] texts)
        {
            if (texts == null) return;
            for (int i = 0; i < texts.Length; i++)
            {
                TMP_Text text = texts[i];
                TMP_FontAsset original;
                if (text != null && text.font != null && McmOriginalFonts.TryGetValue(text.font, out original))
                    text.font = original;
            }
        }

        private static void LogMcmFontWarning(string message)
        {
            if (_mcmFontWarningLogged) return;
            _mcmFontWarningLogged = true;
            Debug.LogWarning("[ItemIntelligence][McmFonts] " + message);
        }
    }

    internal sealed class QiiMcmFontScope : MonoBehaviour
    {
        private TMP_Text[] _texts;
        private bool _capturePending;

        internal void RefreshTexts()
        {
            _texts = GetComponentsInChildren<TMP_Text>(true);
            ModMain.ApplyMcmFonts(_texts);
        }

        internal void RestoreFonts() { ModMain.RestoreMcmFonts(_texts); }

        private void OnEnable()
        {
            _capturePending = true;
            RefreshTexts();
        }

        private void LateUpdate()
        {
            // Dropdown items are added after template activation. Capture that completed
            // tree once; later frames only inspect cached labels, with no scene search.
            if (_capturePending) { _capturePending = false; RefreshTexts(); }
            else ModMain.ApplyMcmFonts(_texts);
        }

        private void OnDisable() { RestoreFonts(); }
    }
}
