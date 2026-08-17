using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Runtime.InteropServices;
using MGSC;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ItemIntelligence
{
    public static partial class ModMain
    {

        // v1.7.36-test2: feature-owned state moved out of Runtime.cs.
        // Declaration ownership only; lifecycle and behavior are unchanged.

        // v1.7.36-test8: trade/runtime-market services are owned by Trade.
        private static object _stationsState;
        private static object _stationSystem;
        private static object _worldPricesSystem;
        private static object _tradeSystem;
        private static object _itemsPrices;
        private static int _marketEmptyRetryCooldown;
        private static bool _stationSchemaLogged;
        private static MethodInfo _tradeIsValidItemMethod;
        private static bool _tradeIsValidItemMethodResolved;
        private static bool _tradeSellContractStatusLogged;
        private static bool _tradeSellContractFailureLogged;

        private static string _marketItemId = string.Empty;
        private static readonly List<object> MarketStations = new List<object>();
        private static readonly List<LiveMarketEntry> MarketEntries = new List<LiveMarketEntry>();
        private static readonly List<LiveMarketEntry> TradeSellEntries = new List<LiveMarketEntry>();
        private static readonly List<LiveMarketEntry> TradeBuyEntries = new List<LiveMarketEntry>();

        // v1.7.19: read-only vanilla travel time for Trade rows. The exact runtime
        // contract is resolved once per space session. Any failure is isolated to this
        // optional column; prices, stock, station links and the Trade tab stay active.
        private static object _tradeTravelMetadata;
        private static object _tradeTravelSpaceObjects;
        private static Type _tradeTravelMetadataType;
        private static Type _tradeTravelSpaceObjectsType;
        private static MethodInfo _tradeTravelHoursMethod;
        private static MethodInfo _tradeTravelFormatMethod;
        private static bool _tradeTravelContractChecked;
        private static bool _tradeTravelContractAvailable;
        private static bool _tradeTravelWarningLogged;
        private static int _tradeTravelStateResolveNextFrame;
        private static string _tradeTravelOriginSpaceObjectId = string.Empty;
        private static string _tradeTravelOriginSignature = string.Empty;
        private static bool _tradeTravelInBramfatura;
        private static int _marketTravelRefreshIndex = -1;
        private static int _marketTravelRenderThrottle;

        private static int _marketStationIndex;
        private static bool _marketScanActive;
        private static bool _marketScanComplete;
        private static bool _marketResolveAttempted;
        private const int MarketRenderStationBatch = 10;
        private const int MarketRenderNewEntriesBatch = 3;
        private static int _marketRenderThrottle;
        private static int _marketEntriesAtLastRender;

        private static readonly Dictionary<string, List<TradeRelation>> BarterConsumers = new Dictionary<string, List<TradeRelation>>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, List<TradeRelation>> BarterSources = new Dictionary<string, List<TradeRelation>>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, int> MarketFactionRelations =
            new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        private sealed class TradeRelation
        {
            public readonly string Id;
            public readonly int Quantity;
            public readonly Dictionary<string, int> RelatedItems;

            public TradeRelation(string id, int quantity, Dictionary<string, int> relatedItems)
            {
                Id = id;
                Quantity = quantity;
                RelatedItems = relatedItems == null
                    ? new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
                    : new Dictionary<string, int>(relatedItems, StringComparer.OrdinalIgnoreCase);
            }
        }

        private static void InitializeTradeSpaceSessionState()
        {
            _marketResolveAttempted = false;
            _stationsState = null;
            _stationSystem = null;
            _tradeSystem = null;
            _worldPricesSystem = null;
            _itemsPrices = null;
            ResetTradeTravelSession();
            ResetTradeMissionSession();
            _marketEmptyRetryCooldown = 0;
            _stationSchemaLogged = false;
        }

        private static void ResetTradeMenuSessionState()
        {
            _marketResolveAttempted = false;
            _stationsState = null;
            _stationSystem = null;
            _tradeSystem = null;
            _worldPricesSystem = null;
            _itemsPrices = null;
            ResetTradeTravelSession();
            ResetTradeMissionSession();
            _marketItemId = string.Empty;
            _marketStationIndex = 0;
            _marketScanActive = false;
            _marketScanComplete = false;
            MarketStations.Clear();
            MarketEntries.Clear();
            TradeSellEntries.Clear();
            TradeBuyEntries.Clear();
            MarketFactionRelations.Clear();
        }

        // Trade owns exact barter indexes and live-market relation state.
        private static void ResetTradeIndexState()
        {
            BarterConsumers.Clear();
            BarterSources.Clear();
            MarketFactionRelations.Clear();
        }

        private static object ParseFactionTradeCategory(string name)
        {
            if (_factionTradeCategoryType == null || string.IsNullOrEmpty(name)) return null;
            try { return Enum.Parse(_factionTradeCategoryType, name, true); }
            catch { return null; }
        }

        private static IEnumerable InvokeFactionTradeItems(object runtimeFaction, int techLevel, object categoryValue)
        {
            if (runtimeFaction == null || categoryValue == null || _factionGetTradeItemsMethod == null ||
                _factionDropCollection == null) return null;
            try
            {
                object raw = _factionGetTradeItemsMethod.Invoke(
                    _factionDropCollection,
                    new object[] { runtimeFaction, techLevel, categoryValue, false });
                if (raw is string) return null;
                return raw as IEnumerable;
            }
            catch { return null; }
        }

        private static void BuildBrowserTrade(string itemId)
        {
            bool ru = IsRussian();

            if (!ShowSources && !ShowTradeInformation) return;

            if (!_compatTrade)
            {
                AddCompatibilityUnavailableLine("Trade");
                return;
            }
            EnsureTradeStateDependencies();
            if (SpaceObjectRecordsById.Count == 0) BuildSpaceObjectIndex();
            if (!string.Equals(_marketItemId, itemId, StringComparison.OrdinalIgnoreCase))
                StartMarketScan(itemId);

            PrepareTradePresentationEntries();
            MarkTradeMissionCountdownUiRendered();
            int sellToPlayerCount = TradeSellEntries.Count;
            int buyFromPlayerCount = TradeBuyEntries.Count;

            if (ShowSources && sellToPlayerCount > 0)
            {
                BrowserLines.Add(BrowserLine.FullSection((Ui("ui.buy_at_stations")) + "  •  " + sellToPlayerCount.ToString(CultureInfo.InvariantCulture) +
                    "  (" + Ui("ui.current_stock_may_change_during_travel") + ")"));
                BrowserLines.Add(BrowserLine.TradeHeader(
                    Ui("ui.station"), Ui("ui.price"), Ui("ui.stock"), Ui("ui.mission"), Ui("ui.travel")));
                for (int i = 0; i < TradeSellEntries.Count; i++)
                {
                    LiveMarketEntry entry = TradeSellEntries[i];
                    string price = entry.StationSellPrice.HasValue
                        ? entry.StationSellPrice.Value.ToString(CultureInfo.InvariantCulture)
                        : "?";
                    string stock = entry.Stock.HasValue
                        ? entry.Stock.Value.ToString(CultureInfo.InvariantCulture)
                        : "—";
                    BrowserLines.Add(BrowserLine.TradeStation(
                        entry.Label, price, stock, GetTradeMissionDisplay(entry), entry.TravelTime, entry.SpaceObjectId,
                        entry.OwnerFactionId, entry.OwnerRelation, entry.MissionArrivalState));
                }
            }

            if (ShowTradeInformation && buyFromPlayerCount > 0)
            {
                BrowserLines.Add(BrowserLine.Section((Ui("ui.sell_to_stations")) + "  •  " + buyFromPlayerCount.ToString(CultureInfo.InvariantCulture)));
                BrowserLines.Add(BrowserLine.TradeHeader(
                    Ui("ui.station"), Ui("ui.price"), string.Empty, Ui("ui.mission"), Ui("ui.travel")));
                for (int i = 0; i < TradeBuyEntries.Count; i++)
                {
                    LiveMarketEntry entry = TradeBuyEntries[i];
                    string price = entry.StationBuyPrice.HasValue
                        ? entry.StationBuyPrice.Value.ToString(CultureInfo.InvariantCulture)
                        : "?";
                    BrowserLines.Add(BrowserLine.TradeStation(
                        entry.Label, price, string.Empty, GetTradeMissionDisplay(entry), entry.TravelTime, entry.SpaceObjectId,
                        entry.OwnerFactionId, entry.OwnerRelation, entry.MissionArrivalState));
                }
            }

            if ((ShowSources && sellToPlayerCount > 0) ||
                (ShowTradeInformation && buyFromPlayerCount > 0))
                BrowserLines.Add(BrowserLine.Note(Ui("ui.click_a_station_to_open_its_location_on_the_star")));

            if (_runtimeFallbackResolveActive)
            {
                BrowserLines.Add(BrowserLine.Note(Ui("ui.connecting_to_market_data")));
            }
            if (_marketScanActive)
            {
                BrowserLines.Add(BrowserLine.Note((Ui("ui.market_processed")) +
                    _marketStationIndex.ToString(CultureInfo.InvariantCulture) + "/" + MarketStations.Count.ToString(CultureInfo.InvariantCulture)));
            }
            else if (_marketScanComplete && MarketStations.Count == 0)
            {
                BrowserLines.Add(BrowserLine.Note(Ui("ui.station_list_is_unavailable_from_the_current_gam")));
            }

            // Live station rows are actionable. BarterReceipt rows below are separate
            // station-economy recipe relations, not a promise of direct player barter.
            List<TradeRelation> sources = new List<TradeRelation>();
            List<TradeRelation> consumers = new List<TradeRelation>();
            List<TradeRelation> list;
            if (ShowSources)
            {
                if (BarterSources.TryGetValue(itemId, out list) && list != null) sources.AddRange(list);
            }
            if (ShowTradeInformation)
            {
                if (BarterConsumers.TryGetValue(itemId, out list) && list != null) consumers.AddRange(list);
            }

            if (sources.Count > 0)
            {
                BrowserLines.Add(BrowserLine.Section(Ui("ui.station_economy_recipe_output")));
                AddBrowserBarterRelations(sources, true);
            }
            if (consumers.Count > 0)
            {
                BrowserLines.Add(BrowserLine.Section(Ui("ui.station_economy_recipe_input")));
                AddBrowserBarterRelations(consumers, false);
            }

            if (BrowserLines.Count == 0 && (ShowSources || ShowTradeInformation))
                BrowserLines.Add(BrowserLine.Note(Ui("ui.no_trade_relationships_found_yet")));
        }

        private static void AddBrowserBarterRelations(List<TradeRelation> relations, bool selectedItemIsOutput)
        {
            if (relations == null || relations.Count == 0) return;

            List<TradeRelation> unique = new List<TradeRelation>();
            HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < relations.Count; i++)
            {
                TradeRelation relation = relations[i];
                if (relation == null || string.IsNullOrEmpty(relation.Id) || !seen.Add(relation.Id)) continue;
                unique.Add(relation);
            }
            unique.Sort(delegate(TradeRelation a, TradeRelation b)
            {
                return string.Compare(a.Id, b.Id, StringComparison.OrdinalIgnoreCase);
            });

            string selectedVerb = selectedItemIsOutput ? Ui("ui.economy_output") : Ui("ui.economy_input");
            string relatedVerb = selectedItemIsOutput ? Ui("ui.economy_input") : Ui("ui.economy_output");
            for (int i = 0; i < unique.Count; i++)
            {
                TradeRelation relation = unique[i];
                int selectedQuantity = Math.Max(1, relation.Quantity);
                if (unique.Count > 1)
                {
                    BrowserLines.Add(BrowserLine.Normal(
                        Ui("ui.economy_recipe") + " " + (i + 1).ToString(CultureInfo.InvariantCulture),
                        selectedVerb + "  x" + selectedQuantity.ToString(CultureInfo.InvariantCulture)));
                }
                else if (!selectedItemIsOutput || selectedQuantity > 1)
                {
                    BrowserLines.Add(BrowserLine.Normal(
                        Ui("ui.this_item"),
                        selectedVerb + "  x" + selectedQuantity.ToString(CultureInfo.InvariantCulture)));
                }

                List<KeyValuePair<string, int>> related =
                    new List<KeyValuePair<string, int>>(relation.RelatedItems);
                related.Sort(delegate(KeyValuePair<string, int> a, KeyValuePair<string, int> b)
                {
                    int localized = string.Compare(
                        LocalizeItem(a.Key), LocalizeItem(b.Key), StringComparison.OrdinalIgnoreCase);
                    if (localized != 0) return localized;
                    return string.Compare(a.Key, b.Key, StringComparison.OrdinalIgnoreCase);
                });
                for (int r = 0; r < related.Count; r++)
                {
                    KeyValuePair<string, int> item = related[r];
                    if (string.IsNullOrEmpty(item.Key)) continue;
                    BrowserLines.Add(BrowserLine.Item(
                        item.Key,
                        relatedVerb + "  x" + Math.Max(1, item.Value).ToString(CultureInfo.InvariantCulture)));
                }
            }
        }

        private static void StartMarketScan(string itemId, bool forceRefresh = false)
        {
            if (!ShowSources && !ShowTradeInformation) return;
            if (string.IsNullOrEmpty(itemId)) return;
            if (!forceRefresh && string.Equals(_marketItemId, itemId, StringComparison.OrdinalIgnoreCase))
            {
                if (_marketScanActive) return;
                if (_marketScanComplete && MarketStations.Count > 0) return;
            }

            _marketItemId = itemId;
            MarketEntries.Clear();
            MarketStations.Clear();
            MarketFactionRelations.Clear();
            _marketStationIndex = 0;
            _marketScanComplete = false;
            _marketScanActive = false;
            _marketRenderThrottle = 0;
            _marketEntriesAtLastRender = 0;
            _marketTravelRefreshIndex = -1;
            _marketTravelRenderThrottle = 0;
            RefreshTradeTravelOriginSnapshotSafe(false);

            if (_stationsState == null) _marketResolveAttempted = false;
            TickStateServiceResolver();
            TryResolveRuntimeServicesLightweight();
            PopulateMarketStationsFromResolvedState();

            if (MarketStations.Count > 0)
            {
                _marketScanActive = true;
            }
            else
            {
                BeginRuntimeFallbackResolver();
                _marketScanActive = _runtimeFallbackResolveActive;
                _marketScanComplete = !_marketScanActive;
            }

            Debug.Log("[ItemIntelligence] Market scan start for " + itemId +
                ": stations=" + MarketStations.Count +
                ", ItemsPrices=" + (_itemsPrices != null) +
                ", Factions=" + (_factionsState != null) +
                ", Magnum=" + (_magnumProgression != null) +
                ", fallbackResolver=" + _runtimeFallbackResolveActive + ".");
        }

        private static void TickMarketScan()
        {
            if (!_inspectorOpen || _browserTab != (int)BrowserTabId.Trade) return;

            // Travel refresh owns its own failure boundary. It must never trip the
            // existing Trade compatibility breaker.
            TickTradeTravelTimeRefreshSafe();
            TickTradeMissionCountdownUiRefresh();
            TickStateServiceResolver();
            if (_marketEmptyRetryCooldown > 0) _marketEmptyRetryCooldown--;
            if (_marketScanComplete && MarketStations.Count == 0 && (_stationsState != null || _stationSystem != null) && _marketEmptyRetryCooldown <= 0)
            {
                _marketEmptyRetryCooldown = 180;
                _marketItemId = string.Empty;
                StartMarketScan(_inspectorItemId);
            }

            if (_runtimeFallbackResolveActive)
            {
                TickRuntimeFallbackResolver();
                if (_runtimeFallbackResolveActive) return;
                PopulateMarketStationsFromResolvedState();
                if (MarketStations.Count == 0)
                {
                    _marketScanActive = false;
                    _marketScanComplete = true;
                    RenderBrowser(_inspectorItemId);
                    return;
                }
                _marketScanActive = true;
                RenderBrowser(_inspectorItemId);
                return;
            }

            if (!_marketScanActive) return;
            if (_marketStationIndex >= MarketStations.Count)
            {
                _marketScanActive = false;
                _marketScanComplete = true;
                RenderBrowser(_inspectorItemId);
                return;
            }

            object station = MarketStations[_marketStationIndex++];
            if (station != null)
            {
                LiveMarketEntry entry = BuildLiveMarketEntry(station, _marketItemId);
                if (entry != null) MarketEntries.Add(entry);
            }

            _marketRenderThrottle++;
            bool scanFinished = _marketStationIndex >= MarketStations.Count;
            bool enoughNewEntries = MarketEntries.Count - _marketEntriesAtLastRender >= MarketRenderNewEntriesBatch;
            if (_marketRenderThrottle >= MarketRenderStationBatch || enoughNewEntries || scanFinished)
            {
                _marketRenderThrottle = 0;
                _marketEntriesAtLastRender = MarketEntries.Count;
                if (scanFinished)
                {
                    _marketScanActive = false;
                    _marketScanComplete = true;
                }
                RenderBrowser(_inspectorItemId);
            }
        }

        private static void ResetTradeTravelSession()
        {
            _tradeTravelMetadata = null;
            _tradeTravelSpaceObjects = null;
            _tradeTravelMetadataType = null;
            _tradeTravelSpaceObjectsType = null;
            _tradeTravelHoursMethod = null;
            _tradeTravelFormatMethod = null;
            _tradeTravelContractChecked = false;
            _tradeTravelContractAvailable = false;
            _tradeTravelWarningLogged = false;
            _tradeTravelStateResolveNextFrame = 0;
            _tradeTravelOriginSpaceObjectId = string.Empty;
            _tradeTravelOriginSignature = string.Empty;
            _tradeTravelInBramfatura = false;
            _marketTravelRefreshIndex = -1;
            _marketTravelRenderThrottle = 0;
        }

        private static bool TryResolveTradeTravelContract()
        {
            if (_tradeTravelContractChecked) return _tradeTravelContractAvailable;
            _tradeTravelContractChecked = true;

            try
            {
                _tradeTravelMetadataType = AccessTools.TypeByName("MGSC.TravelMetadata");
                _tradeTravelSpaceObjectsType = AccessTools.TypeByName("MGSC.SpaceObjects");
                Type travelSystemType = AccessTools.TypeByName("MGSC.TravelSystem");
                Type formatHelperType = AccessTools.TypeByName("MGSC.FormatHelper");

                if (_tradeTravelMetadataType == null || _tradeTravelSpaceObjectsType == null ||
                    travelSystemType == null || formatHelperType == null)
                    throw new TypeLoadException("Vanilla travel types are unavailable.");

                _tradeTravelHoursMethod = travelSystemType.GetMethod(
                    "GetTravelHoursBetweenPoints",
                    StaticFlags,
                    null,
                    new Type[]
                    {
                        _tradeTravelMetadataType,
                        _tradeTravelSpaceObjectsType,
                        typeof(string),
                        typeof(string)
                    },
                    null);

                _tradeTravelFormatMethod = formatHelperType.GetMethod(
                    "ToLocalizedDaysAndHours",
                    StaticFlags,
                    null,
                    new Type[] { typeof(double) },
                    null);

                if (_tradeTravelHoursMethod == null ||
                    _tradeTravelHoursMethod.ReturnType != typeof(double) ||
                    _tradeTravelFormatMethod == null ||
                    _tradeTravelFormatMethod.ReturnType != typeof(string))
                    throw new MissingMethodException("Exact vanilla travel-time contract was not found.");

                _tradeTravelContractAvailable = true;
                Debug.Log("[ItemIntelligence] Trade travel-time contract resolved (vanilla double -> localized string).");
                return true;
            }
            catch (Exception ex)
            {
                _tradeTravelContractAvailable = false;
                LogTradeTravelWarningOnce("contract", ex);
                return false;
            }
        }

        private static bool TryEnsureTradeTravelState()
        {
            if (!TryResolveTradeTravelContract()) return false;
            if (_tradeTravelMetadata != null && _tradeTravelSpaceObjects != null) return true;

            int frame = Time.frameCount;
            if (frame < _tradeTravelStateResolveNextFrame) return false;
            _tradeTravelStateResolveNextFrame = frame + 30;

            try
            {
                if (_tradeTravelMetadata == null)
                    _tradeTravelMetadata = ResolveStateModule(_tradeTravelMetadataType);
                if (_tradeTravelSpaceObjects == null)
                    _tradeTravelSpaceObjects = ResolveStateModule(_tradeTravelSpaceObjectsType);
            }
            catch { }

            return _tradeTravelMetadata != null && _tradeTravelSpaceObjects != null;
        }

        private static bool RefreshTradeTravelOriginSnapshotSafe(bool scheduleRefresh)
        {
            try
            {
                if (!TryEnsureTradeTravelState()) return false;

                string origin = GetStringMember(_tradeTravelMetadata, "CurrentSpaceObject");
                bool inBramfatura = GetBoolMember(_tradeTravelMetadata, "IsInBramfatura").GetValueOrDefault(false);
                // The cached value is already localized by vanilla, so a live language
                // change must invalidate it just like a location change.
                string signature = (inBramfatura ? "bramfatura|" : "space|") +
                    origin + "|" + GetLanguageSignature();
                bool changed = !string.Equals(
                    signature,
                    _tradeTravelOriginSignature,
                    StringComparison.OrdinalIgnoreCase);

                _tradeTravelOriginSpaceObjectId = origin;
                _tradeTravelInBramfatura = inBramfatura;
                _tradeTravelOriginSignature = signature;

                if (changed && scheduleRefresh && MarketEntries.Count > 0)
                {
                    _marketTravelRefreshIndex = 0;
                    _marketTravelRenderThrottle = 0;
                }
                return true;
            }
            catch (Exception ex)
            {
                LogTradeTravelWarningOnce("origin", ex);
                return false;
            }
        }

        private static string GetTradeTravelTimeSafe(string destinationSpaceObjectId)
        {
            double? ignoredHours;
            return GetTradeTravelTimeSafe(destinationSpaceObjectId, out ignoredHours);
        }

        private static string GetTradeTravelTimeSafe(string destinationSpaceObjectId, out double? travelHours)
        {
            travelHours = null;
            try
            {
                // TravelSystem.GetTravelHoursBetweenPoints dereferences live space-mode
                // state. Test13's long mission log proved that invoking it from Dungeon
                // can throw inside vanilla even though cached TravelMetadata still exists.
                // The column is informational, so fail closed before the vanilla call.
                string spaceContextReason;
                if (!IsStarmapExperimentSpaceContext(out spaceContextReason)) return "—";
                if (!RefreshTradeTravelOriginSnapshotSafe(false)) return "—";
                if (_tradeTravelInBramfatura) return "—";
                if (string.IsNullOrEmpty(_tradeTravelOriginSpaceObjectId) ||
                    string.IsNullOrEmpty(destinationSpaceObjectId))
                    return "—";

                if (string.Equals(
                    _tradeTravelOriginSpaceObjectId,
                    destinationSpaceObjectId,
                    StringComparison.OrdinalIgnoreCase))
                {
                    travelHours = 0d;
                    return Ui("ui.here");
                }

                object rawHours = _tradeTravelHoursMethod.Invoke(
                    null,
                    new object[]
                    {
                        _tradeTravelMetadata,
                        _tradeTravelSpaceObjects,
                        _tradeTravelOriginSpaceObjectId,
                        destinationSpaceObjectId
                    });
                double hours = Convert.ToDouble(rawHours, CultureInfo.InvariantCulture);
                if (double.IsNaN(hours) || double.IsInfinity(hours) || hours < 0d) return "—";

                travelHours = hours;
                object formatted = _tradeTravelFormatMethod.Invoke(null, new object[] { hours });
                string text = formatted as string;
                return string.IsNullOrWhiteSpace(text) ? "—" : text;
            }
            catch (Exception ex)
            {
                travelHours = null;
                LogTradeTravelWarningOnce("calculation", ex);
                return "—";
            }
        }

        private static void PrepareTradePresentationEntries()
        {
            RefreshTradeMissionStatusSnapshot();
            TradeSellEntries.Clear();
            TradeBuyEntries.Clear();
            for (int i = 0; i < MarketEntries.Count; i++)
            {
                LiveMarketEntry entry = MarketEntries[i];
                if (entry == null) continue;
                ApplyTradeMissionState(entry);
                if (entry.StationSells) TradeSellEntries.Add(entry);
                if (entry.StationBuys) TradeBuyEntries.Add(entry);
            }

            if (TradeSellEntries.Count > 1) TradeSellEntries.Sort(CompareMarketEntriesByTravel);
            if (TradeBuyEntries.Count > 1) TradeBuyEntries.Sort(CompareMarketEntriesByTravel);
        }

        private static int CompareMarketEntriesByTravel(LiveMarketEntry a, LiveMarketEntry b)
        {
            if (ReferenceEquals(a, b)) return 0;
            if (a == null) return 1;
            if (b == null) return -1;

            if (a.TravelHours.HasValue && b.TravelHours.HasValue)
            {
                int travel = a.TravelHours.Value.CompareTo(b.TravelHours.Value);
                if (travel != 0) return travel;
            }
            else if (a.TravelHours.HasValue) return -1;
            else if (b.TravelHours.HasValue) return 1;

            return string.Compare(a.Label, b.Label, StringComparison.CurrentCultureIgnoreCase);
        }

        private static void TickTradeTravelTimeRefreshSafe()
        {
            try
            {
                if (!RefreshTradeTravelOriginSnapshotSafe(true)) return;
                if (_marketTravelRefreshIndex < 0) return;
                if (_marketTravelRefreshIndex >= MarketEntries.Count)
                {
                    _marketTravelRefreshIndex = -1;
                    return;
                }

                LiveMarketEntry entry = MarketEntries[_marketTravelRefreshIndex++];
                if (entry != null)
                {
                    double? travelHours;
                    entry.TravelTime = GetTradeTravelTimeSafe(entry.SpaceObjectId, out travelHours);
                    entry.TravelHours = travelHours;
                    RefreshTradeMissionArrivalState(entry);
                }

                _marketTravelRenderThrottle++;
                bool complete = _marketTravelRefreshIndex >= MarketEntries.Count;
                if (_marketTravelRenderThrottle >= 3 || complete)
                {
                    _marketTravelRenderThrottle = 0;
                    if (complete) _marketTravelRefreshIndex = -1;
                    RenderBrowser(_inspectorItemId);
                }
            }
            catch (Exception ex)
            {
                _marketTravelRefreshIndex = -1;
                LogTradeTravelWarningOnce("refresh", ex);
            }
        }

        private static void LogTradeTravelWarningOnce(string stage, Exception ex)
        {
            if (_tradeTravelWarningLogged) return;
            _tradeTravelWarningLogged = true;
            Exception detail = ex is TargetInvocationException && ex.InnerException != null
                ? ex.InnerException
                : ex;
            Debug.LogWarning(
                "[ItemIntelligence] Trade travel time unavailable at " + stage + ". " +
                (detail == null ? string.Empty : detail.GetType().Name + ": " + detail.Message) +
                " The Trade tab remains active and the TRAVEL column will show —.");
        }

        private static void EnsureTradeStateDependencies()
        {
            try
            {
                if (_itemsPrices == null)
                {
                    Type t = AccessTools.TypeByName("MGSC.ItemsPrices");
                    if (t != null) _itemsPrices = ResolveStateModule(t);
                }
                if (_factionsState == null)
                {
                    Type t = AccessTools.TypeByName("MGSC.Factions");
                    if (t != null) _factionsState = ResolveStateModule(t);
                }
                if (_difficultyState == null)
                {
                    Type t = AccessTools.TypeByName("MGSC.Difficulty");
                    if (t != null) _difficultyState = ResolveStateModule(t);
                }
                TryResolveMagnumProgressionLightweight();
            }
            catch { }
        }

        private static void PopulateMarketStationsFromResolvedState()
        {
            if (MarketStations.Count > 0) return;
            TickStateServiceResolver();
            List<object> stations = GetRuntimeStationsLightweight();
            for (int i = 0; i < stations.Count; i++) MarketStations.Add(stations[i]);
        }

        private static bool TryEvaluateVanillaSellToStationGate(object station, string itemId, out bool accepts)
        {
            accepts = false;
            if (station == null || string.IsNullOrEmpty(itemId)) return false;
            try
            {
                EnsureTradeStateDependencies();
                Station typedStation = station as Station;
                Faction typedFaction = ResolveStationFaction(station) as Faction;
                if (typedStation == null || typedFaction == null) return false;

                if (!_tradeIsValidItemMethodResolved)
                {
                    _tradeIsValidItemMethodResolved = true;
                    Type tradeType = AccessTools.TypeByName("MGSC.TradeSystem");
                    if (tradeType != null)
                        _tradeIsValidItemMethod = AccessTools.Method(
                            tradeType, "IsValidItem", new Type[] { typeof(Faction), typeof(Station), typeof(string) });
                    if (!_tradeSellContractStatusLogged)
                    {
                        _tradeSellContractStatusLogged = true;
                        if (_tradeIsValidItemMethod != null)
                            Debug.Log("[ItemIntelligence][TradeSellContract] exactGate=TradeSystem.IsValidItem(Faction,Station,string).");
                        else
                            Debug.LogWarning("[ItemIntelligence][TradeSellContract] exact vanilla gate unavailable; SELL TO STATIONS relations are omitted rather than guessed.");
                    }
                }
                if (_tradeIsValidItemMethod == null) return false;

                object raw = _tradeIsValidItemMethod.Invoke(null, new object[] { typedFaction, typedStation, itemId });
                if (!(raw is bool)) return false;
                accepts = (bool)raw;
                return true;
            }
            catch (Exception ex)
            {
                if (!_tradeSellContractFailureLogged)
                {
                    _tradeSellContractFailureLogged = true;
                    Debug.LogWarning("[ItemIntelligence][TradeSellContract] exact vanilla gate failed; SELL TO STATIONS relations are omitted. " + ex.GetType().Name + ": " + ex.Message);
                }
                return false;
            }
        }

        private static LiveMarketEntry BuildLiveMarketEntry(object station, string itemId)
        {
            if (station == null || string.IsNullOrEmpty(itemId)) return null;
            try
            {
                // Vanilla StationCargoTradePage.InitTradePanels reads InternalStorage.
                // If that exact contract is unavailable, BUY AT STATIONS fails closed.
                object storage = GetMember(station, "InternalStorage");
                int stock = GetContainerItemCount(storage, itemId);


                bool sells = stock > 0;
                bool buys;
                bool sellGateResolved = TryEvaluateVanillaSellToStationGate(station, itemId, out buys);
                if (!sellGateResolved) buys = false;
                if (!buys && !sells) return null;

                string stationId = FirstNonEmpty(
                    GetStringMember(station, "Id"),
                    GetStringMember(station, "StationId"),
                    GetStringMember(station, "SpaceObjectId"));
                string spaceObjectId = GetStringMember(station, "SpaceObjectId");
                string stationLabel = FirstNonEmpty(GetStringMember(station, "Name"), LocalizeStation(stationId), stationId);
                if (string.IsNullOrEmpty(stationLabel)) stationLabel = station.GetType().Name;
                string locationLabel = BuildStationLocationLabel(spaceObjectId);
                string label = !string.IsNullOrEmpty(locationLabel)
                    ? locationLabel + "  —  " + stationLabel
                    : stationLabel;

                int price;
                int? stationBuyPrice = null;
                int? stationSellPrice = null;
                if (buys && TryGetExactStationPrice(station, itemId, true, out price)) stationBuyPrice = price;
                if (sells && TryGetExactStationPrice(station, itemId, false, out price)) stationSellPrice = price;


                // OwnerFactionId is the CURRENT runtime/save owner. Quasimorph stations
                // can be captured, so the initial owner must never drive this display.
                string ownerFactionId = GetStringMember(station, "OwnerFactionId");
                object ownerFaction = ResolveFactionById(ownerFactionId);
                int ownerRelation = GetMarketFactionRelation(ownerFactionId, ownerFaction);

                if (!string.IsNullOrEmpty(ownerFactionId))
                    TryResolveFactionSmallIcon(ownerFactionId, ownerFaction);

                LiveMarketEntry result = new LiveMarketEntry(
                    stationId, spaceObjectId, label, buys, sells,
                    stationBuyPrice, stationSellPrice, sells ? (int?)stock : null,
                    ownerFactionId, ownerRelation);
                double? travelHours;
                result.TravelTime = GetTradeTravelTimeSafe(spaceObjectId, out travelHours);
                result.TravelHours = travelHours;
                return result;
            }
            catch { return null; }
        }

        private static int GetMarketFactionRelation(string factionId, object faction)
        {
            if (string.IsNullOrEmpty(factionId)) return 0;

            int cached;
            if (MarketFactionRelations.TryGetValue(factionId, out cached))
                return cached;

            int relation = ResolveFactionRelationState(factionId, faction);
            MarketFactionRelations[factionId] = relation;
            return relation;
        }


    }
}
