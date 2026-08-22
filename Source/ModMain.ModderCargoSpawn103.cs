using System;
using System.Reflection;
using MGSC;
using HarmonyLib;
using UnityEngine;

namespace ItemIntelligence
{
    /// <summary>
    /// Exact Quasimorph 1.0.3 ship-cargo spawn boundary for Modder Mode.
    /// Uses the same CreateForInventory -> MagnumCargoSystem.AddCargo call shape
    /// observed in vanilla space scenarios; no developer-console availability gate.
    /// </summary>
    public static partial class ModMain
    {
        private static bool TrySpawnModderItemToCargoViaSystem103(object cargo, string itemId)
        {
            if (cargo == null || string.IsNullOrEmpty(itemId) || !ModderMode) return false;
            try
            {
                Type spaceTimeType = AccessTools.TypeByName("MGSC.SpaceTime");
                Type cargoSystemType = AccessTools.TypeByName("MGSC.MagnumCargoSystem");
                if (spaceTimeType == null || cargoSystemType == null) return false;

                object spaceTime = ResolveStateModule(spaceTimeType);
                if (spaceTime == null) return false;

                ItemFactory factory = SingletonMonoBehaviour<ItemFactory>.Instance;
                if (factory == null) return false;
                BasePickupItem item = factory.CreateForInventory(itemId, false, false);
                if (item == null) return false;

                MethodInfo addCargo = null;
                MethodInfo[] methods = cargoSystemType.GetMethods(StaticFlags);
                for (int i = 0; i < methods.Length; i++)
                {
                    MethodInfo method = methods[i];
                    if (!string.Equals(method.Name, "AddCargo", StringComparison.Ordinal) ||
                        method.ReturnType != typeof(void)) continue;
                    ParameterInfo[] p = method.GetParameters();
                    if (p.Length != 6 || !p[0].ParameterType.IsInstanceOfType(cargo) ||
                        !p[1].ParameterType.IsInstanceOfType(spaceTime) ||
                        !p[2].ParameterType.IsInstanceOfType(item) ||
                        !string.Equals(p[3].ParameterType.Name, "ItemStorage", StringComparison.Ordinal) ||
                        p[4].ParameterType != typeof(bool) || p[5].ParameterType != typeof(bool)) continue;
                    addCargo = method;
                    break;
                }
                if (addCargo == null || !ModderMode) return false;

                // Exact 1.0.3 vanilla call shape audited from BaseSpacemodeScenario:
                // AddCargo(cargo, spaceTime, item, null, splittedItem:false, tabFilter:true).
                addCargo.Invoke(null, new object[] { cargo, spaceTime, item, null, false, true });
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[ItemIntelligence][ModderMode] Audited 1.0.3 cargo API failed safely: " + ex.GetType().Name + ".");
                return false;
            }
        }

    }
}
