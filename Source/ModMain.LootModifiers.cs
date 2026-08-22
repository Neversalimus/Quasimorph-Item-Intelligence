using System;
using System.Collections.Generic;
using System.Globalization;

namespace ItemIntelligence
{
    public static partial class ModMain
    {
        // v1.7.38: read-only Loot modifier simulation. Manual controls never touch the
        // mercenary, perk list, RNG, inventory, loot tables, or save state; they only
        // change how Item Intelligence projects the already-indexed Loot information.
        private static bool _lootModifierUseManual;
        private static int _lootManualMarauderLevel;
        private static bool _lootManualOrganization;
        private static bool _lootManualFieldMedic;
        private static readonly List<LootContainerSource> LootActiveContainerPresentationBuffer =
            new List<LootContainerSource>(64);
        private sealed class LootModifierSnapshot
        {
            public readonly double StorageExpected;
            public readonly double CorpseExpected;
            public readonly double ImplantAdditionalChance;
            public readonly double ImplantRecoveryChance;
            public readonly bool CurrentAvailable;

            public LootModifierSnapshot(
                double storageExpected,
                double corpseExpected,
                double implantAdditionalChance,
                double implantRecoveryChance,
                bool currentAvailable)
            {
                StorageExpected = storageExpected;
                CorpseExpected = corpseExpected;
                ImplantAdditionalChance = implantAdditionalChance;
                ImplantRecoveryChance = implantRecoveryChance;
                CurrentAvailable = currentAvailable;
            }
        }

        private static void ResetLootModifierSessionState()
        {
            _lootModifierUseManual = false;
            _lootManualMarauderLevel = 0;
            _lootManualOrganization = false;
            _lootManualFieldMedic = false;
            LootActiveContainerPresentationBuffer.Clear();
            LootEnemyRegularPresentationBuffer.Clear();
            LootEnemyCorpseBonusPresentationBuffer.Clear();
            ResetLootModifierRuntimeSessionCache();
        }

        private static LootModifierSnapshot GetLootModifierSnapshot()
        {
            if (_lootModifierUseManual)
                return BuildManualLootModifierSnapshot();

            object creatureData = ResolveCurrentLootModifierCreatureData();
            if (creatureData == null)
                return new LootModifierSnapshot(-1.0, -1.0, -1.0, ResolveImplantRecoveryChance(-1.0), false);

            double storage = GetLootPerkParameterSum(creatureData, "FLootStorageItem");
            double corpse = GetLootPerkParameterSum(creatureData, "FLootCorpseItem");
            double implant = GetCurrentAdditionalImplantDropChance(creatureData);
            bool available = storage >= 0.0 && corpse >= 0.0;
            return new LootModifierSnapshot(
                storage,
                corpse,
                implant,
                ResolveImplantRecoveryChance(implant),
                available);
        }

        private static LootModifierSnapshot BuildManualLootModifierSnapshot()
        {
            double marauder = GetManualMarauderExpectedBonus(_lootManualMarauderLevel);
            double corpse = marauder + (_lootManualOrganization ? 0.5 : 0.0);
            double implant = _lootManualFieldMedic ? 0.25 : 0.0;
            return new LootModifierSnapshot(
                marauder,
                corpse,
                implant,
                ResolveImplantRecoveryChance(implant),
                true);
        }

        private static double GetManualMarauderExpectedBonus(int level)
        {
            switch (level)
            {
                case 1: return 0.3;
                case 2: return 0.6;
                case 3: return 0.9;
                case 4: return 1.2;
                default: return 0.0;
            }
        }

        private static void AppendLootModifierControlLines(LootModifierSnapshot snapshot)
        {
            if (snapshot == null) return;

            bool manualProjectionVerified = IsLootManualProjectionContractVerified();
            if (_lootModifierUseManual || manualProjectionVerified)
            {
                BrowserLines.Add(BrowserLine.InternalAction(
                    _lootModifierUseManual ? Ui("ui.loot_modifiers_manual") : Ui("ui.loot_modifiers_current"),
                    FormatLootModifierSummary(snapshot),
                    BrowserAction.LootModifier(BrowserLootModifierCommand.ToggleMode)));
            }
            else
            {
                // Manual values are an audited simulator, not live perk reads. Keep CURRENT
                // read-only values but disable the projection unless its feature-owned
                // assembly + Data.Perks parameter contract validates exactly.
                BrowserLines.Add(BrowserLine.Accent(
                    Ui("ui.loot_modifiers_current"),
                    FormatLootModifierSummary(snapshot)));
            }

            if (!_lootModifierUseManual) return;

            BrowserLines.Add(BrowserLine.InternalAction(
                Ui("ui.loot_marauder"),
                FormatManualMarauderState(),
                BrowserAction.LootModifier(BrowserLootModifierCommand.CycleMarauder)));
            BrowserLines.Add(BrowserLine.InternalAction(
                Ui("ui.loot_marika_organization"),
                _lootManualOrganization
                    ? Ui("ui.loot_on") + "  +0.5 " + (IsRussian() ? "Т" : "B")
                    : Ui("ui.loot_off"),
                BrowserAction.LootModifier(BrowserLootModifierCommand.ToggleOrganization)));
            BrowserLines.Add(BrowserLine.InternalAction(
                Ui("ui.loot_laksha_field_medic"),
                _lootManualFieldMedic
                    ? Ui("ui.loot_on") + "  +25 " + Ui("ui.loot_pp")
                    : Ui("ui.loot_off"),
                BrowserAction.LootModifier(BrowserLootModifierCommand.ToggleFieldMedic)));
        }

        private static string FormatLootModifierSummary(LootModifierSnapshot snapshot)
        {
            bool ru = IsRussian();
            return (ru ? "К " : "C ") + FormatExpectedBonusCompact(snapshot.StorageExpected, ru) +
                "   " + (ru ? "Т " : "B ") + FormatExpectedBonusCompact(snapshot.CorpseExpected, ru) +
                "   " + (ru ? "И " : "I ") + FormatPercentPointBonusCompact(snapshot.ImplantAdditionalChance, ru);
        }

        private static string FormatManualMarauderState()
        {
            if (_lootManualMarauderLevel <= 0) return Ui("ui.loot_off");
            string roman;
            switch (_lootManualMarauderLevel)
            {
                case 1: roman = "I"; break;
                case 2: roman = "II"; break;
                case 3: roman = "III"; break;
                default: roman = "IV"; break;
            }
            return roman + "  +" + FormatExpectedNumber(GetManualMarauderExpectedBonus(_lootManualMarauderLevel), IsRussian()) +
                " " + (IsRussian() ? "К/Т" : "C/B");
        }

        private static string FormatExpectedBonusCompact(double value, bool ru)
        {
            if (value < 0.0 || double.IsNaN(value) || double.IsInfinity(value)) return "?";
            return "+" + FormatExpectedNumber(value, ru);
        }

        private static string FormatPercentPointBonusCompact(double value, bool ru)
        {
            if (value < 0.0 || double.IsNaN(value) || double.IsInfinity(value)) return "?";
            return "+" + Math.Round(value * 100.0).ToString("0", CultureInfo.InvariantCulture) + "%";
        }

        private static string FormatExpectedNumber(double value, bool ru)
        {
            if (double.IsNaN(value) || double.IsInfinity(value)) return "?";
            string text = value.ToString("0.##", CultureInfo.InvariantCulture);
            return ru ? text.Replace('.', ',') : text;
        }

        private static void HandleLootModifierAction(BrowserLootModifierCommand command)
        {
            if (command == BrowserLootModifierCommand.ToggleMode)
            {
                if (!_lootModifierUseManual && !IsLootManualProjectionContractVerified())
                    return;
                _lootModifierUseManual = !_lootModifierUseManual;
            }
            else if (command == BrowserLootModifierCommand.CycleMarauder)
                _lootManualMarauderLevel = (_lootManualMarauderLevel + 1) % 5;
            else if (command == BrowserLootModifierCommand.ToggleOrganization)
                _lootManualOrganization = !_lootManualOrganization;
            else if (command == BrowserLootModifierCommand.ToggleFieldMedic)
                _lootManualFieldMedic = !_lootManualFieldMedic;
            else
                return;

            UnityEngine.Debug.Log(
                "[ItemIntelligence][LootModifiers] mode=" + (_lootModifierUseManual ? "MANUAL" : "CURRENT") +
                ", marauder=" + _lootManualMarauderLevel.ToString(CultureInfo.InvariantCulture) +
                ", organization=" + _lootManualOrganization.ToString() +
                ", fieldMedic=" + _lootManualFieldMedic.ToString() + ".");

            BrowserNavigation.ScrollOffset = 0;
            if (_inspectorOpen && !string.IsNullOrEmpty(_inspectorItemId))
            {
                if (ModderMode)
                {
                    long renderStarted = System.Diagnostics.Stopwatch.GetTimestamp();
                    RenderBrowser(_inspectorItemId);
                    double renderMs =
                        (System.Diagnostics.Stopwatch.GetTimestamp() - renderStarted) * 1000.0 /
                        System.Diagnostics.Stopwatch.Frequency;
                    UnityEngine.Debug.Log(
                        "[ItemIntelligence][LootModifiers][Perf] render=" +
                        renderMs.ToString("0.0", CultureInfo.InvariantCulture) + "ms, rows=" +
                        BrowserLines.Count.ToString(CultureInfo.InvariantCulture) + ".");
                }
                else
                {
                    RenderBrowser(_inspectorItemId);
                }
            }
        }

        private static List<LootContainerSource> FilterActiveLootContainerSources(
            List<LootContainerSource> sources,
            double storageExpected)
        {
            LootActiveContainerPresentationBuffer.Clear();
            if (sources == null || sources.Count == 0) return null;
            for (int i = 0; i < sources.Count; i++)
            {
                LootContainerSource source = sources[i];
                if (source == null) continue;
                // A zero-base manual profile is not an ordinary source. It becomes
                // relevant only when a resolved Marauder/storage bonus adds rolls.
                if (!source.RollRangeResolved || source.MaxRolls > 0 || storageExpected > 0.0)
                    LootActiveContainerPresentationBuffer.Add(source);
            }
            return LootActiveContainerPresentationBuffer.Count == 0
                ? null
                : LootActiveContainerPresentationBuffer;
        }

        private static string FormatLootContainerRolls(
            LootContainerSource source,
            double storageExpected)
        {
            if (source == null) return "-";
            if (!source.RollRangeResolved)
            {
                if (storageExpected > 0.0)
                    return "? +" + FormatExpectedNumber(storageExpected, IsRussian());
                return "?";
            }

            string baseRolls = source.MinRolls == source.MaxRolls
                ? source.MaxRolls.ToString(CultureInfo.InvariantCulture)
                : source.MinRolls.ToString(CultureInfo.InvariantCulture) +
                  "-" + source.MaxRolls.ToString(CultureInfo.InvariantCulture);

            if (storageExpected > 0.0)
            {
                string bonus = "+" + FormatExpectedNumber(storageExpected, IsRussian());
                return baseRolls + " " + bonus;
            }
            return baseRolls;
        }

        private static string GetEnemyLootResultLabelWithModifiers(
            LootEnemySource source,
            LootModifierSnapshot snapshot,
            bool ru)
        {
            if (source == null) return string.Empty;
            bool implant =
                string.Equals(source.Kind, "GrantedImplant", StringComparison.Ordinal) ||
                string.Equals(source.Kind, "RandomImplant", StringComparison.Ordinal);
            if (implant && snapshot != null && snapshot.ImplantRecoveryChance >= 0.0)
            {
                return Ui("ui.amputate") + " " +
                    FormatLootPercent((float)(snapshot.ImplantRecoveryChance * 100.0));
            }
            return GetEnemyLootResultLabel(source, ru);
        }
    }
}
