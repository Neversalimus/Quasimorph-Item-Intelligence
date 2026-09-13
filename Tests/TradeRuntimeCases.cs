namespace UnityEngine
{
    public class GameObject { public bool activeInHierarchy = true; }
    public class Component { public GameObject gameObject = new GameObject(); }
    public static class Time { public static float unscaledDeltaTime = 0.01f; }
    public static class Debug
    {
        public static readonly System.Collections.Generic.List<string> Messages = new System.Collections.Generic.List<string>();
        public static void Log(string message) { Messages.Add(message); }
    }
}
namespace MGSC
{
    public class SingletonMonoBehaviour<T> { public static T Instance; }
    public class UI : UnityEngine.Component
    {
        public Type CurrentView;
        public System.Collections.IDictionary _instantiatedViews = new System.Collections.Hashtable();
        public static Type DefaultView { get { return SingletonMonoBehaviour<UI>.Instance.CurrentView; } }
    }
    public class SpaceHudScreen : UnityEngine.Component { }
    public class MissionScreen : UnityEngine.Component { }
}
namespace ItemIntelligence
{
    public static partial class ModMain
    {
        private static int _assertions, _vanillaCalls, _registryReads;
        private static readonly object _tradeTravelMetadata = new object(), _tradeTravelSpaceObjects = new object();
        private static string _tradeTravelOriginSpaceObjectId = "earth";
        private static bool _tradeTravelInBramfatura, _marketScanActive, UsePreviousTradeLayout, EnhancedReadability;
        private static bool _stateReady = true;
        private static int _marketStationIndex;
        private static string _marketItemId = "alcohol_container";
        private static readonly System.Collections.Generic.List<object> MarketStations = new System.Collections.Generic.List<object>();
        private static readonly System.Collections.Generic.List<object> MarketEntries = new System.Collections.Generic.List<object>();
        private static readonly System.Reflection.MethodInfo _tradeTravelHoursMethod = typeof(ModMain).GetMethod("VanillaHours");
        private static readonly System.Reflection.MethodInfo _tradeTravelFormatMethod = typeof(ModMain).GetMethod("VanillaFormat");
        public static double VanillaHours(object metadata, object space, string origin, string destination)
        {
            _vanillaCalls++;
            if (origin != "earth" || destination != "mars") throw new System.InvalidOperationException("Unexpected route");
            return 48d;
        }
        public static string VanillaFormat(double hours) { return hours.ToString(System.Globalization.CultureInfo.InvariantCulture) + "h"; }
        private static object GetMember(object owner, string name)
        {
            _registryReads++;
            return owner.GetType().GetField(name).GetValue(owner);
        }
        private static bool RefreshTradeTravelOriginSnapshotSafe(bool schedule) { return _stateReady; }
        private static string Ui(string key) { return key; }
        private static void LogTradeTravelWarningOnce(string stage, System.Exception ex) { }
        private static void CheckTrade(bool condition, string message)
        {
            if (!condition) throw new System.Exception(message);
            _assertions++;
        }
        private static void ExpectNoTravel(string reason)
        {
            int calls = _vanillaCalls;
            double? hours;
            CheckTrade(GetTradeTravelTimeSafe("mars", out hours) == "—" && !hours.HasValue && calls == _vanillaCalls, reason);
        }
        public static int RunTradeRuntimeCases()
        {
            double? hours;
            SingletonMonoBehaviour<MGSC.UI>.Instance = null;
            ExpectNoTravel("No UI owner must not invoke vanilla or reuse stale context");
            MGSC.UI ui = new MGSC.UI();
            SingletonMonoBehaviour<MGSC.UI>.Instance = ui;
            ExpectNoTravel("Uninitialized UI must fail closed");
            ui.CurrentView = typeof(SpaceHudScreen);
            for (int i = 0; i < 175; i++)
                CheckTrade(GetTradeTravelTimeSafe("mars", out hours) == "48h" && hours == 48d, "Every station uses original vanilla hours");
            CheckTrade(_vanillaCalls == 175 && _registryReads == 1, "Space route must skip registry/global discovery for all 175 stations");
            SpaceHudScreen hud = new SpaceHudScreen();
            ui._instantiatedViews[typeof(SpaceHudScreen)] = hud;
            ui.CurrentView = typeof(MissionScreen); hud.gameObject.activeInHierarchy = false;
            ExpectNoTravel("Immediate space-to-mission transition must block vanilla even with retained HUD");
            ui.CurrentView = null; hud.gameObject.activeInHierarchy = true;
            CheckTrade(IsTradeTravelSpaceContext(), "Active HUD fallback during UI transition");
            hud.gameObject = null;
            ExpectNoTravel("Destroyed HUD root must fail closed");
            ui._instantiatedViews = null;
            ExpectNoTravel("Missing registry must fail closed");
            ui.CurrentView = typeof(SpaceHudScreen);
            CheckTrade(IsTradeTravelSpaceContext(), "Nested ship screens still use space default view");
            SingletonMonoBehaviour<MGSC.UI>.Instance = new MGSC.UI { CurrentView = typeof(MissionScreen) };
            ExpectNoTravel("Replaced UI owner must invalidate previous space result immediately");
            SingletonMonoBehaviour<MGSC.UI>.Instance = ui;
            _stateReady = false; ExpectNoTravel("Missing live state blocks vanilla"); _stateReady = true;
            _tradeTravelInBramfatura = true; ExpectNoTravel("Bramfatura still unavailable"); _tradeTravelInBramfatura = false;
            _tradeTravelOriginSpaceObjectId = ""; ExpectNoTravel("Missing origin still unavailable"); _tradeTravelOriginSpaceObjectId = "earth";
            CheckTrade(GetTradeTravelTimeSafe("earth", out hours) == "ui.here" && hours == 0d, "Same-location semantics preserved");
            CheckTrade(GetTradeTravelTimeSafe("mars", out hours) == "48h" && hours == 48d, "Space mode restored without reloading mod");

            Debug.Messages.Clear(); ResetTradePerformanceWindow();
            _marketScanActive = true;
            for (int i = 0; i < 200; i++) FinishTradePerformanceTick(TradePerfTimestamp(), false);
            CheckTrade(Debug.Messages.Count == 0, "Diagnostic must not log every tick");
            _tradePerfWindowStart -= System.Diagnostics.Stopwatch.Frequency * 6;
            FinishTradePerformanceTick(TradePerfTimestamp(), false);
            CheckTrade(Debug.Messages.Count == 1 && Debug.Messages[0].Contains("phase=scan"), "Five-second window emits a single scan summary");
            CheckTrade(_tradePerfTick.Count == 0, "Reported counters reset for independent next window");
            _marketScanActive = false;
            RecordTradePerformance(TradePerfStage.Station, TradePerfTimestamp());
            FinishTradePerformanceTick(TradePerfTimestamp(), true);
            CheckTrade(Debug.Messages.Count == 2 && Debug.Messages[1].Contains("completed=True"), "Completion emits final partial window");
            _tradePerfWindowStart -= System.Diagnostics.Stopwatch.Frequency * 6;
            FinishTradePerformanceTick(TradePerfTimestamp(), false);
            CheckTrade(Debug.Messages.Count == 3 && Debug.Messages[2].Contains("station=0:"), "Idle window retains no previous station timings");
            return _assertions;
        }
    }
}
