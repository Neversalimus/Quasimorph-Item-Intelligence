using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Runtime.InteropServices;
using System.Threading;
using MGSC;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ItemIntelligence
{
    public static partial class ModMain
    {
        private static readonly Dictionary<string, string> LocalizationCache =
            new Dictionary<string, string>(StringComparer.Ordinal);
        // Final entity-display caches avoid rebuilding candidate-key arrays and re-running
        // normalization on every pooled-row redraw. Keys include the active language.
        private static readonly Dictionary<string, string> LocalizedItemDisplayCache =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, string> LocalizedMagnumPerkDisplayCache =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private static MethodInfo _localizationGetMethod;
        private static ParameterInfo[] _localizationGetParameters;
        private static string _localizationCacheLanguage = string.Empty;
        private static Type _localizationManagerType;
        private static bool _localizationManagerTypeResolved;

        // QII-authored UI text is key-based; vanilla entity names still come from
        // Quasimorph's localization service.
        private static readonly Dictionary<string, string> ExternalUiTranslations =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, string> EnglishUiFallback =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private static readonly HashSet<string> MissingUiTranslationKeys =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, string> ResolvedUiTextCache =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private static string _externalUiTranslationLanguage = string.Empty;
        private static string _externalUiTranslationFile = string.Empty;
        private static bool _externalUiEnglishLoaded;
        private static string _cachedGameLanguageSignature = string.Empty;
        private static int _cachedGameLanguageFrame = -100000;
        private const int GameLanguageRefreshFrames = 240;

        private static string LocalizeItem(string itemId)
        {
            if (string.IsNullOrEmpty(itemId)) return string.Empty;
            EnsureLocalizationCacheLanguage();
            string displayKey = (_localizationCacheLanguage ?? string.Empty) + "|item|" + itemId;
            string cachedDisplay;
            if (LocalizedItemDisplayCache.TryGetValue(displayKey, out cachedDisplay)) return cachedDisplay;

            string[] keys = new string[] { "item." + itemId + ".name", "items." + itemId + ".name", itemId };
            string resolved = NormalizeGameText(LocalizeCandidates(keys, itemId));
            LocalizedItemDisplayCache[displayKey] = resolved;
            return resolved;
        }

        private static string LocalizeMagnumPerk(string perkId)
        {
            if (string.IsNullOrEmpty(perkId)) return string.Empty;
            EnsureLocalizationCacheLanguage();
            string displayKey = (_localizationCacheLanguage ?? string.Empty) + "|magnum|" + perkId;
            string cachedDisplay;
            if (LocalizedMagnumPerkDisplayCache.TryGetValue(displayKey, out cachedDisplay)) return cachedDisplay;

            string[] keys = new string[]
            {
                "mgperk." + perkId + ".name",
                "magnumperk." + perkId + ".name",
                "mgproject." + perkId + ".name",
                "magnumproject." + perkId + ".name",
                "project." + perkId + ".name",
                perkId
            };
            string resolved = NormalizeGameText(LocalizeCandidates(keys, perkId));
            LocalizedMagnumPerkDisplayCache[displayKey] = resolved;
            return resolved;
        }

        private static string LocalizeCandidates(string[] keys, string fallback)
        {
            EnsureLocalizationMethod();

            if (keys == null || keys.Length == 0 || _localizationGetMethod == null)
                return fallback;

            for (int k = 0; k < keys.Length; k++)
            {
                string key = keys[k];
                if (string.IsNullOrEmpty(key)) continue;

                string cacheKey = _localizationCacheLanguage + "|" + key;
                string cached;
                if (LocalizationCache.TryGetValue(cacheKey, out cached))
                {
                    if (!string.IsNullOrEmpty(cached)) return cached;
                    continue;
                }

                string value = string.Empty;
                try
                {
                    object[] args = new object[_localizationGetParameters.Length];
                    args[0] = key;
                    bool valid = true;
                    for (int a = 1; a < args.Length; a++)
                    {
                        ParameterInfo p = _localizationGetParameters[a];
                        if (p.HasDefaultValue) args[a] = p.DefaultValue;
                        else if (!p.ParameterType.IsValueType) args[a] = null;
                        else { valid = false; break; }
                    }

                    if (valid)
                        value = _localizationGetMethod.Invoke(null, args) as string;
                }
                catch { value = string.Empty; }

                if (string.IsNullOrEmpty(value) ||
                    string.Equals(value, key, StringComparison.OrdinalIgnoreCase) ||
                    value.IndexOf("No localization", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    LocalizationCache[cacheKey] = string.Empty;
                    continue;
                }

                LocalizationCache[cacheKey] = value;
                return NormalizeGameText(value);
            }

            return NormalizeGameText(fallback);
        }

        private static void EnsureLocalizationMethod()
        {
            if (_localizationGetMethod != null) return;
            try
            {
                Type loc = AccessTools.TypeByName("MGSC.Localization");
                if (loc == null) return;

                MethodInfo[] methods = loc.GetMethods(StaticFlags);
                for (int i = 0; i < methods.Length; i++)
                {
                    MethodInfo method = methods[i];
                    if (!string.Equals(method.Name, "Get", StringComparison.Ordinal) || method.ReturnType != typeof(string)) continue;
                    ParameterInfo[] p = method.GetParameters();
                    if (p.Length < 1 || p[0].ParameterType != typeof(string)) continue;

                    bool supported = true;
                    for (int a = 1; a < p.Length; a++)
                    {
                        if (!p[a].HasDefaultValue && p[a].ParameterType.IsValueType)
                        {
                            supported = false;
                            break;
                        }
                    }
                    if (!supported) continue;

                    _localizationGetMethod = method;
                    _localizationGetParameters = p;
                    return;
                }
            }
            catch { }
        }

        private static string InvokeLocalizationRaw(string key)
        {
            if (string.IsNullOrEmpty(key)) return string.Empty;

            EnsureLocalizationMethod();
            if (_localizationGetMethod == null || _localizationGetParameters == null)
                return string.Empty;

            try
            {
                object[] args = new object[_localizationGetParameters.Length];
                args[0] = key;

                for (int i = 1; i < args.Length; i++)
                {
                    ParameterInfo parameter = _localizationGetParameters[i];

                    if (parameter.HasDefaultValue)
                    {
                        args[i] = parameter.DefaultValue;
                    }
                    else if (!parameter.ParameterType.IsValueType)
                    {
                        args[i] = null;
                    }
                    else
                    {
                        return string.Empty;
                    }
                }

                string value = _localizationGetMethod.Invoke(null, args) as string;
                if (string.IsNullOrEmpty(value)) return string.Empty;
                if (string.Equals(value, key, StringComparison.OrdinalIgnoreCase)) return string.Empty;
                if (value.IndexOf("No localization", StringComparison.OrdinalIgnoreCase) >= 0) return string.Empty;

                return NormalizeGameText(value);
            }
            catch
            {
                return string.Empty;
            }
        }

        private static string ProbeVanillaLanguage()
        {
            // These are stable vanilla item IDs already present in Data.Items. We do not
            // compare against translated words; we only ask the game's own Localization.Get
            // what it currently renders and check the script of that returned value.
            string[] keys = new string[]
            {
                "item.geoscanner_device.name",
                "items.geoscanner_device.name",
                "geoscanner_device",
                "item.rags.name",
                "items.rags.name",
                "rags",
                "item.battery_basic_ammo.name",
                "items.battery_basic_ammo.name",
                "battery_basic_ammo"
            };

            bool foundLocalizedValue = false;

            for (int i = 0; i < keys.Length; i++)
            {
                string value = InvokeLocalizationRaw(keys[i]);
                if (string.IsNullOrEmpty(value)) continue;

                foundLocalizedValue = true;

                if (ContainsCyrillic(value))
                    return "Russian";
            }

            // Item Intelligence currently ships RU/EN UI text. If vanilla localization
            // returned a valid non-Cyrillic translation, use the English UI branch.
            if (foundLocalizedValue)
                return "English";

            return string.Empty;
        }

        private static Type ResolveLocalizationManagerType()
        {
            if (_localizationManagerTypeResolved)
                return _localizationManagerType;

            _localizationManagerTypeResolved = true;

            try
            {
                _localizationManagerType = AccessTools.TypeByName("MGSC.LocalizationManager");
                if (_localizationManagerType != null) return _localizationManagerType;

                _localizationManagerType = AccessTools.TypeByName("LocalizationManager");
                if (_localizationManagerType != null) return _localizationManagerType;

                Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
                for (int a = 0; a < assemblies.Length; a++)
                {
                    Type[] types;
                    try { types = assemblies[a].GetTypes(); }
                    catch { continue; }

                    for (int i = 0; i < types.Length; i++)
                    {
                        Type type = types[i];
                        if (type == null) continue;
                        if (!string.Equals(type.Name, "LocalizationManager", StringComparison.Ordinal)) continue;

                        _localizationManagerType = type;
                        return _localizationManagerType;
                    }
                }
            }
            catch { }

            return null;
        }

        private static string ReadLanguageFromOwner(Type owner)
        {
            if (owner == null) return string.Empty;

            string[] names = new string[]
            {
                "CurrentLang",
                "currentLang",
                "CurrentLanguage",
                "currentLanguage",
                "Language",
                "language"
            };

            for (int i = 0; i < names.Length; i++)
            {
                object current = GetStaticMember(owner, names[i]);
                string language = ConvertToStableString(current);
                if (!string.IsNullOrEmpty(language)) return language;
            }

            object instance = FirstNonNull(
                GetStaticMember(owner, "Instance"),
                GetStaticMember(owner, "instance"),
                GetStaticMember(owner, "Current"),
                GetStaticMember(owner, "current"));

            if (instance != null)
            {
                for (int i = 0; i < names.Length; i++)
                {
                    object current = GetMember(instance, names[i]);
                    string language = ConvertToStableString(current);
                    if (!string.IsNullOrEmpty(language)) return language;
                }
            }

            return string.Empty;
        }

        private static string ResolveLanguageSignatureUncached()
        {
            try
            {
                // Exact selected language metadata is needed for community files such
                // as Japanese/Korean/Chinese. This is a reflective lookup, so it must
                // never run for every Ui(key) call or every rendered browser row.
                Type manager = ResolveLocalizationManagerType();
                string language = ReadLanguageFromOwner(manager);
                if (!string.IsNullOrEmpty(language)) return language;

                Type localization = AccessTools.TypeByName("MGSC.Localization");
                language = ReadLanguageFromOwner(localization);
                if (!string.IsNullOrEmpty(language)) return language;

                string probed = ProbeVanillaLanguage();
                if (!string.IsNullOrEmpty(probed)) return probed;
            }
            catch { }

            return "English";
        }

        private static string GetLanguageSignature()
        {
            int frame = Time.frameCount;
            if (!string.IsNullOrEmpty(_cachedGameLanguageSignature) &&
                frame >= _cachedGameLanguageFrame &&
                frame - _cachedGameLanguageFrame < GameLanguageRefreshFrames)
                return _cachedGameLanguageSignature;

            string resolved = ResolveLanguageSignatureUncached();
            if (string.IsNullOrEmpty(resolved)) resolved = "English";
            _cachedGameLanguageSignature = resolved;
            _cachedGameLanguageFrame = frame;
            return resolved;
        }

        private static void EnsureLocalizationCacheLanguage()
        {
            string current = GetLanguageSignature();
            if (string.Equals(current, _localizationCacheLanguage, StringComparison.OrdinalIgnoreCase))
                return;

            _localizationCacheLanguage = current;
            LocalizationCache.Clear();
            LocalizedItemDisplayCache.Clear();
            LocalizedMagnumPerkDisplayCache.Clear();

            Debug.Log("[ItemIntelligence] Game language resolved from vanilla localization: " +
                (string.IsNullOrEmpty(current) ? "<unknown>" : current) +
                "; browser=" + (Ui("ui.english")) + ".");
        }

        private static string LocalizeKind(string kind, bool ru)
        {
            if (string.Equals(kind, "Production", StringComparison.OrdinalIgnoreCase))
                return Ui("ui.conveyor");
            if (string.Equals(kind, "Workbench", StringComparison.OrdinalIgnoreCase))
                return Ui("ui.mission_workbench");
            return kind;
        }

        private static string NormalizeModLanguageToken(string value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            StringBuilder result = new StringBuilder(value.Length);
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                if (char.IsLetterOrDigit(c)) result.Append(char.ToLowerInvariant(c));
            }
            return result.ToString();
        }

        private static bool ExternalLanguageMatches(string gameLanguage, string declaredLanguages)
        {
            if (string.IsNullOrEmpty(gameLanguage) || string.IsNullOrEmpty(declaredLanguages)) return false;
            string[] tokens = declaredLanguages.Split(new char[] { ';', ',', '|' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < tokens.Length; i++)
            {
                if (LanguageAliasMatches(gameLanguage, tokens[i]))
                    return true;
            }
            return false;
        }

        private static bool LanguageAliasMatches(string language, string alias)
        {
            if (string.IsNullOrWhiteSpace(language) || string.IsNullOrWhiteSpace(alias)) return false;

            string normalizedLanguage = NormalizeModLanguageToken(language);
            string normalizedAlias = NormalizeModLanguageToken(alias);
            if (string.IsNullOrEmpty(normalizedLanguage) || string.IsNullOrEmpty(normalizedAlias)) return false;
            if (string.Equals(normalizedLanguage, normalizedAlias, StringComparison.OrdinalIgnoreCase)) return true;

            // Accept metadata such as "ru-RU" or "Russian (ru)" only at real token
            // boundaries. This preserves locale aliases without treating Belarusian,
            // Ukrainian or another Cyrillic language as Russian merely because its
            // full display name happens to contain the letters "ru".
            string candidate = language.Trim();
            string expected = alias.Trim();
            int start = 0;
            while (start < candidate.Length)
            {
                int index = candidate.IndexOf(expected, start, StringComparison.OrdinalIgnoreCase);
                if (index < 0) break;

                int end = index + expected.Length;
                bool leftBoundary = index == 0 || !char.IsLetterOrDigit(candidate[index - 1]);
                bool rightBoundary = end >= candidate.Length || !char.IsLetterOrDigit(candidate[end]);
                if (leftBoundary && rightBoundary) return true;

                start = index + 1;
            }

            return false;
        }

        private static void LoadUiLanguageFile(string path, Dictionary<string, string> target)
        {
            if (string.IsNullOrEmpty(path) || target == null || !File.Exists(path)) return;
            string[] lines = ReadUtf8LinesStrict(path);
            for (int i = 0; i < lines.Length; i++)
            {
                string raw = lines[i] ?? string.Empty;
                string trimmed = raw.TrimStart();
                if (string.IsNullOrWhiteSpace(raw) || trimmed.StartsWith("#") || trimmed.StartsWith("@")) continue;
                int tab = raw.IndexOf('\t');
                if (tab <= 0)
                {
                    RecordLocalizationMalformedLine(path, i + 1);
                    continue;
                }
                string key = raw.Substring(0, tab).Trim();
                // Whitespace around localization values is authored UI content. Several
                // keys deliberately provide a leading or trailing separator for runtime
                // composition (for example "PAGE " + number). ReadAllLines has already
                // removed the newline, so keep the value bytes exactly as written.
                string value = raw.Substring(tab + 1);
                if (key.Length == 0)
                {
                    RecordLocalizationMalformedLine(path, i + 1);
                    continue;
                }
                if (target.ContainsKey(key))
                    RecordLocalizationDuplicateKey(path, key);
                target[key] = value;
            }
        }

        private static string ReadUiLanguageDeclaration(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return string.Empty;
            try
            {
                string[] lines = ReadUtf8LinesStrict(path);
                for (int i = 0; i < lines.Length; i++)
                {
                    string line = (lines[i] ?? string.Empty).Trim();
                    if (line.StartsWith("@language=", StringComparison.OrdinalIgnoreCase))
                        return line.Substring(10).Trim();
                }
            }
            catch { }
            return string.Empty;
        }

        private static bool ReadUiForceDeclaration(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return false;
            try
            {
                string[] lines = ReadUtf8LinesStrict(path);
                for (int i = 0; i < lines.Length; i++)
                {
                    string line = (lines[i] ?? string.Empty).Trim();
                    if (!line.StartsWith("@force=", StringComparison.OrdinalIgnoreCase)) continue;
                    string value = line.Substring(7).Trim();
                    return string.Equals(value, "true", StringComparison.OrdinalIgnoreCase) || value == "1" ||
                        string.Equals(value, "yes", StringComparison.OrdinalIgnoreCase);
                }
            }
            catch { }
            return false;
        }

        private static void EnsureExternalUiTranslations()
        {
            string language = GetLanguageSignature();
            if (string.Equals(language, _externalUiTranslationLanguage, StringComparison.OrdinalIgnoreCase) &&
                _externalUiEnglishLoaded) return;

            _externalUiTranslationLanguage = language ?? string.Empty;
            _externalUiTranslationFile = string.Empty;
            ExternalUiTranslations.Clear();
            MissingUiTranslationKeys.Clear();
            ResolvedUiTextCache.Clear();
            ResetLocalizationHealthForReload();

            try
            {
                string assemblyPath = Assembly.GetExecutingAssembly().Location;
                string root = string.IsNullOrEmpty(assemblyPath) ? string.Empty : Path.GetDirectoryName(assemblyPath);
                if (string.IsNullOrEmpty(root)) return;
                string localizationDir = Path.Combine(root, "Localization");
                if (!Directory.Exists(localizationDir)) return;

                if (!_externalUiEnglishLoaded)
                {
                    EnglishUiFallback.Clear();
                    LoadUiLanguageFile(Path.Combine(localizationDir, "en.lang"), EnglishUiFallback);
                    _externalUiEnglishLoaded = EnglishUiFallback.Count > 0;
                }

                string[] files = Directory.GetFiles(localizationDir, "*.lang");
                string activePath = string.Empty;
                string builtInPath = string.Empty;
                string exactCommunityPath = string.Empty;
                string forcedPath = string.Empty;
                int forcedCount = 0;
                for (int i = 0; i < files.Length; i++)
                {
                    string fileName = Path.GetFileName(files[i]);
                    if (string.Equals(fileName, "TranslationTemplate.lang", StringComparison.OrdinalIgnoreCase)) continue;
                    string declared = ReadUiLanguageDeclaration(files[i]);
                    bool builtIn = string.Equals(fileName, "en.lang", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(fileName, "ru.lang", StringComparison.OrdinalIgnoreCase);
                    if (builtIn)
                    {
                        if (ExternalLanguageMatches(language, declared)) builtInPath = files[i];
                        continue;
                    }

                    if (ExternalLanguageMatches(language, declared))
                        exactCommunityPath = files[i];
                    if (ReadUiForceDeclaration(files[i]))
                    {
                        forcedPath = files[i];
                        forcedCount++;
                    }
                }

                // Community files take precedence over the built-in language file. A
                // single @force=true file can intentionally override an ambiguous
                // metadata result (for example a build that reports every non-RU script
                // as English), which is useful for CJK translation packs and testing.
                if (!string.IsNullOrEmpty(exactCommunityPath)) activePath = exactCommunityPath;
                else if (forcedCount == 1) activePath = forcedPath;
                else activePath = builtInPath;

                if (!string.IsNullOrEmpty(activePath))
                {
                    LoadUiLanguageFile(activePath, ExternalUiTranslations);
                    _externalUiTranslationFile = Path.GetFileName(activePath);
                }

                Debug.Log("[ItemIntelligence] UI localization: language=" + language +
                    ", file=" + (string.IsNullOrEmpty(_externalUiTranslationFile) ? "<english fallback>" : _externalUiTranslationFile) +
                    ", entries=" + ExternalUiTranslations.Count.ToString(CultureInfo.InvariantCulture) +
                    ", english=" + EnglishUiFallback.Count.ToString(CultureInfo.InvariantCulture) + ".");
                WriteLocalizationHealthReportSafe(false);
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[ItemIntelligence] External UI localization skipped: " + ex.Message);
            }
        }

        private static string Ui(string key)
        {
            if (string.IsNullOrEmpty(key)) return string.Empty;
            EnsureExternalUiTranslations();

            string cached;
            if (ResolvedUiTextCache.TryGetValue(key, out cached))
                return cached;

            string value;
            if (ExternalUiTranslations.TryGetValue(key, out value) && !string.IsNullOrEmpty(value))
            {
                value = NormalizeGameText(value);
                ResolvedUiTextCache[key] = value;
                return value;
            }

            if (EnglishUiFallback.TryGetValue(key, out value) && !string.IsNullOrEmpty(value))
            {
                bool english = IsEnglishLanguageToken(_externalUiTranslationLanguage);
                if (!english && MissingUiTranslationKeys.Add(key))
                {
                    Debug.LogWarning("[ItemIntelligence] Missing UI translation key for active language: " + key);
                    MarkLocalizationHealthDirty();
                }
                value = NormalizeGameText(value);
                ResolvedUiTextCache[key] = value;
                return value;
            }

            if (MissingUiTranslationKeys.Add(key))
            {
                Debug.LogWarning("[ItemIntelligence] Missing UI localization key: " + key);
                MarkLocalizationHealthDirty();
            }
            value = "[" + key + "]";
            ResolvedUiTextCache[key] = value;
            return value;
        }

        private static bool IsEnglishLanguageToken(string language)
        {
            return ExternalLanguageMatches(language, "English;en;Английский");
        }

        private static bool IsEnglishLanguage()
        {
            return IsEnglishLanguageToken(GetLanguageSignature());
        }

        private static string NormalizeModUiText(string value)
        {
            // v1.7.17 no longer performs phrase replacement. Authored UI strings are
            // resolved through Ui(key), preventing translations from accidentally
            // modifying vanilla item, mob, faction, or station names.
            return NormalizeGameText(value);
        }
    }
}
