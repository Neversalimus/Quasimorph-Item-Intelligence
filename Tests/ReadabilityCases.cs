// Platform fixtures. Real configuration, row stamps and text wrapping are extracted
// from production. Synthetic glyph advances test wrapping, not a game's font atlas.
namespace UnityEngine
{
    public enum KeyCode { F2 }
    public static class Application { public static string persistentDataPath; }
    public static class Debug
    { public static void Log(object value) {} public static void LogWarning(object value) {} public static void LogError(object value) {} }
    public struct Color
    {
        public float r, g, b, a;
        public Color(float red, float green, float blue, float alpha) { r = red; g = green; b = blue; a = alpha; }
        public static bool operator ==(Color x, Color y) { return x.Equals(y); }
        public static bool operator !=(Color x, Color y) { return !x.Equals(y); }
        public override bool Equals(object obj) { return obj is Color && r == ((Color)obj).r && g == ((Color)obj).g && b == ((Color)obj).b && a == ((Color)obj).a; }
        public override int GetHashCode() { return r.GetHashCode() ^ g.GetHashCode() ^ b.GetHashCode() ^ a.GetHashCode(); }
    }
    public struct Vector2 { public float x, y; public Vector2(float a, float b) { x = a; y = b; } }
    public class GameObject { public bool activeSelf; public void SetActive(bool value) { activeSelf = value; } }
}
namespace UnityEngine.UI
{
    public class Graphic { public Color color; public bool raycastTarget; }
    public class Selectable { public bool interactable; }
    public class Button : Selectable {}
}
namespace TMPro
{
    public enum FontStyles { Normal, Bold, Italic }
    public class TMP_Text : Graphic
    {
        public float fontSize;
        public bool enableWordWrapping, enableAutoSizing, ThrowOnMeasure;
        public FontStyles fontStyle;
        public Vector2 GetPreferredValues(string text, float width, float height)
        {
            if (ThrowOnMeasure) throw new InvalidOperationException("Metric fixture failure");
            float units = 0f;
            TextElementEnumerator elements = StringInfo.GetTextElementEnumerator(text);
            while (elements.MoveNext()) units += elements.GetTextElement()[0] >= 0x3000 ? 1f : 0.55f;
            return new Vector2(units * fontSize * (fontStyle == FontStyles.Bold ? 1.2f : 1f), fontSize * 1.4f);
        }
    }
}
namespace ItemIntelligence
{
    public static partial class ModMain
    {
        private const string Version = "fixture";
        private const float BrowserFullNoteWidth = 698f;
        private static BrowserViewportGeometry _browserViewport;
        private static bool BrowserExpanded;
        private static int BrowserWindowZoom = 100, BrowserExpandedZoom = 150, readabilityChecks, renderRequests;
        private static bool BrowserInterfaceIconLayoutEnabled { get { return ShowInterfaceIcons; } }
        private static void CheckReadability(bool value, string reason)
        { readabilityChecks++; if (!value) throw new Exception("Readability: " + reason); }
        private static string Ui(string key) { return key; }
        private static string NormalizeModUiText(string value) { return value; }
        private static int GetLanguageAwareWrapFallback(int ru, int en) { return Math.Min(ru, en); }
        private static void SetShowInspectorHint(bool value) { ShowInspectorHint = value; }
        private static void SetInspectorKey(string value, string origin) { InspectorKeyName = value; }
        private static void SetUiLanguagePreference(string value, string origin) { UiLanguagePreference = value; }
        private static void CloseInspector() { _inspectorOpen = false; }
        private static void HideHoverHint() {}
        private static void RefreshBrowserInterfaceIconSetting() {}
        private static void RenderBrowser(string itemId) { renderRequests++; }
        private static object GetMember(object value, string name)
        {
            var property = value.GetType().GetProperty(name);
            return property == null ? null : property.GetValue(value, null);
        }
        private sealed class WrappedBool { public bool Value { get; set; } }

        public static int RunReadabilityCases(string fixture, string localization)
        {
            Application.persistentDataPath = Path.Combine(fixture, "game");
            Directory.CreateDirectory(ConfigDirectory);
            File.WriteAllText(ConfigPath, "[Inspector]\nBrowserWindowZoom=125\nBrowserExpanded=true\n");
            EnsureConfigLoaded();
            CheckReadability(!EnhancedReadability && !VerboseLogging && BrowserWindowZoom == 125 && BrowserExpanded,
                "legacy config defaults to standard/normal logging without resetting viewport");
            ApplyConfigValue("enhancedreadability", true);
            ApplyConfigValue("verboselogging", true);
            CheckReadability(SaveConfig(), "save enhanced profile and verbose logging");
            EnhancedReadability = false; VerboseLogging = false; _configLoaded = false; EnsureConfigLoaded();
            CheckReadability(EnhancedReadability && VerboseLogging && BrowserWindowZoom == 125,
                "restart restores profile, verbose logging and zoom");
            File.WriteAllText(ConfigPath, "EnhancedReadability=invalid\n");
            _configLoaded = false; EnsureConfigLoaded();
            CheckReadability(EnhancedReadability, "invalid value does not silently disable preference");
            string feedback;
            _inspectorOpen = true; _inspectorItemId = "powder";
            CheckReadability(OnMcmConfigSaved(new Dictionary<string, object> {
                { "EnhancedReadability", new WrappedBool { Value = false } },
                { "VerboseLogging", new WrappedBool { Value = false } } }, out feedback), "MCM wrapper accepted");
            CheckReadability(!EnhancedReadability && !VerboseLogging && renderRequests == 1,
                "MCM refreshes existing page and applies normal logging");
            EnhancedReadability = true; _configLoaded = false; EnsureConfigLoaded();
            CheckReadability(!EnhancedReadability, "MCM persists standard on restart");
            OnMcmConfigSaved(new Dictionary<string, object> {
                { "EnhancedReadability", "true" }, { "VerboseLogging", "true" } }, out feedback);
            OnMcmConfigSaved(new Dictionary<string, object>(), out feedback);
            CheckReadability(EnhancedReadability && VerboseLogging && BrowserWindowZoom == 125 && BrowserExpanded,
                "missing MCM key preserves profile, logging mode and independent zoom");

            _browserHelpText = new TMP_Text();
            EnhancedReadability = false; ApplyBrowserHelpReadability();
            float standardHelp = _browserHelpText.fontSize;
            Color standardColor = _browserHelpText.color;
            float standardTab = GetBrowserInterfaceTabFontSize();
            EnhancedReadability = true; ApplyBrowserHelpReadability();
            CheckReadability(_browserHelpText.fontSize > standardHelp && _browserHelpText.color.g > standardColor.g,
                "existing footer responds to enhanced profile");
            CheckReadability(GetBrowserInterfaceTabFontSize() > standardTab && GetBrowserInterfaceTabFontSize() > BrowserHelpFontSize,
                "tab retains priority over supporting footer");
            EnhancedReadability = false; ApplyBrowserHelpReadability();
            CheckReadability(_browserHelpText.fontSize == standardHelp && _browserHelpText.color == standardColor,
                "standard footer restored on same object");

            BrowserRowRoots[0] = new GameObject(); BrowserRowRight[0] = new TMP_Text();
            TMP_Text measure = BrowserRowLeft[0] = new TMP_Text {
                fontSize = 18f, fontStyle = FontStyles.Bold, enableAutoSizing = true, enableWordWrapping = true };
            BrowserLine note = BrowserLine.FullNote("Note");
            CaptureBrowserRowRenderStamp(0, note, "en");
            CheckReadability(CanReuseBrowserRowRender(0, note, "en"), "unchanged row is reused");
            EnhancedReadability = true;
            CheckReadability(!CanReuseBrowserRowRender(0, note, "en"), "profile change rejects stale row style");
            CaptureBrowserRowRenderStamp(0, note, "en"); EnhancedReadability = false;
            CheckReadability(!CanReuseBrowserRowRender(0, note, "en"), "return to standard rejects enhanced cache");

            _browserViewport = CalculateBrowserViewport(1366, 768, false, 150, false);
            string word = new string('m', 90);
            List<string> standard = WrapBrowserFullWidthText(word, 100);
            EnhancedReadability = true;
            List<string> enhanced = WrapBrowserFullWidthText(word, 100);
            CheckReadability(standard.Count == 1 && enhanced.Count > standard.Count && string.Concat(enhanced) == word,
                "long words wrap at larger size without losing characters");
            CheckReadability(measure.fontSize == 18f && measure.fontStyle == FontStyles.Bold &&
                measure.enableAutoSizing && measure.enableWordWrapping, "measurement restores pooled text state");
            measure.ThrowOnMeasure = true;
            try { WrapBrowserFullWidthText("text", 100); } catch (InvalidOperationException) {}
            CheckReadability(measure.fontSize == 18f && measure.fontStyle == FontStyles.Bold &&
                measure.enableAutoSizing && measure.enableWordWrapping, "measurement failure also restores pooled state");
            measure.ThrowOnMeasure = false;

            string cjk = "成功截肢后的基础概率，不计入Magnum升级。汉😀字e\u0301汉字。";
            CheckReadability(string.Concat(WrapBrowserFullWidthText(cjk + cjk + cjk, 100)) == cjk + cjk + cjk,
                "CJK text elements preserved");
            BrowserLines.Clear();
            BrowserLine section = BrowserLine.FullSection("Section");
            BrowserLine action = BrowserLine.ItemAction("powder", "Open");
            BrowserLines.Add(section); BrowserLines.Add(BrowserLine.FullNote(word)); BrowserLines.Add(action);
            WrapBrowserNoteRows();
            CheckReadability(object.ReferenceEquals(BrowserLines[0], section) &&
                object.ReferenceEquals(BrowserLines[BrowserLines.Count - 1], action), "wrapping preserves surrounding rows and actions");
            int wrappedCount = BrowserLines.Count; WrapBrowserNoteRows();
            CheckReadability(BrowserLines.Count == wrappedCount, "normalizing prewrapped notes is idempotent");
            BrowserRowLeft[0] = null;
            List<string> fallbackRows = WrapBrowserFullWidthText(word, 100);
            CheckReadability(fallbackRows.Count > 1 && string.Concat(fallbackRows) == word,
                "no-font fallback preserves long text at the selected profile");
            BrowserRowLeft[0] = measure;

            // All shipped long notes, both profiles and both viewport modes. These
            // assertions concern the wrapping algorithm under declared metrics.
            foreach (string language in new string[] { "en", "ru", "de", "pl", "zh-Hans", "es", "pt-BR", "fr" })
            foreach (string entry in File.ReadAllLines(Path.Combine(localization, language + ".lang")))
            {
                int separator = entry.IndexOf('\t');
                if (separator < 0 || !entry.Substring(0, separator).Contains("note")) continue;
                string value = entry.Substring(separator + 1);
                for (int mode = 0; mode < 2; mode++)
                for (int profile = 0; profile < 2; profile++)
                {
                    EnhancedReadability = profile != 0;
                    _browserViewport = CalculateBrowserViewport(mode == 0 ? 1366 : 1920, mode == 0 ? 768 : 1080, mode != 0, 150, false);
                    List<string> rows = WrapBrowserFullWidthText(value, 100);
                    CheckReadability(string.Concat(rows).Replace(" ", "") == value.Replace(" ", ""), "all note content retained: " + language);
                    float limit = BrowserColumnCoordinate(BrowserFullNoteWidth, _browserViewport.Width) - 4f;
                    measure.fontSize = BrowserNoteFontSize; measure.fontStyle = FontStyles.Normal;
                    foreach (string row in rows)
                        CheckReadability(measure.GetPreferredValues(row, 4096f, 0f).x <= limit, "note fits declared width: " + language);
                }
            }
            return readabilityChecks;
        }
    }
}
