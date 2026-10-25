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
            var options = new List<string> { LanguageCatalog.Auto };
            foreach (UiLanguage language in LanguageCatalog.All) options.Add(language.Label);
            return options;
        }

        private static string NormalizeUiLanguagePreference(string value)
        {
            UiLanguage language = LanguageCatalog.Find(value);
            return language == null ? LanguageCatalog.Auto : language.Label;
        }

        private static void SetUiLanguagePreference(string value, string source)
        {
            string normalized = NormalizeUiLanguagePreference(value);
            if (string.Equals(normalized, UiLanguagePreference, StringComparison.Ordinal)) return;
            UiLanguagePreference = normalized;
            UiText.Reset();
            VerboseLog("[ItemIntelligence] UI language preference: " + normalized +
                " (" + (source ?? "unknown") + ").");
        }

        private static string GetUiLanguageSignature()
        {
            if (UiLanguagePreference == LanguageCatalog.Auto) return GetLanguageSignature();
            UiLanguage language = LanguageCatalog.Find(UiLanguagePreference);
            return language == null ? GetLanguageSignature() : language.Signature;
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

        private static CultureInfo GetUiNumberCulture()
        {
            UiLanguage language = LanguageCatalog.Find(GetUiLanguageSignature());
            return language == null ? CultureInfo.InvariantCulture : language.Culture;
        }

        private static string GetUiPercentSuffix()
        {
            UiLanguage language = LanguageCatalog.Find(GetUiLanguageSignature());
            return language != null && language.SpaceBeforePercent ? " %" : "%";
        }

        private static int GetLanguageAwareWrapFallback(int russianFallbackChars, int englishFallbackChars)
        {
            UiLanguage language = LanguageCatalog.Find(GetUiLanguageSignature());
            if (language == null || language.Id == "EN") return englishFallbackChars;
            return language.Cjk ? Math.Max(36, (int)Math.Floor(englishFallbackChars * 0.62)) :
                Math.Min(russianFallbackChars, englishFallbackChars);
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
