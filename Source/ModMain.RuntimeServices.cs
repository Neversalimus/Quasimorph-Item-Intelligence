using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using MGSC;
using HarmonyLib;
using UnityEngine;

namespace ItemIntelligence
{
    public static partial class ModMain
    {
        // v1.7.36-test8: shared runtime-service discovery has a dedicated owner.
        // Feature state remains in its feature files; this module only coordinates
        // read-only discovery of save/space services exposed by MGSC state containers.
        private static object _customResources;
        private static bool _customResourcesResolutionAttempted;
        private static Type[] _runtimeResolveOwnerTypes;
        private static int _runtimeResolveOwnerIndex;
        private static bool _runtimeFallbackResolveActive;
        private static bool _stateServicesResolved;
        private static bool _stateServicesLogged;
        private static int _stateResolveCooldown;
        private static int _stateResolveAttempts;
        private static bool _stateServiceTypesResolved;
        private static Type _stateStationSystemType;
        private static Type _stateStationsType;
        private static Type _stateTradeType;
        private static Type _statePricesType;
        private static Type _stateResourcesType;
        private static Type _stateItemsPricesType;
        private static Type _stateFactionsType;
        private static Type _stateDifficultyType;

        private static void EnsureStateServiceTypesResolved()
        {
            if (_stateServiceTypesResolved) return;
            _stateServiceTypesResolved = true;
            _stateStationSystemType = AccessTools.TypeByName("MGSC.StationSystem");
            _stateStationsType = AccessTools.TypeByName("MGSC.Stations");
            _stateTradeType = AccessTools.TypeByName("MGSC.TradeSystem");
            _statePricesType = AccessTools.TypeByName("MGSC.WorldPricesSystem");
            _stateResourcesType = AccessTools.TypeByName("MGSC.CustomResources");
            _stateItemsPricesType = AccessTools.TypeByName("MGSC.ItemsPrices");
            _stateFactionsType = AccessTools.TypeByName("MGSC.Factions");
            _stateDifficultyType = AccessTools.TypeByName("MGSC.Difficulty");
        }

        private static void ResetRuntimeServiceResolverSessionState()
        {
            _customResources = null;
            _customResourcesResolutionAttempted = false;
            _runtimeResolveOwnerTypes = null;
            _runtimeResolveOwnerIndex = 0;
            _runtimeFallbackResolveActive = false;
            _stateServicesResolved = false;
            _stateServicesLogged = false;
            _stateResolveCooldown = 0;
            _stateResolveAttempts = 0;
        }

        private static void StopRuntimeServiceFrameWork()
        {
            _runtimeFallbackResolveActive = false;
        }

        private static void TickStateServiceResolver()
        {
            if (_stateServicesResolved) return;
            if (_stateResolveCooldown > 0)
            {
                _stateResolveCooldown--;
                return;
            }
            _stateResolveAttempts++;
            try
            {
                EnsureStateServiceTypesResolved();

                if (_stationSystem == null && _stateStationSystemType != null) _stationSystem = ResolveStateModule(_stateStationSystemType);
                if (_stationsState == null && _stateStationsType != null) _stationsState = ResolveStateModule(_stateStationsType);
                if (_tradeSystem == null && _stateTradeType != null) _tradeSystem = ResolveStateModule(_stateTradeType);
                if (_worldPricesSystem == null && _statePricesType != null) _worldPricesSystem = ResolveStateModule(_statePricesType);
                if (_customResources == null && _stateResourcesType != null) _customResources = ResolveStateModule(_stateResourcesType);
                if (_itemsPrices == null && _stateItemsPricesType != null) _itemsPrices = ResolveStateModule(_stateItemsPricesType);
                if (_factionsState == null && _stateFactionsType != null) _factionsState = ResolveStateModule(_stateFactionsType);
                if (_difficultyState == null && _stateDifficultyType != null) _difficultyState = ResolveStateModule(_stateDifficultyType);

                if (_stationsState == null && _stationSystem != null)
                {
                    object value = GetMember(_stationSystem, "Stations");
                    if (value != null) _stationsState = value;
                }

                bool haveStationAccess = _stationsState != null || _stationSystem != null;
                _stateServicesResolved = haveStationAccess && _itemsPrices != null && _factionsState != null;

                if (!_stateServicesLogged && (haveStationAccess || _tradeSystem != null || _worldPricesSystem != null || _customResources != null))
                {
                    _stateServicesLogged = true;
                    Debug.Log("[ItemIntelligence] State modules: StationSystem=" + (_stationSystem != null) +
                        ", Stations=" + (_stationsState != null) +
                        ", TradeSystem=" + (_tradeSystem != null) +
                        ", ItemsPrices=" + (_itemsPrices != null) +
                        ", Factions=" + (_factionsState != null) +
                        ", Difficulty=" + (_difficultyState != null) +
                        ", CustomResources=" + (_customResources != null) + ".");
                        QueueTest3RowsRefresh(); // QII1739T3_MAGNUM_REFRESH_STATE
                }
            }
            catch { }

            if (_stateServicesResolved) _stateResolveCooldown = 0;
            else if (_stateResolveAttempts <= 2) _stateResolveCooldown = 30;
            else if (_stateResolveAttempts <= 4) _stateResolveCooldown = 60;
            else if (_stateResolveAttempts <= 6) _stateResolveCooldown = 120;
            else _stateResolveCooldown = 300;
        }

        private static object ResolveStateModule(Type target)
        {
            if (target == null || _modContext == null) return null;
            object state = null;
            try { state = _modContext.State; } catch { state = GetMember(_modContext, "State"); }
            if (state == null) return null;
            if (target.IsInstanceOfType(state)) return state;

            // State implementations commonly expose modules through Get<T>() or a module dictionary.
            try
            {
                MethodInfo[] methods = state.GetType().GetMethods(InstanceFlags);
                for (int i = 0; i < methods.Length; i++)
                {
                    MethodInfo method = methods[i];
                    string name = method.Name ?? string.Empty;
                    if (name.IndexOf("Get", StringComparison.OrdinalIgnoreCase) < 0 &&
                        name.IndexOf("Find", StringComparison.OrdinalIgnoreCase) < 0 &&
                        name.IndexOf("Module", StringComparison.OrdinalIgnoreCase) < 0)
                        continue;

                    if (method.IsGenericMethodDefinition && method.GetGenericArguments().Length == 1 && method.GetParameters().Length == 0)
                    {
                        try
                        {
                            MethodInfo closed = method.MakeGenericMethod(target);
                            object value = closed.Invoke(state, new object[0]);
                            if (value != null && target.IsInstanceOfType(value)) return value;
                        }
                        catch { }
                    }

                    ParameterInfo[] p = method.GetParameters();
                    if (!method.IsGenericMethodDefinition && p.Length == 1 && p[0].ParameterType == typeof(Type))
                    {
                        try
                        {
                            object value = method.Invoke(state, new object[] { target });
                            if (value != null && target.IsInstanceOfType(value)) return value;
                        }
                        catch { }
                    }
                }
            }
            catch { }

            try
            {
                List<MemberInfo> members = GetReadableMembers(state.GetType());
                for (int i = 0; i < members.Count; i++)
                {
                    object value = GetMemberValue(state, members[i]);
                    object found = FindTargetInModuleContainer(value, target, 2, new HashSet<object>(ReferenceComparer.Instance));
                    if (found != null) return found;
                }
            }
            catch { }
            return null;
        }

        private static object FindTargetInModuleContainer(object value, Type target, int depth, HashSet<object> visited)
        {
            if (value == null || target == null || depth < 0) return null;
            if (target.IsInstanceOfType(value)) return value;
            if (visited.Contains(value)) return null;
            visited.Add(value);

            IDictionary dict = value as IDictionary;
            if (dict != null)
            {
                foreach (DictionaryEntry entry in dict)
                {
                    if (entry.Value != null && target.IsInstanceOfType(entry.Value)) return entry.Value;
                    if (entry.Key != null && target.IsInstanceOfType(entry.Key)) return entry.Key;
                }
                return null;
            }

            IEnumerable enumerable = value as IEnumerable;
            if (enumerable != null && !(value is string))
            {
                int count = 0;
                foreach (object entry in enumerable)
                {
                    if (++count > 256) break;
                    if (entry != null && target.IsInstanceOfType(entry)) return entry;
                    object nested = FindTargetInModuleContainer(entry, target, depth - 1, visited);
                    if (nested != null) return nested;
                }
                return null;
            }

            if (depth == 0 || IsSimple(value.GetType())) return null;
            string ns = value.GetType().Namespace ?? string.Empty;
            if (!ns.StartsWith("MGSC", StringComparison.Ordinal)) return null;

            List<MemberInfo> members = GetReadableMembers(value.GetType());
            for (int i = 0; i < members.Count; i++)
            {
                object child = GetMemberValue(value, members[i]);
                if (child == null) continue;
                if (target.IsInstanceOfType(child)) return child;
                object nested = FindTargetInModuleContainer(child, target, depth - 1, visited);
                if (nested != null) return nested;
            }
            return null;
        }

        private static void BeginRuntimeFallbackResolver()
        {
            if (_runtimeFallbackResolveActive) return;
            try
            {
                if (_runtimeResolveOwnerTypes == null)
                {
                    try { _runtimeResolveOwnerTypes = typeof(Data).Assembly.GetTypes(); }
                    catch (ReflectionTypeLoadException ex) { _runtimeResolveOwnerTypes = ex.Types; }
                }
                _runtimeResolveOwnerIndex = 0;
                _runtimeFallbackResolveActive = _runtimeResolveOwnerTypes != null && _runtimeResolveOwnerTypes.Length > 0;
            }
            catch
            {
                _runtimeFallbackResolveActive = false;
            }
        }

        private static void TickRuntimeFallbackResolver()
        {
            if (!_runtimeFallbackResolveActive || _runtimeResolveOwnerTypes == null) return;
            EnsureStateServiceTypesResolved();
            Type stationsType = _stateStationsType;
            Type stationSystemType = _stateStationSystemType;
            Type tradeType = _stateTradeType;
            Type pricesType = _statePricesType;

            int budget = 12;
            while (budget-- > 0 && _runtimeResolveOwnerIndex < _runtimeResolveOwnerTypes.Length)
            {
                Type owner = _runtimeResolveOwnerTypes[_runtimeResolveOwnerIndex++];
                if (owner == null) continue;

                FieldInfo[] fields;
                try { fields = owner.GetFields(StaticFlags); } catch { fields = new FieldInfo[0]; }
                for (int i = 0; i < fields.Length; i++)
                {
                    FieldInfo field = fields[i];
                    Type fieldType = field.FieldType;
                    bool wantStations = _stationsState == null && stationsType != null && stationsType.IsAssignableFrom(fieldType);
                    bool wantStationSystem = _stationSystem == null && stationSystemType != null && stationSystemType.IsAssignableFrom(fieldType);
                    bool wantTrade = _tradeSystem == null && tradeType != null && tradeType.IsAssignableFrom(fieldType);
                    bool wantPrices = _worldPricesSystem == null && pricesType != null && pricesType.IsAssignableFrom(fieldType);
                    if (!wantStations && !wantStationSystem && !wantTrade && !wantPrices) continue;
                    try
                    {
                        object value = field.GetValue(null);
                        if (value == null) continue;
                        if (wantStations) _stationsState = value;
                        if (wantStationSystem) _stationSystem = value;
                        if (wantTrade) _tradeSystem = value;
                        if (wantPrices) _worldPricesSystem = value;
                    }
                    catch { }
                }

                PropertyInfo[] props;
                try { props = owner.GetProperties(StaticFlags); } catch { props = new PropertyInfo[0]; }
                for (int i = 0; i < props.Length; i++)
                {
                    PropertyInfo prop = props[i];
                    if (!prop.CanRead || prop.GetIndexParameters().Length != 0) continue;
                    Type propType = prop.PropertyType;
                    bool wantStations = _stationsState == null && stationsType != null && stationsType.IsAssignableFrom(propType);
                    bool wantStationSystem = _stationSystem == null && stationSystemType != null && stationSystemType.IsAssignableFrom(propType);
                    bool wantTrade = _tradeSystem == null && tradeType != null && tradeType.IsAssignableFrom(propType);
                    bool wantPrices = _worldPricesSystem == null && pricesType != null && pricesType.IsAssignableFrom(propType);
                    if (!wantStations && !wantStationSystem && !wantTrade && !wantPrices) continue;
                    try
                    {
                        object value = prop.GetValue(null, null);
                        if (value == null) continue;
                        if (wantStations) _stationsState = value;
                        if (wantStationSystem) _stationSystem = value;
                        if (wantTrade) _tradeSystem = value;
                        if (wantPrices) _worldPricesSystem = value;
                    }
                    catch { }
                }

                if ((_stationsState != null || _stationSystem != null) && _itemsPrices != null && _factionsState != null)
                {
                    _runtimeFallbackResolveActive = false;
                    Debug.Log("[ItemIntelligence] Incremental market services resolved after " + _runtimeResolveOwnerIndex + " owner types.");
                    return;
                }
            }

            if (_runtimeResolveOwnerIndex >= _runtimeResolveOwnerTypes.Length)
            {
                _runtimeFallbackResolveActive = false;
                Debug.Log("[ItemIntelligence] Incremental market service resolver finished. StationSystem=" + (_stationSystem != null) +
                    ", Stations=" + (_stationsState != null) +
                    ", ItemsPrices=" + (_itemsPrices != null) + ", Factions=" + (_factionsState != null) + ".");
            }
        }

        private static void TryResolveRuntimeServicesLightweight()
        {
            if (_marketResolveAttempted && _stationsState != null) return;
            _marketResolveAttempted = true;
            try
            {
                Type stationsType = AccessTools.TypeByName("MGSC.Stations");
                Type stationSystemType = AccessTools.TypeByName("MGSC.StationSystem");
                Type tradeType = AccessTools.TypeByName("MGSC.TradeSystem");
                Type pricesType = AccessTools.TypeByName("MGSC.WorldPricesSystem");
                Type itemsPricesType = AccessTools.TypeByName("MGSC.ItemsPrices");
                Type factionsType = AccessTools.TypeByName("MGSC.Factions");

                object stateRoot = null;
                try { if (_modContext != null) stateRoot = _modContext.State; } catch { }
                object[] roots = new object[] { stateRoot, _modContext, _stationSystem, _activeTooltipFactory, _activeTooltip };
                for (int r = 0; r < roots.Length; r++)
                {
                    object root = roots[r];
                    if (root == null) continue;
                    if (_stationSystem == null && stationSystemType != null)
                        _stationSystem = FindNestedRuntimeObject(root, stationSystemType, 3, new HashSet<object>(ReferenceComparer.Instance));
                    if (_stationsState == null && stationsType != null)
                        _stationsState = FindNestedRuntimeObject(root, stationsType, 3, new HashSet<object>(ReferenceComparer.Instance));
                    if (_tradeSystem == null && tradeType != null)
                        _tradeSystem = FindNestedRuntimeObject(root, tradeType, 3, new HashSet<object>(ReferenceComparer.Instance));
                    if (_worldPricesSystem == null && pricesType != null)
                        _worldPricesSystem = FindNestedRuntimeObject(root, pricesType, 3, new HashSet<object>(ReferenceComparer.Instance));
                    if (_itemsPrices == null && itemsPricesType != null)
                        _itemsPrices = FindNestedRuntimeObject(root, itemsPricesType, 4, new HashSet<object>(ReferenceComparer.Instance));
                    if (_factionsState == null && factionsType != null)
                        _factionsState = FindNestedRuntimeObject(root, factionsType, 4, new HashSet<object>(ReferenceComparer.Instance));
                }
            }
            catch { }
        }

        private static object FindNestedRuntimeObject(object root, Type target, int depth, HashSet<object> visited)
        {
            if (root == null || target == null) return null;
            if (target.IsInstanceOfType(root)) return root;
            if (depth <= 0 || IsSimple(root.GetType()) || visited.Contains(root)) return null;
            visited.Add(root);

            List<MemberInfo> members = GetReadableMembers(root.GetType());
            for (int i = 0; i < members.Count; i++)
            {
                object value = GetMemberValue(root, members[i]);
                if (value == null) continue;
                if (target.IsInstanceOfType(value)) return value;
                Type valueType = value.GetType();
                if (ShouldTraverse(valueType))
                {
                    object nested = FindNestedRuntimeObject(value, target, depth - 1, visited);
                    if (nested != null) return nested;
                }
            }
            return null;
        }
    }
}
