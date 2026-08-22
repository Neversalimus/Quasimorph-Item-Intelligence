using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using MGSC;
using HarmonyLib;
using UnityEngine;

namespace ItemIntelligence
{
    /// <summary>
    /// Owns the single save-affecting Modder Mode exception. Normal Item Intelligence
    /// never enters this owner: every public UI path and both mutation boundaries recheck
    /// ModderMode, the current item and the active game context before creating anything.
    /// </summary>
    public static partial class ModMain
    {
        private static bool _modderMissionActive;
        private static object _modderCloneInventory;
        private static object _modderCargoState;
        private static MethodInfo _modderInventoryTryAddMethod;
        private static int _modderSpawnLastFrame = -1000;

        [Hook(ModHookType.DungeonStarted)]
        public static void ModderSpawnDungeonStarted(IModContext context)
        {
            if (context != null) _modContext = context;
            ResetModderSpawnRuntime(true);
        }

        [Hook(ModHookType.DungeonFinished)]
        public static void ModderSpawnDungeonFinished(IModContext context)
        {
            if (context != null) _modContext = context;
            ResetModderSpawnRuntime(false);
        }

        private static void ResetModderSpawnRuntime(bool missionActive)
        {
            _modderMissionActive = missionActive;
            _modderCloneInventory = null;
            _modderCargoState = null;
            _modderSpawnLastFrame = -1000;
            ResetModderSpawnPanelStatus();
        }

        private static bool IsModderSpawnMissionContext()
        {
            // Do not infer mission state from a cached Player object: that object can
            // survive the transition back to orbit. Lifecycle hooks are authoritative.
            return _modderMissionActive;
        }

        private static bool IsModderSpawnTargetAvailable()
        {
            if (!ModderMode) return false;
            if (IsModderSpawnMissionContext())
            {
                object inventory;
                return TryResolveModderCloneInventory(out inventory);
            }
            return TryResolveModderCargoState() != null;
        }

        private static bool TrySpawnCurrentModderItem(out string statusKey)
        {
            statusKey = "ui.modder_spawn_failed";
            if (!ModderMode || !_inspectorOpen || string.IsNullOrEmpty(_inspectorItemId))
                return false;
            if (!IsKnownItemId(_inspectorItemId))
            {
                statusKey = "ui.modder_spawn_invalid_item";
                return false;
            }
            if (_modderSpawnLastFrame == Time.frameCount)
            {
                statusKey = "ui.modder_spawn_wait";
                return false;
            }
            _modderSpawnLastFrame = Time.frameCount;

            if (IsModderSpawnMissionContext())
                return TrySpawnModderItemToClone(_inspectorItemId, out statusKey);
            return TrySpawnModderItemToCargo(_inspectorItemId, out statusKey);
        }

        private static bool TryResolveModderCloneInventory(out object inventory)
        {
            inventory = _modderCloneInventory;
            if (inventory != null) return true;
            try
            {
                object creatures = ResolveStateModule(typeof(Creatures));
                object player = GetMember(creatures, "Player");
                object mercenary = GetMember(player, "Mercenary");
                object creatureData = GetMember(mercenary, "CreatureData");
                inventory = GetMember(creatureData, "Inventory");
                if (inventory == null) return false;
                _modderCloneInventory = inventory;
                return true;
            }
            catch
            {
                inventory = null;
                return false;
            }
        }

        private static object TryResolveModderCargoState()
        {
            if (_modderCargoState != null) return _modderCargoState;
            try
            {
                Type cargoType = AccessTools.TypeByName("MGSC.MagnumCargo");
                if (cargoType != null) _modderCargoState = ResolveStateModule(cargoType);
            }
            catch { _modderCargoState = null; }
            return _modderCargoState;
        }

        private static bool TrySpawnModderItemToClone(string itemId, out string statusKey)
        {
            statusKey = "ui.modder_spawn_clone_unavailable";
            object inventory;
            if (!TryResolveModderCloneInventory(out inventory) || !ModderMode) return false;

            try
            {
                ItemFactory factory = SingletonMonoBehaviour<ItemFactory>.Instance;
                if (factory == null) return false;
                BasePickupItem item = factory.CreateForInventory(itemId, false, false);
                if (item == null) return false;

                MethodInfo add = ResolveModderInventoryTryAdd(inventory, item);
                if (add == null)
                {
                    statusKey = "ui.modder_spawn_contract_unavailable";
                    return false;
                }

                object[] args = BuildModderInventoryTryAddArguments(add, item);
                if (args == null || !ModderMode) return false;
                object raw = add.Invoke(inventory, args);
                bool added = raw is bool && (bool)raw;
                statusKey = added
                    ? "ui.modder_spawn_clone_success"
                    : "ui.modder_spawn_inventory_full";
                if (added)
                    Debug.Log("[ItemIntelligence][ModderMode] Added one item to clone inventory: " + itemId + ".");
                return added;
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[ItemIntelligence][ModderMode] Clone inventory spawn failed safely: " + ex.GetType().Name + ".");
                return false;
            }
        }

        private static MethodInfo ResolveModderInventoryTryAdd(object inventory, BasePickupItem item)
        {
            if (_modderInventoryTryAddMethod != null &&
                _modderInventoryTryAddMethod.DeclaringType.IsInstanceOfType(inventory))
                return _modderInventoryTryAddMethod;

            MethodInfo[] methods = inventory.GetType().GetMethods(InstanceFlags);
            for (int i = 0; i < methods.Length; i++)
            {
                MethodInfo method = methods[i];
                if (!string.Equals(method.Name, "TryAddItemToAnyStorage", StringComparison.Ordinal) ||
                    method.ReturnType != typeof(bool)) continue;
                ParameterInfo[] p = method.GetParameters();
                if (p.Length != 4 || !p[0].ParameterType.IsInstanceOfType(item) ||
                    !string.Equals(p[1].ParameterType.Name, "CellPosition", StringComparison.Ordinal) ||
                    !string.Equals(p[2].ParameterType.Name, "StoragePriority", StringComparison.Ordinal) ||
                    p[3].ParameterType != typeof(bool)) continue;
                _modderInventoryTryAddMethod = method;
                return method;
            }
            return null;
        }

        private static object[] BuildModderInventoryTryAddArguments(MethodInfo method, BasePickupItem item)
        {
            ParameterInfo[] p = method == null ? null : method.GetParameters();
            if (p == null || p.Length != 4) return null;
            object priority = Enum.IsDefined(p[2].ParameterType, "Backpack")
                ? Enum.Parse(p[2].ParameterType, "Backpack")
                : Activator.CreateInstance(p[2].ParameterType);
            return new object[]
            {
                item,
                Activator.CreateInstance(p[1].ParameterType),
                priority,
                false
            };
        }

        private static bool TrySpawnModderItemToCargo(string itemId, out string statusKey)
        {
            statusKey = "ui.modder_spawn_cargo_unavailable";
            object cargo = TryResolveModderCargoState();
            if (cargo == null || !ModderMode) return false;

            // On audited 1.0.3 bypass the console command availability gate and use
            // vanilla CreateForInventory -> MagnumCargoSystem.AddCargo; keep legacy fallback.
            if (IsCurrent103CargoSpawnAssembly() && TrySpawnModderItemToCargoViaSystem103(cargo, itemId))
            {
                statusKey = "ui.modder_spawn_cargo_success";
                Debug.Log("[ItemIntelligence][ModderMode] Added one item through audited 1.0.3 cargo API: " + itemId + ".");
                return true;
            }

            try
            {
                object command;
                MethodInfo execute;
                if (!TryResolveVanillaCargoCommand(out command, out execute))
                {
                    statusKey = "ui.modder_spawn_contract_unavailable";
                    return false;
                }

                // Legacy fallback: use the command object registered by the current game.
                // CommandInterface is non-public, so keep the object opaque and never create
                // a detached command instance.
                if (!ModderMode) return false;
                execute.Invoke(command, new object[] { new List<string> { itemId, "1" } });
                statusKey = "ui.modder_spawn_cargo_success";
                Debug.Log("[ItemIntelligence][ModderMode] Added one item through vanilla ship-cargo command: " + itemId + ".");
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[ItemIntelligence][ModderMode] Ship cargo spawn failed safely: " + ex.GetType().Name + ".");
                return false;
            }
        }

        private static bool TryResolveVanillaCargoCommand(out object command, out MethodInfo execute)
        {
            command = null;
            execute = null;
            try
            {
                DevConsole console = UI.Get<DevConsole>();
                object daemon = GetMember(console, "Daemon");
                IDictionary commands = GetMember(daemon, "_commands") as IDictionary;
                if (commands == null || !commands.Contains("item")) return false;

                command = commands["item"];
                if (command == null) return false;

                object available = GetMember(command, "IsAvailable");
                if (!(available is bool) || !(bool)available)
                {
                    command = null;
                    return false;
                }

                execute = command.GetType().GetMethod(
                    "Execute", InstanceFlags, null,
                    new Type[] { typeof(List<string>) }, null);
                if (execute == null || execute.IsStatic)
                {
                    command = null;
                    execute = null;
                    return false;
                }
                return true;
            }
            catch
            {
                command = null;
                execute = null;
                return false;
            }
        }
    }
}
