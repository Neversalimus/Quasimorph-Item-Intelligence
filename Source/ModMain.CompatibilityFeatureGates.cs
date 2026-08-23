using System;
using System.Collections.Generic;
using System.Reflection;
using MGSC;
using HarmonyLib;

namespace ItemIntelligence
{
    public static partial class ModMain
    {
        // Feature-owned compatibility fingerprints. The 1.0.3 hash is deliberately NOT
        // promoted to the global VERIFIED build identity until a full runtime regression
        // pass is complete. Only the exact contracts re-audited for these features use it.
        private const string AuditedFeatureAssemblySha102 =
            "EFF608C5118735359CD07FEAD8A8219E1CFB557E3A5A57517DB4428F04834B8B";
        private const string AuditedFeatureAssemblySha103 =
            "FE68E4355D4ED9CBAB7F8B1BA7717DBC1CC3FD749D0D11A644A9A3DB5EAB478F";
        // 1.0.3.578 hotfix was re-audited specifically for narrow feature paths.
        // Keep these ownership-specific aliases separate from the broader feature gate: validating
        // Trade or cargo spawning must not silently certify Loot/Scavenger/other exact families.
        private const string AuditedTradeAssemblySha103Hotfix =
            "A38C4D993C9BF60D0DDE0EDD348F201C97574F907808417A33C8A20F4772E9C1";
        private const string AuditedCargoSpawnAssemblySha103Hotfix =
            "A38C4D993C9BF60D0DDE0EDD348F201C97574F907808417A33C8A20F4772E9C1";
        // 1.0.3.578 hotfix exactness was re-audited independently for the Loot modifier,
        // container save-estimate, and Scavengers/Purge Brigade paths. Keep separate
        // aliases so passing one contract never certifies the other feature families.
        private const string AuditedLootModifiersAssemblySha103Hotfix =
            "A38C4D993C9BF60D0DDE0EDD348F201C97574F907808417A33C8A20F4772E9C1";
        private const string AuditedContainerSaveEstimateAssemblySha103Hotfix =
            "A38C4D993C9BF60D0DDE0EDD348F201C97574F907808417A33C8A20F4772E9C1";
        private const string AuditedScavengerAssemblySha103Hotfix =
            "A38C4D993C9BF60D0DDE0EDD348F201C97574F907808417A33C8A20F4772E9C1";
        // 1.0.3.578 hardcoded story acquisition and random-start source-family
        // paths were re-audited independently. This alias owns ONLY those source families.
        private const string AuditedSourceFamilyAssemblySha103Hotfix =
            "A38C4D993C9BF60D0DDE0EDD348F201C97574F907808417A33C8A20F4772E9C1";

        private static int _lootManualProjectionContractState;
        private static int _containerSaveEstimateContractState;
        private static int _scavengerChanceContractState;
        private static int _sourceFamilyContractState;

        private static bool IsAuditedFeatureAssembly()
        {
            if (!_compatStaticChecked) RunCompatibilityShieldStatic();
            return string.Equals(_compatAssemblySha256, AuditedFeatureAssemblySha102, StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(_compatAssemblySha256, AuditedFeatureAssemblySha103, StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsCurrent103CargoSpawnAssembly()
        {
            if (!_compatStaticChecked) RunCompatibilityShieldStatic();
            return string.Equals(_compatAssemblySha256, AuditedFeatureAssemblySha103, StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(_compatAssemblySha256, AuditedCargoSpawnAssemblySha103Hotfix, StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsCurrent103TradeAssembly()
        {
            if (!_compatStaticChecked) RunCompatibilityShieldStatic();
            return string.Equals(_compatAssemblySha256, AuditedFeatureAssemblySha103, StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(_compatAssemblySha256, AuditedTradeAssemblySha103Hotfix, StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsLegacy102FeatureAssembly()
        {
            if (!_compatStaticChecked) RunCompatibilityShieldStatic();
            return string.Equals(_compatAssemblySha256, AuditedFeatureAssemblySha102, StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsCurrentLootModifiersAssembly()
        {
            if (!_compatStaticChecked) RunCompatibilityShieldStatic();
            return IsAuditedFeatureAssembly() ||
                   string.Equals(_compatAssemblySha256, AuditedLootModifiersAssemblySha103Hotfix, StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsCurrentContainerSaveEstimateAssembly()
        {
            if (!_compatStaticChecked) RunCompatibilityShieldStatic();
            return IsAuditedFeatureAssembly() ||
                   string.Equals(_compatAssemblySha256, AuditedContainerSaveEstimateAssemblySha103Hotfix, StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsCurrentScavengerAssembly()
        {
            if (!_compatStaticChecked) RunCompatibilityShieldStatic();
            return IsAuditedFeatureAssembly() ||
                   string.Equals(_compatAssemblySha256, AuditedScavengerAssemblySha103Hotfix, StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsCurrentSourceFamilyAssembly()
        {
            if (!_compatStaticChecked) RunCompatibilityShieldStatic();
            return IsAuditedFeatureAssembly() ||
                   string.Equals(_compatAssemblySha256, AuditedSourceFamilyAssemblySha103Hotfix, StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsLootManualProjectionContractVerified()
        {
            if (_lootManualProjectionContractState > 0) return _compatLoot;
            if (_lootManualProjectionContractState < 0) return false;
            if (!IsCurrentLootModifiersAssembly() || !_compatLoot)
            {
                _lootManualProjectionContractState = -1;
                return false;
            }

            try
            {
                object perks = GetStaticMember(typeof(Data), "Perks");
                if (perks == null) return false;
                List<DataEntry> records = EnumerateData(perks);
                if (records == null || records.Count == 0) return false;

                HashSet<int> marauderTenths = new HashSet<int>();
                bool organization = false;
                bool fieldMedic = false;
                const double epsilon = 0.00001;

                for (int i = 0; i < records.Count; i++)
                {
                    object record = records[i] == null ? null : records[i].Value;
                    if (record == null) continue;
                    List<DataEntry> parameters = EnumerateData(GetMember(record, "Parameters"));
                    if (parameters == null || parameters.Count == 0) continue;

                    bool hasStorage = false;
                    bool hasCorpse = false;
                    bool hasImplant = false;
                    double storage = 0.0;
                    double corpse = 0.0;
                    double implant = 0.0;
                    for (int p = 0; p < parameters.Count; p++)
                    {
                        object parameter = parameters[p] == null ? null : parameters[p].Value;
                        if (parameter == null) continue;
                        string name = GetStringMember(parameter, "Name");
                        double value;
                        if (string.IsNullOrEmpty(name) ||
                            !TryToDoubleSafe(GetMember(parameter, "FloatVal"), out value) ||
                            double.IsNaN(value) || double.IsInfinity(value))
                            continue;

                        if (string.Equals(name, "FLootStorageItem", StringComparison.Ordinal))
                        {
                            hasStorage = true;
                            storage = value;
                        }
                        else if (string.Equals(name, "FLootCorpseItem", StringComparison.Ordinal))
                        {
                            hasCorpse = true;
                            corpse = value;
                        }
                        else if (string.Equals(name, "FImplantDropChance", StringComparison.Ordinal))
                        {
                            hasImplant = true;
                            implant = value;
                        }
                    }

                    if (hasStorage && hasCorpse && Math.Abs(storage - corpse) <= epsilon && storage > 0.0)
                    {
                        int tenths = (int)Math.Round(storage * 10.0);
                        if (Math.Abs(storage - tenths / 10.0) <= epsilon)
                            marauderTenths.Add(tenths);
                    }
                    if (hasCorpse && Math.Abs(corpse - 0.5) <= epsilon &&
                        (!hasStorage || Math.Abs(storage) <= epsilon))
                        organization = true;
                    if (hasImplant && Math.Abs(implant - 0.25) <= epsilon)
                        fieldMedic = true;
                }

                bool marauder = marauderTenths.Contains(3) && marauderTenths.Contains(6) &&
                    marauderTenths.Contains(9) && marauderTenths.Contains(12);
                _lootManualProjectionContractState = marauder && organization && fieldMedic ? 1 : -1;
            }
            // A transient read during vanilla data bootstrap is not evidence that the
            // audited constants changed. Definitive complete-data mismatch above still
            // caches -1, but an exception remains unresolved and can retry later.
            catch { return false; }

            return _lootManualProjectionContractState > 0 && _compatLoot;
        }

        private static bool IsContainerSaveEstimateContractVerified()
        {
            if (_containerSaveEstimateContractState > 0) return _compatLoot && _compatFactions;
            if (_containerSaveEstimateContractState < 0) return false;
            if (!IsCurrentContainerSaveEstimateAssembly() || !_compatLoot || !_compatFactions)
            {
                _containerSaveEstimateContractState = -1;
                return false;
            }

            try
            {
                // Data.ContainerItemDrop can still be null during early session bootstrap.
                // That is not a contract failure: leave state unresolved so the Loot tab
                // can retry once vanilla data has finished loading.
                object drops = GetStaticMember(typeof(Data), "ContainerItemDrop");
                if (drops == null) return false;

                Type dropType = drops.GetType();
                Type obstacleType = AccessTools.TypeByName("MGSC.ObstacleContainerRecord");
                Type itemRecordType = AccessTools.TypeByName("MGSC.ItemRecord");
                if (obstacleType == null || itemRecordType == null)
                {
                    _containerSaveEstimateContractState = -1;
                    return false;
                }

                MethodInfo getDrop = FindCompatibleMethod(dropType, "GetDrop", 2, typeof(string));
                if (getDrop == null || getDrop.GetParameters()[1].ParameterType != typeof(string) ||
                    FindCompatibleMethod(dropType, "GetDropBiomes", 1, typeof(string)) == null ||
                    FindCachedMember(obstacleType, "ManualDropId", false) == null ||
                    FindCachedMember(obstacleType, "ManualDropItemCount", false) == null ||
                    FindCachedMember(itemRecordType, "TechLevel", false) == null)
                {
                    _containerSaveEstimateContractState = -1;
                    return false;
                }

                _containerSaveEstimateContractState = 1;
            }
            catch { return false; }
            return _containerSaveEstimateContractState > 0 && _compatLoot && _compatFactions;
        }

        private static bool IsScavengerChanceContractVerified()
        {
            if (_scavengerChanceContractState > 0) return _compatFactions && _compatMagnum;
            if (_scavengerChanceContractState < 0) return false;
            _scavengerChanceContractState = -1;
            if (!IsCurrentScavengerAssembly() || !_compatFactions || !_compatMagnum) return false;

            try
            {
                Type progression = AccessTools.TypeByName("MGSC.MagnumProgression");
                Type missionSystem = AccessTools.TypeByName("MGSC.MissionSystem");
                if (progression == null || missionSystem == null) return false;
                string[] members = new string[]
                {
                    "HasPurgeBrigadeDepartment",
                    "PurgeBrigadeResourcesBonus",
                    "PurgeBrigadeArmorWeaponBonus",
                    "PurgeBrigadeFoodMedsBonus",
                    "PurgeBrigadeAmmoGrenadesBonus"
                };
                for (int i = 0; i < members.Length; i++)
                    if (FindCachedMember(progression, members[i], false) == null) return false;
                bool missionFinished = false;
                MethodInfo[] missionMethods = missionSystem.GetMethods(InstanceFlags | StaticFlags);
                for (int i = 0; i < missionMethods.Length; i++)
                    if (string.Equals(missionMethods[i].Name, "MissionFinishedByPlayer", StringComparison.Ordinal))
                    {
                        missionFinished = true;
                        break;
                    }
                if (!missionFinished) return false;
                _scavengerChanceContractState = 1;
            }
            catch { }
            return _scavengerChanceContractState > 0 && _compatFactions && _compatMagnum;
        }

        private static bool IsAuditedSourceFamilyContractVerified()
        {
            if (_sourceFamilyContractState > 0) return _compatLoot;
            if (_sourceFamilyContractState < 0) return false;
            _sourceFamilyContractState =
                IsCurrentSourceFamilyAssembly() && _compatLoot ? 1 : -1;
            return _sourceFamilyContractState > 0 && _compatLoot;
        }
    }
}
