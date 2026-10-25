using System;
using System.Collections.Generic;

namespace ItemIntelligence
{
    internal sealed class GameLocalization
    {
        private readonly Func<string> resolveLanguage;
        private readonly Func<string, string> resolveKey, normalize;
        private readonly Func<int> frameCount;
        private readonly Action<string> log;
        internal int KeyCount { get { return LocalizationCache.Count; } }
        internal int ItemCount { get { return LocalizedItemDisplayCache.Count; } }
        internal int PerkCount { get { return LocalizedMagnumPerkDisplayCache.Count; } }

        internal GameLocalization(Func<string> resolveLanguage, Func<string, string> resolveKey,
            Func<int> frameCount, Func<string, string> normalize, Action<string> log)
        {
            this.resolveLanguage = resolveLanguage; this.resolveKey = resolveKey;
            this.frameCount = frameCount; this.normalize = normalize; this.log = log;
        }

        internal void ClearResults()
        {
            LocalizationCache.Clear();
            LocalizedItemDisplayCache.Clear();
            LocalizedMagnumPerkDisplayCache.Clear();
        }

        internal void Reset()
        {
            ClearResults();
            _localizationCacheLanguage = string.Empty;
            _cachedGameLanguageSignature = string.Empty;
            _cachedGameLanguageFrame = -100000;
        }

        private readonly Dictionary<string, string> LocalizationCache =
            new Dictionary<string, string>(StringComparer.Ordinal);
        // Final entity-display caches avoid rebuilding candidate-key arrays and re-running
        // normalization on every pooled-row redraw. These separate caches hold only
        // the active language; EnsureLocalizationCacheLanguage clears all three.
        private readonly Dictionary<string, string> LocalizedItemDisplayCache =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, string> LocalizedMagnumPerkDisplayCache =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private string _localizationCacheLanguage = string.Empty;
        private string _cachedGameLanguageSignature = string.Empty;
        private int _cachedGameLanguageFrame = -100000;
        private const int GameLanguageRefreshFrames = 240;

        internal string LocalizeItem(string itemId)
        {
            if (string.IsNullOrEmpty(itemId)) return string.Empty;
            EnsureLocalizationCacheLanguage();
            string cachedDisplay;
            if (LocalizedItemDisplayCache.TryGetValue(itemId, out cachedDisplay)) return cachedDisplay;

            string[] keys = new string[] { "item." + itemId + ".name", "items." + itemId + ".name", itemId };
            string resolved = normalize(LocalizeCandidates(keys, itemId));
            LocalizedItemDisplayCache[itemId] = resolved;
            return resolved;
        }

        internal string LocalizeMagnumPerk(string perkId)
        {
            if (string.IsNullOrEmpty(perkId)) return string.Empty;
            EnsureLocalizationCacheLanguage();
            string cachedDisplay;
            if (LocalizedMagnumPerkDisplayCache.TryGetValue(perkId, out cachedDisplay)) return cachedDisplay;

            string[] keys = new string[]
            {
                "mgperk." + perkId + ".name",
                "magnumperk." + perkId + ".name",
                "mgproject." + perkId + ".name",
                "magnumproject." + perkId + ".name",
                "project." + perkId + ".name",
                perkId
            };
            string resolved = normalize(LocalizeCandidates(keys, perkId));
            LocalizedMagnumPerkDisplayCache[perkId] = resolved;
            return resolved;
        }

        internal string LocalizeCandidates(string[] keys, string fallback)
        {
            EnsureLocalizationCacheLanguage();

            if (keys == null || keys.Length == 0)
                return fallback;

            for (int k = 0; k < keys.Length; k++)
            {
                string key = keys[k];
                if (string.IsNullOrEmpty(key)) continue;

                string cached;
                if (LocalizationCache.TryGetValue(key, out cached))
                {
                    if (!string.IsNullOrEmpty(cached)) return cached;
                    continue;
                }

                string value = resolveKey(key);

                if (string.IsNullOrEmpty(value) ||
                    string.Equals(value, key, StringComparison.OrdinalIgnoreCase) ||
                    value.IndexOf("No localization", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    LocalizationCache[key] = string.Empty;
                    continue;
                }

                value = normalize(value);
                LocalizationCache[key] = value;
                return value;
            }

            return normalize(fallback);
        }

        internal string GetLanguageSignature()
        {
            int frame = frameCount();
            if (!string.IsNullOrEmpty(_cachedGameLanguageSignature) &&
                frame >= _cachedGameLanguageFrame &&
                frame - _cachedGameLanguageFrame < GameLanguageRefreshFrames)
                return _cachedGameLanguageSignature;

            string resolved = resolveLanguage();
            if (string.IsNullOrEmpty(resolved)) resolved = "English";
            _cachedGameLanguageSignature = resolved;
            _cachedGameLanguageFrame = frame;
            return resolved;
        }

        internal void EnsureLocalizationCacheLanguage()
        {
            string current = GetLanguageSignature();
            if (string.Equals(current, _localizationCacheLanguage, StringComparison.OrdinalIgnoreCase))
                return;

            _localizationCacheLanguage = current;
            ClearResults();

            log("[ItemIntelligence] Game language: " + current + ".");
        }

    }
}
