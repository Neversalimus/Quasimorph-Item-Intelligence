using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;

namespace ItemIntelligence
{
    // The game, settings, file loader and developer tools share this registry.
    // Public read-only data also lets build checks use the compiled production type.
    public sealed class UiLanguage
    {
        public readonly string Id, Signature, Label, FileName, Aliases;
        public readonly CultureInfo Culture;
        public readonly bool Cjk, SpaceBeforePercent;

        internal UiLanguage(string id, string signature, string label, string fileName,
            string culture, bool cjk, bool spaceBeforePercent, string aliases)
        {
            Id = id; Signature = signature; Label = label; FileName = fileName;
            Culture = CultureInfo.GetCultureInfo(culture);
            Cjk = cjk; SpaceBeforePercent = spaceBeforePercent; Aliases = aliases;
        }
    }

    public static class LanguageCatalog
    {
        public const string Auto = "Auto (Game)";
        public static readonly ReadOnlyCollection<UiLanguage> All = Array.AsReadOnly(new[]
        {
            new UiLanguage("EN", "English", "English", "en.lang", "", false, false,
                "EnglishUS;English;en;en-US;en-GB;Английский"),
            new UiLanguage("RU", "Russian", "Русский", "ru.lang", "ru-RU", false, true,
                "Russian;ru;ru-RU;Русский;рус"),
            new UiLanguage("DE", "German", "Deutsch", "de.lang", "de-DE", false, true,
                "German;Deutsch;de;de-DE"),
            new UiLanguage("PL", "Polish", "Polski", "pl.lang", "pl-PL", false, true,
                "Polish;Polski;pl;pl-PL"),
            new UiLanguage("ZH", "ChineseSimplified", "简体中文 / Chinese", "zh-Hans.lang", "zh-CN", true, false,
                "ChineseSimp;ChineseSimplified;SimplifiedChinese;schinese;zhcn;zh-CN;zh-Hans;简体中文;Chinese (Simplified);Simplified Chinese"),
            new UiLanguage("ES", "Spanish", "Español", "es.lang", "es-ES", false, true,
                "Spanish;Español;Espanol;es;es-ES"),
            new UiLanguage("PTBR", "BrazilianPortugal", "Português (Brasil)", "pt-BR.lang", "pt-BR", false, false,
                "BrazilianPortugal;BrazilianPortuguese;PortugueseBrazil;Português (Brasil);Portugues (Brasil);pt-BR"),
            new UiLanguage("FR", "French", "Français", "fr.lang", "fr-FR", false, true,
                "French;Français;Francais;fr;fr-FR"),
            new UiLanguage("JA", "Japanese", "日本語 / Japanese", "ja.lang", "ja-JP", true, false,
                "Japanese;ja;ja-JP;日本語"),
            new UiLanguage("KO", "Korean", "한국어 / Korean", "ko.lang", "ko-KR", true, false,
                "Korean;ko;ko-KR;한국어"),
            new UiLanguage("TR", "Turkish", "Türkçe", "tr.lang", "tr-TR", false, false,
                "Turkish;Türkçe;Turkce;tr;tr-TR")
        });
        private static readonly Dictionary<string, UiLanguage> ByAlias = BuildAliases();

        private static Dictionary<string, UiLanguage> BuildAliases()
        {
            var result = new Dictionary<string, UiLanguage>(StringComparer.OrdinalIgnoreCase);
            foreach (UiLanguage language in All)
            {
                foreach (string alias in (language.Aliases + ";" + language.Label + ";" + language.Signature).Split(';'))
                {
                    string key = Normalize(alias);
                    UiLanguage previous;
                    if (result.TryGetValue(key, out previous) && previous != language)
                        throw new InvalidOperationException("Conflicting language alias: " + alias);
                    result[key] = language;
                    result[alias] = language;
                }
            }
            return result;
        }

        public static UiLanguage Find(string value)
        {
            UiLanguage language;
            // Canonical signatures and saved labels are the render-loop path.
            // Normalize only an unrecognized spelling, without a per-frame cache.
            if (value != null && ByAlias.TryGetValue(value, out language)) return language;
            return ByAlias.TryGetValue(Normalize(value), out language) ? language : null;
        }

        public static bool IsBuiltInFile(string fileName)
        {
            foreach (UiLanguage language in All)
                if (string.Equals(fileName, language.FileName, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        public static string Normalize(string value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            var result = new StringBuilder(value.Length);
            foreach (char c in value)
                if (char.IsLetterOrDigit(c)) result.Append(char.ToLowerInvariant(c));
            return result.ToString();
        }

        public static bool MatchesDeclaration(string language, string declaration)
        {
            if (string.IsNullOrWhiteSpace(language) || string.IsNullOrWhiteSpace(declaration)) return false;
            foreach (string alias in declaration.Split(new[] { ';', ',', '|' }, StringSplitOptions.RemoveEmptyEntries))
                if (MatchesAlias(language, alias)) return true;
            return false;
        }

        private static bool MatchesAlias(string language, string alias)
        {
            string normalized = Normalize(alias);
            if (normalized.Length == 0) return false;
            if (Normalize(language) == normalized) return true;
            string candidate = language.Trim(), expected = alias.Trim();
            for (int start = 0; start < candidate.Length;)
            {
                int index = candidate.IndexOf(expected, start, StringComparison.OrdinalIgnoreCase);
                if (index < 0) break;
                int end = index + expected.Length;
                if ((index == 0 || !char.IsLetterOrDigit(candidate[index - 1])) &&
                    (end >= candidate.Length || !char.IsLetterOrDigit(candidate[end]))) return true;
                start = index + 1;
            }
            return false;
        }
    }
}
