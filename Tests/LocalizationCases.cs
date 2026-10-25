// Minimal game/TMP fixtures. All language selection, reflection, parsing, wrapping,
// formatting and private font fallback ownership come from production declarations.
namespace UnityEngine
{
    public enum HideFlags { HideAndDontSave }
    public static class Time { public static int frameCount = 1000; }
    public static class Debug { public static void Log(object value) {} public static void LogWarning(object value) {} }
    public class Object
    {
        public static T Instantiate<T>(T value) where T : TMPro.TMP_FontAsset
        { return (T)value.Copy(); }
    }
}
namespace TMPro
{
    public class TMP_FontAsset
    {
        private static int sequence;
        private readonly int id = ++sequence;
        public string name;
        public UnityEngine.HideFlags hideFlags;
        public List<TMP_FontAsset> fallbackFontAssetTable = new List<TMP_FontAsset>();
        public int GetInstanceID() { return id; }
        public TMP_FontAsset Copy() { return new TMP_FontAsset { name = name, fallbackFontAssetTable = fallbackFontAssetTable }; }
    }
}
namespace MGSC
{
    public class Singleton<T> where T : new() { public static T Instance { get; } = new T(); }
    public class Localization : Singleton<Localization> { public string CurrentLang { get; set; } = "Russian"; }
    public class LocalizationFontKeeper : Singleton<LocalizationFontKeeper>
    { public List<FontPreset> FontPresets { get; set; } = new List<FontPreset>(); }
    public class FontPreset
    { public List<string> AvaialableLangs { get; set; } public TMPro.TMP_FontAsset FontAsset { get; set; } }
}
namespace HarmonyLib
{
    public static class AccessTools
    {
        public static Type TypeByName(string name)
        {
            if (name == "MGSC.Localization") return typeof(MGSC.Localization);
            if (name == "MGSC.LocalizationFontKeeper") return typeof(MGSC.LocalizationFontKeeper);
            return null;
        }
    }
}
namespace ItemIntelligence
{
    public static partial class ModMain
    {
        private static int localizationChecks, duplicates, malformed, invalidUtf8;
        private static TMP_FontAsset _inspectorFont;
        private static string _tradeTravelOriginSpaceObjectId = "earth";
        private static string InvokeLocalizationRaw(string key) { return "Game item"; }
        private static void ResetLocalizationHealthForReload() { duplicates = malformed = invalidUtf8 = 0; }
        private static void RecordLocalizationDuplicateKey(string path, string key) { duplicates++; }
        private static void RecordLocalizationMalformedLine(string path, int line) { malformed++; }
        private static void RecordLocalizationUtf8Failure(string path, string detail) { invalidUtf8++; }
        private static void MarkLocalizationHealthDirty() {}
        private static void WriteLocalizationHealthReportSafe(bool force) {}
        private static void Verify(bool success, string reason)
        { localizationChecks++; if (!success) throw new Exception("Localization regression: " + reason); }
        private static void GameLanguage(string language)
        { MGSC.Localization.Instance.CurrentLang = language; UnityEngine.Time.frameCount += 241; }
        private static void ReloadUi()
        { _externalUiTranslationLanguage = string.Empty; ResolvedUiTextCache.Clear(); }

        public static int RunLocalizationCases(string root)
        {
            string dir = Path.Combine(root, "Localization");
            string[,] languages = { { "EnglishUS", "en.lang", "English" }, { "Russian", "ru.lang", "Русский" },
                { "German", "de.lang", "Deutsch" }, { "Polish", "pl.lang", "Polski" }, { "ChineseSimp", "zh-Hans.lang", "简体中文" },
                { "Spanish", "es.lang", "Español" }, { "BrazilianPortugal", "pt-BR.lang", "Português (Brasil)" }, { "French", "fr.lang", "Français" },
                { "Japanese", "ja.lang", "日本語 / Japanese" }, { "Korean", "ko.lang", "한국어 / Korean" }, { "Turkish", "tr.lang", "Türkçe" } };
            // A plain reflection call cannot see inherited static Instance. The
            // production owner traversal must recover it, never guess from the OS.
            Verify(GetStaticMember(typeof(MGSC.Localization), "Instance") == null, "fixture reproduces inherited static member boundary");
            for (int i = 0; i < languages.GetLength(0); i++)
            {
                SetUiLanguagePreference("Auto (Game)", "test");
                GameLanguage(languages[i, 0]);
                EnsureExternalUiTranslations();
                Verify(GetLanguageSignature() == languages[i, 0], "exact game metadata " + languages[i, 0]);
                Verify(_externalUiTranslationFile == languages[i, 1], "Auto file " + languages[i, 0]);
                Verify(ExternalUiTranslations.Count == 695, "complete language " + languages[i, 0]);
                foreach (string key in EnglishUiFallback.Keys)
                {
                    Verify(Ui(key) == NormalizeGameText(ExternalUiTranslations[key]), "active value " + key);
                    string format = Ui(key).Replace("{KEY}", "F2");
                    string.Format(CultureInfo.InvariantCulture, format, new object[] { 2, 3, 4, 5, 6 });
                }
                Verify(MissingUiTranslationKeys.Count == 0, "no fallback for shipped language");
            }
            GameLanguage("Russian");
            SetUiLanguagePreference("de-DE", "MCM");
            Verify(GetLanguageSignature() == "Russian" && GetUiLanguageSignature() == "German", "manual changes UI only");
            Verify(Ui("tab.loot.short") == "BEUTE", "manual replaces cached Russian text");
            Verify(FormatExpectedNumber(0.5) == "0,5" && GetUiPercentSuffix() == " %", "German decimals");
            Verify(FormatContainerEstimateNumber(0.005) == "<0,01", "German tiny chance");
            Verify(FormatUiDaysAndHours(23.6) == "1T 0h", "vanilla day rollover");
            Verify(FormatUiDaysAndHours(0.25) == "<1h", "vanilla short duration");
            Verify(IsWeaponModeLabelCompatibleWithCurrentLanguage("Очередь"), "game-provided Russian mode retained");
            Verify(!IsWeaponModeLabelCompatibleWithCurrentLanguage("Burst"), "stale English mode rejected in Russian game");
            SetUiLanguagePreference("Polski", "test");
            Verify(FormatUiDaysAndHours(2.5) == "2g", "banker's rounding follows vanilla");
            Verify(FormatUiDaysAndHours(3.5) == "4g", "odd half rounds up");
            Verify(FormatExpectedNumber(1.25) == "1,25", "Polish decimals");
            string[,] latinLanguages = {
                { "es-ES", "Spanish", "BOTÍN", "es-ES", " %", "1d 3h" },
                { "pt-BR", "BrazilianPortugal", "SAQUE", "pt-BR", "%", "1d 3h" },
                { "fr-FR", "French", "BUTIN", "fr-FR", " %", "1j 3h" } };
            for (int i = 0; i < latinLanguages.GetLength(0); i++)
            {
                SetUiLanguagePreference(latinLanguages[i, 0], "MCM");
                Verify(GetUiLanguageSignature() == latinLanguages[i, 1] && GetLanguageSignature() == "Russian", "Latin UI keeps game language");
                Verify(Ui("tab.loot.short") == latinLanguages[i, 2], "Latin switch clears previous text cache");
                Verify(GetUiNumberCulture().Name == latinLanguages[i, 3] && FormatExpectedNumber(1.25) == "1,25", "Latin culture and decimals");
                Verify(GetUiPercentSuffix() == latinLanguages[i, 4] && FormatContainerEstimateNumber(0.005) == "<0,01", "Latin percentages and tiny chances");
                Verify(FormatUiDaysAndHours(27) == latinLanguages[i, 5] && FormatCompactUiDurationFallback(0.25) == "15m", "Latin time units");
                Verify(GetLanguageAwareWrapFallback(60, 90) == 60, "long Latin notes use conservative wrapping");
            }
            string[,] latinAliases = {
                { "Spanish", "Español" }, { "Español", "Español" }, { "Espanol", "Español" }, { "es", "Español" },
                { "BrazilianPortugal", "Português (Brasil)" }, { "BrazilianPortuguese", "Português (Brasil)" },
                { "Portuguese (Brazil)", "Português (Brasil)" }, { "Português (Brasil)", "Português (Brasil)" },
                { "Portugues (Brasil)", "Português (Brasil)" }, { "pt_BR", "Português (Brasil)" },
                { "French", "Français" }, { "Français", "Français" }, { "Francais", "Français" }, { "fr", "Français" } };
            for (int i = 0; i < latinAliases.GetLength(0); i++)
                Verify(NormalizeUiLanguagePreference(latinAliases[i, 0]) == latinAliases[i, 1], "Latin saved preference alias " + latinAliases[i, 0]);
            Verify(NormalizeUiLanguagePreference("pt-PT") == "Auto (Game)", "European Portuguese is not Brazilian Portuguese");
            Verify(!ExternalLanguageMatches("pt-PT", ReadUiLanguageDeclaration(Path.Combine(dir, "pt-BR.lang"))), "Brazilian file does not claim European Portuguese");
            SetUiLanguagePreference("ChineseSimp", "test");
            Verify(Ui("tab.loot.short") == "战利品" && GetUiNumberCulture().Name == "zh-CN", "Chinese alias and culture");
            Verify(FormatCompactUiDurationFallback(0.25) == "15分", "Chinese fallback minutes");
            Verify(FormatUiDaysAndHours(27) == "1天 3时", "Chinese units");
            Verify(FormatUiDaysAndHours(double.NaN) == "—" && FormatCompactUiDurationFallback(double.PositiveInfinity) == "—", "invalid time hidden");
            Verify(FormatUiDaysAndHours(double.MaxValue) == ">999天", "huge time bounded before conversion");
            Verify(NormalizeUiLanguagePreference("en-US") == "English" && NormalizeUiLanguagePreference("ru-RU") == "Русский", "locale aliases");
            Verify(NormalizeUiLanguagePreference("unknown") == "Auto (Game)", "unknown preference safely defaults");
            Verify(!ExternalLanguageMatches("Belarusian", "ru;Russian"), "no substring language match");
            Verify(!ExternalLanguageMatches("ChineseTraditional", "ChineseSimp;zh-CN;zh-Hans"), "do not misclassify traditional Chinese");

            string[,] addedLanguages = {
                { "ja-JP", "Japanese", "戦利品", "ja-JP", "1.25", "15分", "1日 3時" },
                { "ko-KR", "Korean", "전리품", "ko-KR", "1.25", "15분", "1일 3시" },
                { "tr-TR", "Turkish", "GANİMET", "tr-TR", "1,25", "15dk", "1g 3sa" } };
            for (int i = 0; i < addedLanguages.GetLength(0); i++)
            {
                SetUiLanguagePreference(addedLanguages[i, 0], "MCM");
                Verify(GetUiLanguageSignature() == addedLanguages[i, 1] && GetLanguageSignature() == "Russian", "new manual UI keeps game language");
                Verify(Ui("tab.loot.short") == addedLanguages[i, 2], "new language replaces previous cache");
                Verify(GetUiNumberCulture().Name == addedLanguages[i, 3] && FormatExpectedNumber(1.25) == addedLanguages[i, 4], "new number cultures");
                Verify(FormatCompactUiDurationFallback(0.25) == addedLanguages[i, 5] && FormatUiDaysAndHours(27) == addedLanguages[i, 6], "new compact time units");
                Verify(IsCjkLanguage() == (i < 2), "Turkish is not CJK");
                Verify(GetLanguageAwareWrapFallback(60, 90) == (i < 2 ? 55 : 60), "new conservative fallback widths");
            }
            string[,] addedAliases = {
                { "日本語", "日本語 / Japanese" }, { "Japanese", "日本語 / Japanese" }, { "ja_JP", "日本語 / Japanese" }, { "ja", "日本語 / Japanese" },
                { "한국어", "한국어 / Korean" }, { "Korean", "한국어 / Korean" }, { "ko_KR", "한국어 / Korean" }, { "ko", "한국어 / Korean" },
                { "Türkçe", "Türkçe" }, { "Turkce", "Türkçe" }, { "Turkish", "Türkçe" }, { "tr", "Türkçe" } };
            for (int i = 0; i < addedAliases.GetLength(0); i++)
                Verify(NormalizeUiLanguagePreference(addedAliases[i, 0]) == addedAliases[i, 1], "new saved language alias " + addedAliases[i, 0]);
            foreach (string option in GetUiLanguageOptions())
                Verify(NormalizeUiLanguagePreference(option) == option, "dropdown label round-trips through saved config: " + option);
            Verify(!ExternalLanguageMatches("TraditionalChinese", ReadUiLanguageDeclaration(Path.Combine(dir, "tr.lang"))), "tr is not a substring match");

            // Community overrides, force rules, English fallback and damaged files.
            string custom = Path.Combine(dir, "custom.lang");
            File.WriteAllText(custom, "@language=German\n@force=true\ntab.loot.short\tCustom\n", new UTF8Encoding(false));
            SetUiLanguagePreference("Polski", "test");
            Verify(Ui("tab.loot.short") == "ŁUP", "forced German cannot hijack manual Polish");
            SetUiLanguagePreference("Deutsch", "test");
            Verify(Ui("tab.loot.short") == "Custom", "exact community override");
            Verify(Ui("tab.trade.short") == "TRADE", "missing community key falls back to English");
            Verify(MissingUiTranslationKeys.Contains("tab.trade.short"), "fallback diagnosed");
            for (int i = 0; i < latinLanguages.GetLength(0); i++)
            {
                SetUiLanguagePreference(latinLanguages[i, 0], "test");
                Verify(Ui("tab.loot.short") == latinLanguages[i, 2], "forced German cannot hijack manual Latin language");
                SetUiLanguagePreference("Auto (Game)", "test"); GameLanguage(latinLanguages[i, 1]);
                Verify(Ui("tab.loot.short") == "Custom", "single force can override new built-in language in Auto");
            }
            for (int i = 0; i < addedLanguages.GetLength(0); i++)
            {
                SetUiLanguagePreference(addedLanguages[i, 0], "test");
                Verify(Ui("tab.loot.short") == addedLanguages[i, 2], "forced file cannot hijack new manual languages");
                SetUiLanguagePreference("Auto (Game)", "test"); GameLanguage(addedLanguages[i, 1]);
                Verify(Ui("tab.loot.short") == "Custom", "new shipped files are built-ins, not community overrides");
            }
            SetUiLanguagePreference("Auto (Game)", "test");
            GameLanguage("Italian");
            Verify(Ui("tab.loot.short") == "Custom", "single forced community language in Auto");
            File.Delete(custom); ReloadUi();
            Verify(Ui("tab.loot.short") == "LOOT", "unsupported language safely uses English");
            File.WriteAllText(custom, "@language=Japanese\ntab.loot.short\t個人訳\n", new UTF8Encoding(false));
            SetUiLanguagePreference("ja-JP", "test");
            Verify(Ui("tab.loot.short") == "個人訳", "exact Japanese community override wins over shipped Japanese");
            File.Delete(custom); ReloadUi();
            Verify(Ui("tab.loot.short") == "戦利品", "removing override restores shipped Japanese");
            File.WriteAllText(custom, "@language=Custom\na\t leading \na\treplacement\nmalformed\n", new UTF8Encoding(false));
            Dictionary<string, string> map = new Dictionary<string, string>();
            ResetLocalizationHealthForReload(); LoadUiLanguageFile(custom, map);
            Verify(duplicates == 1 && malformed == 1, "duplicate/malformed lines diagnosed");
            Verify(map["a"] == "replacement", "duplicate policy stays deterministic");
            File.WriteAllText(custom, "a\t leading \n", new UTF8Encoding(false)); map.Clear(); LoadUiLanguageFile(custom, map);
            Verify(map["a"] == " leading ", "authored separators preserved");
            File.WriteAllBytes(custom, new byte[] { 0x61, 0x09, 0xc3, 0x28 });
            bool rejected = false;
            try { LoadUiLanguageFile(custom, map); } catch (DecoderFallbackException) { rejected = true; }
            Verify(rejected && invalidUtf8 == 1, "invalid UTF8 rejected and diagnosed"); File.Delete(custom);

            string cjk = "成功截肢后的基础概率，不计入Magnum升级。已安装的植入体单独列出。";
            List<string> wrapped = WrapUnspacedBrowserText(cjk, s => s.Length > 9);
            Verify(string.Concat(wrapped) == cjk && wrapped.Count > 1, "wrapping loses no Chinese characters");
            foreach (string line in wrapped) Verify(line.Length <= 9 && "，。".IndexOf(line[0]) < 0, "CJK bounds/punctuation");
            wrapped = WrapUnspacedBrowserText("汉😀字e\u0301汉字", s => s.Length > 4);
            Verify(string.Concat(wrapped) == "汉😀字e\u0301汉字", "surrogate and combining sequence preserved");
            foreach (string line in wrapped)
                Verify(!char.IsLowSurrogate(line[0]) && !char.IsHighSurrogate(line[line.Length-1]) && line[0] != '\u0301', "text element boundaries");
            string japanese = "「この契約」は対象です（返還）。オーグメントを確認してください。";
            for (int width = 5; width <= 12; width++)
            {
                wrapped = WrapUnspacedBrowserText(japanese, s => s.Length > width);
                Verify(string.Concat(wrapped) == japanese, "Japanese punctuation wrapping preserves all characters");
                foreach (string line in wrapped)
                    Verify(line.Length <= width && "）。」、ーっゃゅょ".IndexOf(line[0]) < 0 && "（「".IndexOf(line[line.Length - 1]) < 0,
                        "Japanese punctuation clusters and small kana stay with text");
            }

            // Native game fonts are shared. QII's mixed-script fallback must be private
            // and reused after repeated switches, with no growth of original lists.
            TMP_FontAsset latin = new TMP_FontAsset { name = "Latin" };
            TMP_FontAsset han = new TMP_FontAsset { name = "Chinese" };
            _inspectorFont = latin;
            MGSC.LocalizationFontKeeper.Instance.FontPresets.Add(new MGSC.FontPreset {
                FontAsset = latin, AvaialableLangs = new List<string> { "EnglishUS", "Russian", "German", "Polish", "Spanish", "BrazilianPortugal", "French", "Turkish" } });
            MGSC.LocalizationFontKeeper.Instance.FontPresets.Add(new MGSC.FontPreset {
                FontAsset = han, AvaialableLangs = new List<string> { "ChineseSimp" } });
            GameLanguage("Russian"); SetUiLanguagePreference("简体中文", "test");
            TMP_FontAsset mixed = ResolveUiFont();
            Verify(mixed != han && mixed != latin && mixed.fallbackFontAssetTable.Contains(latin), "private mixed Chinese/Russian font");
            Verify(han.fallbackFontAssetTable.Count == 0 && latin.fallbackFontAssetTable.Count == 0, "no vanilla font mutation");
            SetUiLanguagePreference("English", "test"); Verify(ResolveUiFont() == latin, "Latin restored");
            SetUiLanguagePreference("简体中文", "test"); Verify(ResolveUiFont() == mixed && UiMixedFonts.Count == 1, "mixed font reused");
            GameLanguage("ChineseSimp");
            TMP_FontAsset mixedLatin = null;
            for (int i = 0; i < latinLanguages.GetLength(0); i++)
            {
                SetUiLanguagePreference(latinLanguages[i, 0], "test");
                Verify(FindGameLanguageFont(GetUiLanguageSignature()) == latin, "Latin UI resolves exact game preset");
                TMP_FontAsset currentFont = ResolveUiFont();
                Verify(currentFont != latin && currentFont != han && currentFont.fallbackFontAssetTable.Contains(han), "Latin UI keeps Chinese game names readable");
                if (mixedLatin == null) mixedLatin = currentFont;
                Verify(currentFont == mixedLatin && UiMixedFonts.Count == 2, "Latin languages reuse one private mixed font");
            }
            Verify(han.fallbackFontAssetTable.Count == 0 && latin.fallbackFontAssetTable.Count == 0, "Latin switches preserve shared font lists");
            TMP_FontAsset kana = new TMP_FontAsset { name = "Japanese" };
            TMP_FontAsset hangul = new TMP_FontAsset { name = "Korean" };
            MGSC.LocalizationFontKeeper.Instance.FontPresets.Add(new MGSC.FontPreset {
                FontAsset = kana, AvaialableLangs = new List<string> { "Japanese" } });
            MGSC.LocalizationFontKeeper.Instance.FontPresets.Add(new MGSC.FontPreset {
                FontAsset = hangul, AvaialableLangs = new List<string> { "Korean" } });
            string[] fontLanguages = { "Turkish", "ChineseSimp", "Japanese", "Korean" };
            TMP_FontAsset[] nativeFonts = { latin, han, kana, hangul };
            for (int repeat = 0; repeat < 2; repeat++)
            for (int gameIndex = 0; gameIndex < nativeFonts.Length; gameIndex++)
            for (int uiIndex = 0; uiIndex < nativeFonts.Length; uiIndex++)
            {
                GameLanguage(fontLanguages[gameIndex]); SetUiLanguagePreference(fontLanguages[uiIndex], "test");
                TMP_FontAsset actual = ResolveUiFont();
                Verify(FindGameLanguageFont(GetUiLanguageSignature()) == nativeFonts[uiIndex], "exact new native font preset");
                Verify(uiIndex == gameIndex ? actual == nativeFonts[uiIndex] :
                    actual != nativeFonts[uiIndex] && actual.fallbackFontAssetTable.Contains(nativeFonts[gameIndex]), "mixed scripts preserve game names");
                foreach (TMP_FontAsset shared in nativeFonts) Verify(shared.fallbackFontAssetTable.Count == 0, "shared game font remains untouched");
            }
            Verify(UiMixedFonts.Count == 12, "all script pairs reuse a bounded font cache");

            string mcmValue;
            Verify(TryReadMcmString(new Dictionary<string, object> { { "UiLanguage", new McmValue { Value = "Polski" } } }, "UiLanguage", out mcmValue)
                && mcmValue == "Polski", "MCM wrapped string");
            List<object> configs = new List<object>();
            Verify(GetUiLanguageOptions()[5] == "简体中文 / Chinese" && GetUiLanguageOptions()[6] == "Español" &&
                GetUiLanguageOptions()[7] == "Português (Brasil)" && GetUiLanguageOptions()[8] == "Français" &&
                GetUiLanguageOptions()[9] == "日本語 / Japanese" && GetUiLanguageOptions()[10] == "한국어 / Korean" &&
                GetUiLanguageOptions()[11] == "Türkçe", "new options preserve existing language order");
            AddMcmStringDropdown(typeof(List<object>).GetMethod("Add"), configs, typeof(ModernDropdown), typeof(LegacyDropdown),
                "UiLanguage", "Auto (Game)", "Language", "Auto (Game)", "Tip", "Label", GetUiLanguageOptions());
            Verify(((ModernDropdown)configs[0]).Options.Count == 12, "modern dropdown constructor");
            AddMcmStringDropdown(typeof(List<object>).GetMethod("Add"), configs, null, typeof(LegacyDropdown),
                "UiLanguage", "Auto (Game)", "Language", "Auto (Game)", "Tip", "Label", GetUiLanguageOptions());
            Verify(((LegacyDropdown)configs[1]).Options.Count == 12, "legacy dropdown constructor");
            return localizationChecks;
        }
        private class McmValue { public string Value { get; set; } }
        public class ModernDropdown
        {
            public List<object> Options;
            public ModernDropdown(string key, object value, string header, object defaultValue, string tooltip, string label, List<object> options) { Options = options; }
        }
        public class LegacyDropdown
        {
            public List<string> Options;
            public LegacyDropdown(string key, object value, string header, object defaultValue, string tooltip, string label, List<string> options) { Options = options; }
        }
    }
}
