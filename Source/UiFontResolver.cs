using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace ItemIntelligence
{
    internal sealed class UiFontResolver
    {
        private readonly Func<string, TMP_FontAsset> findFont;
        private readonly Action<string> log, warn;
        private int _lastFrame;
        internal string Signature { get { return _uiFontSignature; } }
        internal int OwnedFontCount { get { return UiMixedFonts.Count; } }

        internal UiFontResolver(Func<string, TMP_FontAsset> findFont, Action<string> log, Action<string> warn)
        {
            this.findFont = findFont; this.log = log; this.warn = warn;
        }

        // Destroy only clones made by this owner. Game assets are borrowed.
        internal void Reset()
        {
            foreach (TMP_FontAsset font in UiMixedFonts.Values)
                if (font != null) UnityEngine.Object.Destroy(font);
            UiMixedFonts.Clear();
            _uiFontSignature = string.Empty;
            _uiResolvedFont = null;
            _uiFontReady = false;
            _uiFontRetryFrame = _lastFrame = 0;
        }

        private readonly Dictionary<string, TMP_FontAsset> UiMixedFonts =
            new Dictionary<string, TMP_FontAsset>(StringComparer.Ordinal);
        private string _uiFontSignature = string.Empty;
        private TMP_FontAsset _uiResolvedFont;
        private int _uiFontRetryFrame;
        private bool _uiFontReady;

        internal TMP_FontAsset Resolve(string uiLanguage, string gameLanguage, TMP_FontAsset inspectorFont, int frame)
        {
            string signature = uiLanguage + "|" + gameLanguage;
            if (signature == _uiFontSignature &&
                ((_uiFontReady && _uiResolvedFont != null) || frame >= _lastFrame && frame < _uiFontRetryFrame))
                return _uiResolvedFont ?? inspectorFont;
            _lastFrame = frame;
            _uiFontSignature = signature;
            _uiFontRetryFrame = frame + 240;
            _uiFontReady = false;
            try
            {
                TMP_FontAsset ui = findFont(uiLanguage);
                TMP_FontAsset game = findFont(gameLanguage);
                // A temporary fallback must not prevent retrying a late preset.
                // Both scripts are required when mod and game languages differ.
                _uiFontReady = ui != null && game != null;
                if (game == null) game = inspectorFont;
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
                log("[ItemIntelligence] UI font: " + signature + "; asset=" +
                    (_uiResolvedFont == null ? "<pending>" : _uiResolvedFont.name) + ".");
            }
            catch (Exception ex)
            {
                _uiResolvedFont = null;
                _uiFontReady = false;
                warn("[ItemIntelligence] UI font lookup unavailable: " + ex.Message);
            }
            return _uiResolvedFont ?? inspectorFont;
        }

    }
}
