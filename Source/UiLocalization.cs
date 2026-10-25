using System;
using System.IO;
using System.Text;
using System.Globalization;
using System.Collections.Generic;

namespace ItemIntelligence
{
    internal sealed class UiLocalization
    {
        private readonly Func<string> directory;
        private readonly Func<string, string> normalize;
        private readonly Action<string> log, warn;
        private bool _allowForced;

        internal UiLocalization(Func<string> directory, Func<string, string> normalize,
            Action<string> log, Action<string> warn)
        {
            this.directory = directory; this.normalize = normalize;
            this.log = log; this.warn = warn;
        }

        internal string Language { get { return _externalUiTranslationLanguage; } }
        internal string FileName { get { return _externalUiTranslationFile; } }
        internal int EnglishCount { get { return EnglishUiFallback.Count; } }
        internal int ActiveCount { get { return ExternalUiTranslations.Count; } }
        internal int ResolvedCount { get { return ResolvedUiTextCache.Count; } }
        internal int MissingCount { get { return MissingUiTranslationKeys.Count; } }
        internal IEnumerable<string> EnglishKeys { get { return new List<string>(EnglishUiFallback.Keys); } }
        internal ICollection<string> MissingKeys { get { return new List<string>(MissingUiTranslationKeys).AsReadOnly(); } }
        internal IList<string> Duplicates { get { return LocalizationHealthDuplicateKeys.AsReadOnly(); } }
        internal IList<string> MalformedLines { get { return LocalizationHealthMalformedLines.AsReadOnly(); } }
        internal IList<string> Utf8Failures { get { return LocalizationHealthUtf8Failures.AsReadOnly(); } }
        internal bool HealthDirty { get { return _localizationHealthDirty; } }
        internal void AcknowledgeHealth() { _localizationHealthDirty = false; }
        internal string GetActiveValue(string key) { return ExternalUiTranslations[key]; }

        // Reload explicitly on configuration/session changes. No global caches are exposed.
        internal void Reset()
        {
            _externalUiTranslationLanguage = string.Empty;
            _externalUiTranslationFile = string.Empty;
            _externalUiEnglishLoaded = false;
            ExternalUiTranslations.Clear();
            EnglishUiFallback.Clear();
            ClearResolvedCache();
            ResetLocalizationHealthForReload();
        }

        internal void ClearResolvedCache()
        {
            ResolvedUiTextCache.Clear();
            MissingUiTranslationKeys.Clear();
        }

        // QII-authored UI text is key-based; vanilla entity names still come from
        // Quasimorph's localization service.
        private readonly Dictionary<string, string> ExternalUiTranslations =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, string> EnglishUiFallback =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> MissingUiTranslationKeys =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, string> ResolvedUiTextCache =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private string _externalUiTranslationLanguage = string.Empty;
        private string _externalUiTranslationFile = string.Empty;
        private bool _externalUiEnglishLoaded;
        private readonly List<string> LocalizationHealthDuplicateKeys = new List<string>();
        private readonly List<string> LocalizationHealthMalformedLines = new List<string>();
        private readonly List<string> LocalizationHealthUtf8Failures = new List<string>();
        private bool _localizationHealthDirty = true;

        internal void LoadUiLanguageFile(string path, Dictionary<string, string> target)
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

        internal string ReadUiLanguageDeclaration(string path)
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

        internal bool ReadUiForceDeclaration(string path)
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

        internal bool EnsureLoaded(string language, bool auto)
        {
            if (string.Equals(language, _externalUiTranslationLanguage, StringComparison.OrdinalIgnoreCase) &&
                _externalUiEnglishLoaded && auto == _allowForced) return false;
            _allowForced = auto;

            _externalUiTranslationLanguage = language ?? string.Empty;
            _externalUiTranslationFile = string.Empty;
            ExternalUiTranslations.Clear();
            MissingUiTranslationKeys.Clear();
            ResolvedUiTextCache.Clear();
            ResetLocalizationHealthForReload();

            try
            {
                string localizationDir = directory();
                if (string.IsNullOrEmpty(localizationDir) || !Directory.Exists(localizationDir)) return false;

                if (!_externalUiEnglishLoaded)
                {
                    EnglishUiFallback.Clear();
                    LoadUiLanguageFile(Path.Combine(localizationDir, "en.lang"), EnglishUiFallback);
                    _externalUiEnglishLoaded = EnglishUiFallback.Count > 0;
                }

                string[] files = Directory.GetFiles(localizationDir, "*.lang");
                Array.Sort(files, StringComparer.Ordinal);
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
                    bool builtIn = LanguageCatalog.IsBuiltInFile(fileName);
                    if (builtIn)
                    {
                        if (LanguageCatalog.MatchesDeclaration(language, declared)) builtInPath = files[i];
                        continue;
                    }

                    if (LanguageCatalog.MatchesDeclaration(language, declared))
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
                else if (forcedCount == 1 && auto) activePath = forcedPath;
                else activePath = builtInPath;

                if (!string.IsNullOrEmpty(activePath))
                {
                    LoadUiLanguageFile(activePath, ExternalUiTranslations);
                    _externalUiTranslationFile = Path.GetFileName(activePath);
                }

                log("[ItemIntelligence] UI localization: language=" + language +
                    ", file=" + (string.IsNullOrEmpty(_externalUiTranslationFile) ? "<english fallback>" : _externalUiTranslationFile) +
                    ", entries=" + ExternalUiTranslations.Count.ToString(CultureInfo.InvariantCulture) +
                    ", english=" + EnglishUiFallback.Count.ToString(CultureInfo.InvariantCulture) + ".");

            }
            catch (Exception ex)
            {
                warn("[ItemIntelligence] External UI localization skipped: " + ex.Message);
            }
            return true;
        }

        internal string GetText(string key)
        {
            if (string.IsNullOrEmpty(key)) return string.Empty;

            string cached;
            if (ResolvedUiTextCache.TryGetValue(key, out cached))
                return cached;

            string value;
            if (ExternalUiTranslations.TryGetValue(key, out value) && !string.IsNullOrEmpty(value))
            {
                value = normalize(value);
                ResolvedUiTextCache[key] = value;
                return value;
            }

            if (EnglishUiFallback.TryGetValue(key, out value) && !string.IsNullOrEmpty(value))
            {
                bool english = LanguageCatalog.Find(_externalUiTranslationLanguage) == LanguageCatalog.All[0];
                if (!english && MissingUiTranslationKeys.Add(key))
                {
                    warn("[ItemIntelligence] Missing UI translation key for active language: " + key);
                    MarkLocalizationHealthDirty();
                }
                value = normalize(value);
                ResolvedUiTextCache[key] = value;
                return value;
            }

            if (MissingUiTranslationKeys.Add(key))
            {
                warn("[ItemIntelligence] Missing UI localization key: " + key);
                MarkLocalizationHealthDirty();
            }
            value = "[" + key + "]";
            ResolvedUiTextCache[key] = value;
            return value;
        }

        private string[] ReadUtf8LinesStrict(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return new string[0];
            try
            {
                UTF8Encoding strict = new UTF8Encoding(false, true);
                string text = File.ReadAllText(path, strict);
                text = text.Replace("\r\n", "\n").Replace('\r', '\n');
                return text.Split(new char[] { '\n' });
            }
            catch (DecoderFallbackException ex)
            {
                RecordLocalizationUtf8Failure(path, ex.Message);
                throw;
            }
        }

        internal void ResetLocalizationHealthForReload()
        {
            LocalizationHealthDuplicateKeys.Clear();
            LocalizationHealthMalformedLines.Clear();
            LocalizationHealthUtf8Failures.Clear();
            _localizationHealthDirty = true;
        }

        private void RecordLocalizationDuplicateKey(string path, string key)
        {
            string value = Path.GetFileName(path ?? string.Empty) + ":" + (key ?? string.Empty);
            if (!LocalizationHealthDuplicateKeys.Contains(value))
                LocalizationHealthDuplicateKeys.Add(value);
            _localizationHealthDirty = true;
            warn("[ItemIntelligence] Localization duplicate key: " + value);
        }

        private void RecordLocalizationMalformedLine(string path, int line)
        {
            string value = Path.GetFileName(path ?? string.Empty) + ":line " + line.ToString(CultureInfo.InvariantCulture);
            if (!LocalizationHealthMalformedLines.Contains(value))
                LocalizationHealthMalformedLines.Add(value);
            _localizationHealthDirty = true;
            warn("[ItemIntelligence] Localization malformed line: " + value);
        }

        private void RecordLocalizationUtf8Failure(string path, string detail)
        {
            string value = Path.GetFileName(path ?? string.Empty) + ":" + (detail ?? "invalid UTF-8");
            if (!LocalizationHealthUtf8Failures.Contains(value))
                LocalizationHealthUtf8Failures.Add(value);
            _localizationHealthDirty = true;
            warn("[ItemIntelligence] Localization file is not valid UTF-8: " + value);
        }

        private void MarkLocalizationHealthDirty()
        {
            _localizationHealthDirty = true;
        }

        internal bool HasLocalizationHealthProblems()
        {
            return MissingUiTranslationKeys.Count > 0 ||
                LocalizationHealthDuplicateKeys.Count > 0 ||
                LocalizationHealthMalformedLines.Count > 0 ||
                LocalizationHealthUtf8Failures.Count > 0;
        }

    }
}
