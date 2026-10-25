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
        private static MethodInfo _localizationGetMethod;
        private static ParameterInfo[] _localizationGetParameters;
        private static Type _localizationManagerType;
        private static bool _localizationManagerTypeResolved;

        private static readonly GameLocalization GameText = new GameLocalization(
            ResolveLanguageSignatureUncached, InvokeLocalizationRaw, () => Time.frameCount, NormalizeGameText, VerboseLog);

        private static string GetLanguageSignature() { return GameText.GetLanguageSignature(); }
        private static void EnsureLocalizationCacheLanguage() { GameText.EnsureLocalizationCacheLanguage(); }
        private static string LocalizeItem(string itemId) { return GameText.LocalizeItem(itemId); }
        private static string LocalizeMagnumPerk(string perkId) { return GameText.LocalizeMagnumPerk(perkId); }
        private static string LocalizeCandidates(string[] keys, string fallback) { return GameText.LocalizeCandidates(keys, fallback); }

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
            bool foundHangul = false;
            bool foundKana = false;
            bool foundHan = false;

            for (int i = 0; i < keys.Length; i++)
            {
                string value = InvokeLocalizationRaw(keys[i]);
                if (string.IsNullOrEmpty(value)) continue;

                foundLocalizedValue = true;

                if (ContainsCyrillic(value))
                    return "Russian";
                foundHangul |= ContainsHangulScript(value);
                foundKana |= ContainsKanaScript(value);
                foundHan |= ContainsHanScript(value);
            }

            // Aggregate across several item names before classifying CJK. Japanese
            // labels commonly mix Kanji with Kana, so Kana takes precedence over Han.
            if (foundHangul) return "Korean";
            if (foundKana) return "Japanese";
            if (foundHan) return "ChineseSimplified";

            // If exact selected-language metadata is unavailable and the probe is a
            // Latin-script language we cannot identify safely from item names alone.
            // Keep English as the fail-soft fallback rather than guessing German/Polish.
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

            object instance = GetLocalizationOwnerInstance(owner);

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

        private static string LocalizeKind(string kind, bool ru)
        {
            if (string.Equals(kind, "Production", StringComparison.OrdinalIgnoreCase))
                return Ui("ui.conveyor");
            if (string.Equals(kind, "Workbench", StringComparison.OrdinalIgnoreCase))
                return Ui("ui.mission_workbench");
            return kind;
        }

        private static readonly UiLocalization UiText = new UiLocalization(
            () => Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "Localization"),
            NormalizeGameText, VerboseLog, message => Debug.LogWarning(message));

        private static bool ExternalLanguageMatches(string language, string declaration)
        {
            return LanguageCatalog.MatchesDeclaration(language, declaration);
        }

        private static void EnsureExternalUiTranslations()
        {
            if (UiText.EnsureLoaded(GetUiLanguageSignature(), UiLanguagePreference == LanguageCatalog.Auto))
                WriteLocalizationHealthReportSafe(false);
        }

        private static string Ui(string key)
        {
            if (string.IsNullOrEmpty(key)) return string.Empty;
            EnsureExternalUiTranslations();
            return UiText.GetText(key);
        }

        private static void ResetLocalizationSession()
        {
            UiText.Reset();
            GameText.Reset();
        }

        private static bool IsEnglishLanguageToken(string language)
        {
            return LanguageCatalog.Find(language) == LanguageCatalog.All[0];
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
