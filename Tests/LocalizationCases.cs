// Minimal game/TMP fixtures. All language selection, reflection, parsing, wrapping,
// formatting and private font fallback ownership come from production declarations.
namespace UnityEngine
{
    public enum HideFlags { HideAndDontSave }
    public static class Time { public static int frameCount = 1000; }
    public static class Debug { public static void Log(object value) {} public static void LogWarning(object value) {} }
    public class Object
    {
        public static readonly List<TMPro.TMP_FontAsset> Destroyed = new List<TMPro.TMP_FontAsset>();
        public static void Destroy(TMPro.TMP_FontAsset value) { Destroyed.Add(value); }
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
        private static int localizationChecks;
        private static TMP_FontAsset _inspectorFont;
        private static string _tradeTravelOriginSpaceObjectId = "earth";
        private static string InvokeLocalizationRaw(string key) { return "Game item"; }
        private static void WriteLocalizationHealthReportSafe(bool force) {}
        private static void Verify(bool success, string reason)
        { localizationChecks++; if (!success) throw new Exception("Localization regression: " + reason); }
        private static void GameLanguage(string language)
        { MGSC.Localization.Instance.CurrentLang = language; UnityEngine.Time.frameCount += 241; }
        private static void ReloadUi()
        { UiText.Reset(); }

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
                Verify(UiText.FileName == languages[i, 1], "Auto file " + languages[i, 0]);
                Verify(UiText.ActiveCount == 696, "complete language " + languages[i, 0]);
                foreach (string key in UiText.EnglishKeys)
                {
                    Verify(Ui(key) == NormalizeGameText(UiText.GetActiveValue(key)), "active value " + key);
                    string format = Ui(key).Replace("{KEY}", "F2");
                    string.Format(CultureInfo.InvariantCulture, format, new object[] { 2, 3, 4, 5, 6 });
                }
                Verify(UiText.MissingCount == 0, "no fallback for shipped language");
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
            Verify(!ExternalLanguageMatches("pt-PT", UiText.ReadUiLanguageDeclaration(Path.Combine(dir, "pt-BR.lang"))), "Brazilian file does not claim European Portuguese");
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
                Verify(LanguageCatalog.Find(GetUiLanguageSignature()).Cjk == (i < 2), "Turkish is not CJK");
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
            Verify(!ExternalLanguageMatches("TraditionalChinese", UiText.ReadUiLanguageDeclaration(Path.Combine(dir, "tr.lang"))), "tr is not a substring match");

            int nativeReads = 0, nativeFrame = 0, languageReads = 0;
            string nativeLanguage = "English";
            var gameText = new GameLocalization(() => { languageReads++; return nativeLanguage; },
                key => { nativeReads++; return key == "missing" ? string.Empty : nativeLanguage + ":" + key; },
                () => nativeFrame, NormalizeGameText, message => {});
            string cachedItem = gameText.LocalizeItem("test");
            Verify(gameText.LocalizeItem("test") == cachedItem && nativeReads == 1, "game entity cache avoids repeated native calls");
            for (int i = 0; i < 1000; i++) gameText.LocalizeItem("test");
            Verify(nativeReads == 1 && languageReads == 1, "warm redraw does not repeat native lookup or metadata reflection");
            gameText.LocalizeMagnumPerk("test"); gameText.LocalizeMagnumPerk("test");
            Verify(nativeReads == 2 && gameText.PerkCount == 1, "Magnum names have their own warm result cache");
            nativeLanguage = "Russian";
            Verify(gameText.GetLanguageSignature() == "English", "game metadata refresh is throttled");
            nativeFrame += 241;
            Verify(gameText.LocalizeCandidates(new[] { "direct" }, "fallback") == "Russian:direct", "direct key lookup invalidates on game language change");
            Verify(gameText.LocalizeItem("test") == "Russian:item.test.name" && gameText.ItemCount == 1, "game names refresh independently of UI preference");
            Verify(gameText.LocalizeCandidates(new[] { "missing" }, "fallback") == "fallback", "unknown native key keeps fallback");
            gameText.LocalizeMagnumPerk("perk"); gameText.Reset();
            Verify(gameText.ItemCount == 0 && gameText.KeyCount == 0 && gameText.PerkCount == 0, "game-localization session clears all result caches");
            nativeFrame = 0; nativeLanguage = "Japanese";
            Verify(gameText.GetLanguageSignature() == "Japanese", "session reset forces a fresh game language read");
            var normalizedGameText = new GameLocalization(() => "Russian", key => "Ёлка", () => 0, NormalizeGameText, message => {});
            Verify(normalizedGameText.LocalizeCandidates(new[] { "key" }, "") == "Елка" &&
                normalizedGameText.LocalizeCandidates(new[] { "key" }, "") == "Елка", "cached and fresh native text use identical normalization");

            // Verify presentation results instead of particular source expressions.
            SetUiLanguagePreference("English", "test");
            Verify(FormatContainerEstimateRange(.001, .25, true) == "~ 0.1–25%", "estimate range uses a separator, not subtraction");
            Verify(FormatContainerEstimateRange(.25, .25, false) == "~ 25%*", "unknown modifier is disclosed");
            Verify(FormatContainerEstimateRange(double.NaN, .25, true) == "—" &&
                FormatContainerEstimateRange(.5, .25, true) == "—" &&
                FormatContainerEstimateRange(-.1, .25, true) == "—" &&
                FormatContainerEstimateRange(0, double.PositiveInfinity, true) == "—", "invalid estimate ranges are hidden");
            SetUiLanguagePreference("Russian", "test");
            Verify(FormatContainerEstimateRange(.001, .25, true) == "~ 0,1–25 %", "estimate range follows UI number culture");
            foreach (UiLanguage language in LanguageCatalog.All)
            {
                SetUiLanguagePreference(language.Label, "test");
                Verify(Ui("loot.note.container_context_chance").Contains("~"), "approximation symbol is explained in every language");
            }

            // Community overrides, force rules, English fallback and damaged files.
            string custom = Path.Combine(dir, "custom.lang");
            File.WriteAllText(custom, "@language=German\n@force=true\ntab.loot.short\tCustom\n", new UTF8Encoding(false));
            SetUiLanguagePreference("Polski", "test");
            Verify(Ui("tab.loot.short") == "ŁUP", "forced German cannot hijack manual Polish");
            SetUiLanguagePreference("Deutsch", "test");
            Verify(Ui("tab.loot.short") == "Custom", "exact community override");
            Verify(Ui("tab.trade.short") == "TRADE", "missing community key falls back to English");
            Verify(UiText.MissingKeys.Contains("tab.trade.short"), "fallback diagnosed");
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
            UiText.ResetLocalizationHealthForReload(); UiText.LoadUiLanguageFile(custom, map);
            Verify(UiText.Duplicates.Count == 1 && UiText.MalformedLines.Count == 1, "duplicate/malformed lines diagnosed");
            Verify(map["a"] == "replacement", "duplicate policy stays deterministic");
            File.WriteAllText(custom, "a\t leading \n", new UTF8Encoding(false)); map.Clear(); UiText.LoadUiLanguageFile(custom, map);
            Verify(map["a"] == " leading ", "authored separators preserved");
            File.WriteAllBytes(custom, new byte[] { 0x61, 0x09, 0xc3, 0x28 });
            bool rejected = false;
            try { UiText.LoadUiLanguageFile(custom, map); } catch (DecoderFallbackException) { rejected = true; }
            Verify(rejected && UiText.Utf8Failures.Count == 1, "invalid UTF8 rejected and diagnosed"); File.Delete(custom);

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
            SetUiLanguagePreference("简体中文", "test"); Verify(ResolveUiFont() == mixed && UiFonts.OwnedFontCount == 1, "mixed font reused");
            GameLanguage("ChineseSimp");
            TMP_FontAsset mixedLatin = null;
            for (int i = 0; i < latinLanguages.GetLength(0); i++)
            {
                SetUiLanguagePreference(latinLanguages[i, 0], "test");
                Verify(FindGameLanguageFont(GetUiLanguageSignature()) == latin, "Latin UI resolves exact game preset");
                TMP_FontAsset currentFont = ResolveUiFont();
                Verify(currentFont != latin && currentFont != han && currentFont.fallbackFontAssetTable.Contains(han), "Latin UI keeps Chinese game names readable");
                if (mixedLatin == null) mixedLatin = currentFont;
                Verify(currentFont == mixedLatin && UiFonts.OwnedFontCount == 2, "Latin languages reuse one private mixed font");
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
            Verify(UiFonts.OwnedFontCount == 12, "all script pairs reuse a bounded font cache");

            // A usable fallback is temporary when the selected language's preset
            // has not loaded yet. Both UI and game-name fonts must recover.
            List<MGSC.FontPreset> presets = MGSC.LocalizationFontKeeper.Instance.FontPresets;
            MGSC.FontPreset japanesePreset = presets.Find(p => p.FontAsset == kana);
            presets.Remove(japanesePreset);
            GameLanguage("Russian"); SetUiLanguagePreference("Japanese", "test");
            Verify(ResolveUiFont() == latin, "late UI font temporarily uses the game font");
            presets.Add(japanesePreset);
            UnityEngine.Time.frameCount++;
            Verify(ResolveUiFont() == latin, "late UI font retries are throttled");
            UnityEngine.Time.frameCount += 241;
            TMP_FontAsset recoveredUi = ResolveUiFont();
            Verify(recoveredUi != latin && recoveredUi.name.Contains(kana.name) && recoveredUi.fallbackFontAssetTable.Contains(latin),
                "late UI font recovers without changing language or restarting");

            MGSC.FontPreset chinesePreset = presets.Find(p => p.FontAsset == han);
            presets.Remove(chinesePreset);
            GameLanguage("ChineseSimp"); SetUiLanguagePreference("Korean", "test");
            TMP_FontAsset temporaryGame = ResolveUiFont();
            Verify(temporaryGame.fallbackFontAssetTable.Contains(latin), "late game font temporarily uses inspector fallback");
            presets.Add(chinesePreset);
            UnityEngine.Time.frameCount += 241;
            TMP_FontAsset recoveredGame = ResolveUiFont();
            Verify(recoveredGame.fallbackFontAssetTable.Contains(han) && recoveredGame != temporaryGame,
                "late game font recovers without changing language or restarting");

            // Session reset frees only owned assets, and a fresh resolver can recover.
            int ownedFonts = UiFonts.OwnedFontCount;
            UiFonts.Reset();
            Verify(UiFonts.OwnedFontCount == 0 && UnityEngine.Object.Destroyed.Count == ownedFonts, "session releases every private font");
            foreach (TMP_FontAsset shared in nativeFonts)
                Verify(!UnityEngine.Object.Destroyed.Contains(shared), "session never destroys a borrowed font");
            TMP_FontAsset afterReset = ResolveUiFont();
            Verify(afterReset != recoveredGame && afterReset.fallbackFontAssetTable.Contains(han), "font recreated after session reset");
            UiFonts.Reset(); UiFonts.Reset();
            Verify(UiFonts.OwnedFontCount == 0, "font reset is idempotent");

            UiText.Reset();
            Verify(UiText.ActiveCount == 0 && UiText.EnglishCount == 0 && UiText.ResolvedCount == 0 && UiText.MissingCount == 0,
                "localization owner clears its session state");
            Verify(Ui("tab.loot.short") == "전리품", "translations reload after session reset");
            var independent = new UiLocalization(() => dir, NormalizeGameText, message => {}, message => {});
            independent.EnsureLoaded("German", false);
            Verify(independent.GetText("tab.loot.short") == "BEUTE" && Ui("tab.loot.short") == "전리품", "localization instances do not share mutable caches");
            Verify(LanguageCatalog.All.Count == 11, "official game language count");
            foreach (UiLanguage language in LanguageCatalog.All)
            {
                Verify(LanguageCatalog.Find(language.Signature) == language && LanguageCatalog.Find(language.Label) == language, "registry canonical names round trip");
                Verify(LanguageCatalog.IsBuiltInFile(language.FileName), "registry owns built-in files");
                foreach (string alias in language.Aliases.Split(';'))
                    Verify(LanguageCatalog.Find(alias) == language, "registry alias resolves: " + alias);
            }

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
            var warnings = new List<string>();
            var reader = new RequiredDataReader((type, name) => FindCachedMember(type, name, false), warnings.Add);
            var owner = new RequiredReadFixture();
            Verify(reader.Read(owner, "Present").Status == MemberReadStatus.Success &&
                (int)reader.Read(owner, "Present").Value == 7, "required field read");
            Verify(reader.Read(owner, "Empty").Status == MemberReadStatus.Success &&
                reader.Read(owner, "Empty").Value == null, "null value is not a missing member");
            Verify(reader.Read(null, "Present").Status == MemberReadStatus.OwnerUnavailable && warnings.Count == 0, "unloaded owner is quiet");
            Verify(reader.Read(owner, "Missing").Status == MemberReadStatus.MissingMember, "missing member is identified");
            Verify(reader.Read(owner, "Broken").Status == MemberReadStatus.ReadFailed, "throwing getter is identified");
            for (int i = 0; i < 100; i++) reader.Read(owner, "Broken");
            Verify(warnings.Count == 2 && warnings[1].Contains("InvalidOperationException") && !warnings[1].Contains("secret"), "diagnostics deduplicate and omit values");
            for (int i = 0; i < 100; i++) reader.Read(owner, "Missing" + i);
            Verify(reader.DistinctFailures == 32 && reader.LimitReached && warnings.Count == 33, "diagnostics have bounded storage and output");
            reader.Reset();
            Verify(reader.DistinctFailures == 0 && !reader.LimitReached, "diagnostics reset at session boundary");
            reader.Read(owner, "Broken");
            Verify(warnings.Count == 34, "new session can report a recurring failure");
            return localizationChecks;
        }
        private class RequiredReadFixture
        {
            public int Present = 7;
            public object Empty { get { return null; } }
            public object Broken { get { throw new InvalidOperationException("secret"); } }
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
