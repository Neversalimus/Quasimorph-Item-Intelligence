using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Reflection.Emit;
using System.Security.Cryptography;
using System.Text;
using System.Runtime.InteropServices;
using System.Threading;
using MGSC;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ItemIntelligence
{
    public static partial class ModMain
    {
        // A hash mismatch is diagnostic only. API-contract failures or observed runtime
        // exceptions are the only events that trip feature circuit breakers.
        private const string ValidatedAssemblyCSharpSha256 =
            "EFF608C5118735359CD07FEAD8A8219E1CFB557E3A5A57517DB4428F04834B8B";

        private static bool _compatStaticChecked;
        private static bool _compatRuntimeChecked;
        private static bool _compatReportWritten;
        private static string _compatAssemblySha256 = string.Empty;
        private static string _compatBuildStatus = "UNKNOWN";
        private static bool _compatCore = true;
        private static bool _compatSearchCatalog = true;
        private static bool _compatMagnum = true;
        private static bool _compatRecipes = true;
        private static bool _compatTrade = true;
        private static bool _compatAmmo = true;
        private static bool _compatDisassembly = true;
        private static bool _compatFactions = true;
        private static bool _compatLoot = true;
        private static bool _compatTooltip = true;
        private static bool _compatInputGuard = true;

        private static readonly Dictionary<string, string> CompatibilityReasons =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private static readonly HashSet<string> CompatibilityFailureLogs =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private static readonly HashSet<string> RuntimeBoundaryWarningLogs =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        private static string CompatibilityReportPath
        {
            get { return Path.Combine(ConfigDirectory, "compatibility_report.txt"); }
        }

        private static void RunCompatibilityShieldStatic()
        {
            if (_compatStaticChecked) return;
            _compatStaticChecked = true;

            _compatAssemblySha256 = ComputeAssemblySha256(typeof(Data).Assembly);
            _compatBuildStatus = string.Equals(
                _compatAssemblySha256,
                ValidatedAssemblyCSharpSha256,
                StringComparison.OrdinalIgnoreCase)
                ? "VERIFIED"
                : "UNVERIFIED BUILD";

            if (FindCachedMember(typeof(Data), "Items", true) == null)
            {
                TripCompatibilityFeature("Core", "Data.Items static member is missing.");
                TripCompatibilityFeature("SearchCatalog", "Core item database contract failed.");
            }

            if (!_compatCore)
            {
                TripCompatibilityFeature("SearchCatalog", "Core item database is unavailable.");
                TripCompatibilityFeature("Magnum", "Core game data is unavailable.");
            }

            bool produceReceipts =
                FindCachedMember(typeof(Data), "ProduceReceipts", true) != null;
            bool workbenchReceipts =
                FindCachedMember(typeof(Data), "WorkbenchReceipts", true) != null;
            if (!produceReceipts && !workbenchReceipts)
                TripCompatibilityFeature(
                    "Recipes",
                    "ProduceReceipts and WorkbenchReceipts are both missing.");

            // Trade already has several state-resolution fallbacks. Keep its contract
            // deliberately narrow so optional owner/price field changes do not disable
            // the whole Trade page.
            if (AccessTools.TypeByName("MGSC.Station") == null)
                TripCompatibilityFeature("Trade", "MGSC.Station type is missing.");

            if (AccessTools.TypeByName("MGSC.WeaponRecord") == null ||
                AccessTools.TypeByName("MGSC.AmmoRecord") == null)
                TripCompatibilityFeature(
                    "Ammo",
                    "WeaponRecord/AmmoRecord contract is missing.");

            // Chip percentages are a narrower contract than the Ammo/relations feature.
            // Verify only the two known vanilla datadisk-selection methods. A mismatch
            // hides percentages but leaves unlock contents and the rest of Ammo intact.
            VerifyChipUnlockChanceContract();

            Type itemRecordType = AccessTools.TypeByName("MGSC.ItemRecord");
            if (itemRecordType == null ||
                FindCachedMember(itemRecordType, "Disassembly", false) == null)
                TripCompatibilityFeature(
                    "Disassembly",
                    "ItemRecord.Disassembly contract is missing.");

            Type contentDropType = AccessTools.TypeByName("MGSC.ContentDropRecord");
            Type factionType = AccessTools.TypeByName("MGSC.Faction");
            if (FindCachedMember(typeof(Data), "FactionDrop", true) == null ||
                contentDropType == null ||
                factionType == null ||
                FindCachedMember(contentDropType, "TechLevel", false) == null ||
                FindCachedMember(contentDropType, "ContentIds", false) == null ||
                FindCachedMember(contentDropType, "Weight", false) == null ||
                FindCachedMember(factionType, "CurrentTechLevel", false) == null)
                TripCompatibilityFeature(
                    "Factions",
                    "Faction reward record contract changed.");

            // Loot Sources relies only on public Data collections plus reflective
            // access to the concrete weighted container collection. The exact runtime
            // GetDrop/GetDropBiomes signature is validated after entering the save.
            if (FindCachedMember(typeof(Data), "ContainerItemDrop", true) == null ||
                FindCachedMember(typeof(Data), "ObstacleContainers", true) == null ||
                FindCachedMember(typeof(Data), "MobClasses", true) == null ||
                FindCachedMember(typeof(Data), "Bramfaturas", true) == null ||
                FindCachedMember(typeof(Data), "StationTypes", true) == null ||
                FindCachedMember(typeof(Data), "Factions", true) == null)
                TripCompatibilityFeature(
                    "Loot",
                    "Loot source Data collections changed.");

            Type tooltipHandlerType =
                AccessTools.TypeByName("MGSC.ItemTooltipHandler");
            if (tooltipHandlerType == null ||
                FindCompatibleMethod(
                    tooltipHandlerType, "Initialize", 1, typeof(string)) == null ||
                FindMethodByNameAndParameterCount(
                    tooltipHandlerType, "OnPointerEnter", 1) == null ||
                FindMethodByNameAndParameterCount(
                    tooltipHandlerType, "OnPointerExit", 1) == null)
                TripCompatibilityFeature(
                    "Tooltip",
                    "ItemTooltipHandler contract changed.");

            int actionQueries = CountInputControllerActionContracts();
            if (actionQueries < 3 ||
                AccessTools.TypeByName("MGSC.DragController") == null)
                TripCompatibilityFeature(
                    "InputGuard",
                    "Modal input/drag contract changed.");

            LogCompatibilitySummary("static");
            WriteCompatibilityReport();
        }

        private static void RunCompatibilityShieldRuntime()
        {
            _compatRuntimeChecked = true;
            _compatReportWritten = false;

            // SHA is diagnostic only. A new game build can remain fully compatible.
            object items = null;
            try { items = GetStaticMember(typeof(Data), "Items"); }
            catch { items = null; }

            if (_compatCore && items == null)
            {
                TripCompatibilityFeature(
                    "Core",
                    "Data.Items is null in SpaceStarted.");
                TripCompatibilityFeature(
                    "SearchCatalog",
                    "Core item database is unavailable at runtime.");
            }

            if (_compatRecipes)
            {
                object produce =
                    GetStaticMember(typeof(Data), "ProduceReceipts");
                object workbench =
                    GetStaticMember(typeof(Data), "WorkbenchReceipts");
                if (produce == null && workbench == null)
                    TripCompatibilityFeature(
                        "Recipes",
                        "Recipe collections are unavailable at runtime.");
            }

            if (_compatDisassembly)
            {
                object global = null;
                try { global = Data.Global; }
                catch { global = null; }

                if (global == null ||
                    FindCachedMember(
                        global.GetType(),
                        "SpawnItemOnDisassembleChance",
                        false) == null)
                    TripCompatibilityFeature(
                        "Disassembly",
                        "GlobalSettings.SpawnItemOnDisassembleChance is unavailable.");
            }

            if (_compatFactions)
            {
                object drop = null;
                try { drop = Data.FactionDrop; }
                catch { drop = null; }

                if (drop == null ||
                    !ValidateFactionRewardRuntimeContract(drop))
                    TripCompatibilityFeature(
                        "Factions",
                        "FactionDrop.GetTradeItems/GetTechLevelLimit contract changed.");
            }

            if (_compatLoot)
            {
                object containerDrops = null;
                try { containerDrops = GetStaticMember(typeof(Data), "ContainerItemDrop"); }
                catch { containerDrops = null; }

                if (containerDrops == null || !ValidateLootRuntimeContract(containerDrops))
                    TripCompatibilityFeature(
                        "Loot",
                        "ContainerItemDrop GetDrop/GetDropBiomes contract changed.");
            }

            LogCompatibilitySummary("runtime");
            WriteCompatibilityReport();
        }

        private static bool ValidateFactionRewardRuntimeContract(
            object dropCollection)
        {
            if (dropCollection == null) return false;

            try
            {
                MethodInfo[] methods =
                    dropCollection.GetType().GetMethods(InstanceFlags);
                bool trade = false;
                bool techLimit = false;

                for (int i = 0; i < methods.Length; i++)
                {
                    MethodInfo method = methods[i];
                    if (method == null) continue;

                    ParameterInfo[] parameters = method.GetParameters();

                    if (string.Equals(
                            method.Name,
                            "GetTradeItems",
                            StringComparison.Ordinal) &&
                        parameters != null &&
                        parameters.Length == 4 &&
                        parameters[1].ParameterType == typeof(int) &&
                        parameters[2].ParameterType != null &&
                        parameters[2].ParameterType.IsEnum &&
                        parameters[3].ParameterType == typeof(bool))
                        trade = true;

                    if (string.Equals(
                            method.Name,
                            "GetTechLevelLimit",
                            StringComparison.Ordinal) &&
                        parameters != null &&
                        parameters.Length == 1)
                        techLimit = true;
                }

                return trade && techLimit;
            }
            catch { return false; }
        }

        private static bool ValidateLootRuntimeContract(object containerDrops)
        {
            if (containerDrops == null) return false;

            try
            {
                MethodInfo getDrop = null;
                MethodInfo getBiomes = null;
                MethodInfo[] methods = containerDrops.GetType().GetMethods(InstanceFlags);

                for (int i = 0; i < methods.Length; i++)
                {
                    MethodInfo method = methods[i];
                    if (method == null) continue;
                    ParameterInfo[] p = method.GetParameters();

                    if (string.Equals(method.Name, "GetDrop", StringComparison.Ordinal) &&
                        p != null && p.Length == 2 &&
                        p[0].ParameterType == typeof(string) &&
                        p[1].ParameterType == typeof(string))
                        getDrop = method;

                    if (string.Equals(method.Name, "GetDropBiomes", StringComparison.Ordinal) &&
                        p != null && p.Length == 1 &&
                        p[0].ParameterType == typeof(string))
                        getBiomes = method;
                }

                return getDrop != null && getBiomes != null;
            }
            catch { return false; }
        }

        private static MethodInfo FindCompatibleMethod(
            Type type,
            string name,
            int parameterCount,
            Type firstParameterType)
        {
            if (type == null || string.IsNullOrEmpty(name))
                return null;

            try
            {
                MethodInfo[] methods =
                    type.GetMethods(InstanceFlags | StaticFlags);

                for (int i = 0; i < methods.Length; i++)
                {
                    MethodInfo method = methods[i];
                    if (method == null ||
                        !string.Equals(
                            method.Name,
                            name,
                            StringComparison.Ordinal))
                        continue;

                    ParameterInfo[] parameters =
                        method.GetParameters();
                    if (parameters == null ||
                        parameters.Length != parameterCount)
                        continue;

                    if (parameterCount > 0 &&
                        firstParameterType != null &&
                        parameters[0].ParameterType != firstParameterType)
                        continue;

                    return method;
                }
            }
            catch { }

            return null;
        }

        private static MethodInfo FindMethodByNameAndParameterCount(
            Type type,
            string name,
            int parameterCount)
        {
            if (type == null || string.IsNullOrEmpty(name))
                return null;

            try
            {
                MethodInfo[] methods =
                    type.GetMethods(InstanceFlags | StaticFlags);

                for (int i = 0; i < methods.Length; i++)
                {
                    MethodInfo method = methods[i];
                    if (method == null ||
                        !string.Equals(
                            method.Name,
                            name,
                            StringComparison.Ordinal))
                        continue;

                    ParameterInfo[] parameters =
                        method.GetParameters();

                    if (parameters != null &&
                        parameters.Length == parameterCount)
                        return method;
                }
            }
            catch { }

            return null;
        }

        private static int CountInputControllerActionContracts()
        {
            bool down = false;
            bool held = false;
            bool up = false;

            try
            {
                MethodInfo[] methods =
                    typeof(InputController).GetMethods(
                        BindingFlags.Static |
                        BindingFlags.Instance |
                        BindingFlags.Public |
                        BindingFlags.NonPublic);

                for (int i = 0; i < methods.Length; i++)
                {
                    MethodInfo method = methods[i];
                    if (method == null ||
                        method.ReturnType != typeof(bool))
                        continue;

                    if (string.Equals(
                        method.Name,
                        "IsKeyDown",
                        StringComparison.Ordinal))
                        down = true;
                    else if (string.Equals(
                        method.Name,
                        "IsKey",
                        StringComparison.Ordinal))
                        held = true;
                    else if (string.Equals(
                        method.Name,
                        "IsKeyUp",
                        StringComparison.Ordinal))
                        up = true;
                }
            }
            catch { }

            return
                (down ? 1 : 0) +
                (held ? 1 : 0) +
                (up ? 1 : 0);
        }

        private static readonly OpCode[] ChipIlOneByteOpCodes = new OpCode[0x100];
        private static readonly OpCode[] ChipIlTwoByteOpCodes = new OpCode[0x100];
        private static bool _chipIlOpcodeTablesReady;

        private static void VerifyChipUnlockChanceContract()
        {
            if (_chipUnlockChanceContractChecked) return;
            _chipUnlockChanceContractChecked = true;

            string createReason;
            string spawnReason;
            bool createOk = VerifyDatadiskSelectionMethod(
                "MGSC.ItemFactory",
                "CreateComponent",
                out createReason);
            bool spawnOk = VerifyDatadiskSelectionMethod(
                "MGSC.SpawnItemCommand",
                "Execute",
                out spawnReason);

            _chipUnlockChanceContractVerified = createOk && spawnOk;
            _chipUnlockChanceContractReason =
                "ItemFactory.CreateComponent=" + (createOk ? "OK" : createReason) +
                "; SpawnItemCommand.Execute=" + (spawnOk ? "OK" : spawnReason);

            if (_chipUnlockChanceContractVerified)
            {
                Debug.Log(
                    "[ItemIntelligence] Chip unlock chance contract: VERIFIED; " +
                    _chipUnlockChanceContractReason + ".");
            }
            else
            {
                LogRuntimeBoundaryWarningOnce(
                    "chip.chance.contract",
                    "Chip unlock chance contract is unverified; percentages are hidden. " +
                    _chipUnlockChanceContractReason + ".",
                    null);
            }
        }

        private static bool VerifyDatadiskSelectionMethod(
            string typeName,
            string methodName,
            out string reason)
        {
            reason = "method missing";
            Type type = AccessTools.TypeByName(typeName);
            if (type == null)
            {
                reason = "type missing";
                return false;
            }

            MethodInfo[] methods;
            try { methods = type.GetMethods(InstanceFlags | StaticFlags); }
            catch (Exception ex)
            {
                reason = ex.GetType().Name;
                return false;
            }

            bool foundNamedMethod = false;
            string lastReason = "selection sequence not found";
            for (int i = 0; i < methods.Length; i++)
            {
                MethodInfo method = methods[i];
                if (method == null ||
                    !string.Equals(method.Name, methodName, StringComparison.Ordinal))
                    continue;

                foundNamedMethod = true;
                string methodReason;
                if (MethodHasUniformDatadiskSelectionContract(method, out methodReason))
                {
                    reason = "OK";
                    return true;
                }
                lastReason = methodReason;
            }

            reason = foundNamedMethod ? lastReason : "method missing";
            return false;
        }

        private static bool MethodHasUniformDatadiskSelectionContract(
            MethodInfo method,
            out string reason)
        {
            reason = "IL unavailable";
            List<MethodBase> calls = new List<MethodBase>();
            if (!TryReadCalledMethodsInOrder(method, calls, out reason))
                return false;

            int unlockFirst = FindCalledMethod(calls, 0, "MGSC.DatadiskRecord", "get_UnlockIds");
            if (unlockFirst < 0) { reason = "UnlockIds missing"; return false; }

            int unlockSecond = FindCalledMethod(calls, unlockFirst + 1, "MGSC.DatadiskRecord", "get_UnlockIds");
            if (unlockSecond < 0) { reason = "second UnlockIds missing"; return false; }

            int count = FindGenericListMethod(calls, unlockSecond + 1, "get_Count");
            if (count < 0) { reason = "UnlockIds.Count missing"; return false; }

            int range = FindUnityRandomRangeInt(calls, count + 1);
            if (range < 0) { reason = "Random.Range(int,int) missing"; return false; }

            int item = FindGenericListMethod(calls, range + 1, "get_Item");
            if (item < 0) { reason = "UnlockIds[index] missing"; return false; }

            int setUnlock = FindCalledMethod(calls, item + 1, "MGSC.DatadiskComponent", "SetUnlockId");
            if (setUnlock < 0) { reason = "SetUnlockId missing"; return false; }

            for (int i = 0; i <= setUnlock && i < calls.Count; i++)
            {
                MethodBase call = calls[i];
                if (call != null && string.Equals(call.Name, "IsAlreadyUnlockedDatadisk", StringComparison.Ordinal))
                {
                    reason = "unlocked-state filter occurs before selection";
                    return false;
                }
            }

            reason = "OK";
            return true;
        }

        private static int FindCalledMethod(
            List<MethodBase> calls,
            int start,
            string declaringTypeName,
            string methodName)
        {
            if (calls == null) return -1;
            for (int i = Math.Max(0, start); i < calls.Count; i++)
            {
                MethodBase call = calls[i];
                if (call == null || !string.Equals(call.Name, methodName, StringComparison.Ordinal))
                    continue;
                Type declaring = call.DeclaringType;
                string fullName = declaring == null ? string.Empty : (declaring.FullName ?? string.Empty);
                if (string.Equals(fullName, declaringTypeName, StringComparison.Ordinal))
                    return i;
            }
            return -1;
        }

        private static int FindGenericListMethod(List<MethodBase> calls, int start, string methodName)
        {
            if (calls == null) return -1;
            for (int i = Math.Max(0, start); i < calls.Count; i++)
            {
                MethodBase call = calls[i];
                if (call == null || !string.Equals(call.Name, methodName, StringComparison.Ordinal))
                    continue;
                Type declaring = call.DeclaringType;
                if (declaring == null) continue;
                string fullName = declaring.FullName ?? string.Empty;
                if (fullName.IndexOf("System.Collections.Generic.List`1", StringComparison.Ordinal) >= 0)
                    return i;
            }
            return -1;
        }

        private static int FindUnityRandomRangeInt(List<MethodBase> calls, int start)
        {
            if (calls == null) return -1;
            for (int i = Math.Max(0, start); i < calls.Count; i++)
            {
                MethodInfo call = calls[i] as MethodInfo;
                if (call == null || !string.Equals(call.Name, "Range", StringComparison.Ordinal))
                    continue;
                Type declaring = call.DeclaringType;
                if (declaring == null || !string.Equals(declaring.FullName, "UnityEngine.Random", StringComparison.Ordinal))
                    continue;
                ParameterInfo[] p;
                try { p = call.GetParameters(); }
                catch { continue; }
                if (p.Length == 2 && p[0].ParameterType == typeof(int) && p[1].ParameterType == typeof(int) &&
                    call.ReturnType == typeof(int))
                    return i;
            }
            return -1;
        }

        private static bool TryReadCalledMethodsInOrder(
            MethodInfo method,
            List<MethodBase> calls,
            out string reason)
        {
            reason = "IL unavailable";
            if (method == null || calls == null) return false;

            MethodBody body;
            byte[] il;
            try
            {
                body = method.GetMethodBody();
                if (body == null) { reason = "method body missing"; return false; }
                il = body.GetILAsByteArray();
            }
            catch (Exception ex)
            {
                reason = ex.GetType().Name;
                return false;
            }
            if (il == null || il.Length == 0) { reason = "IL empty"; return false; }

            EnsureChipIlOpcodeTables();
            Module module = method.Module;
            Type[] typeArgs = Type.EmptyTypes;
            Type[] methodArgs = Type.EmptyTypes;
            try
            {
                if (method.DeclaringType != null && method.DeclaringType.IsGenericType)
                    typeArgs = method.DeclaringType.GetGenericArguments();
                if (method.IsGenericMethod)
                    methodArgs = method.GetGenericArguments();
            }
            catch { }

            int pos = 0;
            while (pos < il.Length)
            {
                OpCode op;
                byte first = il[pos++];
                if (first == 0xFE)
                {
                    if (pos >= il.Length) { reason = "truncated opcode"; return false; }
                    op = ChipIlTwoByteOpCodes[il[pos++]];
                }
                else
                {
                    op = ChipIlOneByteOpCodes[first];
                }

                if (string.IsNullOrEmpty(op.Name))
                {
                    reason = "unknown opcode";
                    return false;
                }

                if (op.OperandType == OperandType.InlineMethod)
                {
                    if (pos + 4 > il.Length) { reason = "truncated method token"; return false; }
                    int token = BitConverter.ToInt32(il, pos);
                    MethodBase called = null;
                    try { called = module.ResolveMethod(token, typeArgs, methodArgs); }
                    catch
                    {
                        try { called = module.ResolveMethod(token); }
                        catch { called = null; }
                    }
                    if (called != null) calls.Add(called);
                    pos += 4;
                    continue;
                }

                int operandSize = GetIlOperandSize(op.OperandType, il, pos, out reason);
                if (operandSize < 0) return false;
                if (pos + operandSize > il.Length) { reason = "truncated operand"; return false; }
                pos += operandSize;
            }

            reason = "OK";
            return true;
        }

        private static int GetIlOperandSize(
            OperandType operandType,
            byte[] il,
            int operandStart,
            out string reason)
        {
            reason = "OK";
            switch (operandType)
            {
                case OperandType.InlineNone: return 0;
                case OperandType.ShortInlineBrTarget:
                case OperandType.ShortInlineI:
                case OperandType.ShortInlineVar: return 1;
                case OperandType.InlineVar: return 2;
                case OperandType.InlineBrTarget:
                case OperandType.InlineField:
                case OperandType.InlineI:
                case OperandType.InlineMethod:
                case OperandType.InlineSig:
                case OperandType.InlineString:
                case OperandType.InlineTok:
                case OperandType.InlineType:
                case OperandType.ShortInlineR: return 4;
                case OperandType.InlineI8:
                case OperandType.InlineR: return 8;
                case OperandType.InlineSwitch:
                    if (operandStart + 4 > il.Length)
                    {
                        reason = "truncated switch";
                        return -1;
                    }
                    int count = BitConverter.ToInt32(il, operandStart);
                    if (count < 0 || count > 100000)
                    {
                        reason = "invalid switch";
                        return -1;
                    }
                    return 4 + (count * 4);
                default:
                    reason = "unsupported operand type " + operandType;
                    return -1;
            }
        }

        private static void EnsureChipIlOpcodeTables()
        {
            if (_chipIlOpcodeTablesReady) return;
            FieldInfo[] fields = typeof(OpCodes).GetFields(BindingFlags.Public | BindingFlags.Static);
            for (int i = 0; i < fields.Length; i++)
            {
                FieldInfo field = fields[i];
                if (field == null || field.FieldType != typeof(OpCode)) continue;
                OpCode op;
                try { op = (OpCode)field.GetValue(null); }
                catch { continue; }
                int value = unchecked((ushort)op.Value);
                if (value < 0x100)
                    ChipIlOneByteOpCodes[value] = op;
                else if ((value & 0xFF00) == 0xFE00)
                    ChipIlTwoByteOpCodes[value & 0xFF] = op;
            }
            _chipIlOpcodeTablesReady = true;
        }

        private static string ComputeAssemblySha256(Assembly assembly)
        {
            // Mod loaders may shadow-copy the loaded Assembly-CSharp. Exact-math gates
            // must fingerprint the physical game binary BUILD_AND_STAGE compiled against,
            // not a loader-owned copy whose bytes/path can differ at runtime.
            string managedPath = string.Empty;
            try
            {
                if (!string.IsNullOrEmpty(Application.dataPath))
                    managedPath = Path.Combine(Application.dataPath, "Managed", "Assembly-CSharp.dll");
            }
            catch { managedPath = string.Empty; }

            string managedHash = ComputeFileSha256Safe(managedPath);
            if (!string.IsNullOrEmpty(managedHash)) return managedHash;

            if (assembly == null) return string.Empty;
            string loadedPath = string.Empty;
            try { loadedPath = assembly.Location; }
            catch { loadedPath = string.Empty; }
            return ComputeFileSha256Safe(loadedPath);
        }

        private static string ComputeFileSha256Safe(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return string.Empty;
            try
            {
                using (SHA256 sha = SHA256.Create())
                using (FileStream stream = File.OpenRead(path))
                {
                    byte[] hash = sha.ComputeHash(stream);
                    return BitConverter.ToString(hash).Replace("-", string.Empty);
                }
            }
            catch { return string.Empty; }
        }

        private static void TripCompatibilityFeature(
            string feature,
            string reason)
        {
            if (string.IsNullOrEmpty(feature)) return;

            SetCompatibilityFeature(feature, false);

            if (!string.IsNullOrEmpty(reason))
                CompatibilityReasons[feature] = reason;

            string logKey =
                feature + "|" + (reason ?? string.Empty);

            if (CompatibilityFailureLogs.Add(logKey))
                Debug.LogWarning(
                    "[ItemIntelligence] Compatibility Shield disabled " +
                    feature + ": " +
                    (reason ?? "unknown contract failure"));

            _compatReportWritten = false;
        }

        private static void TripCompatibilityFeatureRuntime(
            string feature,
            Exception ex)
        {
            string reason = ex == null
                ? "runtime exception"
                : ex.GetType().Name + ": " + ex.Message;

            TripCompatibilityFeature(feature, reason);
            WriteCompatibilityReport();
            WriteDiagnosticsReportSafe("CompatibilityTrip:" + (feature ?? string.Empty));
        }

        private static void LogRuntimeBoundaryWarningOnce(
            string key,
            string message,
            Exception ex)
        {
            string normalizedKey = string.IsNullOrEmpty(key) ? "unknown" : key;
            if (!RuntimeBoundaryWarningLogs.Add(normalizedKey)) return;

            string suffix = ex == null
                ? string.Empty
                : " " + ex.GetType().Name + ": " + ex.Message;
            Debug.LogWarning(
                "[ItemIntelligence] Safety recovery: " +
                (message ?? normalizedKey) + suffix);
        }

        private static void SetCompatibilityFeature(
            string feature,
            bool value)
        {
            if (string.Equals(
                    feature,
                    "Core",
                    StringComparison.OrdinalIgnoreCase))
                _compatCore = value;
            else if (string.Equals(
                    feature,
                    "SearchCatalog",
                    StringComparison.OrdinalIgnoreCase))
                _compatSearchCatalog = value;
            else if (string.Equals(
                    feature,
                    "Magnum",
                    StringComparison.OrdinalIgnoreCase))
                _compatMagnum = value;
            else if (string.Equals(
                    feature,
                    "Recipes",
                    StringComparison.OrdinalIgnoreCase))
                _compatRecipes = value;
            else if (string.Equals(
                    feature,
                    "Trade",
                    StringComparison.OrdinalIgnoreCase))
                _compatTrade = value;
            else if (string.Equals(
                    feature,
                    "Ammo",
                    StringComparison.OrdinalIgnoreCase))
                _compatAmmo = value;
            else if (string.Equals(
                    feature,
                    "Disassembly",
                    StringComparison.OrdinalIgnoreCase))
                _compatDisassembly = value;
            else if (string.Equals(
                    feature,
                    "Factions",
                    StringComparison.OrdinalIgnoreCase))
                _compatFactions = value;
            else if (string.Equals(
                    feature,
                    "Loot",
                    StringComparison.OrdinalIgnoreCase))
                _compatLoot = value;
            else if (string.Equals(
                    feature,
                    "Tooltip",
                    StringComparison.OrdinalIgnoreCase))
                _compatTooltip = value;
            else if (string.Equals(
                    feature,
                    "InputGuard",
                    StringComparison.OrdinalIgnoreCase))
                _compatInputGuard = value;
        }

        private static bool GetCompatibilityFeature(string feature)
        {
            if (string.Equals(
                    feature,
                    "Core",
                    StringComparison.OrdinalIgnoreCase))
                return _compatCore;
            if (string.Equals(
                    feature,
                    "SearchCatalog",
                    StringComparison.OrdinalIgnoreCase))
                return _compatSearchCatalog;
            if (string.Equals(
                    feature,
                    "Magnum",
                    StringComparison.OrdinalIgnoreCase))
                return _compatMagnum;
            if (string.Equals(
                    feature,
                    "Recipes",
                    StringComparison.OrdinalIgnoreCase))
                return _compatRecipes;
            if (string.Equals(
                    feature,
                    "Trade",
                    StringComparison.OrdinalIgnoreCase))
                return _compatTrade;
            if (string.Equals(
                    feature,
                    "Ammo",
                    StringComparison.OrdinalIgnoreCase))
                return _compatAmmo;
            if (string.Equals(
                    feature,
                    "Disassembly",
                    StringComparison.OrdinalIgnoreCase))
                return _compatDisassembly;
            if (string.Equals(
                    feature,
                    "Factions",
                    StringComparison.OrdinalIgnoreCase))
                return _compatFactions;
            if (string.Equals(
                    feature,
                    "Loot",
                    StringComparison.OrdinalIgnoreCase))
                return _compatLoot;
            if (string.Equals(
                    feature,
                    "Tooltip",
                    StringComparison.OrdinalIgnoreCase))
                return _compatTooltip;
            if (string.Equals(
                    feature,
                    "InputGuard",
                    StringComparison.OrdinalIgnoreCase))
                return _compatInputGuard;

            return true;
        }

        private static string GetCompatibilityReason(string feature)
        {
            string reason;
            return
                CompatibilityReasons.TryGetValue(
                    feature,
                    out reason) &&
                !string.IsNullOrEmpty(reason)
                    ? reason
                    : "game API compatibility check failed";
        }

        private static string CompatibilityState(string feature)
        {
            return GetCompatibilityFeature(feature)
                ? "OK"
                : "DISABLED";
        }

        private static void LogCompatibilitySummary(string phase)
        {
            Debug.Log(
                "[ItemIntelligence] Compatibility Shield " +
                phase +
                ": build=" + _compatBuildStatus +
                ", Core=" + CompatibilityState("Core") +
                ", SearchCatalog=" + CompatibilityState("SearchCatalog") +
                ", Magnum=" + CompatibilityState("Magnum") +
                ", Recipes=" + CompatibilityState("Recipes") +
                ", Trade=" + CompatibilityState("Trade") +
                ", Ammo=" + CompatibilityState("Ammo") +
                ", ChipChance=" + (_chipUnlockChanceContractVerified ? "VERIFIED" : "HIDDEN") +
                ", Disassembly=" + CompatibilityState("Disassembly") +
                ", Factions=" + CompatibilityState("Factions") +
                ", Loot=" + CompatibilityState("Loot") +
                ", Tooltip=" + CompatibilityState("Tooltip") +
                ", Input=" + CompatibilityState("InputGuard") +
                ".");
        }

        private static bool IsHealthyVerifiedCompatibilityState()
        {
            return string.Equals(_compatBuildStatus, "VERIFIED", StringComparison.OrdinalIgnoreCase) &&
                _compatCore && _compatSearchCatalog && _compatMagnum && _compatRecipes &&
                _compatTrade && _compatAmmo && _compatDisassembly && _compatFactions &&
                _compatLoot && _compatTooltip && _compatInputGuard;
        }

        private static void WriteCompatibilityReport()
        {
            if (_compatReportWritten &&
                _compatRuntimeChecked)
                return;

            // Healthy verified releases do not perform synchronous report I/O on
            // startup/menu lifecycle boundaries. Unverified/degraded builds and
            // Modder Mode keep the report for troubleshooting. Runtime trips reset
            // _compatReportWritten and naturally pass this gate because a feature is off.
            if (!ModderMode && IsHealthyVerifiedCompatibilityState())
            {
                _compatReportWritten = true;
                return;
            }

            try
            {
                Directory.CreateDirectory(ConfigDirectory);

                List<string> lines =
                    new List<string>();

                lines.Add(
                    "Item Intelligence Compatibility Shield");
                lines.Add(
                    "ModVersion=" + Version);
                lines.Add(
                    "ApplicationVersion=" +
                    (Application.version ?? string.Empty));
                lines.Add("LastVerifiedGameVersion=" + LastVerifiedGameVersion);
                lines.Add("BuildFingerprint=" + (_buildFingerprint ?? string.Empty));
                lines.Add("CompatibilityVerdict=" + (_compatibilityVerdict ?? string.Empty));
                lines.Add(
                    "AssemblyCSharpSHA256=" +
                    (_compatAssemblySha256 ?? string.Empty));
                lines.Add(
                    "ValidatedSHA256=" +
                    ValidatedAssemblyCSharpSha256);
                lines.Add(
                    "BuildStatus=" +
                    _compatBuildStatus);
                lines.Add(
                    "StaticChecked=" +
                    _compatStaticChecked);
                lines.Add(
                    "RuntimeChecked=" +
                    _compatRuntimeChecked);
                lines.Add("ChipUnlockChanceContract=" +
                    (_chipUnlockChanceContractVerified ? "VERIFIED" : "HIDDEN"));
                lines.Add("ChipUnlockChanceReason=" + (_chipUnlockChanceContractReason ?? string.Empty));
                lines.Add("");

                AddCompatibilityReportLine(lines, "Core");
                AddCompatibilityReportLine(lines, "SearchCatalog");
                AddCompatibilityReportLine(lines, "Magnum");
                AddCompatibilityReportLine(lines, "Recipes");
                AddCompatibilityReportLine(lines, "Trade");
                AddCompatibilityReportLine(lines, "Ammo");
                AddCompatibilityReportLine(lines, "Disassembly");
                AddCompatibilityReportLine(lines, "Factions");
                AddCompatibilityReportLine(lines, "Loot");
                AddCompatibilityReportLine(lines, "Tooltip");
                AddCompatibilityReportLine(lines, "InputGuard");

                File.WriteAllLines(
                    CompatibilityReportPath,
                    lines.ToArray());

                _compatReportWritten = true;
            }
            catch (Exception ex)
            {
                Debug.LogWarning(
                    "[ItemIntelligence] Compatibility report write failed: " +
                    ex.Message);
            }
        }

        private static void AddCompatibilityReportLine(
            List<string> lines,
            string feature)
        {
            if (lines == null) return;

            string state =
                CompatibilityState(feature);

            string reason =
                GetCompatibilityFeature(feature)
                    ? string.Empty
                    : GetCompatibilityReason(feature);

            lines.Add(
                feature + "=" + state +
                (string.IsNullOrEmpty(reason)
                    ? string.Empty
                    : " | " + reason));
        }

        private static void AddCompatibilityUnavailableLine(
            string feature)
        {
            BrowserLines.Add(
                BrowserLine.Section(
                    Ui("ui.compatibility")));

            BrowserLines.Add(
                BrowserLine.Note(Ui("compat.disabled_feature")));

            BrowserLines.Add(
                BrowserLine.Normal(Ui("compat.module"), feature ?? string.Empty));

            BrowserLines.Add(
                BrowserLine.FullNote(Ui("compat.reason") + " " + GetCompatibilityReason(feature)));

            BrowserLines.Add(
                BrowserLine.Note(
                    Ui("ui.other_compatible_item_intelligence_features_rema")));
        }

        private static void RunCompatibilityIndexStage(
            string name,
            string feature,
            Action action,
            ref int failures)
        {
            System.Diagnostics.Stopwatch timer =
                System.Diagnostics.Stopwatch.StartNew();

            try
            {
                if (action != null)
                    action();
            }
            catch (Exception ex)
            {
                failures++;
                TripCompatibilityFeatureRuntime(
                    feature,
                    ex);
            }
            finally
            {
                timer.Stop();
                Debug.Log(
                    "[ItemIntelligence] Index stage " +
                    name + ": " +
                    timer.ElapsedMilliseconds +
                    " ms.");
            }
        }

        private static void TickMarketScanCompatibilitySafe()
        {
            if (!_compatTrade) return;

            try { TickMarketScan(); }
            catch (Exception ex)
            {
                TripCompatibilityFeatureRuntime(
                    "Trade",
                    ex);
            }
        }
    }
}
