using System.Globalization;
using UnityEngine;

namespace UnityEngine
{
    public static class Mathf
    {
        public static int RoundToInt(float value) { return checked((int)Math.Round(value, MidpointRounding.ToEven)); }
    }
}
namespace MGSC
{
    public class ItemStorage
    {
        public int Count = 17;
        public int CountItems(string itemId) { return Count; }
    }
    public class Station { public ItemStorage InternalStorage = new ItemStorage(); }
    public class Faction { }
    public class Factions { }
    public class MagnumProgression { }
    public class BasePickupItem { public string Id = "alcohol_container"; public int StackCount = 20; }
    public class Difficulty { public Preset Preset = new Preset(); }
    public class Preset { public double BarterValue = 0.45; }
    public class ItemsPrices
    {
        public int SellBase = 399;
        public int[] BuyTotals = { 0, 277, 610, 928, 1230, 1535 };
        public int[] SellUnits = { 180, 170, 162, 153, 146 };
    }
    // Contract fixtures deliberately supply nonlinear quoted prices. The mod must
    // preserve API calls and rounding, not replace a batch with unit price * count.
    public static class TradeSystem
    {
        public static readonly List<string> Calls = new List<string>();
        public static object[] LastInputs;
        public static int GetBuyPrice(MagnumProgression magnum, Factions factions, ItemsPrices prices, Station station, Dictionary<string, int> quantities)
        {
            int quantity = quantities["alcohol_container"];
            Calls.Add("buy:" + quantity);
            LastInputs = new object[] { magnum, factions, prices, station };
            return prices.BuyTotals[quantity];
        }
        public static int GetItemSellPrice(MagnumProgression magnum, Faction faction, Station station, ItemsPrices prices, string id, bool ignoreProxy)
        {
            Calls.Add("sell:" + id + ":" + ignoreProxy);
            LastInputs = new object[] { magnum, faction, station, prices };
            return prices.SellBase;
        }
        private static int GetItemSellTradePoints(MagnumProgression magnum, Faction faction, Station station, ItemsPrices prices, Difficulty difficulty, BasePickupItem item, Dictionary<string, int> sold, bool ignoreProxy)
        {
            int count; sold.TryGetValue(item.Id, out count);
            if (item.StackCount != 1 || ignoreProxy) throw new Exception("Wrong detached preview or proxy rule");
            Calls.Add("batch-sell:" + count);
            LastInputs = new object[] { magnum, faction, station, prices, difficulty, item };
            sold[item.Id] = count + item.StackCount;
            return prices.SellUnits[count];
        }
        public static int WrongParameter(object station) { return 0; }
        public static float WrongReturn(Station station) { return 0; }
    }
}
namespace ItemIntelligence
{
    public static partial class ModMain
    {
        private static readonly BindingFlags InstanceFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private static readonly BindingFlags StaticFlags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        private static object _itemsPrices = new ItemsPrices(), _magnumProgression = new MagnumProgression(),
            _difficultyState = new Difficulty(), _factionsState = new Factions();
        private static Faction _faction = new Faction();
        private static int _assertions, _boundaryWarnings;
        private static bool _audited = true;
        private static void EnsureTradeStateDependencies() { }
        private static bool IsCurrent103TradeAssembly() { return _audited; }
        private static bool IsLegacy102FeatureAssembly() { return false; }
        private static bool TryGetLegacyExactStationPrice102(object station, string item, bool buys, out int price) { price = 0; return false; }
        private static object ResolveStateModule(Type type) { return null; }
        private static object ResolveStationFaction(object station) { return _faction; }
        private static BasePickupItem CreateBrowserTooltipPreviewItem(string id) { return new BasePickupItem { Id = id }; }
        private static object GetMember(object obj, string name)
        {
            if (obj == null) return null;
            FieldInfo field = obj.GetType().GetField(name);
            return field == null ? null : field.GetValue(obj);
        }
        private static bool TryToDoubleSafe(object raw, out double value)
        {
            value = 0; if (raw == null) return false;
            try { value = Convert.ToDouble(raw, CultureInfo.InvariantCulture); return true; } catch { return false; }
        }
        private static bool TryToInt(object raw, out int value) { value = raw is int ? (int)raw : 0; return raw is int; }
        private static bool TryExtractPriceValue(object raw, out int value) { return TryToInt(raw, out value); }
        private static void LogRuntimeBoundaryWarningOnce(string id, string text, Exception ex) { _boundaryWarnings++; }
        private static void CheckPrice(bool condition, string message)
        {
            if (!condition) throw new Exception(message);
            _assertions++;
        }
        public static int RunTradePricingCases()
        {
            Station station = new Station();
            int price, total, last;
            CheckPrice(TryGetExactStationPrice(station, "alcohol_container", false, out price) && price == 277, "Player purchase uses exact one-item quote");
            CheckPrice(TradeSystem.Calls.Count == 1 && TradeSystem.Calls[0] == "buy:1", "One-item quote is one vanilla call");
            CheckPrice(TryGetExactStationPrice(station, "alcohol_container", true, out price) && price == 180, "Player sale applies and rounds current barter coefficient");
            CheckPrice(TradeSystem.Calls[1] == "sell:alcohol_container:False", "Proxy rule remains enabled");
            TradeSystem.Calls.Clear();
            CheckPrice(TryGetExactStationBatchPrice103(station, "alcohol_container", false, 5, out total, out last) && total == 1535 && last == 305, "Purchase batch total and last marginal price are nonlinear");
            CheckPrice(string.Join(",", TradeSystem.Calls.ToArray()) == "buy:5,buy:4", "Last purchase price uses difference of consecutive totals");
            TradeSystem.Calls.Clear();
            CheckPrice(TryGetExactStationBatchPrice103(station, "alcohol_container", false, 1, out total, out last) && total == 277 && last == 277 && TradeSystem.Calls.Count == 1, "Single remaining stock uses one quote");
            TradeSystem.Calls.Clear();
            CheckPrice(TryGetExactStationBatchPrice103(station, "alcohol_container", true, 5, out total, out last) && total == 811 && last == 146, "Sale batch is sum of five vanilla marginal prices");
            CheckPrice(string.Join(",", TradeSystem.Calls.ToArray()) == "batch-sell:0,batch-sell:1,batch-sell:2,batch-sell:3,batch-sell:4", "Sold-count map advances exactly once per unit");
            CheckPrice(station.InternalStorage.Count == 17, "Pricing does not mutate stock");
            CheckPrice(GetTradeBatchSampleQuantity(false, 2) == 2 && GetTradeBatchSampleQuantity(false, 17) == 5 && GetTradeBatchSampleQuantity(true, null) == 5, "Quantity follows available stock");

            // Simulate replacing save-owned data after metadata has already been cached.
            _magnumProgression = new MagnumProgression(); _factionsState = new Factions();
            _faction = new Faction(); _difficultyState = new Difficulty { Preset = new Preset { BarterValue = 0.5 } };
            _itemsPrices = new ItemsPrices { SellBase = 401, BuyTotals = new int[] { 0, 98, 220, 365, 499, 777 } };
            Station replacement = new Station();
            CheckPrice(TryGetExactStationPrice(replacement, "alcohol_container", false, out price) && price == 98, "Cached API reads replacement save prices");
            CheckPrice(ReferenceEquals(TradeSystem.LastInputs[0], _magnumProgression) && ReferenceEquals(TradeSystem.LastInputs[1], _factionsState) && ReferenceEquals(TradeSystem.LastInputs[2], _itemsPrices) && ReferenceEquals(TradeSystem.LastInputs[3], replacement), "Purchase receives current live references");
            CheckPrice(TryGetExactStationPrice(replacement, "alcohol_container", true, out price) && price == 200, "Current difficulty and tie-to-even rounding preserved");
            CheckPrice(ReferenceEquals(TradeSystem.LastInputs[1], _faction), "Sale receives current faction");
            for (int i = 0; i < 175; i++)
            {
                replacement.InternalStorage.Count = i;
                CheckPrice(GetContainerItemCount(replacement.InternalStorage, "alcohol_container") == i, "Stock read remains live across station scan");
                CheckPrice(TryGetExactStationBatchPrice103(replacement, "alcohol_container", false, 5, out total, out last) && total == 777 && last == 278, "Repeated batch quotes preserve values");
            }
            CheckPrice(GetContainerItemCount(new object(), "alcohol_container") == 0, "Unknown storage fails closed");
            int before = TradeSystem.Calls.Count; _audited = false;
            CheckPrice(!TryGetExactStationPrice(replacement, "alcohol_container", false, out price) && TradeSystem.Calls.Count == before, "Unverified binary cannot use cached exact API"); _audited = true;
            CheckPrice(!TryGetExactStationPrice(new object(), "alcohol_container", false, out price), "Wrong station type rejected");
            object prices = _itemsPrices; _itemsPrices = new object();
            CheckPrice(!TryGetExactStationBatchPrice103(replacement, "alcohol_container", false, 5, out total, out last), "Wrong price state rejected"); _itemsPrices = prices;
            ((Difficulty)_difficultyState).Preset.BarterValue = double.NaN;
            CheckPrice(!TryGetExactStationPrice(replacement, "alcohol_container", true, out price), "Invalid barter coefficient rejected");
            ((Difficulty)_difficultyState).Preset.BarterValue = 0.5;
            ((ItemsPrices)_itemsPrices).BuyTotals[1] = -1;
            CheckPrice(!TryGetExactStationPrice(replacement, "alcohol_container", false, out price), "Negative quote rejected");
            ((ItemsPrices)_itemsPrices).BuyTotals[4] = 1000;
            CheckPrice(!TryGetExactStationBatchPrice103(replacement, "alcohol_container", false, 5, out total, out last), "Decreasing total must not invent marginal price");
            ((ItemsPrices)_itemsPrices).SellUnits = new int[] { int.MaxValue, int.MaxValue };
            CheckPrice(!TryGetExactStationBatchPrice103(replacement, "alcohol_container", true, 2, out total, out last) && _boundaryWarnings == 1, "Batch overflow fails closed");
            CheckPrice(!TryGetExactStationBatchPrice103(replacement, "alcohol_container", true, 0, out total, out last), "Zero quantity rejected");
            MethodInfo resolve = typeof(TradePriceApi103).GetMethod("Resolve", StaticFlags);
            CheckPrice(resolve.Invoke(null, new object[] { "MissingMethod", new Type[] { typeof(Station) } }) == null, "Missing API cannot be selected");
            CheckPrice(resolve.Invoke(null, new object[] { "WrongParameter", new Type[] { typeof(Station) } }) == null, "Assignable but inexact overload rejected");
            CheckPrice(resolve.Invoke(null, new object[] { "WrongReturn", new Type[] { typeof(Station) } }) == null, "Inexact return type rejected");
            return _assertions;
        }
    }
}
