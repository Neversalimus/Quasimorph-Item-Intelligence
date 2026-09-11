using System;
using System.Globalization;
using System.Collections.Generic;
using UnityEngine;

namespace ItemIntelligence
{
    public static partial class ModMain
    {
        private static List<string> GetUiLanguageOptions()
        {
            return new List<string>
            {
                "Auto (Game)",
                "English",
                "Русский",
                "Deutsch",
                "Polski",
                "简体中文 / Chinese", // Latin suffix remains readable in an MCM font without Han glyphs.
                "Español",
                "Português (Brasil)",
                "Français"
            };
        }

        private static string NormalizeUiLanguagePreference(string value)
        {
            string token = NormalizeModLanguageToken(value);
            if (string.IsNullOrEmpty(token) || token == "auto" || token == "autogame" ||
                token == "game" || token == "default")
                return "Auto (Game)";
            if (token == "english" || token == "en" || token == "englishus" || token == "enus" || token == "engb") return "English";
            if (token == "russian" || token == "ru" || token == "ruru" || token == "русский" || token == "рус")
                return "Русский";
            if (token == "german" || token == "deutsch" || token == "de" || token == "dede")
                return "Deutsch";
            if (token == "polish" || token == "polski" || token == "pl" || token == "plpl")
                return "Polski";
            if (token == "spanish" || token == "español" || token == "espanol" || token == "es" || token == "eses") return "Español";
            if (token == "brazilianportugal" || token == "brazilianportuguese" || token == "portuguesebrazil" ||
                token == "portuguêsbrasil" || token == "portuguesbrasil" || token == "ptbr") return "Português (Brasil)";
            if (token == "french" || token == "français" || token == "francais" || token == "fr" || token == "frfr") return "Français";
            if (token == "chinesesimp" || token == "chinesesimplified" || token == "simplifiedchinese" ||
                token == "schinese" || token == "zhcn" || token == "zhhans" ||
                token == "简体中文" || token == "简体中文chinese")
                return "简体中文 / Chinese";
            return "Auto (Game)";
        }

        private static void SetUiLanguagePreference(string value, string source)
        {
            string normalized = NormalizeUiLanguagePreference(value);
            if (string.Equals(normalized, UiLanguagePreference, StringComparison.Ordinal))
                return;

            UiLanguagePreference = normalized;
            _externalUiTranslationLanguage = string.Empty;
            _externalUiTranslationFile = string.Empty;
            ExternalUiTranslations.Clear();
            ResolvedUiTextCache.Clear();
            MissingUiTranslationKeys.Clear();
            ResetLocalizationHealthForReload();
            Debug.Log("[ItemIntelligence] UI language preference: " + normalized +
                " (" + (source ?? "unknown") + ").");
        }

        private static string GetUiLanguageSignature()
        {
            string preference = UiLanguagePreference;
            if (string.Equals(preference, "English", StringComparison.Ordinal)) return "English";
            if (string.Equals(preference, "Русский", StringComparison.Ordinal)) return "Russian";
            if (string.Equals(preference, "Deutsch", StringComparison.Ordinal)) return "German";
            if (string.Equals(preference, "Polski", StringComparison.Ordinal)) return "Polish";
            if (string.Equals(preference, "Español", StringComparison.Ordinal)) return "Spanish";
            if (string.Equals(preference, "Português (Brasil)", StringComparison.Ordinal)) return "BrazilianPortugal";
            if (string.Equals(preference, "Français", StringComparison.Ordinal)) return "French";
            if (string.Equals(preference, "简体中文 / Chinese", StringComparison.Ordinal)) return "ChineseSimplified";
            return GetLanguageSignature();
        }

        private static object GetLocalizationOwnerInstance(Type owner)
        {
            // Unity game singletons declare Instance on a generic base class.
            // GetProperties without FlattenHierarchy does not expose that static
            // member on the derived Localization / LocalizationFontKeeper type.
            for (Type current = owner; current != null; current = current.BaseType)
            {
                object instance = FirstNonNull(GetStaticMember(current, "Instance"),
                    GetStaticMember(current, "instance"), GetStaticMember(current, "Current"),
                    GetStaticMember(current, "current"));
                if (instance != null) return instance;
            }
            return null;
        }

        private static bool IsUiRussianLanguage()
        {
            return ExternalLanguageMatches(GetUiLanguageSignature(), "Russian;ru;Русский;рус");
        }

        private static bool IsGermanLanguage()
        {
            return ExternalLanguageMatches(GetUiLanguageSignature(), "German;german;Deutsch;de;de-DE");
        }

        private static bool IsPolishLanguage()
        {
            return ExternalLanguageMatches(GetUiLanguageSignature(), "Polish;polish;Polski;pl;pl-PL");
        }

        private static bool IsSimplifiedChineseLanguage()
        {
            return ExternalLanguageMatches(
                GetUiLanguageSignature(),
                "ChineseSimp;ChineseSimplified;SimplifiedChinese;schinese;zhcn;zh-CN;zh-Hans;简体中文;Chinese (Simplified);Simplified Chinese");
        }

        private static bool IsCjkLanguage()
        {
            string language = GetUiLanguageSignature();
            return ExternalLanguageMatches(
                language,
                "ChineseSimp;ChineseSimplified;SimplifiedChinese;schinese;zhcn;zh-CN;zh-Hans;简体中文;Chinese (Simplified);Simplified Chinese;" +
                "Japanese;japanese;ja;ja-JP;日本語;Korean;korean;ko;ko-KR;한국어");
        }

        private static string _uiNumberLanguage = string.Empty;
        private static CultureInfo _uiNumberCulture = CultureInfo.InvariantCulture;
        private static CultureInfo GetUiNumberCulture()
        {
            string language = GetUiLanguageSignature();
            if (_uiNumberLanguage == language) return _uiNumberCulture;
            _uiNumberLanguage = language;
            string preference = NormalizeUiLanguagePreference(language);
            _uiNumberCulture = IsUiRussianLanguage() ? CultureInfo.GetCultureInfo("ru-RU") :
                IsGermanLanguage() ? CultureInfo.GetCultureInfo("de-DE") :
                IsPolishLanguage() ? CultureInfo.GetCultureInfo("pl-PL") :
                preference == "Español" ? CultureInfo.GetCultureInfo("es-ES") :
                preference == "Português (Brasil)" ? CultureInfo.GetCultureInfo("pt-BR") :
                preference == "Français" ? CultureInfo.GetCultureInfo("fr-FR") :
                IsSimplifiedChineseLanguage() ? CultureInfo.GetCultureInfo("zh-CN") : CultureInfo.InvariantCulture;
            return _uiNumberCulture;
        }

        private static string GetUiPercentSuffix()
        {
            string culture = GetUiNumberCulture().Name;
            return culture == "ru-RU" || culture == "de-DE" || culture == "pl-PL" || culture == "es-ES" || culture == "fr-FR" ? " %" : "%";
        }

        private static int GetLanguageAwareWrapFallback(
            int russianFallbackChars,
            int englishFallbackChars)
        {
            if (IsCjkLanguage())
                return Math.Max(36, (int)Math.Floor(englishFallbackChars * 0.62));

            string culture = GetUiNumberCulture().Name;
            if (IsUiRussianLanguage() || IsGermanLanguage() || IsPolishLanguage() || culture == "es-ES" || culture == "pt-BR" || culture == "fr-FR")
                return Math.Min(russianFallbackChars, englishFallbackChars);

            return englishFallbackChars;
        }

        private static string FormatCompactUiDurationFallback(double hours)
        {
            if (double.IsNaN(hours) || double.IsInfinity(hours)) return "—";
            hours = Math.Max(0d, hours);
            if (hours >= 24000d) return ">999" + Ui("ui.unit_day_short");
            int totalHours = (int)Math.Floor(hours);
            if (totalHours >= 24)
            {
                int days = totalHours / 24;
                int remainderHours = totalHours % 24;
                return days.ToString(CultureInfo.InvariantCulture) + Ui("ui.unit_day_short") + " " +
                    remainderHours.ToString(CultureInfo.InvariantCulture) + Ui("ui.unit_hour_short");
            }

            if (totalHours >= 1)
                return totalHours.ToString(CultureInfo.InvariantCulture) + Ui("ui.unit_hour_short");

            int minutes = Math.Max(1, (int)Math.Floor(hours * 60d));
            return minutes.ToString(CultureInfo.InvariantCulture) + Ui("ui.unit_minute_short");
        }

        private static bool ContainsHanScript(string value)
        {
            if (string.IsNullOrEmpty(value)) return false;
            for (int i = 0; i < value.Length; i++)
            {
                int c = value[i];
                if ((c >= 0x3400 && c <= 0x4DBF) ||
                    (c >= 0x4E00 && c <= 0x9FFF) ||
                    (c >= 0xF900 && c <= 0xFAFF))
                    return true;
            }
            return false;
        }

        private static bool ContainsKanaScript(string value)
        {
            if (string.IsNullOrEmpty(value)) return false;
            for (int i = 0; i < value.Length; i++)
            {
                int c = value[i];
                if ((c >= 0x3040 && c <= 0x30FF) ||
                    (c >= 0x31F0 && c <= 0x31FF))
                    return true;
            }
            return false;
        }

        private static bool ContainsHangulScript(string value)
        {
            if (string.IsNullOrEmpty(value)) return false;
            for (int i = 0; i < value.Length; i++)
            {
                int c = value[i];
                if ((c >= 0x1100 && c <= 0x11FF) ||
                    (c >= 0x3130 && c <= 0x318F) ||
                    (c >= 0xAC00 && c <= 0xD7AF))
                    return true;
            }
            return false;
        }
    }
}
