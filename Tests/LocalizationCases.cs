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
                { "German", "de.lang", "Deutsch" }, { "Polish", "pl.lang", "Polski" }, { "ChineseSimp", "zh-Hans.lang", "简体中文" } };
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
                Verify(ExternalUiTranslations.Count == 653, "complete language " + languages[i, 0]);
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

            // Community overrides, force rules, English fallback and damaged files.
            string custom = Path.Combine(dir, "custom.lang");
            File.WriteAllText(custom, "@language=German\n@force=true\ntab.loot.short\tCustom\n", new UTF8Encoding(false));
            SetUiLanguagePreference("Polski", "test");
            Verify(Ui("tab.loot.short") == "ŁUP", "forced German cannot hijack manual Polish");
            SetUiLanguagePreference("Deutsch", "test");
            Verify(Ui("tab.loot.short") == "Custom", "exact community override");
            Verify(Ui("tab.trade.short") == "TRADE", "missing community key falls back to English");
            Verify(MissingUiTranslationKeys.Contains("tab.trade.short"), "fallback diagnosed");
            SetUiLanguagePreference("Auto (Game)", "test");
            GameLanguage("French");
            Verify(Ui("tab.loot.short") == "Custom", "single forced community language in Auto");
            File.Delete(custom); ReloadUi();
            Verify(Ui("tab.loot.short") == "LOOT", "unsupported language safely uses English");
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

            // Native game fonts are shared. QII's mixed-script fallback must be private
            // and reused after repeated switches, with no growth of original lists.
            TMP_FontAsset latin = new TMP_FontAsset { name = "Latin" };
            TMP_FontAsset han = new TMP_FontAsset { name = "Chinese" };
            _inspectorFont = latin;
            MGSC.LocalizationFontKeeper.Instance.FontPresets.Add(new MGSC.FontPreset {
                FontAsset = latin, AvaialableLangs = new List<string> { "EnglishUS", "Russian", "German", "Polish" } });
            MGSC.LocalizationFontKeeper.Instance.FontPresets.Add(new MGSC.FontPreset {
                FontAsset = han, AvaialableLangs = new List<string> { "ChineseSimp" } });
            GameLanguage("Russian"); SetUiLanguagePreference("简体中文", "test");
            TMP_FontAsset mixed = ResolveUiFont();
            Verify(mixed != han && mixed != latin && mixed.fallbackFontAssetTable.Contains(latin), "private mixed Chinese/Russian font");
            Verify(han.fallbackFontAssetTable.Count == 0 && latin.fallbackFontAssetTable.Count == 0, "no vanilla font mutation");
            SetUiLanguagePreference("English", "test"); Verify(ResolveUiFont() == latin, "Latin restored");
            SetUiLanguagePreference("简体中文", "test"); Verify(ResolveUiFont() == mixed && UiMixedFonts.Count == 1, "mixed font reused");

            string mcmValue;
            Verify(TryReadMcmString(new Dictionary<string, object> { { "UiLanguage", new McmValue { Value = "Polski" } } }, "UiLanguage", out mcmValue)
                && mcmValue == "Polski", "MCM wrapped string");
            List<object> configs = new List<object>();
            AddMcmStringDropdown(typeof(List<object>).GetMethod("Add"), configs, typeof(ModernDropdown), typeof(LegacyDropdown),
                "UiLanguage", "Auto (Game)", "Language", "Auto (Game)", "Tip", "Label", GetUiLanguageOptions());
            Verify(((ModernDropdown)configs[0]).Options.Count == 6, "modern dropdown constructor");
            AddMcmStringDropdown(typeof(List<object>).GetMethod("Add"), configs, null, typeof(LegacyDropdown),
                "UiLanguage", "Auto (Game)", "Language", "Auto (Game)", "Tip", "Label", GetUiLanguageOptions());
            Verify(((LegacyDropdown)configs[1]).Options.Count == 6, "legacy dropdown constructor");
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
