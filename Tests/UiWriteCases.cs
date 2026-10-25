// Every fixture setter counts writes, including redundant ones. The guards under
// test are the whole production file; these fixtures do not suppress assignments.
namespace UnityEngine
{
    public class Object
    {
        public bool Destroyed;
        private readonly System.Collections.Generic.Dictionary<string, int> writes = new System.Collections.Generic.Dictionary<string, int>();
        protected void Write<T>(ref T field, T value, string property)
        { field = value; writes[property] = Writes(property) + 1; }
        public int Writes(string property) { int count; return writes.TryGetValue(property, out count) ? count : 0; }
        public static bool operator ==(Object a, Object b)
        {
            bool leftNull = ReferenceEquals(a, null) || a.Destroyed;
            bool rightNull = ReferenceEquals(b, null) || b.Destroyed;
            return leftNull || rightNull ? leftNull && rightNull : ReferenceEquals(a, b);
        }
        public static bool operator !=(Object a, Object b) { return !(a == b); }
        public override bool Equals(object other) { return ReferenceEquals(this, other); }
        public override int GetHashCode() { return base.GetHashCode(); }
    }
    public class GameObject : Object
    {
        private bool active;
        public bool activeSelf { get { return active; } }
        public void SetActive(bool value) { Write(ref active, value, "activeSelf"); }
    }
    public class Behaviour : Object
    {
        private bool isEnabled;
        public bool enabled { get { return isEnabled; } set { Write(ref isEnabled, value, "enabled"); } }
    }
    public class Sprite : Object {}
    public struct Vector2
    {
        public float x, y;
        public Vector2(float a, float b) { x = a; y = b; }
        public static bool operator ==(Vector2 a, Vector2 b) { float x = a.x - b.x, y = a.y - b.y; return x*x + y*y < 9.99999944e-11f; }
        public static bool operator !=(Vector2 a, Vector2 b) { return !(a == b); }
        public override bool Equals(object obj) { return obj is Vector2 && x == ((Vector2)obj).x && y == ((Vector2)obj).y; }
        public override int GetHashCode() { return x.GetHashCode() ^ y.GetHashCode(); }
    }
    public struct Color
    {
        public float r, g, b, a;
        public Color(float red, float green, float blue, float alpha) { r = red; g = green; b = blue; a = alpha; }
        public static bool operator ==(Color x, Color y)
        { float r=x.r-y.r, g=x.g-y.g, b=x.b-y.b, a=x.a-y.a; return r*r + g*g + b*b + a*a < 9.99999944e-11f; }
        public static bool operator !=(Color x, Color y) { return !(x == y); }
        public override bool Equals(object obj) { return obj is Color && r == ((Color)obj).r && g == ((Color)obj).g && b == ((Color)obj).b && a == ((Color)obj).a; }
        public override int GetHashCode() { return r.GetHashCode() ^ g.GetHashCode() ^ b.GetHashCode() ^ a.GetHashCode(); }
    }
    public class RectTransform : Object
    {
        private Vector2 position, size;
        public Vector2 anchoredPosition { get { return position; } set { Write(ref position, value, "anchoredPosition"); } }
        public Vector2 sizeDelta { get { return size; } set { Write(ref size, value, "sizeDelta"); } }
    }
}
namespace UnityEngine.UI
{
    public class Selectable : Behaviour
    {
        private bool canInteract;
        public bool interactable { get { return canInteract; } set { Write(ref canInteract, value, "interactable"); } }
    }
    public class Graphic : Behaviour
    {
        private bool raycast;
        private Color tint;
        public bool raycastTarget { get { return raycast; } set { Write(ref raycast, value, "raycastTarget"); } }
        public Color color { get { return tint; } set { Write(ref tint, value, "color"); } }
    }
    public class Image : Graphic
    {
        private Sprite picture;
        public Sprite sprite { get { return picture; } set { Write(ref picture, value, "sprite"); } }
    }
    public class Outline : Behaviour
    {
        private Color tint;
        private Vector2 distance;
        public Color effectColor { get { return tint; } set { Write(ref tint, value, "effectColor"); } }
        public Vector2 effectDistance { get { return distance; } set { Write(ref distance, value, "effectDistance"); } }
    }
}
namespace TMPro
{
    public enum FontStyles { Normal, Bold, Italic }
    public enum TextOverflowModes { Overflow, Ellipsis }
    public enum TextAlignmentOptions { Left, Right }
    public class TMP_Text : Graphic
    {
        private string content = "";
        private float size, min, max;
        private bool autoSize, wrap;
        private FontStyles style;
        private TextOverflowModes overflow;
        private TextAlignmentOptions align;
        public string text { get { return content; } set { Write(ref content, value, "text"); } }
        public float fontSize { get { return size; } set { Write(ref size, value, "fontSize"); } }
        public float fontSizeMin { get { return min; } set { Write(ref min, value, "fontSizeMin"); } }
        public float fontSizeMax { get { return max; } set { Write(ref max, value, "fontSizeMax"); } }
        public bool enableAutoSizing { get { return autoSize; } set { Write(ref autoSize, value, "enableAutoSizing"); } }
        public bool enableWordWrapping { get { return wrap; } set { Write(ref wrap, value, "enableWordWrapping"); } }
        public FontStyles fontStyle { get { return style; } set { Write(ref style, value, "fontStyle"); } }
        public TextOverflowModes overflowMode { get { return overflow; } set { Write(ref overflow, value, "overflowMode"); } }
        public TextAlignmentOptions alignment { get { return align; } set { Write(ref align, value, "alignment"); } }
    }
}
namespace ItemIntelligence
{
    public static partial class ModMain
    {
        private static int uiWriteChecks;
        private static void CheckWrite(bool value, string reason)
        { uiWriteChecks++; if (!value) throw new Exception("UI write guard: " + reason); }
        private static void CheckSetter<TTarget, TValue>(string property, Action<TTarget, TValue> setter,
            Func<TTarget, TValue> getter, TValue changed) where TTarget : UnityEngine.Object, new()
        {
            TTarget target = new TTarget();
            TValue original = getter(target);
            setter(target, original);
            CheckWrite(target.Writes(property) == 0, property + ": unchanged initial value");
            setter(target, changed);
            CheckWrite(target.Writes(property) == 1 && System.Collections.Generic.EqualityComparer<TValue>.Default.Equals(getter(target), changed),
                property + ": changed value applied once");
            for (int i = 0; i < 1000; i++) setter(target, changed);
            CheckWrite(target.Writes(property) == 1, property + ": warm redraws do not write");
            setter(target, original);
            CheckWrite(target.Writes(property) == 2 && System.Collections.Generic.EqualityComparer<TValue>.Default.Equals(getter(target), original),
                property + ": returning to previous value is a real change");
            TTarget another = new TTarget(); setter(another, changed);
            CheckWrite(another.Writes(property) == 1, property + ": independent target");
            setter(null, changed);
            target.Destroyed = true; setter(target, changed);
            CheckWrite(target.Writes(property) == 2, property + ": missing or destroyed target ignored");
        }
        public static int RunUiWriteCases()
        {
            uiWriteChecks = 0;
            CheckSetter<GameObject, bool>("activeSelf", SetBrowserActiveIfChanged, x => x.activeSelf, true);
            CheckSetter<TMP_Text, string>("text", SetBrowserTextIfChanged, x => x.text, "日本語 / 한국어 / Türkçe / Русский");
            CheckSetter<RectTransform, Vector2>("anchoredPosition", (x,v) => SetBrowserRectPositionIfChanged(x,v.x,v.y), x => x.anchoredPosition, new Vector2(12, -34));
            CheckSetter<RectTransform, Vector2>("sizeDelta", (x,v) => SetBrowserRectSizeIfChanged(x,v.x,v.y), x => x.sizeDelta, new Vector2(300, 48));
            CheckSetter<TMP_Text, float>("fontSize", SetBrowserFontSizeIfChanged, x => x.fontSize, 18);
            CheckSetter<TMP_Text, FontStyles>("fontStyle", SetBrowserFontStyleIfChanged, x => x.fontStyle, FontStyles.Bold);
            CheckSetter<TMP_Text, bool>("enableAutoSizing", SetBrowserAutoSizingIfChanged, x => x.enableAutoSizing, true);
            CheckSetter<TMP_Text, float>("fontSizeMin", SetBrowserFontSizeMinIfChanged, x => x.fontSizeMin, 12);
            CheckSetter<TMP_Text, float>("fontSizeMax", SetBrowserFontSizeMaxIfChanged, x => x.fontSizeMax, 24);
            CheckSetter<TMP_Text, bool>("enableWordWrapping", SetBrowserWordWrappingIfChanged, x => x.enableWordWrapping, true);
            CheckSetter<TMP_Text, TextOverflowModes>("overflowMode", SetBrowserOverflowIfChanged, x => x.overflowMode, TextOverflowModes.Ellipsis);
            CheckSetter<Selectable, bool>("interactable", SetBrowserInteractableIfChanged, x => x.interactable, true);
            CheckSetter<Graphic, bool>("raycastTarget", SetBrowserRaycastTargetIfChanged, x => x.raycastTarget, true);
            CheckSetter<Graphic, Color>("color", SetBrowserGraphicColorIfChanged, x => x.color, new Color(.2f, .4f, .6f, 1));
            CheckSetter<Image, Sprite>("sprite", SetBrowserImageSpriteIfChanged, x => x.sprite, new Sprite());
            CheckSetter<Image, bool>("enabled", SetBrowserImageEnabledIfChanged, x => x.enabled, true);
            CheckSetter<TMP_Text, TextAlignmentOptions>("alignment", SetBrowserAlignmentIfChanged, x => x.alignment, TextAlignmentOptions.Right);
            CheckSetter<Outline, bool>("enabled", SetBrowserOutlineEnabledIfChanged, x => x.enabled, true);
            CheckSetter<Outline, Color>("effectColor", SetBrowserOutlineColorIfChanged, x => x.effectColor, new Color(.1f, .2f, .3f, 1));
            CheckSetter<Outline, Vector2>("effectDistance", SetBrowserOutlineDistanceIfChanged, x => x.effectDistance, new Vector2(-2, 2));
            TMP_Text text = new TMP_Text();
            SetBrowserTextIfChanged(text, null);
            CheckWrite(text.text == "" && text.Writes("text") == 0, "null text normalized without a redundant empty write");
            SetBrowserTextIfChanged(text, new string('x', 12)); SetBrowserTextIfChanged(text, new string('x', 12));
            CheckWrite(text.Writes("text") == 1, "equal text on different string instances avoids a write");
            SetBrowserTextIfChanged(text, null);
            CheckWrite(text.text == "" && text.Writes("text") == 2, "null clears previous text");
            CheckWrite(NormalizeGameText(null) == "" && NormalizeGameText("") == "", "empty game text");
            CheckWrite(NormalizeGameText("Ёж и ёлка: Ёё") == "Еж и елка: Ее", "both Russian yo cases normalized");
            foreach (string value in new string[] { "Plain text", "日本語の説明", "한국어 설명", "Türkçe ıİ", "Кириллица без замены", "" })
                CheckWrite(ReferenceEquals(NormalizeGameText(value), value), "unmodified game text reuses the original string");
            return uiWriteChecks;
        }
    }
}
