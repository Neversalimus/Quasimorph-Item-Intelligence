using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;

namespace ItemIntelligence
{
    public static partial class ModMain
    {
        // v1.7.38-test7: reflection used by CURRENT loot modifiers is cached at
        // method/type boundaries. Toggling Marauder/Organization/Field Medic must
        // not rescan State types or MethodInfo arrays for every 100+ row Loot render.
        private static readonly HashSet<string> LootModifierProbeFailures =
            new HashSet<string>(StringComparer.Ordinal);
        private static Type _lootCreaturesType;
        private static Type _lootMercenariesType;
        private static Type _lootPerkSystemType;
        private static Type _lootAmputationSystemType;
        private static bool _lootModifierTypesResolved;
        private static MethodInfo _lootPerkSumMethod;
        private static Type _lootPerkCreatureDataType;
        private static object _lootPerkMatchClassDefault;
        private static object _lootPerkSubClassDefault;
        private static readonly object[] LootPerkInvokeArgs = new object[4];
        private static Type _lootImplantChanceCreatureType;
        private static MethodInfo _lootImplantChanceMethod;
        private static MethodInfo _lootImplantGainChanceMethod;
        private static readonly object[] LootImplantGainInvokeArgs = new object[1];
        private static object _lootImplantBaseProgression;
        private static double _lootImplantBaseChance = -1.0;

        private static void ResetLootModifierRuntimeSessionCache()
        {
            _lootImplantBaseProgression = null;
            _lootImplantBaseChance = -1.0;
            LootPerkInvokeArgs[0] = null;
            LootPerkInvokeArgs[1] = null;
            LootImplantGainInvokeArgs[0] = null;
        }

        private static void EnsureLootModifierRuntimeContracts()
        {
            if (_lootModifierTypesResolved) return;
            _lootModifierTypesResolved = true;
            _lootCreaturesType = AccessTools.TypeByName("MGSC.Creatures");
            _lootMercenariesType = AccessTools.TypeByName("MGSC.Mercenaries");
            _lootPerkSystemType = AccessTools.TypeByName("MGSC.PerkSystem");
            _lootAmputationSystemType = AccessTools.TypeByName("MGSC.AmputationSystem");
        }

        private static void NoteLootModifierProbeFailure(string boundary, Exception ex)
        {
            if (!LootModifierProbeFailures.Add(boundary)) return;
            UnityEngine.Debug.LogWarning("[ItemIntelligence][LootModifiers] " + boundary +
                " failed safely: " + ex.GetType().Name + ": " + ex.Message);
        }

        private static object ResolveCurrentLootModifierCreatureData()
        {
            EnsureLootModifierRuntimeContracts();
            try
            {
                object creatures = _lootCreaturesType == null ? null : ResolveStateModule(_lootCreaturesType);
                object creatureData = GetMember(GetMember(creatures, "Player"), "CreatureData");
                if (creatureData != null) return creatureData;
            }
            catch (Exception ex) { NoteLootModifierProbeFailure("Creatures.Player", ex); }

            // Space-screen fallback: the combat Player may not exist, while the raid
            // mercenary still exposes the same CreatureData without touching gameplay.
            try
            {
                object mercenaries = _lootMercenariesType == null ? null : ResolveStateModule(_lootMercenariesType);
                object creatureData = GetMember(GetMember(mercenaries, "MercenaryInRaid"), "CreatureData");
                if (creatureData != null) return creatureData;
            }
            catch (Exception ex) { NoteLootModifierProbeFailure("Mercenaries.MercenaryInRaid", ex); }
            return null;
        }

        private static double GetLootPerkParameterSum(object creatureData, string parameterName)
        {
            if (creatureData == null || string.IsNullOrEmpty(parameterName)) return -1.0;
            try
            {
                EnsureLootModifierRuntimeContracts();
                if (_lootPerkSystemType == null) return -1.0;
                if (_lootPerkSumMethod == null)
                {
                    MethodInfo[] methods = _lootPerkSystemType.GetMethods(StaticFlags);
                    for (int i = 0; i < methods.Length; i++)
                    {
                        MethodInfo candidate = methods[i];
                        if (candidate == null ||
                            !string.Equals(candidate.Name, "GetPerkParameterSumFloat", StringComparison.Ordinal))
                            continue;
                        ParameterInfo[] parameters = candidate.GetParameters();
                        if (parameters == null || parameters.Length != 4 ||
                            !parameters[0].ParameterType.IsInstanceOfType(creatureData)) continue;
                        _lootPerkSumMethod = candidate;
                        _lootPerkCreatureDataType = parameters[0].ParameterType;
                        _lootPerkMatchClassDefault = CreateDefaultLootModifierArgument(parameters[2].ParameterType);
                        _lootPerkSubClassDefault = CreateDefaultLootModifierArgument(parameters[3].ParameterType);
                        break;
                    }
                }
                if (_lootPerkSumMethod == null) return -1.0;
                if (_lootPerkCreatureDataType == null ||
                    !_lootPerkCreatureDataType.IsInstanceOfType(creatureData)) return -1.0;
                LootPerkInvokeArgs[0] = creatureData;
                LootPerkInvokeArgs[1] = parameterName;
                LootPerkInvokeArgs[2] = _lootPerkMatchClassDefault;
                LootPerkInvokeArgs[3] = _lootPerkSubClassDefault;
                object raw = _lootPerkSumMethod.Invoke(null, LootPerkInvokeArgs);
                LootPerkInvokeArgs[0] = null;
                LootPerkInvokeArgs[1] = null;
                double expected;
                return TryToDoubleSafe(raw, out expected) ? Math.Max(0.0, expected) : -1.0;
            }
            catch (Exception ex)
            {
                LootPerkInvokeArgs[0] = null;
                LootPerkInvokeArgs[1] = null;
                NoteLootModifierProbeFailure("PerkSystem.GetPerkParameterSumFloat", ex);
            }
            return -1.0;
        }

        private static object CreateDefaultLootModifierArgument(Type type)
        {
            if (type == null || !type.IsValueType) return null;
            try
            {
                return type.IsEnum ? Enum.ToObject(type, 0) : Activator.CreateInstance(type);
            }
            catch (Exception ex)
            {
                NoteLootModifierProbeFailure("DefaultArgument:" + type.FullName, ex);
                return null;
            }
        }

        // FImplantDropChance is already summed by CreatureData. Cache the exact
        // method for the current CreatureData runtime type instead of resolving it
        // again on every modifier click.
        private static double GetCurrentAdditionalImplantDropChance(object creatureData)
        {
            if (creatureData == null) return -1.0;
            try
            {
                Type type = creatureData.GetType();
                if (_lootImplantChanceCreatureType != type)
                {
                    _lootImplantChanceCreatureType = type;
                    _lootImplantChanceMethod = type.GetMethod(
                        "GetAdditionalImplantDropChance", InstanceFlags, null, Type.EmptyTypes, null);
                }
                if (_lootImplantChanceMethod == null) return -1.0;
                object raw = _lootImplantChanceMethod.Invoke(creatureData, null);
                double value;
                return TryToDoubleSafe(raw, out value) ? Math.Max(0.0, value) : -1.0;
            }
            catch (Exception ex) { NoteLootModifierProbeFailure("CreatureData.GetAdditionalImplantDropChance", ex); }
            return -1.0;
        }

        private static double ResolveImplantRecoveryChance(double additionalChance)
        {
            if (additionalChance < 0.0) return -1.0;
            try
            {
                TryResolveMagnumProgressionLightweight();
                if (_magnumProgression == null) return -1.0;
                if (!ReferenceEquals(_lootImplantBaseProgression, _magnumProgression))
                {
                    EnsureLootModifierRuntimeContracts();
                    _lootImplantBaseProgression = _magnumProgression;
                    _lootImplantBaseChance = -1.0;
                    if (_lootImplantGainChanceMethod == null && _lootAmputationSystemType != null)
                    {
                        MethodInfo[] methods = _lootAmputationSystemType.GetMethods(StaticFlags);
                        for (int i = 0; i < methods.Length; i++)
                        {
                            MethodInfo candidate = methods[i];
                            if (candidate == null ||
                                !string.Equals(candidate.Name, "GetImplantGainChance", StringComparison.Ordinal))
                                continue;
                            ParameterInfo[] parameters = candidate.GetParameters();
                            if (parameters != null && parameters.Length == 1 &&
                                parameters[0].ParameterType.IsInstanceOfType(_magnumProgression))
                            {
                                _lootImplantGainChanceMethod = candidate;
                                break;
                            }
                        }
                    }
                    if (_lootImplantGainChanceMethod != null)
                    {
                        ParameterInfo[] parameters = _lootImplantGainChanceMethod.GetParameters();
                        if (parameters[0].ParameterType.IsInstanceOfType(_magnumProgression))
                        {
                            LootImplantGainInvokeArgs[0] = _magnumProgression;
                            object raw = _lootImplantGainChanceMethod.Invoke(null, LootImplantGainInvokeArgs);
                            LootImplantGainInvokeArgs[0] = null;
                            double baseChance;
                            if (TryToDoubleSafe(raw, out baseChance))
                                _lootImplantBaseChance = Math.Max(0.0, Math.Min(1.0, baseChance));
                        }
                    }
                }
                return _lootImplantBaseChance < 0.0
                    ? -1.0
                    : Math.Max(0.0, Math.Min(1.0, _lootImplantBaseChance + additionalChance));
            }
            catch (Exception ex)
            {
                LootImplantGainInvokeArgs[0] = null;
                NoteLootModifierProbeFailure("AmputationSystem.GetImplantGainChance", ex);
            }
            return -1.0;
        }
    }
}
