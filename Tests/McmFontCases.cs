namespace UnityEngine
{
    public enum HideFlags { HideAndDontSave }
    public static class Time { public static int frameCount; }
    public static class Debug
    {
        public static int Warnings;
        public static void Log(object value) { }
        public static void LogWarning(object value) { Warnings++; }
    }
    public class Object
    {
        public virtual string name { get; set; }
        public static T Instantiate<T>(T value) where T : TMPro.TMP_FontAsset
        { return (T)value.Copy(); }
    }
    public class Component : Object
    {
        public GameObject gameObject;
        public Transform transform { get { return gameObject.transform; } }
        public override string name { get { return gameObject.name; } set { gameObject.name = value; } }
        public T GetComponent<T>() where T : Component { return gameObject.GetComponent<T>(); }
        public T GetComponentInParent<T>() where T : Component
        {
            for (Transform at = transform; at != null; at = at.parent)
            { T value = at.GetComponent<T>(); if (value != null) return value; }
            return null;
        }
        public T[] GetComponentsInChildren<T>(bool includeInactive) where T : Component
        { return gameObject.GetComponentsInChildren<T>(includeInactive); }
    }
    public class Transform : Component
    {
        public Transform parent;
        public readonly List<Transform> Children = new List<Transform>();
        public int childCount { get { return Children.Count; } }
        public Transform GetChild(int i) { return Children[i]; }
        public void SetParent(Transform other)
        {
            if (parent != null) parent.Children.Remove(this);
            parent = other;
            if (parent != null) parent.Children.Add(this);
        }
    }
    public class RectTransform : Transform { }
    public class MonoBehaviour : Component
    {
        private bool _enabled = true;
        public bool enabled
        {
            get { return _enabled; }
            set
            {
                if (_enabled == value) return;
                _enabled = value;
                if (gameObject.activeInHierarchy) Dispatch(value ? "OnEnable" : "OnDisable");
            }
        }
        internal void Dispatch(string method)
        {
            MethodInfo info = GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (info != null) info.Invoke(this, null);
        }
    }
    public class GameObject : Object
    {
        public static int TreeScans;
        private readonly List<Component> components = new List<Component>();
        public readonly RectTransform transform;
        public bool activeSelf = true;
        public bool activeInHierarchy { get { return activeSelf && (transform.parent == null || transform.parent.gameObject.activeInHierarchy); } }
        public GameObject(string label)
        {
            name = label;
            transform = new RectTransform { gameObject = this };
            components.Add(transform);
        }
        public T AddComponent<T>() where T : Component
        {
            T value = (T)Activator.CreateInstance(typeof(T), true);
            value.gameObject = this;
            components.Add(value);
            MonoBehaviour behaviour = value as MonoBehaviour;
            if (behaviour != null && activeInHierarchy) behaviour.Dispatch("OnEnable");
            return value;
        }
        public T GetComponent<T>() where T : Component
        { foreach (Component c in components) if (c is T) return (T)c; return null; }
        public T[] GetComponentsInChildren<T>(bool includeInactive) where T : Component
        {
            TreeScans++;
            var result = new List<T>();
            Collect(result, includeInactive);
            return result.ToArray();
        }
        private void Collect<T>(List<T> result, bool includeInactive) where T : Component
        {
            if (!includeInactive && !activeInHierarchy) return;
            foreach (Component c in components) if (c is T) result.Add((T)c);
            foreach (Transform child in transform.Children) child.gameObject.Collect(result, includeInactive);
        }
        public void SetActive(bool active)
        {
            if (activeSelf == active) return;
            if (active) { activeSelf = true; Events("OnEnable"); }
            else { Events("OnDisable"); activeSelf = false; }
        }
        private void Events(string method)
        {
            foreach (Component c in components)
            { MonoBehaviour b = c as MonoBehaviour; if (b != null && b.enabled) b.Dispatch(method); }
            foreach (Transform child in transform.Children)
                if (child.gameObject.activeSelf) child.gameObject.Events(method);
        }
        public void Frame()
        {
            Time.frameCount++;
            if (activeInHierarchy) Events("LateUpdate");
        }
    }
}
namespace TMPro
{
    public class TMP_FontAsset : UnityEngine.Object
    {
        public UnityEngine.HideFlags hideFlags;
        public string Glyphs = string.Empty;
        public List<TMP_FontAsset> fallbackFontAssetTable = new List<TMP_FontAsset>();
        public TMP_FontAsset Copy()
        { return new TMP_FontAsset { name = name, Glyphs = Glyphs, fallbackFontAssetTable = fallbackFontAssetTable }; }
        public bool Has(char ch)
        {
            if (Glyphs.IndexOf(ch) >= 0) return true;
            if (fallbackFontAssetTable != null)
                foreach (var fallback in fallbackFontAssetTable) if (fallback != null && fallback.Has(ch)) return true;
            return false;
        }
    }
    public class TMP_Text : UnityEngine.Component
    {
        public string text;
        private TMP_FontAsset _font;
        public int Writes;
        public TMP_FontAsset font { get { return _font; } set { _font = value; Writes++; } }
    }
    public class TMP_Dropdown : UnityEngine.Component { public UnityEngine.RectTransform template; }
}
namespace HarmonyLib { public class Harmony { public Harmony(string id) { } } }
namespace ItemIntelligence
{
    public static partial class ModMain
    {
        private const string HarmonyId = "QII.Fixture";
        private static int mcmChecks, fixturePatchCount = 1, fontLookups;
        private static readonly Dictionary<string, TMP_FontAsset> FixtureFonts = new Dictionary<string, TMP_FontAsset>();
        private static object GetMember(object value, string name)
        {
            if (value == null) return null;
            const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
            FieldInfo field = value.GetType().GetField(name, flags);
            if (field != null) return field.GetValue(value);
            PropertyInfo property = value.GetType().GetProperty(name, flags);
            return property == null ? null : property.GetValue(value, null);
        }
        private static int PatchNamedMethods(Harmony harmony, string type, string method, string prefix, string postfix)
        { return fixturePatchCount; }
        private static TMP_FontAsset FindGameLanguageFont(string language)
        { fontLookups++; TMP_FontAsset value; return FixtureFonts.TryGetValue(language, out value) ? value : null; }
        private sealed class McmData { public string ModName { get; set; } }
        private sealed class McmMenu { public Transform ModListRoot { get; set; } }
        private sealed class Hover : Component { public Component _tooltip { get; set; } }
        private static void CheckMcm(bool okay, string reason)
        { mcmChecks++; if (!okay) throw new Exception("MCM font regression: " + reason); }
        private static GameObject Child(GameObject parent, string name)
        { var child = new GameObject(name); child.transform.SetParent(parent.transform); return child; }
        private static TMP_Text Label(GameObject parent, string name, TMP_FontAsset font, string value)
        { var label = Child(parent, name).AddComponent<TMP_Text>(); label.font = font; label.text = value; return label; }
        private static void ResetMcmFixture()
        {
            _mcmRegisteredName = string.Empty;
            McmMixedFonts.Clear(); McmOriginalFonts.Clear(); FixtureFonts.Clear();
            _mcmHanFont = _mcmLatinFont = null;
            _mcmFontRetryFrame = 0; _mcmFontWarningLogged = _mcmFontReadyLogged = false;
            fixturePatchCount = 1; fontLookups = 0; Time.frameCount = 1000; Debug.Warnings = 0;
        }

        public static int RunMcmFontCases()
        {
            mcmChecks = 0;
            ResetMcmFixture();
            var latin = new TMP_FontAsset { name = "NotoLatin", Glyphs = "Item / ChineseРусскийDeutschPolski" };
            var han = new TMP_FontAsset { name = "NotoSC", Glyphs = "物品语言简体中文" };
            var symbols = new TMP_FontAsset { name = "ExistingSymbols", Glyphs = "*" };
            latin.fallbackFontAssetTable.Add(symbols);
            var originalFallbackList = latin.fallbackFontAssetTable;
            FixtureFonts["ChineseSimplified"] = han; FixtureFonts["EnglishUS"] = latin;
            InstallMcmFontSupport("Item Intelligence - 物品");
            CheckMcm(McmMixedFonts.Count == 0, "registration never changes fonts or opens MCM");

            var foreign = new GameObject("foreign");
            var foreignText = Label(foreign, "label", latin, "Other mod");
            McmConfigBuiltPostfix(new McmData { ModName = "Item Intelligence - 物品 OTHER" }, foreign.transform);
            CheckMcm(foreign.GetComponent<QiiMcmFontScope>() == null && foreignText.font == latin, "exact ownership, no prefix match");
            McmConfigBuiltPostfix(null, foreign.transform);
            McmConfigBuiltPostfix(new McmData { ModName = _mcmRegisteredName }, null);
            CheckMcm(foreignText.font == latin, "null and foreign config leave UI untouched");

            var own = new GameObject("own");
            var ownText = Label(own, "label", latin, "物品语言");
            var dropdown = Child(own, "language").AddComponent<TMP_Dropdown>();
            dropdown.template = Child(dropdown.gameObject, "Template").transform;
            dropdown.template.gameObject.SetActive(false);
            var optionTemplate = Label(dropdown.template.gameObject, "option", latin, "简体中文 / Chinese");
            CheckMcm(!ownText.font.Has('物'), "fixture reproduces missing Han glyph before restart registration");
            McmConfigBuiltPostfix(new McmData { ModName = _mcmRegisteredName }, own.transform);
            var mixed = ownText.font;
            CheckMcm(mixed != latin && mixed.Has('物') && mixed.Has('Я') == latin.Has('Я'), "private font supplies Han while preserving primary glyphs");
            CheckMcm(ownText.text == "物品语言", "font repair does not replace or translate captions");
            CheckMcm(ReferenceEquals(originalFallbackList, latin.fallbackFontAssetTable) && originalFallbackList.Count == 1, "original list identity and contents unchanged");
            CheckMcm(!ReferenceEquals(mixed.fallbackFontAssetTable, originalFallbackList) && mixed.Has('*'), "private list preserves existing fallback");
            CheckMcm(han.fallbackFontAssetTable.Count == 0 && foreignText.font == latin, "other mod and Han asset unchanged");
            CheckMcm(optionTemplate.font.Has('简') && dropdown.template.GetComponent<QiiMcmFontScope>() != null, "inactive dropdown template is marked and repaired");
            own.Frame();
            int scans = GameObject.TreeScans, writes = ownText.Writes, lookups = fontLookups;
            for (int i = 0; i < 100; i++) own.Frame();
            CheckMcm(GameObject.TreeScans == scans && ownText.Writes == writes && fontLookups == lookups, "steady frames use cached labels and fonts without redundant writes");

            // Simulate a dropdown clone reparented to another canvas, with entries
            // populated after activation, as TMP does when opening the options.
            var popup = new GameObject("Dropdown List");
            popup.AddComponent<QiiMcmFontScope>();
            var popupText = Label(popup, "late option", latin, "简体中文 / Chinese");
            popup.Frame();
            CheckMcm(popupText.font.Has('简'), "late-created options repaired under detached template scope");
            popupText.font = latin; popup.Frame();
            CheckMcm(popupText.font == mixed, "MCM font synchronizer overwrite repaired before next render");
            popup.SetActive(false);
            CheckMcm(popupText.font == latin, "dropdown releases its font on disable");

            for (int i = 0; i < 5; i++)
            {
                own.SetActive(false);
                CheckMcm(ownText.font == latin, "closed menu restores source font " + i);
                own.SetActive(true); own.Frame();
                CheckMcm(ownText.font.Has('语') && ownText.font == mixed, "reopen keeps Chinese captions readable " + i);
            }
            CheckMcm(McmMixedFonts.Count == 1, "reopening does not clone fonts indefinitely");

            var menuList = new GameObject("mods");
            var ourButton = Child(menuList, "[" + _mcmRegisteredName.Replace(" ", "") + "]");
            var ourButtonText = Label(ourButton, "name", latin, _mcmRegisteredName);
            var otherButton = Child(menuList, "[OtherMod]");
            var otherButtonText = Label(otherButton, "name", latin, "Other");
            McmButtonsBuiltPostfix(new McmMenu { ModListRoot = menuList.transform });
            CheckMcm(ourButtonText.font.Has('物') && otherButtonText.font == latin, "only our sidebar title receives fallback");

            var tooltip = new GameObject("shared tooltip");
            var tooltipText = Label(tooltip, "text", latin, "物品");
            tooltip.SetActive(false);
            var ourHover = Child(own, "hover").AddComponent<Hover>(); ourHover._tooltip = tooltip.transform;
            var foreignHover = Child(foreign, "hover").AddComponent<Hover>(); foreignHover._tooltip = tooltip.transform;
            McmHoverFontPrefix(ourHover); tooltip.SetActive(true); tooltip.Frame();
            CheckMcm(tooltipText.font.Has('物') && tooltip.GetComponent<QiiMcmFontScope>().enabled, "our hover owns the shared tooltip");
            McmHoverFontPrefix(foreignHover);
            CheckMcm(tooltipText.font == latin && !tooltip.GetComponent<QiiMcmFontScope>().enabled, "other hover restores shared tooltip before replacing text");
            tooltipText.font = symbols; tooltip.Frame();
            CheckMcm(tooltipText.font == symbols, "released scope cannot overwrite another mod's font");
            McmHoverFontPrefix(null);
            McmHoverFontPrefix(new GameObject("empty hover").AddComponent<Hover>());
            CheckMcm(tooltipText.font == symbols, "missing tooltip remains a no-op");

            var chinesePrimary = Label(own, "Chinese-game option", han, "Русский / 物品");
            own.GetComponent<QiiMcmFontScope>().RefreshTexts();
            CheckMcm(chinesePrimary.font != han && chinesePrimary.font.Has('Р') && chinesePrimary.font.Has('物'), "Chinese game font receives Latin/Cyrillic options");
            CheckMcm(chinesePrimary.font.fallbackFontAssetTable.Count == 1, "no self-fallback or duplicate assets");
            var changedByGame = new TMP_FontAsset { name = "New game font", Glyphs = "N" };
            ownText.font = changedByGame;
            own.GetComponent<QiiMcmFontScope>().RestoreFonts();
            CheckMcm(ownText.font == changedByGame, "restoration respects a newer external font assignment");
            ApplyMcmFonts(null); RestoreMcmFonts(null);
            ApplyMcmFonts(new TMP_Text[] { null }); RestoreMcmFonts(new TMP_Text[] { null });
            CheckMcm(Debug.Warnings == 0, "valid lifecycle has no warning");

            ResetMcmFixture();
            var delayed = Label(new GameObject("pending"), "label", latin, "物品");
            ApplyMcmFonts(new[] { delayed });
            CheckMcm(delayed.font == latin, "missing font keeper does not install an incomplete clone");
            lookups = fontLookups;
            for (int i = 0; i < 50; i++) { Time.frameCount++; ApplyMcmFonts(new[] { delayed }); }
            CheckMcm(fontLookups == lookups, "missing font lookup is throttled");
            FixtureFonts["ChineseSimplified"] = han; FixtureFonts["EnglishUS"] = latin;
            Time.frameCount += 120; ApplyMcmFonts(new[] { delayed });
            CheckMcm(delayed.font.Has('物'), "late font keeper becomes available without restart");
            fixturePatchCount = 0;
            InstallMcmFontSupport("Item Intelligence"); InstallMcmFontSupport("Item Intelligence");
            CheckMcm(Debug.Warnings == 1, "unsupported optional MCM hooks warn once without breaking registration");
            return mcmChecks;
        }
    }
}
